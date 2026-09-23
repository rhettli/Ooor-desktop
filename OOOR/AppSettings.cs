using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ooor.Core
{
    /// <summary>
    /// 应用启动设置（{ConfigRoot}\app.conf，key=value 行格式）：
    ///   auto_start=true/false            ← 开机自启动
    ///   auto_start_model=true/false      ← 自启动后自动启动之前运行的模型
    ///   auto_start_hide=true/false       ← 自启动后不显示主窗口（最小化到托盘）
    ///
    /// 自启动实现：写 HKCU\Software\Microsoft\Windows\CurrentVersion\Run\Ooor，
    /// 命令行 "<exe>" --autostart。Program.Main 解析 --autostart 后按本设置决定是否隐藏窗口 / 自动启动模型。
    /// </summary>
    public sealed class AppSettings
    {
        public const string SettingsFileName = "app.conf";

        public static string SettingsPath => Path.Combine(LlamaRuntime.ConfigRoot, SettingsFileName);

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Ooor";
        /// <summary>自启动命令行参数（Program.Main 据此识别开机自启场景）</summary>
        public const string AutoStartArg = "--autostart";

        public bool AutoStart;
        public bool AutoStartModel;
        public bool AutoStartHide;

        /// <summary>界面语言代码（"zh" / "en"），默认简体中文</summary>
        public string Language = "zh";

        /// <summary>app.conf 中是否显式设置了 language（false = 首次运行，需弹语言选择）</summary>
        public bool LanguageSet;

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return s;
                foreach (string line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    int eq = t.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = t.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = t.Substring(eq + 1).Trim();
                    bool b = string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                    if (key == "auto_start") s.AutoStart = b;
                    else if (key == "auto_start_model") s.AutoStartModel = b;
                    else if (key == "auto_start_hide") s.AutoStartHide = b;
                    else if (key == "language") { s.Language = val; s.LanguageSet = true; }
                }
            }
            catch { }
            if (string.IsNullOrEmpty(s.Language)) s.Language = "zh";
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
                var sb = new StringBuilder();
                sb.AppendLine("# Ooor 应用设置");
                sb.AppendLine("auto_start=" + (AutoStart ? "true" : "false"));
                sb.AppendLine("auto_start_model=" + (AutoStartModel ? "true" : "false"));
                sb.AppendLine("auto_start_hide=" + (AutoStartHide ? "true" : "false"));
                sb.AppendLine("language=" + (string.IsNullOrEmpty(Language) ? "zh" : Language));
                File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        /// <summary>按 AutoStart 开关写入/移除开机自启注册表项</summary>
        public void ApplyAutoStart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (AutoStart)
                    {
                        string exe = Application.ExecutablePath;
                        key.SetValue(RunValueName, "\"" + exe + "\" " + AutoStartArg,
                                     RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(RunValueName) != null) key.DeleteValue(RunValueName, false);
                    }
                }
            }
            catch { }
        }
    }
}
