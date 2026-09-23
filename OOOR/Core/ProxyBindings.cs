using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// URL → GitHub 加速镜像 绑定持久化（每个 URL 独立一个 .proxy 文件，避免单文件全量重写）。
    ///   - 目录：{ConfigRoot}/proxies/
    ///   - 文件名：MD5(URL) + ".proxy"
    ///   - 文件内容：代理 URL（一行字符串，UTF-8 无 BOM）
    ///   - 写入时机：用户在「下载管理」窗口对某个任务切换代理后（FileDownloadForm.mi_Proxy_Click）
    ///   - 读取时机：DownloadManager.Enqueue 入队时，proxyUrl 参数留空时回落到此处
    /// 优点：
    ///   - 单 URL 切换代理只写一个 ~50 字节的小文件（不重写全量表）
    ///   - 各 URL 之间完全独立（独立文件），不存在"半写入污染整张映射"
    ///   - 无需启动期全量加载（懒加载：按 URL 直接读对应文件）
    ///   - 单文件读/写失败不影响其它 URL，也不阻塞启动/入队
    /// </summary>
    public static class ProxyBindings
    {
        private const string DirName = "proxies";
        private const string Ext = ".proxy";

        /// <summary>代理绑定持久化目录（{ConfigRoot}/proxies/）</summary>
        public static string ConfigDir => Path.Combine(LlamaRuntime.ConfigRoot, DirName);

        /// <summary>启动期显式预热；其余场景无需调用（Get/Set 都是按 URL 直接 IO）</summary>
        public static void Load()
        {
            try { Directory.CreateDirectory(ConfigDir); }
            catch { /* 目录创建失败不影响功能：Set 时还会再尝试 */ }
        }

        /// <summary>查 URL 上次绑定的代理；未记录或读盘失败均返回 null</summary>
        public static string Get(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var path = FilePathFor(url);
                if (!File.Exists(path)) return null;
                var s = File.ReadAllText(path, Encoding.UTF8).Trim();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            catch
            {
                // 单文件读失败：当作未绑定处理；不影响入队主流程
                return null;
            }
        }

        /// <summary>绑定 URL → proxyUrl（立即同步写盘；原子重命名避免读到半写入）</summary>
        public static void Set(string url, string proxyUrl)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(proxyUrl)) return;
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var path = FilePathFor(url);
                // 写临时文件再原子重命名，避免半写入破坏配置
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, proxyUrl, new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch
            {
                // 单条写入失败：不影响当前任务使用新代理（内存已生效），也不影响其它 URL
            }
        }

        // ---------- 内部 ----------

        /// <summary>URL → 文件路径（MD5 命名，不暴露原始 URL）</summary>
        private static string FilePathFor(string url)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(url ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return Path.Combine(ConfigDir, sb.ToString() + Ext);
            }
        }
    }
}