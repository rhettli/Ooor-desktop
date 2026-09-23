using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>HF 模型搜索结果条目（/api/models 精简字段；字段名与 JSON 严格一致，JavaScriptSerializer 区分大小写）</summary>
    public class HfModel
    {
        public string id;
        public long downloads;
        public long likes;
        public string pipeline_tag;
        public object gated;        // 可能是 bool 或 "auto"/"manual" 字符串
        public string lastModified;

        /// <summary>gated 仓库（Llama 官方系等）有授权限制，禁止分发下载</summary>
        public bool IsGated
        {
            get
            {
                if (gated is bool b) return b;
                if (gated is string s) return !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase);
                return false;
            }
        }
    }

    /// <summary>HF tree 接口文件条目（ooor 网关 / hf-mirror 直连两种来源统一）</summary>
    public class HfFile
    {
        public string path;
        public long size;
        public string type;
        public bool cached;         // 仅 ooor 网关返回（本地缓存命中可直接下载）
        public bool is_gguf;

        public bool IsGGUF => is_gguf ||
            (path != null && path.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase));

        public bool IsMmproj => path != null && path.StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase);

        /// <summary>tree 直连返回 "file"/"blob" 才是文件</summary>
        public bool IsFile => type == "file" || type == "blob" || string.IsNullOrEmpty(type);
    }

    /// <summary>
    /// HF API 客户端（搜索 / 趋势榜 / 文件列表），双来源：
    ///   - ooor 网关：{server}/api/v1/search、{server}/api/v1/files（带 cached 标记）
    ///   - hf-mirror 直连：/api/models?search=、/api/models/{repo}/tree/main（Link 分页）
    /// </summary>
    public static class HfApi
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly JavaScriptSerializer _ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        /// <summary>搜索模型（按下载量降序）；keyword 为空时自动改为趋势榜。
        /// viaOoor=true 时 baseUrl 为网关地址，否则为 hf-mirror。
        /// ggufOnly=true 只返回带 GGUF 权重的仓库（对应官网 ?apps=llama.cpp 的筛选）。</summary>
        public static async Task<List<HfModel>> SearchAsync(string baseUrl, bool viaOoor, string keyword, int limit,
            string bearerToken, CancellationToken ct, bool ggufOnly = true)
        {
            // 空关键词 = 趋势榜：模型市场窗口打开即调它，用户不必先输关键词
            if (string.IsNullOrEmpty(keyword) || keyword.Trim().Length == 0)
                return await TrendingAsync(baseUrl, viaOoor, limit, bearerToken, ct, ggufOnly);

            string url;
            if (viaOoor)
                url = baseUrl + "/api/v1/search?q=" + Uri.EscapeDataString(keyword) + "&limit=" + limit
                    + (ggufOnly ? "&gguf_only=1" : "");
            else
                url = baseUrl + "/api/models?search=" + Uri.EscapeDataString(keyword)
                    + "&sort=downloads&direction=-1&limit=" + limit
                    + (ggufOnly ? "&filter=gguf" : "");

            string json = await GetStringAsync(url, bearerToken, ct);
            var models = _ser.Deserialize<List<HfModel>>(json) ?? new List<HfModel>();
            return models;
        }

        /// <summary>
        /// 热门趋势榜（等价官网 models?apps=llama.cpp&amp;sort=trending 的默认列表）：
        /// 不带关键词，直接按 HuggingFace 热度返回当前最流行、且能跑的仓库。
        /// 注意上游只认 sort=trendingScore，传 sort=trending 会返回 400，故此处做映射。
        /// </summary>
        public static async Task<List<HfModel>> TrendingAsync(string baseUrl, bool viaOoor, int limit,
            string bearerToken, CancellationToken ct, bool ggufOnly = true)
        {
            string url;
            if (viaOoor)
                url = baseUrl + "/api/v1/search?sort=trending&limit=" + limit
                    + (ggufOnly ? "&gguf_only=1" : "");
            else
                url = baseUrl + "/api/models?sort=trendingScore&direction=-1&limit=" + limit
                    + (ggufOnly ? "&filter=gguf" : "");

            string json = await GetStringAsync(url, bearerToken, ct);
            return _ser.Deserialize<List<HfModel>>(json) ?? new List<HfModel>();
        }

        /// <summary>列出仓库文件（只返回文件条目）。两种来源响应结构不同，此处统一。
        /// 结果按「源 + 仓库」缓存到磁盘，未过期时直接返回，不再请求上游。</summary>
        public static async Task<List<HfFile>> ListFilesAsync(string baseUrl, bool viaOoor, string repo,
            string bearerToken, CancellationToken ct)
        {
            string cacheKey = FilesCacheKey(baseUrl, viaOoor, repo);
            string cached = HfCache.Read(cacheKey);
            if (cached != null)
            {
                try
                {
                    var hit = _ser.Deserialize<List<HfFile>>(cached);
                    if (hit != null) return hit;
                }
                catch { /* 缓存内容损坏：照常走网络，下面会重写 */ }
            }

            var files = await FetchFilesAsync(baseUrl, viaOoor, repo, bearerToken, ct);
            HfCache.Write(cacheKey, _ser.Serialize(files));
            return files;
        }

        /// <summary>文件列表缓存键：源 + 服务地址 + 仓库（换源/换网关地址互不串用）</summary>
        private static string FilesCacheKey(string baseUrl, bool viaOoor, string repo)
            => "files|" + (viaOoor ? "ooor" : "mirror") + "|" + baseUrl + "|" + repo;

        /// <summary>ooor 网关文件列表接口地址（取回与清缓存两处共用，避免拼法走样）</summary>
        private static string OoorFilesUrl(string baseUrl, string repo)
            => baseUrl + "/api/v1/files?repo=" + Uri.EscapeDataString(repo);

        /// <summary>
        /// 清除某仓库文件列表的本地磁盘缓存（模型市场右键「清除缓存刷新文件」调用）。
        /// 必须清两处：ListFilesAsync 自己按 FilesCacheKey 写的「合并结果」缓存，
        /// 以及 ooor 源下 GetStringAsync 按 URL 写的 /api/v1/files 响应缓存——
        /// 只清前者的话，重新拉取会再次命中后者、拿回同一份旧数据，刷新等于没刷。
        /// 网关侧不缓存文件列表（每次实时问上游），故清本地即可见效。
        /// 返回实际清掉的条目数（0 = 本来就没有缓存）。
        /// </summary>
        public static int ClearFilesCache(string baseUrl, bool viaOoor, string repo)
        {
            int removed = 0;
            if (HfCache.Remove(FilesCacheKey(baseUrl, viaOoor, repo))) removed++;
            if (viaOoor && HfCache.Remove(OoorFilesUrl(baseUrl, repo))) removed++;
            return removed;
        }

        /// <summary>请求上游取回文件列表（ooor 网关一次取回；hf-mirror 直连需按 Link 翻页）</summary>
        private static async Task<List<HfFile>> FetchFilesAsync(string baseUrl, bool viaOoor, string repo,
            string bearerToken, CancellationToken ct)
        {
            if (viaOoor)
            {
                string json = await GetStringAsync(OoorFilesUrl(baseUrl, repo), bearerToken, ct);
                // 网关响应：{repo, count, files:[{path,size,cached,is_gguf}]}
                var wrapper = _ser.Deserialize<FilesResponse>(json);
                return wrapper?.files ?? new List<HfFile>();
            }

            // hf-mirror 直连：tree 接口裸数组 + Link header 分页翻完。
            // recursive=true 必须带：GGUF 大仓按量化方案分目录（UD-Q4_K_XL/、BF16/、MTP/ 等），
            // recursive=false 只返回根目录，子目录里的模型会被整批漏掉。
            var files = new List<HfFile>();
            string url = baseUrl + "/api/models/" + repo + "/tree/main?recursive=true";
            while (!string.IsNullOrEmpty(url) && files.Count < 2000)
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "1.0"));
                    if (!string.IsNullOrEmpty(bearerToken))
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                    using (var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct))
                    {
                        if (!resp.IsSuccessStatusCode)
                            throw new HttpRequestException("tree 接口返回 " + (int)resp.StatusCode);
                        string json = await resp.Content.ReadAsStringAsync();
                        var page = _ser.Deserialize<List<HfFile>>(json) ?? new List<HfFile>();
                        files.AddRange(page);
                        // 解析 Link: <url>; rel="next"（.NET FW 4.8 无 Headers.Link 强类型，取原始值）
                        IEnumerable<string> linkValues;
                        if (resp.Headers.TryGetValues("Link", out linkValues))
                            url = ParseNextLink(string.Join(",", linkValues));
                        else
                            url = null;
                    }
                }
            }
            return files;
        }

        /// <summary>
        /// 拼下载 URL（Range 续传兼容）。
        ///   - ooor 网关：{server}/dl/{repo}/resolve/main/{file}（网关下载路由挂在 /dl 下）
        ///   - hf-mirror：{mirror}/{repo}/resolve/main/{file}
        /// filename 可为仓库内子目录路径（UD-Q4_K_XL/xxx.gguf），逐段编码保留分隔符。
        /// </summary>
        public static string BuildDownloadUrl(string baseUrl, string repo, string filename, bool viaOoor = false)
        {
            string root = (baseUrl ?? "").TrimEnd('/');
            if (viaOoor) root += "/dl";
            return root + "/" + EncodeUrlPath(repo) + "/resolve/main/" + EncodeUrlPath(filename);
        }

        /// <summary>
        /// 按路径段编码：保留 / 作为分隔符，其余段各自转义。
        /// 整段 Uri.EscapeDataString 会把 / 编成 %2F，上游 WAF 与本网关都会直接拒绝。
        /// </summary>
        public static string EncodeUrlPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = Uri.EscapeDataString(parts[i]);
            return string.Join("/", parts);
        }

        /// <summary>GET 文本：命中未过期的磁盘缓存直接返回（省一次上游请求），否则请求并写缓存</summary>
        private static async Task<string> GetStringAsync(string url, string bearerToken, CancellationToken ct)
        {
            string cached = HfCache.Read(url);
            if (cached != null) return cached;

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("Ooor", "1.0"));
                if (!string.IsNullOrEmpty(bearerToken))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                using (var resp = await _http.SendAsync(req, ct))
                {
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException("服务器返回 " + (int)resp.StatusCode + " " + resp.ReasonPhrase);
                    string text = await resp.Content.ReadAsStringAsync();
                    HfCache.Write(url, text);
                    return text;
                }
            }
        }

        private static string ParseNextLink(string linkHeader)
        {
            if (string.IsNullOrEmpty(linkHeader)) return null;
            foreach (string seg in linkHeader.Split(','))
            {
                if (!seg.Contains("rel=\"next\"")) continue;
                int lt = seg.IndexOf('<');
                int gt = seg.IndexOf('>');
                if (lt >= 0 && gt > lt) return seg.Substring(lt + 1, gt - lt - 1);
            }
            return null;
        }

        private class FilesResponse
        {
            public string repo;
            public int count;
            public List<HfFile> files;
        }
    }
}
