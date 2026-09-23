using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 模型市场窗口（对接真实 HuggingFace 生态，替换原占位实现）：
    ///   - 顶部选择下载源：两个选项「hf-mirror 直连」（默认，开箱即用）
    ///     或「HuggingFace 官方直连」（海外线路）；本窗口已不提供 ooor 加速源选项
    ///   - 窗口打开即自动载入「趋势榜」：不带关键词按 HF 热度列出最流行的仓库
    ///     （等同官网 models?apps=llama.cpp&amp;sort=trending），可点「趋势榜」按钮随时重载
    ///   - 搜索框调 HF 模型搜索（按下载量降序），上方表格列出仓库
    ///   - 选中仓库自动加载文件列表（.gguf / mmproj-*.gguf，含大小与「适配硬件」建议）
    ///   - 点击「下载」先弹出对话框确认保存位置（默认目录 / 另存为），
    ///     提交后任务进入 DownloadManager 后台队列，在「下载任务管理器」窗口查看进度
    ///   - gated 仓库（Llama 官方系等）禁止下载（版权红线）
    /// </summary>
    public partial class ModelDownloadForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;
        private const string DirectMirrorBase = "https://hf-mirror.com";
        private const string HuggingFaceBase = "https://huggingface.co";
        private const int SearchLimit = 30;

        private static readonly OoorSettings _ooor = OoorSettings.Load();

        private readonly List<HfModel> _models = new List<HfModel>();
        private readonly List<HfFile> _files = new List<HfFile>();

        /// <summary>仓库列表请求（趋势榜/搜索）的取消源</summary>
        private CancellationTokenSource _cts;

        /// <summary>文件列表请求的取消源：切换仓库时取消上一次，避免旧结果回填到新列表</summary>
        private CancellationTokenSource _filesCts;

        /// <summary>仓库列表是否正在刷新（刷新期间忽略行选中事件）</summary>
        private bool _loading;

        /// <summary>代码内部主动改选中态时置位：屏蔽 SelectionChanged，避免重复发文件列表请求</summary>
        private bool _suppressSelection;

        /// <summary>当前是否走 hf-mirror 直连（默认）</summary>
        private bool IsHfMirror => cmbSource.SelectedIndex == 0;

        /// <summary>当前是否走 HuggingFace 官方源</summary>
        private bool IsHuggingFace => cmbSource.SelectedIndex == 1;

        /// <summary>当前请求 base（hf-mirror 或 HuggingFace 官方）</summary>
        private string BaseUrl => IsHuggingFace ? HuggingFaceBase : DirectMirrorBase;

        private string BearerToken => "";

        public ModelDownloadForm()
        {
            InitializeComponent();

            // 源默认值：默认走 hf-mirror 直连（开箱即用，不依赖网关配置），需要在
            // 海外线路直接访问 HF 时在顶部手动切换到「huggingface」
            cmbSource.Items.AddRange(new object[] { "hf-mirror", "huggingface" });
            cmbSource.SelectedIndex = 0;
            UpdateSourceHint();

            // 模型表格右键菜单：在浏览器打开模型所在页面
            var ctxModels = new System.Windows.Forms.ContextMenuStrip();
            var miOpenPage = new System.Windows.Forms.ToolStripMenuItem(L.T("mdl.menu.openPage"));
            _miOpenPage = miOpenPage;
            miOpenPage.Click += (s, e) => OpenModelPageInBrowser();
            ctxModels.Items.Add(miOpenPage);

            var miRefreshFiles = new System.Windows.Forms.ToolStripMenuItem(L.T("mdl.menu.refreshFiles"));
            _miRefreshFiles = miRefreshFiles;
            miRefreshFiles.Click += async (s, e) => await ClearCacheAndRefreshFilesAsync();
            ctxModels.Items.Add(miRefreshFiles);

            // 两项都作用于当前选中行，没有选中行时置灰（避免点了没反应）
            ctxModels.Opening += (s, e) =>
            {
                bool has = dgvModels.CurrentRow != null && dgvModels.CurrentRow.Index < _models.Count;
                miOpenPage.Enabled = has;
                miRefreshFiles.Enabled = has;
            };
            dgvModels.ContextMenuStrip = ctxModels;
            // 右击时先选中所在行（默认右击不改变选中，菜单操作对象会错位）
            dgvModels.CellMouseDown += dgvModels_CellMouseDown;

            // 「仅 GGUF」过滤开关（对应官网 ?apps=llama.cpp，默认开启）
            _chkGgufOnly = new System.Windows.Forms.CheckBox
            {
                Text = L.T("mdl.chk.ggufOnly"),
                Checked = true,
                AutoSize = true,
                Location = new System.Drawing.Point(902, 15),
                Name = "chkGgufOnly"
            };
            // TODO: 加入 splitContainer.Panel1（新版 ToolStrip 布局还没决定放哪，先不显示）
            new System.Windows.Forms.ToolTip().SetToolTip(_chkGgufOnly, L.T("mdl.tip.ggufOnly"));

            // 「趋势榜」按钮：一键回到最流行的模型列表（与官网 sort=trending 同源）
            _btnTrending = new System.Windows.Forms.Button
            {
                Text = L.T("mdl.btn.trending"),
                Location = new System.Drawing.Point(1000, 11),
                Size = new System.Drawing.Size(104, 31),
                UseVisualStyleBackColor = true,
                Name = "btnTrending",
                TabIndex = 6
            };
            _btnTrending.Click += async (s, e) => await LoadTrendingAsync();
            // TODO: 加入 splitContainer.Panel1（新版 ToolStrip 布局还没决定放哪，先不显示）
            new System.Windows.Forms.ToolTip().SetToolTip(_btnTrending, L.T("mdl.tip.trending"));

            // 加载遮罩：拉取期间盖住对应列表显示等待提示，完成后隐藏
            _lblLoading = CreateLoadingOverlay(splitContainer.Panel1, dgvModels, "lblLoading");
            _lblFilesLoading = CreateLoadingOverlay(splitContainer.Panel2, dgvFiles, "lblFilesLoading");
            splitContainer.SplitterMoved += (s, e) => SyncLoadingBounds();

            // 窗口打开即载入趋势榜：不必先输关键词，直接看到热门可运行模型
            Load += async (s, e) => await LoadTrendingAsync();
        }

        /// <summary>按当前语言刷新窗口自身静态文本（标题、列头、按钮、菜单项等）</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("mdl.title");

            // 工具栏搜索按钮
            toolStripButton2.Text = L.T("mdl.btn.search");

            // 动态创建的控件
            if (_chkGgufOnly != null) _chkGgufOnly.Text = L.T("mdl.chk.ggufOnly");
            if (_btnTrending != null) _btnTrending.Text = L.T("mdl.btn.trending");
            if (_miOpenPage != null) _miOpenPage.Text = L.T("mdl.menu.openPage");
            if (_miRefreshFiles != null) _miRefreshFiles.Text = L.T("mdl.menu.refreshFiles");

            // 列头
            colRepoId.HeaderText = L.T("mdl.col.repo");
            colDownloads.HeaderText = L.T("mdl.col.downloads");
            colLikes.HeaderText = L.T("mdl.col.likes");
            colTask.HeaderText = L.T("mdl.col.task");
            colGated.HeaderText = L.T("mdl.col.gated");
            colUpdated.HeaderText = L.T("mdl.col.updated");
            colFileName.HeaderText = L.T("mdl.col.file");
            colFileSize.HeaderText = L.T("mdl.col.size");
            colFileType.HeaderText = L.T("mdl.col.type");
            colHardware.HeaderText = L.T("mdl.col.hardware");
            colFileAction.HeaderText = L.T("mdl.col.action");
            colFileAction.Text = L.T("mdl.btn.download");
        }

        /// <summary>「仅 GGUF」复选框（构造函数中动态创建）</summary>
        private readonly System.Windows.Forms.CheckBox _chkGgufOnly;

        /// <summary>「趋势榜」按钮（构造函数中动态创建）</summary>
        private readonly System.Windows.Forms.Button _btnTrending;

        /// <summary>右键菜单项（构造函数中动态创建）</summary>
        private readonly System.Windows.Forms.ToolStripMenuItem _miOpenPage;
        private readonly System.Windows.Forms.ToolStripMenuItem _miRefreshFiles;

        /// <summary>「请稍等」加载遮罩（覆盖仓库列表，构造函数中动态创建）</summary>
        private readonly System.Windows.Forms.Label _lblLoading;

        /// <summary>「拉取中」加载遮罩（覆盖文件列表，构造函数中动态创建）</summary>
        private readonly System.Windows.Forms.Label _lblFilesLoading;

        // ==================== 加载遮罩 ====================

        /// <summary>
        /// 在列表所在面板上建一个等待遮罩，并让它始终与该列表同位置同大小。
        /// 注意不能用 Dock=Fill——同容器里两个 Fill 控件会互相抢布局，把列表压成 0 高；
        /// 这里改为跟随列表 Bounds，列表自身布局完全不受影响。
        /// </summary>
        private System.Windows.Forms.Label CreateLoadingOverlay(Control panel, Control list, string name)
        {
            var lbl = new System.Windows.Forms.Label
            {
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Text = L.T("mdl.loading.wait"),
                Font = new System.Drawing.Font(Font.FontFamily, 13F),
                ForeColor = System.Drawing.Color.FromArgb(96, 96, 96),
                BackColor = System.Drawing.Color.FromArgb(248, 248, 248),
                Visible = false,
                Name = name
            };
            panel.Controls.Add(lbl);
            lbl.BringToFront();
            // 列表尺寸/位置变化时遮罩跟着走（两者同一父容器，Bounds 可直接复用）
            EventHandler sync = (s, e) => lbl.Bounds = list.Bounds;
            list.SizeChanged += sync;
            list.LocationChanged += sync;
            lbl.Bounds = list.Bounds;
            return lbl;
        }

        /// <summary>两个遮罩各自与对应列表对齐</summary>
        private void SyncLoadingBounds()
        {
            if (_lblLoading != null) _lblLoading.Bounds = dgvModels.Bounds;
            if (_lblFilesLoading != null) _lblFilesLoading.Bounds = dgvFiles.Bounds;
        }

        private static void ShowOverlay(System.Windows.Forms.Label lbl, Control list, string text)
        {
            lbl.Bounds = list.Bounds;
            lbl.Text = text;
            lbl.Visible = true;
            lbl.BringToFront();
            lbl.Update();   // 立刻绘制，避免 await 前不刷新
        }

        /// <summary>盖住仓库列表显示等待提示（拉取期间列表为空，遮罩让状态一目了然）</summary>
        private void ShowLoading(string text) => ShowOverlay(_lblLoading, dgvModels, text);

        private void HideLoading() => _lblLoading.Visible = false;

        /// <summary>盖住文件列表显示「拉取中」（切换仓库、拉取 gguf 清单期间）</summary>
        private void ShowFilesLoading(string text) => ShowOverlay(_lblFilesLoading, dgvFiles, text);

        private void HideFilesLoading() => _lblFilesLoading.Visible = false;

        // ==================== 右键菜单 ====================

        private void dgvModels_CellMouseDown(object sender, System.Windows.Forms.DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Right) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            try { dgvModels.CurrentCell = dgvModels.Rows[e.RowIndex].Cells[e.ColumnIndex]; }
            catch { /* 单元格不可设为当前时忽略 */ }
        }

        /// <summary>用系统默认浏览器打开模型仓库页面（huggingface.co/{owner}/{repo}）</summary>
        private void OpenModelPageInBrowser()
        {
            int idx = dgvModels.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _models.Count) return;

            string repoId = _models[idx].id;
            if (string.IsNullOrEmpty(repoId))
            {
                lblStatus.Text = L.T("mdl.status.noRepoId");
                return;
            }

            string url = "https://huggingface.co/" + repoId.Trim('/');
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                lblStatus.Text = string.Format(L.T("mdl.status.openedPage"), url);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mdl.status.openLinkFail"), ex.Message), L.T("mdl.msg.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 清除当前仓库的本地缓存并重新拉取文件列表（右键菜单）。
        /// 文件列表缓存有效期 30 分钟：上游新增的 GGUF、刚被 ooor 导入的文件的缓存标记
        /// 命中缓存时都看不到，必须清掉重拉。
        /// </summary>
        private async Task ClearCacheAndRefreshFilesAsync()
        {
            int idx = dgvModels.CurrentRow?.Index ?? -1;
            if (idx < 0 || idx >= _models.Count)
            {
                lblStatus.Text = L.T("mdl.status.selectRepoFirst");
                return;
            }

            var model = _models[idx];
            int removed = HfApi.ClearFilesCache(BaseUrl, false, model.id);
            lblStatus.Text = string.Format(L.T("mdl.status.cacheCleared"), model.id, removed);

            // 复用常规加载流程：gated 仓库仍只给提示不发请求，遮罩/取消逻辑也一致
            await LoadFilesForRowAsync(idx);
        }

        // ==================== 源切换与设置 ====================

        private void cmbSource_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSourceHint();
            // 换源后清空文件列表（hf-mirror 与 huggingface 仓库列表缓存键不同，避免误回填）
            dgvFiles.Rows.Clear();
            _files.Clear();
        }

        private void UpdateSourceHint()
        {
            if (IsHuggingFace)
                lblStatus.Text = L.T("mdl.status.sourceHf");
            else
                lblStatus.Text = L.T("mdl.status.sourceMirror");
        }

        private void btnOoorSettings_Click(object sender, EventArgs e)
        {
            using (var dlg = new OoorSettingsForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // 重新读取设置（OoorSettingsForm 已保存）
                    var fresh = OoorSettings.Load();
                    _ooor.ServerUrl = fresh.ServerUrl;
                    _ooor.Token = fresh.Token;
                    UpdateSourceHint();
                }
            }
        }

        // ==================== 搜索 ====================

        private async void btnSearch_Click(object sender, EventArgs e) => await SearchAsync();

        private async void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        }

        /// <summary>关键词搜索；搜索框留空时等同重新载入趋势榜（不再提示输入关键词）。</summary>
        private async Task SearchAsync() => await FetchModelsAsync(txtSearch.Text.Trim());

        /// <summary>载入趋势榜：窗口打开即调用，直接显示当前最流行的可运行模型。</summary>
        private async Task LoadTrendingAsync() => await FetchModelsAsync(null);

        /// <summary>
        /// 取回模型列表并填表。
        /// keyword 为空 → 趋势榜（HF 热度，等价官网 models?apps=llama.cpp&amp;sort=trending）；
        /// keyword 非空 → 关键词搜索（按累计下载量降序）。
        /// </summary>
        private async Task FetchModelsAsync(string keyword)
        {
            if (_loading) return;

            bool trending = string.IsNullOrEmpty(keyword);

            _loading = true;
            // 仓库搜索/趋势榜 3 秒硬超时：用户看到进度条转太久不至于干等，
            // 超时自动取消并提示"切源/检查网络"。文件列表拿另外的超时策略（慢一些没事）。
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            _cts = cts;
            _filesCts?.Cancel();      // 仓库列表要换，上一批文件请求作废
            toolStripButton2.Enabled = false;   // 拉取期间禁用工具栏搜索按钮（避免连点导致重复请求）
            HideFilesLoading();
        
            lblStatus.Text = trending
                ? L.T("mdl.status.loadingTrending")
                : string.Format(L.T("mdl.status.searching"), keyword);
            ShowLoading(trending ? L.T("mdl.loading.trending") : L.T("mdl.loading.search"));
            dgvModels.Rows.Clear();
            dgvFiles.Rows.Clear();
            _models.Clear();
            _files.Clear();

            try
            {
                // 空关键词由 HfApi 内部转为趋势榜请求
                var models = await HfApi.SearchAsync(BaseUrl, false, keyword ?? "", SearchLimit, BearerToken,
                    cts.Token, _chkGgufOnly != null && _chkGgufOnly.Checked);
                foreach (var m in models)
                {
                    _models.Add(m);
                    int row = dgvModels.Rows.Add(
                        m.id ?? "",
                        FormatCount(m.downloads),
                        FormatCount(m.likes),
                        string.IsNullOrEmpty(m.pipeline_tag) ? "-" : m.pipeline_tag,
                        m.IsGated ? L.T("mdl.cell.gated") : "",
                        FormatDate(m.lastModified));
                    // gated 行灰显：不可下载（版权红线）
                    if (m.IsGated)
                    {
                        dgvModels.Rows[row].DefaultCellStyle.ForeColor = System.Drawing.Color.Gray;
                        foreach (DataGridViewCell cell in dgvModels.Rows[row].Cells)
                            cell.ToolTipText = L.T("mdl.gated.tip");
                    }
                }
                lblStatus.Text = trending
                    ? string.Format(L.T("mdl.status.trendingDone"), models.Count, SourceName())
                    : string.Format(L.T("mdl.status.searchDone"), models.Count, SourceName());
            }
            catch (TaskCanceledException) when (cts.IsCancellationRequested)
            {
                // 3 秒硬超时触发：明确告诉用户超时 + 建议切换到另一个源再试
                lblStatus.Text = trending
                    ? L.T("mdl.status.trendingTimeout")
                    : string.Format(L.T("mdl.status.searchTimeout"), keyword);
            }
            catch (TaskCanceledException)
            {
                // 窗口关闭等外部取消：保持原来"已取消"提示
                lblStatus.Text = L.T("mdl.status.cancelled");
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format(
                    trending ? L.T("mdl.status.trendingFail") : L.T("mdl.status.searchFail"),
                    ex.Message, SourceName());
            }
            finally
            {
                _loading = false;
                toolStripButton2.Enabled = true;
                HideLoading();
            }

            // 列表就绪后默认选中第一条仓库：随即自动展开它的 GGUF 文件列表，省一次点击
            await SelectFirstModelRowAsync();
        }

        /// <summary>
        /// 默认选中第一行仓库并加载它的文件列表。
        /// 注意不能只靠「设置 CurrentCell 触发 SelectionChanged」——填表时 DataGridView 已经把
        /// 第一行自动设为当前行，这里再设同一个单元格不会引发 SelectionChanged，文件列表就会一直空着；
        /// 所以选中过程用 _suppressSelection 屏蔽事件，选中后显式调用一次加载。
        /// </summary>
        private async Task SelectFirstModelRowAsync()
        {
            if (_models.Count == 0 || dgvModels.Rows.Count == 0) return;

            _suppressSelection = true;
            try
            {
                dgvModels.ClearSelection();
                dgvModels.Rows[0].Selected = true;
                dgvModels.CurrentCell = dgvModels.Rows[0].Cells[0];
            }
            catch { /* 单元格不可设为当前时忽略 */ }
            finally
            {
                _suppressSelection = false;
            }

            // 直接按索引加载：不依赖 CurrentRow 是否已生效（窗口首次显示前设置 CurrentCell 可能被系统忽略）
            await LoadFilesForRowAsync(0);
        }

        // ==================== 文件列表 ====================

        private async void dgvModels_SelectionChanged(object sender, EventArgs e)
        {
            if (_loading || _suppressSelection) return;
            await LoadFilesForRowAsync(dgvModels.CurrentRow?.Index ?? -1);
        }

        /// <summary>加载指定行仓库的文件列表（gated 仓库只给提示，不发请求）</summary>
        private async Task LoadFilesForRowAsync(int idx)
        {
            if (idx < 0 || idx >= _models.Count) return;

            var model = _models[idx];
            if (model.IsGated)
            {
                dgvFiles.Rows.Clear();
                _files.Clear();
                lblStatus.Text = L.T("mdl.status.gated");
                return;
            }

            await LoadFilesAsync(model);
        }

        private async Task LoadFilesAsync(HfModel model)
        {
            // 切换仓库时取消上一个未完成的请求：旧结果不再回填到新列表
            _filesCts?.Cancel();
            var cts = new CancellationTokenSource();
            _filesCts = cts;

            lblStatus.Text = string.Format(L.T("mdl.status.loadingFiles"), model.id);
            ShowFilesLoading(L.T("mdl.loading.files"));
            dgvFiles.Rows.Clear();
            _files.Clear();
            bool hasSubDir = false;

            try
            {
                var files = await HfApi.ListFilesAsync(BaseUrl, false, model.id, BearerToken, cts.Token);
                if (cts.IsCancellationRequested) return;   // 期间已切到别的仓库，丢弃结果
                foreach (var f in files)
                {
                    if (!f.IsFile) continue;
                    if (!f.IsGGUF) continue;   // 只列 .gguf（含 mmproj-*.gguf）
                    _files.Add(f);
                    if (f.path != null && f.path.IndexOf('/') >= 0) hasSubDir = true;
                    // 类型列带上分片信息：大仓按量化方案分目录（UD-Q4_K_XL/xxx-00001-of-00004.gguf），
                    // 分片必须整组下载才能加载，标明「第几片/共几片」避免只下一片
                    string typeLabel = f.IsMmproj ? L.T("mdl.cell.mmproj") : L.T("mdl.cell.model");
                    string shard = ShardLabel(f.path);
                    if (shard != null) typeLabel = string.Format(L.T("mdl.cell.modelShard"), shard);
                    // 「适配硬件」列：按参数量（文件名/仓库名）＋模型体积给出硬件建议，悬停显示依据
                    string hardTip;
                    string hardware = HardwareLabel(f, model.id, files, out hardTip);
                    if (f.cached) hardTip += L.T("mdl.hw.cached");
                    int newRow = dgvFiles.Rows.Add(
                        f.path,
                        FormatSize(f.size),
                        typeLabel,
                        hardware,
                        L.T("mdl.btn.download"));
                    dgvFiles.Rows[newRow].Cells[colHardware.Index].ToolTipText = hardTip;
                }
                bool hasGguf = _files.Count > 0;
                lblStatus.Text = hasGguf
                    ? string.Format(L.T("mdl.status.filesFound"), _files.Count,
                        hasSubDir ? L.T("mdl.status.filesHasSubdir") : "")
                    : L.T("mdl.status.noGguf");
            }
            catch (TaskCanceledException)
            {
                // 被切换仓库/关闭窗口取消：界面已由新请求或关闭流程接管，这里不再改状态栏
            }
            catch (Exception ex)
            {
                if (!cts.IsCancellationRequested)
                    lblStatus.Text = string.Format(L.T("mdl.status.filesFail"), ex.Message);
            }
            finally
            {
                // 只有自己仍是最新请求时才收起遮罩，否则会把新请求的遮罩一起关掉
                if (_filesCts != null && _filesCts.Token.Equals(cts.Token))
                    HideFilesLoading();
            }
        }

        // ==================== 适配硬件建议 ====================

        /// <summary>1 GB 的字节数（与 FormatSize 同口径，按 1024 进制）</summary>
        private const long BytesPerGB = 1024L * 1024 * 1024;

        /// <summary>MoE 命名（8x7B = 8 个专家 × 7B），按总参数量计</summary>
        private static readonly System.Text.RegularExpressions.Regex MoEParamRegex =
            new System.Text.RegularExpressions.Regex(
                @"(\d+)x(\d+(?:\.\d+)?)b(?![a-z0-9])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>十亿参数量命名：8B / 1.5B / 0.6B</summary>
        private static readonly System.Text.RegularExpressions.Regex BillionParamRegex =
            new System.Text.RegularExpressions.Regex(
                @"(\d+(?:\.\d+)?)b(?![a-z0-9])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>百万参数量命名：360M / 135M（按 0.36B 计入）</summary>
        private static readonly System.Text.RegularExpressions.Regex MillionParamRegex =
            new System.Text.RegularExpressions.Regex(
                @"(\d+(?:\.\d+)?)m(?![a-z0-9])",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        /// 从文件名/仓库名识别参数量（单位：十亿），识别不到返回 null。
        /// 例：Qwen3-8B-Q4_K_M.gguf → 8，Mixtral-8x7B → 56，Qwen2.5-0.5B → 0.5，Qwen2-360M → 0.36。
        /// 结尾的 (?![a-z0-9]) 用于排除 Q4_K_M、4bit、f16 这类同字母结尾但不是参数量的标记。
        /// </summary>
        private static double? ParseParamBillion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var moe = MoEParamRegex.Match(text);
            if (moe.Success)
            {
                double experts, each;
                if (double.TryParse(moe.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out experts)
                    && double.TryParse(moe.Groups[2].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out each))
                    return experts * each;
            }

            var billions = BillionParamRegex.Match(text);
            if (billions.Success)
            {
                double n;
                if (double.TryParse(billions.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out n))
                    return n;
            }

            var millions = MillionParamRegex.Match(text);
            if (millions.Success)
            {
                double n;
                if (double.TryParse(millions.Groups[1].Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out n))
                    return n / 1000.0;
            }

            return null;
        }

        /// <summary>
        /// 模型体积：分片模型取同目录下所有分片之和（只算单片体积会严重低估显存需求），
        /// 非分片文件取自身大小（mmproj 投影文件本身也按自身大小算）。
        /// </summary>
        private static long ModelSizeBytes(HfFile file, List<HfFile> all)
        {
            if (file.size > 0 && ShardLabel(file.path) == null) return file.size;
            if (string.IsNullOrEmpty(file.path)) return file.size;   // 无路径信息，不做分片归组

            string dir = DirOf(file.path);
            long total = 0;
            foreach (var f in all)
            {
                if (f == null || ShardLabel(f.path) == null) continue;
                if (!string.Equals(DirOf(f.path), dir, StringComparison.OrdinalIgnoreCase)) continue;
                total += f.size;
            }
            return total > 0 ? total : file.size;
        }

        /// <summary>取所在目录（文件名没有目录部分时返回空串；空路径直接返回空串，避免 GetDirectoryName 抛异常）</summary>
        private static string DirOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return System.IO.Path.GetDirectoryName(path) ?? "";
        }

        /// <summary>
        /// 「适配硬件」建议文本：
        ///   3B 及以下 → 一般 CPU 就能跑；
        ///   3B 以上 ~ 7B → 多核心 CPU 勉强跑，建议上 4G 独显；
        ///   7B 以上 → 按模型体积给显存档位：≤4G → 4G 独显，4G ~ 16G → 16G 独显，>16G → 高性能高显存独显＋高内存。
        /// 参数量识别不到时（文件名与仓库名都没有「8B」这类标记）直接按体积判断：
        /// 体积 16G 以上的 GGUF 必然是大模型。
        /// </summary>
        private static string HardwareAdvice(double? paramB, long bytes)
        {
            if (paramB.HasValue)
            {
                if (paramB.Value <= 3) return L.T("mdl.hw.cpu");
                if (paramB.Value <= 7) return L.T("mdl.hw.cpuMulti");
            }

            if (bytes <= 0) return "";
            if (bytes <= 4 * BytesPerGB) return L.T("mdl.hw.4g");
            if (bytes <= 16 * BytesPerGB) return L.T("mdl.hw.16g");
            return L.T("mdl.hw.high");
        }

        /// <summary>
        /// 「适配硬件」列文本；tip 输出判断依据（鼠标悬停显示），让用户知道结论是怎么来的。
        /// 多模态投影文件（mmproj-*.gguf）只是视觉编码器，不能单独运行、不代表主模型的硬件需求，
        /// 显示「--」不参与适配判断。
        /// </summary>
        private static string HardwareLabel(HfFile file, string repoId, List<HfFile> all, out string tip)
        {
            if (file.IsMmproj)
            {
                tip = L.T("mdl.hw.mmprojTip");
                return "--";
            }

            // 参数量优先取文件名（同仓库的不同参数版本靠它区分），文件名里没有再看仓库名
            double? paramB = ParseParamBillion(file.path) ?? ParseParamBillion(repoId);
            long bytes = ModelSizeBytes(file, all);
            string sizeText = bytes > 0 ? FormatSize(bytes) : L.T("mdl.hw.unknownSize");

            tip = paramB.HasValue
                ? string.Format(L.T("mdl.hw.tipByParam"),
                    paramB.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), sizeText)
                : string.Format(L.T("mdl.hw.tipBySize"), sizeText);

            string advice = HardwareAdvice(paramB, bytes);
            if (advice.Length == 0) tip += L.T("mdl.hw.tipNoSize");
            return advice;
        }

        // ==================== 下载 ====================

        /// <summary>识别 llama.cpp 分片名（xxx-00001-of-00004.gguf），返回 "1/4"；非分片返回 null</summary>
        private static string ShardLabel(string path)
        {
            string name = System.IO.Path.GetFileName(path ?? "");
            var m = System.Text.RegularExpressions.Regex.Match(
                name, @"-(\d{5})-of-(\d{5})\.gguf$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            return m.Groups[1].Value.TrimStart('0') + "/" + m.Groups[2].Value.TrimStart('0');
        }

        /// <summary>
        /// 分片模型的下载提醒；返回 null 表示不是分片。
        /// llama.cpp 加载分片模型要求同一目录下 N 片齐全，只下第 1 片会直接加载失败。
        /// </summary>
        private static string ShardHint(string path, List<HfFile> files)
        {
            string name = System.IO.Path.GetFileName(path ?? "");
            var m = System.Text.RegularExpressions.Regex.Match(
                name, @"-(\d{5})-of-(\d{5})\.gguf$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return null;

            string dir = System.IO.Path.GetDirectoryName(path ?? "") ?? "";
            int total = int.Parse(m.Groups[2].Value);
            int listed = 0;
            foreach (var f in files)
            {
                if (string.Equals(System.IO.Path.GetDirectoryName(f.path ?? "") ?? "", dir,
                        StringComparison.OrdinalIgnoreCase)
                    && ShardLabel(f.path) != null)
                    listed++;
            }

            string where = string.IsNullOrEmpty(dir) ? L.T("mdl.shard.root") : dir + "/";
            return string.Format(L.T("mdl.shard.hint"),
                m.Groups[1].Value.TrimStart('0'), total, where, listed);
        }

        private void dgvFiles_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (!(dgvFiles.Columns[e.ColumnIndex] is DataGridViewButtonColumn)) return;
            if (e.RowIndex >= _files.Count) return;

            int modelIdx = dgvModels.CurrentRow?.Index ?? -1;
            if (modelIdx < 0 || modelIdx >= _models.Count) return;
            var model = _models[modelIdx];
            var file = _files[e.RowIndex];

            // 分片提醒（非分片为 null）：在详情确认窗口中展示，不再单独弹 MessageBox
            string shardHint = ShardHint(file.path, _files);

            // 下载 URL：hf-mirror / huggingface 都走 /{repo}/resolve/main/{file}
            string url = HfApi.BuildDownloadUrl(BaseUrl, model.id, file.path);

            // 保存目录默认 {配置根}\models\{owner-repo}\，仓库内子目录结构原样保留
            //（如 UD-Q4_K_XL/xxx-00001-of-00004.gguf 整组同目录，llama.cpp 才能按第 1 片找到后续分片）
            string dirName = model.id.Replace('/', '-');
            string saveDir = System.IO.Path.Combine(LlamaRuntime.ModelsDir, dirName);

            // 适配硬件建议（与文件列表中显示的口径一致；mmproj 为 "--"）
            string hardware = HardwareLabel(file, model.id, _files, out _);

            // 开始下载前：先弹出详情确认窗口；取消则不下载
            if (ModelDownloadConfirmForm.Show(this, model, file, url, saveDir,
                    SourceName(), hardware, shardHint) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("mdl.status.downloadCancelled"), file.path);
                return;
            }

            // 下载前弹出对话框让用户确定保存位置（默认目录 / 另存为其他目录）；
            // 提交后任务进入后台下载队列（gguf 无需解压，ExtractDir=null）
            using (var dlg = new DownloadTargetForm(dirName, file.path, file.size, saveDir))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                DownloadManager.Instance.Enqueue(dirName, file.path, url, file.size,
                    dlg.SaveDir, null, BearerToken, DownloadManager.LastProxyUrl);
                lblStatus.Text = string.Format(L.T("mdl.status.enqueued"), file.path, dlg.SaveDir);
                FileDownloadForm.ShowSingle();
            }
        }

        // ==================== 关闭 ====================

        private void ModelDownloadForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _filesCts?.Cancel();
        }

        // ==================== 辅助 ====================

        private string SourceName() => IsHuggingFace ? L.T("mdl.source.hf") : L.T("mdl.source.mirror");

        private static string FormatCount(long n)
        {
            if (n >= 1000000) return (n / 1000000.0).ToString("0.0") + "M";
            if (n >= 1000) return (n / 1000.0).ToString("0.0") + "K";
            return n.ToString();
        }

        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            if (DateTime.TryParse(iso, out var dt)) return dt.ToString("yyyy-MM-dd");
            return iso.Length > 10 ? iso.Substring(0, 10) : iso;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
            if (bytes >= GB) return (bytes / GB).ToString("0.00") + " GB";
            if (bytes >= MB) return (bytes / MB).ToString("0.0") + " MB";
            if (bytes >= KB) return (bytes / KB).ToString("0") + " KB";
            return bytes + " B";
        }
    }
}
