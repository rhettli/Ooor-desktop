using System;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// ooor 插件入口：本地 llama.cpp 模型服务管理。
    /// 支持两种运行方式：
    ///   1) 独立窗口程序：Main 入口直接启动主窗口；
    ///   2) 主程序插件：由主程序经 proxy.exe 反射调用（窗口单例复用）。
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// 应用程序的主入口点（独立运行时启动主窗口）。
        /// </summary>
        [STAThread]
        static void Main()
        {
            AgentHost.SyncCoreEnv();   // 把 LlamaRuntime/DEF 的路径映射给 OoorFunc.Core（Agent 工具用）
            //Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 解析开机自启参数（注册表 Run 项写入的 --autostart）：
            // 据此识别"开机自动拉起"场景，由 MainForm 按用户设置决定隐藏窗口 / 跑上次模型
            bool autoStart = Array.IndexOf(Environment.GetCommandLineArgs(),
                                           ooor.Core.AppSettings.AutoStartArg) >= 0;

            // 启动时按设置载入界面语言（app.conf 的 language=zh/en）
            var settings = ooor.Core.AppSettings.Load();
            if (!settings.LanguageSet)
            {
                // 首次运行：弹语言选择对话框
                string picked = ShowLanguagePicker();
                settings.Language = picked;
                settings.Save();
            }
            LanguageManager.Instance.LoadLanguage(settings.Language);

            var main = new MainForm { AutoStartMode = autoStart };
            Application.Run(main);
        }

        /// <summary>首次启动语言选择对话框，返回 "zh" 或 "en"</summary>
        private static string ShowLanguagePicker()
        {
            string result = "zh";
            using (var dlg = new Form())
            {
                dlg.Text = "Language / 语言";
                dlg.StartPosition = FormStartPosition.CenterScreen;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.Width = 340;
                dlg.Height = 220;
                dlg.BackColor = System.Drawing.Color.White;

                var lbl = new System.Windows.Forms.Label
                {
                    Text = "请选择界面语言\nPlease select a language",
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                    Dock = System.Windows.Forms.DockStyle.Top,
                    Height = 70,
                    Font = new System.Drawing.Font("Microsoft YaHei UI", 11f)
                };
                dlg.Controls.Add(lbl);

                var btnZh = new System.Windows.Forms.Button
                {
                    Text = "中文（简体）",
                    Width = 130,
                    Height = 55,
                    Font = new System.Drawing.Font("Microsoft YaHei UI", 10f),
                    Location = new System.Drawing.Point(30, 95)
                };
                var btnEn = new System.Windows.Forms.Button
                {
                    Text = "English",
                    Width = 130,
                    Height = 55,
                    Font = new System.Drawing.Font("Segoe UI", 10f),
                    Location = new System.Drawing.Point(175, 95)
                };
                btnZh.Click += (s, e) => { result = "zh"; dlg.Close(); };
                btnEn.Click += (s, e) => { result = "en"; dlg.Close(); };
                dlg.Controls.Add(btnZh);
                dlg.Controls.Add(btnEn);
                dlg.AcceptButton = btnZh;
                dlg.ShowDialog();
            }
            return result;
        }

        private static MainForm _mainForm;

        /// <summary>主窗口入口（Profile 的 top_tool/bottom_tool 绑定函数，:FORM_MAIN）</summary>
        public static IntPtr ShowMainForm()
        {
            AgentHost.SyncCoreEnv();   // 插件加载路径同样初始化 OoorFunc.Core 的宿主上下文
            if (_mainForm == null || _mainForm.IsDisposed)
            {
                _mainForm = new MainForm();
                _mainForm.Show();
            }
            else
            {
                _mainForm.Show();
                if (_mainForm.WindowState == FormWindowState.Minimized)
                    _mainForm.WindowState = FormWindowState.Normal;
            }

            _mainForm.Activate();
            _mainForm.BringToFront();
            return _mainForm.Handle;
        }

        /// <summary>本插件加载完成：把宿主路径映射给 OoorFunc.Core（Agent 工具的 CoreEnv）</summary>
        public static void AfterLoaded()
        {
            AgentHost.SyncCoreEnv();
        }

        /// <summary>全部插件加载完成：暂无依赖其他插件的初始化</summary>
        public static void AfterAllLoaded()
        {
        }

        /// <summary>主程序广播消息（语言切换等）；当前无需要响应的事件</summary>
        public static void Notify(string jsonStr)
        {
        }

        /// <summary>插件函数注册表（主程序扫描入口）</summary>
        public static string AllFunc()
        {
            return @"[
{""fun"":""Notify"",""i18n_name"":{""zh"":""消息通知"",""zh-hant"":""消息通知"",""en"":""Notify""},""param"":[{""arg"":""json_str"",""i18n_desc"":{""zh"":""json数据"",""zh-hant"":""json數據"",""en"":""json data""}}],""type"":"":NOTIFY""},
{""fun"":""AfterLoaded"",""i18n_name"":{""zh"":""此插件加载完成后执行的函数"",""zh-hant"":""此插件載入完成後執行的函數"",""en"":""After loaded""},""param"":[],""type"":"":AFTER_LOADED""},
{""fun"":""AfterAllLoaded"",""i18n_name"":{""zh"":""所有插件加载完成后执行的函数"",""zh-hant"":""所有插件載入完成後執行的函數"",""en"":""After all loaded""},""param"":[],""type"":"":AFTER_ALL_LOADED""},
{""fun"":""ShowMainForm"",""i18n_name"":{""zh"":""本地模型管理"",""zh-hant"":""本地模型管理"",""en"":""Local LLM Manager""},""param"":[],""type"":"":FORM_MAIN""}
]";
        }

        /// <summary>插件元数据</summary>
        public static string Profile()
        {
            // 生成 16x16 顶部工具栏图标（青底白色 L 字样占位）
            string iconBase64 = "";
            try
            {
                using (var bmp = new System.Drawing.Bitmap(16, 16))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Teal);
                        using (var font = new System.Drawing.Font("Arial", 9f, System.Drawing.FontStyle.Bold))
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            var sf = new System.Drawing.StringFormat
                            {
                                Alignment = System.Drawing.StringAlignment.Center,
                                LineAlignment = System.Drawing.StringAlignment.Center
                            };
                            g.DrawString("L", font, brush, new System.Drawing.RectangleF(0, -1, 16, 16), sf);
                        }
                    }
                    using (var ms = new System.IO.MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        iconBase64 = "Base64," + Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch
            {
                // 图标生成失败不影响插件加载（按钮退化为纯文字）
            }

            return @"{
""version"":""" + DEF.ver + @""",
""key"":""ooor"",
""author"":"""",
""website"":"""",
""icon"":"""",
""ext_field"":"""",
""i18n_app_name"":{""zh"":""本地模型管理"",""zh-hant"":""本地模型管理"",""en"":""Local LLM Manager""},
""i18n_desc"":{""zh"":""本地 llama.cpp 模型服务（llama-server）启动与日志管理"",""zh-hant"":""本地 llama.cpp 模型服務（llama-server）啟動與日誌管理"",""en"":""Start and monitor local llama.cpp (llama-server) models""},
""top_tool"":[
    {""i18n_name"":{""zh"":""本地模型"",""zh-hant"":""本地模型"",""en"":""Local LLM""},""type"":""button"",""bind_fun"":""ShowMainForm"",""display"":""ImageText"",""icon"":""" + iconBase64 + @"""}
]
}";
        }
    }
}
