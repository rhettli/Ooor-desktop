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
            Add(new SearchInFilesTool(options));
            Add(new GetTimeTool());
            Add(new GetEnvironmentTool());
            Add(new GetAppPathsTool());
            Add(new GetAppInfoTool());
            Add(new ListModelsTool());
            Add(new ListLlamaVersionsTool());

            // ---- 写入类：AllowWrite 开启才注册 ----
            if (options.AllowWrite)
            {
                Add(new WriteFileTool(options, confirm, TurnLog));
                Add(new CreateFileTool(options, confirm, TurnLog));
                Add(new EditFileTool(options, confirm, TurnLog));
                Add(new DeleteFileTool(options, confirm));
                Add(new MoveFileTool(options, confirm, TurnLog));
                Add(new CopyFileTool(options, confirm, TurnLog));
                Add(new MakeDirectoryTool(options, confirm));
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
                Add(new DownloadFileTool(options, TurnLog));
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
            public string Description
            {
                get
                {
                    return "读取文本文件内容（UTF-8）。两种读法：① 不传 offset/limit 时整文件读取，文件大小受 ReadMaxBytes 限制，超报错；" +
                           "② 传 offset（起始行号，从 1 开始）/ limit（读取行数，默认 2000）按行分页读取，大文件必须用这种方式，逐段读完整个文件。" +
                           "分页结果每行前带行号。另有 max_bytes 可收紧整文件读取上限。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "文件绝对路径或相对沙盒根的路径" } } },
                                { "offset", new Dictionary<string, object> { { "type", "integer" }, { "description", "起始行号（从 1 开始）。传了它或 limit 就按行分页读取，适合大文件" } } },
                                { "limit", new Dictionary<string, object> { { "type", "integer" }, { "description", "分页读取的最大行数，默认 2000" } } },
                                { "max_bytes", new Dictionary<string, object> { { "type", "integer" }, { "description", "整文件读取（不分页）时的上限（字节），默认走 Options.ReadMaxBytes" } } }
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

                int offset = AsInt(args, "offset", 0, 0, 100000000);
                int limit = AsInt(args, "limit", 0, 0, 10000);
                bool paged = offset > 0 || limit > 0;

                if (paged) return ReadPaged(p, offset > 0 ? offset : 1, limit > 0 ? limit : 2000);

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
                    return AgentToolResult.Err("文件过大（" + len + " bytes > 上限 " + cap +
                        "），请改用 offset/limit 按行分页读取");

                try
                {
                    string text = File.ReadAllText(p, new UTF8Encoding(false));
                    return AgentToolResult.Ok(text);
                }
                catch (Exception ex) { return AgentToolResult.Err("读取失败：" + ex.Message); }
            }

            /// <summary>
            /// 按行分页读取：File.ReadLines 流式枚举（不会整文件载入内存），跳过 startLine 之前的行；
            /// 每行前加「行号: 」前缀，超长行截断；达到行数上限或输出字节上限时标注还有后续。
            /// </summary>
            private AgentToolResult ReadPaged(string p, int startLine, int takeLines)
            {
                var sb = new StringBuilder();
                int shown = 0;
                long bytes = 0;
                bool more = false;
                try
                {
                    int lineNo = 0;
                    foreach (string raw in File.ReadLines(p, new UTF8Encoding(false)))
                    {
                        lineNo++;
                        if (lineNo < startLine) continue;
                        if (shown >= takeLines || bytes > _opt.ReadMaxBytes) { more = true; break; }

                        string line = raw;
                        if (line.Length > 500) line = line.Substring(0, 500) + "…（本行过长已截断）";
                        sb.Append(lineNo).Append(": ").Append(line).Append('\n');
                        bytes += line.Length + 16;
                        shown++;
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("读取失败：" + ex.Message); }

                if (shown == 0)
                    return AgentToolResult.Ok("（第 " + startLine + " 行起没有内容：文件没有这么多行）");
                if (more)
                    sb.Append("…（还有更多内容：增大 limit，或把 offset 设为 ").Append(startLine + shown).Append(" 继续读）");
                return AgentToolResult.Ok(sb.ToString().TrimEnd('\n'));
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

        /// <summary>
        /// 按【文件内容】递归搜索（search_files 只按文件名找，本工具是 Grep 等价物）。
        /// 自动跳过二进制文件（扩展名黑名单 + 文件头 NUL 字节嗅探）和 .git 目录；
        /// 单文件只扫前 PerFileCap 字节，输出按「文件:行号:内容」逐行返回，命中/输出超限标注截断。
        /// </summary>
        private sealed class SearchInFilesTool : IAgentTool
        {
            public string Name { get { return "search_in_files"; } }
            public string Description
            {
                get
                {
                    return "在目录下递归搜索【文件内容】（search_files 只按文件名查找，找代码/文字内容用本工具）。" +
                           "逐行返回「文件路径:行号:匹配行」。pattern 为要找的文本；regex=true 时 pattern 按正则表达式解析；" +
                           "glob 可限定文件名通配符（如 *.cs、*.txt，默认全部）；ignore_case 默认 true。" +
                           "自动跳过二进制文件和 .git 目录。命中过多会截断，可缩小 path 范围或加 glob 再搜。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "搜索根目录（必须在沙盒内）" } } },
                                { "pattern", new Dictionary<string, object> { { "type", "string" }, { "description", "要查找的文本；regex=true 时为正则表达式" } } },
                                { "glob", new Dictionary<string, object> { { "type", "string" }, { "description", "文件名通配符过滤，如 *.cs、*.py；默认 *（全部文件）" } } },
                                { "regex", new Dictionary<string, object> { { "type", "boolean" }, { "description", "pattern 是否按正则表达式解析，默认 false（普通子串匹配）" } } },
                                { "ignore_case", new Dictionary<string, object> { { "type", "boolean" }, { "description", "是否不区分大小写，默认 true" } } },
                                { "max_results", new Dictionary<string, object> { { "type", "integer" }, { "description", "最多返回的匹配行数，1-500，默认 100" } } }
                            }
                        },
                        { "required", new[] { "path", "pattern" } }
                    };
                }
            }

            /// <summary>单文件最多扫描的字节数（防止 GB 级日志把一轮对话吃光）</summary>
            private const int PerFileCap = 2 * 1024 * 1024;
            /// <summary>本轮最多扫描的文件数</summary>
            private const int MaxFilesScan = 5000;
            /// <summary>返回文本的字符上限（超出截断）</summary>
            private const int MaxOutputChars = 30000;

            private static readonly HashSet<string> BinaryExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".exe",".dll",".so",".dylib",".pdb",".class",".jar",".pyc",".wasm",".bin",".dat",
                ".zip",".7z",".rar",".tar",".gz",".bz2",".xz",".cab",".msi",
                ".png",".jpg",".jpeg",".gif",".bmp",".ico",".webp",".tiff",".svgz",
                ".mp3",".mp4",".avi",".mkv",".mov",".wav",".flac",".ogg",
                ".pdf",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".iso",".vmdk"
            };

            private readonly AgentOptions _opt;
            public SearchInFilesTool(AgentOptions opt) { _opt = opt; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                string pattern = AsString(args, "pattern", true);
                string glob = AsString(args, "glob", false);
                bool useRegex = AsBool(args, "regex", false);
                bool ignoreCase = AsBool(args, "ignore_case", true);
                int maxResults = AsInt(args, "max_results", 100, 1, 500);
                if (string.IsNullOrEmpty(glob)) glob = "*";

                p = _opt.ResolveAllowed(p);
                if (!Directory.Exists(p)) return AgentToolResult.Err("目录不存在：" + p);

                // 编译匹配器：正则非法时直接报错，让模型修正后重试
                Func<string, bool> match;
                if (useRegex)
                {
                    Regex rx;
                    try { rx = new Regex(pattern, RegexOptions.Compiled | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)); }
                    catch (ArgumentException ex) { return AgentToolResult.Err("正则表达式不合法：" + ex.Message); }
                    match = line => rx.IsMatch(line);
                }
                else
                {
                    var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                    match = line => line.IndexOf(pattern, cmp) >= 0;
                }

                string globRe = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";

                var sb = new StringBuilder();
                int hits = 0, filesScanned = 0, filesSkipped = 0;
                bool truncated = false;

                foreach (string f in EnumerateFilesSafe(p))
                {
                    if (filesScanned >= MaxFilesScan) { truncated = true; break; }

                    string name;
                    try { name = Path.GetFileName(f); } catch { continue; }
                    if (!Regex.IsMatch(name, globRe, RegexOptions.IgnoreCase)) continue;
                    if (BinaryExt.Contains(Path.GetExtension(f))) { filesSkipped++; continue; }
                    if (LooksBinary(f)) { filesSkipped++; continue; }

                    filesScanned++;
                    string content;
                    try
                    {
                        // 读原始字节（单文件封顶），复用按行 UTF-8/GBK 自动识别解码
                        byte[] data = ReadCappedBytes(f, PerFileCap);
                        content = DecodeMixedOutput(data);
                    }
                    catch { continue; }

                    string[] lines = content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        if (line.Length > 0 && line[line.Length - 1] == '\r') line = line.Substring(0, line.Length - 1);
                        if (!match(line)) continue;

                        string shown = line.Length > 400 ? line.Substring(0, 400) + "…" : line;
                        sb.Append(f).Append(':').Append(i + 1).Append(": ").Append(shown).Append('\n');
                        hits++;
                        if (hits >= maxResults || sb.Length > MaxOutputChars) { truncated = true; break; }
                    }
                    if (truncated) break;
                }

                if (hits == 0)
                {
                    string extra = filesScanned == 0 && filesSkipped > 0 ? "（扫描范围内只有二进制文件，已跳过 " + filesSkipped + " 个）" : "（无命中）";
                    return AgentToolResult.Ok(extra);
                }

                var head = new StringBuilder();
                head.Append("（扫描 ").Append(filesScanned).Append(" 个文本文件");
                if (filesSkipped > 0) head.Append("，跳过 ").Append(filesSkipped).Append(" 个二进制文件");
                head.Append("，命中 ").Append(hits).Append(" 行）\n");
                if (truncated) head.Append("…（结果已截断，请缩小 path 范围、加 glob 或提高 max_results 再搜）\n");
                return AgentToolResult.Ok(head + sb.ToString().TrimEnd('\n'));
            }

            /// <summary>安全的递归文件枚举：无权限目录跳过；.git 不进入。</summary>
            private static IEnumerable<string> EnumerateFilesSafe(string dir)
            {
                string[] files, subdirs;
                try { files = Directory.GetFiles(dir); } catch { yield break; }
                try { subdirs = Directory.GetDirectories(dir); } catch { subdirs = new string[0]; }

                foreach (string f in files) yield return f;
                foreach (string d in subdirs)
                {
                    string dn;
                    try { dn = Path.GetFileName(d); } catch { continue; }
                    if (string.Equals(dn, ".git", StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (string f in EnumerateFilesSafe(d)) yield return f;
                }
            }

            /// <summary>读文件前 cap 字节（超出截断，扫描场景不需要尾部）。</summary>
            private static byte[] ReadCappedBytes(string path, int cap)
            {
                using (var fs = File.OpenRead(path))
                {
                    int n = (int)Math.Min(cap, fs.Length);
                    var buf = new byte[n];
                    int read = 0;
                    while (read < n)
                    {
                        int r = fs.Read(buf, read, n - read);
                        if (r <= 0) break;
                        read += r;
                    }
                    if (read < n) Array.Resize(ref buf, read);
                    return buf;
                }
            }

            /// <summary>嗅探文件头 4KB：含 NUL 字节视为二进制（UTF-16 文本也会命中，跳过可接受）。</summary>
            private static bool LooksBinary(string path)
            {
                try
                {
                    using (var fs = File.OpenRead(path))
                    {
                        int n = (int)Math.Min(4096, fs.Length);
                        var buf = new byte[n];
                        int read = 0;
                        while (read < n)
                        {
                            int r = fs.Read(buf, read, n - read);
                            if (r <= 0) break;
                            read += r;
                        }
                        for (int i = 0; i < read; i++) if (buf[i] == 0) return true;
                    }
                }
                catch { return true; }
                return false;
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

        /// <summary>领域只读工具：列出本机模型库（内置模型目录下的 .gguf，含大小与多模态投影标注）。</summary>
        private sealed class ListModelsTool : IAgentTool
        {
            public string Name { get { return "list_models"; } }
            public string Description
            {
                get
                {
                    return "列出 ooor 本机模型库：" + CoreEnv.ModelsDir + " 下的全部 .gguf 文件（含大小，标注多模态投影 mmproj 文件）。" +
                           "可选 keyword 按文件名过滤。管理器中引用的外部目录不在扫描范围；选择要用的模型后，相关信息可从 get_app_paths / list_directory 获取。";
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
                                { "keyword", new Dictionary<string, object> { { "type", "string" }, { "description", "文件名过滤关键词（不区分大小写的包含匹配），可省略" } } }
                            }
                        }
                    };
                }
            }
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string root = CoreEnv.ModelsDir;
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return AgentToolResult.Err("模型目录不存在：" + root + "（可在主界面设置模型目录，或把 .gguf 放入该目录）");

                string keyword = (AsString(args, "keyword", false) ?? "").Trim();
                var found = new List<string>();
                try
                {
                    foreach (string f in Directory.EnumerateFiles(root, "*.gguf", SearchOption.AllDirectories))
                    {
                        if (keyword.Length > 0 &&
                            f.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        found.Add(f);
                        if (found.Count >= 300) break;   // 防异常目录拖垮输出
                    }
                }
                catch (Exception ex) { return AgentToolResult.Err("扫描模型目录失败：" + ex.Message); }

                if (found.Count == 0)
                    return AgentToolResult.Err(keyword.Length > 0
                        ? "模型目录中没有匹配 \"" + keyword + "\" 的 .gguf 文件：" + root
                        : "模型目录是空的：" + root);

                var sb = new StringBuilder();
                sb.Append("模型目录：").Append(root).Append('\n');
                if (keyword.Length > 0) sb.Append("过滤关键词：").Append(keyword).Append('\n');
                long total = 0;
                for (int i = 0; i < found.Count; i++)
                {
                    string f = found[i];
                    long len = 0;
                    try { len = new FileInfo(f).Length; } catch { }
                    total += len;
                    string rel;
                    try { rel = f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
                    catch { rel = f; }
                    string tag = Path.GetFileName(f).StartsWith("mmproj", StringComparison.OrdinalIgnoreCase)
                        ? "　[多模态投影]" : "";
                    sb.Append(i + 1).Append(". ").Append(rel).Append("　").Append(FormatBytes(len)).Append(tag).Append('\n');
                }
                sb.Append("（共 ").Append(found.Count).Append(" 个文件，合计 ").Append(FormatBytes(total)).Append('）');
                return AgentToolResult.Ok(sb.ToString());
            }

            private static string FormatBytes(long b)
            {
                if (b >= 1073741824L) return (b / 1073741824.0).ToString("F2") + " GB";
                if (b >= 1048576) return (b / 1048576.0).ToString("F1") + " MB";
                if (b >= 1024) return (b / 1024.0).ToString("F1") + " KB";
                return b + " B";
            }
        }

        /// <summary>领域只读工具：列出已下载的 llama.cpp 运行版本与当前选中版本。</summary>
        private sealed class ListLlamaVersionsTool : IAgentTool
        {
            public string Name { get { return "list_llama_versions"; } }
            public string Description
            {
                get
                {
                    return "列出已下载的 llama.cpp 运行版本（llama-bin 目录下的子目录，标注哪个含 llama-server.exe）与当前选中版本。无参数。" +
                           "只做查看；切换版本请在主界面操作。";
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
            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string root = CoreEnv.LlamaBinsDir;
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    return AgentToolResult.Err("llama 版本目录不存在：" + root + "（尚未下载过任何版本）");

                string selected = null;
                try { selected = CoreEnv.GetSelectedVersion(); } catch { }

                string[] dirs;
                try { dirs = Directory.GetDirectories(root); }
                catch (Exception ex) { return AgentToolResult.Err("扫描版本目录失败：" + ex.Message); }
                Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);

                if (dirs.Length == 0)
                    return AgentToolResult.Err("llama 版本目录是空的：" + root);

                var sb = new StringBuilder();
                sb.Append("版本目录：").Append(root).Append('\n');
                foreach (string d in dirs)
                {
                    string name = Path.GetFileName(d);
                    bool hasServer = false;
                    try { hasServer = File.Exists(Path.Combine(d, "llama-server.exe")); } catch { }
                    sb.Append("- ").Append(name)
                      .Append(hasServer ? "　[含 llama-server]" : "")
                      .Append(name == selected ? "　← 当前选中" : "")
                      .Append('\n');
                }
                sb.Append("当前选中版本：").Append(string.IsNullOrEmpty(selected) ? "(未选择)" : selected);
                return AgentToolResult.Ok(sb.ToString());
            }
        }

        private sealed class WriteFileTool : IAgentTool
        {
            public string Name { get { return "write_file"; } }
            public string Description { get { return "把文本写入文件（覆盖已有文件，UTF-8；文件不存在会自动创建）。高危：默认弹窗请用户确认。"; } }
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
                                { "confirm", ConfirmParamSchema() },
                                { "content", new Dictionary<string, object> { { "type", "string" } } }
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

        /// <summary>
        /// 局部精确替换：只改 old_text 命中的片段，不必整文件重写（省 token、避免误伤其他内容）。
        /// old_text 必须在文件中唯一出现；多处都要改时传 replace_all=true。UTF-8 读写。
        /// </summary>
        private sealed class EditFileTool : IAgentTool
        {
            public string Name { get { return "edit_file"; } }
            public string Description
            {
                get
                {
                    return "对已存在文件做精确的局部文本替换（只改命中片段，不需要重写整个文件，适合改代码）。" +
                           "old_text 必须与文件内容【完全一致】（含缩进换行）且在文件中唯一出现；匹配不到或匹配到多处都会报错；" +
                           "确实要全部替换时传 replace_all=true。new_text 为空字符串表示删除该片段。UTF-8。高危：默认弹窗请用户确认。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "要修改的文件（绝对路径或相对沙盒根的路径）" } } },
                                { "old_text", new Dictionary<string, object> { { "type", "string" }, { "description", "要被替换的原文，必须与文件中内容完全一致（含缩进、换行）" } } },
                                { "new_text", new Dictionary<string, object> { { "type", "string" }, { "description", "替换后的文本；传空字符串表示删除 old_text" } } },
                                { "replace_all", new Dictionary<string, object> { { "type", "boolean" }, { "description", "是否替换全部命中处，默认 false（要求唯一命中）" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path", "old_text" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public EditFileTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p = AsString(args, "path", true);
                string oldText = AsString(args, "old_text", true);
                string newText = AsString(args, "new_text", false) ?? "";
                bool replaceAll = AsBool(args, "replace_all", false);
                bool wantConfirm = AsBool(args, "confirm", false);
                p = _opt.ResolveAllowed(p);

                if (!File.Exists(p)) return AgentToolResult.Err("文件不存在：" + p);
                if (oldText.Length == 0) return AgentToolResult.Err("old_text 不能为空");

                string text;
                try { text = File.ReadAllText(p, new UTF8Encoding(false)); }
                catch (Exception ex) { return AgentToolResult.Err("读取失败：" + ex.Message); }

                // 统计出现次数（逐次 IndexOf，大小写敏感，要求模型给精确原文）
                int count = 0;
                int idx = -1;
                int searchFrom = 0;
                while (true)
                {
                    int found = text.IndexOf(oldText, searchFrom, StringComparison.Ordinal);
                    if (found < 0) break;
                    if (idx < 0) idx = found;
                    count++;
                    searchFrom = found + oldText.Length;
                }

                if (count == 0)
                    return AgentToolResult.Err("文件中找不到与 old_text 完全一致的片段（注意缩进、换行、标点必须完全一致），请先 read_file 核对");
                if (count > 1 && !replaceAll)
                    return AgentToolResult.Err("old_text 在文件中出现了 " + count + " 处，不唯一。请加长 old_text 保证唯一，或传 replace_all=true 全部替换");

                string preview = "修改文件：" + p + "\n替换处数：" + (replaceAll ? count.ToString() : "1") +
                                 "\n\n--- 原文 ---\n" + Clip(oldText) +
                                 "\n\n--- 改为 ---\n" + Clip(newText);
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", preview))
                    return AgentToolResult.Err("用户拒绝修改");

                try
                {
                    string result = replaceAll
                        ? text.Replace(oldText, newText)
                        : text.Substring(0, idx) + newText + text.Substring(idx + oldText.Length);
                    File.WriteAllText(p, result, new UTF8Encoding(false));
                    _log?.RecordWritten(p);
                    return AgentToolResult.Ok("已替换 " + (replaceAll ? count + " 处" : "1 处") + "：" + p);
                }
                catch (Exception ex) { return AgentToolResult.Err("写入失败：" + ex.Message); }
            }

            /// <summary>确认框预览过长时截断，避免弹窗撑爆屏幕。</summary>
            private static string Clip(string s)
            {
                if (string.IsNullOrEmpty(s)) return "（空）";
                return s.Length > 800 ? s.Substring(0, 800) + "…（共 " + s.Length + " 字符）" : s;
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

        private sealed class MoveFileTool : IAgentTool
        {
            public string Name { get { return "move_file"; } }
            public string Description
            {
                get
                {
                    return "移动或重命名文件（不支持移动目录；目标已存在会报错，不会覆盖）。" +
                           "源路径和目标路径都必须在沙盒白名单内（先调用 list_roots 确认）；" +
                           "相对路径会自动落到临时工作目录 temp 下。高危：默认弹窗请用户确认。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "源文件路径（沙盒白名单内）" } } },
                                { "dest", new Dictionary<string, object> { { "type", "string" }, { "description", "目标路径（须在沙盒白名单内，且不能已存在）" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path", "dest" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public MoveFileTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string src;
                string dst;
                try
                {
                    src = _opt.ResolveAllowed(AsString(args, "path", true));
                    dst = _opt.ResolveAllowed(AsString(args, "dest", true));
                }
                catch (Exception ex) { return AgentToolResult.Err(ex.Message); }
                if (!File.Exists(src)) return AgentToolResult.Err("源文件不存在：" + src);
                if (File.Exists(dst)) return AgentToolResult.Err("目标已存在，未做修改：" + dst + "（如需替换请先删除目标文件）");

                bool wantConfirm = AsBool(args, "confirm", false);
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", "移动文件：\n" + src + "\n→ " + dst))
                    return AgentToolResult.Err("用户拒绝移动文件");

                try
                {
                    string dir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Move(src, dst);
                    _log?.RecordWritten(dst);
                    return AgentToolResult.Ok("已移动：" + src + "\n→ " + dst);
                }
                catch (Exception ex) { return AgentToolResult.Err("移动失败：" + ex.Message); }
            }
        }

        private sealed class CopyFileTool : IAgentTool
        {
            public string Name { get { return "copy_file"; } }
            public string Description
            {
                get
                {
                    return "复制文件（不支持复制目录）。源路径和目标路径都必须在沙盒白名单内（先调用 list_roots 确认）；" +
                           "目标已存在时须传 overwrite=true 才会覆盖。相对路径会自动落到临时工作目录 temp 下。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "源文件路径（沙盒白名单内）" } } },
                                { "dest", new Dictionary<string, object> { { "type", "string" }, { "description", "目标路径（须在沙盒白名单内）" } } },
                                { "overwrite", new Dictionary<string, object> { { "type", "boolean" }, { "description", "目标已存在时是否覆盖，默认 false（直接报错）" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path", "dest" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            private readonly AgentTurnLog _log;
            public CopyFileTool(AgentOptions opt, AgentConfirm confirm, AgentTurnLog log)
            { _opt = opt; _confirm = confirm; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string src;
                string dst;
                try
                {
                    src = _opt.ResolveAllowed(AsString(args, "path", true));
                    dst = _opt.ResolveAllowed(AsString(args, "dest", true));
                }
                catch (Exception ex) { return AgentToolResult.Err(ex.Message); }
                if (!File.Exists(src)) return AgentToolResult.Err("源文件不存在：" + src);
                bool overwrite = AsBool(args, "overwrite", false);
                if (File.Exists(dst) && !overwrite)
                    return AgentToolResult.Err("目标已存在，未做修改：" + dst + "（确需覆盖请传 overwrite=true）");

                bool wantConfirm = AsBool(args, "confirm", false);
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？",
                        "复制文件：\n" + src + "\n→ " + dst + (File.Exists(dst) ? "（覆盖已有文件）" : "")))
                    return AgentToolResult.Err("用户拒绝复制文件");

                try
                {
                    string dir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Copy(src, dst, overwrite);
                    _log?.RecordWritten(dst);
                    return AgentToolResult.Ok("已复制：" + src + "\n→ " + dst + "（" + new FileInfo(dst).Length + " 字节）");
                }
                catch (Exception ex) { return AgentToolResult.Err("复制失败：" + ex.Message); }
            }
        }

        private sealed class MakeDirectoryTool : IAgentTool
        {
            public string Name { get { return "make_directory"; } }
            public string Description
            {
                get
                {
                    return "创建目录（多级一次创建；已存在时直接成功，不报错）。路径须在沙盒白名单内（先调用 list_roots 确认）；" +
                           "相对路径会自动落到临时工作目录 temp 下。";
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
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "要创建的目录路径（沙盒白名单内）" } } },
                                { "confirm", ConfirmParamSchema() }
                            }
                        },
                        { "required", new[] { "path" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentConfirm _confirm;
            public MakeDirectoryTool(AgentOptions opt, AgentConfirm confirm) { _opt = opt; _confirm = confirm; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string p;
                try { p = _opt.ResolveAllowed(AsString(args, "path", true)); }
                catch (Exception ex) { return AgentToolResult.Err(ex.Message); }

                if (Directory.Exists(p)) return AgentToolResult.Ok("目录已存在：" + p);

                bool wantConfirm = AsBool(args, "confirm", false);
                if (!AskConfirm(_opt, _confirm, wantConfirm, "请您确认是否继续执行敏感操作？", "创建目录：" + p))
                    return AgentToolResult.Err("用户拒绝创建目录");

                try
                {
                    Directory.CreateDirectory(p);
                    return AgentToolResult.Ok("已创建目录：" + p);
                }
                catch (Exception ex) { return AgentToolResult.Err("创建失败：" + ex.Message); }
            }
        }

        private sealed class RunCommandTool : IAgentTool
        {
            public string Name { get { return "run_command"; } }
            public string Description
            {
                get
                {
                    return "在 PowerShell 里执行一条命令（同步等待、捕获输出、超时强杀）。当前目录固定为沙盒临时工作目录 "
                         + _opt.TempDir + "（即 create_file 相对路径的落地目录），命令里用相对路径即可操作其中文件。"
                         + "注意：这是 PowerShell 宿主，语法与 cmd 不同——环境变量用 $env:NAME（不是 %NAME%），"
                         + "命令串联用 ; 或换行（PS 5.1 不支持 &&），打开文件/网址用 Start-Process " + "\"路径\"" + "（不是 start），"
                         + "管道传递的是对象不是纯文本；需要跑多行脚本请改用 execute_script。";
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
                int timeoutSec = (int)_opt.CommandTimeout.TotalSeconds;
                object ts;
                if (args != null && args.TryGetValue("timeout_seconds", out ts) && ts != null)
                {
                    int v;
                    if (int.TryParse(ts.ToString(), out v) && v > 0 && v < 600) timeoutSec = v;
                }

                // 粗粒度黑名单：极易误操作的命令在 UI 端弹窗时让用户二次确认
                // 覆盖 cmd 风格（format / del /s）与 PowerShell 风格（Remove-Item -Recurse / Stop-Computer）
                string lower = (cmd ?? "").ToLowerInvariant();
                bool highRisk =
                    lower.Contains("format ") || lower.Contains("rmdir /s") || lower.Contains("rd /s") ||
                    lower.Contains("del /f /s") || lower.Contains("del /s /q") || lower.Contains("reg delete") ||
                    lower.Contains("net user") || lower.Contains("net stop") || lower.Contains("bcdedit") ||
                    lower.Contains("diskpart") || lower.Contains("shutdown") || lower.Contains("cipher /w") ||
                    lower.Contains("remove-item -recurse") || lower.Contains("rm -recurse") ||
                    lower.Contains("del -recurse") || lower.Contains("stop-computer") ||
                    lower.Contains("restart-computer") || lower.Contains("format-volume") ||
                    lower.Contains("clear-disk") || lower.Contains("stop-service") || lower.Contains("stop-process -force");
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
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -",
                        WorkingDirectory = workDir ?? "",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true
                    };
                    // 命令里若调用 python：让其 stdio/文件默认走 UTF-8（输出由 C# 端按行自动识别编码）
                    psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                    psi.EnvironmentVariables["PYTHONUTF8"] = "1";
                    using (var p = Process.Start(psi))
                    {
                        if (p == null) return AgentToolResult.Err("进程启动失败");
                        // 通过 stdin 传入命令（-Command - 模式）：避免 -Command "脚本" 的引号转义问题，
                        // 命令字符串原封不动地交给 PS 解析器，管道、引号、$ 符号都按 PS 语义处理
                        try
                        {
                            // PowerShell 5.1 默认按 UTF-16 读 stdin；写 UTF-8 会乱码。
                            // 用 ASCII 写入命令文本（命令本身通常是 ASCII，中文参数少见；
                            // 若含中文，PS stdin 编码限制是已知问题，建议改用 execute_script 落文件再跑）
                            var stdinEnc = new UTF8Encoding(false);
                            byte[] cmdBytes = stdinEnc.GetBytes(cmd + "\n");
                            p.StandardInput.BaseStream.Write(cmdBytes, 0, cmdBytes.Length);
                            p.StandardInput.BaseStream.Flush();
                            p.StandardInput.Close();
                        }
                        catch { }
                        // 读原始字节并按行自动识别 UTF-8/GBK：PS 输出默认 UTF-16，
                        // 但 python 子进程仍是 UTF-8；按行识别能兼容两者混排
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
                                { "overwrite", new Dictionary<string, object> { { "type", "boolean" }, { "description", "目标已存在时是否覆盖，默认 false（直接报错）" } } },
                                { "confirm", new Dictionary<string, object> { { "type", "boolean" }, { "description", "是否先请用户确认，默认 false（新建文件通常无需打扰用户）" } } },
                                { "content", new Dictionary<string, object> { { "type", "string" }, { "description", "写入的文本内容" } } }
                               
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

        /// <summary>
        /// 下载 URL 到沙盒内文件（流式写盘，适合大文件如 .gguf 模型）。
        /// 与 fetch_url 的区别：fetch_url 只把网页文本回给模型读，本工具把文件落到磁盘。
        /// 仅在用户开启「允许联网」时注册；目标路径受沙盒白名单约束。
        /// </summary>
        private sealed class DownloadFileTool : IAgentTool
        {
            public string Name { get { return "download_file"; } }
            public string Description
            {
                get
                {
                    return "下载一个 http/https 文件到本地（流式写盘，支持大文件如 .gguf / .zip）。" +
                           "与 fetch_url 的区别：fetch_url 返回网页文本供阅读，本工具把文件保存到磁盘。" +
                           "path 只写文件名或相对路径时自动落到临时工作目录 temp 下；目标已存在须传 overwrite=true。" +
                           "下载模型大文件时建议先确认目标磁盘剩余空间，并给足 timeout_seconds。";
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
                                { "url", new Dictionary<string, object> { { "type", "string" }, { "description", "要下载的完整网址（http/https）" } } },
                                { "path", new Dictionary<string, object> { { "type", "string" }, { "description", "保存路径（沙盒白名单内）；省略时自动按 URL 文件名落到临时工作目录 temp 下" } } },
                                { "overwrite", new Dictionary<string, object> { { "type", "boolean" }, { "description", "目标已存在时是否覆盖，默认 false（直接报错）" } } },
                                { "timeout_seconds", new Dictionary<string, object> { { "type", "integer" }, { "description", "超时秒数，默认 600，最大 7200；大文件给足时间" } } }
                            }
                        },
                        { "required", new[] { "url" } }
                    };
                }
            }
            private readonly AgentOptions _opt;
            private readonly AgentTurnLog _log;
            public DownloadFileTool(AgentOptions opt, AgentTurnLog log) { _opt = opt; _log = log; }

            public AgentToolResult Execute(IDictionary<string, object> args)
            {
                string url = (AsString(args, "url", true) ?? "").Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return AgentToolResult.Err("仅支持 http/https 网址：" + url);

                int timeout = AsInt(args, "timeout_seconds", 600, 30, 7200);
                bool overwrite = AsBool(args, "overwrite", false);

                string p = AsString(args, "path", false);
                if (string.IsNullOrEmpty(p))
                {
                    // 未给目标路径：从 URL 推断文件名（去掉 query），落在临时工作目录
                    string name = url;
                    int q = name.IndexOfAny(new[] { '?', '#' });
                    if (q >= 0) name = name.Substring(0, q);
                    name = name.Substring(name.LastIndexOf('/') + 1);
                    if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                        name = "download_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    try { p = Path.Combine(_opt.TempDir, name); } catch { p = name; }
                }

                string dst;
                try { dst = _opt.ResolveAllowed(p); }
                catch (Exception ex) { return AgentToolResult.Err(ex.Message); }
                if (File.Exists(dst) && !overwrite)
                    return AgentToolResult.Err("目标已存在，未做修改：" + dst + "（确需覆盖请传 overwrite=true）");

                try
                {
                    string dir = Path.GetDirectoryName(dst);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    long bytes = WebHttp.DownloadToFile(url, dst, timeout);
                    sw.Stop();

                    // 下载中断/超时会抛异常，走到这里说明完整落盘
                    _log?.RecordWritten(dst);
                    return AgentToolResult.Ok("已下载：" + url + "\n→ " + dst
                        + "\n大小：" + FormatSize(bytes) + "　用时：" + (int)sw.Elapsed.TotalSeconds + " 秒");
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(dst)) File.Delete(dst); } catch { }   // 清掉半截文件
                    return AgentToolResult.Err(ex.Message);
                }
            }

            private static string FormatSize(long bytes)
            {
                if (bytes >= 1073741824L) return (bytes / 1073741824.0).ToString("F2") + " GB";
                if (bytes >= 1048576) return (bytes / 1048576.0).ToString("F1") + " MB";
                if (bytes >= 1024) return (bytes / 1024.0).ToString("F1") + " KB";
                return bytes + " 字节";
            }
        }
    }
}