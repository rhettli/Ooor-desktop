using System;
using System.Drawing;
using System.Windows.Forms;

namespace ooor.Controls
{
    /// <summary>
    /// 更新日志行控件（2 行布局）：
    ///   v0.0040  stable  2026-09-29 10:00       ← 粗体版本 + 通道 + 发布时间
    ///   - 修复了某某问题                         ← 更新日志（多行）
    ///   - 添加了某某功能
    /// 鼠标悬停变色，底部 1px 分隔线。
    /// </summary>
    public class UpdateLogRowControl : UserControl
    {
        public const int RowHeight = 90;

        private static readonly Color BgDefault = Color.White;
        private static readonly Color BgHover = Color.FromArgb(245, 248, 252);
        private static readonly Color BorderClr = Color.FromArgb(0xE8, 0xE8, 0xE8);
        private static readonly Color VersionClr = Color.FromArgb(0x1a, 0x1a, 0x1a);
        private static readonly Color MetaClr = Color.FromArgb(0x88, 0x88, 0x88);
        private static readonly Color NotesClr = Color.FromArgb(0x44, 0x44, 0x44);

        private bool _hovered;
        private readonly Label lblHeader;   // v0.0040  stable  2026-09-29 10:00
        private readonly Label lblNotes;    // 更新日志（多行）

        public UpdateLogRowControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            Height = RowHeight;
            Margin = new Padding(0);
            Padding = new Padding(0);
            BorderStyle = BorderStyle.None;
            Font = new Font("Segoe UI", 9F);

            lblHeader = new Label
            {
                Location = new Point(16, 8),
                Size = new Size(760, 22),
                AutoSize = false,
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
                ForeColor = VersionClr,
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
            lblNotes = new Label
            {
                Location = new Point(16, 32),
                Size = new Size(760, 54),
                AutoSize = false,
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Regular),
                ForeColor = NotesClr,
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
            Controls.Add(lblHeader);
            Controls.Add(lblNotes);

            BackColor = BgDefault;
        }

        /// <summary>填充一条日志</summary>
        public void Bind(string version, string channel, string notes,
            bool force, bool fileDeleted, string publishedAt)
        {
            SuspendLayout();

            string header = $"v{version}  {channel}";
            if (force) header += "  [强制更新]";
            if (fileDeleted) header += "  [已归档]";
            if (!string.IsNullOrEmpty(publishedAt)) header += $"    {publishedAt}";
            lblHeader.Text = header;

            lblNotes.Text = string.IsNullOrEmpty(notes) ? "-" : notes;

            BackColor = _hovered ? BgHover : BgDefault;
            ReflowWidth();
            ResumeLayout(false);
        }

        public void ReflowWidth()
        {
            if (lblHeader == null || lblNotes == null) return;
            int w = Math.Max(100, (Parent?.ClientSize.Width ?? 780) - 24);
            lblHeader.Width = w;
            lblNotes.Width = w;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ReflowWidth();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            BackColor = BgHover;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            BackColor = BgDefault;
            Invalidate();
        }

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
                if (!ClientRectangle.Contains(PointToClient(Cursor.Position)))
                    OnMouseLeave(EventArgs.Empty);
            };
            foreach (Control child in c.Controls) WireChildHover(child);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var b = new SolidBrush(BackColor))
                e.Graphics.FillRectangle(b, ClientRectangle);
            base.OnPaint(e);
            using (var p = new Pen(BorderClr))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }
}
