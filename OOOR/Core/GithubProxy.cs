using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>
    /// GitHub 加速镜像加载与代理 URL 拼装。
    ///
    /// 数据来源分为两类（彼此独立）：
    ///   1. 服务器代理（ApiProxies）：GET {server_url}/api/v1/proxies?platform=github
    ///      拉取后缓存到 {ConfigRoot}\github-proxy-api.json，缓存有效期 24 小时；
    ///      URL 只读，用户只能启用/禁用（禁用状态也持久化在缓存文件里）。
    ///   2. 用户自定义代理（UserProxies）：{ConfigRoot}\github-proxy.txt，每行一个 URL，
    ///      # 开头为禁用；完全由用户增删改。
    ///
    /// 下拉框使用的 Proxies = 启用的服务器代理 + 启用的用户自定义代理，
    /// 末尾固定追加 DirectMarker（"github.com"，= 直连不代理）。
    ///
    /// 拼装规则：{base.TrimEnd('/')}/{originalUrl}
    ///   例：https://ghproxy.net/ + https://github.com/WJQSERVER-STUDIO/.../file.zip
    ///     = https://ghproxy.net/https://github.com/WJQSERVER-STUDIO/.../file.zip
    /// 已以代理域名为前缀的 URL 不二次代理，避免链式拼接。
    /// </summary>
    public static class GithubProxy
    {
        /// <summary>用户自定义代理配置文件名（{ConfigRoot} 下）</summary>
        public const string FileName = "github-proxy.txt";

        /// <summary>服务器代理缓存文件名（{ConfigRoot} 下）</summary>
        public const string ApiCacheFileName = "github-proxy-api.json";

        /// <summary>缓存有效期（24 小时）</summary>
        public static readonly TimeSpan ApiCacheTtl = TimeSpan.FromHours(24);

        /// <summary>用户自定义配置文件完整路径</summary>
        public static string ConfigPath => Path.Combine(LlamaRuntime.ConfigRoot, FileName);

        /// <summary>服务器代理缓存文件完整路径</summary>
        public static string ApiCachePath => Path.Combine(LlamaRuntime.ConfigRoot, ApiCacheFileName);

        /// <summary>下拉框末尾固定追加的"原始链接"标识，显示为 "github.com"</summary>
        public const string DirectMarker = "github.com";

        /// <summary>硬编码兜底镜像列表（服务器拉不到且无缓存时使用）</summary>
        public static readonly IReadOnlyList<string> BuiltinProxies = new[]
        {
            "https://ghproxy.net/",
            "https://gh-proxy.org/",
        };

        // ============ 服务器代理 ============

        /// <summary>服务器代理条目</summary>
        public sealed class ApiProxy
        {
            public string Url = "";
            public string Name = "";
            public bool Enabled = true;
        }

        private static List<ApiProxy> _apiProxies;
        private static DateTime _apiFetchedAt = DateTime.MinValue;
        private static readonly object _apiSync = new object();

        /// <summary>
        /// 服务器代理列表（含启用/禁用状态）。
        /// 首次访问时从缓存文件读取；缓存超过 24 小时则异步刷新（本次仍返回旧缓存）。
        /// </summary>
        public static IReadOnlyList<ApiProxy> ApiProxies
        {
            get
            {
                if (_apiProxies != null) return _apiProxies;
                lock (_apiSync)
                {
                    if (_apiProxies != null) return _apiProxies;
                    _apiProxies = LoadApiCache();
                    // 缓存过期 → 后台刷新（不阻塞调用方）
                    if (DateTime.UtcNow - _apiFetchedAt > ApiCacheTtl)
                    {
                        _ = RefreshApiAsync();
                    }
                    return _apiProxies;
                }
            }
        }

        /// <summary>服务器代理列表（仅 URL），供下拉框使用</summary>
        private static IEnumerable<string> ApiProxyUrls
        {
            get
            {
                foreach (var p in ApiProxies)
                    if (p.Enabled && !string.IsNullOrEmpty(p.Url))
                        yield return p.Url;
            }
        }

        /// <summary>
        /// 设置服务器代理的启用/禁用状态，并持久化到缓存文件。
        /// </summary>
        public static void SetApiProxyEnabled(string url, bool enabled)
        {
            if (string.IsNullOrEmpty(url)) return;
            lock (_apiSync)
            {
                if (_apiProxies == null) _apiProxies = LoadApiCache();
                var p = _apiProxies.FirstOrDefault(x =>
                    string.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase));
                if (p != null)
                {
                    p.Enabled = enabled;
                    SaveApiCache();
                }
            }
        }

        /// <summary>
        /// 缓存过期时刷新；未过期直接返回。供窗口首次打开时调用。
        /// </summary>
        public static async Task EnsureApiFreshAsync()
        {
            if (_apiProxies == null) { var _ = ApiProxies; }
            if (DateTime.UtcNow - _apiFetchedAt > ApiCacheTtl || _apiFetchedAt == DateTime.MinValue)
                await RefreshApiAsync();
        }

        /// <summary>
        /// 强制从服务器刷新代理列表（忽略缓存），成功后更新缓存文件。
        /// 失败时静默保留旧缓存。
        /// </summary>
        public static async Task RefreshApiAsync()
        {
            try
            {
                var list = await FetchProxiesFromServerAsync();
                lock (_apiSync)
                {
                    // 保留用户已设置的禁用状态
                    var oldDisabled = new HashSet<string>(
                        (_apiProxies ?? new List<ApiProxy>())
                            .Where(p => !p.Enabled)
                            .Select(p => p.Url),
                        StringComparer.OrdinalIgnoreCase);
                    _apiProxies = list.Select(p => new ApiProxy
                    {
                        Url = p.Url,
                        Name = p.Name,
                        Enabled = !oldDisabled.Contains(p.Url),
                    }).ToList();
                    _apiFetchedAt = DateTime.UtcNow;
                    SaveApiCache();
                }
            }
            catch
            {
                // 网络失败：保留旧缓存
            }
        }

        /// <summary>从本地缓存文件加载服务器代理；文件不存在或损坏返回空列表</summary>
        private static List<ApiProxy> LoadApiCache()
        {
            try
            {
                if (!File.Exists(ApiCachePath)) return new List<ApiProxy>();
                var json = File.ReadAllText(ApiCachePath);
                var ser = new JavaScriptSerializer();
                var data = ser.Deserialize<ApiCacheFile>(json);
                if (data == null || data.proxies == null) return new List<ApiProxy>();
                if (DateTime.TryParse(data.fetched_at,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AdjustToUniversal |
                        System.Globalization.DateTimeStyles.AssumeUniversal,
                        out var t))
                    _apiFetchedAt = t;
                return data.proxies
                    .Where(p => p != null && !string.IsNullOrEmpty(p.url))
                    .Select(p => new ApiProxy
                    {
                        Url = p.url,
                        Name = p.name ?? "",
                        Enabled = p.enabled,
                    })
                    .ToList();
            }
            catch
            {
                return new List<ApiProxy>();
            }
        }

        /// <summary>把内存中的服务器代理列表写入缓存文件</summary>
        private static void SaveApiCache()
        {
            try
            {
                var data = new ApiCacheFile
                {
                    fetched_at = _apiFetchedAt.ToString("o"),
                    proxies = _apiProxies.Select(p => new ApiCacheProxy
                    {
                        url = p.Url,
                        name = p.Name,
                        enabled = p.Enabled,
                    }).ToList(),
                };
                var ser = new JavaScriptSerializer();
                File.WriteAllText(ApiCachePath, ser.Serialize(data));
            }
            catch
            {
                // 写入失败：忽略
            }
        }

        // 缓存文件 JSON 结构（字段名与服务端响应对齐，小写）
        private sealed class ApiCacheFile
        {
            public string fetched_at = "";
            public List<ApiCacheProxy> proxies = new List<ApiCacheProxy>();
        }
        private sealed class ApiCacheProxy
        {
            public string url = "";
            public string name = "";
            public bool enabled = true;
        }

        /// <summary>
        /// 调用 GET {server_url}/api/v1/proxies?platform=github&page_size=100 获取代理列表。
        /// 服务地址复用 OoorUpdate.CurrentServerUrl()（ooor.conf 配了就用，否则官网）。
        /// </summary>
        private static async Task<List<ApiProxy>> FetchProxiesFromServerAsync()
        {
            string baseUrl = OoorUpdate.CurrentServerUrl().TrimEnd('/');
            string url = baseUrl + "/api/v1/proxies?platform=github&page_size=100";
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.UserAgent.ParseAdd("ooor/1.0");
                    req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    using (var resp = await http.SendAsync(req))
                    {
                        if (!resp.IsSuccessStatusCode)
                            throw new Exception("HTTP " + (int)resp.StatusCode);
                        string text = await resp.Content.ReadAsStringAsync();
                        var ser = new JavaScriptSerializer();
                        var data = ser.Deserialize<ApiListResponse>(text);
                        if (data?.proxies == null) return new List<ApiProxy>();
                        return data.proxies
                            .Where(p => p != null && !string.IsNullOrEmpty(p.url))
                            .Select(p => new ApiProxy
                            {
                                Url = p.url.TrimEnd('/'),
                                Name = p.name ?? "",
                                Enabled = true,
                            })
                            .ToList();
                    }
                }
            }
        }

        // 服务端响应结构
        private sealed class ApiListResponse
        {
            public List<ApiProxyItem> proxies;
        }
        private sealed class ApiProxyItem
        {
            public string url = "";
            public string name = "";
        }

        // ============ 用户自定义代理 ============

        private static IReadOnlyList<string> _userProxies;
        private static readonly object _userSync = new object();

        /// <summary>用户自定义代理列表（不含 DirectMarker），按文件顺序、去重。</summary>
        public static IReadOnlyList<string> UserProxies
        {
            get
            {
                if (_userProxies != null) return _userProxies;
                lock (_userSync)
                {
                    if (_userProxies != null) return _userProxies;
                    _userProxies = LoadUserFromFile();
                    return _userProxies;
                }
            }
        }

        /// <summary>保存用户自定义代理到配置文件（覆盖写）。</summary>
        public static void SaveUserProxies(IEnumerable<string> urls)
        {
            lock (_userSync)
            {
                _userProxies = urls?.Where(u => !string.IsNullOrEmpty(u)).ToList() ?? new List<string>();
                try
                {
                    File.WriteAllLines(ConfigPath, _userProxies);
                }
                catch
                {
                    // 写入失败：内存已更新，下次启动从文件读不到会回落内置
                }
            }
        }

        /// <summary>从磁盘重新加载用户配置（外部修改了 txt 后可手动刷新）</summary>
        public static void Reload()
        {
            lock (_userSync) _userProxies = LoadUserFromFile();
        }

        private static IReadOnlyList<string> LoadUserFromFile()
        {
            try
            {
                string p = ConfigPath;
                if (!File.Exists(p)) return new List<string>();

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var list = new List<string>();
                foreach (string raw in File.ReadAllLines(p))
                {
                    string t = (raw ?? "").Trim();
                    if (t.Length == 0 || t[0] == '#') continue;
                    if (seen.Add(t)) list.Add(t);
                }
                return list;
            }
            catch
            {
                return new List<string>();
            }
        }

        // ============ 合并（供下拉框使用） ============

        private static IReadOnlyList<string> _combined;
        private static readonly object _combinedSync = new object();

        /// <summary>
        /// 下拉框使用的合并列表：启用的服务器代理 + 用户自定义代理 + 直连标识。
        /// 启用/禁用状态变化后需调用 <see cref="InvalidateCombined"/> 刷新。
        /// </summary>
        public static IReadOnlyList<string> Proxies
        {
            get
            {
                if (_combined != null) return _combined;
                lock (_combinedSync)
                {
                    if (_combined != null) return _combined;
                    var list = new List<string>();
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var u in ApiProxyUrls)
                        if (seen.Add(u)) list.Add(u);
                    foreach (var u in UserProxies)
                        if (seen.Add(u)) list.Add(u);
                    // 兜底：全部为空时用内置
                    if (list.Count == 0) list.AddRange(BuiltinProxies);
                    list.Add(DirectMarker);
                    _combined = list;
                    return _combined;
                }
            }
        }

        /// <summary>使合并缓存失效（启用/禁用、保存用户列表后调用）</summary>
        public static void InvalidateCombined()
        {
            lock (_combinedSync) _combined = null;
        }

        // ============ 工具 ============

        /// <summary>是否为 GitHub URL（含 github.com 与 *.github.com 子域）</summary>
        public static bool IsGithub(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var u = new Uri(url);
                string host = (u.Host ?? "").ToLowerInvariant();
                return host == "github.com" || host.EndsWith(".github.com");
            }
            catch { return false; }
        }

        /// <summary>
        /// 拼装代理 URL：原始链接（github.com）→ 直连；其他 base → "{base.TrimEnd('/')}/{originalUrl}"。
        /// 原始 URL 自身已经以代理域名为前缀时不再二次代理。
        /// </summary>
        public static string Apply(string originalUrl, string proxyBase)
        {
            if (string.IsNullOrEmpty(originalUrl)) return originalUrl;
            if (string.IsNullOrEmpty(proxyBase) || proxyBase == DirectMarker) return originalUrl;
            try
            {
                if (originalUrl.StartsWith(proxyBase, StringComparison.OrdinalIgnoreCase))
                    return originalUrl;   // 已是代理 URL，不再二次代理
            }
            catch { /* 启动异常 → 走下面的常规拼装 */ }
            return proxyBase.TrimEnd('/') + "/" + originalUrl;
        }
    }
}
