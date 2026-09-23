using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ooor.Core
{
    /// <summary>一套服务启动参数方案（模型不包含在内：模型单独选择）</summary>
    internal sealed class RunProfile
    {
        public string Name = "";
        public long Ctx = 131072;
        public int NPred = 8192;
        public int Ngl = 0;
        public string Host = "127.0.0.1";
        public int Port = 6080;

        public override string ToString() { return Name; }
    }

    /// <summary>
    /// 参数方案存储（model_profiles.conf，纯文本）：
    /// 每个方案一个「[#方案名]」标题行，后跟 key=value 参数行。
    /// </summary>
    internal static class ProfileStore
    {
        public const string ProfilesFileName = "model_profiles.conf";

        /// <summary>方案配置文件（{ConfigRoot}\model_profiles.conf）</summary>
        public static string ProfilesPath => Path.Combine(LlamaRuntime.ConfigRoot, ProfilesFileName);

        /// <summary>读取全部方案（按名称排序）；没有文件时返回空列表</summary>
        public static List<RunProfile> LoadProfiles()
        {
            var result = new List<RunProfile>();
            string path = ProfilesPath;
            if (!File.Exists(path)) return result;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return result; }

            RunProfile cur = null;
            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line.StartsWith("[#", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    string name = line.Substring(2, line.Length - 3).Trim();
                    if (name.Length == 0) continue;
                    cur = new RunProfile { Name = name };
                    result.Add(cur);
                    continue;
                }

                if (cur == null) continue;   // 没有方案头就出现的参数行：忽略

                int sep = line.IndexOf('=');
                if (sep <= 0) continue;
                string key = line.Substring(0, sep).Trim().ToLowerInvariant();
                string val = line.Substring(sep + 1).Trim();

                long lv; int iv;
                switch (key)
                {
                    case "ctx": if (long.TryParse(val, out lv) && lv > 0) cur.Ctx = lv; break;
                    case "npred": if (int.TryParse(val, out iv) && iv > 0) cur.NPred = iv; break;
                    case "ngl": if (int.TryParse(val, out iv) && iv >= 0) cur.Ngl = iv; break;
                    case "host": if (val.Length > 0) cur.Host = val; break;
                    case "port": if (int.TryParse(val, out iv) && iv > 0 && iv <= 65535) cur.Port = iv; break;
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        /// <summary>写回全部方案（UTF-8 无 BOM；目录不存在时自动创建）</summary>
        public static void SaveProfiles(IEnumerable<RunProfile> profiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 参数方案配置：每个方案一个 [#名称] 标题行，后跟 key=value 参数行");

            foreach (var p in profiles ?? new RunProfile[0])
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name)) continue;

                // 方案名去换行、去掉「]」避免破坏标题行
                string name = (p.Name ?? "").Replace("\r", " ").Replace("\n", " ").Replace("]", "］").Trim();
                if (name.Length == 0) continue;

                sb.AppendLine();
                sb.AppendLine("[#" + name + "]");
                sb.AppendLine("ctx=" + p.Ctx);
                sb.AppendLine("npred=" + p.NPred);
                sb.AppendLine("ngl=" + p.Ngl);
                sb.AppendLine("host=" + (p.Host ?? ""));
                sb.AppendLine("port=" + p.Port);
            }

            Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
            File.WriteAllText(ProfilesPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
