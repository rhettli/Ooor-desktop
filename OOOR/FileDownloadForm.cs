using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ooor.Controls;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 下载任务管理窗口（迅雷式行卡片布局）：
    ///   - 顶部 toolbar：新建 / 开始 / 重下 / 清半成品 / 加速代理 / 关于
    ///   - 中部 Panel（AutoScroll，占满）：每行一个 RowControl（绝对定位 Y = i * RowHeight）
    ///       行按状态排序：下载中 → 解压中 → 排队 → 暂停 → 失败 → 完成（状态变化自动上浮/下沉）
    ///       列1 文件 [图标+文件名 / 保存路径 / 时间·分块·状态]
    ///       列2 进度 [原生ProgressBar / 数字行 / 分块火柴图]
    ///       列3 操作 [主按钮（态感知，原生 Button） / 删除]
    ///   - 右键菜单：开始/暂停 / 重下 / 清半成品 / 改保存目录 / 改解压目录 / 打开目录 / 复制 URL / 查看 URL / 删除
    ///   - 底部 statusStrip：全局统计（任务总数·活动中·总速度·已用流量）
    /// 数据流：DownloadManager.TaskAdded/Changed/Removed → 增/改/删 RowControl + 调其 RefreshProgress()
    /// 单例下载在后台执行，关窗不中断。
    /// </summary>
    public partial class FileDownloadForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>已打开的管理窗口实例（重复打开 = 激活同一个）</summary>
        private static FileDownloadForm _instance;

        /// <summary>任务对象 → 行索引（避免每次行查找都线性扫描）</summary>
        private readonly Dictionary<DownloadTask, int> _taskIndex = new Dictionary<DownloadTask, int>();

        /// <summary>当前选中的任务（null = 无选中）</summary>
        private int _selectedIndex = -1;
        private DownloadTask _contextTask;   // 右键菜单针对的任务

        private static FileDownloadForm d;

        public static void ShowSingle()
        {
            if (d == null)
            {
                d = new FileDownloadForm();
                d.Show();
            }
            else
            {
                d.Show();
                d.BringToFront();
            }
        }

        public FileDownloadForm()
        {
            InitializeComponent();
            InitListView();
            InitContextMenu();
            InitStatusTimer();

            // 加速镜像：LinkLabel 显示当前选中任务绑定的代理
            UpdateProxyLabel();
            linkLabelProxy.Enabled = false;

            // 钩子：单例管理器通知任务变化（后台线程触发，先封送到 UI 线程）
            DownloadManager.Instance.TaskAdded += OnTaskAdded;
            DownloadManager.Instance.TaskChanged += OnTaskChanged;
            DownloadManager.Instance.TaskRemoved += OnTaskRemoved;

            // 容器尺寸变化（含纵向滚动条出现/消失）→ 同步每个 RowControl 宽度
            flpRows.ClientSizeChanged += (s, e) => ReflowRowWidths();
            ReflowRowWidths();

            ReloadAll();
            RestoreSelectedIndex();
            RefreshDetail();
            RefreshStatusStrip();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、工具栏按钮、右键菜单、状态栏等）</summary>
        protected override void ApplyLanguage()
        {
            // 工具栏静态按钮（开始按钮文本随选中任务状态变化，由 RefreshDetail 统一刷新）
            toolStripButtonCreate.Text = L.T("fdf.btn.create");
            toolStripBtnRedownload.Text = L.T("fdf.btn.redownload");
            toolStripBtnAbout.Text = L.T("fdf.btn.about");

            // 行右键菜单（miPrimary 文本随任务状态在菜单打开时刷新，这里给默认值）
            if (miPrimary != null) miPrimary.Text = L.T("fdf.action.start");
            if (miRedownload != null) miRedownload.Text = L.T("fdf.menu.redownload");
            if (miCleanPart != null) miCleanPart.Text = L.T("fdf.menu.cleanPart");
            if (miChangeSaveDir != null) miChangeSaveDir.Text = L.T("fdf.menu.changeSaveDir");
            if (miChangeExtractDir != null) miChangeExtractDir.Text = L.T("fdf.menu.changeExtractDir");
            if (miOpenFolder != null) miOpenFolder.Text = L.T("fdf.menu.openFolder");
            if (miCopyUrl != null) miCopyUrl.Text = L.T("fdf.menu.copyUrl");
            if (miViewUrl != null) miViewUrl.Text = L.T("fdf.menu.viewUrl");
            if (miRemove != null) miRemove.Text = L.T("fdf.menu.remove");

            // 标题、开始按钮、加速标签等与选中任务相关的动态文本统一走刷新逻辑
            RefreshDetail();
            RefreshStatusStrip();
        }

        /// <summary>打开（或激活已打开的）下载任务管理窗口；下载在后台单例中继续，与本窗口无关</summary>
        public static void ShowManager(IWin32Window owner)
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new FileDownloadForm();
                _instance.Show(owner);
            }
            else
            {
                if (_instance.WindowState == FormWindowState.Minimized)
                    _instance.WindowState = FormWindowState.Normal;
                _instance.Activate();
            }
        }

        // ==================== 初始化 ====================

        private void InitListView()
        {
            // flpRows = NoAutoScrollPanel（普通 Panel + AutoScroll，重写 ScrollToControl
            // 返回 DisplayRectangle.Location，禁用"子控件获得焦点/布局变更时自动滚到该控件"）。
            //   行内刷新（ProgressBar.Value / Label.Text）不会再把正在下载的行拽回可视区，
            //   滚动条保留，用户手动滚动与 ScrollControlIntoView 不受影响。
            // 双缓冲已在 NoAutoScrollPanel 构造函数中开启。
        }

        // ==================== 选中切换（RowControl → Form） ====================

        /// <summary>由 RowControl.OnMouseDown 调用：切换选中到指定行</summary>
        public void SetSelectedFromRow(int rowIndex)
        {
            SetSelected(rowIndex);
        }

        private void SetSelected(int rowIndex)
        {
            if (rowIndex == _selectedIndex) return;
            if (_selectedIndex >= 0 && _selectedIndex < flpRows.Controls.Count)
                ((RowControl)flpRows.Controls[_selectedIndex]).Selected = false;
            if (rowIndex >= 0 && rowIndex < flpRows.Controls.Count)
                ((RowControl)flpRows.Controls[rowIndex]).Selected = true;
            _selectedIndex = rowIndex;
            RefreshDetail();
        }

        // ==================== 工具栏按钮状态 / 标题 ====================

        private void RefreshDetail()
        {
            var task = SelectedTask;
            if (task == null)
            {
                Text = L.T("fdf.title");
                toolStripStatusLabel1.Text = L.T("fdf.status.ready");
                linkLabelProxy.Enabled = false;
                linkLabelProxy.Text = string.Format(L.T("fdf.proxy.label"), DownloadManager.LastProxyUrl ?? GithubProxy.DirectMarker);
                toolStripBtnStart.Text = L.T("fdf.btn.start");
                toolStripBtnStart.Enabled = false;
                toolStripBtnRedownload.Enabled = false;
                //toolStripBtnCleanPart.Enabled = false;
                return;
            }

            Text = string.Format(L.T("fdf.title.withTask"), task.FileName);

            bool busy = task.IsBusy;
            bool queued = task.State == DownloadState.Queued;
            bool downloading = task.State == DownloadState.Downloading;

            bool gh = GithubProxy.IsGithub(task.Url);
            linkLabelProxy.Enabled = gh;
            string boundProxy = string.IsNullOrEmpty(task.ProxyUrl) ? DownloadManager.LastProxyUrl : task.ProxyUrl;
            linkLabelProxy.Text = string.Format(L.T("fdf.proxy.label"),
                string.IsNullOrEmpty(boundProxy) ? GithubProxy.DirectMarker : boundProxy);

            if (downloading)
            {
                toolStripBtnStart.Text = L.T("fdf.btn.stop");
                toolStripBtnStart.Enabled = true;
            }
            else if (busy)
            {
                toolStripBtnStart.Text = L.T("fdf.btn.stop");
                toolStripBtnStart.Enabled = false;
            }
            else
            {
                bool resumable = task.State == DownloadState.Paused || task.State == DownloadState.Failed;
                toolStripBtnStart.Text = resumable && DownloadManager.Instance.HasPartial(task)
                    ? L.T("fdf.btn.resume")
                    : L.T("fdf.btn.start");
                toolStripBtnStart.Enabled = resumable || task.State == DownloadState.Queued;
            }
            toolStripBtnRedownload.Enabled = !busy && !queued;
            //toolStripBtnCleanPart.Enabled = !busy && DownloadManager.Instance.HasPartial(task);
        }

        // ==================== 加速镜像 LinkLabel ====================

        private void UpdateProxyLabel() => RefreshDetail();

        private void linkLabelProxy_LinkClicked(object sender, EventArgs e)
        {
            contextMenuProxy.Show(this, new Point(linkLabelProxy.Bounds.Left, linkLabelProxy.Bounds.Top + linkLabelProxy.Bounds.Height));
        }

        private void contextMenuProxy_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            contextMenuProxy.Items.Clear();

            DownloadTask task = SelectedTask;
            string current = (task != null && !string.IsNullOrEmpty(task.ProxyUrl))
                ? task.ProxyUrl
                : (DownloadManager.LastProxyUrl ?? GithubProxy.DirectMarker);

            var miManage = new ToolStripMenuItem(L.T("fdf.proxy.manage"));
            miManage.Click += miManageProxy_Click;
            contextMenuProxy.Items.Add(miManage);

            var proxies = GithubProxy.Proxies;
            if (proxies != null && proxies.Count > 0)
            {
                contextMenuProxy.Items.Add(new ToolStripSeparator());
                foreach (string p in proxies)
                {
                    var mi = new ToolStripMenuItem(p)
                    {
                        Tag = p,
                        Checked = string.Equals(p, current, StringComparison.OrdinalIgnoreCase)
                    };
                    mi.Click += mi_Proxy_Click;
                    contextMenuProxy.Items.Add(mi);
                }
            }

            contextMenuProxy.Items.Add(new ToolStripSeparator());
            var miDirect = new ToolStripMenuItem(GithubProxy.DirectMarker)
            {
                Tag = GithubProxy.DirectMarker,
                Checked = string.Equals(GithubProxy.DirectMarker, current, StringComparison.OrdinalIgnoreCase)
            };
            miDirect.Click += mi_Proxy_Click;
            contextMenuProxy.Items.Add(miDirect);
        }

        private void mi_Proxy_Click(object sender, EventArgs e)
        {
            string proxy = (sender as ToolStripMenuItem)?.Tag as string;
            if (string.IsNullOrEmpty(proxy)) return;

            DownloadTask task = SelectedTask;

            if (task != null)
            {
                if (string.Equals(proxy, task.ProxyUrl, StringComparison.OrdinalIgnoreCase))
                    return;

                if (task.State == DownloadState.Downloading)
                {
                    var r = MessageBox.Show(this,
                        L.T("fdf.msg.proxyBusy"),
                        L.T("fdf.msg.proxyTitle"),
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r != DialogResult.Yes) return;
                    DownloadManager.Instance.Stop(task);
                }

                DownloadManager.Instance.SetTaskProxy(task, proxy);
            }
            else
            {
                if (string.Equals(proxy, DownloadManager.LastProxyUrl, StringComparison.OrdinalIgnoreCase))
                    return;
                DownloadManager.LastProxyUrl = proxy;
            }

            RefreshDetail();
        }

        private void miManageProxy_Click(object sender, EventArgs e)
        {
            string current = "";
            try
            {
                if (System.IO.File.Exists(GithubProxy.ConfigPath))
                    current = System.IO.File.ReadAllText(GithubProxy.ConfigPath);
            }
            catch { }

            ProxyManagerForm.ShowDialog(this, current, newText =>
            {
                try
                {
                    System.IO.File.WriteAllText(GithubProxy.ConfigPath, newText ?? "");
                    GithubProxy.Reload();
                    UpdateProxyLabel();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format(L.T("fdf.msg.saveFail"), ex.Message), L.T("fdf.msg.infoTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        // ==================== 右键菜单 ====================

        private ToolStripMenuItem miPrimary, miRedownload, miCleanPart, miChangeSaveDir,
                                   miChangeExtractDir, miOpenFolder, miCopyUrl, miViewUrl, miRemove;

        private void InitContextMenu()
        {
            miPrimary = new ToolStripMenuItem(L.T("fdf.action.start"));
            miPrimary.Click += (s, e) => { var t = ContextTask; if (t != null) HandlePrimaryAction(t); };
            miRedownload = new ToolStripMenuItem(L.T("fdf.menu.redownload"));
            miRedownload.Click += (s, e) => { var t = ContextTask; if (t != null) RedownloadTask(t); };
            miCleanPart = new ToolStripMenuItem(L.T("fdf.menu.cleanPart"));
            miCleanPart.Click += (s, e) => { var t = ContextTask; if (t != null) CleanPartialTask(t); };
            miChangeSaveDir = new ToolStripMenuItem(L.T("fdf.menu.changeSaveDir"));
            miChangeSaveDir.Click += (s, e) => { var t = ContextTask; if (t != null) ChangeSaveDir(t); };
            miChangeExtractDir = new ToolStripMenuItem(L.T("fdf.menu.changeExtractDir"));
            miChangeExtractDir.Click += (s, e) => { var t = ContextTask; if (t != null) ChangeExtractDir(t); };
            miOpenFolder = new ToolStripMenuItem(L.T("fdf.menu.openFolder"));
            miOpenFolder.Click += (s, e) => { var t = ContextTask; if (t != null) OpenContainingFolder(t); };
            miCopyUrl = new ToolStripMenuItem(L.T("fdf.menu.copyUrl"));
            miCopyUrl.Click += (s, e) => { var t = ContextTask; if (t != null) CopyUrl(t); };
            miViewUrl = new ToolStripMenuItem(L.T("fdf.menu.viewUrl"));
            miViewUrl.Click += (s, e) => { var t = ContextTask; if (t != null) ViewUrl(t); };
            miRemove = new ToolStripMenuItem(L.T("fdf.menu.remove"));
            miRemove.Click += (s, e) => { var t = ContextTask; if (t != null) DeleteTask(t); };

            contextMenuRow.Items.AddRange(new ToolStripItem[]
            {
                miPrimary,
                miRedownload,
                miCleanPart,
                new ToolStripSeparator(),
                miChangeSaveDir,
                miChangeExtractDir,
                miOpenFolder,
                new ToolStripSeparator(),
                miCopyUrl,
                miViewUrl,
                new ToolStripSeparator(),
                miRemove
            });
            flpRows.ContextMenuStrip = contextMenuRow;
        }

        private DownloadTask ContextTask => _contextTask ?? SelectedTask;

        private void contextMenuRow_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 右击时 RowControl.OnMouseDown 已通过 SetSelectedFromRow(_rowIndex) 设置好 _selectedIndex，
            // 这里直接用 SelectedTask 作为目标任务（与左击选中行同源），不再做坐标反推。
            // 坐标反推在排序/滚动后会命中错误的行（_rowIndex 由 SortRowsIfNeeded 同步，永远准确）。
            _contextTask = null;
            var t = ContextTask;   // = SelectedTask（右击行已选中）；点空白时退回已选中行
            bool has = t != null;
            bool busy = has && t.IsBusy;
            bool hasPartial = has && DownloadManager.Instance.HasPartial(t);
            bool isQueued = has && t.State == DownloadState.Queued;
            bool isGh = has && GithubProxy.IsGithub(t.Url);

            if (has)
            {
                miPrimary.Text = PrimaryActionMenuText(t.State);
                miPrimary.Enabled = t.State != DownloadState.Completed && t.State != DownloadState.Extracting;
                miRedownload.Enabled = !busy && !isQueued;
                miCleanPart.Enabled = !busy && hasPartial;
                miChangeSaveDir.Enabled = !busy;
                miChangeExtractDir.Enabled = !busy && t.ExtractDir != null;
                miOpenFolder.Enabled = hasPartial || (has && !string.IsNullOrEmpty(t.SaveDir) && Directory.Exists(t.SaveDir));
                miCopyUrl.Enabled = !string.IsNullOrEmpty(t.Url);
                miViewUrl.Enabled = !string.IsNullOrEmpty(t.Url);
                miRemove.Enabled = !busy;
            }
            else
            {
                miPrimary.Enabled = miRedownload.Enabled = miCleanPart.Enabled =
                miChangeSaveDir.Enabled = miChangeExtractDir.Enabled =
                miOpenFolder.Enabled = miCopyUrl.Enabled = miViewUrl.Enabled =
                miRemove.Enabled = false;
            }
        }

        private static string PrimaryActionMenuText(DownloadState state)
        {
            switch (state)
            {
                case DownloadState.Downloading: return L.T("fdf.action.pause");
                case DownloadState.Paused:
                case DownloadState.Failed: return L.T("fdf.action.resume");
                case DownloadState.Queued: return L.T("fdf.action.start");
                case DownloadState.Completed: return L.T("fdf.action.completed");
                case DownloadState.Extracting: return L.T("fdf.action.extracting");
                default: return L.T("fdf.action.start");
            }
        }

        // ==================== 操作分发（来自 RowControl 按钮） ====================

        private void OnRowPrimaryClicked(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= flpRows.Controls.Count) return;
            var rc = (RowControl)flpRows.Controls[rowIndex];
            var t = rc.Tag as DownloadTask;
            if (t != null) HandlePrimaryAction(t);
        }

        private void OnRowDeleteClicked(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= flpRows.Controls.Count) return;
            var rc = (RowControl)flpRows.Controls[rowIndex];
            var t = rc.Tag as DownloadTask;
            if (t != null) DeleteTask(t);
        }

        private void HandlePrimaryAction(DownloadTask t)
        {
            switch (t.State)
            {
                case DownloadState.Downloading:
                    DownloadManager.Instance.Stop(t);
                    break;
                case DownloadState.Paused:
                case DownloadState.Failed:
                case DownloadState.Queued:
                    DownloadManager.Instance.Resume(t);
                    break;
            }
            RefreshDetail();
        }

        // ==================== 右键 / 工具栏 共享操作 ====================

        private void RedownloadTask(DownloadTask t) => btnRedownload_Click(t, EventArgs.Empty);
        private void CleanPartialTask(DownloadTask t) => btnCleanPart_Click(t, EventArgs.Empty);

        private void ChangeSaveDir(DownloadTask t)
        {
            if (t.IsBusy) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = string.Format(L.T("fdf.dlg.selectSaveDir"), Path.GetFileName(t.FileName));
                if (Directory.Exists(t.SaveDir)) dlg.SelectedPath = t.SaveDir;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    t.SaveDir = dlg.SelectedPath;
                    ReloadAll();
                }
            }
        }

        private void ChangeExtractDir(DownloadTask t)
        {
            if (t.IsBusy || t.ExtractDir == null) return;
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = L.T("fdf.dlg.selectExtractDir");
                if (Directory.Exists(t.ExtractDir)) dlg.SelectedPath = t.ExtractDir;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    t.ExtractDir = dlg.SelectedPath;
                    ReloadAll();
                }
            }
        }

        private void OpenContainingFolder(DownloadTask t)
        {
            string dir = !string.IsNullOrEmpty(t.SaveDir) && Directory.Exists(t.SaveDir)
                ? t.SaveDir : Path.GetDirectoryName(t.TempPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try { System.Diagnostics.Process.Start("explorer.exe", dir); }
            catch (Exception ex) { MessageBox.Show(this, string.Format(L.T("fdf.msg.openFolderFail"), ex.Message)); }
        }

        private void CopyUrl(DownloadTask t)
        {
            try
            {
                string url = DownloadManager.ComposeRequestUrl(t);
                Clipboard.SetText(url ?? t.Url ?? "");
            }
            catch (Exception ex) { MessageBox.Show(this, string.Format(L.T("fdf.msg.copyFail"), ex.Message)); }
        }

        private void ViewUrl(DownloadTask t)
        {
            string url = DownloadManager.ComposeRequestUrl(t);
            ViewUrlDialogForm.Show(this, url, t.FileName);
        }

        /// <summary>
        /// 删除任务（行内 ✕ 按钮 + 右键菜单"删除"共用）：
        /// 弹确认框询问是否连原文件一起删；删文件失败不阻塞移除记录。
        /// </summary>
        private void DeleteTask(DownloadTask t)
        {
            if (t.IsBusy)
            {
                MessageBox.Show(this, L.T("fdf.msg.busyDelete"), L.T("fdf.msg.deleteTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool fileExists = false;
            long part = 0, done = 0;
            try { if (File.Exists(t.TempPath)) part = new FileInfo(t.TempPath).Length; } catch { }
            try { if (File.Exists(t.SavePath)) done = new FileInfo(t.SavePath).Length; } catch { }
            fileExists = part > 0 || done > 0;

            string fileInfo = null;
            if (fileExists)
            {
                if (part > 0) fileInfo = string.Format(L.T("fdf.delete.partial"), DownloadManager.FormatSize(part));
                if (done > 0)
                    fileInfo = (fileInfo != null ? fileInfo + L.T("fdf.text.joiner") : "")
                        + string.Format(L.T("fdf.delete.finalFile"), DownloadManager.FormatSize(done));
                fileInfo = string.Format(L.T("fdf.delete.willDelete"), fileInfo);
            }

            DialogResult r = DeleteConfirmForm.Show(this, t.FileName, fileExists, fileInfo);
            if (r != DialogResult.Yes && r != DialogResult.No) return;

            // 选"是"：连原文件（含未下完的半成品）一起删；失败只提示，记录仍移除
            if (r == DialogResult.Yes)
            {
                try
                {
                    if (File.Exists(t.SavePath)) File.Delete(t.SavePath);
                    if (File.Exists(t.TempPath)) File.Delete(t.TempPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format(L.T("fdf.msg.deleteFileFail"), ex.Message), L.T("fdf.msg.deleteTitle"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            if (!DownloadManager.Instance.Remove(t))
                MessageBox.Show(this, L.T("fdf.msg.deleteRecordFail"), L.T("fdf.msg.deleteTitle"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ==================== 钩子回调（封送到 UI 线程） ====================

        private void OnTaskAdded(DownloadTask task) => Post(() => { AddOrUpdateRow(task); RefreshDetail(); RefreshStatusStrip(); });
        private void OnTaskRemoved(DownloadTask task) => Post(() => { RemoveRow(task); RefreshDetail(); RefreshStatusStrip(); });
        private void OnTaskChanged(DownloadTask task)
        {
            Post(() =>
            {
                UpdateRow(task);
                RefreshDetail();
                RefreshStatusStrip();
                MaybeNotifyFailure(task);
            });
        }

        // 失败弹窗去重：状态刚转入 Failed 时只弹一次；重试后再次失败会重新弹
        private readonly HashSet<DownloadTask> _failNotified = new HashSet<DownloadTask>();

        private void MaybeNotifyFailure(DownloadTask task)
        {
            if (task.State != DownloadState.Failed) { _failNotified.Remove(task); return; }
            if (!_failNotified.Add(task)) return;   // 已经弹过（状态停在 Failed 反复刷新）
            MessageBox.Show(this,
                string.Format(L.T("fdf.msg.taskFailed"), task.FileName,
                    string.IsNullOrEmpty(task.Message) ? L.T("fdf.common.unknown") : task.Message),
                L.T("fdf.msg.failTitle"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void Post(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { /* 窗口正在关闭 */ }
            catch (InvalidOperationException) { /* 句柄未就绪 */ }
        }

        // ==================== 行增/改/删 ====================

        /// <summary>当前选中的任务（null = 无选中）</summary>
        private DownloadTask SelectedTask =>
            (_selectedIndex >= 0 && _selectedIndex < flpRows.Controls.Count)
                ? flpRows.Controls[_selectedIndex].Tag as DownloadTask
                : null;

        private void ReflowRowWidths()
        {
            int w = flpRows.ClientSize.Width;
            if (w <= 0) return;
            foreach (RowControl rc in flpRows.Controls)
                rc.SetRowWidth(w);
        }

        private void ReloadAll()
        {
            flpRows.SuspendLayout();
            try
            {
                foreach (RowControl rc in flpRows.Controls) rc.Dispose();
                flpRows.Controls.Clear();
                _taskIndex.Clear();
                _selectedIndex = -1;

                int idx = 0;
                foreach (var t in DownloadManager.Instance.Snapshot()
                             .Select((t, i) => (t, i))
                             .OrderBy(x => RankOf(x.t)).ThenBy(x => x.i)
                             .Select(x => x.t))
                    AppendRow(t, idx++);
            }
            finally { flpRows.ResumeLayout(true); }
            ReflowRowWidths();
        }

        private void AppendRow(DownloadTask t, int idx)
        {
            var rc = new RowControl { Tag = t };
            rc.Bind(idx, t);
            rc.SetRowParity((idx % 2) == 1);
            rc.PrimaryClicked += OnRowPrimaryClicked;
            rc.DeleteClicked += OnRowDeleteClicked;
            // 双击空白 = 打开目录（保留原 CellDoubleClick 行为）
            rc.DoubleClick += (s, e) => OpenContainingFolder(t);
            // 绝对定位：第 idx 行固定在 Y = idx * RowHeight（X 由 ReflowRowWidths 设宽）
            rc.Location = new Point(0, idx * RowControl.RowHeight);
            flpRows.Controls.Add(rc);
            _taskIndex[t] = idx;
        }

        private void AddOrUpdateRow(DownloadTask t)
        {
            if (_taskIndex.ContainsKey(t)) { UpdateRow(t); return; }
            int idx = flpRows.Controls.Count;
            flpRows.SuspendLayout();
            AppendRow(t, idx);
            flpRows.ResumeLayout(true);
            ReflowRowWidths();
            SortRowsIfNeeded();   // 新任务（通常为排队中）插到已完成/暂停等行之前
        }

        private void UpdateRow(DownloadTask t)
        {
            if (!_taskIndex.TryGetValue(t, out int idx))
            {
                AddOrUpdateRow(t);
                return;
            }
            if (idx < 0 || idx >= flpRows.Controls.Count) return;

            var rc = (RowControl)flpRows.Controls[idx];
            rc.RefreshProgress();
            // 状态变化时上浮/下沉（开始下载 → 顶上去；下载完成 → 沉底）
            SortRowsIfNeeded();
        }

        // ==================== 行排序（下载中的靠前，完成的沉底）====================

        /// <summary>行的排位权重：值小 = 排前面。下载中 > 解压中 > 排队 > 暂停 > 失败 > 完成</summary>
        private static int RankOf(DownloadTask t)
        {
            if (t == null) return 99;
            switch (t.State)
            {
                case DownloadState.Downloading: return 0;
                case DownloadState.Extracting:  return 1;
                case DownloadState.Queued:      return 2;
                case DownloadState.Paused:      return 3;
                case DownloadState.Failed:      return 4;
                case DownloadState.Completed:   return 5;
                default:                       return 6;
            }
        }

        /// <summary>
        /// 按权重稳定排序现有行；顺序没变化时零开销（不动任何控件）。
        /// 只调整 ChildIndex / Top / 行号 / 斑马纹 / 索引表，不重建控件。
        /// </summary>
        private void SortRowsIfNeeded()
        {
            int n = flpRows.Controls.Count;
            if (n < 2) return;

            var rows = new RowControl[n];
            for (int i = 0; i < n; i++) rows[i] = (RowControl)flpRows.Controls[i];
            var order = rows
                .Select((rc, i) => (rc, i, rank: RankOf(rc.Tag as DownloadTask)))
                .OrderBy(x => x.rank).ThenBy(x => x.i)   // 同权重保持现有先后（稳定）
                .ToArray();

            bool moved = false;
            for (int i = 0; i < n; i++)
                if (order[i].i != i) { moved = true; break; }
            if (!moved) return;

            // 选中行随控件走，不跟旧索引
            RowControl sel = (_selectedIndex >= 0 && _selectedIndex < n) ? rows[_selectedIndex] : null;

            flpRows.SuspendLayout();
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var rc = order[i].rc;
                    flpRows.Controls.SetChildIndex(rc, i);
                    rc.RowIndex = i;
                    rc.Top = i * RowControl.RowHeight;
                    rc.SetRowParity((i % 2) == 1);
                    var tt = rc.Tag as DownloadTask;
                    if (tt != null) _taskIndex[tt] = i;
                }
            }
            finally { flpRows.ResumeLayout(false); }

            if (sel != null)
                _selectedIndex = flpRows.Controls.IndexOf(sel);
        }

        private void RemoveRow(DownloadTask t)
        {
            if (!_taskIndex.TryGetValue(t, out int idx)) return;
            flpRows.SuspendLayout();
            try
            {
                if (idx < flpRows.Controls.Count)
                {
                    var rc = (RowControl)flpRows.Controls[idx];
                    rc.PrimaryClicked -= OnRowPrimaryClicked;
                    rc.DeleteClicked -= OnRowDeleteClicked;
                    flpRows.Controls.RemoveAt(idx);
                    rc.Dispose();
                }
                _taskIndex.Remove(t);
                // 重建索引（RemoveAt 后索引移位）
                _taskIndex.Clear();
                for (int i = 0; i < flpRows.Controls.Count; i++)
                {
                    var tt = flpRows.Controls[i].Tag as DownloadTask;
                    if (tt != null) _taskIndex[tt] = i;
                    // 绝对定位：被删行之后的整体上移一格 + 同步行号/斑马纹（之前的行不动）
                    if (i >= idx)
                    {
                        flpRows.Controls[i].Top -= RowControl.RowHeight;
                        ((RowControl)flpRows.Controls[i]).RowIndex = i;
                    }
                    ((RowControl)flpRows.Controls[i]).SetRowParity((i % 2) == 1);
                }
                // 选中态调整
                if (_selectedIndex == idx) _selectedIndex = -1;
                else if (_selectedIndex > idx) _selectedIndex--;
            }
            finally { flpRows.ResumeLayout(true); }
            ReflowRowWidths();
        }

        // ==================== 底部按钮 ====================

        private void btnStart_Click(object sender, EventArgs e)
        {
            var task = SelectedTask;
            if (task == null) return;
            if (task.State == DownloadState.Downloading)
                DownloadManager.Instance.Stop(task);
            else
                DownloadManager.Instance.Resume(task);
            RefreshDetail();
        }

        private void btnRedownload_Click(object sender, EventArgs e)
        {
            // 右键菜单把目标任务当 sender 传入；工具栏按钮则用当前选中行
            var task = sender as DownloadTask ?? SelectedTask;
            if (task == null) return;

            long part = 0, done = 0;
            try { if (File.Exists(task.TempPath)) part = new FileInfo(task.TempPath).Length; } catch { }
            try { if (File.Exists(task.SavePath)) done = new FileInfo(task.SavePath).Length; } catch { }
            if (part > 0 || done > 0)
            {
                string what = "";
                if (part > 0) what += string.Format(L.T("fdf.redownload.partialData"), DownloadManager.FormatSize(part));
                if (done > 0)
                    what += (what.Length > 0 ? L.T("fdf.text.joiner") : "")
                        + string.Format(L.T("fdf.redownload.finalFile"), DownloadManager.FormatSize(done));
                var r = MessageBox.Show(this,
                    string.Format(L.T("fdf.msg.redownloadConfirm"), what),
                    L.T("fdf.msg.redownloadTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }
            DownloadManager.Instance.Redownload(task, true);
            RefreshDetail();
        }

        private void FileDownloadForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            d = null;
        }

        private void btnCleanPart_Click(object sender, EventArgs e)
        {
            var task = SelectedTask;
            if (task == null) return;

            long part = 0;
            try { if (File.Exists(task.TempPath)) part = new FileInfo(task.TempPath).Length; } catch { }
            var r = MessageBox.Show(this,
                string.Format(L.T("fdf.msg.cleanConfirm"), DownloadManager.FormatSize(part)),
                L.T("fdf.msg.cleanTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            DownloadManager.Instance.CleanPartial(task);
            RefreshDetail();
        }

        private void toolStripButtonCreate_Click(object sender, EventArgs e)
        {
            new FileDownloadBefore().Show();
        }

        // ==================== 状态栏（每秒刷新总览 + 选中行进度）====================

        private Timer _statusTimer;

        private void InitStatusTimer()
        {
            _statusTimer = new Timer { Interval = 500 };   // 500ms 节流
            _statusTimer.Tick += (s, e) =>
            {
                // 刷新选中行的进度数字（避免每个 RowControl 都自定时器）
                var sel = SelectedTask;
                if (sel != null && sel.State == DownloadState.Downloading)
                {
                    if (_selectedIndex >= 0 && _selectedIndex < flpRows.Controls.Count)
                        ((RowControl)flpRows.Controls[_selectedIndex]).RefreshProgress();
                }
                RefreshStatusStrip();
            };
            _statusTimer.Start();
        }

        private void RefreshStatusStrip()
        {
            int total = 0, active = 0;
            double totalBps = 0;
            long totalBytes = 0, downloadedBytes = 0;
            var snap = DownloadManager.Instance.Snapshot();
            foreach (var t in snap)
            {
                total++;
                if (t.State == DownloadState.Downloading) { active++; totalBps += t.SpeedBps; }
                totalBytes += t.TotalBytes > 0 ? t.TotalBytes : t.ExpectedSize;
                downloadedBytes += t.DownloadedBytes;
            }
            toolStripStatusLabel1.Text = string.Format(
                L.T("fdf.status.summary"),
                total, active,
                DownloadManager.FormatSize((long)totalBps),
                DownloadManager.FormatSize(downloadedBytes),
                DownloadManager.FormatSize(totalBytes));
        }

        // ==================== 关闭 / 选中恢复 ====================

        private void LlamaFileDownloadForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DownloadManager.Instance.TaskAdded -= OnTaskAdded;
            DownloadManager.Instance.TaskChanged -= OnTaskChanged;
            DownloadManager.Instance.TaskRemoved -= OnTaskRemoved;

            _statusTimer?.Stop();
            _statusTimer?.Dispose();
            _statusTimer = null;

            var sel = SelectedTask;
            UiState.Set(StateKeySelected, sel != null ? sel.FileName + "|" + sel.State : "");
        }

        private const string StateKeySelected = "downloadForm.selectedKey";

        private void RestoreSelectedIndex()
        {
            string key = UiState.Get(StateKeySelected, "");
            if (string.IsNullOrEmpty(key)) return;
            for (int i = 0; i < flpRows.Controls.Count; i++)
            {
                var t = flpRows.Controls[i].Tag as DownloadTask;
                if (t != null && (t.FileName + "|" + t.State) == key)
                {
                    SetSelected(i);
                    // 滚动到该行
                    flpRows.ScrollControlIntoView(flpRows.Controls[i]);
                    return;
                }
            }
        }
    }
}