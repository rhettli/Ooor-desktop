using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Script.Serialization;

namespace ooor.Core
{
    /// <summary>任务状态：Queued→Downloading→(Extracting)→Completed；Paused/Failed 可重新入队</summary>
    public enum DownloadState
    {
        Queued,       // 已入队，等待下载
        Downloading,  // 下载中
        Extracting,   // 下载完成，解压中
        Completed,    // 完成（含「文件已存在、跳过下载」）
        Paused,       // 已暂停（半成品保留，可继续）
        Failed        // 失败（网络 / IO / 解压错误）
    }

    /// <summary>
    /// 一个下载任务。
    /// 保存目录在入队前就确定（模型下载由用户在 DownloadTargetForm 对话框确认）；
    /// 需要解压的文件（llama.cpp 的 zip）带 ExtractDir，gguf 等直接落盘 ExtractDir=null。
    /// </summary>
    public class DownloadTask
    {
        public readonly string Id = Guid.NewGuid().ToString("N");

        public string Tag;           // 分组显示：模型仓库 id 或 release tag
        public string FileName;      // 落盘文件名（可含仓库内子目录，如 UD-Q4_K_XL/xxx.gguf）
        public string Url;
        public long ExpectedSize;    // 列表接口给的参考大小（0 = 未知）
        public string BearerToken;   // ooor 加速源鉴权（可为空）
        public string SaveDir;       // 保存目录（下载前确定；未开始下载前可在管理窗口修改）
        public string ExtractDir;    // 解压目录（null = 无需解压）
        public string ProxyUrl;      // 用户选中的 GitHub 加速镜像（github.com = 直连，不代理）
        public int Threads = 1;      // 分块下载线程数（入队时定格；1 = 单线程旧路径）

        public DownloadState State = DownloadState.Queued;
        public long DownloadedBytes;
        public long TotalBytes;
        public double SpeedBps;      // 本次统计的瞬时速度
        public string Message = "排队中…";
        public DateTime CreateTime = DateTime.Now;

        /// <summary>运行期诊断：分块回退单线程的原因（null = 正常；不持久化，仅供 UI tooltip 显示）</summary>
        public string ChunkFallbackReason;

        /// <summary>正在下载/解压（管理窗口此时禁改目录、禁重新下载）</summary>
        public bool IsBusy => State == DownloadState.Downloading || State == DownloadState.Extracting;

        public string SavePath
        {
            get { try { return Path.Combine(SaveDir ?? "", FileName ?? ""); } catch { return ""; } }
        }

        public string TempPath => SavePath + ".downloading";

        internal CancellationTokenSource Cts;
        internal DateTime LastNotifyUtc = DateTime.MinValue;

        /// <summary>
        /// 运行期上下文：多线程分块下载时由 RunMultiThreadInner 挂载，Stop() 据此 Dispose
        /// 各分块的 HttpResponseMessage 强制断连（比单纯 CTS.Cancel 更快让阻塞的 ReadAsync 退出）。
        /// 单线程下载时为 null，改用 ActiveSingleResponse。
        /// </summary>
        internal object ActiveRunCtx;
        /// <summary>单线程下载时持有的响应对象（Stop 时 Dispose 以打断阻塞的流读取）</summary>
        internal System.Net.Http.HttpResponseMessage ActiveSingleResponse;

        /// <summary>从 .mtmeta 重建的分块快照缓存（暂停/排队且非运行期时供 UI 绘制；按落盘时间戳失效）</summary>
        internal ChunkSnapshot[] PersistedChunks;
        internal long PersistedChunksStampTicks;

        /// <summary>
        /// 多线程分块下载时各块的实时进度快照（运行期字段，非分块模式/未开始/已收尾 = null）。
        /// UI 端只读；内部在分块记账锁内同步刷新 Done，原子可见，无需额外锁。
        /// LiveChunks 被赋值后由 RunMultiThread 在 finally 统一置回 null，避免悬挂。
        /// </summary>
        public ChunkSnapshot[] LiveChunks;
    }

    /// <summary>
    /// 单个分块的只读快照（供 UI 绘制"分块火柴图"，避免把内部 MtChunk 类型暴露出去）。
    /// Done 在分块线程锁内同步更新；其它字段在分块开始时定格后不再变。
    /// </summary>
    public sealed class ChunkSnapshot
    {
        public int Index;        // 块序号（0..N-1）
        public long Start;       // 块起始字节偏移（含）
        public long End;          // 块结束字节偏移（含）
        public long Done;        // 块内已下载字节（每 80KB 写盘 + 锁内更新）
        public bool IsDone() { return Done >= End - Start + 1; }
    }

    /// <summary>
    /// 下载管理器（单例）：任务队列 + 断点续传下载 + zip 解压，后台多线程执行，
    /// 默认最多同时下载 MaxConcurrent（3）个任务，其余排队（FIFO）。
    /// 每个任务内部还支持分块多线程下载（task.Threads，默认走 LastThreadCount=5）：
    /// 服务器支持 Range 且文件 ≥ 8MB 时按块并发（峰值连接数 = MaxConcurrent × Threads），
    /// 分块进度持久化到 .downloading.mtmeta，暂停/失败/重启后按块续传；不满足条件自动回退单线程。
    /// 窗口（FileDownloadForm）只通过 TaskAdded / TaskChanged / TaskRemoved 钩子观察任务与进度，
    /// 关闭窗口不影响下载。下载逻辑自原 LlamaFileDownloadForm 迁移：
    ///   - .downloading 半成品 + .meta(ETag) 续传，If-Range 防源文件更新后拼坏文件
    ///   - 服务器 416（半成品与源文件对不上）→ 清半成品从零重试一轮
    ///   - zip 解压为逐文件覆盖（不整目录清空）：CPU 主程序与 CUDA 运行库可合并解压到同一目录
    /// 任务记录持久化到 {配置根}/download_tasks.json：
    ///   - 新任务入队 / 状态变化 / 移除记录时落盘，程序重启后从配置恢复列表
    ///   - 恢复时：进行中的任务（排队/下载/解压）重新排队自动续传（半成品文件仍在），
    ///     已完成 / 已暂停 / 失败的任务按原状态恢复为记录，不自动开始
    /// </summary>
    public sealed class DownloadManager
    {
        public static readonly DownloadManager Instance = new DownloadManager();

        private const string TempSuffix = ".downloading";
        private const string MetaSuffix = ".meta";
        private const string MtMetaSuffix = ".mtmeta";       // 多线程分块状态（区别于单流的 .meta）
        private const string PersistFileName = "download_tasks.json";

        /// <summary>小于该值的文件不分块（多线程收益低，直接走单线程路径）</summary>
        private const long MultiThreadMinSize = 8L * 1024 * 1024;

        /// <summary>单个分块失败的重试次数（退避 2s/4s/6s，超限取消整个任务）</summary>
        private const int ChunkMaxRetries = 3;

        /// <summary>
        /// 多线程下载中 .mtmeta 落盘的最大时间间隔（毫秒）。
        /// 实际触发条件：本块累计 MetaPending ≥ MetaSaveBytes，或全块级距上次落盘 ≥ 此值，两者满足其一即写。
        /// </summary>
        private const int MetaSaveIntervalMs = 10 * 1000;

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromHours(2) };

        static DownloadManager()
        {
            // 分块并发需要大量同主机连接；默认每主机 2 个连接会把多线程卡成串行
            try { ServicePointManager.DefaultConnectionLimit = 256; } catch { }
        }

        private readonly object _sync = new object();
        private readonly List<DownloadTask> _tasks = new List<DownloadTask>();
        private readonly Queue<DownloadTask> _pending = new Queue<DownloadTask>();

        /// <summary>当前存活的工作线程数（进入/退出 WorkerLoop 时在 _sync 内增减）</summary>
        private int _workerCount;

        /// <summary>最大同时下载数（队列中任务多于该数时其余继续排队）</summary>
        public int MaxConcurrent { get; set; } = 3;

