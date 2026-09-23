using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ooor.Core;

namespace ooor.Controls
{
    /// <summary>
    /// 分块进度条（自绘）：每个分块一格"外框 + 进度填充"的竖格，
    /// 块内按已完成字节比例从底部向上填充，下载多少填多少，完成整格变绿。
    /// 多线程下载时实时刷新；单线程/未启用时显示"单线程"灰字。
    ///
    /// 设计要点：
    ///   - 唯一自绘控件（其余全部用原生控件，无闪烁）
    ///   - 高度 20px，块宽自适应（≥2px），块间 1px 间隙，每格 1px 外框
    ///   - 颜色：空槽=淡蓝，填充中=蓝，完成=绿，失败=红，暂停=橙
    /// </summary>
    public class ChunkSparkline : Control
    {
        private ChunkSnapshot[] _snaps;
        private DownloadState _state = DownloadState.Queued;
        private int _fallbackSlots;   // 无快照时的空槽占位数（= 任务线程数），用于暂停/排队的多线程任务

        public ChunkSparkline()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            ForeColor = Color.FromArgb(0x9E, 0x9E, 0x9E);
            Size = new Size(200, 20);
        }

        /// <summary>设置无快照时的空槽占位数（任务分块数），0 表示未知</summary>
        public void SetFallbackSlots(int slots)
        {
            _fallbackSlots = slots;
            Invalidate();
        }

        /// <summary>设置当前分块快照（来自 DownloadManager.GetChunkSnapshots）</summary>
        public void SetSnapshots(ChunkSnapshot[] snaps, DownloadState state)
        {
            // 暂停收尾会清空 LiveChunks 导致快照为 null：窗口内暂停时保留最后一次分块画面（颜色随暂停态变橙）
            if (snaps == null && _snaps != null && _snaps.Length > 0 && state == DownloadState.Paused)
            {
                _state = state;
                Invalidate();
                return;
            }
            _snaps = snaps;
            _state = state;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;
            var r = ClientRectangle;

            if (_snaps == null || _snaps.Length == 0)
            {
                // 已完成收尾后分块快照被清空：不再画"单线程"占位（避免被误读成用单线程下载）
                if (_state == DownloadState.Completed) return;
                // 多线程任务无快照：暂停/排队画空槽；下载中也画空槽（首下探测期/续传探测期还没建好快照，此时还没定论是否回退单线程）
                if (_fallbackSlots > 1 && (_state == DownloadState.Paused || _state == DownloadState.Queued
                    || _state == DownloadState.Downloading))
                {
                    DrawEmptySlots(g, r, _fallbackSlots);
                    return;
                }
                // 单线程模式 / 探测回退：如实显示"单线程"灰字（不画占位纹，避免误导为分块在跑）
                string label = "单线程";
                using (var f = new Font("Microsoft YaHei UI", 8.5f))
                    TextRenderer.DrawText(g, label, f, r, ForeColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                return;
            }

            const int gap = 1;
            int n = _snaps.Length;
            int barW = Math.Max(2, (r.Width - (n - 1) * gap) / n);
            int innerH = Math.Max(1, r.Height - 2);        // 上下各留 1px 给外框
            bool thin = barW <= 3;                          // 块太多太窄：省略外框只画实心竖条

            using (var borderPen = new Pen(Color.FromArgb(0xB0, 0xC4, 0xD8)))
            using (var emptyBrush = new SolidBrush(Color.FromArgb(0xE3, 0xF2, 0xFD)))
            {
                for (int i = 0; i < n; i++)
                {
                    var s = _snaps[i];
                    long sz = s.End - s.Start + 1;
                    float ratio = sz > 0 ? Math.Min(1f, (float)s.Done / sz) : 0f;
                    int x = r.X + i * (barW + gap);

                    if (thin)
                    {
                        using (var br = new SolidBrush(PickColor(ratio, _state)))
                            g.FillRectangle(br, x, r.Y, barW, r.Height);
                        continue;
                    }

                    // 1) 空槽底色
                    g.FillRectangle(emptyBrush, x, r.Y, barW, r.Height);

                    // 2) 进度填充：从底部向上长，填多少算多少；完成时整格覆盖为绿
                    int fillH = (int)Math.Round(innerH * ratio);
                    if (fillH > 0)
                    {
                        using (var br = new SolidBrush(PickColor(ratio, _state)))
                            g.FillRectangle(br, x + 1, r.Y + r.Height - 1 - fillH, barW - 2, fillH);
                    }

                    // 3) 1px 外框
                    g.DrawRectangle(borderPen, x, r.Y, barW - 1, r.Height - 1);
                }
            }
        }

        /// <summary>画 n 个空槽（淡蓝底 + 1px 外框，无填充），用于无快照的多线程暂停/排队任务</summary>
        private void DrawEmptySlots(Graphics g, Rectangle r, int n)
        {
            const int gap = 1;
            int barW = Math.Max(2, (r.Width - (n - 1) * gap) / n);
            using (var borderPen = new Pen(Color.FromArgb(0xB0, 0xC4, 0xD8)))
            using (var emptyBrush = new SolidBrush(Color.FromArgb(0xE3, 0xF2, 0xFD)))
            {
                for (int i = 0; i < n; i++)
                    g.FillRectangle(emptyBrush, r.X + i * (barW + gap), r.Y, barW, r.Height);
                for (int i = 0; i < n; i++)
                    g.DrawRectangle(borderPen, r.X + i * (barW + gap), r.Y, barW - 1, r.Height - 1);
            }
        }

        private static Color PickColor(float ratio, DownloadState state)
        {
            if (ratio >= 1f) return Color.FromArgb(0x4C, 0xAF, 0x50);   // 完成=绿
            switch (state)
            {
                case DownloadState.Failed: return Color.FromArgb(0xF4, 0x43, 0x36);   // 失败=红
                case DownloadState.Paused: return Color.FromArgb(0xFB, 0x8C, 0x00);   // 暂停=橙
                default: return Color.FromArgb(0x21, 0x96, 0xF3);                     // 下载中=蓝
            }
        }
    }
}