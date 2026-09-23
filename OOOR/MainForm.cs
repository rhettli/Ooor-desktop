using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 本地模型管理主窗口（界面见 MainForm.Designer.cs，可在设计器预览）：
    /// 动态识别 llama-bin 下含 llama-server.exe 的版本（「选择llama」窗口切换），
    /// 通过「选择模型」窗口挑选 *.gguf（内置 models 目录 + ref_models.conf 引用，自动排除 mmproj-*），
    /// 按 Start.bat 同款参数启动/停止 llama-server，实时显示日志。
    /// llama-server 由 ServerManager 全局单例持有：关闭窗口不停止服务，
    /// 重新打开窗口时从 run_state.conf 恢复运行状态（模型、参数、运行中 UI）。
    /// 选中的 llama 版本与模型写入 ui-state.conf，下次启动程序时自动恢复选择。
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>全局单例的 llama-server 封装（关窗不 Dispose、不停服务）</summary>
        private readonly LlamaServer _server = ServerManager.Server;

        /// <summary>最新扫描出的全部可选 llama 版本（llama-bin 下含 llama-server.exe 的子目录）</summary>
        private List<VersionInfo> _versions = new List<VersionInfo>();

        /// <summary>当前选中的 llama 版本（通过「选择llama」窗口确定）；完整路径只存在这里，编辑框只显示版本名</summary>
        private VersionInfo _selectedVersion;

        /// <summary>最新扫描出的全部可选模型（内置 models 目录 + ref_models.conf 引用，内置优先）</summary>
        private List<ModelInfo> _models = new List<ModelInfo>();

        /// <summary>当前选中的模型（通过「选择模型」窗口确定）；完整路径只存在这里，编辑框只显示名称</summary>
        private ModelInfo _selectedModel;

        /// <summary>当前模型自动匹配到的多模态投影文件（mmproj-*.gguf）路径；找不到为 null</summary>
        private string _autoMmproj;

        /// <summary>程序化填充 mmproj 下拉框期间为 true，避免误触发「选择变化」处理</summary>
        private bool _loadingMmproj;

        /// <summary>服务是否已输出监听地址（形如 http://127.0.0.1:8080）。
        /// 「打开网页」按钮仅在此为 true 且服务运行时可点。
        /// </summary>
        private bool _webReady;

        /// <summary>启动模型时的进度动画定时器：每秒 +5，到达 100 停留；
        /// 服务就绪后跳满 + 500ms 隐藏 panelProgress；失败/退出则立即隐藏</summary>
        private System.Windows.Forms.Timer _progressTimer;

        /// <summary>匹配日志中的监听地址，捕获端口号（如 main: server is listening on http://127.0.0.1:8080）</summary>
        private static readonly System.Text.RegularExpressions.Regex ListenUrlRegex =
            new System.Text.RegularExpressions.Regex(
                @"https?://[^\s""']+:(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        // ==================== 托盘图标 ====================

        /// <summary>系统托盘图标（启动即创建；双击 = 显示/隐藏主窗口）</summary>
        private NotifyIcon _tray;

        /// <summary>托盘菜单「退出」置 true；否则点关闭按钮只是隐藏到托盘（服务/下载继续）</summary>
        private bool _reallyExit;

        /// <summary>开机自启场景标志（Program.Main 解析 --autostart 时置 true）：
        /// 配合 AppSettings 决定是否隐藏主窗口 / 自动启动上次模型</summary>
        public bool AutoStartMode;

        public MainForm()
        {
            InitializeComponent();

            _server.LineReceived += OnServerLine;
            _server.ProcessExited += OnServerExited;

            InitTray();   // 启动即创建托盘图标（双击显示/隐藏主窗口）

            Load += MainForm_Load;

            // panelProgress 默认隐藏（启动模型时才显示）；进度条初始化为 0
            panelProgress.Visible = false;
            progressBar1.Value = 0;
            progressBar1.Maximum = 100;

            // 进度定时器：每秒 +5，到 100 停留（服务就绪后跳满 + 500ms 隐藏；失败立即隐藏）
            _progressTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _progressTimer.Tick += (s, e) =>
            {
                if (progressBar1.Value < 100)
                    progressBar1.Value = Math.Min(100, progressBar1.Value + 5);
                else
                    _progressTimer.Stop();   // 已满，停表保持满进度，等服务就绪/退出再隐藏
            };
        }

        /// <summary>创建托盘图标与右键菜单（主窗口 / 下载管理 / 退出）</summary>
        private void InitTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示 / 隐藏主窗口", null, (s, e) => ToggleMainWindow());
            menu.Items.Add("下载管理", null, (s, e) => FileDownloadForm.ShowSingle());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (s, e) => ExitApp());

            // 项目未内置 ico 资源：优先取 exe 关联图标，失败退回系统默认
            Icon icon;
            try { icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
            catch { icon = SystemIcons.Application; }

            _tray = new NotifyIcon
            {
                Icon = icon,
                Text = "ooor（双击显示/隐藏主窗口）",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (s, e) => ToggleMainWindow();
        }

        /// <summary>双击托盘：主窗口可见则隐藏，隐藏/最小化则恢复显示并前置</summary>
        private void ToggleMainWindow()
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                Hide();
            }
            else
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            }
        }

        /// <summary>真正退出程序（托盘菜单「退出」）</summary>
        private void ExitApp()
        {
            _reallyExit = true;
            Close();
        }

        /// <summary>
        /// 点窗口关闭按钮 = 隐藏到托盘（llama-server 与下载任务都继续后台运行）；
        /// 只有托盘菜单「退出」（_reallyExit）才允许真正关闭。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            base.OnFormClosing(e);
        }

        // 与 Start.bat 默认参数一致：CPU 版没有 GPU 后端，ngl 默认 0
        private const int DefaultCtx = 131072;
        private const int DefaultNPred = 8192;
        private const int Iq2Ctx = 8192;
        private const int Iq2NPred = 4096;

        // 上下文/预测长度下拉框首项占位值：选中即按模型自动取默认（IQ2 小上下文，其余大上下文）
        private static string ParamDefaultItem => LanguageManager.Instance.T("cmb.default");

        private long DefaultCtxForModel()
        {
            return _selectedModel != null && _selectedModel.IsIq2 ? Iq2Ctx : DefaultCtx;
        }

        private int DefaultNPredForModel()
        {
            return _selectedModel != null && _selectedModel.IsIq2 ? Iq2NPred : DefaultNPred;
        }

        /// <summary>输入为空或为「--默认--」（中英文）→ 视为选择默认</summary>
        private static bool IsParamDefaultText(string text)
        {
            string t = (text ?? "").Trim();
            if (t.Length == 0) return true;
            string zh = LanguageManager.Instance.T("cmb.default");
            string en = "--Default--";
            return t == zh || t == en || t == "--默认--";
        }

        /// <summary>解析上下文长度：默认项按模型取默认值，其余按输入正整数校验（弹窗提示）</summary>
        private bool TryResolveCtx(out long ctx)
        {
            if (IsParamDefaultText(cmbCtx.Text)) { ctx = DefaultCtxForModel(); return true; }
            return TryParsePositive(cmbCtx.Text, out ctx, "上下文长度");
        }

        /// <summary>解析预测长度：默认项按模型取默认值，其余按输入正整数校验（弹窗提示）</summary>
        private bool TryResolveNPred(out int npred)
        {
            if (IsParamDefaultText(cmbNPred.Text)) { npred = DefaultNPredForModel(); return true; }

            long v;
            if (!TryParsePositive(cmbNPred.Text, out v, "预测长度")) { npred = 0; return false; }
            if (v > int.MaxValue)
            {
                MessageBox.Show(this, "预测长度过大", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                npred = 0;
                return false;
            }
            npred = (int)v;
            return true;
        }

        // ==================== 选择持久化（ui-state.conf） ====================

        /// <summary>上次选择的 llama 版本名（ui-state.conf 的 key；下次启动据此恢复）</summary>
        private const string StateKeySelectedVersion = "mainForm.selectedLlamaVersion";

        /// <summary>上次选择的模型完整路径（ui-state.conf 的 key；下次启动据此恢复）</summary>
        private const string StateKeySelectedModel = "mainForm.selectedModelPath";

        // ==================== 数据加载 ====================

        private void MainForm_Load(object sender, EventArgs e)
        {
            // 注册下载完成钩子：手动文件下载（FileDownloadBefore 入队，Tag 为空）
            // 成功后自动刷新版本与模型列表（模型下到 models / llama-bin 时无需手动点刷新）
            DownloadManager.Instance.TaskCompleted += OnDownloadTaskCompleted;

            // 多语言：载入时应用一次 + 订阅切换事件即时刷新菜单
            LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
            ApplyLanguage();

            ReloadRuntime();
            ReloadProfiles();
            RestoreRunningState();

            // 开机自启场景：按 AppSettings 决定是否隐藏主窗口 / 自动启动上次模型
            if (AutoStartMode)
            {
                var app = AppSettings.Load();
                if (app.AutoStartHide)
                {
                    WindowState = FormWindowState.Minimized;
                    ShowInTaskbar = false;
                }
                if (app.AutoStartModel)
                {
                    // UI 已就绪后再触发启动，避免恢复选择尚未完成
                    BeginInvoke((MethodInvoker)(() => btnStart_Click(this, EventArgs.Empty)));
                }
            }
        }

        /// <summary>下载完成钩子（后台线程触发）：手动文件下载成功后回到 UI 线程刷新运行时</summary>
        private void OnDownloadTaskCompleted(DownloadTask task)
        {
            if (!string.IsNullOrEmpty(task.Tag)) return;   // 模型仓库 / llama 版本下载不走这里
            if (!IsHandleCreated || IsDisposed) return;    // 窗口已关：下载器仍在跑，静默忽略
            BeginInvoke((MethodInvoker)ReloadRuntime);
        }

        /// <summary>按当前语言刷新主窗口菜单文本（一级菜单 + 所有子项）</summary>
        private void ApplyLanguage()
        {
            var L = LanguageManager.Instance;

            // 一级菜单
            控制台ToolStripMenuItem.Text = L.T("menu.console");
            llama服务ToolStripMenuItem.Text = L.T("menu.llama");
            模型ToolStripMenuItem.Text = L.T("menu.model");
            方案ToolStripMenuItem.Text = L.T("menu.profile");
            帮助ToolStripMenuItem.Text = L.T("menu.more");

            // 控制台
            打开控制台AI助手ToolStripMenuItem.Text = L.T("console.openCli");
            打开控制台AI助手继续聊ToolStripMenuItem.Text = L.T("console.openCliContinue");
            打开控制台AI助手开放权限ToolStripMenuItem.Text = L.T("console.openCliFullPerm");
            打开窗口AI助手ToolStripMenuItem.Text = L.T("console.openWindowAgent");
            打开网页ToolStripMenuItem1.Text = L.T("console.openWeb");

            // Llama
            启动服务ToolStripMenuItem.Text = L.T("llama.start");
            复制OpenAPI地址ToolStripMenuItem.Text = L.T("llama.copyApi");
            llama原生控制台启动ToolStripMenuItem.Text = L.T("llama.runCli");
            下载新版本ToolStripMenuItem.Text = L.T("llama.downloadNew");
            管理版本ToolStripMenuItem.Text = L.T("llama.manageVersion");
            清空日志ToolStripMenuItem.Text = L.T("llama.clearLog");

            // 模型
            管理ToolStripMenuItem.Text = L.T("model.manage");
            下载新模型ToolStripMenuItem.Text = L.T("model.downloadNew");
            控制台对话管理ToolStripMenuItem.Text = L.T("model.consoleChat");
            ToolStripMenuItemConsoleTalkManager.Text = L.T("model.consoleChat");
            agent管理ToolStripMenuItem.Text = L.T("model.agentManage");

            // 方案
            保存方案ToolStripMenuItem.Text = L.T("profile.save");
            管理方案ToolStripMenuItem1.Text = L.T("profile.manage");

            // 更多
            下载管理ToolStripMenuItem.Text = L.T("more.downloadMgr");
            ToolStripMenuItemSetting.Text = L.T("more.settings");
            关于ToolStripMenuItem.Text = L.T("more.about");

            // 顶部参数区标签
            lblDir.Text = L.T("lbl.dir");
            lblModel.Text = L.T("lbl.model");
            lblCtx.Text = L.T("lbl.ctx");
            lblNPred.Text = L.T("lbl.npred");
            lblNgl.Text = L.T("lbl.ngl");
            lblHost.Text = L.T("lbl.host");
            lblPort.Text = L.T("lbl.port");

            // 按钮（工具栏 + 参数区）
            btnOpenChooseLlama.Text = L.T("btn.chooseLlama");
            buttonChooseRunModel.Text = L.T("btn.chooseModel");
            btnMoreParams.Text = L.T("btn.moreParams");
            btnStart.Text = L.T("llama.start");
            toolStripButtonSaveSlu.Text = L.T("profile.save");
            toolStripButtonDownloadForm.Text = L.T("btn.downloadTask");
            toolStripButtonRefresh.Text = L.T("btn.refresh");
            toolStripSplitButton3.Text = L.T("model.agentManage");
            ToolStripMenuItemOpenConsole.Text = L.T("console.openCli");

            // 复选框 + 下拉默认文本（当前为默认占位时随语言刷新）
            chkMmproj.Text = L.T("chk.mmproj");
            if (IsParamDefaultText(cmbCtx.Text)) cmbCtx.Text = ParamDefaultItem;
            if (IsParamDefaultText(cmbNPred.Text)) cmbNPred.Text = ParamDefaultItem;

            // 状态栏 + 窗口标题
            lblStatus.Text = L.T("lbl.status");
            Text = L.T("form.title") + $" - Ver({DEF.ver})";
        }

        /// <summary>语言切换事件：回到 UI 线程刷新菜单 + 方案下拉占位</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (!IsHandleCreated || IsDisposed) return;
            BeginInvoke((MethodInvoker)(() =>
            {
                ApplyLanguage();
                ReloadProfiles();   // 刷新「--请选择方案--」占位文本
            }));
        }

        /// <summary>重新识别程序目录、服务端程序与模型列表</summary>
        private void ReloadRuntime()
        {
            ReloadVersions();

            // 没有版本可选时直接报错（提示用户把 llama.cpp 发行版放进 llama-bin）
            if (_versions.Count == 0)
            {
                SetStatus("未检测到可用的 llama 版本", true);
                LogT("log.err.noLlamaBin", LlamaRuntime.LlamaBinsDir);
                LogT("log.hint.putLlama");
                SetRunUi(false);
                return;
            }

            // 首次加载：恢复上次持久化的选择（ui-state.conf）；已加载过则保留当前选择，避免重扫（刷新）改掉用户选择。
            // 恢复目标已不存在或已被禁用时退回第一个可用版本（LlamaRuntime.IsVersionDisabled 静态缓存，不重复扫盘）
            if (_selectedVersion == null)
            {
                VersionInfo restored = FindVersion(UiState.Get(StateKeySelectedVersion, ""));
                if (restored != null && !LlamaRuntime.IsVersionDisabled(restored.Name))
                {
                    LogT("log.info.restoredVersion", restored.Name);
                }
                else
                {
                    restored = _versions.Find(v => !LlamaRuntime.IsVersionDisabled(v.Name));
                }
                SelectVersion(restored);   // 可能为 null（全部禁用 → 清空输入框）
            }
            ApplySelectedVersion();
        }

        /// <summary>扫描 llama-bin 下的子目录，填充版本列表（保留当前选择）</summary>
        private void ReloadVersions()
        {
            VersionInfo keep = _selectedVersion;

            var versions = LlamaRuntime.ScanLlamaVersions();
            _versions = versions;

            if (versions.Count == 0)
            {
                LogT("log.hint.noServerInBin", LlamaRuntime.LlamaBinsDir);
            }
            else
            {
                LogT("log.info.foundVersions", versions.Count, LlamaRuntime.LlamaBinsDir);
            }

            // 保留之前的选择：重扫后对象已是新实例，需按名称重新指向列表里的那个。
            // 但 llama 版本可能被标记为禁用——已禁用的版本不应保留为当前选中。
            VersionInfo match = null;
            if (keep != null && !LlamaRuntime.IsVersionDisabled(keep.Name))
                match = versions.Find(v => string.Equals(v.Name, keep.Name, StringComparison.OrdinalIgnoreCase));
            SelectVersion(match);
        }

        /// <summary>更新选中的版本（只改字段与输入框文本，不做可用性检查）</summary>
        private void SelectVersion(VersionInfo v)
        {
            _selectedVersion = v;
            txtLlamaDir.Text = v == null ? "" : v.ToString();
        }

        /// <summary>选中了某个 llama 版本：写入运行时并重检服务端/模型</summary>
        private void ApplySelectedVersion()
        {
            var v = _selectedVersion;
            if (v == null)
            {
                LlamaRuntime.SetSelectedVersion(null);
                SetRunUi(false);
                return;
            }

            // 防御：当前选中的版本已被「llama 版本」窗口禁用 → 清空并提示
            if (LlamaRuntime.IsVersionDisabled(v.Name))
            {
                LogT("log.warn.versionDisabled", v.Name);
                LlamaRuntime.SetSelectedVersion(null);
                SelectVersion(null);
                SetStatus("当前选中的版本已被禁用", true);
                SetRunUi(false);
                return;
            }

            LlamaRuntime.SetSelectedVersion(v.Name);
            UiState.Set(StateKeySelectedVersion, v.Name);   // 记住选择，下次启动程序恢复
            LogT("log.info.selectedVersion", v.FullPath);

            if (!v.HasServerEx || !LlamaRuntime.ServerPresent)
            {
                SetStatus("当前版本缺少 " + LlamaRuntime.ServerExeName, true);
                LogT("log.err.noServer", LlamaRuntime.ServerExePath);
                LogT("log.hint.putServer");
                SetRunUi(false);
            }
            else
            {
                SetStatus("已检测到 llama-server（" + v.Name + "）", false);
                LogT("log.info.serverPath", LlamaRuntime.ServerExePath);
                if (!_server.IsRunning) SetRunUi(false);
            }

            ReloadModels();
        }

        private void ReloadModels()
        {
            var models = LlamaRuntime.ScanModels();
            _models = models;

            if (models.Count == 0)
            {
                LogT("log.hint.noModel", LlamaRuntime.ModelsDir);
                LogT("log.hint.importModel");
            }
            else
            {
                LogT("log.info.foundModels", models.Count, LlamaRuntime.ModelsDir);
            }

            // 选择优先级：本次会话已选（按完整路径匹配）→ 上次持久化的选择（ui-state.conf）→ 列表第一个
            ModelInfo keep = null;
            if (_selectedModel != null)
            {
                keep = models.Find(m => string.Equals(m.FullPath, _selectedModel.FullPath,
                    StringComparison.OrdinalIgnoreCase));
            }
            if (keep == null)
            {
                keep = FindModel(models, UiState.Get(StateKeySelectedModel, ""));
                if (keep != null) LogT("log.info.restoredModel", keep.FullPath);
            }
            if (keep == null && models.Count > 0) keep = models[0];

            _selectedModel = keep;
            OnModelSelected();
        }

        /// <summary>切换模型：编辑框只显示模型名称，按 Start.bat 规则给 IQ2 模型填小上下文，并检测 mmproj</summary>
        private void OnModelSelected()
        {
            var model = _selectedModel;
            if (model == null)
            {
                txtModel.Text = "";
                ClearMmprojUi("（未选择模型）");
                return;
            }

            // 只显示模型名称（不显示目录）；完整路径保存在 _selectedModel 中
            txtModel.Text = Path.GetFileName(model.FullPath);

            // 记住选择，下次启动程序恢复（ui-state.conf）
            UiState.Set(StateKeySelectedModel, model.FullPath);

            // 上下文/预测长度回落到「--默认--」：由所选模型自动决定（高压缩模型用小上下文），用户之后仍可手改
            cmbCtx.Text = ParamDefaultItem;
            cmbNPred.Text = ParamDefaultItem;

            LoadMmprojCandidates(model);
        }

        /// <summary>
        /// 载入该模型的多模态投影文件（mmproj-*.gguf，文件名以 "mmproj-" 开头）候选：
        /// 自动匹配结果（同级目录优先，其次名称同族兜底）作为下拉框默认选中项，
        /// 用户可点开下拉框改选其它投影文件；未匹配到则不勾选，按纯文本模式启动。
        /// </summary>
        private void LoadMmprojCandidates(ModelInfo model)
        {
            var index = LlamaRuntime.MmprojIndex.Build();
            _autoMmproj = index.Find(model.FullPath);

            _loadingMmproj = true;
            try
            {
                cmbMmproj.Items.Clear();
                foreach (string f in index.Candidates(model.FullPath)) cmbMmproj.Items.Add(f);

                if (!string.IsNullOrEmpty(_autoMmproj)) cmbMmproj.SelectedItem = _autoMmproj;
                else if (cmbMmproj.Items.Count > 0) cmbMmproj.SelectedIndex = 0;
                else cmbMmproj.SelectedIndex = -1;
            }
            finally
            {
                _loadingMmproj = false;
            }

            if (string.IsNullOrEmpty(_autoMmproj))
            {
                chkMmproj.Checked = false;
                chkMmproj.Enabled = false;
                cmbMmproj.Visible = false;
            }
            else
            {
                chkMmproj.Enabled = true;
                chkMmproj.Checked = true;
                cmbMmproj.Visible = true;
                UpdateMmprojHint();
            }
        }

        /// <summary>清空多模态投影相关 UI（未选择模型 / 模型已不在列表中时）</summary>
        private void ClearMmprojUi(string hint)
        {
            _autoMmproj = null;

            _loadingMmproj = true;
            try
            {
                cmbMmproj.Items.Clear();
                cmbMmproj.SelectedIndex = -1;
            }
            finally
            {
                _loadingMmproj = false;
            }

            chkMmproj.Checked = false;
            chkMmproj.Enabled = false;
            cmbMmproj.Visible = false;
        }

        /// <summary>当前下拉框选中的投影文件完整路径；未选中为 null</summary>
        private string SelectedMmprojPath()
        {
            return cmbMmproj.SelectedItem as string;
        }

        /// <summary>刷新 mmproj 提示：与自动匹配结果相同显示「自动检测」，改选过则显示「已选择」</summary>
        private void UpdateMmprojHint()
        {
            string path = SelectedMmprojPath();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

        }

        /// <summary>勾选多模态投影后显示投影文件下拉框（未勾选或无候选则隐藏）</summary>
        private void chkMmproj_CheckedChanged(object sender, EventArgs e)
        {
            cmbMmproj.Visible = chkMmproj.Checked && chkMmproj.Enabled;
        }

        /// <summary>下拉框改选投影文件：仅更新提示文字，启动时以选中项为准</summary>
        private void cmbMmproj_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingMmproj) return;
            UpdateMmprojHint();
        }

        /// <summary>
        /// 窗口打开后恢复服务运行状态（窗口关闭不停止服务）：
        /// 1. 服务仍在运行（单例内存记录优先）→ 直接恢复 UI；
        /// 2. 读 run_state.conf，用记录的 PID 重新接管（Attach）存活的 llama-server → 恢复 UI；
        /// 3. 状态文件存在但进程已不在 → 清除状态记录。
        /// </summary>
        private void RestoreRunningState()
        {
            ServerManager.RunState st = ServerManager.LastRunState;

            if (!_server.IsRunning)
            {
                var file = ServerManager.LoadRunState();
                if (file != null)
                {
                    if (_server.Attach(file.Pid, file.Port))
                    {
                        st = file;
                    }
                    else
                    {
                        // 状态文件有记录但服务已退出：清掉过期状态
                        ServerManager.OnStopped();
                        LogT("log.hint.serverGone");
                    }
                }
            }
            else if (st == null)
            {
                // 单例仍在运行但没有内存记录（理论上少见）：尝试读文件补齐
                var file = ServerManager.LoadRunState();
                if (file != null) st = file;
            }

            if (st == null || !_server.IsRunning) return;

            // 恢复选中模型与参数
            var model = _models.Find(m => string.Equals(m.FullPath, st.ModelPath,
                StringComparison.OrdinalIgnoreCase));
            if (model != null)
            {
                _selectedModel = model;
                OnModelSelected();
            }
            else
            {
                // 模型可能已被删除/软删除：只显示文件名并提示
                _selectedModel = null;
                txtModel.Text = Path.GetFileName(st.ModelPath);
                ClearMmprojUi("（模型已不在当前列表中）");
                LogT("log.warn.modelGone", st.ModelPath);
            }

            if (st.Ctx > 0) cmbCtx.Text = st.Ctx == DefaultCtxForModel() ? ParamDefaultItem : st.Ctx.ToString();
            if (st.NPred > 0) cmbNPred.Text = st.NPred == DefaultNPredForModel() ? ParamDefaultItem : st.NPred.ToString();
            if (st.Ngl >= 0) txtNgl.Text = st.Ngl.ToString();
            if (!string.IsNullOrEmpty(st.Host)) txtHost.Text = st.Host;
            if (st.Port > 0) txtPort.Text = st.Port.ToString();

            _webReady = false;
            SetRunUi(true);
            SetStatus("服务运行中（上次会话恢复，PID=" + _server.Pid + "）", false);
            LogT("log.info.serverRunning", _server.Pid);
            LogT("log.info.cannotRetakeLog");

            // 异步探测监听端口：能连通则视为服务已就绪，恢复「打开网页」按钮
            ProbeWebReady(st.Host, st.Port);
        }

        /// <summary>后台探测 host:port 是否可连通（llama-server 就绪后才会监听）</summary>
        private void ProbeWebReady(string host, int port)
        {
            if (string.IsNullOrEmpty(host) || port <= 0) return;

            string probeHost = host == "0.0.0.0" || host == "::" || host == "*" || host == "+" ? "127.0.0.1" : host;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = false;
                try
                {
                    using (var client = new TcpClient())
                    {
                        var ar = client.BeginConnect(probeHost, port, null, null);
                        ok = ar.AsyncWaitHandle.WaitOne(1500);
                        if (ok)
                        {
                            try { client.EndConnect(ar); }
                            catch { ok = false; }
                        }
                    }
                }
                catch { ok = false; }

                if (!ok || IsDisposed) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!_server.IsRunning || _webReady) return;
                        _webReady = true;
                        ToolStripMenuItemOpenConsole.Enabled = true;
                        SetStatus("服务已就绪（恢复）  http://" + HostForDisplay() + ":" + _server.Port, false);
                    });
                }
                catch { /* 窗口关闭中 */ }
            });
        }

        // ==================== 界面事件 ====================

        private void toolStripButtonDownloadModels_Click(object sender, EventArgs e)
        {
            // 弹出独立窗口（用 using 确保 Dispose），避免重复打开
            using (var f = new ModelDownloadForm())
            {
                f.ShowDialog(this);
            }
        }

        private void toolStripButtonDownloadLlama_Click(object sender, EventArgs e)
        {
            // 弹出独立窗口，从 GitHub 拉取 ggerganov/llama.cpp 的 Release 列表
            using (var f = new llamaVersionForm())
            {
                f.ShowDialog(this);
                btnReload_Click(sender,e);
            }
        }

        /// <summary>
        /// 导入模型：选择其它位置存放模型的文件夹，把该文件夹路径作为引用写入 ref_models.conf，
        /// 之后扫描模型时会「先 models 目录、再引用目录」一并列出（不复制文件）。
        /// </summary>
        private void toolStripButtonImportModel_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择存放 .gguf 模型的文件夹（只记录引用，不复制文件）";
                dlg.ShowNewFolderButton = false;

                // 上次导入过的位置作为起始目录，方便再次选择
                try
                {
                    var refs = LlamaRuntime.GetModelReferences();
                    if (refs.Count > 0)
                    {
                        string last = refs[refs.Count - 1];
                        if (Directory.Exists(last))
                        {
                            dlg.SelectedPath = last;
                        }
                        else
                        {
                            string parent = Path.GetDirectoryName(last);
                            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                                dlg.SelectedPath = parent;
                        }
                    }
                }
                catch { /* 起始目录取不到不影响导入 */ }

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    string folder = dlg.SelectedPath;
                    var before = LlamaRuntime.GetModelReferences();
                    var after = LlamaRuntime.AddModelReferences(new[] { folder });
                    bool added = after.Count > before.Count;

                    if (added)
                    {
                        LogT("log.info.refModelFolder", folder);
                        LogT("log.info.refConf", LlamaRuntime.RefModelsConfPath);
                    }
                    else
                    {
                        // 未新增：该文件夹位于 models 目录内，或已在引用列表中
                        LogT("log.hint.refSkipped", folder);
                        MessageBox.Show(this,
                            "未新增引用：该文件夹位于 models 目录内\r\n（会被自动扫描），或已在引用列表中。",
                            "导入模型", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                    ReloadModels();
                }
                catch (Exception ex)
                {
                    LogT("log.err.importModel", ex.Message);
                    MessageBox.Show(this, "导入模型失败：" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 模型管理：弹窗管理全部模型（含软删除），支持修改备注、添加模型目录、
        /// 删除（进回收站）、软删除/取消软删除；窗口内有过改动则重新加载模型列表。
        /// </summary>
        private void toolStripButtonManageModels_Click(object sender, EventArgs e)
        {
            using (var f = new ModelManagerForm())
            {
                f.ShowDialog(this);
                if (f.Changed)
                {
                    ReloadModels(); btnReload_Click(sender, e);
                }
            }
        }

        /// <summary>关于窗口</summary>
        private void toolStripButtonAbout_Click(object sender, EventArgs e)
        {
            using (var f = new AboutForm())
            {
                f.ShowDialog(this);
            }
        }

        /// <summary>「设置」菜单：打开应用设置窗（自启动 / 自启后跑上次模型 / 自启后隐藏主窗口）</summary>
        private void ToolStripMenuItemSetting_Click(object sender, EventArgs e)
        {
            using (var f = new AppSettingsForm())
            {
                f.ShowDialog(this);
            }
        }

        // ==================== 参数方案 ====================

        /// <summary>程序填充下拉框中（不触发方案应用）</summary>
        private bool _loadingProfiles;

        /// <summary>「参数方案」下拉框第一项固定占位值：选中它时「保存方案」= 另存为新方案</summary>
        private static string ChooseProfileItem => LanguageManager.Instance.T("profile.choose");

        /// <summary>重新填充「参数方案」下拉框（保留当前选择；第一项固定为占位值）</summary>
        private void ReloadProfiles()
        {
            var combo = cmbProfiles.ComboBox;
            string keep = cmbProfiles.SelectedItem as string;

            _loadingProfiles = true;
            combo.BeginUpdate();
            try
            {
                combo.Items.Clear();
                combo.Items.Add(ChooseProfileItem);
                foreach (var p in ProfileStore.LoadProfiles()) combo.Items.Add(p.Name);
                if (combo.Items.Count > 0)
                {
                    int idx = 0;
                    if (keep != null && keep != ChooseProfileItem)
                    {
                        for (int i = 1; i < combo.Items.Count; i++)
                            if (string.Equals(combo.Items[i] as string, keep, StringComparison.Ordinal)) { idx = i; break; }
                    }
                    combo.SelectedIndex = idx;   // 只定初始显示，不应用（避免覆盖用户手改的参数）
                }
            }
            finally
            {
                combo.EndUpdate();
                _loadingProfiles = false;
            }
        }

        /// <summary>选中指定名称的方案行（不触发方案应用）；找不到则回到占位项</summary>
        private void SelectProfile(string name)
        {
            var combo = cmbProfiles.ComboBox;
            _loadingProfiles = true;
            try
            {
                int idx = 0;
                if (!string.IsNullOrEmpty(name))
                {
                    for (int i = 1; i < combo.Items.Count; i++)
                        if (string.Equals(combo.Items[i] as string, name, StringComparison.Ordinal)) { idx = i; break; }
                }
                if (combo.Items.Count > 0) combo.SelectedIndex = idx;
            }
            finally
            {
                _loadingProfiles = false;
            }
        }

        /// <summary>
        /// 更多参数：弹出全部可选启动参数列表（参数名称 / 参数值 / 参数描述），
        /// 双击或右击「修改值」编辑；值保存于 server_params.conf，启动时追加到命令行。
        /// </summary>
        private void btnMoreParams_Click(object sender, EventArgs e)
        {
            using (var f = new MoreParamsForm())
                f.ShowDialog(this);
        }

        /// <summary>主窗口当前参数打包（新建方案的默认值）</summary>
        private RunProfile CurrentParametersAsProfile()
        {
            long ctx; int npred, ngl, port;

            // 下拉框为「--默认--」时按模型解析默认值；非法输入回落到兜底值（与旧行为一致）
            if (IsParamDefaultText(cmbCtx.Text)) ctx = DefaultCtxForModel();
            else if (!long.TryParse((cmbCtx.Text ?? "").Trim(), out ctx) || ctx <= 0) ctx = 131072;

            if (IsParamDefaultText(cmbNPred.Text)) npred = DefaultNPredForModel();
            else if (!int.TryParse((cmbNPred.Text ?? "").Trim(), out npred) || npred <= 0) npred = 8192;

            int.TryParse((txtNgl.Text ?? "").Trim(), out ngl);
            int.TryParse((txtPort.Text ?? "").Trim(), out port);

            return new RunProfile
            {
                Name = "",
                Ctx = ctx > 0 ? ctx : 131072,
                NPred = npred > 0 ? npred : 8192,
                Ngl = ngl >= 0 ? ngl : 0,
                Host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text.Trim(),
                Port = port > 0 ? port : 6080
            };
        }

        /// <summary>
        /// 保存方案：下拉框选中「--请选择方案--」（或未选中）时为另存为 —— 输入新方案名保存；
        /// 否则为覆盖 —— 确认后用当前参数覆盖选中的方案。
        /// </summary>
        private void toolStripButtonSaveSlu_Click(object sender, EventArgs e)
        {
            string name = cmbProfiles.SelectedItem as string;
            bool overwrite = !string.IsNullOrEmpty(name) && name != ChooseProfileItem;

            if (!overwrite)
            {
                // 另存为：输入新方案名
                name = "";
                if (!InputBox.Show(this, "另存方案", "方案名称：", ref name)) return;
                name = (name ?? "").Trim();
                if (name.Length == 0)
                {
                    MessageBox.Show(this, "方案名称不能为空。", "保存方案",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (name == ChooseProfileItem)
                {
                    MessageBox.Show(this, "该名称是保留的占位值，请换一个名称。", "保存方案",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else
            {
                // 覆盖：确认后写入
                if (MessageBox.Show(this, "是否覆盖方案「" + name + "」？", "保存方案",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            try
            {
                var p = CurrentParametersAsProfile();
                p.Name = name;

                var profiles = ProfileStore.LoadProfiles();
                profiles.RemoveAll(x => string.Equals(x.Name, name, StringComparison.Ordinal));
                profiles.Add(p);
                ProfileStore.SaveProfiles(profiles);

                LogT(overwrite ? "log.info.overwriteProfile" : "log.info.saveProfile", name);

                ReloadProfiles();
                SelectProfile(name);   // 选回刚保存的方案
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存方案失败：" + ex.Message, "保存方案",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>选择参数方案：把方案参数填入下方各编辑框（服务运行中方案下拉已禁用）</summary>
        private void cmbProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loadingProfiles || _server.IsRunning) return;

            string name = cmbProfiles.SelectedItem as string;
            if (string.IsNullOrEmpty(name) || name == ChooseProfileItem) return;

            var p = ProfileStore.LoadProfiles().Find(x => x.Name == name);
            if (p == null) return;

            cmbCtx.Text = p.Ctx.ToString();
            cmbNPred.Text = p.NPred.ToString();
            txtNgl.Text = p.Ngl.ToString();
            txtHost.Text = p.Host;
            txtPort.Text = p.Port.ToString();
            LogT("log.info.applyProfile", p.Name);
        }

        /// <summary>管理参数方案：左侧方案名列表，右侧方案参数；关闭后刷新下拉框</summary>
        private void toolStripButtonManageProfiles_Click(object sender, EventArgs e)
        {
            using (var f = new ProfileManagerForm(CurrentParametersAsProfile()))
            {
                f.ShowDialog(this);
                if (f.Changed) ReloadProfiles();
            }
        }

        private void btnReload_Click(object sender, EventArgs e) => ReloadRuntime();

        /// <summary>
        /// 选择 llama 版本：弹出窗口以列表列出 llama-bin 下的版本（版本名 / 安装目录 / 是否使用中），
        /// 双击一个行或点「确定」完成选择，结果写入输入框（只显示版本名）。
        /// </summary>
        private void btnOpenChooseLlama_Click(object sender, EventArgs e)
        {
            if (_versions.Count == 0)
            {
                var r=MessageBox.Show(this,
                    "未检测到可用的 llama 版本。\r\n是否去管理窗口？\r\n", "选择 llama", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (r==DialogResult.Yes)
                {
                    toolStripButtonDownloadLlama_Click(sender, e);
                }
                return;
            }

            using (var f = new LlamaVersionPickerForm(_versions, _selectedVersion))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Selected == null) return;

                SelectVersion(f.Selected);
                ApplySelectedVersion();
            }
        }

        private void btnOpenModelsDir_Click(object sender, EventArgs e) => OpenDir(LlamaRuntime.ModelsDir);

        /// <summary>
        /// 选择模型：弹出窗口以 ListView 列出全部模型（名称/目录/类型/备注/日期时间），
        /// 双击一个行或点「确定」完成选择，结果写入编辑框（只显示名称）。
        /// </summary>
        private void buttonChooseRunModel_Click(object sender, EventArgs e)
        {
            if (_models.Count == 0)
            {
                var r=MessageBox.Show(this,"模型列表为空。\r\n是否去模型管理窗口？","选择模型", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (r == DialogResult.Yes)
                {
                    toolStripButtonManageModels_Click(sender, e);
                }
                
                return;
            }

            using (var f = new ModelPickerForm(_models, _selectedModel))
            {
                if (f.ShowDialog(this) != DialogResult.OK || f.Selected == null) return;

                _selectedModel = f.Selected;
                OnModelSelected();
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e) => txtLog.Clear();

        /// <summary>用默认浏览器打开 llama-server 的 Web 入口（http://主机:端口）</summary>
        private void btnOpenWeb_Click(object sender, EventArgs e)
        {
            // 按钮平时禁用，此处兜底：服务未输出监听地址前不允许打开
            if (!_webReady || !_server.IsRunning)
            {
                MessageBox.Show(this, "服务尚未就绪。请先启动服务，等待日志出现监听地址。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string host = HostForDisplay();
            if (!ushort.TryParse(txtPort.Text.Trim(), out ushort port) || port == 0)
            {
                MessageBox.Show(this, "端口必须是 1-65535 的整数", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPort.Focus();
                return;
            }

            string url = "http://" + host + ":" + port + "/";
            try
            {
                // UseShellExecute=true 时直接传 URL，系统用默认浏览器打开
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "打开浏览器失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================== 启动 / 停止 ====================

        private void btnStart_Click(object sender, EventArgs e)
        {
            // 启动/停止合并按钮：服务运行中点击即为停止
            if (_server.IsRunning)
            {
                StopService();
                return;
            }

            var model = _selectedModel;
            if (model == null)
            {
                MessageBox.Show(this, "请先选择一个 .gguf 模型", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!LlamaRuntime.ServerPresent)
            {
                MessageBox.Show(this, "未检测到 llama-server.exe，请先部署程序文件。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            long ctx; int npred, ngl, port; string host;
            if (!TryResolveCtx(out ctx)) return;
            if (!TryResolveNPred(out npred)) return;
            if (!TryParseNonNeg(txtNgl.Text, out ngl, "GPU 层数")) return;
            if (!ushort.TryParse(txtPort.Text.Trim(), out ushort portU16) || portU16 == 0)
            {
                MessageBox.Show(this, "端口必须是 1-65535 的整数", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPort.Focus();
                return;
            }
            port = portU16;
            host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text.Trim();

            // 多模态投影：以下拉框选中的文件为准（默认即自动匹配结果），兜底再自动检测一次
            string mmproj = null;
            if (chkMmproj.Checked && chkMmproj.Enabled)
            {
                mmproj = SelectedMmprojPath();
                if (string.IsNullOrEmpty(mmproj)) mmproj = LlamaRuntime.FindMmproj(model.FullPath);
            }

            try
            {
                _webReady = false;   // 新一次启动：等待日志确认监听地址后才允许打开网页

                // 「更多参数」窗口配置的可选参数（server_params.conf），原样追加到命令行末尾
                string extraArgs = ServerParams.BuildExtraArgs();
                if (extraArgs.Length > 0)
                    LogT("log.info.extraArgs", extraArgs);

                // 显示启动进度遮罩：每秒 +5，服务就绪后跳满 + 500ms 隐藏
                progressBar1.Value = 0;
                panelProgress.Visible = true;
                panelProgress.BringToFront();
                _progressTimer.Start();

                _server.Start(model, mmproj, ngl, ctx, npred, host, port, extraArgs);

                // 记录运行状态（run_state.conf + 内存）：关窗后重开窗口时恢复
                ServerManager.OnStarted(model, ctx, npred, ngl, host, port);

                SetRunUi(true);
                SetStatus("服务启动中…等待监听地址", false);
            }
            catch (Exception ex)
            {
                LogT("log.err.startFailed", ex.Message);
                MessageBox.Show(this, ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // 启动失败：立即隐藏进度遮罩
                _progressTimer.Stop();
                panelProgress.Visible = false;
            }
        }

        /// <summary>
        /// 停止服务（「启动/停止」合并按钮在服务运行中时触发）。
        /// Stop 会等待进程真实退出（最长数秒）并枚举残留进程，放后台线程避免卡住界面。
        /// </summary>
        private void StopService()
        {
            btnStart.Enabled = false;    // 停止中防重复点击（后台线程完成后由 SetRunUi 恢复）
            ToolStripMenuItemOpenConsole.Enabled = false;  // 停止服务 → 网页按钮立即不可点
            LogT("log.info.stopping");

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                int killed;
                Exception error = null;
                try { killed = _server.Stop(); }
                catch (Exception ex) { killed = 0; error = ex; }

                if (IsDisposed) return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (error != null)
                        {
                            LogT("log.err.stopFailed", error.Message);
                            // 服务仍在运行：按钮恢复可点（仍为「停止服务」）
                            btnStart.Enabled = _server.IsRunning;
                            // 服务仍在运行且此前已就绪 → 恢复网页按钮
                            ToolStripMenuItemOpenConsole.Enabled = _server.IsRunning && _webReady;
                            return;
                        }

                        // 0 表示本来就没有存活实例（含残留也没找到）
                        AppendLog(killed > 0
                            ? "[信息] 已停止 llama-server（含残留共 " + killed + " 个进程）"
                            : "[信息] 没有正在运行的 llama-server");

                        // 清除运行状态记录（含 run_state.conf）
                        ServerManager.OnStopped();

                        // 正常退出会由 ProcessExited 事件刷新 UI；此处再兜底同步一次状态
                        if (!_server.IsRunning)
                        {
                            SetRunUi(false);
                            SetStatus("服务已停止", false);
                        }
                    });
                }
                catch { /* 窗口关闭中 */ }
            });
        }

        // ==================== cmd 窗口运行（llama-cli 交互对话） ====================

        /// <summary>
        /// 「AI 助手（可操作沙盒内文件）」：打开 agent 聊天窗口（走 llama-server 的工具调用循环）。
        /// 与 cmd 窗口（llama-cli）不同：本窗口内的模型可以调用 list_directory / read_file 等工具。
        /// </summary>
        private void ToolStripMenuItemRunAgent_Click(object sender, EventArgs e)
        {
            AgentChatWebForm.ShowSingle();
        }

        /// <summary>
        /// 「AI助手（cli）」：新开控制台窗口运行 Ooor-cli.exe（连接本地 llama-server 的终端 Agent，
        /// 与本程序共用同一份服务进程，不额外加载模型）。
        /// 服务地址不用传：CLI 启动时自动读 run_state.conf 找到本机服务；
        /// --pause 为内部参数：CLI 启动失败（如服务未运行）时停住窗口显示原因，避免一闪而过。
        /// </summary>
        private void ToolStripMenuItemOpenCli_Click(object sender, EventArgs e)
        {
            string exe = Path.Combine(Application.StartupPath, "Ooor-cli.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, "找不到 Ooor-cli.exe（应与本程序同目录）：\r\n" + exe,
                    "AI 助手（cli）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_server.IsRunning &&
                MessageBox.Show(this,
                    "本地服务未运行，Ooor-cli 可能连不上模型。\r\n是否仍要打开？",
                    "AI 助手（cli）", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--pause",
                    UseShellExecute = true,                      // 控制台程序：Shell 启动会新开一个控制台窗口
                    WorkingDirectory = LlamaRuntime.ConfigRoot   // CLI 会把工作目录加入沙盒白名单
                };
                Process.Start(psi);
                LogT("log.info.cliOpened");
                SetStatus("已打开 AI 助手（cli）", false);
            }
            catch (Exception ex)
            {
                LogT("log.err.cliStart", ex.Message);
                MessageBox.Show(this, ex.Message, "AI 助手（cli）", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 「打开控制台AI助手（开放权限）」：与 ToolStripMenuItemOpenConsole 主按钮一样新开控制台跑 Ooor-cli，
        /// 但额外授予"全开"权限（--trust：模型可自行判断跳过高危确认），并在启动前把白名单目录与
        /// "建议把文件写到 ./" 一次性展示给用户，避免用户不清楚 AI 能动哪些目录、文件落到了哪里。
        /// </summary>
        private void ToolStripMenuItemOpenAllPer_Click(object sender, EventArgs e)
        {
            string exe = Path.Combine(Application.StartupPath, "Ooor-cli.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, "找不到 Ooor-cli.exe（应与本程序同目录）：\r\n" + exe,
                    "打开控制台AI助手（开放权限）", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_server.IsRunning &&
                MessageBox.Show(this,
                    "本地服务未运行，Ooor-cli 可能连不上模型。\r\n是否仍要打开？",
                    "打开控制台AI助手（开放权限）", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // 收集白名单：与 Ooor-cli 启动后实际白名单保持一致（内置 models/config + agent.conf 追加项）
            var rawRoots = new List<string>();
            try
            {
                var opt = OoorFunc.Core.AgentOptions.Default();
                if (opt != null && opt.AllowedRoots != null) rawRoots.AddRange(opt.AllowedRoots);
            }
            catch { /* 配置损坏也不影响主流程，下面 still 有兜底 */ }
            // 兜底：Default() 异常时仍把内置两个核心目录展示出来
            if (rawRoots.Count == 0)
            {
                if (!string.IsNullOrEmpty(LlamaRuntime.ModelsDir)) rawRoots.Add(LlamaRuntime.ModelsDir);
                if (!string.IsNullOrEmpty(LlamaRuntime.ConfigRoot)) rawRoots.Add(LlamaRuntime.ConfigRoot);
            }

            string cwd = LlamaRuntime.ConfigRoot;
            string tempDir;
            try { tempDir = Path.Combine(cwd, "temp"); }
            catch { tempDir = cwd; }

            // 去重 + 规范化展示路径
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normRoots = new List<string>();
            foreach (var r in rawRoots)
            {
                try
                {
                    string n = Path.GetFullPath(r).TrimEnd('\\', '/');
                    if (n.Length > 0 && seen.Add(n)) normRoots.Add(n);
                }
                catch { }
            }
            string rootText = normRoots.Count == 0
                ? "  （无）"
                : string.Join("\r\n", normRoots.ConvertAll(p => "  • " + p));

            string msg =
                "将以「开放权限」模式启动 Ooor-cli（控制台 AI 助手）：\r\n" +
                "  • 文件读写 / 删除：开\r\n" +
                "  • 执行命令：开\r\n" +
                "  • 联网工具（web_search / fetch_url）：开\r\n" +
                "  • 高危操作确认：跳过（模型可自行判断，无需每次 Y/N）\r\n\r\n" +
                "AI 仅能访问下面的白名单目录（沙盒外路径会被拒绝）：\r\n" +
                rootText + "\r\n\r\n" +
                "工作目录（./）： " + cwd + "\r\n" +
                "如写文件，请让 AI 用相对路径（如 ./xxx.txt），CLI 会自动落到该目录下的 temp 子目录：\r\n" +
                "  " + tempDir + "\r\n\r\n" +
                "是否现在打开控制台？";

            if (MessageBox.Show(this, msg, "打开控制台AI助手（开放权限）",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    // --trust 让模型跳过 Y/N 确认（"开放权限"的实质差异点）；
                    // --root 显式把 cwd 也加一份，避免 ShellExecute 下 Environment.CurrentDirectory 与 WorkingDirectory 不一致的边界情况；
                    // --pause 同默认入口：服务未就绪时停窗显示原因，不闪退。
                    Arguments = "--pause --trust --root \"" + cwd + "\"",
                    UseShellExecute = true,                    // 控制台程序：Shell 启动会新开一个控制台窗口
                    WorkingDirectory = cwd                     // 工作目录 = ConfigRoot，CLI 会自动把它加进沙盒
                };
                Process.Start(psi);
                LogT("log.info.cliFullPerm");
                SetStatus("已打开控制台AI助手（开放权限）", false);
            }
            catch (Exception ex)
            {
                LogT("log.err.cliStart", ex.Message);
                MessageBox.Show(this, ex.Message, "打开控制台AI助手（开放权限）",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 「cmd 窗口运行模型」：新开一个 cmd 窗口，用当前版本的 llama-cli 加载当前模型，
        /// 用户直接在窗口里输入问题与模型对话。
        /// 与「启动服务」互不影响：不占用端口、不接管进程、不写 run_state.conf，
        /// 关掉窗口即结束（主程序退出也不会杀掉它）。
        /// </summary>
        private void ToolStripMenuItemRunCli_Click(object sender, EventArgs e)
        {
            var model = _selectedModel;
            if (model == null)
            {
                MessageBox.Show(this, "请先选择一个 .gguf 模型", "cmd 窗口运行",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cliExe = LlamaRuntime.FindCliExe();
            if (cliExe == null)
            {
                MessageBox.Show(this,
                    "当前版本目录下没有 " + LlamaRuntime.CliExeName + "，无法命令行交互运行。\r\n" +
                    "请在「选择llama」窗口重新下载 / 部署完整发行包：\r\n" + LlamaRuntime.BaseDir,
                    "cmd 窗口运行", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            long ctx; int npred, ngl;
            if (!TryResolveCtx(out ctx)) return;
            if (!TryResolveNPred(out npred)) return;
            if (!TryParseNonNeg(txtNgl.Text, out ngl, "GPU 层数")) return;

            // 服务已在跑：再开一个 CLI 会加载第二份模型，内存/显存翻倍，先确认
            if (_server.IsRunning &&
                MessageBox.Show(this,
                    "本地服务正在运行，cmd 窗口会再加载一份模型（内存 / 显存占用翻倍）。是否继续？",
                    "cmd 窗口运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // 多模态投影：以下拉框选中的文件为准（默认即自动匹配结果），兜底再自动检测一次
            string mmproj = null;
            if (chkMmproj.Checked && chkMmproj.Enabled)
            {
                mmproj = SelectedMmprojPath();
                if (string.IsNullOrEmpty(mmproj)) mmproj = LlamaRuntime.FindMmproj(model.FullPath);
            }

            try
            {
                // 附加参数（server_params.conf）过滤掉 llama-cli 不支持的服务专用项
                string extra = ServerParams.BuildCliExtraArgs();
                if (extra.Length > 0) LogT("log.info.cmdExtraArgs", extra);

                string cmdLine = LlamaCli.Run(model, mmproj, ngl, ctx, npred, extra);

                LogT("log.info.cmdRunModel", cmdLine);
                LogT("log.info.cmdHint");
                SetStatus("已在 cmd 窗口运行模型（可对话）", false);
            }
            catch (Exception ex)
            {
                LogT("log.err.cmdStart", ex.Message);
                MessageBox.Show(this, ex.Message, "cmd 窗口运行", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnServerLine(string line)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    AppendLog(line);

                    // 日志出现监听地址（http://host:端口，端口与本次启动一致）→ 服务启动成功
                    if (!_webReady && IsListenUrlOfService(line))
                    {
                        _webReady = true;
                        ToolStripMenuItemOpenConsole.Enabled = true;
                        SetStatus("服务已就绪  http://" + HostForDisplay() + ":" + _server.Port, false);

                        // 进度条跳满 → 500ms 后隐藏 panelProgress
                        _progressTimer.Stop();
                        progressBar1.Value = 100;
                        var t = new System.Windows.Forms.Timer { Interval = 500 };
                        t.Tick += (s2, e2) =>
                        {
                            t.Stop();
                            t.Dispose();
                            if (!IsDisposed) panelProgress.Visible = false;
                        };
                        t.Start();
                    }
                });
            }
            catch { /* 窗口关闭中 */ }
        }

        /// <summary>
        /// 该行是否为本次服务的监听地址输出：URL 中的端口需与 _server.Port 一致，
        /// 避免把日志里其他地址（如模型下载地址）误判为服务就绪。
        /// </summary>
        private bool IsListenUrlOfService(string line)
        {
            if (string.IsNullOrEmpty(line)) return false;
            int port = _server.Port;
            if (port <= 0) return false;

            foreach (System.Text.RegularExpressions.Match m in ListenUrlRegex.Matches(line))
            {
                int got;
                if (int.TryParse(m.Groups[1].Value, out got) && got == port) return true;
            }
            return false;
        }

        /// <summary>用于展示/打开的主机名：监听 0.0.0.0 / :: 时本地访问用 127.0.0.1</summary>
        private string HostForDisplay()
        {
            string host = string.IsNullOrWhiteSpace(txtHost.Text) ? "127.0.0.1" : txtHost.Text.Trim();
            if (host == "0.0.0.0" || host == "::" || host == "*" || host == "+") return "127.0.0.1";
            return host;
        }

        private void OnServerExited(int code)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    // 服务退出（手动停止或自行退出）：清除运行状态记录
                    ServerManager.OnStopped();
                    SetRunUi(false);
                    SetStatus(code == 0 ? "服务已停止" : ("服务已退出 (code=" + code + ")"), code != 0);

                    // 进程退出：无论成败，立即停表隐藏进度遮罩
                    _progressTimer.Stop();
                    panelProgress.Visible = false;
                });
            }
            catch { /* 窗口关闭中 */ }
        }

        private void SetRunUi(bool running)
        {
            // 停止/未运行时清除「已就绪」标记，网页按钮回到不可点
            if (!running) _webReady = false;

            // 合并按钮：未运行时显示「启动服务」（需 llama-server 就位），运行中显示「停止服务」
            btnStart.Text = running ? LanguageManager.Instance.T("llama.stop") : LanguageManager.Instance.T("llama.start");
            btnStart.Enabled = running || LlamaRuntime.ServerPresent;
            ToolStripMenuItemOpenConsole.Enabled = running && _webReady;
            txtLlamaDir.Enabled = !running;
            btnOpenChooseLlama.Enabled = !running;
            txtModel.Enabled = !running;
            buttonChooseRunModel.Enabled = !running;
            //toolStripButtonRefresh.Enabled = !running;
            cmbProfiles.Enabled = !running;                 // 参数方案只影响未启动时的编辑框
   
            cmbCtx.Enabled = !running;
            cmbNPred.Enabled = !running;
            txtNgl.Enabled = !running;
            txtHost.Enabled = !running;
            txtPort.Enabled = !running;
            btnMoreParams.Enabled = !running;
            // 多模态投影：运行中不可改；自动匹配结果缓存在 _autoMmproj，避免每次刷新都扫盘
            chkMmproj.Enabled = !running && !string.IsNullOrEmpty(_autoMmproj);
            cmbMmproj.Enabled = chkMmproj.Enabled;
            cmbMmproj.Visible = chkMmproj.Checked && !string.IsNullOrEmpty(_autoMmproj);
        }

        // ==================== 小工具 ====================

        /// <summary>按版本名在当前版本列表中查找（忽略大小写与首尾空白）；找不到返回 null</summary>
        private VersionInfo FindVersion(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _versions.Find(v => string.Equals(v.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>按完整路径在模型列表中查找（忽略大小写与首尾空白）；找不到返回 null</summary>
        private static ModelInfo FindModel(List<ModelInfo> models, string fullPath)
        {
            if (models == null || string.IsNullOrWhiteSpace(fullPath)) return null;
            return models.Find(m => string.Equals(m.FullPath, fullPath.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private bool TryParsePositive(string text, out long value, string fieldName)
        {
            if (long.TryParse((text ?? "").Trim(), out value) && value > 0) return true;
            MessageBox.Show(this, fieldName + "必须是正整数", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private bool TryParseNonNeg(string text, out int value, string fieldName)
        {
            if (int.TryParse((text ?? "").Trim(), out value) && value >= 0) return true;
            MessageBox.Show(this, fieldName + "必须是 ≥0 的整数", "参数错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        private void AppendLog(string line)
        {
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);

            // 防止超长日志吃掉内存：超过 40 万字符截掉头部一半
            if (txtLog.TextLength > 400000)
            {
                txtLog.SelectionStart = 0;
                txtLog.SelectionLength = 200000;
                txtLog.SelectedText = "";
            }
        }

        /// <summary>写一条可翻译的日志（key 对应 Lang/*.json 的 log.* 条目，支持 {0} 占位）</summary>
        private void LogT(string key, params object[] args)
        {
            string tpl = LanguageManager.Instance.T(key);
            AppendLog(args.Length == 0 ? tpl : string.Format(tpl, args));
        }

        private void SetStatus(string text, bool error)
        {
            lblStatus.Text = text;
            lblStatus.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
        }

        private void OpenDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void toolStripButtonDownloadForm_Click(object sender, EventArgs e)
        {
            FileDownloadForm.ShowSingle();
        }

        private void ToolStripMenuItemCopyApiAddr_Click(object sender, EventArgs e)
        {
            // OpenAPI（OpenAI 兼容）地址 = http://{主机}:{端口}/v1
            // 主机/端口取界面配置（0.0.0.0 等通配地址按 127.0.0.1 展示），与「打开网页」同口径
            string host = HostForDisplay();
            string portText = (txtPort.Text ?? "").Trim();
            // 空端口与启动逻辑一致回退默认 6080；只有非数字/超范围才报错
            if (!ushort.TryParse(portText, out ushort port))
            {
                if (portText.Length == 0) port = 6080;
                else
                {
                    MessageBox.Show(this, "端口必须是 1-65535 的整数。", "参数错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtPort.Focus();
                    return;
                }
            }

            string addr = "http://" + host + ":" + port + "/v1";

            // 取启动参数里配置的 --api-key（调用方需带 Authorization: Bearer <key>）
            string apiKey = null;
            try
            {
                var values = ServerParams.LoadValues();
                if (values.TryGetValue("--api-key", out var k) && !string.IsNullOrWhiteSpace(k))
                    apiKey = k.Trim();
            }
            catch { }

            ViewUrlDialogForm.Show(this, addr, null,
                "OpenAPI 地址（OpenAI 兼容接口）",
                "本服务提供 OpenAI 兼容接口（/v1/chat/completions、/v1/models 等），\n"
                + "把下面的基础地址填入支持自定义 OpenAI 端点的客户端即可：",
                apiKey);
        }

        private void ToolStripMenuItemOpenConsoleUseAgent_Click(object sender, EventArgs e)
        {
            new ConsoleChatRecord().Show(this);
        }

        private void toolStripSplitButton3_ButtonClick(object sender, EventArgs e)
        {
            new AgentManager().Show(this);
        }

        private void ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // 管理控制台对话session
            new ConsoleChatRecord().Show(this);
        }

        private void ToolStripMenuItemDownloadNewLlama_Click(object sender, EventArgs e)
        {
            new LlamaDownloadForm().Show(this);
        }

     
    }
}
