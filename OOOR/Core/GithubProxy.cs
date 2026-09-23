using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ooor.Core
{
    /// <summary>
    /// GitHub 加速镜像加载与代理 URL 拼装（与 model_meta.conf / disabled_versions.conf 同款纯文本风格）。
    ///
    /// 配置：{ConfigRoot}\github-proxy.txt，每行一个 URL，# 开头为注释；
    ///   - 读取顺序即下拉框显示顺序；同 URL 自动去重（忽略大小写）；
    ///   - 文件不存在或全为空 → 回落 <see cref="BuiltinProxies"/>；
    ///   - 下拉框末尾固定追加 <see cref="DirectMarker"/>（"github.com"，= 直连不代理）。
    ///
    /// 拼装规则：{base.TrimEnd('/')}/{originalUrl}
    ///   例：https://ghproxy.net/ + https://github.com/WJQSERVER-STUDIO/.../file.zip
    ///     = https://ghproxy.net/https://github.com/WJQSERVER-STUDIO/.../file.zip
    /// 已以代理域名为前缀的 URL 不二次代理，避免链式拼接。
    /// </summary>
    public static class GithubProxy
    {
        /// <summary>配置文件名（{ConfigRoot} 下）</summary>
        public const string FileName = "github-proxy.txt";

        /// <summary>配置文件完整路径</summary>
        public static string ConfigPath => System.IO.Path.Combine(LlamaRuntime.ConfigRoot, FileName);

        /// <summary>下拉框末尾固定追加的"原始链接"标识，显示为 "github.com"</summary>
        public const string DirectMarker = "github.com";

        /// <summary>硬编码兜底镜像列表（文件读不到或全空时使用）</summary>
        public static readonly IReadOnlyList<string> BuiltinProxies = new[]
        {
            "https://ghproxy.net/",
            "https://gh-proxy.org/",
        };

        private static IReadOnlyList<string> _loaded;
        private static readonly object _sync = new object();

        /// <summary>
        /// 镜像代理列表（不含 DirectMarker），按文件顺序、去重。
        /// 首次访问时从配置文件读取并缓存；文件缺失/全空时回落内置默认。
        /// </summary>
        public static IReadOnlyList<string> Proxies
        {
            get
            {
                if (_loaded != null) return _loaded;
                lock (_sync)
                {
                    if (_loaded != null) return _loaded;
                    _loaded = LoadFromFile();
                    return _loaded;
                }
            }
        }

        /// <summary>从磁盘重新加载（外部修改了 txt 后可手动刷新）</summary>
        public static void Reload()
        {
            lock (_sync) _loaded = LoadFromFile();
        }

        private static IReadOnlyList<string> LoadFromFile()
        {
            try
            {
                string p = ConfigPath;
                if (!File.Exists(p)) return BuiltinProxies;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var list = new List<string>();
                foreach (string raw in File.ReadAllLines(p))
                {
                    string t = (raw ?? "").Trim();
                    if (t.Length == 0 || t[0] == '#') continue;
                    if (seen.Add(t)) list.Add(t);
                }
                return list.Count > 0 ? (IReadOnlyList<string>)list : BuiltinProxies;
            }
            catch
            {
                // 读取失败（编码异常 / 权限等）→ 回落内置默认
                return BuiltinProxies;
            }
        }

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