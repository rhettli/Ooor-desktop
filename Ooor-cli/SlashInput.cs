using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OoorFunc.Core;

namespace Ooor_cli
{
    /// <summary>
    /// 带「斜杠命令弹出菜单」的逐键行输入（替代 Console.ReadLine）。
    ///
    /// 交互：
    ///   - 输入首字符为 / 时，输入行下方弹出命令菜单（/agent /new-console /talk /file /help /clear /exit，
    ///     /clear、/exit 固定最后，超过 6 行可在窗口内滚动），↑/↓ 选择、回车选中；继续输入会按前缀过滤（如 /age 只剩 /agent）。
    ///   - 选中 /agent、/talk、/file 后，下方切换为对应子菜单：Agent 列表 / 历史会话 / 文件，
    ///     再 ↑/↓、回车选定。/clear、/help、/new-console、/exit 回车即执行。
    ///   - Esc 关闭菜单；Tab 在命令层只补全不执行。
    ///   - 保留原有行为：双击 Ctrl+C 退出（第一次只提示、内容不丢）、中文双宽退格、自动折行。
    ///
    /// 渲染：菜单画在输入行「下一行」，每次重绘先用 ESC[J 从该位置清到屏幕底部，再重画，
    /// 画完用相对位移把光标送回输入位置。任何会改变输入布局的按键都「先擦菜单再改输入」，
    /// 避免自动折行时新字符落在菜单残留行上。
    /// </summary>
    internal static class SlashInput
    {
        private enum MenuMode { None, Commands, Agents, Sessions, Files }

        private sealed class MenuItem
        {
            public string Label;
            public string Hint;
            public string Payload;
        }

        // 固定的斜杠命令：名称 + 说明（/clear、/exit 固定排最后）
        private static KeyValuePair<string, string>[] Commands => new[]
        {
            new KeyValuePair<string, string>("/agent",       CliLang.T("cmd.agent")),
            new KeyValuePair<string, string>("/new-console", CliLang.T("cmd.newConsole")),
            new KeyValuePair<string, string>("/talk",        CliLang.T("cmd.talk")),
            new KeyValuePair<string, string>("/file",        CliLang.T("cmd.file")),
            new KeyValuePair<string, string>("/help",        CliLang.T("cmd.help")),
            new KeyValuePair<string, string>("/clear",       CliLang.T("cmd.clear")),
            new KeyValuePair<string, string>("/exit",        CliLang.T("cmd.exit")),
        };

        private const int MaxMenuRows = 6;

