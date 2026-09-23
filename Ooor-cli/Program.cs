using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OoorFunc.Core;

namespace Ooor_cli
{
    /// <summary>
    /// Ooor-cli：连接本地 llama-server（OpenAI 兼容接口）的终端 AI 助手。
    ///
    ///   - 服务地址解析顺序：--base 参数 → ooor 的 run_state.conf（主界面点「启动服务」后写入）→ 127.0.0.1:6080
    ///   - 工具默认全开（读/写/删/执行/联网），高危操作每次在终端 Y/N 确认；--trust 允许模型自行判断跳过
    ///   - Ctrl+C：生成中=中断本次回答回到提示符（不退出窗口）；输入时第一次提示、再按一次退出
    ///   - 斜杠命令：/help /tools /roots /clear /exit
    /// </summary>
    internal static class Program
    {

        // todo 控制台输入 @符号，开始引用文件，引用的文件自动加为白名单
        // /file /talk @

        private static LocalModelAgent _agent;
        private static AgentOptions _opt;
        private static StreamRenderer _renderer;

        /// <summary>当前回合的取消源（Ctrl+C 触发取消；等待输入时为 null）</summary>
        private static volatile CancellationTokenSource _turnCts;

        /// <summary>当前回合的统计（每轮 RunTurn 起始 new、finally 清空；OnStep 在线程池回调里修改，Render 在主线程读）</summary>
        private static TurnStats _turnStats;

        /// <summary>当前持久化的聊天会话（首轮成功后自动创建；/clear 后置空重新开一个）。null=尚未落库的新会话</summary>
        private static ChatRecord _session;

        /// <summary>当前服务实际加载的模型 id（ProbeModel 探测结果，落库到 ChatRecord.Model）</summary>
        private static string _modelId = "";

        /// <summary>当前使用的 Agent 名称（/agent 切换或 --agent 启动时记录；null=内置默认人设）。/new-console 据此开新窗口</summary>
        private static string _currentAgentName;

        /// <summary>是否为 --trust 开放权限模式（/new-console 新窗口保持一致）</summary>
        private static bool _trust;

        private static int Main(string[] argv)
        {
            CliUi.Init();
            CliLang.Init();
            try { Console.Title = "Ooor-cli"; } catch { }

            // ===================== 参数 =====================
            string argBase = null;
            bool trust = false;
            bool pause = false;   // --pause：启动失败时停窗显示原因（OOOR 主界面菜单拉起时使用）
            string argSession = null;  // --session <id>：从 LiteDB 恢复指定会话接着聊
            string argModel = null;    // --model <path>：期望模型（记录用；实际以服务已加载模型为准）
            string argAgent = null;    // --agent <name>：以指定 Agent 的系统提示词开始一个空白新对话
            var extraRoots = new List<string>();
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                if (a == "--help" || a == "-h" || a == "/?") { PrintUsage(); return 0; }
                if (a == "--trust" || a == "-y") { trust = true; continue; }
                if (a == "--pause") { pause = true; continue; }
                if (a == "--base" && i + 1 < argv.Length) { argBase = argv[++i]; continue; }
                if (a == "--root" && i + 1 < argv.Length) { extraRoots.Add(argv[++i]); continue; }
                if (a == "--session" && i + 1 < argv.Length) { argSession = argv[++i]; continue; }
                if (a == "--model" && i + 1 < argv.Length) { argModel = argv[++i]; continue; }
                if (a == "--agent" && i + 1 < argv.Length) { argAgent = argv[++i]; continue; }
                CliUi.Error(CliLang.Tf("unknownArg", a));
                PrintUsage();
                return 1;
            }

            // ===================== 宿主身份 =====================
            // CoreEnv 默认按 Ooor-cli.exe 位置推导（与 ooor 主程序同目录时约定一致）
            CoreEnv.AppName = CliLang.IsEnglish ? "Ooor-cli (command-line assistant)" : "Ooor-cli（ooor 命令行助手）";
            CoreEnv.AppDescription = CliLang.IsEnglish
                ? "ooor command-line AI assistant: terminal Agent connecting to local llama-server (Ooor-cli.exe)."
                : "ooor 的命令行 AI 助手：连接本地 llama-server 的终端 Agent（Ooor-cli.exe）。";
            try { CoreEnv.AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3); }
            catch { CoreEnv.AppVersion = ""; }

            // ===================== 连接 llama-server =====================
            string baseUrl = ResolveBaseUrl(argBase);
            string modelId;
            string probeErr;
            modelId = ProbeModel(baseUrl, out probeErr);
            if (modelId == null)
            {
                CliUi.Error(CliLang.Tf("connectFail", baseUrl, probeErr));
                CliUi.Info(CliLang.T("connectHint"));
                if (pause)
                {
                    CliUi.Info(CliLang.T("pressAnyKey"));
                    try { Console.ReadKey(true); } catch { }
                }
                return 1;
            }
            _modelId = modelId ?? "";

