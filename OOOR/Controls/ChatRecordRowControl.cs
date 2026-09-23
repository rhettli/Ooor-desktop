using System;
using System.Drawing;
using System.Windows.Forms;
using OoorFunc.Core;

namespace ooor.Controls
{
    /// <summary>
    /// 控制台聊天记录的单行卡片控件（字段上下叠放，一行展示原 ListView 7 列的信息）：
    ///
    ///   ┌────────────────────────────────────────────────────────────┐
    ///   │ 聊天标题（粗体）……              Agent：xx        09-21 14:30│
    ///   │ 模型：Qwen3-8B.gguf（灰色，超长省略）                        │
    ///   │ 思考 12.3 字符/s · 正文 30.1 字符/s · 备注：……（灰色省略）    │
    ///   └────────────────────────────────────────────────────────────┘
    ///
    /// 全部使用原生 Label，零自绘文本；选中态边框/底色沿用下载列表 RowControl 的配色。
    /// </summary>
    public class ChatRecordRowControl : UserControl
    {
        public const int RowHeight = 66;

        private const int PadX = 12;
        private const int RightPad = 20;
        private const int MinTitleW = 160;

        // 配色（与下载列表 RowControl 保持一致的极小跨度）
        private static readonly Color[] _bg = {
            Color.White,                  // 0  奇数行
            Color.FromArgb(252,252,252),  // 1  偶数行
            Color.FromArgb(245,248,252),  // 2  hover
            Color.FromArgb(225,239,254),  // 3  selected
            Color.FromArgb(214,232,251),  // 4  selected+hover
        };
        private static readonly Color _borderSelected = Color.FromArgb(0xB4, 0xD2, 0xF0);

        private ChatRecord _record;
        private int _rowIndex;
        private bool _hovered;
        private bool _selected;
        private bool _rowParity;

        private Label lblTitle, lblAgent, lblTime, lblModel, lblMeta;
        private readonly ToolTip _tip = new ToolTip();

        /// <summary>本行被点击选中时通知窗口（参数为行号）</summary>
        public event Action<int> RowSelected;
        /// <summary>本行被双击（含双击在 Label 上）→ 双击接着聊</summary>
        public event Action<int> RowActivated;

        public ChatRecordRowControl()
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

        public int RowIndex { get => _rowIndex; set => _rowIndex = value; }
        public ChatRecord Record => _record;

        /// <summary>填充一条聊天记录到本行。</summary>
        public void Bind(int rowIndex, ChatRecord c)
        {
            _rowIndex = rowIndex;
            _record = c;

            lblTitle.Text = string.IsNullOrEmpty(c.Title) ? "(未命名)" : c.Title;
            lblAgent.Text = string.IsNullOrEmpty(c.Agent) ? "" : "Agent：" + c.Agent;
            lblTime.Text = FormatTime(c.UpdatedAt);
            //lblTime.TextAlign = ContentAlignment.MiddleRight;
            lblTime.AutoSize = true;

            string model = string.IsNullOrEmpty(c.Model) ? "" : ShortModel(c.Model);
            lblModel.Text = string.IsNullOrEmpty(model) ? "模型：（未记录）" : "模型：" + model;

            string meta = "";
            if (!string.IsNullOrEmpty(c.LastReasoningSpeed)) meta += "思考 " + c.LastReasoningSpeed;
            if (!string.IsNullOrEmpty(c.LastContentSpeed))
                meta += (meta.Length > 0 ? "  ·  " : "") + "正文 " + c.LastContentSpeed;
            if (!string.IsNullOrEmpty(c.Remark))
                meta += (meta.Length > 0 ? "  ·  " : "") + "备注：" + c.Remark;
            lblMeta.Text = meta;

            // 悬停看完整长文本
            _tip.SetToolTip(lblTitle, lblTitle.Text);
            _tip.SetToolTip(lblModel, string.IsNullOrEmpty(c.Model) ? null : c.Model);
            _tip.SetToolTip(lblMeta, meta);

            UpdateBackColor();
        }

        public bool Selected
        {
            get => _selected;
            set { if (_selected == value) return; _selected = value; UpdateBackColor(); Invalidate(); }
        }

