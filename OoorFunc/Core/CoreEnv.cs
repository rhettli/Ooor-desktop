using System;
using System.IO;

namespace OoorFunc.Core
{
    /// <summary>
    /// 宿主环境上下文：Agent 工具集 / LocalModelAgent 对宿主程序（ooor 主程序、Ooor-cli 命令行）的
    /// 路径与身份信息的唯一来源。库内不引用任何宿主类型：
    ///   - OOOR 在启动时（Main / ShowMainForm / AfterLoaded）调用 CoreEnvSync 把 LlamaRuntime 的值映射进来；
    ///   - Ooor-cli 使用静态构造器按 exe 位置推导的默认值（与 ooor 主程序的目录约定一致）。
    /// </summary>
    public static class CoreEnv
    {
        /// <summary>宿主程序显示名（get_app_paths / get_app_info 返回给模型）</summary>
        public static string AppName = "ooor（欧尔模型坞）";

        /// <summary>宿主程序版本号（宿主启动时注入，如 OOOR 的 DEF.ver）</summary>
        public static string AppVersion = "";

        /// <summary>宿主程序一句话描述（模型理解工程布局用）</summary>
        public static string AppDescription =
            "WinForms 主程序（同时可作为 muaaa 主程序插件加载），内置 llama-cli / llama-server 管理与本地模型 Agent。";

        /// <summary>宿主 exe 所在目录（替代原 Application.StartupPath）</summary>
        public static string StartupPath = "";

        /// <summary>插件/程序配置根目录（ooor 约定：exe 同级 ..\config）</summary>
        public static string ConfigRoot = "";

        /// <summary>llama 版本容器目录（{ConfigRoot}\llama-bin）</summary>
        public static string LlamaBinsDir = "";

        /// <summary>模型目录（{ConfigRoot}\models）</summary>
        public static string ModelsDir = "";

        /// <summary>页面资源目录（html）</summary>
        public static string HtmlDir = "";

        /// <summary>当前选中的 llama 版本名（运行期会变化，宿主注入 provider 取值）；空 = 未选择</summary>
        public static Func<string> SelectedVersionProvider;

        static CoreEnv() { InitDefaults(); }

        /// <summary>按当前进程 exe 位置推导默认目录（CLI 场景；与 ooor 主程序的目录约定一致）</summary>
        public static void InitDefaults()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetEntryAssembly()
                         ?? System.Reflection.Assembly.GetExecutingAssembly();
                StartupPath = Path.GetDirectoryName(asm.Location) ?? Environment.CurrentDirectory;
            }
            catch { StartupPath = Environment.CurrentDirectory; }

            try { ConfigRoot = Path.GetFullPath(Path.Combine(StartupPath, "..", "config")); }
            catch { ConfigRoot = Path.Combine(StartupPath, "config"); }
            LlamaBinsDir = Path.Combine(ConfigRoot, "llama-bin");
            ModelsDir = Path.Combine(ConfigRoot, "models");
            HtmlDir = Path.Combine(StartupPath, "html");
        }

        /// <summary>当前选中的 llama 版本名（未注入 provider 或取值异常时返回空字符串）</summary>
        public static string GetSelectedVersion()
        {
            try { return SelectedVersionProvider != null ? (SelectedVersionProvider() ?? "") : ""; }
            catch { return ""; }
        }
    }
}
