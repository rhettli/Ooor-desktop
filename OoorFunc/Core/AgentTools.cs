using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OoorFunc.Core
{
    /// <summary>本地 Agent 工具接口。Execute 抛异常会被调用方转成 Err 结果，便于回灌给模型。</summary>
    public interface IAgentTool
    {
        string Name { get; }
        string Description { get; }
        /// <summary>OpenAI 兼容的 tools[].function.parameters（JSON Schema 子对象）。</summary>
        IDictionary<string, object> ParametersSchema { get; }
        AgentToolResult Execute(IDictionary<string, object> args);
    }

    /// <summary>工具执行结果：Ok=true 时 Text 给模型读，Ok=false 时 Text 是错误描述（同样回灌）。</summary>
    public sealed class AgentToolResult
    {
        public bool Success;
        public string Text = "";

        public static AgentToolResult Ok(string text) { return new AgentToolResult { Success = true, Text = text ?? "" }; }
        public static AgentToolResult Err(string text) { return new AgentToolResult { Success = false, Text = text ?? "" }; }
    }

    /// <summary>工具注册表：按 Options.AllowWrite/AllowCommand 决定高危工具是否对外开放。</summary>
    public sealed class AgentToolRegistry
    {
        private readonly Dictionary<string, IAgentTool> _tools = new Dictionary<string, IAgentTool>(StringComparer.OrdinalIgnoreCase);
        /// <summary>注册顺序表：Dictionary 枚举顺序无保证，发给模型的 tools 数组按此顺序拼装</summary>
        private readonly List<IAgentTool> _order = new List<IAgentTool>();

        /// <summary>本轮文件动作记录：写/执行类工具上报到此，SendAsync 结束汇总进 AgentResult</summary>
        public AgentTurnLog TurnLog { get; } = new AgentTurnLog();

        public AgentToolRegistry(AgentOptions options, AgentConfirm confirm)
        {
            if (options == null) throw new ArgumentNullException("options");

            // ---- 只读 / 环境类：始终可用 ----
            Add(new ListRootsTool(options));
            Add(new ListDirectoryTool(options));
            Add(new ReadFileTool(options));
            Add(new SearchFilesTool(options));
            Add(new GetTimeTool());
            Add(new GetEnvironmentTool());
            Add(new GetAppPathsTool());
            Add(new GetAppInfoTool());

            // ---- 写入类：AllowWrite 开启才注册 ----
            if (options.AllowWrite)
            {
                Add(new WriteFileTool(options, confirm, TurnLog));
                Add(new CreateFileTool(options, confirm, TurnLog));
                Add(new DeleteFileTool(options, confirm));
            }
            // ---- 执行类：AllowCommand 开启才注册 ----
            // 顺序即发给模型的 tools 数组顺序：run_command 在前，避免模型只认 execute_script 不往后找
            if (options.AllowCommand)
            {
                Add(new RunCommandTool(options, confirm, TurnLog));
                Add(new ExecuteScriptTool(options, confirm, TurnLog));
            }
            // ---- 联网类：AllowInternet 开启才注册 ----
            if (options.AllowInternet)
            {
                Add(new WebSearchTool(options));
                Add(new FetchUrlTool(options));
            }
        }

        /// <summary>已注册的工具名列表（给 UI「AI 能做什么」用）</summary>
        public string[] Names
        {
            get
            {
                var arr = new string[_order.Count];
                for (int i = 0; i < _order.Count; i++) arr[i] = _order[i].Name;
                return arr;
            }
        }

        private void Add(IAgentTool t)
        {
            _tools[t.Name] = t;
            if (!_order.Contains(t)) _order.Add(t);
        }

        public bool TryGet(string name, out IAgentTool tool)
        {
            return _tools.TryGetValue(name ?? "", out tool);
        }

        /// <summary>拼出 OpenAI 兼容的 tools 数组（按注册顺序：run_command 在 execute_script 之前，引导模型优先选对工具）。</summary>
        public List<object> BuildOpenAIToolsArray()
        {
            var list = new List<object>();
            foreach (var t in _order)
            {
                list.Add(new Dictionary<string, object>
                {
                    { "type", "function" },
                    { "function", new Dictionary<string, object>
                        {
                            { "name", t.Name },
                            { "description", t.Description },
                            { "parameters", t.ParametersSchema }
                        }
                    }
                });
            }
            return list;
        }

        public int Count { get { return _tools.Count; } }

        // ===================== 工具实现 =====================

        private static string AsString(IDictionary<string, object> args, string key, bool required)
        {
            object v;
            string s = args != null && args.TryGetValue(key, out v) && v != null ? v.ToString() : null;
            if (string.IsNullOrEmpty(s))
            {
                if (required) throw new ArgumentException("缺少参数：" + key);
                return null;
            }
            return s;
        }

        /// <summary>读布尔参数（模型可能给 "true" / true / 1）</summary>
        private static bool AsBool(IDictionary<string, object> args, string key, bool dflt)
        {
            object v;
            if (args == null || !args.TryGetValue(key, out v) || v == null) return dflt;
            string s = v.ToString().Trim();
            if (s.Length == 0) return dflt;
            s = s.ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes") return true;
            if (s == "false" || s == "0" || s == "no") return false;
            return dflt;
        }

        /// <summary>写 / 删 / 执行类工具共用的 confirm 参数 schema</summary>
        private static Dictionary<string, object> ConfirmParamSchema()
        {
            return new Dictionary<string, object>
            {
                { "type", "boolean" },
                { "description", "是否需要用户确认。默认跟随「AI 自行判断」设置：开启时默认不弹窗直接执行，关闭时会弹窗。确信操作安全时可不传；操作风险高时传 true 强制让用户确认。" }
            };
        }

        /// <summary>读整数参数（越界回退默认值）</summary>
        private static int AsInt(IDictionary<string, object> args, string key, int dflt, int min, int max)
        {
            object v;
            if (args == null || !args.TryGetValue(key, out v) || v == null) return dflt;
            int n;
            if (!int.TryParse(v.ToString(), out n)) return dflt;
            if (n < min) return min;
            if (n > max) return max;
            return n;
        }

        /// <summary>异步读完子进程管道的原始字节（避免用固定编码解码导致 GBK/UTF-8 乱码）。</summary>
        private static async Task<byte[]> ReadPipeBytesAsync(Stream s)
        {
            var ms = new MemoryStream();
            byte[] buf = new byte[4096];
            int n;
            while ((n = await s.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false)) > 0)
                ms.Write(buf, 0, n);
            return ms.ToArray();
        }

        /// <summary>
        /// 按行自动识别编码：先按严格 UTF-8 解码，该行不是合法 UTF-8（cmd 中文输出是 GBK）时
        /// 回退 GBK(936)。0x0A 既不会出现在 UTF-8 多字节序列中，也不会出现在 GBK 双字节中，
        /// 所以按字节切分逐行解码是安全的；同一输出里 cmd(GBK) 与 Python(UTF-8) 混排也能正确显示。
        /// </summary>
        private static string DecodeMixedOutput(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            var utf8Strict = new UTF8Encoding(false, true);
            Encoding gbk = null;
            try { gbk = Encoding.GetEncoding(936); } catch { }

            var sb = new StringBuilder();
            int start = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i != bytes.Length && bytes[i] != (byte)'\n') continue;
                int len = i - start;
                if (len > 0 && bytes[start + len - 1] == (byte)'\r') len--;   // 去掉行尾 CR
                byte[] line = new byte[len];
                Buffer.BlockCopy(bytes, start, line, 0, len);
                try { sb.Append(utf8Strict.GetString(line)); }
                catch (DecoderFallbackException)
                {
                    sb.Append(gbk != null ? gbk.GetString(line) : Encoding.UTF8.GetString(line));
                }
                if (i < bytes.Length) sb.Append('\n');
                start = i + 1;
            }
            return sb.ToString();
        }

        /// <summary>并发读子进程 stdout/stderr 原始字节，waitMs 内未退出则强杀；返回解码后的文本。</summary>
        private static string RunAndCapture(Process p, int waitMs)
        {
            Task<byte[]> outTask = ReadPipeBytesAsync(p.StandardOutput.BaseStream);
            Task<byte[]> errTask = ReadPipeBytesAsync(p.StandardError.BaseStream);
            bool killed = false;
            if (!p.WaitForExit(waitMs))
            {
                killed = true;
                try { p.Kill(); } catch { }
            }
            try { Task.WaitAll(new Task[] { outTask, errTask }, 3000); } catch { }

            string text = DecodeMixedOutput(outTask.IsCompleted ? outTask.Result : new byte[0]);
            string err = DecodeMixedOutput(errTask.IsCompleted ? errTask.Result : new byte[0]);
            if (err.Length > 0) text += (text.Length > 0 ? "\n" : "") + "--- stderr ---\n" + err;
            int code = 0;
            try { code = p.ExitCode; } catch { }
            text += "\n[exit=" + code + "]";
            if (killed) text = "（超时已强杀）\n" + text;
            return text.TrimEnd('\n');
        }

        /// <summary>
        /// 高危操作统一审批入口。wantConfirm = 模型是否希望征求用户同意（各工具默认值不同）：
        ///   - wantConfirm=true            → 弹窗（模型明确要问）；
        ///   - wantConfirm=false（模型跳过）→ 仅当用户开启「AI 自行判断」(TrustAiJudgment) 才放行，否则仍然弹窗；
        ///   - 没有 UI 回调（confirm==null）→ 保守拒绝。
        /// 返回 false 表示用户拒绝执行。
        /// </summary>
        private static bool AskConfirm(AgentOptions opt, AgentConfirm confirm, bool wantConfirm, string title, string detail)
        {
            bool skip = !wantConfirm && opt != null && opt.TrustAiJudgment;
            if (skip) return true;
            if (confirm == null) return false;
            try { return confirm(title, detail); }
            catch { return false; }
        }

        /// <summary>获取沙盒白名单目录合集（模型可读写的根目录），并说明相对路径的落盘规则</summary>
        private sealed class ListRootsTool : IAgentTool
        {
            public string Name { get { return "list_roots"; } }
            public string Description
            {
                get
                {
                    return "获取当前沙盒白名单目录合集（你能读写的全部根目录）。无参数。" +
                           "做任何文件操作前先调它；文件 path 只写文件名或相对路径时会自动落到临时工作目录 temp 下。";
                }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() }
                    };
                }
            }
            private readonly AgentOptions _opt;
            public ListRootsTool(AgentOptions opt) { _opt = opt; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                var sb = new StringBuilder();
                sb.Append("沙盒白名单目录（读写只能在这些目录及其子目录内）：\n");
                var roots = _opt != null && _opt.AllowedRoots != null ? _opt.AllowedRoots : new List<string>();
                if (roots.Count == 0) sb.Append("（空：请用户在界面「沙盒白名单」里添加目录）\n");
                for (int i = 0; i < roots.Count; i++) sb.Append(i + 1).Append(". ").Append(roots[i]).Append('\n');
                string temp = _opt != null ? _opt.TempDir : "";
                sb.Append("\n临时工作目录（写文件/目录时给相对路径或纯文件名的落地位置，已在白名单内）：\n").Append(temp).Append('\n');
                sb.Append("\n规则：path 为绝对路径时必须在上述白名单内；只写文件名或相对路径时自动落到临时工作目录下，工具返回值会给出实际完整路径。");
                return AgentToolResult.Ok(sb.ToString().TrimEnd('\n'));
            }
        }

        private sealed class ListDirectoryTool : IAgentTool
        {
            public string Name { get { return "list_directory"; } }
            public string Description { get { return "列出目录下的直接子项（不递归）。返回相对路径或绝对路径需在沙盒白名单内。"; } }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "目录绝对路径或相对沙盒根的路径" } } }
                            }
                        },
                        { "required", new[] { "path" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            public ListDirectoryTool(AgentOptions opt) { _opt = opt; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                p = _opt.ResolveAllowed(p);
                if (!Directory.Exists(p)) return AgentToolResult.Err("目录不存在：" + p);

                var lines = new StringBuilder();
                int max = _opt.ListMaxEntries;
                int total = 0, shown = 0;
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(p))
                    {
                        total++;
                        if (shown < max) { lines.Append("[DIR]  ").Append(Path.GetFileName(dir)).Append('\n'); shown++; }
                    }
                    foreach (string f in Directory.EnumerateFiles(p))
                    {
                        total++;
                        if (shown < max)
                        {
                            long len = 0; try { len = new FileInfo(f).Length; } catch { }
                            lines.Append("[FILE] ").Append(Path.GetFileName(f)).Append("  (").Append(len).Append(" bytes)\n");
                            shown++;
                        }
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("枚举失败：" + ex.Message); }

                if (total > shown)
                    lines.Append("...\n（已截断，共 ").Append(total).Append(" 项，显示前 ").Append(shown).Append(" 项）");
                return AgentToolResult.Ok(lines.ToString().TrimEnd('\n'));
            }
        }

        private sealed class ReadFileTool : IAgentTool
        {
            public string Name { get { return "read_file"; } }
            public string Description { get { return "读取文本文件内容（UTF-8）。文件大小受 ReadMaxBytes 限制；超过请改用搜索工具分块读取。"; } }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "文件绝对路径或相对沙盒根的路径" } } },
                                { "max_bytes", new Dictionary<string, object> { { "type", "integer" }, { "description", "本次读取上限（字节），默认走 Options.ReadMaxBytes" } } }
                            }
                        },
                        { "required", new[] { "path" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            public ReadFileTool(AgentOptions opt) { _opt = opt; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                p = _opt.ResolveAllowed(p);
                if (!File.Exists(p)) return AgentToolResult.Err("文件不存在：" + p);

                long cap = _opt.ReadMaxBytes;
                object mb;
                if (args != null && args.TryGetValue("max_bytes", out mb) && mb != null)
                {
                    long v;
                    if (long.TryParse(mb.ToString(), out v) && v > 0 && v < cap) cap = v;
                }

                long len;
                try { len = new FileInfo(p).Length; }
                catch (Exception ex) { return AgentToolResult.Err("读取失败：" + ex.Message); }

                if (len > cap)
                    return AgentToolResult.Err("文件过大（" + len + " bytes > 上限 " + cap + "），请用搜索/分块方式读取");

                try
                {
                    string text = File.ReadAllText(p, new UTF8Encoding(false));
                    return AgentToolResult.Ok(text);
                }
                catch (Exception ex) { return AgentToolResult.Err("读取失败：" + ex.Message); }
            }
        }

        private sealed class SearchFilesTool : IAgentTool
        {
            public string Name { get { return "search_files"; } }
            public string Description { get { return "在指定目录下按文件名子串递归查找（不区分大小写）。命中数超限会被截断。"; } }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "搜索根目录（必须在沙盒内）" } } },
                                { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "文件名子串（不含通配符）" } } }
                            }
                        },
                        { "required", new[] { "path", "query" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            public SearchFilesTool(AgentOptions opt) { _opt = opt; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                string q = AsString(args, "query", true);
                p = _opt.ResolveAllowed(p);
                if (!Directory.Exists(p)) return AgentToolResult.Err("目录不存在：" + p);

                var sb = new StringBuilder();
                int max = _opt.ListMaxEntries;
                int shown = 0;
                try
                {
                    foreach (string f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    {
                        string name = Path.GetFileName(f);
                        if (name == null) continue;
                        if (name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (shown >= max)
                        {
                            sb.Append("...\n（已截断，命中数过多）");
                            break;
                        }
                        sb.Append(f).Append('\n');
                        shown++;
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("搜索失败：" + ex.Message); }
                if (shown == 0) return AgentToolResult.Ok("（无命中）");
                return AgentToolResult.Ok(sb.ToString().TrimEnd('\n'));
            }
        }

        // ===================== 环境 / 程序信息类（只读，始终可用） =====================

        /// <summary>获取当前时间（本地 / UTC / 时区 / 周几 / Unix 时间戳）</summary>
        private sealed class GetTimeTool : IAgentTool
        {
            public string Name { get { return "get_time"; } }
            public string Description
            {
                get { return "获取当前系统时间：本地时间、UTC、星期、时区、Unix 时间戳。无参数。涉及“今天/现在/最近”的任务先调它。"; }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() }
                    };
                }
            }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                DateTime now = DateTime.Now;
                DateTime utc = now.ToUniversalTime();
                TimeZoneInfo tz = TimeZoneInfo.Local;
                string[] wk = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
                TimeSpan off = tz.GetUtcOffset(now);

                var sb = new StringBuilder();
                sb.Append("本地时间：").Append(now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                  .Append("（").Append(wk[(int)now.DayOfWeek]).Append("）\n");
                sb.Append("UTC 时间：").Append(utc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("时区：").Append(tz.Id).Append(" (UTC").Append(off.TotalMinutes >= 0 ? "+" : "-")
                  .Append(Math.Abs(off.Hours).ToString("00")).Append(':').Append(Math.Abs(off.Minutes).ToString("00")).Append(")\n");
                sb.Append("夏令时：").Append(tz.IsDaylightSavingTime(now) ? "是" : "否").Append('\n');
                sb.Append("Unix 时间戳：").Append(ToUnixSeconds(now)).Append(" 秒 / ")
                  .Append(ToUnixSeconds(now) * 1000L + now.Millisecond).Append(" 毫秒\n");
                sb.Append("今年第 ").Append(now.DayOfYear).Append(" 天");
                return AgentToolResult.Ok(sb.ToString());
            }

            private static long ToUnixSeconds(DateTime local)
            {
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (long)(local.ToUniversalTime() - epoch).TotalSeconds;
            }
        }

        /// <summary>获取运行环境（系统 / .NET / 硬件 / 用户 / 屏幕）</summary>
        private sealed class GetEnvironmentTool : IAgentTool
        {
            public string Name { get { return "get_environment"; } }
            public string Description
            {
                get { return "获取当前电脑的运行环境：操作系统版本、.NET 运行时、CPU 核心数、内存、机器名/用户名、区域语言、屏幕分辨率。无参数。"; }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() }
                    };
                }
            }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                var sb = new StringBuilder();
                try { sb.Append("操作系统：").Append(Environment.OSVersion.VersionString) .Append("（服务包 ").Append(Environment.OSVersion.ServicePack).Append("）\n"); } catch { }
                sb.Append("系统架构：").Append(Environment.Is64BitOperatingSystem ? "64 位" : "32 位").Append('\n');
                sb.Append(".NET 运行时：").Append(System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()).Append('\n');
                sb.Append("CLR 版本：").Append(Environment.Version).Append('\n');
                sb.Append("当前进程：").Append(Environment.Is64BitProcess ? "64 位" : "32 位").Append(" / ")
                  .Append("PID ").Append(Process.GetCurrentProcess().Id).Append('\n');
                sb.Append("CPU 逻辑核心：").Append(Environment.ProcessorCount).Append('\n');
                try
                {
                    var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                    sb.Append("物理内存：").Append(ToGb(ci.TotalPhysicalMemory)).Append(" 总 / ")
                      .Append(ToGb(ci.AvailablePhysicalMemory)).Append(" 可用\n");
                }
                catch { }
                sb.Append("机器名：").Append(Environment.MachineName).Append("（域 ").Append(SafeUserDomain()).Append("）\n");
                sb.Append("当前用户：").Append(Environment.UserName).Append('\n');
                sb.Append("系统区域：").Append(CultureInfo.CurrentCulture.Name).Append(" / 界面语言 ")
                  .Append(CultureInfo.CurrentUICulture.Name).Append('\n');
                sb.Append("屏幕：").Append(SystemInformation.VirtualScreen.Width).Append('x')
                  .Append(SystemInformation.VirtualScreen.Height).Append("（")
                  .Append(SystemInformation.MonitorCount).Append(" 个显示器）\n");
                sb.Append("运行时长：").Append((DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"hh\:mm\:ss"));
                return AgentToolResult.Ok(sb.ToString());
            }

            private static string ToGb(ulong bytes)
            {
                return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB";
            }
            private static string SafeUserDomain()
            {
                try { return Environment.UserDomainName; } catch { return ""; }
            }
        }

        /// <summary>获取程序运行目录（exe / 启动目录 / 配置根 / 模型目录 / 页面资源目录）</summary>
        private sealed class GetAppPathsTool : IAgentTool
        {
            public string Name { get { return "get_app_paths"; } }
            public string Description
            {
                get { return "获取本程序（ooor）的关键目录：程序目录、exe 路径、启动目录、配置根目录、模型目录、页面资源目录等。无参数。"; }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() }
                    };
                }
            }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                var sb = new StringBuilder();
                sb.Append("程序名称：").Append(CoreEnv.AppName).Append('\n');
                sb.Append("程序版本：").Append(CoreEnv.AppVersion).Append('\n');
                sb.Append("启动目录：").Append(SafePath(() => CoreEnv.StartupPath)).Append('\n');
                sb.Append("程序集位置：").Append(SafePath(() => System.Reflection.Assembly.GetExecutingAssembly().Location)).Append('\n');
                sb.Append("当前工作目录：").Append(SafePath(() => Environment.CurrentDirectory)).Append('\n');
                sb.Append("配置根目录：").Append(SafePath(() => CoreEnv.ConfigRoot)).Append('\n');
                sb.Append("llama 版本目录：").Append(SafePath(() => CoreEnv.LlamaBinsDir)).Append('\n');
                string selVer = CoreEnv.GetSelectedVersion();
                sb.Append("当前 llama 版本：").Append(string.IsNullOrEmpty(selVer) ? "(未选择)" : selVer).Append('\n');
                sb.Append("模型目录：").Append(SafePath(() => CoreEnv.ModelsDir)).Append('\n');
                sb.Append("页面资源目录（html）：").Append(SafePath(() => CoreEnv.HtmlDir)).Append('\n');
                sb.Append("系统临时目录：").Append(SafePath(() => Path.GetTempPath()));
                return AgentToolResult.Ok(sb.ToString());
            }

            private static string SafePath(Func<string> f)
            {
                try { return f(); } catch (Exception ex) { return "(不可用：" + ex.Message + ")"; }
            }
        }

        /// <summary>获取程序信息与目录结构（便于模型理解工程布局后扩展）</summary>
        private sealed class GetAppInfoTool : IAgentTool
        {
            public string Name { get { return "get_app_info"; } }
            public string Description
            {
                get
                {
                    return "获取程序自身信息与目录结构：版本、工具清单、以及程序目录的树状结构（便于理解工程布局、做二次开发/扩展）。" +
                           "参数 depth 控制展开层级（1-4，默认 2）。只读，不受沙盒限制。";
                }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "depth", new Dictionary<string, object> { { "type", "integer" }, { "description", "目录树展开层级，1-4，默认 2" } } },
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "可选：指定要展开的目录，默认程序启动目录" } } }
                            }
                        }
                    };
                }
            }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                int depth = AsInt(args, "depth", 2, 1, 4);
                string root = AsString(args, "path", false);
                if (string.IsNullOrEmpty(root))
                {
                    try { root = CoreEnv.StartupPath; } catch { root = Environment.CurrentDirectory; }
                }
                if (!Directory.Exists(root)) return AgentToolResult.Err("目录不存在：" + root);

                var sb = new StringBuilder();
                sb.Append("== 程序信息 ==\n");
                sb.Append("名称：").Append(CoreEnv.AppName).Append('\n');
                sb.Append("版本：").Append(CoreEnv.AppVersion).Append('\n');
                sb.Append("exe：").Append(SafeAssemblyLocation()).Append('\n');
                sb.Append("启动目录：").Append(root).Append('\n');
                sb.Append("配置根：").Append(SafeConfigRoot()).Append('\n');
                sb.Append("模型目录：").Append(SafeModelsDir()).Append('\n');
                sb.Append("页面资源：").Append(SafeHtmlDir()).Append('\n');
                sb.Append("模块：").Append(CoreEnv.AppDescription).Append('\n');
                sb.Append("\n== 目录结构 ==\n");
                sb.Append(root).Append('\n');

                int count = 0;
                BuildTree(sb, root, 1, depth, ref count, 300, "");
                if (count >= 300) sb.Append("...（条目过多已截断）\n");
                return AgentToolResult.Ok(sb.ToString().TrimEnd('\n'));
            }

            private static void BuildTree(StringBuilder sb, string dir, int level, int maxLevel, ref int count, int maxEntries, string prefix)
            {
                if (level > maxLevel || count >= maxEntries) return;
                string[] dirs, files;
                try { dirs = Directory.GetDirectories(dir); }
                catch { return; }
                try { files = Directory.GetFiles(dir); }
                catch { files = new string[0]; }

                for (int i = 0; i < dirs.Length && count < maxEntries; i++)
                {
                    count++;
                    sb.Append(prefix).Append("├─ [DIR]  ").Append(Path.GetFileName(dirs[i])).Append('\n');
                    BuildTree(sb, dirs[i], level + 1, maxLevel, ref count, maxEntries, prefix + "│  ");
                }
                for (int i = 0; i < files.Length && count < maxEntries; i++)
                {
                    count++;
                    string size = "";
                    try { size = "  (" + new FileInfo(files[i]).Length + " bytes)"; } catch { }
                    sb.Append(prefix).Append("├─ ").Append(Path.GetFileName(files[i])).Append(size).Append('\n');
                }
            }

            private static string SafeAssemblyLocation()
            {
                try { return System.Reflection.Assembly.GetExecutingAssembly().Location; } catch { return ""; }
            }
            private static string SafeConfigRoot()
            {
                try { return CoreEnv.ConfigRoot; } catch { return ""; }
            }
            private static string SafeModelsDir()
            {
                try { return CoreEnv.ModelsDir; } catch { return ""; }
            }
            private static string SafeHtmlDir()
            {
                try { return CoreEnv.HtmlDir; } catch { return ""; }
            }
        }

        private sealed class WriteFileTool : IAgentTool
        {
            public string Name { get { return "write_file"; } }
            public string Description { get { return "把文本写入文件（覆盖已有文件，UTF-8；目录不存在会自动创建）。高危：默认弹窗请用户确认。"; } }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" } } },
                                { "content", new Dictionary<string, object> { { "type", "string" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path", "content" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public WriteFileTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                string content = AsString(args, "content", true) ?? "";
                p = _opt.ResolveAllowed(p);

                bool wantConfirm = AsBool(args, "confirm", false);

                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？",
                            "创建目录并写入文件：\n" + dir + "\n并写入文件：\n" + p))
                        return AgentToolResult.Err("用户拒绝创建目录");
                }

                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", "写入文件：" + p))
                    return AgentToolResult.Err("用户拒绝写入");

                try
                {
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(p, content, new UTF8Encoding(false));
                    _log?.RecordWritten(p);
                    return AgentToolResult.Ok("已写入 " + content.Length + " 字符到 " + p);
                }
                catch (Exception ex) { return AgentToolResult.Err("写入失败：" + ex.Message); }
            }
        }

        private sealed class DeleteFileTool : IAgentTool
        {
            public string Name { get { return "delete_file"; } }
            public string Description { get { return "把文件移到回收站（可恢复；不支持目录递归删除）。高危：默认弹窗请用户确认。"; } }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            public DeleteFileTool(AgentOptions opt, AgentConfirm confirm) { _opt = opt; _confirm = confirm; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                p = _opt.ResolveAllowed(p);
                if (!File.Exists(p)) return AgentToolResult.Err("文件不存在：" + p);

                bool wantConfirm = AsBool(args, "confirm", false);
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", "删除文件（移到回收站）：" + p))
                    return AgentToolResult.Err("用户拒绝删除");

                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(p,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                    return AgentToolResult.Ok("已移到回收站：" + p);
                }
                catch (Exception ex) { return AgentToolResult.Err("删除失败：" + ex.Message); }
            }
        }

        private sealed class RunCommandTool : IAgentTool
        {
            public string Name { get { return "run_command"; } }
            public string Description
            {
                get
                {
                    return "在 cmd.exe 里执行一条命令（同步等待、捕获输出、超时强杀）。当前目录固定为沙盒临时工作目录 "
                         + _opt.TempDir + "（即 create_file 相对路径的落地目录），命令里用相对路径即可操作其中文件。"
                         + "打开文件/网址用 start 时，带引号的路径前必须先给空标题，写 start \"\" \"文件完整路径\"；"
                         + "需要跑多行脚本请改用 execute_script。";
                }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "command", new Dictionary<string, object> { { "type", "string" } } },
                                { "timeout_seconds", new Dictionary<string, object> { { "type", "integer" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "command" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public RunCommandTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string cmd = AsString(args, "command", true);
                cmd = FixStartTitle(cmd);   // start "带引号路径" → start "" "路径"，否则引号内容被当成窗口标题
                int timeoutSec = (int)_opt.CommandTimeout.TotalSeconds;
                object ts;
                if (args != null && args.TryGetValue("timeout_seconds", out ts) && ts != null)
                {
                    int v;
                    if (int.TryParse(ts.ToString(), out v) && v > 0 && v < 600) timeoutSec = v;
                }

                // 粗粒度黑名单：极易误操作的命令在 UI 端弹窗时让用户二次确认
                string lower = (cmd ?? "").ToLowerInvariant();
                bool highRisk =
                    lower.Contains("format ") || lower.Contains("rmdir /s") || lower.Contains("rd /s") ||
                    lower.Contains("del /f /s") || lower.Contains("del /s /q") || lower.Contains("reg delete") ||
                    lower.Contains("net user") || lower.Contains("net stop") || lower.Contains("bcdedit") ||
                    lower.Contains("diskpart") || lower.Contains("shutdown") || lower.Contains("cipher /w");
                string title = "请您确认是否继续执行敏感操作？";
                bool wantConfirm = AsBool(args, "confirm", false);
                if (!AskConfirm(_opt, _confirm, wantConfirm, title, (highRisk ? "⚠ 危险命令\n" : "") + "执行命令：" + cmd)) return AgentToolResult.Err("用户拒绝执行");

                try
                {
                    // 工作目录固定为沙盒 temp：模型 create_file 生成的相对路径文件都在这里
                    string workDir = null;
                    try
                    {
                        workDir = _opt.TempDir;
                        Directory.CreateDirectory(workDir);
                    }
                    catch { workDir = null; }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/d /c " + cmd,
                        WorkingDirectory = workDir ?? "",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    // 命令里若调用 python：让其 stdio/文件默认走 UTF-8（输出由 C# 端按行自动识别编码）
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    psi.EnvironmentVariables["PYTHONUTF8"] = "1";
                    using (var p = Process.Start(psi))
                    {
                        if (p == null) return AgentToolResult.Err("进程启动失败");
                        // 读原始字节并按行自动识别 UTF-8/GBK：重定向管道无控制台，
                        // chcp 不生效，cmd 自身的中文错误信息始终是系统 OEM 码页（GBK）
                        string outText = RunAndCapture(p, timeoutSec * 1000);
                        // 记录本回合实际运行/打开的文件（提取失败不影响结果）
                        try
                        {
                            foreach (string f in ExtractRunTargets(cmd, workDir))
                                _log?.RecordExecuted(f);
                        }
                        catch { }
                        return AgentToolResult.Ok(outText);
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("执行失败：" + ex.Message); }
            }

            // ---- 从命令文本提取"被运行/打开的文件" ----

            /// <summary>解释器名（首 token 命中则跳过，避免把 python.exe 本体当成被运行文件）</summary>
            private static readonly HashSet<string> InterpreterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "python","python3","py","node","cmd","powershell","pwsh","bash","sh",
                "cscript","wscript",
                "python.exe","python3.exe","py.exe","node.exe","cmd.exe",
                "powershell.exe","pwsh.exe","bash.exe","sh.exe","cscript.exe","wscript.exe"
            };

            /// <summary>非 start 命令下只收录这些"可执行/可脚本化"扩展名，避免 copy/del 等参数误报</summary>
            private static readonly HashSet<string> RunnableExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".bat",".cmd",".ps1",".py",".js",".mjs",".vbs",".sh",".exe",".msi",".com"
            };

            /// <summary>
            /// 提取被运行/打开的文件：
            ///   ① start：FixStartTitle 已规范成 start ... "" "目标"，取最后一对引号目标（接受任意已存在文件，
            ///      因为 start 常用于打开 html/pdf/图片/office 文档）；
            ///   ② 其余命令：逐 token（引号内容或非空白串），跳过首位解释器，
            ///      相对路径按工作目录解析，要求"文件存在 + 扩展名可执行"。
            /// </summary>
            private static List<string> ExtractRunTargets(string cmd, string workDir)
            {
                var hits = new List<string>();
                if (string.IsNullOrWhiteSpace(cmd)) return hits;

                if (Regex.IsMatch(cmd, @"^\s*start\b", RegexOptions.IgnoreCase))
                {
                    // FixStartTitle 已把目标补成 "" "目标"；取最后一对，规避 /D "目录" 的干扰
                    var pairs = Regex.Matches(cmd, @"""""\s*""([^""]+)""");
                    if (pairs.Count > 0)
                    {
                        TryAddExisting(hits, pairs[pairs.Count - 1].Groups[1].Value, workDir, true);
                        return hits;
                    }
                    // start 未带引号目标 → 退回 token 扫描
                }

                int tokenIndex = 0;
                foreach (Match tok in Regex.Matches(cmd, @"""([^""]*)""|(\S+)"))
                {
                    string raw = tok.Groups[1].Success ? tok.Groups[1].Value : tok.Value;
                    if (raw.Length > 0)
                    {
                        bool isInterpreter = tokenIndex == 0 && InterpreterNames.Contains(raw);
                        if (!isInterpreter) TryAddExisting(hits, raw, workDir, false);
                    }
                    tokenIndex++;
                }
                return hits;
            }

            /// <summary>把一个路径 token 解析为绝对路径并按规则收录（存在性 + 扩展名 + 去重）。</summary>
            private static void TryAddExisting(List<string> hits, string raw, string workDir, bool acceptAnyFile)
            {
                string candidate;
                try
                {
                    string full;
                    if (Path.IsPathRooted(raw)) full = raw;
                    else
                    {
                        if (string.IsNullOrEmpty(workDir)) return;
                        full = Path.Combine(workDir, raw);
                    }
                    candidate = Path.GetFullPath(full);
                }
                catch { return; }

                if (!File.Exists(candidate)) return;
                string ext = Path.GetExtension(candidate);
                if (acceptAnyFile)
                {
                    if (ext.Length == 0) return;          // start 目标要求像文件（排除目录）
                }
                else if (!RunnableExt.Contains(ext)) return;

                if (InterpreterNames.Contains(Path.GetFileName(candidate))) return;
                for (int i = 0; i < hits.Count; i++)
                    if (string.Equals(hits[i], candidate, StringComparison.OrdinalIgnoreCase)) return;
                hits.Add(candidate);
            }

            /// <summary>
            /// 修正 cmd start 的标题坑：start 把第一个带引号的参数当作“新窗口标题”，
            /// 所以 start "D:\a\b.html" 只会开一个空白控制台窗口、不打开文件。
            /// 当首个引号内容像路径/网址/文件时，自动补空标题：start "" "D:\a\b.html"。
            /// </summary>
            private static string FixStartTitle(string cmd)
            {
                if (string.IsNullOrEmpty(cmd)) return cmd;
                var head = Regex.Match(cmd, @"^\s*start\b", RegexOptions.IgnoreCase);
                if (!head.Success) return cmd;

                int pos = head.Length;
                // 跳过 start 的开关：/B /WAIT /MIN /MAX 等；/D /NODE /AFFINITY /MACHINE 还带一个参数
                while (true)
                {
                    while (pos < cmd.Length && char.IsWhiteSpace(cmd[pos])) pos++;
                    if (pos >= cmd.Length || cmd[pos] != '/') break;
                    int sp = cmd.IndexOfAny(new[] { ' ', '\t' }, pos);
                    string sw = (sp < 0 ? cmd.Substring(pos) : cmd.Substring(pos, sp - pos)).TrimStart('/').ToLowerInvariant();
                    pos = sp < 0 ? cmd.Length : sp;
                    if (sw == "d" || sw == "node" || sw == "affinity" || sw == "machine")
                    {
                        while (pos < cmd.Length && char.IsWhiteSpace(cmd[pos])) pos++;
                        if (pos < cmd.Length && cmd[pos] == '"')
                        {
                            int q = cmd.IndexOf('"', pos + 1);
                            pos = q < 0 ? cmd.Length : q + 1;
                        }
                        else
                        {
                            int v = cmd.IndexOfAny(new[] { ' ', '\t' }, pos);
                            pos = v < 0 ? cmd.Length : v;
                        }
                    }
                }

                while (pos < cmd.Length && char.IsWhiteSpace(cmd[pos])) pos++;
                if (pos >= cmd.Length || cmd[pos] != '"') return cmd;     // 未加引号的参数 start 能正确处理
                int end = cmd.IndexOf('"', pos + 1);
                if (end < 0) return cmd;
                string token = cmd.Substring(pos + 1, end - pos - 1);
                if (token.Length == 0) return cmd;                       // 已显式给了空标题
                // 像目标（含盘符/反斜杠/斜杠/URL，或以扩展名结尾）才补；普通标题不含这些特征
                bool looksTarget =
                    token.IndexOf('\\') >= 0 ||
                    token.IndexOf("://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    token.IndexOf('/') >= 0 ||
                    Regex.IsMatch(token, @"\.[A-Za-z0-9]{1,8}$");
                if (!looksTarget) return cmd;
                return cmd.Substring(0, pos) + "\"\" " + cmd.Substring(pos);
            }
        }

        /// <summary>创建文件：不覆盖已存在文件（除非 overwrite=true），默认不弹确认（可用 confirm=true 主动征求同意）</summary>
        private sealed class CreateFileTool : IAgentTool
        {
            public string Name { get { return "create_file"; } }
            public string Description
            {
                get
                {
                    return "创建新文件并写入内容（UTF-8；上层目录自动创建）。若目标已存在会报错，避免误覆盖（确实要覆盖请传 overwrite=true）。" +
                           "创建文件前先调用 list_roots 获取白名单目录合集，确认目标位置可写。" +
                           "path 只写文件名或相对路径时，文件会自动创建在沙盒内的临时工作目录 temp 下，返回值会给出实际完整路径。" +
                           "新建文件默认直接执行；内容较多或涉及用户既有目录时，可传 confirm=true 先让用户审阅。";
                }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "文件路径：绝对路径须在沙盒白名单内；纯文件名或相对路径会自动落到临时工作目录 temp 下" } } },
                                { "content", new Dictionary<string, object> { { "type", "string" }, { "description", "写入的文本内容" } } },
                                { "overwrite", new Dictionary<string, object> { { "type", "boolean" }, { "description", "目标已存在时是否覆盖，默认 false（直接报错）" } } },
                                { "confirm", new Dictionary<string, object> { { "type", "boolean" }, { "description", "是否先请用户确认，默认 false（新建文件通常无需打扰用户）" } } }
                            }
                        },
                        { "required", new[] { "path", "content" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public CreateFileTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                string content = AsString(args, "content", false) ?? "";
                bool overwrite = AsBool(args, "overwrite", false);
                bool wantConfirm = AsBool(args, "confirm", false);
                p = _opt.ResolveAllowed(p);

                bool exists = File.Exists(p);
                if (exists && !overwrite)
                    return AgentToolResult.Err("文件已存在，未做修改：" + p + "（确需覆盖请传 overwrite=true）");

                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？",
                        (exists ? "覆盖已有文件：" : "创建文件：") + p))
                    return AgentToolResult.Err("用户拒绝创建文件");

                try
                {
                    string dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(p, content, new UTF8Encoding(false));
                    _log?.RecordWritten(p);
                    return AgentToolResult.Ok("已" + (exists ? "覆盖" : "创建") + "文件：" + p + "（" + content.Length + " 字符）");
                }
                catch (Exception ex) { return AgentToolResult.Err("创建失败：" + ex.Message); }
            }
        }

        /// <summary>
        /// 执行脚本：直接跑沙盒内的脚本文件（.bat/.cmd/.ps1/.py/.js/.vbs/.sh），
        /// 或把模型给的 content 落到 {ConfigRoot}\agent-temp 下再跑。高危，默认弹窗确认。
        /// </summary>
        private sealed class ExecuteScriptTool : IAgentTool
        {
            public string Name { get { return "execute_script"; } }
            public string Description
            {
                get
                {
                    return "执行脚本文件并按扩展名挑选解释器（.bat/.cmd→cmd，.ps1→powershell，.py→python，.js→node，.vbs→cscript，.sh→bash），" +
                           "同步等待、捕获输出、超时强杀。两种用法：① path 指向沙盒内脚本；② language+content 直接给脚本内容（自动落到临时目录再执行）。" +
                           "只跑单条命令或运行已创建的脚本文件时优先用 run_command，本工具适合多行脚本。" +
                           "是否需要用户确认由「AI 自行判断」设置决定：开启时默认静默执行，关闭时会弹窗确认；也可用 confirm=true 主动征求同意。";
                }
            }
            public IDictionary<string, object> ParametersSchema
            {
                get
                {
                    return new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "脚本文件路径（沙盒白名单内），与 language+content 二选一" } } },
                                { "language", new Dictionary<string, object> { { "type", "string" }, { "description", "脚本类型：bat|cmd|ps1|powershell|python|py|js|node|vbs|sh" } } },
                                { "content", new Dictionary<string, object> { { "type", "string" }, { "description", "脚本内容（配合 language 使用）" } } },
                                { "args", new Dictionary<string, object> { { "type", "string" }, { "description", "附加命令行参数（原样追加）" } } },
                                { "cwd", new Dictionary<string, object> { { "type", "string" }, { "description", "工作目录（须在沙盒内），默认脚本所在目录" } } },
                                { "timeout_seconds", new Dictionary<string, object> { { "type", "integer" }, { "description", "超时秒数，默认 30，最大 600" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new string[0] }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public ExecuteScriptTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string path = AsString(args, "path", false);
                string language = AsString(args, "language", false);
                string content = AsString(args, "content", false);
                string extraArgs = AsString(args, "args", false) ?? "";
                string cwd = AsString(args, "cwd", false);
                int timeoutSec = AsInt(args, "timeout_seconds", 30, 1, 600);
                bool wantConfirm = AsBool(args, "confirm", false);

                bool tempScript = false;
                if (string.IsNullOrEmpty(path))
                {
                    if (string.IsNullOrEmpty(content)) return AgentToolResult.Err("缺少参数：path 或 language+content");
                    string ext = NormalizeExt(language);
                    if (ext.Length == 0) return AgentToolResult.Err("不支持的 language：" + language + "（可用 bat/cmd/ps1/powershell/python/py/js/node/vbs/sh）");
                    try
                    {
                        string dir = Path.Combine(CoreEnv.ConfigRoot, "agent-temp");
                        Directory.CreateDirectory(dir);
                        path = Path.Combine(dir, "agent_script_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ext);
                        File.WriteAllText(path, content, new UTF8Encoding(false));
                        tempScript = true;
                    }
                    catch (Exception ex) { return AgentToolResult.Err("生成临时脚本失败：" + ex.Message); }
                }
                else
                {
                    try { path = _opt.ResolveAllowed(path); } catch (Exception ex) { return AgentToolResult.Err(ex.Message); }
                    if (!File.Exists(path)) return AgentToolResult.Err("脚本不存在：" + path);
                }

                if (!string.IsNullOrEmpty(cwd))
                {
                    try { cwd = _opt.ResolveAllowed(cwd); } catch (Exception ex) { return AgentToolResult.Err(ex.Message); }
                    if (!Directory.Exists(cwd)) return AgentToolResult.Err("工作目录不存在：" + cwd);
                }

                string ext2 = Path.GetExtension(path).ToLowerInvariant();
                string exe, argPrefix;
                if (!ResolveInterpreter(ext2, out exe, out argPrefix))
                {
                    if (tempScript) { try { File.Delete(path); } catch { } }
                    return AgentToolResult.Err("不支持的脚本扩展名：" + ext2 + "（可用 .bat/.cmd/.ps1/.py/.js/.mjs/.vbs/.sh）");
                }

                string preview = "执行脚本";
                if (!string.IsNullOrEmpty(extraArgs)) preview += "\n参数：" + extraArgs;
                if (tempScript)
                {
                    string head = content.Length > 400 ? (content.Substring(0, 400) + "\n...（共 " + content.Length + " 字符）") : content;
                    preview += "\n\n--- 脚本内容 ---\n" + head;
                }
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", preview))
                {
                    if (tempScript) { try { File.Delete(path); } catch { } }
                    return AgentToolResult.Err("用户拒绝执行脚本");
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = argPrefix + (argPrefix.Length > 0 ? " " : "") + Quote(path) + (extraArgs.Length > 0 ? " " + extraArgs : ""),
                        WorkingDirectory = !string.IsNullOrEmpty(cwd) ? cwd : Path.GetDirectoryName(path),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    // Python 强制 UTF-8 输出/文件默认编码；其余输出由 C# 端按行自动识别 UTF-8/GBK
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    psi.EnvironmentVariables["PYTHONUTF8"] = "1";

                    using (var p = Process.Start(psi))
                    {
                        if (p == null) return AgentToolResult.Err("脚本进程启动失败");
                        // 读原始字节按行自动识别编码（cmd/部分程序输出 GBK，python/node 输出 UTF-8）
                        string text = RunAndCapture(p, timeoutSec * 1000);
                        // 记录本回合实际执行的脚本（path 已解析为绝对路径，含临时脚本）
                        _log?.RecordExecuted(path);
                        if (tempScript) text = "（临时脚本 " + path + "）\n" + text;
                        return AgentToolResult.Ok(text);
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("脚本执行失败：" + ex.Message); }
            }

            private static string NormalizeExt(string language)
            {
                if (string.IsNullOrWhiteSpace(language)) return "";
                string l = language.Trim().TrimStart('.').ToLowerInvariant();
                switch (l)
                {
                    case "bat": return ".bat";
                    case "cmd": return ".cmd";
                    case "ps1": case "powershell": return ".ps1";
                    case "py": case "python": case "python3": return ".py";
                    case "js": case "node": case "mjs": return ".js";
                    case "vbs": case "vbscript": return ".vbs";
                    case "sh": case "bash": case "shell": return ".sh";
                }
                return "";
            }

            private static bool ResolveInterpreter(string ext, out string exe, out string argPrefix)
            {
                exe = null; argPrefix = "";
                switch (ext)
                {
                    case ".bat":
                    case ".cmd":
                        // 重定向管道无控制台、chcp 不生效；输出编码由 C# 端读原始字节按行识别
                        exe = "cmd.exe"; argPrefix = "/d /c"; return true;
                    case ".ps1":
                        exe = "powershell.exe";
                        argPrefix = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File";
                        return true;
                    case ".py":
                        exe = FindOnPath("python.exe") ?? FindOnPath("py.exe");
                        return exe != null;
                    case ".js":
                    case ".mjs":
                        exe = FindOnPath("node.exe") ?? FindOnPath("node.cmd");
                        return exe != null;
                    case ".vbs":
                        exe = "cscript.exe"; argPrefix = "//nologo"; return true;
                    case ".sh":
                        exe = FindOnPath("bash.exe");
                        return exe != null;
                }
                return false;
            }

            /// <summary>在 PATH 里找可执行文件（找不到返回 null，调用方据此判定缺解释器）</summary>
            private static string FindOnPath(string fileName)
            {
                try
                {
                    string pathVar = Environment.GetEnvironmentVariable("PATH");
                    if (string.IsNullOrEmpty(pathVar)) return null;
                    foreach (string dir in pathVar.Split(';'))
                    {
                        if (string.IsNullOrWhiteSpace(dir)) continue;
                        try
                        {
                            string full = Path.Combine(dir.Trim(), fileName);
                            if (File.Exists(full)) return full;
                        }
                        catch { }
                    }
                }
                catch { }
                return null;
            }

            private static string Quote(string s)
            {
                return "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";
            }
        }
    }
}