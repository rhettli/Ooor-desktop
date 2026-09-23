using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OoorFunc.Core
{
    /// <summary>
    /// Agent 持久化设置（{ConfigRoot}\agent.conf，key=value 行格式，UTF-8）：
    ///
    ///   allow_write=true|false      是否注册写入类工具（write_file / create_file / delete_file）
    ///   allow_command=true|false    是否注册执行类工具（run_command / execute_script）
    ///   trust_ai=true|false         危险操作是否允许模型自行判断跳过确认（工具参数 confirm=false 时生效）
    ///   allow_internet=true|false   是否注册联网工具（web_search / fetch_url）
    ///   root=D:\xxx                 额外沙盒白名单目录（可多行，累加到内置的 models / config 之上）
    ///   max_steps=12                单轮最大步数
    ///
    /// 保存策略：AgentOptions.Default() 会先铺内置目录，再 Load 本文件覆盖开关并追加 root；
    /// UI 改动开关/沙盒后调用 Save(opt) 落盘，因此「AI 助手」窗口重开后行为保持一致。
    /// </summary>
    public static class AgentSettings
    {
        public const string FileName = "agent.conf";

        /// <summary>agent.conf 完整路径（{ConfigRoot}\agent.conf）</summary>
        public static string SettingsPath
        {
            get
            {
                try { return Path.Combine(CoreEnv.ConfigRoot, FileName); }
                catch { return FileName; }
            }
        }

        /// <summary>把文件里的设置应用到 opt（不存在的项保持 opt 现值）。</summary>
        public static void Load(AgentOptions opt)
        {
            if (opt == null) return;
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string r in opt.AllowedRoots) seen.Add(Normalize(r));

                foreach (string raw in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key)
                    {
                        case "allow_write": opt.AllowWrite = ParseBool(val, opt.AllowWrite); break;
                        case "allow_command": opt.AllowCommand = ParseBool(val, opt.AllowCommand); break;
                        case "trust_ai": opt.TrustAiJudgment = ParseBool(val, opt.TrustAiJudgment); break;
                        case "allow_internet": opt.AllowInternet = ParseBool(val, opt.AllowInternet); break;
                        case "max_steps":
                            {
                                int v;
                                if (int.TryParse(val, out v) && v >= 1 && v <= 64) opt.MaxSteps = v;
                                break;
                            }
                        case "root":
                            {
                                string p = Normalize(val);
                                if (p.Length > 0 && seen.Add(p)) opt.AllowedRoots.Add(p);
                                break;
                            }
                    }
                }
            }
            catch { /* 配置损坏时按内存默认值继续，不影响使用 */ }
        }

        /// <summary>把 opt 的当前开关 + 全部沙盒目录写回 agent.conf。</summary>
        public static void Save(AgentOptions opt)
        {
            if (opt == null) return;
            try
            {
                Directory.CreateDirectory(CoreEnv.ConfigRoot);
                var sb = new StringBuilder();
                sb.AppendLine("# ooor AI 助手设置（工具权限 / 沙盒白名单）");
                sb.AppendLine("# root 行可多行，均为绝对路径；越出白名单的读写会被直接拒绝");
                sb.AppendLine("allow_write=" + (opt.AllowWrite ? "true" : "false"));
                sb.AppendLine("allow_command=" + (opt.AllowCommand ? "true" : "false"));
                sb.AppendLine("trust_ai=" + (opt.TrustAiJudgment ? "true" : "false"));
                sb.AppendLine("allow_internet=" + (opt.AllowInternet ? "true" : "false"));
                sb.AppendLine("max_steps=" + opt.MaxSteps);
                foreach (string r in opt.AllowedRoots)
                {
                    string p = Normalize(r);
                    if (p.Length > 0) sb.AppendLine("root=" + p);
                }
                File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { /* 落盘失败不影响本次会话 */ }
        }

        private static bool ParseBool(string v, bool fallback)
        {
            if (string.IsNullOrEmpty(v)) return fallback;
            v = v.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off") return false;
            return fallback;
        }

        private static string Normalize(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            try { return Path.GetFullPath(p.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return ""; }
        }
    }
}
