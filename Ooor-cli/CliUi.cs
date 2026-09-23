using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ooor_cli
{
    /// <summary>
    /// 终端输出支持：VT100 转义序列颜色、Markdown → ANSI 的按行流式渲染、控制台 Y/N 确认。
    /// VT 不可用（旧终端/重定向）时自动退化为纯文本，功能不受影响。
    /// </summary>
    internal static class CliUi
    {
        public const string Reset = "\x1b[0m";
        public const string Bold = "\x1b[1m";
        public const string Italic = "\x1b[3m";
        public const string Gray = "\x1b[90m";
        public const string Cyan = "\x1b[36m";
        public const string Green = "\x1b[32m";
        public const string Yellow = "\x1b[33m";
        public const string Red = "\x1b[91m";
        /// <summary>深灰背景（256 色）：思考里的 `代码` / ``重要引文`` 用。</summary>
        public const string BgCode = "\x1b[48;5;238m";

        /// <summary>VT100 是否可用（不可用时流式渲染器退化为纯文本直出）。</summary>
        public static bool Vt { get { return _vt; } }

        [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] private static extern bool GetConsoleMode(IntPtr hConsole, out uint lpMode);
        [DllImport("kernel32.dll")] private static extern bool SetConsoleMode(IntPtr hConsole, uint dwMode);

        private static bool _vt;

        /// <summary>初始化终端：UTF-8 输出 + 启用 VT100 处理（Win10+）。</summary>
        public static void Init()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                IntPtr h = GetStdHandle(-11);
                uint mode;
                if (GetConsoleMode(h, out mode)) _vt = SetConsoleMode(h, mode | 0x0004);
            }
            catch { _vt = false; }
        }

        /// <summary>带色写一行（VT 不可用时只写文本）。</summary>
        public static void WriteLine(string ansi, string text)
        {
            Console.WriteLine(_vt ? ansi + (text ?? "") + Reset : Sanitize(text));
        }

        /// <summary>带色写一段文本（不换行，流式直出用）；始终剥掉 ESC 防注入。</summary>
        public static void Write(string ansi, string text)
        {
            Console.Write(_vt ? ansi + Sanitize(text) + Reset : Sanitize(text));
        }

        /// <summary>装饰一小段文本（列表符/序号等）：VT 不可用时退化为纯文本。</summary>
        public static string Decor(string ansi, string text)
        {
            return _vt ? ansi + text + Reset : Sanitize(text);
        }

        public static void Info(string text) { WriteLine(Gray, text); }
        public static void Error(string text) { WriteLine(Red, text); }

        /// <summary>REPL 提示符。</summary>
        public static void Prompt()
        {
            Console.Write(_vt ? Green + "Ooor" + Reset + Bold + "> " + Reset : "Ooor> ");
        }

        /// <summary>提示符占用的显示列数（"ooor> " / "Ooor> " 均为 6 列 ASCII，不含颜色转义）。</summary>
        public static int PromptWidth() { return 6; }

        /// <summary>
        /// 高危操作确认（在线程池线程上被 AgentConfirm 回调）：
        /// 打印标题+详情，读 Y/N；Enter=N、Esc=N（与 OOOR 弹窗的默认按钮=否 一致）。
        /// </summary>
        public static bool Confirm(string title, string detail)
        {
            Console.WriteLine();
            WriteLine(Yellow, "⚠ " + (string.IsNullOrEmpty(title) ? CliLang.T("confirmTitle") : title));
            if (!string.IsNullOrEmpty(detail))
            {
                string[] lines = (detail ?? "").Replace("\r\n", "\n").Split('\n');
                int shown = Math.Min(lines.Length, 14);
                for (int i = 0; i < shown; i++) WriteLine(Gray, "  " + lines[i]);
                if (lines.Length > shown) WriteLine(Gray, "  " + CliLang.Tf("truncatedLines", lines.Length));
            }
            while (true)
            {
                Console.Write(_vt ? Yellow + CliLang.T("confirmPrompt") + Reset : CliLang.T("confirmPrompt"));
                ConsoleKeyInfo k = Console.ReadKey(true);
                if (k.Key == ConsoleKey.Y) { Console.WriteLine("Y"); return true; }
                if (k.Key == ConsoleKey.N || k.Key == ConsoleKey.Enter || k.Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("N");
                    return false;
                }
            }
        }

        /// <summary>工具调用展示：黄色一行调用（名称+参数摘要），灰色缩进回显结果（截断）。</summary>
        public static void ToolLine(string name, string argsJson, string result, bool ok)
        {
            string args = argsJson ?? "";
            if (args.Length > 110) args = args.Substring(0, 110) + "…";
            WriteLine(Yellow, (ok ? "⚙ " : CliLang.T("toolFail")) + (name ?? "") + (args.Length > 0 ? " " + args : ""));
            if (!string.IsNullOrEmpty(result))
            {
                string[] lines = result.Replace("\r\n", "\n").Split('\n');
                int shown = Math.Min(lines.Length, 8);
                for (int i = 0; i < shown; i++)
                {
                    string l = lines[i];
                    if (l.Length > 160) l = l.Substring(0, 160) + "…";
                    WriteLine(Gray, "  │ " + l);
                }
                if (lines.Length > shown) WriteLine(Gray, "  │ " + CliLang.Tf("truncatedLines", lines.Length));
            }
        }

        /// <summary>行内 Markdown → ANSI：`代码`（青）、**加粗**、*斜体*；剥掉模型输出里的 ESC 防注入。</summary>
        public static string Inline(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (!_vt) return Sanitize(s);

            var sb = new StringBuilder(s.Length + 32);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\x1b') { i++; continue; }

                if (c == '`')
                {
                    int close = s.IndexOf('`', i + 1);
                    if (close > i)
                    {
                        sb.Append(Cyan).Append(s, i + 1, close - i - 1).Append(Reset);
                        i = close + 1;
                        continue;
                    }
                }
                if (c == '*' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int close = s.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (close > i + 1)
                    {
                        sb.Append(Bold).Append(s, i + 2, close - i - 2).Append(Reset);
                        i = close + 2;
                        continue;
                    }
                }
                if (c == '*')
                {
                    int close = s.IndexOf('*', i + 1);
                    if (close > i + 1)
                    {
                        sb.Append(Italic).Append(s, i + 1, close - i - 1).Append(Reset);
                        i = close + 1;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        /// <summary>剥掉 ESC 等控制字符（VT 关闭时防止模型输出里夹带转义序列）。</summary>
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (c != '\x1b') sb.Append(c);
            return sb.ToString();
        }
    }

    /// <summary>
    /// 等待模型回复时的行内 loading 动画（|/-\ 轮转 + 已等待秒数）。
    ///
    /// 无锁并发协议（依赖“退出屏障”而非互斥锁）：
    ///   - 动画线程每帧拼成一个 string 后“单次 Console.Write”输出（同句柄单次写入串行，帧不会撕裂）；
    ///   - Stop() 置 volatile 标志后 Join 等线程真正退出，再 \r+空格+\r 清掉最后一帧——
    ///     Join 返回后动画线程保证不再碰 Console，之后调用方再输出模型文本，时间上严格串行；
    ///   - Join 最多等一帧的间隙（150ms，按 10ms 分片睡，人眼无感）；
    ///   - Start 只在主线程 REPL 循环里被串行调用；Stop 幂等（_thread==null 直接返回），
    ///     OnStep 每次回调都先 Stop 一次，首个增量到达时动画即结束。
    /// VT 不可用（重定向/旧终端）时退化为静态一行“（思考中… Ctrl+C 可中断）”，不清行不动画。
    /// </summary>
    internal static class Spinner
    {
        // 盲文点阵帧：⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏（10 帧，比 |/-\ 更顺滑）
        private static readonly string[] Frames = { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
        private const int FrameMs = 150;

        private static Thread _thread;
        private static volatile bool _stop;
        private static string _label = "";

        /// <summary>开始转圈。label 建议不超过 10 个字（帧长需显著小于控制台宽度，避免折行）。</summary>
        public static void Start(string label)
        {
            Stop();                                       // 幂等：顺带清掉上一轮的残留
            _label = string.IsNullOrEmpty(label) ? CliLang.T("thinking") : label;
            if (_label.Length > 20) _label = _label.Substring(0, 20);

            if (!CliUi.Vt)
            {
                CliUi.Info("（" + _label + "… Ctrl+C）");   // 退化：静态一行
                return;
            }

            _stop = false;
            _thread = new Thread(Run) { IsBackground = true, Name = "cli-spinner" };
            _thread.Start();
        }

        /// <summary>
        /// 结束动画并清掉当前 loading 行（光标停在行首，随后的模型文本从本行行首打出）。
        /// 幂等：动画未启动时直接返回。正常流/取消/异常三条退出路径都必须走到这里。
        /// </summary>
        public static void Stop()
        {
            Thread t = _thread;
            if (t == null) return;
            _stop = true;
            if (Thread.CurrentThread != t)                 // 防御：不 Join 自己
            {
                try { t.Join(FrameMs * 2 + 100); } catch { }
            }
            _thread = null;
            // 帧最长约 30 列（含中英混排双宽），盖 48 列足够；\r 回行首 + 空格覆盖 + 再 \r
            Console.Write("\r" + new string(' ', 48) + "\r");
        }

        private static void Run()
        {
            var sw = Stopwatch.StartNew();
            int i = 0;
            try
            {
                while (!_stop)
                {
                    string frame = "  " + Frames[i % Frames.Length] + " " + _label
                                  + " " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s";
                    // 单次 Write：整帧（\r 前缀 + 颜色 + 文本）一次性写出，不会与其它输出交错撕半帧
                    Console.Write("\r" + CliUi.Gray + frame + CliUi.Reset);
                    i++;
                    for (int w = 0; w < FrameMs && !_stop; w += 10) Thread.Sleep(10);
                }
            }
            catch { /* 控制台输出异常时动画静默结束，不影响主流程 */ }
        }
    }

    /// <summary>
    /// 流式渲染器（“先裸打、行末替换”策略）：
    /// 增量字符一到就按纯文本立即逐字输出（Markdown 标记暂时可见，思考通道灰色），
    /// 不等 `` ` `` / * 标记闭合；收到换行（或回答结束/通道切换）时，用 \r 回到本行起点、
    /// ESC[J 清掉裸文本，再把整行按 Markdown 渲染后原地重打。
    /// 行宽超出控制台宽度自动折行时，先用 ESC[nA 上移对应视觉行再回车。
    /// VT 不可用（旧终端/重定向）时无法回擦，退化为纯文本直出，标记保留可见。
    /// 回调发生在线程池线程上。
    /// </summary>
    internal sealed class StreamRenderer
    {
        private static readonly Regex RxHeading = new Regex(@"^(#{1,6}) (.*)$", RegexOptions.Compiled);
        private static readonly Regex RxBullet = new Regex(@"^([-+*]) (.*)$", RegexOptions.Compiled);
        private static readonly Regex RxOrdered = new Regex(@"^(\d{1,3})\. (.*)$", RegexOptions.Compiled);

        private readonly StringBuilder _line = new StringBuilder();   // 当前逻辑行已收到的裸文本（不含换行）
        private readonly StringBuilder _chunk = new StringBuilder();  // 待立即裸打的一小段

        private bool _hasChannel;
        private bool _reasoning;          // 当前通道：true=思考（灰），false=正文
        private bool _inCodeBlock;        // ``` 代码块内

        private int _col;                 // 裸文本打印后光标所在列（0 起，ANSI 序列不占列）
        private int _wraps;               // 当前行因超宽产生的自动折行数
        private int _width = 80;          // 控制台列宽（每行开始时刷新）

        /// <summary>喂入一段增量（reasoning=true 思考过程；false 正文 Markdown）。</summary>
        public void Feed(bool reasoning, string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            if (_hasChannel && _reasoning != reasoning) FinalizeLine(true);
            _reasoning = reasoning;
            _hasChannel = true;

            for (int k = 0; k < delta.Length; k++)
            {
                char c = delta[k];
                if (c == '\r') continue;
                if (c == '\n') { FlushChunk(); FinalizeLine(true); continue; }

                // 行首：代码块内立即打灰色缩进前缀（行末渲染时会被┆/│版本替换）
                if (_line.Length == 0 && _col == 0)
                {
                    TryGetWidth();
                    if (_inCodeBlock)
                    {
                        CliUi.Write(CliUi.Gray, "  │ ");
                        _col = 4;
                    }
                }

                char next = k + 1 < delta.Length ? delta[k + 1] : '\0';
                bool pair = char.IsHighSurrogate(c) && char.IsLowSurrogate(next);
                int cp = pair ? char.ConvertToUtf32(c, next) : c;
                int w = c == '\t' ? 8 - (_col % 8) : CodepointWidth(cp);

                _line.Append(c);
                _chunk.Append(c);
                if (pair) { _line.Append(next); _chunk.Append(next); k++; }
                Advance(w);

                if (_chunk.Length >= 16) FlushChunk();
            }
            FlushChunk();
        }

        /// <summary>冲掉挂起的半行：原地渲染替换，不补换行（换行由调用方自行输出）。</summary>
        public void Flush()
        {
            FinalizeLine(false);
        }

        /// <summary>
        /// 结束当前未完成行：若光标在行中，先按样式重打该行（擦掉裸打版）再换行，光标落到新行行首；
        /// 已在行首则原样返回。用于在流中途插入“外部行”（如统计行、工具行）——否则会被“先裸打、行末替换”
        /// 的向上擦除一起吞掉、或被挤到裸文本同一行。同时清掉 _hasChannel：随后 Feed 的通道切换判定不会再补 \n。
        /// </summary>
        public void BreakLine()
        {
            if (_col == 0 && _line.Length == 0) { _hasChannel = false; return; }
            FinalizeLine(true);
            _hasChannel = false;
        }

        // ===================== 行末替换 =====================

        private void FinalizeLine(bool addNewline)
        {
            string raw = _line.ToString();
            bool dirty = _col > 0 || _wraps > 0;

            // 围栏状态与 VT 无关也要维护（VT 关闭时裸文本直出，状态仍需正确切换）
            bool isFence = raw.StartsWith("```");
            if (isFence) _inCodeBlock = !_inCodeBlock;

            if (CliUi.Vt && dirty)
            {
                // 回到本行第一视觉行的行首，清掉旧裸文本后原地重打渲染结果
                if (_wraps > 0) Console.Write("\x1b[" + _wraps + "A");
                Console.Write("\r\x1b[J");
                Console.Write(RenderLine(raw, isFence));
            }

            _line.Length = 0;
            _col = 0;
            _wraps = 0;
            if (addNewline) Console.Write("\n");
        }

        /// <summary>整行 Markdown → ANSI（行首结构 + 行内样式）。</summary>
        private string RenderLine(string raw, bool isFence)
        {
            var sb = new StringBuilder(raw.Length + 24);

            if (isFence)
            {
                sb.Append(CliUi.Gray).Append("  ┆ ").Append(CliUi.Sanitize(raw)).Append(CliUi.Reset);
                return sb.ToString();
            }
            if (_inCodeBlock)
            {
                sb.Append(CliUi.Gray).Append("  │ ").Append(CliUi.Sanitize(raw)).Append(CliUi.Reset);
                return sb.ToString();
            }

            string baseAnsi = _reasoning ? CliUi.Gray : "";

            Match m = RxHeading.Match(raw);
            if (m.Success)
            {
                string b = baseAnsi + CliUi.Bold;
                sb.Append(b).Append("■ ");
                AppendInline(sb, m.Groups[2].Value, b);
                return sb.ToString();
            }
            m = RxBullet.Match(raw);
            if (m.Success)
            {
                sb.Append("  ").Append(CliUi.Decor(CliUi.Cyan, "•")).Append(' ');
                AppendInline(sb, m.Groups[2].Value, baseAnsi);
                return sb.ToString();
            }
            m = RxOrdered.Match(raw);
            if (m.Success)
            {
                sb.Append("  ").Append(CliUi.Decor(CliUi.Cyan, m.Groups[1].Value + ".")).Append(' ');
                AppendInline(sb, m.Groups[2].Value, baseAnsi);
                return sb.ToString();
            }

            AppendInline(sb, raw, baseAnsi);
            return sb.ToString();
        }

        /// <summary>行内样式：`代码`、``引文``、**加粗**、*斜体*（baseAnsi 为行基础色，段落后恢复）。</summary>
        private void AppendInline(StringBuilder sb, string s, string baseAnsi)
        {
            sb.Append(baseAnsi);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\x1b') { i++; continue; }

                if (c == '`' || c == '*')
                {
                    int run = 0;
                    while (i + run < s.Length && s[i + run] == c) run++;
                    if (run >= 3) { sb.Append(s, i, run); i += run; continue; }   // ``` 等不当行内样式

                    int close = IndexOfRun(s, i + run, c, run);
                    if (close < 0) { sb.Append(c); i++; continue; }               // 行内未闭合：原样

                    string content = s.Substring(i + run, close - i - run);
                    string style;
                    if (c == '`')
                        style = run == 2 ? CliUi.Bold + CliUi.BgCode
                                        : (_reasoning ? CliUi.Bold + CliUi.BgCode : CliUi.Cyan);
                    else
                        style = run == 2 ? CliUi.Bold : CliUi.Italic;

                    sb.Append(style).Append(CliUi.Sanitize(content)).Append(CliUi.Reset).Append(baseAnsi);
                    i = close + run;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            sb.Append(CliUi.Reset);
        }

        // ===================== 裸打与光标列计数 =====================

        private void FlushChunk()
        {
            if (_chunk.Length == 0) return;
            if (_reasoning) CliUi.Write(CliUi.Gray, _chunk.ToString());
            else Console.Write(CliUi.Sanitize(_chunk.ToString()));
            _chunk.Length = 0;
        }

        private void TryGetWidth()
        {
            try { int w = Console.BufferWidth; if (w > 0) _width = w; } catch { }
        }

        private void Advance(int w)
        {
            if (w <= 0) return;
            if (_col + w > _width) { _wraps++; _col = w; }   // conhost 在越界时自动折到下一行
            else _col += w;
        }

        /// <summary>从 from 起找连续 run 个 c（行内样式的闭合标记）。</summary>
        private static int IndexOfRun(string s, int from, char c, int run)
        {
            for (int i = from; i + run <= s.Length; i++)
            {
                if (s[i] != c) continue;
                bool ok = true;
                for (int j = 1; j < run; j++) if (s[i + j] != c) { ok = false; break; }
                if (ok) return i;
            }
            return -1;
        }

        /// <summary>码点显示宽度：0=组合字符/控制符，2=中文/全角/Emoji，其余 1。</summary>
        internal static int CodepointWidth(int cp)
        {
            if (cp < 0x20) return 0;
            if (cp >= 0x0300 && cp <= 0x036F) return 0;
            if (cp >= 0x1100 && cp <= 0x115F) return 2;
            if (cp >= 0x2E80 && cp <= 0x303E) return 2;
            if (cp >= 0x3041 && cp <= 0x33FF) return 2;
            if (cp >= 0x3400 && cp <= 0x4DBF) return 2;
            if (cp >= 0x4E00 && cp <= 0x9FFF) return 2;
            if (cp >= 0xA000 && cp <= 0xA4CF) return 2;
            if (cp >= 0xAC00 && cp <= 0xD7A3) return 2;
            if (cp >= 0xF900 && cp <= 0xFAFF) return 2;
            if (cp >= 0xFE30 && cp <= 0xFE4F) return 2;
            if (cp >= 0xFF00 && cp <= 0xFF60) return 2;
            if (cp >= 0xFFE0 && cp <= 0xFFE6) return 2;
            if (cp >= 0x1F300 && cp <= 0x1FAFF) return 2;
            if (cp >= 0x20000 && cp <= 0x3FFFD) return 2;
            return 1;
        }
    }
}
