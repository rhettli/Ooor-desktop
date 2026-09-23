using System;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 「查看下载地址」只读对话框：
    ///   - 大号多行文本框显示完整 URL（不可编辑）
    ///   - 「复制地址」按钮把 URL 写入剪贴板，按钮短暂变 "已复制 ✓" 后还原
    ///   - 「关闭」按钮退出（ESC / 回车 都触发）
    /// 用途：右键菜单"查看下载地址"调用。
    /// </summary>
    internal static class ViewUrlDialogForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <param name="owner">父窗口</param>
        /// <param name="url">要显示/复制的地址</param>
        /// <param name="fileName">默认标题中显示的文件名（自定义标题时可忽略）</param>
        /// <param name="title">自定义窗口标题；null 时用默认「查看下载地址」标题</param>
        /// <param name="hint">自定义顶部说明；null 时用默认说明</param>
        /// <param name="secret">要展示的密码/令牌；非空时在地址下方显示「密码：xxx」</param>
        public static void Show(IWin32Window owner, string url, string fileName,
            string title = null, string hint = null, string secret = null)
        {
            using (var f = new Form())
            {
                f.Text = title ?? (L.T("vud.title") + " - " + (fileName ?? ""));
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.ClientSize = new Size(720, 260);

                var toolTip = new ToolTip();

                // 顶部说明文字（固定高度，避免与下方密码行重叠）
                var lblHint = new Label
                {
                    Text = hint ?? L.T("vud.hint"),
                    Location = new Point(12, 8),
                    Size = new Size(696, 32),
                    AutoEllipsis = true,
                    ForeColor = SystemColors.GrayText
                };

                // 密码行：仅在提供了 secret 时显示（固定在 hint 下方一行）
                Label lblSecret = null;
                int txtTop;
                if (!string.IsNullOrEmpty(secret))
                {
                    lblSecret = new Label
                    {
                        Text = string.Format(L.T("vud.secret"), secret),
                        Location = new Point(12, 44),
                        Size = new Size(696, 20),
                        AutoEllipsis = true,
                        ForeColor = SystemColors.GrayText,
                        Font = new Font("Consolas", 9F)
                    };
                    toolTip.SetToolTip(lblSecret, secret);
                    txtTop = 68;
                }
                else
                {
                    txtTop = 44;
                }

                // 只读多行文本框（顶部留出 hint + 密码行空间后填满中部，底部留给按钮栏）
                int txtH = 200 - txtTop;
                var txt = new TextBox
                {
                    Text = url ?? "",
                    ReadOnly = true,
                    Multiline = true,
                    WordWrap = false,
                    ScrollBars = ScrollBars.Both,
                    Location = new Point(12, txtTop),
                    Size = new Size(696, txtH),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    BackColor = SystemColors.Info,
                    Font = new Font("Consolas", 9.5F),
                    BorderStyle = BorderStyle.FixedSingle
                };
                // 打开窗体时默认全选，方便直接 Ctrl+C
                txt.Enter += (s, e) => txt.SelectAll();

                // 底部按钮：复制地址 + 关闭（右对齐）
                var btnCopy = new Button
                {
                    Text = L.T("vud.btn.copy"),
                    Size = new Size(110, 32)
                };
                var btnClose = new Button
                {
                    Text = L.T("vud.btn.close"),
                    Size = new Size(90, 32),
                    DialogResult = DialogResult.Cancel
                };
                // 右对齐坐标（参考：f.ClientSize.Width=720，底部 y=260-32-12=216）
                btnCopy.Location = new Point(720 - 12 - 90 - 8 - 110, 216);
                btnClose.Location = new Point(720 - 12 - 90, 216);

                btnCopy.Click += (s, e) =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(url))
                        {
                            MessageBox.Show(f, L.T("vud.msg.noUrl"), L.T("vud.caption"),
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        Clipboard.SetText(url);
                        btnCopy.Enabled = false;
                        btnCopy.Text = L.T("vud.btn.copied");

                        // 1.2 秒后还原按钮文字（用 UI 线程 Timer，不阻塞）
                        var t = new Timer { Interval = 1200 };
                        t.Tick += (s2, e2) =>
                        {
                            t.Stop();
                            t.Dispose();
                            if (!f.IsDisposed && !btnCopy.IsDisposed)
                            {
                                btnCopy.Text = L.T("vud.btn.copy");
                                btnCopy.Enabled = true;
                            }
                        };
                        t.Start();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(f, string.Format(L.T("vud.msg.copyFail"), ex.Message), L.T("vud.caption"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                f.Controls.Add(lblHint);
                if (lblSecret != null) f.Controls.Add(lblSecret);
                f.Controls.Add(txt);
                f.Controls.Add(btnCopy);
                f.Controls.Add(btnClose);
                f.AcceptButton = btnClose;
                f.CancelButton = btnClose;

                f.ShowDialog(owner);
            }
        }
    }
}