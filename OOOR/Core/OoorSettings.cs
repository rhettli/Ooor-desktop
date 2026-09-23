using System;
using System.IO;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// ooor 加速源设置（{ConfigRoot}\ooor.conf，key=value 行格式）：
    ///   server_url=https://ooor.cc       ← ooor-gateway 服务地址
    ///   token=xxxx                       ← 登录令牌（Bearer，可留空表示匿名）
    ///
    /// 默认地址按编译配置切换（ooor.conf 未显式配置 server_url 时生效）：
    ///   Debug（调试）  → http://127.0.0.1:8080（本地 go 网关）
    ///   Release（正式） → https://ooor.cc
    /// </summary>
    public sealed class OoorSettings
    {
        public const string SettingsFileName = "ooor.conf";

        public static string SettingsPath => Path.Combine(LlamaRuntime.ConfigRoot, SettingsFileName);

#if DEBUG
        /// <summary>调试默认地址：本地 go 网关（ooor-gateway，监听 :8080）</summary>
        public const string DefaultServerUrl = "http://127.0.0.1:8080";
#else
        /// <summary>正式默认地址：线上加速服务</summary>
        public const string DefaultServerUrl = "https://ooor.cc";
#endif

        /// <summary>ooor 网关服务地址（含协议与端口，结尾不带 /）</summary>
        public string ServerUrl = "";

        /// <summary>Bearer 令牌（M1 网关默认允许匿名，可为空）</summary>
        public string Token = "";

        /// <summary>是否已配置（默认地址兜底后始终可用）</summary>
        public bool HasServer => !string.IsNullOrWhiteSpace(ServerUrl);

        /// <summary>
        /// ooor.conf 里是否**显式**写了 server_url。
        /// 区分「用户指定了地址」与「落到编译期默认值（Debug=本地网关）」：
        /// 更新检查据此决定是听配置还是直接问官网（见 OoorUpdate.CurrentServerUrl）。
        /// </summary>
        public bool ServerUrlConfigured;

        public static OoorSettings Load()
        {
            var s = new OoorSettings();
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    // 未配置 → 使用编译期默认地址（Debug=本地网关，Release=线上）
                    s.ServerUrl = DefaultServerUrl;
                    return s;
                }
                foreach (string line in File.ReadAllLines(SettingsPath, Encoding.UTF8))
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    int eq = t.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = t.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = t.Substring(eq + 1).Trim();
                    if (key == "server_url")
                    {
                        s.ServerUrl = val.TrimEnd('/');
                        s.ServerUrlConfigured = s.ServerUrl.Length > 0;
                    }
                    else if (key == "token") s.Token = val;
                }
            }
            catch { /* 读取失败按默认值 */ }
            // 配置文件里 server_url 为空 → 回退编译期默认地址
            if (string.IsNullOrWhiteSpace(s.ServerUrl)) s.ServerUrl = DefaultServerUrl;
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
                var sb = new StringBuilder();
                sb.AppendLine("# ooor 加速源设置（模型市场下载用）");
                sb.AppendLine("# 令牌获取：访问服务地址 /api/v1/login（M1 阶段网关默认允许匿名下载，token 可留空）");
                sb.AppendLine("server_url=" + ServerUrl);
                sb.AppendLine("token=" + Token);
                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
                // 保存失败忽略（下次仍用旧值）
            }
        }
    }
}
