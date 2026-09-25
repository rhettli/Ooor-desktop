using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using OoorFunc.Core;

namespace Ooor_cli
{
    /// <summary>
    /// 「ooor AI 助手」网页控制台窗口（--web 模式）：WebView2 宿主，页面是一个高仿 cmd 的黑底终端。
    ///
    /// 本类只做宿主与桥接（由 OOOR 项目的 AgentChatWebForm 迁移而来）：
    ///   - 模型侧由页面直连 llama-server 的 /v1/chat/completions（SSE 流式）；
    ///   - 本窗口只提供：配置（系统提示 / 工具 schema）、工具执行（沙盒 + 原生 MessageBox 审批）、状态；
    ///   - 权限语义与原生控制台 REPL 不同：读 agent.conf（AgentOptions.Default），不强制工具全开；
    ///   - C#→JS：PostWebMessageAsJson；JS→C#：WebMessageReceived 按 action 分发；
    ///   - 高危审批坚持用原生 MessageBox：真安全边界，页面被注入也无法自行批准。
    /// </summary>
    internal sealed class WebConsoleForm : Form
    {
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 64
        };

        private readonly WebView2 _web;
        private readonly string _baseUrl;
        private readonly string _modelId;
        private readonly bool _trustArg;
        private readonly bool _devTools;   // --devtools：启用 F12/右键开发者工具并在启动时自动打开
        private AgentOptions _opt;           // 当前 Agent 参数（/option、/root 即时落盘 agent.conf）
        private AgentToolRegistry _registry; // 工具集：页面发 {a:'tool'}，这里执行（沙盒 + 审批仍在 C#）
        private bool _webReady;
        private bool _running;

        public WebConsoleForm(string baseUrl, List<string> extraRoots, bool trust, string modelId, bool devTools)
        {
            _baseUrl = baseUrl ?? "";
            _modelId = modelId ?? "";
            _running = !string.IsNullOrEmpty(_baseUrl);
            _trustArg = trust;
            _devTools = devTools;

            Text = "ooor AI 助手（控制台窗口）";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1020, 720);
            MinimumSize = new Size(980, 720);
            BackColor = Color.Black;