        /// <summary>读一行；返回 null 表示用户连按两次 Ctrl+C 要求退出。</summary>
        public static string ReadLine(AgentOptions opt)
        {
            // VT 不可用（重定向/极老终端）时退化为最简逐键输入，不弹菜单
            if (!CliUi.Vt) return ReadLinePlain();

            var buf = new StringBuilder();
            bool quitArmed = false;
            int width = ConsoleWidth();
            int startCol = CliUi.PromptWidth();
            int col = 0, wraps = 0;      // 输入光标布局

            MenuMode mode = MenuMode.None;
            var items = new List<MenuItem>();
            var filePool = new List<MenuItem>();   // /file 子菜单的候选池（进入该模式时只枚举一次，之后内存过滤）
            int sel = 0;
            string filterKey = null;

            Console.TreatControlCAsInput = true;
            try
            {
                while (true)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);

                    // ---------- Ctrl+C：双击退出 ----------
                    if (k.Key == ConsoleKey.C && (k.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        if (quitArmed) { ErasePopup(col, wraps, startCol, items); return null; }
                        quitArmed = true;
                        ErasePopup(col, wraps, startCol, items);
                        mode = MenuMode.None; items.Clear();
                        Console.WriteLine();
                        CliUi.Info(CliLang.T("doubleCtrlC"));
                        CliUi.Prompt();
                        Console.Write(buf.ToString());
                        RecalcLayout(buf, width, startCol, out col, out wraps);
                        EnsureRowsBelow(col, wraps, startCol, MaxMenuRows);
                        continue;
                    }
                    quitArmed = false;

                    // ---------- ↑ / ↓：菜单内移动选择 ----------
                    if (items.Count > 0 && (k.Key == ConsoleKey.UpArrow || k.Key == ConsoleKey.DownArrow))
                    {
                        if (k.Key == ConsoleKey.DownArrow) sel = (sel + 1) % items.Count;
                        else sel = (sel - 1 + items.Count) % items.Count;
                        RedrawPopup(col, wraps, startCol, items, sel);
                        continue;
                    }

                    // ---------- Esc：关闭菜单 ----------
                    if (k.Key == ConsoleKey.Escape && items.Count > 0)
                    {
                        ErasePopup(col, wraps, startCol, items);
                        mode = MenuMode.None; items.Clear();
                        continue;
                    }

                    // ---------- 回车：菜单选中 or 提交 ----------
                    if (k.Key == ConsoleKey.Enter)
                    {
                        if (items.Count > 0)
                        {
                            var it = items[sel];
                            if (mode == MenuMode.Commands)
                            {
                                // /clear、/help、/new-console、/exit 直接执行；其余进入各自子菜单
                                if (it.Payload == "/clear" || it.Payload == "/help" ||
                                    it.Payload == "/new-console" || it.Payload == "/exit")
                                {
                                    ErasePopup(col, wraps, startCol, items);
                                    Console.WriteLine();
                                    return it.Payload;
                                }
                                ErasePopup(col, wraps, startCol, items);
                                SetBuffer(buf, it.Payload, width, startCol, ref col, ref wraps);
                                RefreshMenu(buf, opt, ref mode, items, filePool, ref sel, ref filterKey);
                                DrawPopup(col, wraps, startCol, items, sel);
                                continue;
                            }

                            if (mode == MenuMode.Agents)
                            {
                                ErasePopup(col, wraps, startCol, items);
                                Console.Write(" " + it.Label);   // 补全回显
                                Console.WriteLine();
                                return "/agent " + it.Payload;
                            }
                            if (mode == MenuMode.Sessions)
                            {
                                ErasePopup(col, wraps, startCol, items);
                                Console.Write(" " + it.Label);
                                Console.WriteLine();
                                return "/talk " + it.Payload;
                            }
                            if (mode == MenuMode.Files)
                            {
                                // 文件：把完整路径插入输入行，回到普通编辑
                                ErasePopup(col, wraps, startCol, items);
                                string ins = it.Payload.Contains(" ") ? "\"" + it.Payload + "\"" : it.Payload;
                                SetBuffer(buf, ins, width, startCol, ref col, ref wraps);
                                mode = MenuMode.None; items.Clear();
                                continue;
                            }
                        }

                        ErasePopup(col, wraps, startCol, items);
                        Console.WriteLine();
                        return buf.ToString();
                    }

                    // ---------- Tab：命令层只补全 ----------
                    if (k.Key == ConsoleKey.Tab && items.Count > 0 && mode == MenuMode.Commands)
                    {
                        ErasePopup(col, wraps, startCol, items);
                        var it = items[sel];
                        SetBuffer(buf, it.Payload, width, startCol, ref col, ref wraps);
                        RefreshMenu(buf, opt, ref mode, items, filePool, ref sel, ref filterKey);
                        DrawPopup(col, wraps, startCol, items, sel);
                        continue;
                    }

                    // ---------- 退格 ----------
                    if (k.Key == ConsoleKey.Backspace)
                    {
                        ErasePopup(col, wraps, startCol, items);
                        EraseLastChar(buf, startCol, ref col, ref wraps, width);
                        RefreshMenu(buf, opt, ref mode, items, filePool, ref sel, ref filterKey);
                        DrawPopup(col, wraps, startCol, items, sel);
                        continue;
                    }

                    // ---------- 普通可打印字符 ----------
                    char ch = k.KeyChar;
                    if (ch == '\0' || char.IsControl(ch)) continue;
                    ErasePopup(col, wraps, startCol, items);
                    // 代理对（emoji）尽量成对读取
                    if (char.IsHighSurrogate(ch) && Console.KeyAvailable)
                    {
                        var k2 = Console.ReadKey(true);
                        if (k2.KeyChar != '\0' && char.IsLowSurrogate(k2.KeyChar))
                        {
                            buf.Append(ch).Append(k2.KeyChar);
                            Console.Write(ch); Console.Write(k2.KeyChar);
                            AdvanceCol(ch, width, startCol, ref col, ref wraps);
                            AdvanceCol(k2.KeyChar, width, startCol, ref col, ref wraps);
                        }
                        else
                        {
                            buf.Append(ch); Console.Write(ch);
                            AdvanceCol(ch, width, startCol, ref col, ref wraps);
                        }
                    }
                    else
                    {
                        buf.Append(ch);
                        Console.Write(ch);
                        AdvanceCol(ch, width, startCol, ref col, ref wraps);
                    }
                    RefreshMenu(buf, opt, ref mode, items, filePool, ref sel, ref filterKey);
                    DrawPopup(col, wraps, startCol, items, sel);
                }
            }
            finally
            {
                Console.TreatControlCAsInput = false;
            }
        }

