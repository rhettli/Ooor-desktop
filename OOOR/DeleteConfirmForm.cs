using System;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 删除下载任务确认对话框：
    ///   - 删除所有：删任务记录 + 删原文件（含未下完的半成品）
    ///   - 删除记录保留文件：仅删任务记录
    ///   - 取消：不删
    /// 无文件时「删除所有」自动禁用。
    /// 返回值映射：Yes=删除所有 / No=删除记录保留文件 / Cancel=取消。
    /// </summary>
    internal static class DeleteConfirmForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        public static DialogResult Show(IWin32Window owner, string fileName,
            bool fileExists, string fileInfo)
        {
            using (var f = new Form())
            {
                f.Text = L.T("dcf.title");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.Font = SystemFonts.DefaultFont;
                f.ClientSize = new Size(460, 190);

                // 顶部询问 + 文件信息
                string msg;
                if (fileExists)
                    msg = string.Format(L.T("dcf.msg.exists"), fileName, fileInfo ?? "");
                else
                    msg = string.Format(L.T("dcf.msg.nofile"), fileName);
                var lbl = new Label
                {
                    Text = msg,
                    Location = new Point(16, 16),
                    Size = new Size(428, 80),
                    AutoEllipsis = true
                };

                // 三按钮：删除所有 / 删除记录保留文件 / 取消（右对齐，取消在最右）
                var btnCancel = new Button
                {
                    Text = L.T("dcf.cancel"),
                    Size = new Size(80, 32),
                    DialogResult = DialogResult.Cancel
                };
                var btnKeepFile = new Button
                {
                    Text = L.T("dcf.keep"),
                    Size = new Size(150, 32),
                    DialogResult = DialogResult.No
                };
                var btnDeleteAll = new Button
                {
                    Text = L.T("dcf.deleteall"),
                    Size = new Size(110, 32),
                    DialogResult = DialogResult.Yes
                };

                int y = 190 - 32 - 14;
                btnCancel.Location = new Point(460 - 14 - 80, y);
                btnKeepFile.Location = new Point(460 - 14 - 80 - 8 - 150, y);
                btnDeleteAll.Location = new Point(460 - 14 - 80 - 8 - 150 - 8 - 110, y);

                // 无文件时：删除所有 无意义 → 禁用
                if (!fileExists)
                    btnDeleteAll.Enabled = false;

                f.Controls.Add(lbl);
                f.Controls.Add(btnDeleteAll);
                f.Controls.Add(btnKeepFile);
                f.Controls.Add(btnCancel);
                f.CancelButton = btnCancel;
                // 默认焦点给取消，避免误回车删数据
                f.ActiveControl = btnCancel;

                return f.ShowDialog(owner);
            }
        }
    }
}