        /// <summary>
        /// 用户最近在「下载管理」窗口选中的 GitHub 加速镜像（默认直连 github.com）。
        /// 入队时由调用方（LlamaDownloadForm / ModelDownloadForm）传入并落盘到 task.ProxyUrl，
        /// RunTask 内部据此把 GitHub 链接套上代理再发起请求。
        /// </summary>
        public static string LastProxyUrl { get; set; } = GithubProxy.DirectMarker;

        /// <summary>
        /// 用户最近在下载对话框选择的分块线程数（默认 5）。
        /// Enqueue 不显式传 threads 时用它；大文件（≥8MB）且服务器支持 Range 才真正分块，
        /// 否则自动回退单线程。
        /// </summary>
        public static int LastThreadCount { get; set; } = 5;

        /// <summary>新任务入队（在调用方线程触发）</summary>
        public event Action<DownloadTask> TaskAdded;

        /// <summary>任务状态/进度变化（下载中约 200ms 一次，后台线程触发，订阅方自行封送到 UI）</summary>
        public event Action<DownloadTask> TaskChanged;

        /// <summary>任务记录被移除（UI 据此删掉对应列表行）</summary>
        public event Action<DownloadTask> TaskRemoved;

        /// <summary>任务完成钩子（转为 Completed 时触发一次，含「文件已存在跳过」与解压完成；后台线程触发，订阅方自行封送到 UI）</summary>
        public event Action<DownloadTask> TaskCompleted;

        private DownloadManager()
        {
            ProxyBindings.Load();   // 创建 {ConfigRoot}/proxies/ 目录（首次启动）
            LoadPersisted();
        }

        /// <summary>
        /// 把任务原始 URL 套上绑定代理，得出"实际请求的完整 URL"：
        ///   - GitHub 任务 + 绑定代理非直连 → 套上代理 base；
        ///   - 其它情况保持原始 URL（HF / ooor / 直连 等）。
        /// 供 RunTask 下载发起 + 「查看下载地址」窗口复用，确保两边完全一致。
        /// </summary>
        public static string ComposeRequestUrl(DownloadTask task)
        {
            if (task == null) return "";
            string url = task.Url ?? "";
            if (GithubProxy.IsGithub(url)
                && !string.IsNullOrEmpty(task.ProxyUrl)
                && task.ProxyUrl != GithubProxy.DirectMarker)
            {
                url = GithubProxy.Apply(url, task.ProxyUrl);
            }
            return url;
        }

        /// <summary>当前全部任务的快照</summary>
        public DownloadTask[] Snapshot()
        {
            lock (_sync) return _tasks.ToArray();
        }

        /// <summary>
        /// 提交下载任务：入队即自动开始（FIFO 串行下载）。
        /// 同一 URL 存到同一目录且尚未完成的任务已存在时，直接返回旧任务（重复点击不重复入队）。
        /// proxyUrl 留空时的回落顺序：
        ///   1) <see cref="ProxyBindings.Get"/> —— 该 URL 上次用户绑定过的代理（持久化跨启动复用）
        ///   2) <see cref="LastProxyUrl"/> —— 全局默认（仅在原始 URL 为 GitHub 时套上代理）
        /// </summary>
        public DownloadTask Enqueue(string tag, string fileName, string url, long expectedSize,
            string saveDir, string extractDir, string bearerToken, string proxyUrl = null, int threads = 0)
        {
            var task = new DownloadTask
            {
                Tag = tag ?? "",
                FileName = fileName ?? "",
                Url = url ?? "",
                ExpectedSize = expectedSize,
                SaveDir = saveDir ?? "",
                ExtractDir = string.IsNullOrEmpty(extractDir) ? null : extractDir,
                BearerToken = bearerToken,
                ProxyUrl = string.IsNullOrWhiteSpace(proxyUrl)
                    ? (ProxyBindings.Get(url) ?? LastProxyUrl)
                    : proxyUrl,
                Threads = threads > 0 ? threads : Math.Max(1, LastThreadCount)
            };

            lock (_sync)
            {
                foreach (var t in _tasks)
                {
                    if (t.Url == task.Url
                        && string.Equals(t.SaveDir, task.SaveDir, StringComparison.OrdinalIgnoreCase)
                        && t.State != DownloadState.Completed)
                        return t;   // 未完成的重复任务：不重复入队
                }
                _tasks.Add(task);
                _pending.Enqueue(task);
                EnsureWorkers();
            }
            Save();
            Fire(TaskAdded, task);
            return task;
        }

        /// <summary>
        /// 移除一条任务记录：下载中/解压中的任务不允许移除；
        /// 未完成的任务同时清掉半成品与续传校验信息（已完成的只移除记录，不动落盘文件）。
        /// 成功返回 true 并触发 TaskRemoved。
        /// </summary>
        public bool Remove(DownloadTask task)
        {
            if (task == null) return false;

            lock (_sync)
            {
                if (task.IsBusy) return false;
                if (!_tasks.Remove(task)) return false;

                // 从等待队列中剔除（re-enqueue 法保持其余任务顺序不变）
                int n = _pending.Count;
                for (int i = 0; i < n; i++)
                {
                    var t = _pending.Dequeue();
                    if (!ReferenceEquals(t, task)) _pending.Enqueue(t);
                }
            }

            if (task.State != DownloadState.Completed) CleanPartialCore(task);

            Save();
            Fire(TaskRemoved, task);
            return true;
        }

        /// <summary>
        /// 修改单个任务的代理绑定（用户右键切换代理菜单时调用）：
        ///   - 更新 task.ProxyUrl（内存立即生效，RunTask 入口拍死的代理因此跟着变）
        ///   - 落盘 URL → 代理 映射（ProxyBindings.Set），下次启动新建同 URL 任务时自动复用
        ///   - **Save()** 持久化到 download_tasks.json，否则下次启动 LoadPersisted 会丢失本次绑定
        /// 调用方负责"是否需要先停止在途任务"等业务判断（避免在途任务跑一半被改 URL）。
        /// </summary>
        public void SetTaskProxy(DownloadTask task, string proxyUrl)
        {
            if (task == null || string.IsNullOrEmpty(proxyUrl)) return;
            task.ProxyUrl = proxyUrl;
            ProxyBindings.Set(task.Url, proxyUrl);
            Save();   // ★ 关键：把 task.ProxyUrl 写回 download_tasks.json，否则重启后 LoadPersisted 会被空字符串兜底成 LastProxyUrl
        }

        /// <summary>继续/重试：已暂停或失败的任务重新入队（存在半成品时自动续传）</summary>
        public void Resume(DownloadTask task)
        {
            if (task == null) return;
            lock (_sync)
            {
                if (task.State != DownloadState.Paused && task.State != DownloadState.Failed) return;
                task.State = DownloadState.Queued;
                task.Message = "排队中…";
                task.SpeedBps = 0;
                _pending.Enqueue(task);
                EnsureWorkers();
            }
            Fire(TaskChanged, task);
        }

        /// <summary>
        /// 停止正在下载的任务：取消下载流读取（RunTask 捕获取消后置为已暂停），
        /// 半成品与续传校验信息保留，之后可「继续下载」断点续传。
        /// 为让阻塞在 stream.ReadAsync 上的分块线程立即退出（CancellationToken 在网络空闲时
        /// 不会打断读），同时 Dispose 所有活跃的 HttpResponseMessage 强制断开底层连接。
        /// </summary>
        public void Stop(DownloadTask task)
        {
            if (task == null) return;
            lock (_sync)
            {
                if (task.State != DownloadState.Downloading) return;
                try { task.Cts?.Cancel(); }
                catch (ObjectDisposedException) { /* 收尾竞态：令牌已释放，忽略 */ }
            }

            // 强制断连：Dispose 响应对象释放底层 TCP 连接，阻塞的 ReadAsync 立刻抛异常退出。
            // 不在 _sync 锁内做 Dispose（可能慢），但 Dispose 本身对已释放对象是安全的。
            try
            {
                var ctx = task.ActiveRunCtx as MtContext;
                if (ctx != null)
                {
                    HttpResponseMessage[] copies;
                    lock (ctx.RespSync) copies = new List<HttpResponseMessage>(ctx.ActiveResponses).ToArray();
                    foreach (var r in copies) try { r.Dispose(); } catch { }
                }
                try { task.ActiveSingleResponse?.Dispose(); } catch { }
            }
            catch { }
        }