            // ===================== Agent 配置（工具全开 + 终端 Y/N 把关） =====================
            _opt = AgentOptions.Default();                 // 白名单：models / config + agent.conf 追加
            _opt.AllowWrite = true;
            _opt.AllowCommand = true;
            _opt.AllowInternet = true;
            _opt.TrustAiJudgment = trust;                  // 默认 false：高危操作一律 Y/N 确认

            // 高危操作确认语义按 --trust 区分：开放权限下无需等待用户确认，直接执行；
            // 限制权限下维持 Y/N 弹窗的旧措辞，避免动了旧入口的行为预期。
            string confirmHint = trust
                ? CliLang.T("sys.confirmTrust")
                : CliLang.T("sys.confirmNormal");

            _opt.SystemPrompt = string.Format(CliLang.T("sys.prompt"), confirmHint);

            // 当前目录进白名单（CLI 的自然工作目录）
            AddRoot(_opt, Environment.CurrentDirectory);
            foreach (string r in extraRoots) AddRoot(_opt, r);

            _agent = new LocalModelAgent(baseUrl, _opt, CliUi.Confirm);
            _agent.Step += OnStep;

            // ===================== 恢复历史会话（--session） =====================
            int restoredMsgs = 0;
            if (!string.IsNullOrEmpty(argSession))
            {
                try
                {
                    _session = ChatStore.Get(argSession.Trim());
                    if (_session != null)
                    {
                        _agent.ImportHistoryJson(_session.Messages);
                        restoredMsgs = _agent.HistoryCount;
                        if (!string.IsNullOrEmpty(_session.Agent)) _currentAgentName = _session.Agent;
                        // 换个模型接着聊：以服务当前加载的模型为准，仅更新记录里的模型名
                        if (!string.IsNullOrEmpty(argModel)) _session.Model = argModel;
                        else if (!string.IsNullOrEmpty(modelId)) _session.Model = modelId;
                    }
                    else CliUi.Info(CliLang.Tf("sessionNotFound", argSession));
                }
                catch (Exception ex) { CliUi.Info(CliLang.Tf("sessionRestoreFail", ex.Message)); _session = null; }
            }

            // ===================== Agent 人设（--agent，空白新窗口用） =====================
            _trust = trust;
            if (!string.IsNullOrEmpty(argAgent))
            {
                var startAgent = AgentCatalog.GetByName(argAgent.Trim());
                if (startAgent != null)
                {
                    _agent.ReplaceSystemPrompt(startAgent.SystemPrompt ?? "");
                    _currentAgentName = startAgent.Name;
                }
                else CliUi.Info(CliLang.Tf("agentNotFound", argAgent));
            }

            // ===================== 横幅 =====================
            Console.WriteLine();
            CliUi.WriteLine(CliUi.Bold, CliLang.Tf("banner", CoreEnv.AppVersion));
            CliUi.Info(CliLang.Tf("svcModel", baseUrl, modelId.Length > 0 ? modelId : CliLang.T("autoModel")));
            if (!string.IsNullOrEmpty(_currentAgentName))
                CliUi.Info(CliLang.Tf("agent", _currentAgentName));
            CliUi.Info(CliLang.Tf("tools", _agent.ToolNames.Length, string.Join("  ", _agent.ToolNames)));
            if (_session != null)
                CliUi.Info(CliLang.Tf("sessionLoaded", _session.Title ?? CliLang.Tf("unnamed"), restoredMsgs));
            CliUi.Info(CliLang.T("welcome"));
            Console.WriteLine();

            // 恢复会话：在终端完整回放历史（系统提示词 / 提问 / 回复 / 工具调用与结果）
            if (_session != null) ReplayHistory();

