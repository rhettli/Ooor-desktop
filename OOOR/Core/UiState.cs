using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// 轻量 UI 状态持久化：{ConfigRoot}\ui-state.conf，每行 key=value，# 开头为注释。
    ///
    /// 用于跨进程记住窗口 UI 细节（当前选中索引等），与 model_meta.conf /
    /// github-proxy.txt 同款"纯文本 + 忽略异常"风格：
    ///   - 读写失败静默回落默认值，绝不让 UI 状态文件影响主流程；
    ///   - 内存不缓存——每次 Get 直读文件，保证多窗口/外部编辑即时生效；
    ///   - Set 采用"读全文件 → 改一行 → 写全文件"的最简单实现。
    /// </summary>
    public static class UiState
    {
        /// <summary>状态文件名（{ConfigRoot} 下）</summary>
        public const string FileName = "ui-state.conf";

        /// <summary>状态文件完整路径</summary>
        public static string ConfigPath => Path.Combine(LlamaRuntime.ConfigRoot, FileName);

        /// <summary>读取字符串值；文件缺失 / key 不存在 / 读取异常 → 返回 default</summary>
        public static string Get(string key, string def = null)
        {
            try
            {
                var p = ConfigPath;
                if (!File.Exists(p)) return def;
                foreach (string raw in File.ReadAllLines(p, Encoding.UTF8))
                {
                    string line = (raw ?? "").Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return line.Substring(eq + 1).Trim();
                }
                return def;
            }
            catch { return def; }
        }

        /// <summary>读取 int 值；解析失败 / 不存在 → 返回 def</summary>
        public static int GetInt(string key, int def = -1)
        {
            string s = Get(key);
            return int.TryParse(s, out int v) ? v : def;
        }

        /// <summary>写入（或更新已有行）一个 key=value；文件头自动带注释说明。</summary>
        public static void Set(string key, string value)
        {
            try
            {
                var p = ConfigPath;
                var lines = new List<string>();
                bool replaced = false;

                if (File.Exists(p))
                {
                    foreach (string raw in File.ReadAllLines(p, Encoding.UTF8))
                    {
                        string line = (raw ?? "").Trim();
                        int eq = line.IndexOf('=');
                        bool isKey = eq > 0
                            && string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase);
                        if (isKey)
                        {
                            lines.Add(key + "=" + (value ?? ""));
                            replaced = true;
                        }
                        else
                        {
                            // 保留原行（含注释/空行），但用 Trim 后的副本防尾随空白
                            lines.Add(raw);
                        }
                    }
                }
                if (!replaced) lines.Add(key + "=" + (value ?? ""));

                // 目录可能不存在（首次写）
                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(p, lines, Encoding.UTF8);
            }
            catch
            {
                // UI 状态写不进去就算了，不影响主流程
            }
        }
    }
}