        /// <summary>
        /// 重新下载：清掉半成品与正式文件，从零开始。
        /// ★ deleteTarget 必须删正式文件：否则已完成任务会被 RunTask 开头的
        /// "文件已存在（大小一致）跳过下载"直接转回 Completed，表现为"点了没反应"。
        /// </summary>
        public void Redownload(DownloadTask task, bool deleteTarget)
        {
            if (task == null) return;
            lock (_sync)
            {
                if (task.IsBusy || task.State == DownloadState.Queued) return;
                CleanPartialCore(task);
                if (deleteTarget) TryDelete(task.SavePath);
                task.DownloadedBytes = 0;
                task.TotalBytes = 0;
                task.SpeedBps = 0;
                task.State = DownloadState.Queued;
                task.Message = "排队中…";
                _pending.Enqueue(task);
                EnsureWorkers();
            }
            Fire(TaskChanged, task);
        }

        /// <summary>是否存在半成品（.downloading）</summary>
        public bool HasPartial(DownloadTask task)
        {
            try { return task != null && File.Exists(task.TempPath); }
            catch { return false; }
        }

        /// <summary>删除半成品与续传校验信息（下载中不允许）</summary>
        public void CleanPartial(DownloadTask task)
        {
            if (task == null) return;
            bool had;
            lock (_sync) had = CleanPartialCore(task);
            if (had) Fire(TaskChanged, task);
        }

        // ==================== 队列与工作线程（并发模型） ====================
        //
        // 「按需拉起、空闲即退」的多工作线程：入队时若队列有积压且线程数不足 MaxConcurrent，
        // 就再拉起一个工作线程；线程取不到任务时自行退出（计数 -1）。
        // 不用常驻线程 + Monitor.Wait：入队点一定会调 EnsureWorkers，不存在「没人唤醒」的死角；
        // RunTask 外再兜一层 catch，保证任何异常都不会让工作线程悄悄死掉、拖住整个队列。

