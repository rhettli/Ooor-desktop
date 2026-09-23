using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ooor.Core
{
    /// <summary>
    /// MCP 服务器扫描：在主程序目录下的 mcp 子目录中查找可执行文件（.exe/.bat/.cmd/.ps1）。
    /// 每个 MCP 程序可作为函数提供方被 Agent 绑定；绑定后聊天时由宿主拉起该子进程，
    /// 通过 stdio 与模型交互（具体拉起与协议适配在后续版本实现，本类只负责发现与枚举）。
    /// </summary>
    public static class McpScanner
    {
        private static readonly string[] ExeExts = { ".exe", ".bat", ".cmd", ".ps1" };

        /// <summary>
        /// 扫描 {AppBase}\mcp 与 {ConfigRoot}\mcp 两个目录，返回发现的 MCP 可执行文件信息。
        /// 标识格式：「名称|完整路径」，与 AgentRecord.BoundMcp 一致。
        /// </summary>
        public static List<McpServerInfo> Scan()
        {
            var result = new List<McpServerInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dir in GetSearchDirs())
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                string[] files;
                try { files = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (string f in files)
                {
                    string ext = Path.GetExtension(f);
                    if (string.IsNullOrEmpty(ext)) continue;
                    if (!ExeExts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;
                    if (!seen.Add(f)) continue;   // 两个目录都有时去重

                    string name = Path.GetFileNameWithoutExtension(f);
                    result.Add(new McpServerInfo
                    {
                        Name = name,
                        Path = f,
                        Id = name + "|" + f
                    });
                }
            }
            return result;
        }

        private static IEnumerable<string> GetSearchDirs()
        {
            string baseDir;
            try { baseDir = AppDomain.CurrentDomain.BaseDirectory; }
            catch { baseDir = ""; }
            if (!string.IsNullOrEmpty(baseDir))
                yield return Path.Combine(baseDir, "mcp");

            string cfgRoot;
            try { cfgRoot = LlamaRuntime.ConfigRoot; }
            catch { cfgRoot = ""; }
            if (!string.IsNullOrEmpty(cfgRoot))
                yield return Path.Combine(cfgRoot, "mcp");
        }
    }

    /// <summary>一个可被绑定的 MCP 服务器</summary>
    public class McpServerInfo
    {
        public string Name { get; set; }
        public string Path { get; set; }
        /// <summary>绑定标识：「名称|路径」，存入 AgentRecord.BoundMcp</summary>
        public string Id { get; set; }
    }
}