        // ===================== 菜单内容 =====================

        /// <summary>按当前输入文本决定菜单模式与候选项；模式或过滤词变化时重置选中项。</summary>
        private static void RefreshMenu(StringBuilder buf, AgentOptions opt,
            ref MenuMode mode, List<MenuItem> items, List<MenuItem> filePool, ref int sel, ref string filterKey)
        {
            string text = buf.ToString();
            MenuMode newMode;
            string query;

            if (text.Length == 0 || text[0] != '/') { newMode = MenuMode.None; query = null; }
            else
            {
                int sp = -1;
                for (int i = 1; i < text.Length; i++)
                    if (char.IsWhiteSpace(text[i])) { sp = i; break; }

                string token = sp < 0 ? text : text.Substring(0, sp);
                query = sp < 0 ? "" : text.Substring(sp + 1).TrimStart();

                if (token == "/agent") newMode = MenuMode.Agents;
                else if (token == "/talk") newMode = MenuMode.Sessions;
                else if (token == "/file") newMode = MenuMode.Files;
                else if (sp < 0) newMode = MenuMode.Commands;   // / 前缀（含 /clear /help 完整词）
                else newMode = MenuMode.None;
            }

            string key = newMode + "|" + query;
            bool changed = newMode != mode || key != filterKey;
            bool enteredFiles = newMode == MenuMode.Files && mode != MenuMode.Files;
            mode = newMode;
            filterKey = key;

            items.Clear();
            switch (mode)
            {
                case MenuMode.Commands:
                    foreach (var c in Commands)
                        if (c.Key.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                            items.Add(new MenuItem { Label = c.Key, Hint = c.Value, Payload = c.Key });
                    break;
                case MenuMode.Agents: LoadAgents(items, query); break;
                case MenuMode.Sessions: LoadSessions(items, query); break;
                case MenuMode.Files:
                    if (enteredFiles || filePool.Count == 0)
                    {
                        filePool.Clear();
                        LoadFiles(filePool, "", opt);
                    }
                    FilterFiles(filePool, items, query);
                    break;
            }
            if (changed) sel = 0;
            if (sel >= items.Count) sel = items.Count - 1;
            if (sel < 0) sel = 0;
        }

        private static void LoadAgents(List<MenuItem> items, string query)
        {
            foreach (var a in AgentCatalog.All())
            {
                if (!string.IsNullOrEmpty(query) &&
                    (a.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                items.Add(new MenuItem
                {
                    Label = a.Name ?? CliLang.T("unnamed"),
                    Hint = string.IsNullOrEmpty(a.DefaultModel) ? "" : a.DefaultModel,
                    Payload = a.Name ?? ""
                });
            }
        }

        private static void LoadSessions(List<MenuItem> items, string query)
        {
            foreach (var c in ChatStore.All())
            {
                string title = c.Title ?? CliLang.T("unnamed");
                if (!string.IsNullOrEmpty(query) &&
                    title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string hint = c.UpdatedAt.ToString("MM-dd HH:mm");
                if (!string.IsNullOrEmpty(c.Model))
                {
                    string m = c.Model;
                    int slash = m.LastIndexOfAny(new[] { '\\', '/' });
                    if (slash >= 0 && slash < m.Length - 1) m = m.Substring(slash + 1);
                    if (m.Length > 24) m = m.Substring(0, 24) + "…";
                    hint += "  " + m;
                }
                items.Add(new MenuItem { Label = title, Hint = hint, Payload = c.Id });
            }
        }

        /// <summary>枚举 TempDir 与沙盒白名单根目录下的文件（一层），填充候选池；query 必须传 ""。</summary>
        private static void LoadFiles(List<MenuItem> pool, string query, AgentOptions opt)
        {
            var dirs = new List<string>();
            try { if (!string.IsNullOrEmpty(opt.TempDir)) dirs.Add(opt.TempDir); } catch { }
            try { if (opt.AllowedRoots != null) dirs.AddRange(opt.AllowedRoots); } catch { }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<FileInfo>();
            foreach (var d in dirs.Distinct())
            {
                string dir;
                try { dir = Path.GetFullPath(d); } catch { continue; }
                if (!seen.Add(dir)) continue;
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    files.AddRange(new DirectoryInfo(dir).GetFiles());
                }
                catch { }
            }

            foreach (var f in files.OrderByDescending(f => f.LastWriteTime).Take(200))
            {
                pool.Add(new MenuItem
                {
                    Label = f.Name,
                    Hint = f.DirectoryName ?? "",
                    Payload = f.FullName
                });
            }
        }

        /// <summary>从候选池中按文件名/路径子串过滤（不区分大小写），最多取 60 项。</summary>
        private static void FilterFiles(List<MenuItem> pool, List<MenuItem> items, string query)
        {
            foreach (var p in pool)
            {
                if (!string.IsNullOrEmpty(query) &&
                    p.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                    p.Payload.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                items.Add(p);
                if (items.Count >= 60) break;
            }
        }

        // ===================== 菜单绘制 =====================

        /// <summary>输入光标在其所在视觉行上的列号（0 起，第一行需加上提示符宽度）。</summary>
        private static int InputCol(int col, int wraps, int startCol)
        {
            return wraps == 0 ? startCol + col : col;
        }

        /// <summary>从菜单区回到输入光标位置：上移 upRows 行、回行首、右移到输入列。</summary>
        private static void MoveBackToInput(int upRows, int col, int wraps, int startCol)
        {
            int c = InputCol(col, wraps, startCol);
            Console.Write("\x1b[" + upRows + "A\r");
            if (c > 0) Console.Write("\x1b[" + c + "C");
        }

        /// <summary>
        /// 确保输入光标下方至少留得出 need 个空行；不足时先在底行换行让整屏上滚，再把光标送回输入位。
        /// 这样之后画菜单的下移/换行都不会越过屏幕底边界，避免「画一项滚一下」把历史内容顶上来。
        /// </summary>
        private static void EnsureRowsBelow(int col, int wraps, int startCol, int need)
        {
            int top, height;
            try { top = Console.CursorTop; height = Console.WindowHeight; }
            catch { return; }
            if (height <= 0) return;
            int rowsBelow = height - 1 - top;
            int lack = need - rowsBelow;
            if (lack <= 0) return;

            int c = InputCol(col, wraps, startCol);
            var sb = new StringBuilder();
            if (rowsBelow > 0) sb.Append("\x1b[" + rowsBelow + "B");  // 光标下移到底行（CUD 到底即止，不滚动）
            sb.Append("\r");
            for (int i = 0; i < lack; i++) sb.Append('\n');           // 在底行换行 → 整屏上滚 lack 行
            // 输入行随之被顶上去 lack 行：从底行上移 rowsBelow+lack 即回到新的输入位置
            sb.Append("\x1b[" + (rowsBelow + lack) + "A\r");
            if (c > 0) sb.Append("\x1b[" + c + "C");
            Console.Write(sb.ToString());
        }

        /// <summary>擦除已弹出的菜单：下移到菜单首行、回行首、清到屏幕底部，再回到输入位置。</summary>
        private static void ErasePopup(int col, int wraps, int startCol, List<MenuItem> items)
        {
            if (items == null || items.Count == 0) return;
            Console.Write("\x1b[1B\r\x1b[J");   // 到菜单首行行首并清空菜单区
            MoveBackToInput(1, col, wraps, startCol);
            items.Clear();
        }

        private static void RedrawPopup(int col, int wraps, int startCol, List<MenuItem> items, int sel)
        {
            if (items.Count == 0) return;
            Console.Write("\x1b[1B\r\x1b[J");
            int rows = DrawItems(items, sel);
            MoveBackToInput(rows, col, wraps, startCol);
        }

        private static void DrawPopup(int col, int wraps, int startCol, List<MenuItem> items, int sel)
        {
            if (items == null || items.Count == 0) return;
            // 先保证输入行下方有足够空行，否则画到屏幕底行会触发整屏滚动
            EnsureRowsBelow(col, wraps, startCol, Math.Min(MaxMenuRows, items.Count));
            // 菜单首行 = 输入光标下一行
            Console.Write("\x1b[1B\r");
            int rows = DrawItems(items, sel);
            MoveBackToInput(rows, col, wraps, startCol);
        }

        /// <summary>在当前光标处（菜单首行行首）画出各候选项；返回实际画出的行数，画完光标停在最后一项行内。</summary>
        private static int DrawItems(List<MenuItem> items, int sel)
        {
            int termWidth;
            int top;
            try { termWidth = Console.BufferWidth; top = Console.CursorTop; }
            catch { termWidth = 80; top = 0; }
            if (termWidth <= 0) termWidth = 80;
            // 注意 top 是菜单首行，可用行数含首行本身，所以是 height - top（不是 height - 1 - top）
            int rowsBelow;
            try { rowsBelow = Console.WindowHeight - top; } catch { rowsBelow = MaxMenuRows; }
            int maxShow = Math.Min(Math.Min(MaxMenuRows, items.Count), Math.Max(1, rowsBelow));

            // 选中项滚动到可视窗口内
            int first = 0;
            if (sel >= maxShow) first = sel - maxShow + 1;
            if (first + maxShow > items.Count) first = items.Count - maxShow;

            for (int i = 0; i < maxShow; i++)
            {
                var it = items[first + i];
                bool on = (first + i) == sel;
                string line = BuildItemLine(it, on, termWidth);
                Console.Write(line);
                if (i < maxShow - 1) Console.Write("\r\n");
            }
            return maxShow;
        }

        private static string BuildItemLine(MenuItem it, bool selected, int termWidth)
        {
            var sb = new StringBuilder();
            string marker = selected ? "▸ " : "  ";
            string label = it.Label ?? "";
            string hint = it.Hint ?? "";

            // 简单按字符数截断，保证不超过终端宽度（中文双宽为近似，长 hint 宁可多截一点）
            int used = marker.Length + label.Length;
            if (!string.IsNullOrEmpty(hint))
            {
                int budget = termWidth - used - 3;
                if (budget < 6) hint = "";
                else if (hint.Length > budget) hint = hint.Substring(0, budget - 1) + "…";
            }

            if (selected)
            {
                // 反相显示整条；说明文字用黄色区分
                sb.Append("\x1b[7m").Append(marker).Append(label).Append("\x1b[0m");
                if (!string.IsNullOrEmpty(hint))
                    sb.Append("  ").Append(CliUi.Gray).Append(hint).Append(CliUi.Reset);
            }
            else
            {
                sb.Append(CliUi.Cyan).Append(marker).Append(label).Append(CliUi.Reset);
                if (!string.IsNullOrEmpty(hint))
                    sb.Append("  ").Append(CliUi.Gray).Append(hint).Append(CliUi.Reset);
            }
            return sb.ToString();
        }

        // ===================== 输入布局 =====================

        /// <summary>整体替换输入文本：回到输入起点清屏、重画，并重算光标布局。</summary>
        private static void SetBuffer(StringBuilder buf, string text, int width, int startCol,
            ref int col, ref int wraps)
        {
            // 回到输入起点（提示符之后）：上移 wraps 视觉行 → 行首 → 越过提示符
            if (wraps > 0) Console.Write("\x1b[" + wraps + "A");
            Console.Write("\r");
            if (startCol > 0) Console.Write("\x1b[" + startCol + "C");
            Console.Write("\x1b[J");                 // 清掉旧输入及下方一切
            buf.Length = 0;
            buf.Append(text ?? "");
            Console.Write(buf.ToString());
            RecalcLayout(buf, width, startCol, out col, out wraps);
        }

        private static int CharColWidth(char c)
        {
            if (char.IsHighSurrogate(c)) return 0;
            if (char.IsLowSurrogate(c)) return 2;
            return StreamRenderer.CodepointWidth(c);
        }

        private static void AdvanceCol(char c, int width, int startCol, ref int col, ref int wraps)
        {
            int w = CharColWidth(c);
            if (w <= 0) return;
            int used = wraps == 0 ? startCol + col : col;
            if (used + w > width) { wraps++; col = w; }
            else col += w;
        }

        private static void RecalcLayout(StringBuilder buf, int width, int startCol, out int col, out int wraps)
        {
            col = 0; wraps = 0;
            for (int i = 0; i < buf.Length; i++) AdvanceCol(buf[i], width, startCol, ref col, ref wraps);
        }

        private static void EraseLastChar(StringBuilder buf, int startCol, ref int col, ref int wraps, int width)
        {
            if (buf.Length == 0) return;
            int n = buf.Length;
            char last = buf[n - 1];
            int cut = (n >= 2 && char.IsLowSurrogate(last) && char.IsHighSurrogate(buf[n - 2])) ? 2 : 1;
            int w = cut == 2 ? 2 : CharColWidth(last);
            buf.Length -= cut;
            if (w <= 0) return;

            if (wraps > 0 && col <= w)
            {
                Console.Write(new string('\b', w) + new string(' ', w) + "\x1b[1A\r\x1b[999C");
                wraps--;
                col = wraps == 0 ? width - startCol : width;
            }
            else if (col >= w)
            {
                Console.Write(new string('\b', w) + new string(' ', w) + new string('\b', w));
                col -= w;
            }
        }

        private static int ConsoleWidth()
        {
            try { int w = Console.BufferWidth; if (w > 0) return w; } catch { }
            return 80;
        }

        /// <summary>无 VT 的最简降级输入（双击 Ctrl+C 退出逻辑保持一致）。</summary>
        private static string ReadLinePlain()
        {
            var buf = new StringBuilder();
            bool quitArmed = false;
            Console.TreatControlCAsInput = true;
            try
            {
                while (true)
                {
                    var k = Console.ReadKey(true);
                    if (k.Key == ConsoleKey.C && (k.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        if (quitArmed) return null;
                        quitArmed = true;
                        Console.WriteLine();
                        CliUi.Info(CliLang.T("doubleCtrlC"));
                        CliUi.Prompt();
                        Console.Write(buf.ToString());
                        continue;
                    }
                    quitArmed = false;
                    if (k.Key == ConsoleKey.Enter) { Console.WriteLine(); return buf.ToString(); }
                    if (k.Key == ConsoleKey.Backspace)
                    {
                        if (buf.Length > 0)
                        {
                            char last = buf[buf.Length - 1];
                            int cut = (buf.Length >= 2 && char.IsLowSurrogate(last) && char.IsHighSurrogate(buf[buf.Length - 2])) ? 2 : 1;
                            buf.Length -= cut;
                            Console.Write("\b \b");
                        }
                        continue;
                    }
                    char ch = k.KeyChar;
                    if (ch == '\0' || char.IsControl(ch)) continue;
                    buf.Append(ch);
                    Console.Write(ch);
                }
            }
            finally { Console.TreatControlCAsInput = false; }
        }
    }
}
