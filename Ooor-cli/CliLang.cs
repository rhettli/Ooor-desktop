using System.Collections.Generic;
using System.IO;
using OoorFunc.Core;

namespace Ooor_cli
{
    /// <summary>
    /// Ooor-cli 多语言：读取 config/app.conf 的 language 字段（zh/en），
    /// 内嵌中英文翻译字典，提供 T(key) 与 Tf(key, args) 格式化方法。
    /// 不依赖外部 JSON 文件，自包含。
    /// </summary>
    internal static partial class CliLang
    {
 

        /// <summary>读取 config/app.conf 的 language 字段初始化语言。</summary>
        public static void Init()
        {
            try
            {
                string path = Path.Combine(CoreEnv.ConfigRoot, "app.conf");
                if (File.Exists(path))
                {
                    foreach (string raw in File.ReadAllLines(path))
                    {
                        string l = (raw ?? "").Trim();
                        if (l.StartsWith("language="))
                        {
                            string v = l.Substring(9).Trim().ToLowerInvariant();
                            if (v == "en" || v == "zh") _lang = v;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        public static bool IsEnglish => _lang == "en";

        /// <summary>取翻译；未命中返回 key 本身。</summary>
        public static string T(string key)
        {
            var dict = _lang == "en" ? En : Zh;
            string v;
            return dict.TryGetValue(key, out v) ? v : key;
        }

        /// <summary>取翻译并格式化。</summary>
        public static string Tf(string key, params object[] args)
        {
            string tpl = T(key);
            return args.Length == 0 ? tpl : string.Format(tpl, args);
        }
    }
}
