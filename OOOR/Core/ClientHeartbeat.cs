using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace ooor.Core
{
    /// <summary>
    /// 客户端心跳：后台计算设备指纹（物理硬盘序列号 SHA1）+ 采集硬件概况，
    /// 启动时向 ooor 服务 POST /api/v1/heartbeat 匿名全量上报（失败重试 6 次，间隔 10 分钟），
    /// 成功后每 2 小时 POST /api/v1/heartbeat/ping 轻量刷新 last_at。
    /// 全流程吞异常、带超时，绝不影响主程序启动与 UI。
    /// </summary>
    internal static class ClientHeartbeat
    {
        private const int WmiTimeoutSec = 3;
        private const int HttpTimeoutSec = 5;

        /// <summary>首次上报失败后的重试次数（合计最多 1 + 6 = 7 次尝试）。</summary>
        private const int StartupMaxRetries = 6;
        private static readonly TimeSpan StartupRetryInterval = TimeSpan.FromMinutes(10);
        /// <summary>启动上报成功后，在线轻量心跳间隔（只刷 last_at，不带数据）。</summary>
        private static readonly TimeSpan PingInterval = TimeSpan.FromHours(2);

        // 尊重系统代理设置：用户配了代理（如 Clash）就按代理走，不做绕行
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(HttpTimeoutSec)
        };

        private static int _started;

        /// <summary>启动后台心跳循环（立即返回）；重复调用只生效一次。
        /// 1) 全量启动上报：失败每 10 分钟重试一次，最多再试 6 次；成功才进入下一阶段；
        /// 2) 之后每 2 小时发一次轻量 ping（仅 uuid，服务端只刷 last_at）。</summary>
        public static void StartOnce()
        {
            // 插件入口可能被多次点击；独立 Main 与插件 ShowMainForm 两条路径都只启动一个循环
            if (System.Threading.Interlocked.Exchange(ref _started, 1) != 0) return;

            Task.Run(async () =>
            {
                try
                {
                    string uuid = ComputeDeviceUuid();
                    DEF.UUID = uuid;

                    var settings = OoorSettings.Load();
                    string baseUrl = settings.ServerUrl.TrimEnd('/');

                    // 硬件概况只采集一次，重试期间复用
                    var payload = new Dictionary<string, object>
                    {
                        ["uuid"] = uuid,
                        ["cpu"] = GetCpuName(),
                        ["cores"] = Environment.ProcessorCount,
                        ["mem_mb"] = GetTotalMemoryMb(),
                        ["hostname"] = SafeMachineName(),
                        ["ver"] = DEF.ver,
                    };

                    // ---- 阶段一：启动全量上报（首次 + 最多 6 次重试，间隔 10 分钟）----
                    bool startupOk = false;
                    for (int attempt = 0; attempt <= StartupMaxRetries; attempt++)
                    {
                        try
                        {
                            if (await PostAsync(baseUrl + "/api/v1/heartbeat", payload))
                            {
                                startupOk = true;
                                break;
                            }
                        }
                        catch
                        {
                            // 网络/超时异常：落到下面的重试等待
                        }
                        if (attempt < StartupMaxRetries)
                            await Task.Delay(StartupRetryInterval);
                    }
                    if (!startupOk) return;   // 7 次都失败：放弃本轮会话，下次启动再来

                    // ---- 阶段二：在线轻量心跳，每 2 小时只刷 last_at ----
                    var pingBody = new Dictionary<string, object> { ["uuid"] = uuid };
                    while (true)
                    {
                        await Task.Delay(PingInterval);
                        try { await PostAsync(baseUrl + "/api/v1/heartbeat/ping", pingBody); }
                        catch
                        {
                            // 单次 ping 失败（如临时断网）不退出循环，等下一个 2 小时周期
                        }
                    }
                }
                catch
                {
                    // 指纹采集等整体异常一律忽略，绝不影响主程序
                }
            });
        }

        /// <summary>POST JSON；HTTP 2xx 视为成功，其它状态码（4xx/5xx）返回 false。</summary>
        private static async Task<bool> PostAsync(string url, IDictionary<string, object> payload)
        {
            string json = new JavaScriptSerializer().Serialize(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage resp = await Http.PostAsync(url, content))
            {
                return resp.IsSuccessStatusCode;
            }
        }

        // ==================== 设备指纹 ====================

        /// <summary>
        /// 设备唯一指纹：物理硬盘序列号（WMI）→ 系统盘卷序列号（P/Invoke）→ 机器名，
        /// 规范化后 SHA1（40 位 hex）。优先物理盘序列号：重装系统/重新分区不变，换硬盘才变。
        /// </summary>
        private static string ComputeDeviceUuid()
        {
            string raw = TryGetPhysicalDiskSerial();
            if (raw == null) raw = TryGetSystemVolumeSerial();
            if (raw == null) raw = "HOST:" + SafeMachineName();

            // 规范化：去空白/下划线/连字符并大写，消除不同驱动格式差异（如 "AB CD-12_" / "ABCD12"）
            var sb = new StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                if (c != ' ' && c != '_' && c != '-') sb.Append(char.ToUpperInvariant(c));
            }

            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) hex.Append(b.ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// WMI 查物理硬盘序列号（Win32_DiskDrive）。
        /// 查询级超时 3 秒（防 WMI 服务异常时长时间卡住后台线程）；
        /// 优先返回固定硬盘，否则取第一个有效序列号；虚机占位（空/全 0/NONE）视为无效。
        /// </summary>
        private static string TryGetPhysicalDiskSerial()
        {
            try
            {
                var scope = new ManagementScope(@"\root\cimv2")
                {
                    Options = { Timeout = TimeSpan.FromSeconds(WmiTimeoutSec) }
                };
                var options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(WmiTimeoutSec) };
                string firstValid = null;

                using (var searcher = new ManagementObjectSearcher(
                           scope,
                           new SelectQuery("SELECT SerialNumber, MediaType FROM Win32_DiskDrive"),
                           options))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject disk in results)
                    {
                        using (disk)
                        {
                            string sn = disk["SerialNumber"] as string;
                            if (!IsValidSerial(sn)) continue;
                            string s = sn.Trim();

                            string media = disk["MediaType"] as string;
                            if (media != null && media.IndexOf("Fixed", StringComparison.OrdinalIgnoreCase) >= 0)
                                return s;                       // 固定硬盘优先
                            if (firstValid == null) firstValid = s;
                        }
                    }
                }
                return firstValid;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>序列号有效性：去空白后 ≥6 位、非全 0、非占位词（虚机常返回空或 "0000_0000..."）。</summary>
        private static bool IsValidSerial(string sn)
        {
            if (string.IsNullOrWhiteSpace(sn)) return false;
            string compact = sn.Trim().Replace(" ", "").Replace("_", "").Replace("-", "");
            if (compact.Length < 6) return false;

            bool allZero = true;
            foreach (char c in compact)
            {
                if (c != '0') { allZero = false; break; }
            }
            if (allZero) return false;

            string up = compact.ToUpperInvariant();
            if (up == "NONE" || up == "UNKNOWN" || up == "DEFAULT" || up == "NULL") return false;
            return true;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformation(
            string rootPathName,
            StringBuilder volumeNameBuffer, int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength, out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer, int fileSystemNameSize);

        /// <summary>回退方案：系统盘卷序列号（格式化分区时生成，零依赖；重装系统会变，故仅作回退）。</summary>
        private static string TryGetSystemVolumeSerial()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                var vol = new StringBuilder(261);
                var fs = new StringBuilder(261);
                if (GetVolumeInformation(root, vol, vol.Capacity,
                        out uint serial, out _, out _, fs, fs.Capacity) && serial != 0)
                {
                    return "VOL:" + serial.ToString("X8");
                }
            }
            catch
            {
                // 忽略，落到机器名回退
            }
            return null;
        }

        // ==================== 硬件概况 ====================

        /// <summary>CPU 型号：注册表 ProcessorNameString（毫秒级，免 WMI），如 "13th Gen Intel(R) Core(TM) i7-13700"。</summary>
        private static string GetCpuName()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(
                           @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null && key.GetValue("ProcessorNameString") is string name
                        && !string.IsNullOrWhiteSpace(name))
                    {
                        return name.Trim();
                    }
                }
            }
            catch { /* 注册表不可用时忽略 */ }
            return null;
        }

        /// <summary>物理内存总量（MB，向下取整）。</summary>
        private static int? GetTotalMemoryMb()
        {
            try
            {
                var ci = new Microsoft.VisualBasic.Devices.ComputerInfo();
                ulong bytes = ci.TotalPhysicalMemory;
                if (bytes > 0) return (int)(bytes / 1024UL / 1024UL);
            }
            catch { /* 忽略 */ }
            return null;
        }

        private static string SafeMachineName()
        {
            try { return Environment.MachineName; }
            catch { return null; }
        }
    }
}