        public void SetRowParity(bool even) { _rowParity = even; UpdateBackColor(); }

        /// <summary>窗口宽度变化时重排：时间/Agent 右对齐，标题吃掉左侧剩余宽度。</summary>
        public void SetRowWidth(int w)
        {
            Width = Math.Max(420, w);

            // 右侧：时间（最右）← Agent（时间左边）
            int timeW = TextRenderer.MeasureText(lblTime.Text, lblTime.Font).Width;
            lblTime.Left = Width - RightPad - timeW;

            int agentW = string.IsNullOrEmpty(lblAgent.Text)
                ? 0
                : TextRenderer.MeasureText(lblAgent.Text, lblAgent.Font).Width;
            lblAgent.Left = lblTime.Left - (agentW > 0 ? 10 : 0) - agentW;
            lblAgent.Width = agentW;

            int titleW = lblAgent.Left - 10 - PadX;
            lblTitle.Width = Math.Max(MinTitleW, titleW);

            int fullW = Width - PadX - RightPad;
            lblModel.Width = fullW;
            lblMeta.Width = fullW;
        }

        // ==================== 布局 ====================

        private void BuildChildren()
        {
            SuspendLayout();

            lblTitle = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(PadX, 6),
                Size = new Size(300, 24),
                Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0x21, 0x21, 0x21),
                BackColor = Color.Transparent
            };

            lblAgent = new Label
            {
                AutoSize = false,
                Location = new Point(600, 8),
                Size = new Size(160, 20),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Color.FromArgb(0x2E, 0x5C, 0x8A),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight
            };

            lblTime = new Label
            {
                AutoSize = false,
                Location = new Point(868, 8),
                Size = new Size(120, 20),
                Font = new Font("Microsoft YaHei UI", 9f),
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleRight
            };

            lblModel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(PadX, 31),
                Size = new Size(400, 18),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Color.FromArgb(0x66, 0x66, 0x66),
                BackColor = Color.Transparent
            };

            lblMeta = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(PadX, 47),
                Size = new Size(400, 16),
                Font = new Font("Microsoft YaHei UI", 8.5f),
                ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E),
                BackColor = Color.Transparent
            };

            Controls.Add(lblTitle);
            Controls.Add(lblAgent);
            Controls.Add(lblTime);
            Controls.Add(lblModel);
            Controls.Add(lblMeta);

            ResumeLayout(false);
        }

        // ==================== 文本格式化 ====================

        private static string FormatTime(DateTime t)
        {
            var now = DateTime.Now;
            if (t.Date == now.Date) return "今天 " + t.ToString("HH:mm");
            if (t.Date == now.Date.AddDays(-1)) return "昨天 " + t.ToString("HH:mm");
            if (t.Year == now.Year) return t.ToString("MM-dd HH:mm");
            return t.ToString("yyyy-MM-dd");
        }

        /// <summary>模型路径只留文件名（完整路径在悬停提示里看）。</summary>
        private static string ShortModel(string path)
        {
            int slash = path.LastIndexOfAny(new[] { '\\', '/' });
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }

        // ==================== 底色 / 选中边框 / 鼠标 ====================

        private void UpdateBackColor()
        {
            int idx = _selected
                ? (_hovered ? 4 : 3)
                : (_hovered ? 2 : (_rowParity ? 1 : 0));
            BackColor = _bg[idx];
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_selected)
            {
                using (var pen = new Pen(_borderSelected))
                {
                    var r = ClientRectangle;
                    e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
            }
        }

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
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                RowSelected?.Invoke(_rowIndex);
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            RowActivated?.Invoke(_rowIndex);
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            WireChild(e.Control);
        }

        private void WireChild(Control c)
        {
            c.MouseEnter += (s, _) => OnMouseEnter(EventArgs.Empty);
            c.MouseLeave += (s, _) =>
            {
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    OnMouseLeave(EventArgs.Empty);
            };
            c.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                    RowSelected?.Invoke(_rowIndex);
            };
            // Label 吃掉双击事件，需转发，保证双击行内任意位置都能「接着聊」
            c.DoubleClick += (s, e) => RowActivated?.Invoke(_rowIndex);
        }
    }
}
