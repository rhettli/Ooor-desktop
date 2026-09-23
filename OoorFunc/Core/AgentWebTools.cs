using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace OoorFunc.Core
{
    /// <summary>
    /// 联网工具共用的 HTTP 支持：静态复用 HttpClient、浏览器 UA、TLS1.2、自动 gzip、按响应 charset 解码。
    /// 仅做只读 GET；不携带本机凭据。仅在用户开启「允许联网」时使用。
    /// </summary>
    public static class WebHttp
    {
        // UA 大版本要与下面 sec-ch-ua 里的版本保持一致；版本号过旧+缺少 sec-* 头会被
        // Cloudflare 等 WAF 判为“伪造浏览器”直接 403（即使 UA 字符串本身合法）
        private const string ChromeVersion = "131";
        private const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        private static readonly HttpClient Client;

        static WebHttp()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
            catch { }

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            Client = new HttpClient(handler);
            try
            {
                Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                Client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                Client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
                // 现代 Chrome 必发的客户端提示与导航头：缺这些头却自称 Chrome，
                // 容易被 WAF 指纹识别为伪造客户端而返回 403
                TryAdd("sec-ch-ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"" + ChromeVersion + "\", \"Google Chrome\";v=\"" + ChromeVersion + "\"");
                TryAdd("sec-ch-ua-mobile", "?0");
                TryAdd("sec-ch-ua-platform", "\"Windows\"");
                TryAdd("Sec-Fetch-Dest", "document");
                TryAdd("Sec-Fetch-Mode", "navigate");
                TryAdd("Sec-Fetch-Site", "none");
                TryAdd("Sec-Fetch-User", "?1");
                TryAdd("Upgrade-Insecure-Requests", "1");
            }
            catch { }
            // 注意：不能声明 br（Brotli）——.NET Framework 4.8 的 HttpClient 只支持 gzip/deflate 解压，
            // 声明后收到 br 响应体会乱码；Accept-Encoding 由 AutomaticDecompression 自动带上
        }

        /// <summary>添加受限/非标头（sec-* 等含特殊字符，必须走不校验的添加方式）。</summary>
        private static void TryAdd(string name, string value)
        {
            try { Client.DefaultRequestHeaders.TryAddWithoutValidation(name, value); } catch { }
        }

        /// <summary>GET 一个网页，返回解码后的正文（自动识别 gzip / charset）。失败抛异常。</summary>
        public static string Get(string url, string referer, int timeoutSeconds)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(referer))
            {
                try { req.Headers.Referrer = new Uri(referer); } catch { }
            }

            HttpResponseMessage resp;
            try
            {
                resp = Client.SendAsync(req).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                string msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new InvalidOperationException("网络请求失败（" + msg + "）");
            }

            using (resp)
            {
                byte[] bytes = resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                {
                    // 403/401 等：带回响应体片段与服务器标识，方便分辨是 Cloudflare 挑战、
                    // WAF 拦截还是站点本身权限问题（curl 能过时多半是客户端指纹差异）
                    Encoding encErr = DetectEncoding(resp, bytes);
                    string snippet = encErr.GetString(bytes);
                    if (snippet.Length > 300) snippet = snippet.Substring(0, 300);
                    snippet = Regex.Replace(snippet, @"\s+", " ").Trim();
                    string server = "";
                    try { server = resp.Headers.Server != null ? resp.Headers.Server.ToString() : ""; } catch { }
                    throw new InvalidOperationException(
                        "HTTP " + (int)resp.StatusCode + " " + resp.ReasonPhrase
                        + (string.IsNullOrEmpty(server) ? "" : "（Server: " + server + "）")
                        + (snippet.Length > 0 ? "；响应片段：" + snippet : ""));
                }

                Encoding enc = DetectEncoding(resp, bytes);
                return enc.GetString(bytes);
            }
        }

        private static Encoding DetectEncoding(HttpResponseMessage resp, byte[] bytes)
        {
            // 1) 响应头 charset
            try
            {
                string cs = resp.Content.Headers.ContentType != null ? resp.Content.Headers.ContentType.CharSet : null;
                if (!string.IsNullOrEmpty(cs)) return Encoding.GetEncoding(cs.Trim().Trim('"'));
            }
            catch { }

            // 2) HTML <meta charset=...>（用 ASCII 嗅探前 4KB）
            try
            {
                int n = Math.Min(bytes.Length, 4096);
                string head = Encoding.ASCII.GetString(bytes, 0, n);
                Match m = Regex.Match(head, @"charset\s*=\s*[""']?\s*([\w-]+)", RegexOptions.IgnoreCase);
                if (m.Success) return Encoding.GetEncoding(m.Groups[1].Value.Trim());
            }
            catch { }

            // 3) 兜底：UTF-8（坏字节替换，不抛异常）
            return new UTF8Encoding(false, false);
        }
    }

    /// <summary>联网工具的参数读取（模型可能给 "1" / true 等，统一容错）。</summary>
    public static class WebArgs
    {
        public static string Str(IDictionary<string, object> args, string key, bool required)
        {
            object v;
            string s = args != null && args.TryGetValue(key, out v) && v != null ? v.ToString() : null;
            if (string.IsNullOrEmpty(s))
            {
                if (required) throw new ArgumentException("缺少参数：" + key);
                return null;
            }
            return s;
        }

        public static int Int(IDictionary<string, object> args, string key, int dflt, int min, int max)
        {
            object v;
            if (args == null || !args.TryGetValue(key, out v) || v == null) return dflt;
            int n;
            if (!int.TryParse(v.ToString(), out n)) return dflt;
            return n < min ? min : (n > max ? max : n);
        }

        public static bool Bool(IDictionary<string, object> args, string key, bool dflt)
        {
            object v;
            if (args == null || !args.TryGetValue(key, out v) || v == null) return dflt;
            string s = v.ToString().Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes") return true;
            if (s == "false" || s == "0" || s == "no") return false;
            return dflt;
        }
    }

    /// <summary>HTML → 纯文本工具（结果摘要是 HTML 片段，需要去掉标签与实体）。</summary>
    public static class WebHtml
    {
        private static readonly Regex ScriptStyle = new Regex(
            @"<(script|style|noscript|template)\b[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase);
        private static readonly Regex Comment = new Regex(@"<!--[\s\S]*?-->");
        private static readonly Regex Tag = new Regex(@"<[^>]+>");
        private static readonly Regex Ws = new Regex(@"[ \t\r\n\u00A0]+");
        private static readonly Regex InlineWs = new Regex(@"[ \t\u00A0]+");

        /// <summary>把一段 HTML 压成一行纯文本（用于标题 / 摘要）。</summary>
        public static string CleanText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string s = ScriptStyle.Replace(html, " ");
            s = Comment.Replace(s, " ");
            s = Tag.Replace(s, " ");
            s = WebUtility.HtmlDecode(s);
            s = Ws.Replace(s, " ");
            return s.Trim();
        }

        /// <summary>把整页 HTML 转成可读纯文本（块级标签换行、去脚本样式、解实体）。</summary>
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return "";
            string s = ScriptStyle.Replace(html, " ");
            s = Comment.Replace(s, " ");
            s = Regex.Replace(s, @"<(br|/p|/div|/li|/h[1-6]|/tr|/section|/article)\s*/?>", "\n", RegexOptions.IgnoreCase);
            s = Tag.Replace(s, " ");
            s = WebUtility.HtmlDecode(s);
            s = InlineWs.Replace(s, " ");
            s = Regex.Replace(s, @" *\n *", "\n");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }
    }

    /// <summary>一条搜索结果。</summary>
    public sealed class WebHit
    {
        public string Title = "";
        public string Url = "";
        public string Snippet = "";
    }

    /// <summary>
    /// 联网搜索：抓取搜索引擎结果页并解析出「标题 + 链接 + 摘要」。
    /// 多引擎可选（bing / baidu / google / duckduckgo / sogou），默认 bing（cn.bing.com，国内可达性较好）。
    /// 仅在用户开启「允许联网」时注册。
    /// </summary>
    public sealed class WebSearchTool : IAgentTool
    {
        public string Name { get { return "web_search"; } }

        public string Description
        {
            get
            {
                return "联网搜索互联网，返回若干条「标题 + 链接 + 摘要」。需要实时信息、新闻、资料查询时使用；" +
                       "要读某条结果的正文，再用 fetch_url 打开它的链接。engine 可选 bing|baidu|google|duckduckgo|sogou（默认 bing）。" +
                       "本工具不能访问本机文件；被搜索引擎拦截时换一个 engine 重试即可。";
            }
        }

        public IDictionary<string, object> ParametersSchema
        {
            get
            {
                return new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "query", new Dictionary<string, object> { { "type", "string" }, { "description", "搜索关键词" } } },
                            { "engine", new Dictionary<string, object> { { "type", "string" }, { "description", "搜索引擎：bing|baidu|google|duckduckgo|sogou，默认 bing" } } },
                            { "count", new Dictionary<string, object> { { "type", "integer" }, { "description", "返回结果条数，默认 8，最大 20" } } }
                        }
                    },
                    { "required", new[] { "query" } }
                };
            }
        }

        private readonly AgentOptions _opt;
        public WebSearchTool(AgentOptions opt) { _opt = opt; }

        public AgentToolResult Execute(IDictionary<string, object> args)
        {
            string query = WebArgs.Str(args, "query", true);
            string engine = (WebArgs.Str(args, "engine", false) ?? "bing").Trim().ToLowerInvariant();
            int count = WebArgs.Int(args, "count", 8, 1, 20);
            int timeout = (int)_opt.WebTimeout.TotalSeconds;

            string url, referer;
            Func<string, List<WebHit>> parser;
            switch (engine)
            {
                case "baidu":
                    engine = "baidu";
                    url = "https://www.baidu.com/s?ie=utf-8&wd=" + Uri.EscapeDataString(query);
                    referer = "https://www.baidu.com/";
                    parser = ParseBaidu;
                    break;
                case "google":
                    engine = "google";
                    url = "https://www.google.com/search?hl=zh-CN&num=" + count + "&q=" + Uri.EscapeDataString(query);
                    referer = "https://www.google.com/";
                    parser = ParseGoogle;
                    break;
                case "duckduckgo":
                case "ddg":
                    engine = "duckduckgo";
                    url = "https://html.duckduckgo.com/html/?q=" + Uri.EscapeDataString(query);
                    referer = "https://html.duckduckgo.com/";
                    parser = ParseDuckDuckGo;
                    break;
                case "sogou":
                    engine = "sogou";
                    url = "https://www.sogou.com/web?query=" + Uri.EscapeDataString(query);
                    referer = "https://www.sogou.com/";
                    parser = ParseSogou;
                    break;
                default:
                    engine = "bing";
                    url = "https://cn.bing.com/search?setlang=zh-CN&q=" + Uri.EscapeDataString(query);
                    referer = "https://cn.bing.com/";
                    parser = ParseBing;
                    break;
            }

            string html;
            try { html = WebHttp.Get(url, referer, timeout); }
            catch (Exception ex) { return AgentToolResult.Err("搜索请求失败：" + ex.Message); }

            List<WebHit> hits;
            try { hits = parser(html); }
            catch (Exception ex) { return AgentToolResult.Err("结果解析失败：" + ex.Message); }

            if (hits.Count == 0)
                return AgentToolResult.Err(
                    "未解析到结果（可能被搜索引擎拦截、需要验证码，或页面结构变化）。可换 engine 重试：" +
                    "bing / baidu / google / duckduckgo / sogou。");

            var sb = new StringBuilder();
            sb.Append("引擎：").Append(engine).Append("　关键词：").Append(query).Append('\n');
            int shown = 0;
            foreach (WebHit h in hits)
            {
                if (shown >= count) break;
                shown++;
                sb.Append(shown).Append(". ").Append(h.Title).Append('\n');
                sb.Append("   ").Append(h.Url).Append('\n');
                if (!string.IsNullOrEmpty(h.Snippet)) sb.Append("   ").Append(h.Snippet).Append('\n');
            }
            sb.Append("（共 ").Append(shown).Append(" 条；要读正文请用 fetch_url 打开链接）");
            return AgentToolResult.Ok(sb.ToString());
        }

        // ===================== 各引擎解析 =====================

        private static List<WebHit> ParseBing(string html)
        {
            var list = new List<WebHit>();
            // 主结构：<li class="b_algo">…<h2><a href="URL">TITLE</a></h2>…<p>SNIPPET</p>…</li>
            foreach (Match b in Regex.Matches(html,
                @"<li class=""b_algo""[\s\S]*?(?=<li class=""b_algo""|</ol>|</body>|$)", RegexOptions.IgnoreCase))
            {
                string block = b.Value;
                Match a = Regex.Match(block, @"<h2[^>]*>\s*<a[^>]*href=""([^""]+)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                if (!a.Success) continue;
                string url = WebUtility.HtmlDecode(a.Groups[1].Value);
                string title = WebHtml.CleanText(a.Groups[2].Value);
                string snippet = "";
                Match p = Regex.Match(block, @"<p[^>]*>([\s\S]*?)</p>", RegexOptions.IgnoreCase);
                if (p.Success) snippet = WebHtml.CleanText(p.Groups[1].Value);
                Add(list, title, url, snippet);
            }
            if (list.Count == 0) list = ParseHeadings(html);
            return list;
        }

        private static List<WebHit> ParseBaidu(string html)
        {
            var list = new List<WebHit>();
            // 结果块：<div ... class="result c-container ...">…</div>
            foreach (Match b in Regex.Matches(html,
                @"<div[^>]*class=""[^""]*result[^""]*c-container[^""]*""[\s\S]*?(?=<div[^>]*class=""[^""]*result[^""]*c-container|$)",
                RegexOptions.IgnoreCase))
            {
                string block = b.Value;
                Match a = Regex.Match(block, @"<h3[^>]*>[\s\S]*?<a[^>]*href=""([^""]+)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                if (!a.Success) continue;
                string url = WebUtility.HtmlDecode(a.Groups[1].Value);
                string title = WebHtml.CleanText(a.Groups[2].Value);

                string snippet = "";
                Match s = Regex.Match(block,
                    @"<div[^>]*class=""[^""]*(?:c-abstract|content-right_|c-span-last|c-line-clamp)[^""]*""[^>]*>([\s\S]*?)</div>",
                    RegexOptions.IgnoreCase);
                if (s.Success) snippet = WebHtml.CleanText(s.Groups[1].Value);
                Add(list, title, url, snippet);
            }
            if (list.Count == 0) list = ParseHeadings(html);
            return list;
        }

        private static List<WebHit> ParseGoogle(string html)
        {
            var list = new List<WebHit>();
            // 结果块：<div class="g">…<a href="/url?q=REAL"><h3>TITLE</h3></a>…</div>
            foreach (Match b in Regex.Matches(html, @"<div class=""g""[\s\S]*?(?=<div class=""g""|</body>|$)", RegexOptions.IgnoreCase))
            {
                string block = b.Value;
                Match a = Regex.Match(block, @"<a[^>]*href=""([^""]+)""[^>]*>[\s\S]*?<h3[^>]*>([\s\S]*?)</h3>", RegexOptions.IgnoreCase);
                if (!a.Success) continue;
                string url = ExtractGoogleUrl(a.Groups[1].Value);
                string title = WebHtml.CleanText(a.Groups[2].Value);
                string snippet = "";
                Match s = Regex.Match(block, @"<div[^>]*data-sncf[^>]*>([\s\S]*?)</div>", RegexOptions.IgnoreCase);
                if (!s.Success) s = Regex.Match(block, @"<span[^>]*>([\s\S]*?)</span>", RegexOptions.IgnoreCase);
                if (s.Success) snippet = WebHtml.CleanText(s.Groups[1].Value);
                Add(list, title, url, snippet);
            }
            if (list.Count == 0) list = ParseHeadings(html);
            return list;
        }

        private static string ExtractGoogleUrl(string href)
        {
            string h = WebUtility.HtmlDecode(href ?? "");
            if (h.StartsWith("/url?", StringComparison.OrdinalIgnoreCase))
            {
                Match m = Regex.Match(h, @"[?&]q=([^&]+)");
                if (m.Success) return Uri.UnescapeDataString(m.Groups[1].Value);
            }
            return h;
        }

        private static List<WebHit> ParseDuckDuckGo(string html)
        {
            var list = new List<WebHit>();
            // <a rel="nofollow" class="result__a" href="URL">TITLE</a> … <a class="result__snippet" ...>SNIPPET</a>
            MatchCollection titles = Regex.Matches(html,
                @"<a[^>]*class=""[^""]*result__a[^""]*""[^>]*href=""([^""]+)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
            foreach (Match m in titles)
            {
                string url = WebUtility.HtmlDecode(m.Groups[1].Value);
                string title = WebHtml.CleanText(m.Groups[2].Value);
                string snippet = "";
                // 摘要取该锚点之后最近的 result__snippet
                int from = m.Index + m.Length;
                int to = Math.Min(html.Length, from + 2000);
                Match s = Regex.Match(html.Substring(from, to - from),
                    @"<a[^>]*class=""[^""]*result__snippet[^""]*""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                if (s.Success) snippet = WebHtml.CleanText(s.Groups[1].Value);
                Add(list, title, url, snippet);
            }
            if (list.Count == 0) list = ParseHeadings(html);
            return list;
        }

        private static List<WebHit> ParseSogou(string html)
        {
            var list = new List<WebHit>();
            foreach (Match b in Regex.Matches(html,
                @"<div[^>]*class=""[^""]*vrwrap[^""]*""[\s\S]*?(?=<div[^>]*class=""[^""]*vrwrap|$)", RegexOptions.IgnoreCase))
            {
                string block = b.Value;
                Match a = Regex.Match(block, @"<h3[^>]*>[\s\S]*?<a[^>]*href=""([^""]+)""[^>]*>([\s\S]*?)</a>", RegexOptions.IgnoreCase);
                if (!a.Success) continue;
                string url = WebUtility.HtmlDecode(a.Groups[1].Value);
                string title = WebHtml.CleanText(a.Groups[2].Value);
                string snippet = WebHtml.CleanText(block);
                if (snippet.Length > 300) snippet = snippet.Substring(0, 300);
                Add(list, title, url, snippet);
            }
            if (list.Count == 0) list = ParseHeadings(html);
            return list;
        }

        /// <summary>兜底：抓取所有「标题标签里的外链」，适配结构大改的页面。</summary>
        private static List<WebHit> ParseHeadings(string html)
        {
            var list = new List<WebHit>();
            foreach (Match m in Regex.Matches(html,
                @"<h[1-4][^>]*>[\s\S]*?<a[^>]*href=""(https?://[^""]+)""[^>]*>([\s\S]*?)</a>",
                RegexOptions.IgnoreCase))
            {
                string url = WebUtility.HtmlDecode(m.Groups[1].Value);
                string title = WebHtml.CleanText(m.Groups[2].Value);
                Add(list, title, url, "");
            }
            return list;
        }

        private static void Add(List<WebHit> list, string title, string url, string snippet)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(title)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            title = title.Trim();
            if (title.Length < 2) return;
            if (title.Length > 300) title = title.Substring(0, 300);
            snippet = (snippet ?? "").Trim();
            if (snippet.Length > 400) snippet = snippet.Substring(0, 400);

            foreach (WebHit h in list)
                if (string.Equals(h.Url, url, StringComparison.OrdinalIgnoreCase)) return;

            list.Add(new WebHit { Title = title, Url = url, Snippet = snippet });
        }
    }

    /// <summary>
    /// 抓取指定 URL 的网页并转成纯文本（便于模型阅读搜索结果正文）。
    /// raw=true 时返回原始 HTML（截断）。仅在用户开启「允许联网」时注册。
    /// </summary>
    public sealed class FetchUrlTool : IAgentTool
    {
        public string Name { get { return "fetch_url"; } }

        public string Description
        {
            get
            {
                return "打开一个 http/https 网址并读取其内容（默认转成纯文本，raw=true 返回原始 HTML）。" +
                       "常配合 web_search：先搜索拿到链接，再用本工具读正文。超长内容会被截断。";
            }
        }

        public IDictionary<string, object> ParametersSchema
        {
            get
            {
                return new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>
                        {
                            { "url", new Dictionary<string, object> { { "type", "string" }, { "description", "要打开的完整网址（http/https）" } } },
                            { "max_chars", new Dictionary<string, object> { { "type", "integer" }, { "description", "返回内容上限字符数，默认 20000，最大 200000" } } },
                            { "raw", new Dictionary<string, object> { { "type", "boolean" }, { "description", "是否返回原始 HTML，默认 false（转纯文本）" } } }
                        }
                    },
                    { "required", new[] { "url" } }
                };
            }
        }

        private readonly AgentOptions _opt;
        public FetchUrlTool(AgentOptions opt) { _opt = opt; }

        public AgentToolResult Execute(IDictionary<string, object> args)
        {
            string url = WebArgs.Str(args, "url", true).Trim();
            int maxChars = WebArgs.Int(args, "max_chars", 20000, 200, 200000);
            bool raw = WebArgs.Bool(args, "raw", false);
            int timeout = (int)_opt.WebTimeout.TotalSeconds;

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return AgentToolResult.Err("仅支持 http/https 网址：" + url);

            string content;
            try { content = WebHttp.Get(url, null, timeout); }
            catch (Exception ex) { return AgentToolResult.Err("打开网页失败：" + ex.Message); }

            string text = raw ? content : WebHtml.ToPlainText(content);
            bool truncated = false;
            if (text.Length > maxChars) { text = text.Substring(0, maxChars); truncated = true; }

            var sb = new StringBuilder();
            sb.Append("URL：").Append(url).Append('\n');
            sb.Append("格式：").Append(raw ? "HTML" : "纯文本").Append("　长度：").Append(text.Length).Append('\n');
            sb.Append("----\n");
            sb.Append(text);
            if (truncated) sb.Append("\n…（已截断到 ").Append(maxChars).Append(" 字符）");

            if (string.IsNullOrWhiteSpace(text))
                sb.Append("（页面没有可提取的文本内容，可能全部由脚本渲染）");

            return AgentToolResult.Ok(sb.ToString());
        }
    }
}
