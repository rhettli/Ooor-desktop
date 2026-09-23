using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>
    /// GitHub Release 列表的本地 JSON 缓存（默认 24 小时内有效）。
    ///
    /// 缓存位置：%LOCALAPPDATA%/ooor/llama-releases-cache.json
    ///   {
    ///     "fetchedAt": "2026-09-14T12:00:00.0000000Z",
    ///     "json":      "...GitHub 返回的原始 JSON..."
    ///   }
    ///
    /// 设计要点：
    ///   - 缓存的是 GitHub 原始 JSON 文本（而非反序列化后的对象），
    ///     这样未来字段增减/重命名不影响缓存兼容性。
    ///   - 过期判定用 UTC 时间戳，避免本地时区切换影响。
    ///   - 所有 IO 异常均吞掉：缓存失败不应阻塞主流程。
    /// </summary>
    internal static class ReleaseCache
    {
        /// <summary>缓存有效期</summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string CachePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ooor");
                // 不存在则创建；缓存目录是用户级配置，不依赖插件配置目录
                try { Directory.CreateDirectory(dir); } catch { }
                return Path.Combine(dir, "llama-releases-cache.json");
            }
        }

        /// <summary>
        /// 尝试读取缓存。命中且未过期 → 返回原始 JSON + 抓取时间；
        /// 未命中或已过期 → 返回 null。
        /// </summary>
        public static string TryRead(out DateTime fetchedAtUtc)
        {
            fetchedAtUtc = DateTime.MinValue;
            try
            {
                if (!File.Exists(CachePath)) return null;

                string text = File.ReadAllText(CachePath, Encoding.UTF8);
                var obj = _json.Deserialize<Dictionary<string, object>>(text);
                if (obj == null) return null;

                if (!obj.TryGetValue("fetchedAt", out var rawDt) || rawDt == null) return null;
                if (!DateTime.TryParse(rawDt.ToString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out fetchedAtUtc))
                    return null;

                // 过期判定
                if (DateTime.UtcNow - fetchedAtUtc > Lifetime)
                {
                    fetchedAtUtc = DateTime.MinValue;
                    return null;
                }

                if (!obj.TryGetValue("json", out var rawJson) || rawJson == null) return null;
                return rawJson.ToString();
            }
            catch
            {
                fetchedAtUtc = DateTime.MinValue;
                return null;
            }
        }

        /// <summary>把抓取到的原始 JSON 写入缓存（写入失败静默）</summary>
        public static void Write(string rawJson)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    { "fetchedAt", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                    { "json",      rawJson ?? "" }
                };
                File.WriteAllText(CachePath,
                    _json.Serialize(payload),
                    new UTF8Encoding(false));
            }
            catch
            {
                // 缓存写失败不应阻塞主流程
            }
        }

        /// <summary>主动清空缓存（用于「强制刷新」场景；当前未在 UI 暴露）</summary>
        public static void Clear()
        {
            try { if (File.Exists(CachePath)) File.Delete(CachePath); } catch { }
        }
    }
}