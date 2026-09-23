using System;
using System.Windows.Forms;

namespace OoorFunc.Core
{
    /// <summary>
    /// 高危工具执行前的确认回调：true=放行，false=拒绝。
    /// UI 集成时把 AgentHost.Instance.Confirm 替换为"主线程弹 YesNo 对话框"的实现；
    /// 没接 UI 时走默认实现（MessageBox.Show，本线程弹窗）。
    /// </summary>
    public delegate bool AgentConfirm(string title, string detail);

    /// <summary>默认确认实现：直接 MessageBox 弹 YesNo（线程池线程上调用时由 WinForms 自动 marshal 到 UI 线程）。</summary>
    public static class AgentConfirmDefaults
    {
        public static bool MessageBoxYesNo(string title, string detail)
        {
            try
            {
                DialogResult r = MessageBox.Show(
                    detail ?? "",
                    title ?? "需要确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2);
                return r == DialogResult.Yes;
            }
            catch
            {
                // 极端环境无 WinForms（理论不会发生；OOOR 就是 WinForms 应用）— 拒绝执行
                return false;
            }
        }
    }
}