            _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };
            Controls.Add(_web);

            // 窗口语义：读 agent.conf（默认只读 + 用户保存过的开关/白名单），不强制工具全开
            _opt = AgentOptions.Default();
            if (_trustArg) _opt.TrustAiJudgment = true;
            try
            {
                string cwd = Environment.CurrentDirectory;
                if (!string.IsNullOrEmpty(cwd)) AddRootInternal(cwd);
            }
            catch { }
            if (extraRoots != null)
                foreach (string r in extraRoots) AddRootInternal(r);

            Shown += async (s, e) => await InitWebViewAsync();
        }

        // ==================== WebView2 初始化 ====================

        private async Task InitWebViewAsync()
        {
            string htmlDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "html");
            string index = Path.Combine(htmlDir, "index.html");
            if (!File.Exists(index))
            {
                MessageBox.Show(this,
                    "找不到终端界面资源文件：\r\n" + index + "\r\n\r\n请确认 html 目录随 Ooor-cli.exe 一起发布。",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string udf = Path.Combine(Path.GetTempPath(), "ooor-cli-webview2");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, udf);
                await _web.EnsureCoreWebView2Async(env);

                CoreWebView2 core = _web.CoreWebView2;
                // 开发者工具：--devtools 显式开启，或在 Visual Studio 里挂着托管调试器运行时自动开启
                if (!_devTools && !Debugger.IsAttached)
                {
                    core.Settings.AreDefaultContextMenusEnabled = false;
                    core.Settings.IsStatusBarEnabled = false;
                    core.Settings.AreDevToolsEnabled = false;
                }
                core.Settings.IsWebMessageEnabled = true;

                // 把 html 目录映射成虚拟域名；http:// 页面 fetch 本机 http 服务无混合内容问题（服务端 CORS 全放行）
                core.SetVirtualHostNameToFolderMapping(
                    "ooor.local", htmlDir, CoreWebView2HostResourceAccessKind.Allow);

                core.WebMessageReceived += OnWebMessageReceived;
                core.Navigate("http://ooor.local/index.html");

                if (_devTools)
                {
                    // 导航完成后自动弹出 DevTools 窗口；之后也可随时 F12 或右键「检查」
                    core.NavigationCompleted += (s2, e2) =>
                    {
                        try { core.OpenDevToolsWindow(); } catch { }
                    };
                }

                _webReady = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "WebView2 初始化失败：\r\n" + ex.Message + "\r\n\r\n" +
                    "请确认已安装 WebView2 Runtime（Win10/11 通常自带）。",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== JS → C# ====================

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> msg;
            try { msg = Json.DeserializeObject(e.WebMessageAsJson) as Dictionary<string, object>; }
            catch { return; }
            if (msg == null) return;

            switch (Str(msg, "a"))
            {
                case "ready":
                    try { EnsureRegistry(); } catch { /* 首次对话前还能重建 */ }
                    PostState();
                    break;
                case "cfg":
                    PostCfg();
                    break;
                case "tool":
                    RunTool(Str(msg, "id"), Str(msg, "name"), Str(msg, "args"));
                    break;
                case "options":
                    ApplyOptions(Bool(msg, "allowWrite", false), Bool(msg, "allowCmd", false),
                                 Bool(msg, "trustAi", false), Bool(msg, "allowInternet", false));
                    break;
                case "addRoot":
                    AddRootByDialog();
                    break;
                case "rootAdd":
                    AddRoot(Str(msg, "path"));
                    break;
                case "removeRoot":
                    RemoveRoot(Str(msg, "path"));
                    break;
                case "openPath":
                    OpenPath(Str(msg, "path"));
                    break;
                case "fileRead":
                    FileRead(Str(msg, "id"), Str(msg, "path"));
                    break;
                case "checkServer":
                    CheckServer();
                    break;
                case "newWindow":
                    NewWindow();
                    break;
                case "exit":
                    try { BeginInvoke(new Action(Close)); } catch { }
                    break;
            }
        }

        // ==================== 工具执行 ====================

        private AgentToolRegistry EnsureRegistry()
        {
            if (_registry == null) _registry = new AgentToolRegistry(Opt, UiConfirm);
            return _registry;
        }

        private AgentOptions Opt
        {
            get { return _opt ?? (_opt = AgentOptions.Default()); }
        }

        /// <summary>下发 Agent 配置：服务地址 + 系统提示 + 工具 schema（页面据此直连 /v1 对话）。</summary>
        private void PostCfg()
        {
            AgentToolRegistry reg;
            try { reg = EnsureRegistry(); }
            catch (Exception ex) { PostSystem("工具集初始化失败：" + ex.Message, true); return; }

            AgentOptions o = Opt;
            Post(new Dictionary<string, object>
            {
                { "t", "cfg" },
                { "baseUrl", _baseUrl },
                { "running", _running },
                { "modelId", _modelId },
                { "system", o.SystemPrompt ?? "" },
                { "maxSteps", o.MaxSteps },
                { "temperature", 0.2 },
                { "tools", reg.BuildOpenAIToolsArray() },
                { "toolNames", reg.Names }
            });
        }

        /// <summary>页面发来的工具调用：后台线程执行（可能弹审批框 / 跑脚本），完成后把结果回给页面。</summary>
        private void RunTool(string id, string name, string argsJson)
        {
            AgentToolRegistry reg;
            try { reg = EnsureRegistry(); }
            catch (Exception ex) { PostToolResult(id, name, argsJson, AgentToolResult.Err("工具集初始化失败：" + ex.Message)); return; }

            if (!reg.TryGet(name, out IAgentTool tool))
            {
                PostToolResult(id, name, argsJson,
                    AgentToolResult.Err("未知工具 '" + (name ?? "") + "'（名称不匹配或未启用）。可用工具：" + string.Join(", ", reg.Names)));
                return;
            }

            Task.Run(() =>
            {
                IDictionary<string, object> args;
                try
                {
                    if (string.IsNullOrEmpty(argsJson) || argsJson == "null")
                    {
                        args = new Dictionary<string, object>();
                    }
                    else
                    {
                        // 独立实例：JavaScriptSerializer 非线程安全，避免与 UI 线程的 Post 互踩
                        var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
                        args = serializer.DeserializeObject(argsJson) as IDictionary<string, object>
                               ?? new Dictionary<string, object>();
                    }
                }
                catch (Exception ex)
                {
                    PostToolResult(id, name, argsJson, AgentToolResult.Err("参数 JSON 解析失败：" + ex.Message));
                    return;
                }

                AgentToolResult r;
                try { r = tool.Execute(args); }
                catch (Exception ex) { r = AgentToolResult.Err("工具执行异常：" + ex.Message); }

                PostToolResult(id, name, argsJson, r);
            });
        }

        private void PostToolResult(string id, string name, string argsJson, AgentToolResult r)
        {
            Post(new Dictionary<string, object>
            {
                { "t", "toolResult" },
                { "id", id ?? "" },
                { "name", name ?? "" },
                { "args", argsJson ?? "" },
                { "ok", r != null && r.Success },
                { "text", r != null ? (r.Text ?? "") : "" },
                { "at", DateTime.Now.ToString("HH:mm:ss") }
            });
        }

        // ==================== 设置：开关 / 沙盒 ====================

        private void ApplyOptions(bool allowWrite, bool allowCmd, bool trustAi, bool allowInternet)
        {
            AgentOptions o = Opt;
            bool toolSetChanged = o.AllowWrite != allowWrite || o.AllowCommand != allowCmd || o.AllowInternet != allowInternet;

            o.AllowWrite = allowWrite;
            o.AllowCommand = allowCmd;
            o.TrustAiJudgment = trustAi;
            o.AllowInternet = allowInternet;
            AgentSettings.Save(o);

            if (toolSetChanged)
            {
                try
                {
                    _registry = new AgentToolRegistry(o, UiConfirm);   // 工具增减必须重建
                    Post(new Dictionary<string, object> { { "t", "clear" } });
                    PostCfg();
                    PostSystem("工具权限已变更（写入=" + (allowWrite ? "开" : "关") +
                               "，脚本/命令=" + (allowCmd ? "开" : "关") +
                               "，联网=" + (allowInternet ? "开" : "关") + "），对话已重置。", false);
                }
                catch (Exception ex) { PostSystem("应用设置失败：" + ex.Message, true); }
            }
            PostState();
        }

        private void AddRootByDialog()
        {
            using (var fb = new FolderBrowserDialog { Description = "选择允许 AI 读写的目录（加入沙盒白名单）" })
            {
                if (fb.ShowDialog(this) != DialogResult.OK) return;
                AddRoot(fb.SelectedPath);
            }
        }

        /// <summary>/root add &lt;路径&gt;：终端直接给路径（无路径时页面改发 addRoot 走目录框）。</summary>
        private void AddRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) { AddRootByDialog(); return; }
            string p;
            try { p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { PostSystem("路径无效：" + path, true); return; }

            if (AddRootInternal(p))
            {
                AgentSettings.Save(Opt);
                PostSystem("已加入白名单：" + p, false);
            }
            PostState();
        }

        private bool AddRootInternal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string p;
            try { p = Path.GetFullPath(path.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return false; }
            if (p.Length == 0 || !Directory.Exists(p)) return false;

            AgentOptions o = Opt;
            foreach (string r in o.AllowedRoots)
                if (string.Equals(r, p, StringComparison.OrdinalIgnoreCase)) return false;
            o.AllowedRoots.Insert(0, p);   // 与 Options 同一引用 → 工具集立即生效
            return true;
        }

        private void RemoveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            AgentOptions o = Opt;
            bool removed = false;
            for (int i = o.AllowedRoots.Count - 1; i >= 0; i--)
            {
                if (string.Equals(o.AllowedRoots[i], path, StringComparison.OrdinalIgnoreCase))
                {
                    o.AllowedRoots.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed)
            {
                AgentSettings.Save(o);
                PostSystem("已移出白名单：" + path, false);
            }
            PostState();
        }

        private void OpenPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                if (!Directory.Exists(path) && !File.Exists(path)) { PostSystem("路径不存在：" + path, true); return; }
                // UseShellExecute=true → ShellExecute：文件按系统默认程序打开（.html 走默认浏览器），目录走资源管理器
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { PostSystem("打开失败：" + ex.Message, true); }
        }

        /// <summary>/file &lt;路径&gt;：读文件内容回给终端显示（截断 6000 字符，与原生 CLI 一致）。</summary>
        private void FileRead(string id, string path)
        {
            string text;
            bool ok = false;
            try
            {
                string p = (path ?? "").Trim('"').Trim();
                if (!File.Exists(p)) { text = "文件不存在：" + p; }
                else
                {
                    text = File.ReadAllText(p);
                    ok = true;
                }
            }
            catch (Exception ex) { text = "读取失败：" + ex.Message; }

            Post(new Dictionary<string, object>
            {
                { "t", "fileResult" },
                { "id", id ?? "" },
                { "ok", ok },
                { "path", path ?? "" },
                { "text", text ?? "" }
            });
        }

        /// <summary>页面「检测服务」：重新探活 /v1/models，刷新状态横幅。</summary>
        private void CheckServer()
        {
            Task.Run(() =>
            {
                bool ok = false;
                try
                {
                    using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) })
                    using (HttpResponseMessage resp = http.GetAsync(_baseUrl + "/v1/models").GetAwaiter().GetResult())
                        ok = resp.IsSuccessStatusCode;
                }
                catch { ok = false; }
                _running = ok;
                PostState();
            });
        }

        /// <summary>/new-console：再开一个同样的网页控制台窗口（独立进程）。</summary>
        private void NewWindow()
        {
            try
            {
                string exe = Process.GetCurrentProcess().MainModule.FileName;
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--web" + (Opt.TrustAiJudgment ? " --trust" : ""),
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true      // 窗口模式不要控制台
                };
                Process.Start(psi);
                PostSystem("已新开一个控制台窗口。", false);
            }
            catch (Exception ex) { PostSystem("新窗口打开失败：" + ex.Message, true); }
        }

        /// <summary>高危工具审批：原生 MessageBox（默认按钮=否），必须在 UI 线程。</summary>
        private bool UiConfirm(string title, string detail)
        {
            if (IsDisposed || !IsHandleCreated) return false;
            DialogResult r = DialogResult.No;
            try
            {
                Invoke(new Action(() =>
                {
                    r = MessageBox.Show(this, detail, title,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                }));
            }
            catch { return false; }
            return r == DialogResult.Yes;
        }

        // ==================== C# → JS ====================

        private void PostState()
        {
            AgentOptions o = Opt;
            string tools = "";
            if (_registry != null)
            {
                try { tools = string.Join(", ", _registry.Names); } catch { }
            }

            Post(new Dictionary<string, object>
            {
                { "t", "state" },
                { "running", _running },
                { "baseUrl", _baseUrl },
                { "modelId", _modelId },
                { "roots", new List<string>(o.AllowedRoots) },
                { "allowWrite", o.AllowWrite },
                { "allowCmd", o.AllowCommand },
                { "trustAi", o.TrustAiJudgment },
                { "allowInternet", o.AllowInternet },
                { "maxSteps", o.MaxSteps },
                { "tools", tools },
                { "tempDir", o.TempDir },
                { "appDir", AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/') },
                { "version", CoreEnv.AppVersion ?? "" }
            });
        }

        private void PostSystem(string text, bool isError)
        {
            Post(new Dictionary<string, object>
            {
                { "t", "msg" },
                { "role", isError ? "error" : "system" },
                { "text", text ?? "" }
            });
        }

        private void Post(object payload)
        {
            string json;
            try { json = Json.Serialize(payload); }
            catch { return; }

            Action push = () =>
            {
                try
                {
                    if (_webReady && _web != null && !_web.IsDisposed && _web.CoreWebView2 != null)
                        _web.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch { /* 窗口/内核已释放 */ }
            };

            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(push); } catch { } }
            else push();
        }

        // ==================== 小工具 ====================

        private static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? v.ToString() : "";
        }

        private static bool Bool(Dictionary<string, object> d, string key, bool dflt)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return dflt;
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1") return true;
            if (s == "false" || s == "0") return false;
            return dflt;
        }
    }
}