        /// <summary>按队列积压拉起工作线程（调用方需持有 _sync）</summary>
        private void EnsureWorkers()
        {
            // 只要有空位 + 还有任务没取，就拉起 worker。
            // 旧版用 `_workerCount < _pending.Count` 当条件，会把"在途任务的 worker"
            // 计入 _workerCount，导致队列里已堆积新任务时也不会拉新 worker（只能 1 个并发）。
            // 新版：每个 worker 拉起后会立即在 lock 内 Dequeue；取不到（_pending 为空）就自己退出，
            //       因此这里放宽到 "_pending.Count > 0" 即可，不会拉起冗余 worker。
            while (_workerCount < MaxConcurrent && _pending.Count > 0)
            {
                _workerCount++;
                var t = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "DownloadWorker" + _workerCount
                };
                t.Start();
            }
        }

        private void WorkerLoop()
        {
            while (true)
            {
                DownloadTask task;
                lock (_sync)
                {
                    if (_pending.Count == 0)
                    {
                        _workerCount--;   // 没任务了：线程退出，下次入队时再按需拉起
                        return;
                    }
                    task = _pending.Dequeue();
                }

                if (task.State != DownloadState.Queued) continue;

                try { RunTask(task); }
                catch (Exception ex)
                {
                    // RunTask 内部已兜底各类异常，这里再防一手：线程绝不能带着异常死掉
                    try { SetState(task, DownloadState.Failed, "下载失败：" + ex.Message); }
                    catch { }
                }
            }
        }

        // ==================== 下载主流程（迁移自 LlamaFileDownloadForm） ====================

        private void RunTask(DownloadTask task)
        {
            string savePath = task.SavePath;
            string tempPath = savePath + TempSuffix;
            task.Cts = new CancellationTokenSource();
            SetState(task, DownloadState.Downloading, "开始下载…");

            // GitHub 链接 + 用户选了非直连代理 → 套上镜像 URL（其它源不受影响）
            string requestUrl = ComposeRequestUrl(task);

            try
            {
                try
                {
                    // 保存目录（含仓库内子目录）不存在则创建
                    string sub = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(sub)) Directory.CreateDirectory(sub);
                }
                catch (Exception ex)
                {
                    SetState(task, DownloadState.Failed, "无法创建保存目录：" + ex.Message);
                    return;
                }

                // 目标文件已存在且大小一致 → 视为已下载，直接收尾（含解压）
                try
                {
                    if (File.Exists(savePath) && task.ExpectedSize > 0
                        && new FileInfo(savePath).Length == task.ExpectedSize)
                    {
                        task.DownloadedBytes = task.TotalBytes = task.ExpectedSize;
                        AfterDownload(task, savePath, "文件已存在（大小一致），跳过下载。");
                        return;
                    }
                }
                catch { }

                // 多线程分块下载：threads>1 且（无半成品 或 半成品是分块格式 .mtmeta）时启用；
                // 旧式单流半成品（有 .downloading 无 .mtmeta）保持原单线程路径续传，不迁移格式；
                // 探测发现服务器不支持 Range / 拿不到总长 / 文件太小 → 自动回退单线程。
                if (task.Threads > 1)
                {
                    bool hasMtMeta = File.Exists(tempPath + MtMetaSuffix);
                    if (hasMtMeta || !File.Exists(tempPath))
                    {
                        if (RunMultiThread(task, requestUrl, savePath, tempPath)) return;
                        // 回退单线程：清掉可能残留的分块半成品（预分配文件不能当单流 append 用）
                        TryDelete(tempPath);
                        TryDelete(tempPath + MtMetaSuffix);
                        TryDelete(tempPath + MtMetaSuffix + ".tmp");
                    }
                    else
                    {
                        // 有单流半成品但无 .mtmeta（旧格式/上次单线程留下的）→ 按设计走单线程续传
                        task.ChunkFallbackReason = "存在旧版单流半成品，按单线程续传；清除半成品后重新下载可启用多分块";
                    }
                }

                // 续传起点：已有半成品接着下（If-Range 用上次的 ETag 校验源文件没变过）
                // ★ 预分配残留识别：分块模式用 SetLength(total) 预分配 .downloading（长度直接等于
                // 总大小但中间是洞），若程序在首次 .mtmeta 落盘前被异常终止，重启后此文件无 .mtmeta、
                // 无 .meta，长度却 == 期望大小——绝不能走下面的"大小相等直接改名收尾"把它转正，
                // 也不能当单流半成品续传。判据安全性：单流路径下载完成时必有 .meta（SaveMeta 在
                // fs.Flush 之后写），单流中途被杀则长度 < 期望值走正常续传，都不会命中本分支。
                if (task.Threads > 1 && File.Exists(tempPath)
                    && !File.Exists(tempPath + MtMetaSuffix) && !File.Exists(tempPath + MetaSuffix)
                    && task.ExpectedSize > 0 && PartialLength(tempPath) == task.ExpectedSize)
                {
                    TryDelete(tempPath);   // 有洞的预分配残留：从零开始，别把空壳转成"已完成"
                }

                long downloaded = 0;
                string ifRange = null;
                long part = PartialLength(tempPath);
                if (part > 0)
                {
                    downloaded = part;
                    ifRange = ReadMeta(tempPath);
                }
                else if (part == 0)
                {
                    TryDelete(tempPath);   // 0 字节半成品没有价值
                }

                // 半成品已等于期望大小：不必走网络，改名收尾
                if (downloaded > 0 && task.ExpectedSize > 0 && downloaded == task.ExpectedSize)
                {
                    if (Finish(tempPath, savePath))
                    {
                        task.DownloadedBytes = task.TotalBytes = downloaded;
                        AfterDownload(task, savePath, null);
                    }
                    else
                        SetState(task, DownloadState.Failed, "无法把半成品改名为正式文件，请检查目标目录权限。");
                    return;
                }

                bool finished = false;
                // 最多两轮：首轮续传；服务器 416（半成品与源文件对不上）→ 清零重来一轮
                for (int attempt = 0; attempt < 2 && !finished; attempt++)
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, requestUrl))
                    {
                        req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "1.0"));
                        if (!string.IsNullOrEmpty(task.BearerToken))
                            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", task.BearerToken);

                        if (downloaded > 0)
                        {
                            req.Headers.Range = new RangeHeaderValue(downloaded, null);
                            if (!string.IsNullOrEmpty(ifRange))
                                req.Headers.TryAddWithoutValidation("If-Range", ifRange);
                        }

                        // 工作线程同步阻塞（无 UI 上下文，不会死锁）。
                        // 不用 using：resp 挂到 task.ActiveSingleResponse，Stop() 可 Dispose 强制断连，
                        // 让阻塞的 ReadAsync 立即退出（token 取消在网络空闲时不打断读）。
                        var resp = _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, task.Cts.Token)
                            .GetAwaiter().GetResult();
                        task.ActiveSingleResponse = resp;
                        try
                        {
                            bool partial = (int)resp.StatusCode == 206;

                            if (resp.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && downloaded > 0)
                            {
                                // 416：半成品比源文件还长（源已更新或半成品损坏）→ 删掉下一轮从零开始
                                TryDelete(tempPath);
                                TryDelete(tempPath + MetaSuffix);
                                downloaded = 0;
                                ifRange = null;
                                continue;
                            }

                            if (!resp.IsSuccessStatusCode)
                            {
                                SetState(task, DownloadState.Failed,
                                    "服务器返回错误：" + (int)resp.StatusCode + " " + resp.ReasonPhrase);
                                return;
                            }

                            if (!partial && downloaded > 0)
                                downloaded = 0;   // 服务器忽略 Range：从头写，避免新旧数据拼接

                            long total = TotalLength(resp, downloaded, partial, task.ExpectedSize);
                            string etag = resp.Headers.ETag?.ToString();

                            using (var fs = new FileStream(tempPath,
                                downloaded > 0 ? FileMode.Append : FileMode.Create,
                                FileAccess.Write, FileShare.None, 81920))
                            {
                                long startOffset = fs.Length;
                                downloaded = startOffset;
                                using (var stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                                {
                                    byte[] buffer = new byte[81920];
                                    var sw = Stopwatch.StartNew();
                                    int read;
                                    while ((read = stream.ReadAsync(buffer, 0, buffer.Length, task.Cts.Token)
                                            .GetAwaiter().GetResult()) > 0)
                                    {
                                        fs.Write(buffer, 0, read);
                                        downloaded += read;
                                        task.DownloadedBytes = downloaded;
                                        task.TotalBytes = total;
                                        task.SpeedBps = (downloaded - startOffset)
                                            / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                                        NotifyThrottled(task);
                                    }
                                    fs.Flush();
                                }
                            }

                            SaveMeta(tempPath, etag);

                            // 完整性校验：长度对不上保留半成品，下次接着下
                            long expect = total > 0 ? total : task.ExpectedSize;
                            if (expect > 0 && downloaded != expect)
                            {
                                SetState(task, DownloadState.Paused,
                                    "下载不完整（" + FormatSize(downloaded) + " / " + FormatSize(expect)
                                    + "），已保留半成品，可继续下载。");
                                return;
                            }

                            if (!Finish(tempPath, savePath))
                            {
                                SetState(task, DownloadState.Failed, "无法把下载文件改名为正式文件，请检查目标目录权限。");
                                return;
                            }

                            task.DownloadedBytes = task.TotalBytes = downloaded;
                            task.SpeedBps = 0;
                            finished = true;
                        }
                        finally
                        {
                            task.ActiveSingleResponse = null;
                            resp.Dispose();
                        }
                    }
                }

                if (!finished)
                {
                    TryDelete(tempPath);
                    TryDelete(tempPath + MetaSuffix);
                    SetState(task, DownloadState.Failed, "服务器上的文件与半成品不一致，已清除半成品，请重新下载。");
                    return;
                }

                AfterDownload(task, savePath, null);
            }
            catch (OperationCanceledException)
            {
                long p = PartialLength(tempPath);
                SetState(task, DownloadState.Paused,
                    p > 0 ? "已暂停（已保留 " + FormatSize(p) + " 半成品）。" : "已取消。");
            }
            catch (Exception ex)
            {
                // Stop() 强制 Dispose 响应断连时，阻塞的 ReadAsync 抛 ObjectDisposedException / IOException，
                // 此时 token 已取消 → 按暂停处理（保留半成品，可继续），而不是误报失败
                bool cancelled;
                try { cancelled = task.Cts != null && task.Cts.IsCancellationRequested; }
                catch (ObjectDisposedException) { cancelled = false; }
                if (cancelled)
                {
                    long p = PartialLength(tempPath);
                    SetState(task, DownloadState.Paused,
                        p > 0 ? "已暂停（已保留 " + FormatSize(p) + " 半成品）。" : "已取消。");
                }
                else if (ex is HttpRequestException) SetState(task, DownloadState.Failed, "网络错误：" + ex.Message);
                else if (ex is IOException) SetState(task, DownloadState.Failed, "IO 错误：" + ex.Message);
                else SetState(task, DownloadState.Failed, "下载失败：" + ex.Message);
            }
            finally
            {
                task.Cts?.Dispose();
                task.Cts = null;
            }
        }

        // ==================== 多线程分块下载 ====================
        //
        // 每个任务内部再开 Threads 个专用线程（new Thread，不占线程池），把文件按区间均分：
        //   - 预分配整个 .downloading 文件，各线程持独立 FileStream（FileShare.Write）按块偏移定位写，
        //     块区间互不重叠，多句柄并发写同一文件在 Windows 上安全；
        //   - 分块状态持久化到 .downloading.mtmeta（JSON：ETag + 总长 + 每块进度），
        //     暂停/失败/程序重启后按块续传（Range: start+done - end）；
        //   - 所有块共享 task.Cts：Stop() 取消全部块；单块重试超限则取消整个任务（保留分块进度）；
        //   - 探测（Range: bytes=0-0）不支持 Range / 拿不到总长 / 文件 < 8MB → 回退单线程路径。

        /// <summary>一个分块的下载状态（JSON 持久化字段：Start/End 含义为 [Start, End] 闭区间，Done 为块内已下字节）</summary>
        private sealed class MtChunk
        {
            public long Start;
            public long End;
            public long Done;

            /// <summary>
            /// 自上次 .mtmeta 落盘以来本块累计写入字节（运行期计数，不进 JSON）。
            /// 命中 1MB 或全块级 10 秒定时器时序列化一次并清零——平衡崩溃恢复粒度与写盘频率。
            /// 不加 [ScriptIgnore]：JavaScriptSerializer 默认就会序列化 public 字段；
            /// 旧 .mtmeta 没这字段时反序列化为 0，首次触发立即落盘，行为安全。
            /// </summary>
            [ScriptIgnore]
            public long MetaPending;

            public bool IsDone() { return Done >= End - Start + 1; }
        }

        /// <summary>
        /// 多线程分块下载中累积多少字节才写一次 .mtmeta（崩溃/异常退出时最多丢这点增量）。
        /// 配合 MetaSaveIntervalMs 触发，两者满足其一即写。
        /// </summary>
        private const long MetaSaveBytes = 1L * 1024 * 1024;

        /// <summary>多线程分块下载的整体状态（JSON 持久化到 .mtmeta）</summary>
        private sealed class MtMeta
        {
            public string Etag;
            public long Total;
            public List<MtChunk> Chunks;
        }

        /// <summary>一次分块下载的运行期上下文（线程间共享）</summary>
        private sealed class MtContext
        {
            public DownloadTask Task;
            public string RequestUrl;
            public string TempPath;
            public MtMeta Meta;
            public object MetaSync = new object();   // 序列化 .mtmeta 与 chunk.Done 记账互斥（避免撕裂）
            public List<string> Errors = new List<string>();
            public long SessionBytes;                // 本次会话新增字节（Interlocked）
            public long BaseDone;                    // 会话开始时全部块已完成字节
            public long Total;
            public Stopwatch Sw;
            public DateTime LastMetaSaveUtc;         // .mtmeta 落盘节流（MetaSync 内读写）
            /// <summary>
            /// 各分块当前持有的 HttpResponseMessage 集合（用于 Stop 时强制 Dispose 断连）。
            /// stream.ReadAsync 在网络空闲时不会因 CancellationToken 取消而立即返回，
            /// 必须 Dispose 响应对象释放底层连接，阻塞的读才会立刻抛异常退出。
            /// </summary>
            public readonly System.Collections.Generic.HashSet<HttpResponseMessage> ActiveResponses
                = new System.Collections.Generic.HashSet<HttpResponseMessage>();
            public readonly object RespSync = new object();
            /// <summary>
            /// 与 Meta.Chunks 一一对应的 UI 快照数组；任务开始时建好挂到 task.LiveChunks，
            /// 每条块在 MetaSync 锁内同步刷新 Snapshots[i].Done，供"分块火柴图"实时绘制。
            /// </summary>
            public ChunkSnapshot[] Snapshots;
        }

        /// <summary>
        /// 获取任务当前的分块实时快照（仅多线程分块模式下有数据）。
        /// 返回 null 表示：单线程 / 未开始 / 已收尾 / 探测失败回退单线程。
        /// 数组内容只在分块线程锁内修改，UI 直接读取无撕裂风险。
        /// </summary>
        public ChunkSnapshot[] GetChunkSnapshots(DownloadTask task)
        {
            if (task == null) return null;
            if (task.LiveChunks != null) return task.LiveChunks;
            // 暂停/排队/失败：LiveChunks 已被收尾清空，从 .mtmeta 重建快照（暂停收尾时有最终落盘，进度准确；重启恢复同样适用）
            // 下载中但 LiveChunks 尚未建好（续传探测期，RunMultiThread 入口会先清空）：同样回退 .mtmeta 旧快照，
            // 避免火柴图闪"单线程"；探测成功后 LiveChunks 挂上无缝切换，探测失败回退单线程时 mtmeta 已删 → null。
            if (task.State == DownloadState.Paused || task.State == DownloadState.Queued
                || task.State == DownloadState.Failed || task.State == DownloadState.Downloading)
                return GetPersistedSnapshots(task);
            return null;
        }

        /// <summary>
        /// 从 .downloading.mtmeta 重建分块快照（带文件时间戳缓存，UI 200ms 轮询不会反复读盘）。
        /// 无 .mtmeta（从未开始 / 单线程 / 已收尾清理）返回 null。
        /// </summary>
        private ChunkSnapshot[] GetPersistedSnapshots(DownloadTask task)
        {
            try
            {
                string p = task.TempPath + MtMetaSuffix;
                if (!File.Exists(p)) { task.PersistedChunks = null; return null; }
                long stamp = File.GetLastWriteTimeUtc(p).Ticks;
                if (task.PersistedChunks != null && task.PersistedChunksStampTicks == stamp)
                    return task.PersistedChunks;

                var meta = ReadMtMeta(task.TempPath);
                if (meta == null) { task.PersistedChunks = null; return null; }

                var snaps = new ChunkSnapshot[meta.Chunks.Count];
                for (int i = 0; i < meta.Chunks.Count; i++)
                {
                    var c = meta.Chunks[i];
                    snaps[i] = new ChunkSnapshot { Index = i, Start = c.Start, End = c.End, Done = c.Done };
                }
                task.PersistedChunks = snaps;
                task.PersistedChunksStampTicks = stamp;
                return snaps;
            }
            catch { return null; }
        }

        /// <summary>
        /// 多线程分块下载主流程。返回 true = 任务已收尾（成功/暂停/失败状态已置好）；
        /// 返回 false = 探测后不适合分块（服务器忽略 Range / 拿不到总长 / 文件太小），调用方回退单线程。
        /// 落盘节流：每块每写入 1MB 或每 10 秒全块汇总一次 .mtmeta（崩溃/断电恢复时不超过 1MB/10s 损失）。
        /// </summary>
        private bool RunMultiThread(DownloadTask task, string requestUrl, string savePath, string tempPath)
        {
            // 进入分块前先清掉上一次可能残留的 LiveChunks（重试/异常路径下防御性）
            task.LiveChunks = null;

            try
            {
                return RunMultiThreadInner(task, requestUrl, savePath, tempPath);
            }
            finally
            {
                // 任何退出路径（成功 / 暂停 / 失败 / 回退单线程）都清掉 LiveChunks 与 ActiveRunCtx
                task.LiveChunks = null;
                task.ActiveRunCtx = null;
            }
        }

        private bool RunMultiThreadInner(DownloadTask task, string requestUrl, string savePath, string tempPath)
        {
            MtMeta meta = ReadMtMeta(tempPath);
            bool resume = meta != null;
            long total = 0;
            string etag;

            // ---- 1) 探测：Range: bytes=0-0 拿总长与 ETag；续传时带 If-Range 校验源文件没变 ----
            using (var req = new HttpRequestMessage(HttpMethod.Get, requestUrl))
            {
                req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "1.0"));
                if (!string.IsNullOrEmpty(task.BearerToken))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", task.BearerToken);
                req.Headers.Range = new RangeHeaderValue(0, 0);
                if (resume && !string.IsNullOrEmpty(meta.Etag))
                    req.Headers.TryAddWithoutValidation("If-Range", meta.Etag);

                using (var resp = _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, task.Cts.Token)
                    .GetAwaiter().GetResult())
                {
                    etag = resp.Headers.ETag?.ToString();

                    if (!resp.IsSuccessStatusCode)
                    {
                        SetState(task, DownloadState.Failed,
                            "服务器返回错误：" + (int)resp.StatusCode + " " + resp.ReasonPhrase);
                        return true;
                    }

                    // 200 = 服务器忽略 Range（不支持分块）或续传时 If-Range 不匹配（源已更新）。
                    // 两种情况统一处理：清掉分块半成品，回退单线程从零开始。
                    if ((int)resp.StatusCode != 206)
                    {
                        task.ChunkFallbackReason = "服务器不支持 Range 分块（探测返回 " + (int)resp.StatusCode + "），已回退单线程";
                        if (resume)
                        {
                            TryDelete(tempPath);
                            TryDelete(tempPath + MtMetaSuffix);
                        }
                        return false;
                    }

                    var range = resp.Content.Headers.ContentRange;
                    if (range == null || !range.Length.HasValue || range.Length.Value <= 0)
                    {
                        task.ChunkFallbackReason = "服务器未返回分块所需的总长度（Content-Range），已回退单线程";
                        if (resume)
                        {
                            TryDelete(tempPath);
                            TryDelete(tempPath + MtMetaSuffix);
                        }
                        return false;   // 206 却拿不到总长：无法分块
                    }
                    total = range.Length.Value;

                    // 小文件分块无收益 → 单线程
                    if (total < MultiThreadMinSize)
                    {
                        task.ChunkFallbackReason = "文件小于 8MB，无需分块";
                        if (resume)
                        {
                            TryDelete(tempPath);
                            TryDelete(tempPath + MtMetaSuffix);
                        }
                        return false;
                    }
                }
            }

            // ---- 2) 分块方案：续传沿用旧分块（源没变已由 If-Range/总长双重确认）；新下载按线程数均分 ----
            if (!resume || meta.Total != total
                || !File.Exists(tempPath) || PartialLength(tempPath) != total)
            {
                TryDelete(tempPath);   // 源已变 / 半成品尺寸对不上 → 从零开始
                resume = false;

                // 块数 = min(线程数, total/4MB)，保证每块至少 ~4MB（8MB 文件不会被切成 30 块）
                int n = (int)Math.Min(task.Threads, Math.Max(1, total / (4L * 1024 * 1024)));
                meta = new MtMeta { Etag = etag, Total = total, Chunks = new List<MtChunk>(n) };
                long size = total / n;
                long start = 0;
                for (int i = 0; i < n; i++)
                {
                    long end = (i == n - 1) ? total - 1 : start + size - 1;
                    meta.Chunks.Add(new MtChunk { Start = start, End = end, Done = 0 });
                    start = end + 1;
                }

                // 预分配目标尺寸（各线程按偏移独立写同一文件）
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920))
                    fs.SetLength(total);
            }
            else
            {
                meta.Etag = etag ?? meta.Etag;   // 服务器没给新 ETag 时沿用旧值
            }

            // ---- 3) 拉起分块线程（专用线程 + IsBackground，不占线程池；数量 = 未完成块数）----
            long baseDone = 0;
            foreach (var c in meta.Chunks) baseDone += c.Done;

            var ctx = new MtContext
            {
                Task = task,
                RequestUrl = requestUrl,
                TempPath = tempPath,
                Meta = meta,
                BaseDone = baseDone,
                Total = total,
                Sw = Stopwatch.StartNew(),
                LastMetaSaveUtc = DateTime.UtcNow
            };
            task.DownloadedBytes = baseDone;
            task.TotalBytes = total;

            // 建立与 meta.Chunks 一一对应的 UI 快照数组并挂到 task.LiveChunks。
            // ChunkWorker 在 MetaSync 锁内同步刷新 Snapshots[chunkIdx].Done，UI 读 LiveChunks
            // 即可拿到每块实时进度用于绘制"分块火柴图"。
            ctx.Snapshots = new ChunkSnapshot[meta.Chunks.Count];
            for (int i = 0; i < ctx.Snapshots.Length; i++)
            {
                var c = meta.Chunks[i];
                ctx.Snapshots[i] = new ChunkSnapshot { Index = i, Start = c.Start, End = c.End, Done = c.Done };
            }
            task.LiveChunks = ctx.Snapshots;
            task.ActiveRunCtx = ctx;   // 供 Stop() 强制 Dispose 各分块响应、立即打断阻塞读

            SetState(task, DownloadState.Downloading,
                "开始下载（" + meta.Chunks.Count + " 线程分块）…");

            var threads = new List<Thread>(meta.Chunks.Count);
            for (int chunkIdx = 0; chunkIdx < meta.Chunks.Count; chunkIdx++)
            {
                var chunk = meta.Chunks[chunkIdx];
                if (chunk.IsDone()) continue;   // 上次已完成的块直接跳过
                var closure = chunk;
                int idxCapture = chunkIdx;
                var t = new Thread(() => ChunkWorker(ctx, closure, idxCapture))
                {
                    IsBackground = true,
                    Name = "DlChunk#" + chunk.Start
                };
                threads.Add(t);
                t.Start();
            }

            foreach (var t in threads) t.Join();   // 工作线程本来就阻塞，直接等全部块收工

            lock (ctx.MetaSync) WriteMtMeta(tempPath, meta);   // 最终分块状态落盘

            // ---- 4) 收尾 ----
            bool cancelled;
            try { cancelled = task.Cts != null && task.Cts.IsCancellationRequested; }
            catch (ObjectDisposedException) { cancelled = false; }

            int errCount;
            lock (ctx.Errors) errCount = ctx.Errors.Count;

            if (errCount == 0 && (cancelled || threads.Count == 0 && !AllChunksDone(meta)))
            {
                // threads.Count==0 且未全完成：理论上不该发生，按暂停兜底
                SetState(task, DownloadState.Paused,
                    baseDone + Interlocked.Read(ref ctx.SessionBytes) > 0
                        ? "已暂停（已保留分块进度，可继续下载）。" : "已取消。");
                return true;
            }

            if (errCount > 0)
            {
                string first;
                lock (ctx.Errors) first = ctx.Errors[0];
                SetState(task, DownloadState.Failed,
                    "下载失败：" + first + "（已保留分块进度，可重试续传）。");
                return true;
            }

            // 完整性校验：所有块字节数加起来必须等于总长
            long sum = 0;
            foreach (var c in meta.Chunks) sum += c.Done;
            if (sum != total)
            {
                SetState(task, DownloadState.Paused,
                    "下载不完整（" + FormatSize(sum) + " / " + FormatSize(total) + "），已保留分块进度，可继续下载。");
                return true;
            }

            task.DownloadedBytes = task.TotalBytes = total;
            task.SpeedBps = 0;
            if (!Finish(tempPath, savePath))   // Finish 内部顺带删 .mtmeta
            {
                SetState(task, DownloadState.Failed, "无法把下载文件改名为正式文件，请检查目标目录权限。");
                return true;
            }
            AfterDownload(task, savePath, null);
            return true;
        }

        /// <summary>单个分块下载线程：Range 续传本块 + 失败退避重试；重试超限取消整个任务
        /// chunkIdx：块在 ctx.Snapshots / ctx.Meta.Chunks 中的索引，便于把 Done 同步到 UI 快照。</summary>
        private void ChunkWorker(MtContext ctx, MtChunk chunk, int chunkIdx)
        {
            var task = ctx.Task;
            var token = task.Cts.Token;
            int retries = 0;

            try
            {
                while (!chunk.IsDone() && !token.IsCancellationRequested)
                {
                    try
                    {
                        using (var req = new HttpRequestMessage(HttpMethod.Get, ctx.RequestUrl))
                        {
                            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "1.0"));
                            if (!string.IsNullOrEmpty(task.BearerToken))
                                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", task.BearerToken);
                            req.Headers.Range = new RangeHeaderValue(chunk.Start + chunk.Done, chunk.End);

                            // 不用 using：resp 注册到 ctx 后，Stop() 可从外部 Dispose 强制断连，
                            // 让阻塞在 ReadAsync 的分块线程立即退出（token 取消在网络空闲时不打断读）
                            var resp = _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, token)
                                .GetAwaiter().GetResult();
                            lock (ctx.RespSync) ctx.ActiveResponses.Add(resp);
                            try
                            {
                                if ((int)resp.StatusCode != 206)
                                    throw new HttpRequestException("分块响应异常："
                                        + (int)resp.StatusCode + " " + resp.ReasonPhrase);

                                // 校验服务器/代理返回的范围 == 请求的范围。不校验的话，
                                // 错位响应（代理/镜像常见）会被原样写进错误偏移——
                                // 大小账照样平，但文件内容出现"洞"，zip/压缩包解压时才炸。
                                long reqStart = chunk.Start + chunk.Done;
                                var cr = resp.Content.Headers.ContentRange;
                                if (cr == null || !cr.HasRange || cr.From != reqStart || cr.To > chunk.End)
                                    throw new HttpRequestException("分块响应范围不匹配：期望 "
                                        + reqStart + "-" + chunk.End + "，实际 "
                                        + (cr != null && cr.HasRange ? cr.From + "-" + cr.To : "无 Content-Range"));

                                using (var fs = new FileStream(ctx.TempPath, FileMode.Open, FileAccess.Write,
                                    FileShare.Write, 81920))
                                {
                                    fs.Seek(chunk.Start + chunk.Done, SeekOrigin.Begin);
                                    using (var stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                                    {
                                        byte[] buffer = new byte[81920];
                                        int read;
                                        while ((read = stream.ReadAsync(buffer, 0, buffer.Length, token)
                                                .GetAwaiter().GetResult()) > 0)
                                        {
                                            fs.Write(buffer, 0, read);
                                            Interlocked.Add(ref ctx.SessionBytes, read);

                                            // 块记账 + .mtmeta 落盘节流（同把锁，序列化时不会读到半截值）：
                                            // 本块累计 1MB 或全块级距上次落盘 10 秒即写一次。
                                            // 既保证崩溃/断电恢复粒度 ≤ 1MB/10s，又避免每 80KB 一次 JSON 写。
                                            // 同时把本块 Done 同步到 ctx.Snapshots[chunkIdx]，供 UI 自绘"分块火柴图"实时读取。
                                            lock (ctx.MetaSync)
                                            {
                                                chunk.Done += read;
                                                chunk.MetaPending += read;
                                                ctx.Snapshots[chunkIdx].Done = chunk.Done;   // 同步 UI 快照
                                                bool sizeUp = chunk.MetaPending >= MetaSaveBytes;
                                                bool timeUp = (DateTime.UtcNow - ctx.LastMetaSaveUtc).TotalMilliseconds >= MetaSaveIntervalMs;
                                                if (sizeUp || timeUp)
                                                {
                                                    // ★ 先 Flush 再落盘 .mtmeta：fs 是本块线程私有，
                                                    // FileStream 内部最多压着 ~80KB 缓冲，进程异常终止时这
                                                    // 些数据会丢。若不先刷盘，.mtmeta 记的 Done 会领先磁盘
                                                    // 实际写入，重启续传 Seek 跳过这段区间 → 文件中间留洞。
                                                    fs.Flush();
                                                    if (sizeUp) chunk.MetaPending = 0;
                                                    ctx.LastMetaSaveUtc = DateTime.UtcNow;
                                                    WriteMtMeta(ctx.TempPath, ctx.Meta);
                                                }
                                            }

                                            task.DownloadedBytes = ctx.BaseDone + Interlocked.Read(ref ctx.SessionBytes);
                                            task.TotalBytes = ctx.Total;
                                            task.SpeedBps = Interlocked.Read(ref ctx.SessionBytes)
                                                / Math.Max(0.001, ctx.Sw.Elapsed.TotalSeconds);
                                            NotifyThrottled(task);
                                        }
                                        fs.Flush();
                                    }
                                }
                            }
                            finally
                            {
                                lock (ctx.RespSync) ctx.ActiveResponses.Remove(resp);
                                resp.Dispose();
                            }
                        }

                        if (chunk.IsDone())
                        {
                            lock (ctx.MetaSync) WriteMtMeta(ctx.TempPath, ctx.Meta);   // 块完成立即落盘
                        }
                        else
                        {
                            // 流提前结束但块没下完（连接被掐断）→ 抛异常走重试，下一轮 Range 从当前 Done 续
                            throw new IOException("连接提前中断（块未下完）");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;   // 暂停 / 其它块失败取消：退出，不算失败
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested) return;
                        retries++;
                        if (retries > ChunkMaxRetries)
                        {
                            lock (ctx.Errors) ctx.Errors.Add("分块(" + FormatSize(chunk.Start) + " 起)："
                                + ex.Message);
                            try { task.Cts.Cancel(); } catch { }   // 取消其余块
                            return;
                        }
                        token.WaitHandle.WaitOne(retries * 2000);   // 退避 2s/4s/6s，暂停/取消可立即唤醒
                    }
                }
            }
            catch (OperationCanceledException) { /* 暂停：正常退出 */ }
            catch (Exception ex)
            {
                lock (ctx.Errors) ctx.Errors.Add(ex.Message);
                try { task.Cts.Cancel(); } catch { }
            }
        }

        /// <summary>所有块是否全部完成</summary>
        private static bool AllChunksDone(MtMeta meta)
        {
            foreach (var c in meta.Chunks) if (!c.IsDone()) return false;
            return true;
        }

        /// <summary>读取 .mtmeta 分块状态（文件缺失/损坏/字段非法 → null，视为无分块半成品）</summary>
        private static MtMeta ReadMtMeta(string tempPath)
        {
            try
            {
                string p = tempPath + MtMetaSuffix;
                if (!File.Exists(p)) return null;
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var meta = ser.Deserialize<MtMeta>(File.ReadAllText(p));
                if (meta == null || meta.Total <= 0 || meta.Chunks == null || meta.Chunks.Count == 0) return null;
                foreach (var c in meta.Chunks)
                {
                    if (c == null || c.End < c.Start || c.Done < 0 || c.Done > c.End - c.Start + 1) return null;
                }
                return meta;
            }
            catch { return null; }
        }

        /// <summary>原子重写 .mtmeta（先写 .tmp 再替换，写失败不影响下载）</summary>
        private static void WriteMtMeta(string tempPath, MtMeta meta)
        {
            try
            {
                string p = tempPath + MtMetaSuffix;
                string tmp = p + ".tmp";
                var ser = new JavaScriptSerializer();
                File.WriteAllText(tmp, ser.Serialize(meta), new System.Text.UTF8Encoding(false));
                if (File.Exists(p)) File.Delete(p);
                File.Move(tmp, p);
            }
            catch { /* 状态写盘失败不影响下载本身 */ }
        }

        /// <summary>
        /// 下载完成后的收尾：需要解压的 zip 解压到 ExtractDir；
        /// tar.gz 暂不支持自动解压（提示手动）；其余（gguf 等）直接完成。
        /// </summary>
        private void AfterDownload(DownloadTask task, string savePath, string skipMessage)
        {
            string lower = (savePath ?? "").ToLowerInvariant();
            bool needExtract = !string.IsNullOrEmpty(task.ExtractDir);

            if (needExtract && lower.EndsWith(".zip"))
            {
                SetState(task, DownloadState.Extracting, "下载完成，正在解压…");
                try
                {
                    ExtractZipOverwrite(savePath, task.ExtractDir);
                    SetState(task, DownloadState.Completed, "解压完成：" + task.ExtractDir);
                }
                catch (Exception ex)
                {
                    SetState(task, DownloadState.Failed, "解压失败：" + ex.Message);
                }
                return;
            }

            string msg = skipMessage ?? ("下载完成：" + FormatSize(task.TotalBytes));
            if (needExtract && (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz")))
                msg += "（.tar.gz 暂不支持自动解压，请手动解压到：" + task.ExtractDir + "）";
            SetState(task, DownloadState.Completed, msg);
        }

        /// <summary>
        /// 解压 zip 到目标目录，逐文件覆盖同名项（不整目录清空）：
        /// CUDA 运行库包与 CPU 主程序包可先后解压进同一目录合并内容。
        /// </summary>
        private static void ExtractZipOverwrite(string zipPath, string extractDir)
        {
            Directory.CreateDirectory(extractDir);
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    string dest = Path.Combine(extractDir, entry.FullName);
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(dest);   // 目录条目
                        continue;
                    }
                    string dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    try
                    {
                        entry.ExtractToFile(dest, true);
                    }
                    catch (Exception ex)
                    {
                        // 带上条目名，直接定位是压缩包里哪个文件的数据坏了
                        throw new IOException("条目「" + entry.FullName + "」解压失败：" + ex.Message, ex);
                    }
                }
            }
        }

        // ==================== 状态通知 ====================

        private void SetState(DownloadTask task, DownloadState state, string message)
        {
            task.State = state;
            task.Message = message;
            task.LastNotifyUtc = DateTime.UtcNow;
            Fire(TaskChanged, task);
            if (state == DownloadState.Completed) Fire(TaskCompleted, task);
            Save();   // 状态变化即落盘（进度节流通知不经过这里，写盘频率可控）
        }

        /// <summary>进度节流通知（每 200ms 一次，状态变化走 SetState 立即通知）</summary>
        private void NotifyThrottled(DownloadTask task)
        {
            var now = DateTime.UtcNow;
            if ((now - task.LastNotifyUtc).TotalMilliseconds < 200) return;
            task.LastNotifyUtc = now;
            task.Message = null;   // 下载中：让 UI 自己用字节/速度字段拼进度文案
            Fire(TaskChanged, task);
        }

        private static void Fire(Action<DownloadTask> handler, DownloadTask task)
        {
            if (handler == null) return;
            foreach (Action<DownloadTask> d in handler.GetInvocationList())
            {
                try { d(task); }
                catch { /* 单个订阅者异常不影响下载 */ }
            }
        }

        // ==================== 断点续传辅助（迁移自原下载窗） ====================

        private static long PartialLength(string tempPath)
        {
            try { return File.Exists(tempPath) ? new FileInfo(tempPath).Length : -1; }
            catch { return -1; }
        }

        private static bool CleanPartialCore(DownloadTask task)
        {
            string temp = task.TempPath;
            bool had = false;
            try { if (File.Exists(temp)) { File.Delete(temp); had = true; } } catch { }
            try { if (File.Exists(temp + MetaSuffix)) File.Delete(temp + MetaSuffix); } catch { }
            try { if (File.Exists(temp + MtMetaSuffix)) File.Delete(temp + MtMetaSuffix); } catch { }
            try { if (File.Exists(temp + MtMetaSuffix + ".tmp")) File.Delete(temp + MtMetaSuffix + ".tmp"); } catch { }
            return had;
        }

        private static void SaveMeta(string tempPath, string etag)
        {
            try
            {
                string meta = tempPath + MetaSuffix;
                if (string.IsNullOrEmpty(etag))
                {
                    if (File.Exists(meta)) File.Delete(meta);
                    return;
                }
                File.WriteAllText(meta, etag);
            }
            catch { /* 校验信息写失败不影响下载本身 */ }
        }

        private static string ReadMeta(string tempPath)
        {
            try
            {
                string meta = tempPath + MetaSuffix;
                return File.Exists(meta) ? File.ReadAllText(meta).Trim() : null;
            }
            catch { return null; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        /// <summary>本次下载总大小：206 取 Content-Range 总长，200 取 Content-Length，都拿不到退回期望值</summary>
        private static long TotalLength(HttpResponseMessage resp, long startOffset, bool partial, long expectedSize)
        {
            if (partial)
            {
                var range = resp.Content.Headers.ContentRange;
                if (range != null && range.Length.HasValue) return range.Length.Value;
            }
            long len = resp.Content.Headers.ContentLength ?? 0;
            if (len > 0) return len + startOffset;
            return expectedSize;
        }

        /// <summary>半成品改名为正式文件（跨卷时退化为复制 + 删除）</summary>
        private static bool Finish(string tempPath, string savePath)
        {
            try
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                try { File.Move(tempPath, savePath); }
                catch
                {
                    File.Copy(tempPath, savePath, true);
                    File.Delete(tempPath);
                }
                try { if (File.Exists(tempPath + MetaSuffix)) File.Delete(tempPath + MetaSuffix); } catch { }
                try { if (File.Exists(tempPath + MtMetaSuffix)) File.Delete(tempPath + MtMetaSuffix); } catch { }
                return true;
            }
            catch { return false; }
        }

        /// <summary>仅供窗口层复用的大小格式化</summary>
        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
            if (bytes >= GB) return (bytes / GB).ToString("0.00") + " GB";
            if (bytes >= MB) return (bytes / MB).ToString("0.0") + " MB";
            if (bytes >= KB) return (bytes / KB).ToString("0") + " KB";
            return bytes + " B";
        }

        // ==================== 任务记录持久化（download_tasks.json） ====================

        /// <summary>记录文件路径：{配置根}/download_tasks.json</summary>
        private static string PersistPath
        {
            get
            {
                try { return Path.Combine(LlamaRuntime.ConfigRoot, PersistFileName); }
                catch { return PersistFileName; }
            }
        }

        /// <summary>把当前全部任务记录写入配置文件（新入队 / 状态变化 / 移除时调用；失败不影响下载）</summary>
        private void Save()
        {
            try
            {
                List<TaskRecord> records;
                lock (_sync)
                {
                    records = new List<TaskRecord>(_tasks.Count);
                    foreach (DownloadTask t in _tasks) records.Add(TaskRecord.From(t));
                }

                var ser = new JavaScriptSerializer();
                Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
                File.WriteAllText(PersistPath, ser.Serialize(records), new System.Text.UTF8Encoding(false));
            }
            catch
            {
                // 记录写盘失败（目录只读/被占用等）不影响下载本身
            }
        }

        /// <summary>
        /// 程序启动时从配置文件恢复任务列表：
        ///   - 上次进行中的（排队/下载/解压）→ 重新排队自动续传（半成品文件 + ETag 校验仍在）；
        ///   - 已完成 / 已暂停 / 失败 → 按原状态恢复为记录，不自动开始（暂停/失败可手动继续）。
        /// </summary>
        private void LoadPersisted()
        {
            try
            {
                if (!File.Exists(PersistPath)) return;

                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                List<TaskRecord> records;
                try { records = ser.Deserialize<List<TaskRecord>>(File.ReadAllText(PersistPath)); }
                catch { return; }   // 配置损坏：丢弃记录，不拖垮程序

                if (records == null) return;

                lock (_sync)
                {
                    foreach (TaskRecord r in records)
                    {
                        if (r == null || string.IsNullOrEmpty(r.Url) || string.IsNullOrEmpty(r.FileName)) continue;

                        var task = r.ToTask();
                        switch (task.State)
                        {
                            case DownloadState.Queued:
                            case DownloadState.Downloading:
                            case DownloadState.Extracting:
                                // 上次会话中断的任务：重新排队，有半成品自动续传
                                task.State = DownloadState.Queued;
                                task.Message = "程序重启，继续排队下载…";
                                _tasks.Add(task);
                                _pending.Enqueue(task);
                                break;

                            case DownloadState.Completed:
                            case DownloadState.Paused:
                            case DownloadState.Failed:
                                _tasks.Add(task);
                                break;
                        }
                    }

                    if (_pending.Count > 0) EnsureWorkers();
                }
            }
            catch
            {
                // 恢复失败：当作没有历史记录
            }
        }

        /// <summary>一条任务记录（JSON 序列化 DTO，只挑需要持久化的字段）</summary>
        private sealed class TaskRecord
        {
            public string Tag;
            public string FileName;
            public string Url;
            public string SaveDir;
            public string ExtractDir;
            public string BearerToken;
            public string ProxyUrl;
            public string Message;
            public long ExpectedSize;
            public long DownloadedBytes;
            public long TotalBytes;
            public int State;
            public int Threads;
            public long CreateTimeTicks;

            public static TaskRecord From(DownloadTask t)
            {
                return new TaskRecord
                {
                    Tag = t.Tag ?? "",
                    FileName = t.FileName ?? "",
                    Url = t.Url ?? "",
                    SaveDir = t.SaveDir ?? "",
                    ExtractDir = t.ExtractDir ?? "",
                    BearerToken = t.BearerToken ?? "",
                    ProxyUrl = t.ProxyUrl ?? "",
                    Message = t.Message ?? "",
                    ExpectedSize = t.ExpectedSize,
                    DownloadedBytes = t.DownloadedBytes,
                    TotalBytes = t.TotalBytes,
                    State = (int)t.State,
                    Threads = t.Threads,
                    CreateTimeTicks = t.CreateTime.Ticks
                };
            }

            public DownloadTask ToTask()
            {
                return new DownloadTask
                {
                    Tag = Tag ?? "",
                    FileName = FileName,
                    Url = Url,
                    SaveDir = SaveDir,
                    ExtractDir = string.IsNullOrEmpty(ExtractDir) ? null : ExtractDir,
                    BearerToken = string.IsNullOrEmpty(BearerToken) ? null : BearerToken,
                    // 历史记录无 ProxyUrl 字段时按当前静态默认值兜底，确保 RunTask 不会用错代理
                    ProxyUrl = string.IsNullOrEmpty(ProxyUrl) ? DownloadManager.LastProxyUrl : ProxyUrl,
                    ExpectedSize = ExpectedSize,
                    DownloadedBytes = DownloadedBytes,
                    TotalBytes = TotalBytes,
                    State = (DownloadState)State,
                    // 历史记录无 Threads 字段时按当前全局默认兜底
                    Threads = Threads > 0 ? Threads : DownloadManager.LastThreadCount,
                    Message = Message,
                    CreateTime = CreateTimeTicks > 0 ? new DateTime(CreateTimeTicks, DateTimeKind.Local) : DateTime.Now
                };
            }
        }
    }
}
