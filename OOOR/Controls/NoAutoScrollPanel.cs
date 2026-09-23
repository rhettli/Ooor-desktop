using System;
using System.Drawing;
using System.Windows.Forms;

namespace ooor.Controls
{
    /// <summary>
    /// 禁止"自动滚动到子控件"的 Panel（普通 Panel + AutoScroll 的行为修正）。
    ///
    /// 原生 ScrollableControl 行为：子控件获得焦点 / 布局变更时，容器会调用
    /// ScrollToControl() 把该控件滚进可视区。下载管理窗口里正在下载的行会频繁刷新
    /// （ProgressBar.Value / 状态文本约 200ms 一次），用户往上翻看历史任务时
    /// 会被反复拽回正在更新的行。
    ///
    /// 重写 ScrollToControl() 固定返回当前可视区左上角即可禁用该自动滚动；
    /// 滚动条保留，用户手动滚动（滚轮 / 拖动滚动条）与程序主动
    /// ScrollControlIntoView() 均不受影响。
    /// </summary>
    public class NoAutoScrollPanel : Panel
    {
        public NoAutoScrollPanel()
        {
            // 双缓冲：滚动/刷新不闪（Panel 默认不开）
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override Point ScrollToControl(Control activeControl)
        {
            // 关键：使用 AutoScrollPosition，它代表当前滚动偏移
            // AutoScrollPosition 是负数，取反得到视口原点
            Point currentViewOrigin = new Point(AutoScrollPosition.X, AutoScrollPosition.Y);

            return currentViewOrigin;
        }
 
    }
}
