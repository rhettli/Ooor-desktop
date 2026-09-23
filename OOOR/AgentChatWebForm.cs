using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ooor.Core;
using OoorFunc.Core;

namespace ooor
{
    /// <summary>
    /// 「AI 助手」窗口（WebView2 宿主）：
    ///
    ///   - UI 全部由 html 目录下的页面渲染（index.html / style.css / app.js），本窗口只做宿主与桥接；
    ///   - 模型侧由页面直接 fetch llama-server 的 /v1/chat/completions（SSE 流式），
    ///     好处：DevTools 网络面板能看到每个请求/响应，排查问题不必依赖 C# 日志；
    ///   - 本窗口只提供：配置（系统提示 / 工具 schema）、工具执行（沙盒 + 审批）、状态；
    ///   - C#→JS：CoreWebView2.PostWebMessageAsJson（页面监听 window.chrome.webview message）；
    ///   - JS→C#：页面 postMessage → WebMessageReceived，按 action 分发；
    ///   - 高危工具（写/删/命令/脚本）的审批用原生 MessageBox（真安全边界不用页面弹窗，避免页面被注入后自批）。
    /// </summary>
    internal sealed class AgentChatWebForm : Form
    {
        private static AgentChatWebForm _instance;

        /// <summary>单例打开：已存在则激活（与主窗口共用同一个 Agent 会话）</summary>
        public static void ShowSingle()
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new AgentChatWebForm();
                _instance.Show();
            }
            else
            {
                _instance.Show();
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;
                _instance.Activate();
                _instance.BringToFront();
            }
        }

        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 64
        };

        private readonly WebView2 _web;
        private AgentOptions _opt;           // 窗体持有的当前 Agent 参数（改开关即时落盘）
        private AgentToolRegistry _registry; // 工具集：页面发 {a:'tool'}，这里执行（沙盒 + 审批仍在 C#）
        private bool _webReady;              // CoreWebView2 初始化完成

        private AgentChatWebForm()
        {
            Text = "ooor AI 助手";
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(1020, 720);
            MinimumSize = new Size(760, 520);
            BackColor = Color.FromArgb(17, 19, 24);
            Font = new Font("Microsoft YaHei UI", 9F);

            _web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(17, 19, 24) };
            Controls.Add(_web);

            FormClosed += (s, e) => { _instance = null; };
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            await InitWebViewAsync();
        }

        // ==================== WebView2 初始化 ====================

        private async Task InitWebViewAsync()
        {
            string htmlDir = LlamaRuntime.HtmlDir;
            string index = Path.Combine(htmlDir, "index.html");
            if (!File.Exists(index))
            {
                MessageBox.Show(this,
                    "找不到界面资源文件：\r\n" + index + "\r\n\r\n" +
                    "请确认 html 目录随程序一起发布（源码目录：OOOR\\html）。",
                    "AI 助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                string udf = Path.Combine(Path.GetTempPath(), "ooor-webview2");
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, udf);
                await _web.EnsureCoreWebView2Async(env);

                CoreWebView2 core = _web.CoreWebView2;
                if (!Debugger.IsAttached)
                {
                    core.Settings.AreDefaultContextMenusEnabled = false;
                    core.Settings.IsStatusBarEnabled = false;
                    core.Settings.AreDevToolsEnabled = false;
                }
                core.Settings.IsWebMessageEnabled = true;

                // 把 html 目录映射成虚拟域名，页面可直接 fetch 同目录资源
                core.SetVirtualHostNameToFolderMapping(
                    "ooor.local", htmlDir, CoreWebView2HostResourceAccessKind.Allow);

                core.WebMessageReceived += OnWebMessageReceived;
                // 用 http:// 虚拟域名：页面 fetch llama-server(http://127.0.0.1:port) 属跨域但服务端 CORS 全放行，
                // 且 http 页面不存在 https→http 的混合内容隐患
                core.Navigate("http://ooor.local/index.html");

                _webReady = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "WebView2 初始化失败：\r\n" + ex.Message + "\r\n\r\n" +
                    "请确认已安装 WebView2 Runtime（Win10/11 通常自带）。",
                    "AI 助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== JS → C# ====================

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> msg;
            try { msg = Json.DeserializeObject(e.WebMessageAsJson) as Dictionary<string, object>; }
            catch { return; }
            if (msg == null) return;

            string action = Str(msg, "a");
            switch (action)
            {
                case "ready":
                    try { EnsureRegistry(); } catch { /* 服务未启动时留空，等对话前再建 */ }
                    PostState();
                    break;
                case "cfg":
                    // 页面要开始对话了：下发系统提示 + 工具 schema（模型调用由页面直连 /v1 完成）
                    PostCfg();
                    break;
                case "tool":
                    RunTool(Str(msg, "id"), Str(msg, "name"), Str(msg, "args"));
                    break;
                case "clear":
                    ClearChat();
                    break;
                case "options":
                    ApplyOptions(Bool(msg, "allowWrite", false), Bool(msg, "allowCmd", false),
                                 Bool(msg, "trustAi", false), Bool(msg, "allowInternet", false));
                    break;
                case "addRoot":
                    AddRootByDialog();
                    break;
                case "addAppDir":
                    AddRoot(SafeStartupPath());
                    break;
                case "removeRoot":
                    RemoveRoot(Str(msg, "path"));
                    break;
                case "openPath":
                    OpenPath(Str(msg, "path"));
                    break;
                case "checkServer":
                    PostState();
                    break;
            }
        }

        // ==================== 工具执行（模型调用在页面侧） ====================

        /// <summary>当前 llama-server 基地址；未启动/无状态时返回空串</summary>
        private static string CurrentBaseUrl()
        {
            ServerManager.RunState st = ServerManager.LastRunState ?? ServerManager.LoadRunState();
            if (st == null || string.IsNullOrEmpty(st.Host) || st.Port <= 0) return "";
            return "http://" + st.Host + ":" + st.Port;
        }

        /// <summary>惰性构建工具集（工具集随 AllowWrite / AllowCommand 变化而重建）</summary>
        private AgentToolRegistry EnsureRegistry()
        {
            if (_registry == null) _registry = new AgentToolRegistry(Opt, UiConfirm);
            return _registry;
        }

        private AgentOptions Opt
        {
            get { return _opt ?? (_opt = AgentOptions.Default()); }
        }

        /// <summary>下发 Agent 配置：服务地址 + 系统提示 + 工具 schema（页面据此直连 /v1 对话）</summary>
        private void PostCfg()
        {
            AgentToolRegistry reg;
            try { reg = EnsureRegistry(); }
            catch (Exception ex) { PostSystem("工具集初始化失败：" + ex.Message, true); return; }

            AgentOptions o = Opt;
            Post(new Dictionary<string, object>
            {
                { "t", "cfg" },
                { "baseUrl", CurrentBaseUrl() },
                { "running", SafeServerRunning() },
                { "system", o.SystemPrompt ?? "" },
                { "maxSteps", o.MaxSteps },
                { "temperature", 0.2 },
                { "tools", reg.BuildOpenAIToolsArray() },
                { "toolNames", reg.Names }
            });
        }

        /// <summary>
        /// 页面发来的工具调用：在后台线程执行（可能弹审批框 / 跑脚本，不能卡 UI），
        /// 执行完把结果回给页面（页面塞回 messages 的 role=tool 里继续下一轮）。
        /// </summary>
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

        private void AddRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string p;
            try { p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return; }

            AgentOptions o = Opt;
            foreach (string r in o.AllowedRoots)
            {
                if (string.Equals(r, p, StringComparison.OrdinalIgnoreCase)) { PostState(); return; }
            }
            o.AllowedRoots.Add(p);          // 工具集持有同一 Options 引用 → 立即生效，无需重建
            AgentSettings.Save(o);
            PostState();
        }

        private void RemoveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            AgentOptions o = Opt;
            for (int i = o.AllowedRoots.Count - 1; i >= 0; i--)
            {
                if (string.Equals(o.AllowedRoots[i], path, StringComparison.OrdinalIgnoreCase))
                    o.AllowedRoots.RemoveAt(i);
            }
            AgentSettings.Save(o);
            PostState();
        }

        private void OpenPath(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return;
                if (!Directory.Exists(path) && !File.Exists(path)) { PostSystem("路径不存在：" + path, true); return; }
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { PostSystem("打开失败：" + ex.Message, true); }
        }

        private void ClearChat()
        {
            // 对话历史由页面持有，C# 只需通知「重置」
            Post(new Dictionary<string, object> { { "t", "clear" } });
        }

        /// <summary>高危工具审批：原生 MessageBox（默认按钮=否），必须回到 UI 线程</summary>
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
            var roots = new List<string>(o.AllowedRoots);
            string tools = "";
            if (_registry != null)
            {
                try { tools = string.Join(", ", _registry.Names); } catch { }
            }

            string baseUrl = CurrentBaseUrl();

            Post(new Dictionary<string, object>
            {
                { "t", "state" },
                { "running", SafeServerRunning() },
                { "baseUrl", baseUrl },
                { "roots", roots },
                { "allowWrite", o.AllowWrite },
                { "allowCmd", o.AllowCommand },
                { "trustAi", o.TrustAiJudgment },
                { "allowInternet", o.AllowInternet },
                { "maxSteps", o.MaxSteps },
                { "tools", tools },
                { "appDir", SafeStartupPath() },
                { "htmlDir", SafeHtmlDir() },
                { "version", DEF.ver }
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

        private static string SafeStartupPath()
        {
            try { return Application.StartupPath; } catch { return ""; }
        }

        private static string SafeHtmlDir()
        {
            try { return LlamaRuntime.HtmlDir; } catch { return ""; }
        }

        private static bool SafeServerRunning()
        {
            try { return ServerManager.Server.IsRunning; } catch { return false; }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // AgentChatForm
            // 
            this.ClientSize = new System.Drawing.Size(278, 244);
            this.Name = "AgentChatForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.ResumeLayout(false);

        }
    }
}
