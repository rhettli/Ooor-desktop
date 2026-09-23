using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>
    /// 多语言管理器（单例）。
    /// 语言文件位于程序目录 Lang\{code}.json，扁平 key=value 结构。
    /// 用 System.Web.Script.Serialization.JavaScriptSerializer 解析（项目已引用 System.Web.Extensions，不引入新依赖）。
    /// 切换语言时触发 LanguageChanged 事件，订阅者据此刷新界面文本。
    /// </summary>
    public sealed class LanguageManager
    {
        private static readonly Lazy<LanguageManager> _instance =
            new Lazy<LanguageManager>(() => new LanguageManager());
        public static LanguageManager Instance => _instance.Value;

        private readonly Dictionary<string, string> _dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>当前语言代码（"zh" / "en"）</summary>
        public string CurrentLanguage { get; private set; } = "zh";

        /// <summary>可用语言列表（code -> 显示名）</summary>
        public Dictionary<string, string> AvailableLanguages { get; } =
            new Dictionary<string, string> { { "zh", "简体中文" }, { "en", "English" } };

        /// <summary>语言切换后触发（订阅者刷新界面）</summary>
        public event EventHandler LanguageChanged;

        private LanguageManager() { }

        /// <summary>语言文件目录（程序目录\Lang\）</summary>
        public string LangDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lang");

        /// <summary>取翻译；不存在时原样返回 key</summary>
        public string T(string key)
        {
            string v;
            return _dict.TryGetValue(key, out v) ? v : key;
        }

        /// <summary>加载指定语言；失败保留上一次的字典</summary>
        public void LoadLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) code = "zh";
            if (!AvailableLanguages.ContainsKey(code)) code = "zh";

            string path = Path.Combine(LangDir, code + ".json");
            try
            {
                if (!File.Exists(path)) return;   // 文件缺失：保持当前字典不变
                string json = File.ReadAllText(path, Encoding.UTF8);
                var ser = new JavaScriptSerializer();
                var map = ser.Deserialize<Dictionary<string, string>>(json);
                if (map == null || map.Count == 0) return;

                _dict.Clear();
                foreach (var kv in map)
                    if (!string.IsNullOrEmpty(kv.Key)) _dict[kv.Key] = kv.Value ?? "";

                CurrentLanguage = code;
            }
            catch
            {
                // 解析失败：静默，保持当前字典
            }

            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
