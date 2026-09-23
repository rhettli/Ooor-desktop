using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// llama-server 进程封装：启动参数按 Start.bat 同款口径拼装，
    /// stdout/stderr 实时回调（llama-server 的日志主要走 stderr）。
    ///
    /// 停止可靠性（三层）：
    /// 1. 子进程加入 Windows 作业对象（KILL_ON_JOB_CLOSE）：proxy.exe/插件一旦异常退出，
    ///    llama-server 会被系统强制结束，杜绝“父进程没了、服务还在”的孤儿；
    /// 2. Stop 先 taskkill /T /F 结束进程树，再 Process.Kill 兜底，并同步等待真实退出；
    /// 3. 按部署目录兜底清理同源 llama-server（处理历史遗留孤儿：proxy 重启后本对象已无其句柄）。
    /// </summary>
    internal sealed class LlamaServer : IDisposable
    {
        private Process _process;
        private readonly object _gate = new object();
        private bool _stopping;
        private readonly JobObject _job = new JobObject();

        /// <summary>一行服务端输出（stdout 或 stderr），回调发生在线程池线程，UI 需自行 Invoke</summary>
        public event Action<string> LineReceived;

        /// <summary>进程退出，参数为退出码；回调发生在线程池线程</summary>
        public event Action<int> ProcessExited;

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    var p = _process;
                    return p != null && SafeAlive(p);
                }
            }
        }

        public int Port { get; private set; }

        /// <summary>当前追踪的 llama-server 进程 PID（未启动为 0）</summary>
        public int Pid
        {
            get
            {
                lock (_gate)
                {
                    var p = _process;
                    if (p == null) return 0;
                    try { return SafeAlive(p) ? p.Id : 0; }
                    catch { return 0; }
                }
            }
        }

        /// <summary>
        /// 重新接管一个正在运行的 llama-server 进程（窗口重开后恢复状态用）。
        /// 校验 PID 对应进程确实是本部署目录下的 llama-server，防止 PID 复用误绑。
        /// 注意：接管后无法再读取该进程的 stdout/stderr（管道属于原父进程），仅恢复运行/退出监控。
        /// </summary>
        public bool Attach(int pid, int port)
        {
            lock (_gate)
            {
                if (_process != null && SafeAlive(_process)) return true;

                try
                {
                    var p = Process.GetProcessById(pid);

                    bool valid = string.Equals(p.ProcessName, "llama-server", StringComparison.OrdinalIgnoreCase);
                    if (valid)
                    {
                        try
                        {
                            string exe = p.MainModule == null ? null : p.MainModule.FileName;
                            if (exe != null)
                            {
                                string dir = Path.GetDirectoryName(Path.GetFullPath(exe));
                                valid = string.Equals(TrimSep(dir), TrimSep(LlamaRuntime.BaseDir),
                                    StringComparison.OrdinalIgnoreCase);
                            }
                        }
                        catch { /* 读不到模块信息时不做目录校验，仅按进程名判定 */ }
                    }
                    if (!valid)
                    {
                        p.Dispose();
                        return false;
                    }

                    _stopping = false;
                    p.EnableRaisingEvents = true;
                    p.Exited += OnExited;
                    _process = p;
                    Port = port;
                    return true;
                }
                catch
                {
                    return false;   // PID 不存在（服务已退出）
                }
            }
        }

        public void Start(ModelInfo model, string mmprojPath,
            int gpuLayers, long contextSize, int predictTokens, string host, int port,
            string extraArgs = null)
        {
            if (model == null) throw new InvalidOperationException("未选择模型");
            if (!LlamaRuntime.ServerPresent)
                throw new InvalidOperationException("未找到 " + LlamaRuntime.ServerExeName + "：" + LlamaRuntime.ServerExePath);

            // 启动前先看端口/残留：上次未正常停止的服务会占着端口，直接启动必然绑定失败
            int orphanCount = CountManagedServers();
            lock (_gate)
            {
                bool trackedAlive = _process != null && SafeAlive(_process);
                if (trackedAlive || orphanCount > 0)
                    throw new InvalidOperationException(
                        "已检测到 llama-server 在运行（" +
                        (trackedAlive ? "当前实例" : "可能是上次残留的服务") +
                        "），请先点「停止服务」再启动。");
            }

            Process p;
            lock (_gate)
            {
                var args = new StringBuilder();
                args.Append("-m ").Append(Quote(model.FullPath));
                if (!string.IsNullOrEmpty(mmprojPath))
                    args.Append(" --mmproj ").Append(Quote(mmprojPath));
                args.Append(" -ngl ").Append(gpuLayers)
                    .Append(" -c ").Append(contextSize)
                    .Append(" -n ").Append(predictTokens)
                    .Append(" --host ").Append(host)
                    .Append(" --port ").Append(port);

                // 「更多参数」窗口配置的可选参数（server_params.conf），原样追加在末尾
                string extra = (extraArgs ?? "").Trim();
                if (extra.Length > 0) args.Append(' ').Append(extra);

                var psi = new ProcessStartInfo
                {
                    FileName = LlamaRuntime.ServerExePath,
                    Arguments = args.ToString(),
                    WorkingDirectory = LlamaRuntime.BaseDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                // llama-server 输出 UTF-8；旧系统默认 OEM 代码页会导致中文/方框乱码
                try
                {
                    psi.StandardOutputEncoding = Encoding.UTF8;
                    psi.StandardErrorEncoding = Encoding.UTF8;
                }
                catch { /* 极旧框架不支持时退回默认编码 */ }

                _stopping = false;
                p = new Process { EnableRaisingEvents = true };
                p.StartInfo = psi;
                p.OutputDataReceived += OnOutputData;
                p.ErrorDataReceived += OnOutputData;
                p.Exited += OnExited;

                if (!p.Start()) throw new InvalidOperationException("llama-server 启动失败");

                // 加入作业对象：插件/proxy 进程结束时由系统保证服务一起结束
                _job.Assign(p);

                _process = p;
                Port = port;

                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            Emit("启动 llama-server： " + LlamaRuntime.ServerExePath + " " + p.StartInfo.Arguments);
        }

        /// <summary>
        /// 停止服务：taskkill 进程树 → Kill 兜底 → 等待真实退出 → 清理同源残留。
        /// 返回实际结束的进程数（含残留）；调用方据此给用户确定的反馈。
        /// </summary>
        public int Stop()
        {
            Process p;
            int trackedPid;
            lock (_gate)
            {
                p = _process;
                trackedPid = SafeAlive(p) ? p.Id : 0;
                _stopping = true;
            }

            int killed = 0;

            // 1. 结束追踪中的进程树（llama-server 若派生 RPC 子进程也一并结束）
            if (trackedPid != 0)
            {
                if (TaskKillTree(trackedPid)) killed++;

                try
                {
                    if (SafeAlive(p))
                    {
                        p.Kill();
                        killed++;
                    }
                }
                catch { /* 已退出 */ }

                // 2. 同步等待真实退出（最多 5 秒），而不是“发完指令就当停了”
                try { p.WaitForExit(5000); } catch { }

                if (SafeAlive(p))
                    Emit("[警告] llama-server(PID=" + trackedPid + ") 在 5 秒内未退出，可能正在加载模型，请稍后在任务管理器确认。");
            }

            // 3. 清理本部署目录下的其他同源 llama-server（proxy 重启后失去句柄的历史孤儿）
            foreach (int orphanPid in EnumerateManagedServerPids())
            {
                if (orphanPid == trackedPid) continue;
                Emit("[清理] 结束残留 llama-server，PID=" + orphanPid);
                if (TaskKillTree(orphanPid)) killed++;
            }

            return killed;
        }

        /// <summary>taskkill /PID x /T /F；true 表示命令成功执行（不代表一定杀到，进程可能刚自己退出）</summary>
        private bool TaskKillTree(int pid)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + pid + " /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var kill = Process.Start(psi))
                {
                    if (kill == null) return false;
                    // 必须排空输出，否则管道写满会让 taskkill 卡住直到超时
                    kill.OutputDataReceived += (s, e) => { };
                    kill.ErrorDataReceived += (s, e) => { };
                    kill.BeginOutputReadLine();
                    kill.BeginErrorReadLine();
                    if (!kill.WaitForExit(3000))
                    {
                        try { kill.Kill(); } catch { }
                        return false;
                    }
                    return kill.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>统计当前部署目录下正在运行的 llama-server 进程数（含非本对象启动的残留）</summary>
        private int CountManagedServers()
        {
            int n = 0;
            foreach (var pid in EnumerateManagedServerPids()) n++;
            return n;
        }

        /// <summary>
        /// 枚举“属于本插件部署目录”的 llama-server.exe 进程 PID。
        /// 用镜像路径前缀判定归属，不误伤其他目录/其他用途的 llama-server。
        /// </summary>
        private IEnumerable<int> EnumerateManagedServerPids()
        {
            Process[] all;
            try { all = Process.GetProcessesByName("llama-server"); }
            catch { yield break; }

            string baseDir = "";
            try { baseDir = TrimSep(Path.GetFullPath(LlamaRuntime.BaseDir)); }
            catch { }

            foreach (var pr in all)
            {
                try
                {
                    string exe;
                    try { exe = pr.MainModule == null ? null : pr.MainModule.FileName; }
                    catch { exe = null; }   // 跨位数/权限不足时读不到 MainModule：跳过，不盲杀

                    bool sameDir = exe != null &&
                        string.Equals(Path.GetDirectoryName(Path.GetFullPath(exe))?.TrimEnd('\\'),
                                      baseDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

                    if (sameDir) yield return pr.Id;
                }
                finally
                {
                    pr.Dispose();
                }
            }
        }

        private void OnOutputData(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data)) Emit(e.Data);
        }

        private void OnExited(object sender, EventArgs e)
        {
            int code;
            try { code = _process?.ExitCode ?? -1; }
            catch { code = -1; }

            Emit(_stopping ? "llama-server 已手动停止" : ("llama-server 进程已退出，退出码 " + code));

            var handler = ProcessExited;
            try { handler?.Invoke(code); }
            catch { /* UI 回调异常不影响进程管理 */ }
        }

        private void Emit(string line)
        {
            var handler = LineReceived;
            try { handler?.Invoke(line); }
            catch { /* 订阅方异常不影响读循环 */ }
        }

        private static bool SafeAlive(Process p)
        {
            if (p == null) return false;
            try { return !p.HasExited; }
            catch { return false; }
        }

        private static string Quote(string path)
        {
            return "\"" + (path ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string TrimSep(string p)
        {
            return (p ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public void Dispose()
        {
            try { Stop(); } catch { }
            var p = _process;
            if (p != null)
            {
                try { p.Dispose(); } catch { }
                _process = null;
            }
            _job.Dispose();
        }

        // ==================== Windows 作业对象 ====================

        /// <summary>
        /// 作业对象封装：JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE。
        /// 句柄关闭（正常 Dispose、proxy 崩溃或被强杀）时，作业内所有进程由系统强制结束。
        /// </summary>
        private sealed class JobObject : IDisposable
        {
            private IntPtr _handle = IntPtr.Zero;

            public JobObject()
            {
                try
                {
                    _handle = CreateJobObject(IntPtr.Zero, null);
                    if (_handle == IntPtr.Zero) return;

                    var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    };
                    SetInformationJobObject(_handle, JobObjectBasicLimitInformation,
                        ref info, Marshal.SizeOf(typeof(JOBOBJECT_BASIC_LIMIT_INFORMATION)));
                }
                catch
                {
                    // 作业对象不可用（极旧系统/权限受限）：退化为 Stop 的 taskkill 方案
                    _handle = IntPtr.Zero;
                }
            }

            public void Assign(Process p)
            {
                if (_handle == IntPtr.Zero || p == null) return;
                try
                {
                    AssignProcessToJobObject(_handle, p.Handle);
                }
                catch
                {
                    // 进程已退出或已在不允许嵌套的作业中：忽略，停止时仍有 taskkill 兜底
                }
            }

            public void Dispose()
            {
                if (_handle != IntPtr.Zero)
                {
                    try { CloseHandle(_handle); } catch { }
                    _handle = IntPtr.Zero;
                }
            }

            private const int JobObjectBasicLimitInformation = 2;
            private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

            [StructLayout(LayoutKind.Sequential)]
            private struct IO_COUNTERS
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                public IntPtr PerProcessUserTimeLimit;
                public IntPtr PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
                public IO_COUNTERS IoInfo;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

            [DllImport("kernel32.dll")]
            private static extern bool SetInformationJobObject(IntPtr hJob, int infoType,
                ref JOBOBJECT_BASIC_LIMIT_INFORMATION info, int cbJobObjectInfoLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr hObject);
        }
    }
}
