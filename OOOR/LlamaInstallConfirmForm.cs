using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// llama.cpp 版本安装确认对话框：点击各「下载 xx 版本」按钮后弹出，
    /// 列出本次需要下载的文件（文件名 / 大小 / 解压目录）与合计大小，
    /// 用户确认后才真正入队。
    /// </summary>
    internal static class LlamaInstallConfirmForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>一条待下载文件：压缩包名、字节数、下载后解压目录</summary>
        public sealed class Item
        {
            public string FileName { get; private set; }
            public long SizeBytes { get; private set; }
            public string ExtractDir { get; private set; }

            public Item(string fileName, long sizeBytes, string extractDir)
            {
                FileName = fileName;
                SizeBytes = sizeBytes;
                ExtractDir = extractDir;
            }
        }

        /// <param name="owner">父窗口</param>
        /// <param name="kindName">安装类型名（如 CPU / CPU+CUDA / Vulkan）</param>
        /// <param name="tagName">选中的版本 tag</param>
        /// <param name="items">待下载文件清单（至少 1 条）</param>
        public static DialogResult Show(IWin32Window owner, string kindName, string tagName,
            IList<Item> items)
        {
            using (var f = new Form())
            {
                f.Text = L.T("lic.title");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.Font = SystemFonts.DefaultFont;
                f.ClientSize = new Size(660, 400);

                // 顶部询问
                var lblQuestion = new Label
                {
                    Text = string.Format(L.T("lic.question"), kindName, tagName ?? "", items.Count),
                    Location = new Point(16, 14),
                    Size = new Size(628, 22)
                };

                // 文件清单：文件名 / 大小 / 解压目录
                var list = new ListView
                {
                    Location = new Point(16, 42),
                    Size = new Size(628, 270),
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = true,
                    HeaderStyle = ColumnHeaderStyle.Nonclickable
                };
                list.Columns.Add(L.T("lic.col.file"), 250);
                list.Columns.Add(L.T("lic.col.size"), 80);
                list.Columns.Add(L.T("lic.col.dir"), 290);

                long total = 0;
                foreach (var it in items)
                {
                    total += it.SizeBytes;
                    var lvi = new ListViewItem(it.FileName ?? "");
                    lvi.SubItems.Add(FormatSize(it.SizeBytes));
                    lvi.SubItems.Add(it.ExtractDir ?? "");
                    list.Items.Add(lvi);
                }

                // 合计大小
                var lblTotal = new Label
                {
                    Text = string.Format(L.T("lic.total"), FormatSize(total)),
                    Location = new Point(16, 320),
                    Size = new Size(400, 22)
                };

                // 按钮：确认 / 取消（右对齐，取消在最右）
                int btnY = 400 - 34 - 14;
                var btnOk = new Button
                {
                    Text = L.T("lic.ok"),
                    Size = new Size(90, 32),
                    DialogResult = DialogResult.OK
                };
                var btnCancel = new Button
                {
                    Text = L.T("lic.cancel"),
                    Size = new Size(90, 32),
                    DialogResult = DialogResult.Cancel
                };
                btnCancel.Location = new Point(660 - 16 - 90, btnY);
                btnOk.Location = new Point(660 - 16 - 90 - 10 - 90, btnY);

                f.Controls.Add(lblQuestion);
                f.Controls.Add(list);
                f.Controls.Add(lblTotal);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);
                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;
                f.ActiveControl = btnOk;

                return f.ShowDialog(owner);
            }
        }

        /// <summary>字节数格式化为 MB / GB</summary>
        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";
            const double mb = 1024.0 * 1024.0;
            if (bytes >= 1024L * 1024 * 1024)
                return (bytes / mb / 1024.0).ToString("0.00") + " GB";
            return (bytes / mb).ToString("0.0") + " MB";
        }
    }
}
