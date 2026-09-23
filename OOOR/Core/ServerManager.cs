using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ooor.Core
{
    /// <summary>
    /// llama-server 全局单例管理器（单例模式）：
    /// 服务进程生命周期独立于主窗口——关闭窗口不停止服务，
    /// 下次打开窗口时从 run_state.conf 恢复运行状态（检测进程是否仍存活）。
    ///
    /// run_state.conf（key=value 文本）记录：模型路径、启动参数、PID、启动时间。
    /// 进程内（窗口单例复用/重开）用 LastRunState 直接恢复；跨 proxy 重启用状态文件 + Attach(pid) 接管。
    /// </summary>
    internal static class ServerManager
    {
        /// <summary>全局唯一的 llama-server 封装（不随窗口 Dispose）</summary>
        public static readonly LlamaServer Server = new LlamaServer();

        public const string RunStateFileName = "run_state.conf";

        /// <summary>运行状态文件（{ConfigRoot}\run_state.conf）</summary>
        public static string RunStatePath => Path.Combine(LlamaRuntime.ConfigRoot, RunStateFileName);

        /// <summary>一次成功启动的运行状态</summary>
        public sealed class RunState
        {
            public string ModelPath = "";
            public long Ctx;
            public int NPred;
            public int Ngl;
            public string Host = "";
            public int Port;
            public int Pid;
            public DateTime Started;
        }

        /// <summary>本次进程内最近一次启动的状态（窗口单例复用/重开时优先使用，避免读文件）</summary>
        public static RunState LastRunState { get; private set; }

        /// <summary>启动成功后调用：记录内存状态并写入状态文件</summary>
        public static void OnStarted(ModelInfo model, long ctx, int npred, int ngl, string host, int port)
        {
            var st = new RunState
            {
                ModelPath = model != null ? model.FullPath : "",
                Ctx = ctx,
                NPred = npred,
                Ngl = ngl,
                Host = host ?? "",
                Port = port,
                Pid = Server.Pid,
                Started = DateTime.Now
            };
            LastRunState = st;
            WriteStateFile(st);
        }

        /// <summary>服务停止/退出后调用：清除内存状态与状态文件</summary>
        public static void OnStopped()
        {
            LastRunState = null;
            try { if (File.Exists(RunStatePath)) File.Delete(RunStatePath); }
            catch { /* 状态文件删除失败不影响运行 */ }
        }

        /// <summary>读取状态文件；没有或解析失败返回 null</summary>
        public static RunState LoadRunState()
        {
            string path = RunStatePath;
            if (!File.Exists(path)) return null;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return null; }

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int sep = line.IndexOf('=');
                if (sep <= 0) continue;
                map[line.Substring(0, sep).Trim().ToLowerInvariant()] = line.Substring(sep + 1).Trim();
            }

            string model;
            if (!map.TryGetValue("model", out model) || model.Length == 0) return null;

            return new RunState
            {
                ModelPath = model,
                Ctx = Get(map, "ctx", 0),
                NPred = (int)Get(map, "npred", 0),
                Ngl = (int)Get(map, "ngl", 0),
                Host = Get(map, "host", ""),
                Port = (int)Get(map, "port", 0),
                Pid = (int)Get(map, "pid", 0),
                Started = GetDate(map, "started")
            };
        }

        private static void WriteStateFile(RunState st)
        {
            try
            {
                Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
                File.WriteAllLines(RunStatePath, new[]
                {
                    "# llama-server 运行状态（窗口重开后用于恢复 UI）",
                    "model=" + st.ModelPath,
                    "ctx=" + st.Ctx,
                    "npred=" + st.NPred,
                    "ngl=" + st.Ngl,
                    "host=" + st.Host,
                    "port=" + st.Port,
                    "pid=" + st.Pid,
                    "started=" + st.Started.ToString("yyyy-MM-dd HH:mm:ss")
                }, new System.Text.UTF8Encoding(false));
            }
            catch
            {
                // 状态写不进去只影响下次恢复，不影响本次启动
            }
        }

        private static long Get(Dictionary<string, string> map, string key, long fallback)
        {
            string s;
            long v;
            return map.TryGetValue(key, out s) && long.TryParse(s, out v) ? v : fallback;
        }

        private static string Get(Dictionary<string, string> map, string key, string fallback)
        {
            string s;
            return map.TryGetValue(key, out s) ? s : fallback;
        }

        private static DateTime GetDate(Dictionary<string, string> map, string key)
        {
            string s;
            DateTime d;
            return map.TryGetValue(key, out s) &&
                   DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss",
                       CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                ? d
                : DateTime.MinValue;
        }
    }
}
