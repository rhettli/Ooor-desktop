using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>
    /// 最新版本信息：字段名与 GET /api/v1/releases/latest 响应对齐
    /// （见 ooor-cloudflare/src/releases.ts 的 toDTO + latestHandler）。
    /// </summary>
    public sealed class OoorRelease
    {
        public string Version = "";      // 0.0008
        public string Channel = "";      // stable
        public string FileName = "";     // ooor-Setup-x64-v0.0008.exe
        public long Size;                // 安装包字节数
        public string Sha256 = "";
        public string Notes = "";        // 更新日志（可多行）
        public bool Force;               // 强制更新
        public long Downloads;
        public bool FileDeleted;         // 归档版本：安装包已删（latest 会跳过，正常不会出现）
        public string PublishedAt = "";  // ISO UTC，如 2026-09-17T08:44:49.870Z
        public string DownloadUrl = "";  // {base}/api/v1/releases/{version}/download

        /// <summary>是否比客户端当前版本更新；只有请求带了 current 参数时服务端才回填该字段</summary>
        public bool HasUpdate;
    }

    /// <summary>
    /// 客户端更新检查：GET {server_url}/api/v1/releases/latest?current={当前版本}。
    ///
    /// 服务地址见 CurrentServerUrl()：ooor.conf 显式配了 server_url 就用它，
    /// 没配则直接用官网域名（避开 Debug 默认的本地网关 127.0.0.1:8080）。
    /// 无发布版本时服务端返回 404 {error:"暂无发布版本"}，此处抛出带该文案的异常。
    /// </summary>
    internal static class OoorUpdate
    {
        /// <summary>官网域名（关于窗口跳转用；固定值，不受 server_url 配置影响）</summary>
        public const string HomeUrl = "https://ooor.cc";

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly JavaScriptSerializer _ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        /// <summary>
        /// 更新检查实际使用的服务地址：
        ///   - ooor.conf 里显式配置了 server_url → 用它（便于对着本地网关联调）；
        ///   - 没配置 → 直接用官网域名。
        /// 为什么要这样：编译期默认地址在 Debug 配置下是 http://127.0.0.1:8080（本地 go 网关），
        /// 开发机没启动网关时会一律「检查更新失败」，而版本发布信息本来就在官网上。
        /// </summary>
        public static string CurrentServerUrl()
        {
            var set = OoorSettings.Load();
            return set.ServerUrlConfigured ? set.ServerUrl : HomeUrl;
        }

        /// <summary>
        /// 拉取最新版本。<paramref name="current"/> 传客户端当前版本（DEF.ver），
        /// 服务端据此回填 has_update；留空则不判断是否需要更新。
        /// 连接类抖动（DNS/TLS/超时）自动重试一次；HTTP 错误响应不重试，直接抛出服务端文案。
        /// </summary>
        public static async Task<OoorRelease> CheckLatestAsync(string current, CancellationToken ct)
        {
            string url = CurrentServerUrl().TrimEnd('/') + "/api/v1/releases/latest"
                + (string.IsNullOrWhiteSpace(current)
                    ? ""
                    : "?current=" + Uri.EscapeDataString(current.Trim()));
            string token = OoorSettings.Load().Token;

            for (int attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return await RequestOnceAsync(url, token, ct);
                }
                catch (Exception ex) when (attempt < 2 && IsTransient(ex, ct))
                {
                    await Task.Delay(800, ct);   // 抖动：缓一下再来一次
                }
            }
        }

        /// <summary>可重试的瞬时故障：连不上 / TLS 握手失败 / 超时（用户主动取消不算）</summary>
        private static bool IsTransient(Exception ex, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return false;
            return ex is HttpRequestException || ex is TaskCanceledException;
        }

        private static async Task<OoorRelease> RequestOnceAsync(string url, string token, CancellationToken ct)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "1.0"));
                // 更新检查必须实时：绕开系统/中间层缓存
                req.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                if (!string.IsNullOrWhiteSpace(token))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

                using (var resp = await _http.SendAsync(req, ct))
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                    {
                        string msg = TryReadError(text);
                        throw new Exception(msg ?? ("服务器返回 " + (int)resp.StatusCode + " " + resp.ReasonPhrase));
                    }
                    return Parse(text);
                }
            }
        }

        // ==================== 解析 ====================

        private static OoorRelease Parse(string jsonText)
        {
            var d = _ser.Deserialize<Dictionary<string, object>>(jsonText);
            if (d == null) throw new Exception("服务端返回内容无法解析。");

            return new OoorRelease
            {
                Version = Str(d, "version"),
                Channel = Str(d, "channel"),
                FileName = Str(d, "filename"),
                Size = Num(d, "size"),
                Sha256 = Str(d, "sha256"),
                Notes = Str(d, "notes"),
                Force = Flag(d, "force"),
                Downloads = Num(d, "downloads"),
                FileDeleted = Flag(d, "file_deleted"),
                PublishedAt = Str(d, "published_at"),
                DownloadUrl = Str(d, "download_url"),
                HasUpdate = Flag(d, "has_update"),
            };
        }

        /// <summary>取错误响应里的 {error:"..."} 文案；没有则返回 null（由调用方用状态码兜底）</summary>
        private static string TryReadError(string jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText)) return null;
            try
            {
                var d = _ser.Deserialize<Dictionary<string, object>>(jsonText);
                if (d != null && d.TryGetValue("error", out var v) && v != null)
                {
                    string s = v.ToString().Trim();
                    if (s.Length > 0) return s;
                }
            }
            catch { /* 非 JSON 错误体：交给状态码兜底 */ }
            return null;
        }

        private static string Str(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null) return v.ToString();
            return "";
        }

        private static long Num(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
            {
                long n;
                if (long.TryParse(v.ToString(), out n)) return n;
            }
            return 0;
        }

        private static bool Flag(Dictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
            {
                if (v is bool b) return b;
                return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
