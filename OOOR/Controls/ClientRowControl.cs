using System;
using System.Drawing;
using System.Windows.Forms;

namespace ooor.Controls
{
    /// <summary>
    /// 在线客户端行控件（3 行布局，仿终端输出）：
    ///   #1  DESKTOP-Q0Q3F5T                              ← 粗体
    ///   AMD Ryzen 5 6600H with Radeon Graphics   12 核   15.2 GB   版本 0.0040
    ///   IP：2409:8a30:7c98:…    最后在线 11 分钟前    UUID 7f30cfee98c6…
    /// 奇偶行交替底色，底部 1px 分隔线。宽度跟随父容器。
    /// </summary>
    public class ClientRowControl : UserControl
    {
        public const int RowHeight = 78;

        private static readonly Color BgOdd = Color.White;
        private static readonly Color BgEven = Color.FromArgb(248, 249, 250);
        private static readonly Color BorderClr = Color.FromArgb(0xE8, 0xE8, 0xE8);
        private static readonly Color TitleClr = Color.FromArgb(0x1a, 0x1a, 0x1a);
        private static readonly Color SpecClr = Color.FromArgb(0x44, 0x44, 0x44);

        private readonly Label lblTitle;   // #1  hostname
        private readonly Label lblSpec;    // CPU  cores  mem  ver
        private readonly Label lblMeta;    // IP  最后在线  UUID

        public ClientRowControl()
        {
            // 双缓冲，防闪烁
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            Height = RowHeight;
            Font = new Font("Segoe UI", 9F);

            lblTitle = MakeLabel(16, 6, FontStyle.Bold, TitleClr);
            lblSpec = MakeLabel(16, 28, FontStyle.Regular, SpecClr);
            lblMeta = MakeLabel(16, 50, FontStyle.Regular, Color.Gray);

            BackColor = BgOdd;
        }

        private Label MakeLabel(int x, int y, FontStyle style, Color fore)
        {
            var lbl = new Label
            {
                Location = new Point(x, y),
                AutoSize = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font(Font.FontFamily, Font.Size, style),
                ForeColor = fore,
                BackColor = Color.Transparent,
                UseMnemonic = false,
            };
            Controls.Add(lbl);
            return lbl;
        }

        /// <summary>填充一行数据</summary>
        public void Bind(int index, string hostname, string cpu, int cores,
            long memMb, string ver, string ip, long lastAt, long serverTime,
            string uuid, bool even)
        {
            SuspendLayout();
            BackColor = even ? BgEven : BgOdd;

            lblTitle.Text = $"#{index}  {hostname}";
            lblSpec.Text = $"{cpu}   {cores} 核   {MemToGb(memMb):F1} GB   版本 {ver}";
            lblMeta.Text = $"IP：{ip}    最后在线 {Ago(serverTime - lastAt)}    UUID {ShortUuid(uuid)}";

            ReflowWidth();
            ResumeLayout(false);
        }

        /// <summary>跟随父容器宽度调整各 Label 宽度</summary>
        public void ReflowWidth()
        {
            int w = Math.Max(100, (Parent?.ClientSize.Width ?? 780) - 24);
            if (lblTitle.Width != w) { lblTitle.Width = w; lblSpec.Width = w; lblMeta.Width = w; }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ReflowWidth();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // 底部分隔线
            using (var p = new Pen(BorderClr))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

        // ==================== 小工具 ====================

        private static double MemToGb(long memMb) => memMb / 1024.0;

        private static string Ago(long seconds)
        {
            if (seconds < 0) seconds = 0;
            if (seconds < 60) return seconds + " 秒";
            if (seconds < 3600) return (seconds / 60) + " 分钟";
            if (seconds < 86400) return (seconds / 3600) + " 小时";
            return (seconds / 86400) + " 天";
        }

        private static string ShortUuid(string uuid) =>
            string.IsNullOrEmpty(uuid) ? "" : uuid.Substring(0, Math.Min(12, uuid.Length)) + "…";
    }
}
