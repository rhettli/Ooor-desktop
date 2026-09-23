using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ooor.Core;

namespace ooor.Controls
{
    /// <summary>
    /// 单行下载任务控件（迅雷式布局）：
    ///
    ///   ┌─ 文件 ─────────┬─ 进度 ─────────┬─ 操作 ────┐
    ///   │ 📦 file.zip     │ ████░░░  59%   │ ∥暂停 ✕删 │
    ///   │   D:\save\path  │ 24/43 MB ·5MB/s│           │
    ///   │   🕒 时间·状态   │ ▌▌▌▌▌⬤⬤⬤     │           │
    ///   └─────────────────┴─────────────────┴───────────┘
    ///
    /// 唯一自绘 = ChunkSparkline（分块火柴图）。其余全部用原生控件，零闪烁。
    /// 背景色：极小跨度（白 → 淡灰 → 淡蓝），点击选中后变深蓝。
    /// </summary>
    public class RowControl : UserControl
    {
        // ==================== 布局常量（绝对坐标，相对 RowControl 自身）====================
        public const int RowHeight = 84;
        private const int FileColX = 8,  FileColW = 400;
        private const int ProgColX = 418, ProgColW = 290;
        private const int ActionColX = 718;
        private const int BtnPrimaryW = 66, BtnDeleteW = 66;
        private const int BtnHeight = 32;
        private const int BtnVGap = 4;          // 竖排：主按钮在上，删除按钮在下

        // ==================== 配色（极小跨度）====================
        private static readonly Color[] _bg = {
            Color.White,                              // 0  奇数行
            Color.FromArgb(252,252,252),              // 1  偶数行
            Color.FromArgb(245,248,252),              // 2  hover
            Color.FromArgb(225,239,254),              // 3  selected
            Color.FromArgb(214,232,251),              // 4  selected+hover
        };
        private static readonly Color _borderSelected = Color.FromArgb(0xB4, 0xD2, 0xF0);
        private static readonly Color _borderIdle     = Color.FromArgb(0xEC, 0xEC, 0xEC);

        // 操作按钮配色（低饱和灰白，与左侧基调统一）
        private static readonly Color _btnIdle   = Color.FromArgb(0xFA, 0xFA, 0xFA);   // 常态底
        private static readonly Color _btnText   = Color.FromArgb(0x33, 0x33, 0x33);   // 常态字
        private static readonly Color _btnBorder = Color.FromArgb(0xDC, 0xDC, 0xDC);   // 描边
        private static readonly Color _btnHover  = Color.FromArgb(0xEF, 0xEF, 0xEF);   // 悬停底
        private static readonly Color _btnDown   = Color.FromArgb(0xE2, 0xE2, 0xE2);   // 按下底
        private static readonly Color _btnDisTxt = Color.FromArgb(0xBB, 0xBB, 0xBB);   // 禁用字

        // ==================== 状态 ====================
        private DownloadTask _task;
        private int _rowIndex;
        private bool _hovered;
        private bool _selected;
        private bool _suppressSelect;   // 按钮点击时禁止切换选中
        private ChunkSnapshot[] _lastSnaps;

        // ==================== 子控件 ====================
        private PictureBox picIcon;
        private Label lblFileName, lblSavePath, lblMeta;
        private ProgressBar pbMain;
        private Label lblStatNum;
        private ChunkSparkline ctrlChunks;
        private Button btnPrimary, btnDelete;
        private readonly ToolTip _tip = new ToolTip();

        /// <summary>主操作按钮被点击（按状态分：开始/暂停/继续/重试）</summary>
        public event Action<int> PrimaryClicked;
        /// <summary>删除按钮被点击</summary>
        public event Action<int> DeleteClicked;

        public RowControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);

            Height = RowHeight;
            Margin = new Padding(0);
            Padding = new Padding(0);
            BorderStyle = BorderStyle.None;