            // ===================== Ctrl+C =====================
            // 生成中（_turnCts != null）：取消当前回合，窗口不退；
            // 等待输入时 Ctrl+C 由 ReadInputLine 用 TreatControlCAsInput 自行处理（双击退出），不会进这里
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;                       // 不让运行时直接杀进程，行为自己控制
                CancellationTokenSource cts = _turnCts;
                if (cts != null) { try { cts.Cancel(); } catch { } }
            };

            // ===================== REPL =====================
            while (true)
            {
                CliUi.Prompt();
                string line = SlashInput.ReadLine(_opt);
                if (line == null) break;
                line = line.Trim();
                if (line.Length == 0) continue;

                if (line.StartsWith("/", StringComparison.Ordinal))
                {
                    if (!HandleCommand(line)) break;
                    continue;
                }

                RunTurn(line);
            }

            try { _agent.Dispose(); } catch { }
            CliUi.Info(CliLang.T("bye"));
            return 0;
        }

        // ===================== 一轮对话 =====================

        private static void RunTurn(string input)
        {
            Console.WriteLine();
            var cts = new CancellationTokenSource();
            _turnCts = cts;
            _renderer = new StreamRenderer();
            _turnStats = new TurnStats();              // 每轮统计从此刻起算
            Spinner.Start(CliLang.T("thinking"));                     // 回车后到首个增量之间的行内 loading
            try
            {
                AgentResult res = _agent.SendAsync(input, cts.Token).GetAwaiter().GetResult();
                _renderer.Flush();
                Console.WriteLine();
                // 成功路径：所有通道在 OnStep 的终态步已 EndXxx 收尾；FlushActive 仅兜底（理论上无活可做）
                _turnStats?.FlushActive();
                if (res != null && res.Steps >= _opt.MaxSteps)
                    CliUi.Info(CliLang.Tf("maxSteps", _opt.MaxSteps));
                PrintTurnArtifacts(res);  // 本轮写入的文件 + 执行/运行的文件
                PersistSession(input);   // 成功一轮后落库（新会话首轮自动建档）
            }
            catch (OperationCanceledException)
            {
                Spinner.Stop();                         // Ctrl+C 中断时 OnStep 不会再有输出，这里手动收掉动画
                _renderer.Flush();
                Console.WriteLine();
                CliUi.Info(CliLang.T("interrupted"));
                _turnStats?.FlushActive();
            }
            catch (Exception ex)
            {
                Spinner.Stop();
                _renderer.Flush();
                Console.WriteLine();
                CliUi.Error(CliLang.Tf("error", ex.Message));
                CliUi.Info(CliLang.T("svcStopped"));
                _turnStats?.FlushActive();
            }
            finally
            {
                Spinner.Stop();                         // 兜底：任何路径动画线程都必须收掉
                _turnCts = null;
                _renderer = null;
                _turnStats = null;
            }
        }

        // ===================== 会话持久化 =====================

        /// <summary>
        /// 本轮对话结束后的文件动作小结：先打印模型写入的文件，再打印执行/运行的文件。
        /// 两类都没有则不输出任何内容（保持纯问答轮的简洁）。
        /// </summary>
        private static void PrintTurnArtifacts(AgentResult res)
        {
            if (res == null) return;
            if (res.WrittenFiles.Count == 0 && res.ExecutedFiles.Count == 0) return;

            Console.WriteLine();
            if (res.WrittenFiles.Count > 0)
            {
                CliUi.WriteLine(CliUi.Bold, CliLang.Tf("writtenFiles", res.WrittenFiles.Count));
                foreach (string f in res.WrittenFiles) CliUi.Info("  • " + ToFileUri(f));
            }
            if (res.ExecutedFiles.Count > 0)
            {
                if (res.WrittenFiles.Count > 0) Console.WriteLine();
                CliUi.WriteLine(CliUi.Bold, CliLang.Tf("executedFiles", res.ExecutedFiles.Count));
                foreach (string f in res.ExecutedFiles) CliUi.Info("  • " + ToFileUri(f));
            }
        }

        /// <summary>
        /// 把本地路径转成可点击的 file URI：
        /// 前缀 file:/// + 反斜杠统一为正斜杠，如 C:\dir\a.py → file:///C:/dir/a.py
        /// </summary>
        private static string ToFileUri(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return "file:///" + path.Replace('\\', '/');
        }

        /// <summary>
        /// 每轮成功结束后把当前对话写入 LiteDB：
        ///   - _session 为空 = 这是本窗口（或 /clear 后）的首轮：新建记录，标题取首条用户输入前 30 字；
        ///   - 否则更新同一记录的消息历史 / 模型 / 最近速度 / 更新时间。
        /// 异常不影响对话本身，仅在终端给一行灰色提示。
        /// </summary>
        private static void PersistSession(string userInput)
        {
            try
            {
                string json = _agent.ExportHistoryJson();
                if (_session == null)
                {
                    string title = (userInput ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
                    if (title.Length > 30) title = title.Substring(0, 30) + "…";
                    if (title.Length == 0) title = CliLang.Tf("newChat", DateTime.Now.ToString("MM-dd HH:mm"));
                    _session = new ChatRecord
                    {
                        Title = title,
                        Model = _modelId ?? "",
                        Agent = _currentAgentName ?? "",
                        Messages = json
                    };
                    ChatStore.Add(_session);
                }
                else
                {
                    _session.Messages = json;
                    if (!string.IsNullOrEmpty(_modelId)) _session.Model = _modelId;
                    _session.Agent = _currentAgentName ?? "";
                }

                // 速度：仅当本轮确实产生了对应通道文本才覆盖，避免工具调用轮把上轮速度清空
                var st = _turnStats;
                if (st != null)
                {
                    if (!string.IsNullOrEmpty(st.ReasonSpeedText)) _session.LastReasoningSpeed = st.ReasonSpeedText;
                    if (!string.IsNullOrEmpty(st.ContentSpeedText)) _session.LastContentSpeed = st.ContentSpeedText;
                }
                ChatStore.Update(_session);
            }
            catch (Exception ex)
            {
                CliUi.Info(CliLang.Tf("saveFail", ex.Message));
            }
        }

        /// <summary>
        /// 恢复会话后在终端完整回放历史，顺序与会话发生时一致：
        ///   system  → 灰色完整打印系统提示词
        ///   user    → 绿色 "Ooor> " + 提问原文
        ///   assistant → 回复正文（工具调用先登记参数，待对应 tool 结果一起展示）
        ///   tool    → 复用实时执行的 ToolLine：黄色调用行（函数名+参数）+ 灰色缩进结果
        /// </summary>
        private static void ReplayHistory()
        {
            AgentMessage[] msgs;
            try { msgs = _agent.SnapshotHistory(); }
            catch { return; }
            if (msgs == null || msgs.Length == 0) return;

            int interact = 0;
            foreach (var m in msgs) if (m.Role != AgentRole.System) interact++;

            CliUi.WriteLine(CliUi.Bold, CliLang.Tf("replayHeader", interact));

            // tool_call_id → 该次调用的参数 JSON：assistant 消息只登记，tool 结果到达时合并成一行展示
            var callArgs = new Dictionary<string, string>();

            foreach (var m in msgs)
            {
                switch (m.Role)
                {
                    case AgentRole.System:
                        Console.WriteLine();
                        CliUi.WriteLine(CliUi.Gray, CliLang.T("systemPrompt"));
                        CliUi.WriteLine(CliUi.Gray, (m.Content ?? "").TrimEnd());
                        break;

                    case AgentRole.User:
                        Console.WriteLine();
                        CliUi.WriteLine(CliUi.Green, "Ooor> " + (m.Content ?? ""));
                        break;

                    case AgentRole.Assistant:
                        if (m.ToolCalls != null)
                        {
                            foreach (var t in m.ToolCalls)
                                if (t != null && !string.IsNullOrEmpty(t.Id))
                                    callArgs[t.Id] = t.ArgumentsJson ?? "";
                        }
                        if (!string.IsNullOrEmpty(m.Content))
                        {
                            Console.WriteLine();
                            Console.WriteLine(m.Content.TrimEnd());
                        }
                        break;

                    case AgentRole.Tool:
                        string args = null;
                        if (!string.IsNullOrEmpty(m.ToolCallId)) callArgs.TryGetValue(m.ToolCallId, out args);
                        Console.WriteLine();
                        CliUi.ToolLine(m.ToolName ?? "(tool)", args ?? "", m.Content ?? "", true);
                        break;
                }
            }

            CliUi.WriteLine(CliUi.Bold, CliLang.T("replayEnd"));
            Console.WriteLine();
        }

        // ===================== 单回合统计 =====================

        /// <summary>
        /// 一轮对话的运行统计：按 Delta.Length 累加字符数（非精确 token，仅作速率参考），
        /// 用 Stopwatch.GetTimestamp 记阶段起止。On* 由线程池回调写入、End* 在主线程就地打印一行用时统计。
        /// 通道状态机：0=空闲 → 1=思考 → 2=正文 → 0；在 OnStep 的阶段切换点收尾各通道，统计行紧随对应文本下方。
        /// </summary>
        private sealed class TurnStats
        {
            private const int PhaseNone = 0, PhaseReason = 1, PhaseContent = 2;
            private int _phase;                                       // 同一回合内 OnStep 串行触发，单线程访问，无需 volatile

            private long _reasonChars, _contentChars;
            private long _reasonFirst = long.MinValue, _reasonLast;
            private long _contentFirst = long.MinValue, _contentLast;

            /// <summary>最近一次思考速度展示文本（如 "12.3 字符/s (≈4.1 tok/s 估算)"），无则空</summary>
            public string ReasonSpeedText { get; private set; } = "";
            /// <summary>最近一次正文速度展示文本</summary>
            public string ContentSpeedText { get; private set; } = "";

            public void OnReason(string d)
            {
                if (string.IsNullOrEmpty(d)) return;
                Interlocked.Add(ref _reasonChars, d.Length);
                long t = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _reasonLast, t);
                Interlocked.CompareExchange(ref _reasonFirst, t, long.MinValue);   // 仅首次交换成功 → 真正的首字时刻
            }

            public void OnContent(string d)
            {
                if (string.IsNullOrEmpty(d)) return;
                Interlocked.Add(ref _contentChars, d.Length);
                long t = Stopwatch.GetTimestamp();
                Interlocked.Exchange(ref _contentLast, t);
                Interlocked.CompareExchange(ref _contentFirst, t, long.MinValue);
            }

            /// <summary>思考阶段开始（首个 Reasoning delta 到达前调用）。无文本不打印。</summary>
            public void BeginReason() { if (_phase == PhaseNone) _phase = PhaseReason; }

            /// <summary>供 OnStep 在切换通道前查询当前活跃通道（0=空闲 / 1=思考 / 2=正文）。</summary>
            public int ActivePhase => _phase;

            /// <summary>正文阶段开始（首个 Content delta 到达前调用）；若上一通道是思考则同时收掉思考。</summary>
            public void BeginContent()
            {
                if (_phase == PhaseReason) { PrintEndReason(); _phase = PhaseNone; }
                if (_phase == PhaseNone) _phase = PhaseContent;
            }

            /// <summary>思考阶段结束（终态步、或“思考后紧跟 Tool 事件但正文未出现”的异常时序）。</summary>
            public void EndReason()
            {
                if (_phase == PhaseReason) { PrintEndReason(); _phase = PhaseNone; }
            }

            /// <summary>正文阶段结束（终态步、Tool 事件等“本段内容已交出”的信号点）。</summary>
            public void EndContent()
            {
                if (_phase == PhaseContent) { PrintEndContent(); _phase = PhaseNone; }
            }

            /// <summary>Ctrl+C / 异常提前退出时，若仍有未收尾的活跃通道，按当前时刻补打一行（结束时间取 now）。</summary>
            public void FlushActive()
            {
                if (_phase == PhaseReason) PrintEndReason();
                else if (_phase == PhaseContent) PrintEndContent();
                _phase = PhaseNone;
            }

            // 字符 → token 粗估：英文 ~4 字符/token、中文 ~1.5 字符/token；混合取 3（标“估算”避免误导，未接真实分词器）
            private static double ToTok(double cps) => cps / 3.0;

            private void PrintEndReason()
            {
                // 只要思考通道进入了（_phase == PhaseReason），就一定打用时行——
                // 即使 rc == 0（比如流刚启动就被中断、或模型发了空 delta），用户也得看到一行反馈。
                long rc = Interlocked.Read(ref _reasonChars);
                long nowTick = Stopwatch.GetTimestamp();
                long rf = Interlocked.Read(ref _reasonFirst), rl = Interlocked.Read(ref _reasonLast);
                double sec = rf == long.MinValue ? 0 : Math.Max(0.001, ((rl == 0 ? nowTick : rl) - rf) / (double)Stopwatch.Frequency);
                if (rc == 0)
                {
                    CliUi.WriteLine(CliUi.Gray, CliLang.Tf("reasonTime", sec.ToString("0.0"), "0"));
                    return;
                }
                double rate = rc / sec;
                ReasonSpeedText = rate.ToString("0.0") + CliLang.T("chars") + "/s (≈" + ToTok(rate).ToString("0.0") + " tok/s 估算)";
                CliUi.WriteLine(CliUi.Gray, "\r\n" + CliLang.Tf("reasonRate", sec.ToString("0.0"), rc.ToString(),
                    rate.ToString("0.0"), ToTok(rate).ToString("0.0")) + "\r\n\r\n");
            }

            private void PrintEndContent()
            {

                // 同上：只要进入了正文通道就一定打（即便没收到文本）
                long cc = Interlocked.Read(ref _contentChars);
                long nowTick = Stopwatch.GetTimestamp();
                long cf = Interlocked.Read(ref _contentFirst), cl = Interlocked.Read(ref _contentLast);
                double sec = cf == long.MinValue ? 0 : Math.Max(0.001, ((cl == 0 ? nowTick : cl) - cf) / (double)Stopwatch.Frequency);
                if (cc == 0)
                {
                    CliUi.WriteLine(CliUi.Gray, CliLang.Tf("contentTime", sec.ToString("0.0"), "0"));
                    return;
                }
                double rate = cc / sec;
                ContentSpeedText = rate.ToString("0.0") + CliLang.T("chars") + "/s (≈" + ToTok(rate).ToString("0.0") + " tok/s 估算)";
                CliUi.WriteLine(CliUi.Gray, "\r\n" + CliLang.Tf("contentRate", sec.ToString("0.0"), cc.ToString(),
                    rate.ToString("0.0"), ToTok(rate).ToString("0.0")) + "\r\n");
            }
        }

        // ===================== 行输入（双击 Ctrl+C 退出） =====================
        // 逐键输入 + 斜杠命令弹出菜单的实现见 SlashInput.cs。

        /// <summary>LocalModelAgent 的单步/流式增量事件（回调在线程池线程上）。</summary>
        private static void OnStep(object sender, AgentStep st)
        {
            StreamRenderer r = _renderer;
            if (r == null || st == null) return;
            Spinner.Stop();          // 首个增量到达：结束 loading（幂等，后续回调直接返回）；Stop 内部先 Join 再清行，之后本回调的输出从行首开始
            TurnStats s = _turnStats;
            try
            {
                if (st.Stream == AgentStreamKind.Reasoning)
                {
                    // 思考流到达 → 进入思考通道；若上一通道是 content（异常时序）则先收掉
                    if (s != null)
                    {
                        if (s.ActivePhase == 2) { s.EndContent(); s.BeginReason(); }
                        else if (s.ActivePhase == 0) s.BeginReason();
                    }
                    r.Feed(true, st.Delta);
                    s?.OnReason(st.Delta);
                }
                else if (st.Stream == AgentStreamKind.Content)
                {

                    // 正文流到达 → 进入正文通道；若上一通道是思考则就地收尾思考（在思考文本下方打用时行）
                    if (s != null)
                    {
                        if (s.ActivePhase == 1) { r.BreakLine(); s.EndReason(); s.BeginContent(); }
                        else if (s.ActivePhase == 0) s.BeginContent();
                    }
                    r.Feed(false, st.Delta);
                    s?.OnContent(st.Delta);
                }
                else if (st.Role == AgentRole.Tool)
                {
                    // Tool 事件：上一段正文若未主动收尾，这里视为已结束（模型常以工具调用收尾正文流）
                    if (s != null && s.ActivePhase == 2) { r.BreakLine(); s.EndContent(); }
                    r.BreakLine();
              
                    CliUi.ToolLine(st.ToolName, st.ToolArgs, st.ToolResult, st.ToolOk);

                    r.BreakLine();
                    r.BreakLine();

                }
                else if (st.Role == AgentRole.Assistant)
                {
                    // 终态步：先 BreakLine 结束未完行（样式重打 + 换行），再收掉仍活跃的通道（用时行紧随文本下方）
                    r.BreakLine();
           
                    if (s != null)
                    {
                        if (s.ActivePhase == 1) s.EndReason();
                        else if (s.ActivePhase == 2) s.EndContent();
                    }
                }
            }
            catch { /* 渲染异常不影响 Agent 循环 */ }
        }

        // ===================== 斜杠命令 =====================

        /// <summary>处理 / 命令；返回 false 表示退出 REPL。</summary>
        private static bool HandleCommand(string line)
        {
            // 拆出命令名与参数（参数保留原始大小写，如 Agent 名 / 文件路径）
            string cmd;
            string args;
            int sp = line.IndexOf(' ');
            if (sp < 0) { cmd = line.ToLowerInvariant(); args = ""; }
            else
            {
                cmd = line.Substring(0, sp).ToLowerInvariant();
                args = line.Substring(sp + 1).Trim();
            }

            switch (cmd)
            {
                case "/exit":
                case "/quit":
                case "/q":
                    return false;

                case "/clear":
                    _agent.ResetHistory();
                    _session = null;   // 之后首轮按新会话建档，不再写回旧记录
                    CliUi.Info(CliLang.T("cleared"));
                    return true;

                case "/agent":
                    return CmdAgent(args);

                case "/new-console":
                    return CmdNewConsole();

                case "/talk":
                    return CmdTalk(args);

                case "/file":
                    return CmdFile(args);

                case "/tools":
                    CliUi.WriteLine(CliUi.Bold, CliLang.Tf("toolsHeader", _agent.ToolNames.Length));
                    CliUi.Info(string.Join("  ", _agent.ToolNames));
                    return true;

                case "/roots":
                    CliUi.WriteLine(CliUi.Bold, CliLang.T("rootsHeader"));
                    foreach (string r in _opt.AllowedRoots) CliUi.Info("  " + r);
                    CliUi.Info(CliLang.Tf("tempDir", _opt.TempDir));
                    return true;

                case "/help":
                    CliUi.WriteLine(CliUi.Bold, CliLang.T("helpHeader"));
                    CliUi.Info(CliLang.T("helpAgent"));
                    CliUi.Info(CliLang.T("helpNewConsole"));
                    CliUi.Info(CliLang.T("helpTalk"));
                    CliUi.Info(CliLang.T("helpFile"));
                    CliUi.Info(CliLang.T("helpTools"));
                    CliUi.Info(CliLang.T("helpRoots"));
                    CliUi.Info(CliLang.T("helpHelp"));
                    CliUi.Info(CliLang.T("helpClear"));
                    CliUi.Info(CliLang.T("helpExit"));
                    CliUi.Info(CliLang.T("helpArgs"));
                    return true;

                default:
                    CliUi.Error(CliLang.Tf("unknownCmd", line));
                    return true;
            }
        }

        // ---------- /agent：切换 Agent 人设 ----------
        private static bool CmdAgent(string args)
        {
            var agents = AgentCatalog.All();
            if (agents.Count == 0)
            {
                CliUi.Info(CliLang.T("noAgent"));
                return true;
            }
            if (string.IsNullOrEmpty(args))
            {
                CliUi.WriteLine(CliUi.Bold, CliLang.T("agentList"));
                for (int i = 0; i < agents.Count; i++)
                    CliUi.Info("  " + (i + 1) + ". " + agents[i].Name +
                        (string.IsNullOrEmpty(agents[i].DefaultModel) ? "" : "    " + agents[i].DefaultModel));
                return true;
            }

            var a = AgentCatalog.GetByName(args);
            if (a == null)
            {
                CliUi.Error(CliLang.Tf("agentNotFound2", args));
                return true;
            }
            _agent.ReplaceSystemPrompt(a.SystemPrompt ?? "");
            _currentAgentName = a.Name;
            CliUi.WriteLine(CliUi.Green, CliLang.Tf("agentSwitched", a.Name));
            if (!string.IsNullOrEmpty(a.DefaultModel))
                CliUi.Info(CliLang.Tf("defaultModel", a.DefaultModel));
            return true;
        }

        // ---------- /new-console：新开空白窗口，沿用当前 Agent / 系统提示词 ----------
        private static bool CmdNewConsole()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                var args = new StringBuilder();
                // 有具名 Agent 就带 --agent；没有则新窗口用内置默认系统提示词（与本窗口启动时一致）
                if (!string.IsNullOrEmpty(_currentAgentName))
                    args.Append("--agent \"").Append(_currentAgentName.Replace("\"", "")).Append('"');
                if (_trust) args.Append(args.Length > 0 ? " --trust" : "--trust");

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args.ToString(),
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = true    // 控制台程序 + ShellExecute → 另开一个独立控制台窗口
                };
                Process.Start(psi);
                string suffix = string.IsNullOrEmpty(_currentAgentName)
                    ? CliLang.T("newConsoleBuiltin")
                    : CliLang.Tf("newConsoleAgent", _currentAgentName);
                CliUi.Info(CliLang.Tf("newConsoleOk", suffix));
            }
            catch (Exception ex)
            {
                CliUi.Error(CliLang.Tf("newConsoleFail", ex.Message));
            }
            return true;
        }

        // ---------- /talk：切换历史会话接着聊 ----------
        private static bool CmdTalk(string args)
        {
            var sessions = ChatStore.All();
            if (sessions.Count == 0)
            {
                CliUi.Info(CliLang.T("noHistory"));
                return true;
            }

            string id = null;
            if (!string.IsNullOrEmpty(args))
            {
                // 参数允许是列表序号（/talk 2）或完整会话 Id
                int idx;
                if (int.TryParse(args, out idx) && idx >= 1 && idx <= sessions.Count)
                    id = sessions[idx - 1].Id;
                else id = args.Trim();
            }
            else
            {
                CliUi.WriteLine(CliUi.Bold, CliLang.T("talkList"));
                for (int i = 0; i < Math.Min(sessions.Count, 15); i++)
                    CliUi.Info("  " + (i + 1) + ". " + (sessions[i].Title ?? CliLang.Tf("unnamed"))
                        + "    " + sessions[i].UpdatedAt.ToString("MM-dd HH:mm"));
                return true;
            }

            var target = ChatStore.Get(id);
            if (target == null)
            {
                CliUi.Error(CliLang.Tf("sessionNotFound2", args));
                return true;
            }

            SwitchSession(target);
            return true;
        }

        /// <summary>保存当前会话 → 载入目标会话 → 终端回放完整历史。</summary>
        private static void SwitchSession(ChatRecord target)
        {
            // 先把当前会话落盘，避免切换后丢失上下文
            try
            {
                if (_session != null)
                {
                    _session.Messages = _agent.ExportHistoryJson();
                    ChatStore.Update(_session);
                }
            }
            catch (Exception ex) { CliUi.Info(CliLang.Tf("curSaveFail", ex.Message)); }

            _agent.ImportHistoryJson(target.Messages);
            _session = target;
            if (!string.IsNullOrEmpty(target.Agent)) _currentAgentName = target.Agent;
            if (!string.IsNullOrEmpty(_modelId)) _session.Model = _modelId;
            Console.WriteLine();
            ReplayHistory();
        }

        // ---------- /file：查看文件内容（手动输入路径时） ----------
        private static bool CmdFile(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                CliUi.Info(CliLang.T("fileUsage"));
                return true;
            }
            string path = args.Trim('"');
            if (!File.Exists(path))
            {
                CliUi.Error(CliLang.Tf("fileNotExist", path));
                return true;
            }
            string text;
            try { text = File.ReadAllText(path); }
            catch (Exception ex) { CliUi.Error(CliLang.Tf("fileReadFail", ex.Message)); return true; }

            CliUi.WriteLine(CliUi.Bold, "── " + path + " ──");
            const int max = 6000;
            CliUi.WriteLine(CliUi.Gray, text.Length > max ? text.Substring(0, max) + "\r\n" + CliLang.Tf("fileTruncated", text.Length) : text);
            return true;
        }

        // ===================== 启动辅助 =====================

        private static void PrintUsage()
        {
            Console.WriteLine(CliLang.T("usageTitle"));
            Console.WriteLine();
            Console.WriteLine(CliLang.T("usageBase"));
            Console.WriteLine(CliLang.T("usageRoot"));
            Console.WriteLine(CliLang.T("usageTrust"));
            Console.WriteLine(CliLang.T("usageSession"));
            Console.WriteLine(CliLang.T("usageAgent"));
            Console.WriteLine(CliLang.T("usageModel"));
            Console.WriteLine(CliLang.T("usageHelp"));
            Console.WriteLine();
            Console.WriteLine(CliLang.T("usageRepl"));
        }

        /// <summary>白名单加目录（规范化去重）。</summary>
        private static void AddRoot(AgentOptions opt, string dir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) return;
                string full = Path.GetFullPath(dir.Trim()).TrimEnd('\\', '/');
                if (full.Length == 0 || !Directory.Exists(full)) return;
                foreach (string r in opt.AllowedRoots)
                    if (string.Equals(r, full, StringComparison.OrdinalIgnoreCase)) return;
                opt.AllowedRoots.Insert(0, full);
            }
            catch { }
        }

        /// <summary>解析 llama-server 地址：--base → run_state.conf → 默认 127.0.0.1:6080。</summary>
        private static string ResolveBaseUrl(string argBase)
        {
            if (!string.IsNullOrEmpty(argBase)) return argBase.Trim().TrimEnd('/');
            try
            {
                string path = Path.Combine(CoreEnv.ConfigRoot, "run_state.conf");
                if (File.Exists(path))
                {
                    string host = null;
                    int port = 0;
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string l = (raw ?? "").Trim();
                        if (l.Length == 0 || l[0] == '#') continue;
                        int eq = l.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = l.Substring(0, eq).Trim().ToLowerInvariant();
                        string v = l.Substring(eq + 1).Trim();
                        if (k == "host" && v.Length > 0) host = v;
                        else if (k == "port")
                        {
                            int p;
                            if (int.TryParse(v, out p)) port = p;
                        }
                    }
                    if (!string.IsNullOrEmpty(host) && port > 0)
                    {
                        // 监听 0.0.0.0 / :: 等通配地址时，本地访问要用 127.0.0.1
                        if (host == "0.0.0.0" || host == "::" || host == "*" || host == "+") host = "127.0.0.1";
                        return "http://" + host + ":" + port;
                    }
                }
            }
            catch { }
            return "http://127.0.0.1:6080";
        }

        /// <summary>探活：GET /v1/models，顺带取第一个模型 id；失败返回 null 并给出 err。</summary>
        private static string ProbeModel(string baseUrl, out string err)
        {
            err = null;
            try
            {
                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                using (HttpResponseMessage resp = http.GetAsync(baseUrl + "/v1/models").GetAwaiter().GetResult())
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        err = "HTTP " + (int)resp.StatusCode + " " + resp.ReasonPhrase;
                        return null;
                    }
                    string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    Match m = Regex.Match(body, "\"id\"\\s*:\\s*\"([^\"]+)\"");
                    return m.Success ? m.Groups[1].Value : "";
                }
            }
            catch (Exception ex)
            {
                err = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return null;
            }
        }
    }
}
