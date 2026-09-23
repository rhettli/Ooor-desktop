using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// 模型市场列表接口的磁盘缓存（{ConfigRoot}\cache\api）：
    ///   文件内容格式 = 「过期时间戳」+ 一个空格 + 数据，
    ///   时间戳为 ISO 8601 UTC（例：2026-09-16T12:30:00Z [{"id":"..."}]）。
    /// 读取时先看开头的过期时间戳：未过期才返回数据；已过期/损坏的缓存当作没有并顺手删掉。
    /// 缓存只是提速手段，任何读写失败都不影响正常流程（静默跳过）。
    /// </summary>
    internal static class HfCache
    {
        /// <summary>缓存有效期（趋势榜与仓库文件列表变化很慢，半小时足够）</summary>
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

        private const string FileExt = ".cache";
        private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        private static DateTime _lastCleanupUtc = DateTime.MinValue;

        /// <summary>缓存目录（{ConfigRoot}\cache\api）</summary>
        public static string Dir => Path.Combine(LlamaRuntime.ConfigRoot, "cache", "api");

        /// <summary>取缓存数据；没有缓存、已过期或内容损坏时返回 null</summary>
        public static string Read(string key)
        {
            try { return ReadFresh(PathFor(key)); }
            catch { return null; }
        }

        /// <summary>写缓存：过期时间戳 + 空格 + 数据</summary>
        public static void Write(string key, string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            try
            {
                Directory.CreateDirectory(Dir);
                WriteAtomic(PathFor(key), ExpireStamp() + " " + data);
            }
            catch { /* 缓存失败不影响功能 */ }
            CleanupExpired();
        }

        /// <summary>
        /// 删除指定键的缓存（模型市场「清除缓存刷新文件」用）。
        /// 返回是否真的删掉了文件：键不存在返回 false，便于调用方统计清了几项。
        /// </summary>
        public static bool Remove(string key)
        {
            try
            {
                string path = PathFor(key);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        /// <summary>本次缓存数据的过期时刻（ISO 8601 UTC，不含空格，便于「时间戳 + 空格 + 数据」切分）</summary>
        private static string ExpireStamp() =>
            DateTime.UtcNow.Add(Ttl).ToString(TimeFormat, CultureInfo.InvariantCulture);

        /// <summary>
        /// 读取未过期的缓存内容：形如「2026-09-16T12:30:00Z {...}」。
        /// 前缀缺失、时间戳解析失败或已过期 → 删除该文件并返回 null。
        /// </summary>
        private static string ReadFresh(string path)
        {
            if (!File.Exists(path)) return null;

            string text = File.ReadAllText(path, Encoding.UTF8);
            int sp = text.IndexOf(' ');
            if (sp <= 0)
            {
                TryDelete(path);
                return null;
            }

            DateTimeOffset expire;
            if (!DateTimeOffset.TryParse(text.Substring(0, sp), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out expire)
                || expire.UtcDateTime <= DateTime.UtcNow)
            {
                TryDelete(path);
                return null;
            }

            return text.Substring(sp + 1);
        }

        /// <summary>键 → 缓存文件名：SHA1 十六进制（键含 URL 的 / ? &amp; 等字符，不能直接当文件名）</summary>
        private static string PathFor(string key)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key ?? ""));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return Path.Combine(Dir, sb + FileExt);
            }
        }

        /// <summary>先写临时文件再替换：避免读方拿到写一半的内容（不同键互不影响）</summary>
        private static void WriteAtomic(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>顺手清理过期缓存（写入时触发，最多 10 分钟扫一次目录，避免频繁 IO）</summary>
        private static void CleanupExpired()
        {
            if ((DateTime.UtcNow - _lastCleanupUtc).TotalMinutes < 10) return;
            _lastCleanupUtc = DateTime.UtcNow;
            try
            {
                if (!Directory.Exists(Dir)) return;
                foreach (string f in Directory.GetFiles(Dir, "*" + FileExt))
                    ReadFresh(f);   // 过期/损坏的文件在这里被删掉
            }
            catch { /* 清理失败无所谓 */ }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