            BuildChildren();
            UpdateBackColor();
        }

        /// <summary>行号（供 Form 在增删行后同步，按钮/点击事件回传用）</summary>
        public int RowIndex { get => _rowIndex; set => _rowIndex = value; }

        /// <summary>由 Form 调用：填充数据 + 订阅事件</summary>
        public void Bind(int rowIndex, DownloadTask task)
        {
            _rowIndex = rowIndex;
            _task = task;
            lblFileName.Text = task.FileName;
            // 保存目录直接显示纯文本路径（emoji 前缀会导致 GDI 丢字）
            lblSavePath.Text = string.IsNullOrEmpty(task.SaveDir) ? "(未设置保存目录)" : task.SaveDir;
            lblMeta.Text = FormatMetaLine(task);
            lblStatNum.Text = FormatStatLine(task);   // Bind 时也填充尺寸行，否则重启恢复的任务（不再有进度通知）会一直空白
            picIcon.Image = GetFileIconImage(task.FileName);
            pbMain.Minimum = 0;
            pbMain.Maximum = 1000;
            UpdateProgress();
            UpdatePrimaryButton(task.State);
            // 初次绑定也刷一次火柴图（下载中的任务打开窗口立即有内容，不等 500ms 定时器）
            ctrlChunks.SetFallbackSlots(task.Threads);   // 无快照时（暂停/排队）按分块数画空槽，避免误显示"单线程"
            ctrlChunks.SetSnapshots(DownloadManager.Instance.GetChunkSnapshots(task), task.State);
            _lastSnaps = null;
            UpdateBackColor();
        }

        /// <summary>由 Form 调用：仅刷新进度（节流调用，200ms 一次）</summary>
        public void RefreshProgress()
        {
            if (_task == null) return;
            UpdateProgress();
            var snaps = DownloadManager.Instance.GetChunkSnapshots(_task);
            if (snaps != _lastSnaps)
            {
                _lastSnaps = snaps;
                ctrlChunks.SetSnapshots(snaps, _task.State);
            }
            else
            {
                // snaps 内容（Done 字段）变了 → 重新传一次触发重绘
                ctrlChunks.SetSnapshots(snaps, _task.State);
            }
            lblStatNum.Text = FormatStatLine(_task);
            lblMeta.Text = FormatMetaLine(_task);
            // 分块回退单线程的原因（悬停状态行可见；正常时清除）
            _tip.SetToolTip(lblMeta, string.IsNullOrEmpty(_task.ChunkFallbackReason) ? null : _task.ChunkFallbackReason);
            // 失败原因：悬停"失败"文字可见完整信息（Message 可能很长，行内放不下）
            _tip.SetToolTip(lblStatNum,
                _task.State == DownloadState.Failed && !string.IsNullOrEmpty(_task.Message)
                    ? _task.Message : null);
            UpdatePrimaryButton(_task.State);
        }

        public bool Selected
        {
            get => _selected;
            set { if (_selected == value) return; _selected = value; UpdateBackColor(); }
        }

        /// <summary>
        /// 由 Form 调用：行宽 = FlowLayoutPanel 可视宽。
        /// 进度列吃掉所有剩余宽度，按钮贴右缘（迅雷式），不留大片空白。
        /// </summary>
        public void SetRowWidth(int w)
        {
            Width = Math.Max(TotalMinWidth, w);

            int btnX = Width - RightPad - BtnDeleteW;   // 竖排：两个按钮右对齐同一 X
            btnDelete.Left = btnX;
            btnPrimary.Left = btnX;

            int progW = Math.Max(ProgColMinWidth, btnX - ProgGap - ProgColX);
            pbMain.Width = progW;
            lblStatNum.Width = progW;
            ctrlChunks.Width = progW;
        }
        private const int RightPad = 12;        // 行右缘留白
        private const int ProgGap = 16;         // 进度列与按钮区间距
        private const int ProgColMinWidth = 240;
        private const int TotalMinWidth = 850;

        // ==================== 子控件构建 ====================

        private void BuildChildren()
        {
            SuspendLayout();

            // —— 文件列 ——
            picIcon = new PictureBox
            {
                Size = new Size(16, 16),
                Location = new Point(FileColX, 10),
                SizeMode = PictureBoxSizeMode.StretchImage,
                // PictureBox 不支持 Color.Transparent → 不设 BackColor，在 UpdateBackColor 中同步行背景色
            };

            lblFileName = new Label
            {
                AutoSize = false,
                Location = new Point(FileColX + 22, 6),
                Size = new Size(FileColW - 24, 22),
                Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x21, 0x21, 0x21),
                Text = "",
                BackColor = Color.Transparent
            };

            lblSavePath = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(FileColX, 30),
                Size = new Size(FileColW, 20),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Color.FromArgb(0x80, 0x80, 0x80),
                Text = "",
                BackColor = Color.Transparent
                // 注意：不要用 emoji 前缀（📁 等）——GDI Label 渲染 surrogate pair 后会丢掉后面的文字
            };

            lblMeta = new Label
            {
                AutoSize = false,
                Location = new Point(FileColX, 54),
                Size = new Size(FileColW, 20),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                Text = "",
                BackColor = Color.Transparent
            };

            // —— 进度列 ——
            pbMain = new ProgressBar
            {
                Location = new Point(ProgColX, 8),
                Size = new Size(ProgColW, 18),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 1000
            };

            lblStatNum = new Label
            {
                AutoSize = false,
                Location = new Point(ProgColX, 32),
                Size = new Size(ProgColW, 20),
                Font = new Font("Consolas", 9f),
                ForeColor = Color.FromArgb(0x42, 0x42, 0x42),
                Text = "",
                BackColor = Color.Transparent
            };

            ctrlChunks = new ChunkSparkline
            {
                Location = new Point(ProgColX, 56),
                Size = new Size(ProgColW, 20),
                // ChunkSparkline 继承 Control，不支持 Color.Transparent → 保留自身构造函数里的 BackColor=White
            };

            // —— 操作列：竖排两按钮，上=主操作（暂停/继续…），下=删除 ——
            // 统一为低饱和灰白风格：浅灰底 + 深灰字 + 1px 浅灰描边，与左侧黑白灰基调一致
            btnPrimary = new Button
            {
                Location = new Point(ActionColX, 8),
                Size = new Size(BtnPrimaryW, BtnHeight),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Regular),
                Text = "▶ 开始",
                BackColor = _btnIdle,
                ForeColor = _btnText,
                Cursor = Cursors.Hand
            };
            btnPrimary.FlatAppearance.BorderSize = 1;
            btnPrimary.FlatAppearance.BorderColor = _btnBorder;
            btnPrimary.FlatAppearance.MouseOverBackColor = _btnHover;
            btnPrimary.FlatAppearance.MouseDownBackColor = _btnDown;
            btnPrimary.Click += (s, e) =>
            {
                _suppressSelect = true;
                PrimaryClicked?.Invoke(_rowIndex);
                _suppressSelect = false;
            };

            // 删除按钮：同样灰白底，仅文字用低饱和红提示危险，hover 时底色微偏红
            btnDelete = new Button
            {
                Location = new Point(ActionColX, 8 + BtnHeight + BtnVGap),
                Size = new Size(BtnDeleteW, BtnHeight),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Microsoft YaHei UI", 9f),
                Text = "✕ 删除",
                BackColor = _btnIdle,
                ForeColor = Color.FromArgb(0xA8, 0x42, 0x42),   // 低饱和红，不刺眼
                Cursor = Cursors.Hand
            };
            btnDelete.FlatAppearance.BorderSize = 1;
            btnDelete.FlatAppearance.BorderColor = _btnBorder;
            btnDelete.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xF3, 0xE7, 0xE7);
            btnDelete.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xE8, 0xD5, 0xD5);
            btnDelete.Click += (s, e) =>
            {
                _suppressSelect = true;
                DeleteClicked?.Invoke(_rowIndex);
                _suppressSelect = false;
            };

            Controls.Add(picIcon);
            Controls.Add(lblFileName);
            Controls.Add(lblSavePath);
            Controls.Add(lblMeta);
            Controls.Add(pbMain);
            Controls.Add(lblStatNum);
            Controls.Add(ctrlChunks);
            Controls.Add(btnPrimary);
            Controls.Add(btnDelete);

            ResumeLayout(false);
        }

        // ==================== 文本与格式化 ====================

        private static string FormatMetaLine(DownloadTask t)
        {
            var now = DateTime.Now;
            string time = t.CreateTime.Date == now.Date
                ? "今天 " + t.CreateTime.ToString("HH:mm")
                : t.CreateTime.Date == now.Date.AddDays(-1)
                    ? "昨天 " + t.CreateTime.ToString("HH:mm")
                    : t.CreateTime.ToString("MM-dd HH:mm");

            // 显示真实下载模式：活动中的分块任务看 LiveChunks；
            // 已结束（完成/失败/暂停）时 LiveChunks 被清空 → 按 Threads 配置显示，
            // 只有真的回退过单线程（ChunkFallbackReason 非空）才显示"单线程"
            var live = DownloadManager.Instance.GetChunkSnapshots(t);
            string threadLabel;
            if (live != null) threadLabel = live.Length + " 分块";
            else if (!string.IsNullOrEmpty(t.ChunkFallbackReason)) threadLabel = "单线程";
            else threadLabel = t.Threads > 1 ? t.Threads + " 分块" : "单线程";
            string stateLabel;
            switch (t.State)
            {
                case DownloadState.Queued:      stateLabel = "⬤ 排队中";  break;
                case DownloadState.Downloading: stateLabel = "⬤ 下载中";  break;
                case DownloadState.Extracting:  stateLabel = "⬤ 解压中";  break;
                case DownloadState.Completed:   stateLabel = "⬤ 已完成";  break;
                case DownloadState.Paused:      stateLabel = "⬤ 已暂停";  break;
                case DownloadState.Failed:      stateLabel = "⬤ 失败";   break;
                default: stateLabel = "—"; break;
            }
            return time + " · " + threadLabel + " · " + stateLabel;
        }

        private static string FormatStatLine(DownloadTask t)
        {
            string speedPart;
            if (t.State == DownloadState.Downloading && t.SpeedBps > 0)
                speedPart = DownloadManager.FormatSize((long)t.SpeedBps) + "/s";
            else if (t.State == DownloadState.Completed)
                speedPart = "完成";
            else if (t.State == DownloadState.Failed)
                speedPart = "失败";
            else if (t.State == DownloadState.Paused)
                speedPart = "已暂停";
            else
                speedPart = "—";

            string sizePart;
            if (t.TotalBytes > 0)
                sizePart = DownloadManager.FormatSize(t.DownloadedBytes) + " / " + DownloadManager.FormatSize(t.TotalBytes);
            else if (t.DownloadedBytes > 0)
                sizePart = DownloadManager.FormatSize(t.DownloadedBytes) + " / ?";
            else
                sizePart = "等待开始…";

            string eta = "";
            if (t.State == DownloadState.Downloading && t.SpeedBps > 0 && t.TotalBytes > t.DownloadedBytes)
            {
                double sec = (t.TotalBytes - t.DownloadedBytes) / t.SpeedBps;
                if (sec < 60) eta = " · 剩余 " + ((int)sec) + "s";
                else if (sec < 3600) eta = " · 剩余 " + ((int)(sec / 60)) + "m" + ((int)(sec % 60)) + "s";
                else eta = " · 剩余 " + ((int)(sec / 3600)) + "h" + (((int)(sec / 60)) % 60) + "m";
            }

            return sizePart + " · " + speedPart + eta;
        }

        private void UpdateProgress()
        {
            double pct;
            if (_task.State == DownloadState.Completed) pct = 100;
            else if (_task.TotalBytes > 0) pct = (double)_task.DownloadedBytes * 100.0 / _task.TotalBytes;
            else pct = 0;
            pbMain.Value = Math.Min(pbMain.Maximum, Math.Max(0, (int)(pct * 10)));
        }

        private void UpdatePrimaryButton(DownloadState state)
        {
            // 全部状态统一灰白底 + 深灰字，仅靠文字/图标区分，不再用高饱和色块
            btnPrimary.BackColor = _btnIdle;
            btnPrimary.ForeColor = _btnText;
            btnPrimary.FlatAppearance.BorderColor = _btnBorder;
            switch (state)
            {
                case DownloadState.Queued:
                    btnPrimary.Text = "▶ 开始";
                    btnPrimary.Enabled = true;
                    break;
                case DownloadState.Downloading:
                    btnPrimary.Text = "∥ 暂停";
                    btnPrimary.Enabled = true;
                    break;
                case DownloadState.Paused:
                case DownloadState.Failed:
                    btnPrimary.Text = state == DownloadState.Failed ? "↻ 重试" : "↻ 继续";
                    btnPrimary.Enabled = true;
                    break;
                case DownloadState.Completed:
                    btnPrimary.Text = "✓ 已完成";
                    btnPrimary.Enabled = false;
                    btnPrimary.ForeColor = _btnDisTxt;
                    break;
                case DownloadState.Extracting:
                    btnPrimary.Text = "解压中…";
                    btnPrimary.Enabled = false;
                    btnPrimary.ForeColor = _btnDisTxt;
                    break;
                default:
                    btnPrimary.Text = "—";
                    btnPrimary.Enabled = false;
                    btnPrimary.ForeColor = _btnDisTxt;
                    break;
            }
        }

        // ==================== 背景色阶 + 边框 ====================

        public void SetRowParity(bool even) { _rowIndexParity = even; UpdateBackColor(); }
        private bool _rowIndexParity;

        private void UpdateBackColor()
        {
            int idx = _selected
                ? (_hovered ? 4 : 3)
                : (_hovered ? 2 : (_rowIndexParity ? 1 : 0));
            Color bg = _bg[idx];
            BackColor = bg;
            // PictureBox 不支持 Transparent → 显式同步成行背景，让 16x16 图标块与行底色完全融合
            if (picIcon != null) picIcon.BackColor = bg;
            // ChunkSparkline 是 Control（不支持 Transparent）→ 同步底色，避免 hover/选中时出现白斑
            if (ctrlChunks != null) ctrlChunks.BackColor = bg;
            // 选中态边框统一由 OnPaint 自绘（避免与 BorderStyle.FixedSingle 双重绘制闪）
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 选中态边框（OnPaint 重画以控制颜色）
            if (_selected)
            {
                using (var pen = new Pen(_borderSelected))
                {
                    var r = ClientRectangle;
                    e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }
        }

        // ==================== 鼠标事件：hover / 选中切换 ====================

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            UpdateBackColor();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            UpdateBackColor();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_suppressSelect) return;
            // 左击/右击空白区域 → 通知 Form 选中本行。
            // 右击同样走 _rowIndex 路径，让随后弹出的 ContextMenuStrip 直接用 SelectedTask，
            // 不再靠坐标反推（坐标反推在排序/滚动后会命中错误的行）。
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                ((FileDownloadForm)FindForm())?.SetSelectedFromRow(_rowIndex);
        }

        // 子控件转发 MouseEnter/Leave 让 RowControl 自身也感知（让空白处 hover 时整行变色）
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            WireChildHover(e.Control);
        }

        private void WireChildHover(Control c)
        {
            c.MouseEnter += (s, _) => OnMouseEnter(EventArgs.Empty);
            c.MouseLeave += (s, _) =>
            {
                // 只有当鼠标真的离开本行所有子控件时，才取消 hover
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    OnMouseLeave(EventArgs.Empty);
            };
            // 点击子控件（Label/ProgressBar/ChunkSparkline 等不吃 MouseDown 的区域）也要选中本行，
            // 否则只有点行内空白才能选中 —— 这就是"有时能选中有时不能"的原因。
            // 右击同样走 _rowIndex，保证 ContextMenuStrip 的目标任务与看到的项目一致。
            c.MouseDown += (s, e) =>
            {
                if (_suppressSelect) return;
                if (e.Button != MouseButtons.Left && e.Button != MouseButtons.Right) return;
                ((FileDownloadForm)FindForm())?.SetSelectedFromRow(_rowIndex);
            };
            foreach (Control child in c.Controls) WireChildHover(child);
        }

        // ==================== 图标缓存 ====================

        private static readonly System.Collections.Generic.Dictionary<string, Image> _iconCache =
            new System.Collections.Generic.Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private static Image GetFileIconImage(string fileName)
        {
            string ext = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(ext)) return null;
            if (_iconCache.TryGetValue(ext, out var cached)) return cached;
            try
            {
                using (var icon = IconReader.GetFileIcon(ext, IconReader.IconSize.Small))
                {
                    Image img = icon?.ToBitmap();
                    _iconCache[ext] = img;
                    return img;
                }
            }
            catch { return null; }
        }
    }
}