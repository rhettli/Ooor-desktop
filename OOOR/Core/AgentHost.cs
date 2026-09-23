using System;
using System.IO;
using System.Windows.Forms;
using OoorFunc.Core;

namespace ooor.Core
{
    /// <summary>
    /// 本地模型 Agent 的进程级单例门面：
    ///
    ///   - Instance:首次访问时按 ServerManager.LastRunState 推断 llama-server 的 base URL（http://{host}:{port}），
    ///     若服务没启动（LastRunState 为空）抛 InvalidOperationException，由调用方提示用户去点「启动服务」；
    ///   - Configure/Rebind/SetConfirm:在「启动服务 / 切换端口 / 切换沙盒 / 接入 UI 确认」时重建或调整实例；
    ///   - 单例与 ServerManager.Server 一样在窗口单例复用/重开场景里全程可用（LastRunState 即便服务被外部关掉仍可读）。
    /// </summary>
    internal static class AgentHost
    {
        private static LocalModelAgent _instance;
        private static AgentOptions _options;
        private static AgentConfirm _confirm;

        /// <summary>当前 Agent 实例；首次访问会按 ServerManager.LastRunState 自动创建。线程安全（双检）。</summary>
        public static LocalModelAgent Instance
        {
            get { return GetOrCreate(); }
        }

        /// <summary>外部注入完整参数（baseUrl / Options / Confirm），用于「自定义沙盒 / 接 UI 确认窗口」</summary>
        public static void Configure(string baseUrl, AgentOptions options = null, AgentConfirm confirm = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl 不能为空", "baseUrl");

            SyncCoreEnv();
            DisposeInstance();
            _options = options ?? AgentOptions.Default();
            _confirm = confirm ?? AgentConfirmDefaults.MessageBoxYesNo;
            _instance = new LocalModelAgent(baseUrl, _options, _confirm);
        }

        /// <summary>仅替换 baseUrl（重启服务后端口不变时也用）；保留 Options / Confirm / 历史。</summary>
        public static void Rebind(string baseUrl)
        {
            GetOrCreate().RebindBaseUrl(baseUrl);
        }

        /// <summary>替换确认回调；高频 UI 接入用（YesNo 弹到主窗口上）。</summary>
        public static void SetConfirm(AgentConfirm confirm)
        {
            LocalModelAgent current = GetOrCreate();
            _confirm = confirm ?? AgentConfirmDefaults.MessageBoxYesNo;
            // 实例的 Confirm 是 readonly；要换确认回调只能重建——历史与 model 保留
            string baseUrl = current.BaseUrl;
            DisposeInstance();
            _instance = new LocalModelAgent(baseUrl, _options ?? AgentOptions.Default(), _confirm);
        }

        /// <summary>清空对话历史（保留 Options / Confirm / baseUrl）</summary>
        public static void Reset()
        {
            if (_instance != null) _instance.ResetHistory();
        }

        /// <summary>释放当前实例（窗口退出 / 切换沙盒时调用）</summary>
        public static void Dispose()
        {
            DisposeInstance();
            _options = null;
            _confirm = null;
        }

        private static LocalModelAgent GetOrCreate()
        {
            if (_instance != null) return _instance;
            SyncCoreEnv();
            // 延迟初始化：按 LastRunState 推断 baseUrl
            string baseUrl = ResolveBaseUrlFromRunState();
            _options = _options ?? AgentOptions.Default();
            _confirm = _confirm ?? AgentConfirmDefaults.MessageBoxYesNo;
            _instance = new LocalModelAgent(baseUrl, _options, _confirm);
            return _instance;
        }

        /// <summary>
        /// 把 OOOR 宿主（LlamaRuntime / DEF / Application）的路径与身份映射进 OoorFunc.Core 的 CoreEnv，
        /// 供 get_app_paths / get_app_info / AgentOptions.Default 等使用。幂等，可反复调用。
        /// （Program.Main / ShowMainForm / AfterLoaded 也会各调一次，覆盖独立运行与插件加载两种入口。）
        /// </summary>
        public static void SyncCoreEnv()
        {
            CoreEnv.AppName = "ooor（欧尔模型坞）";
            CoreEnv.AppVersion = DEF.ver;
            CoreEnv.AppDescription =
                "WinForms 主程序（同时可作为 muaaa 主程序插件加载），内置 llama-cli / llama-server 管理与本地模型 Agent。";
            try { CoreEnv.StartupPath = Application.StartupPath; } catch { }
            try { CoreEnv.ConfigRoot = LlamaRuntime.ConfigRoot; } catch { }
            try { CoreEnv.LlamaBinsDir = LlamaRuntime.LlamaBinsDir; } catch { }
            try { CoreEnv.ModelsDir = LlamaRuntime.ModelsDir; } catch { }
            try { CoreEnv.HtmlDir = LlamaRuntime.HtmlDir; } catch { }
            CoreEnv.SelectedVersionProvider = delegate
            {
                try { return LlamaRuntime.SelectedVersion; } catch { return ""; }
            };
        }

        private static string ResolveBaseUrlFromRunState()
        {
            ServerManager.RunState st = ServerManager.LastRunState ?? ServerManager.LoadRunState();
            if (st == null || string.IsNullOrEmpty(st.Host) || st.Port <= 0)
                throw new InvalidOperationException("未检测到 llama-server 运行状态，请先点「启动服务」再使用 Agent");
            return "http://" + st.Host + ":" + st.Port;
        }

        private static void DisposeInstance()
        {
            try { _instance?.Dispose(); } catch { /* 释放失败不影响后续重建 */ }
            _instance = null;
        }
    }
}