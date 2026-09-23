using System;
using System.Drawing;
using System.Windows.Forms;

namespace ooor
{
    /// <summary>简单文本输入对话框（单行）：确定返回 true 并回写 text</summary>
    internal static class InputBox
    {
        public static bool Show(IWin32Window owner, string title, string label, ref string text)
        {
            using (var f = new Form())
            {
                f.Text = title ?? "";
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.ClientSize = new Size(420, 140);

                var lbl = new Label
                {
                    Text = label ?? "",
                    AutoSize = true,
                    Location = new Point(12, 14),
                    MaximumSize = new Size(396, 0)
                };
                var txt = new TextBox
                {
                    Text = text ?? "",
                    Location = new Point(12, 46),
                    Size = new Size(396, 27)
                };
                var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Size = new Size(88, 30) };
                var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Size = new Size(88, 30) };
                ok.Location = new Point(396 - 88 - 12 - 88, 98);
                cancel.Location = new Point(396 - 88, 98);

                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                text = txt.Text;
                return true;
            }
        }
    }
}
