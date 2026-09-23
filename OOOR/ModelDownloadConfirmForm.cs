using System;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 模型下载详情确认对话框：模型市场点「下载」后、选择保存位置之前弹出，
    /// 展示本次下载的完整信息（仓库 / 文件 / 大小 / 下载量 / 适配硬件 / 下载地址 / 保存目录 等），
    /// 用户点「确定」才继续，点「取消」中止。分片模型的整组下载提醒也一并显示在本窗口。
    /// </summary>
    internal static class ModelDownloadConfirmForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <param name="owner">父窗口</param>
        /// <param name="model">选中的仓库</param>
        /// <param name="file">要下载的 GGUF 文件</param>
        /// <param name="downloadUrl">实际下载地址</param>
        /// <param name="saveDir">默认保存目录</param>
        /// <param name="sourceName">下载源显示名（hf-mirror 直连 / HuggingFace 官方）</param>
        /// <param name="hardware">适配硬件建议（mmproj 为 "--"）</param>
        /// <param name="shardHint">分片提醒文本；非分片为 null</param>
        public static DialogResult Show(IWin32Window owner, HfModel model, HfFile file,
            string downloadUrl, string saveDir, string sourceName, string hardware, string shardHint)
        {
            using (var f = new Form())
            {
                f.Text = L.T("mdc.title");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.Font = SystemFonts.DefaultFont;
                f.ClientSize = new Size(640, 500);

                // 顶部标题
                var lblTitle = new Label
                {
                    Text = L.T("mdc.prompt"),
                    Font = new Font(f.Font, FontStyle.Bold),
                    Location = new Point(14, 14),
                    Size = new Size(612, 22)
                };

                // 详情表（两列：字段名 / 值）
                var rows = new[]
                {
                    new Row(L.T("mdc.row.repo"), model.id ?? ""),
                    new Row(L.T("mdc.row.path"), file.path ?? ""),
                    new Row(L.T("mdc.row.size"), FormatSize(file.size)),
                    new Row(L.T("mdc.row.type"), file.IsMmproj ? L.T("mdc.type.mmproj") : L.T("mdc.type.weight")),
                    new Row(L.T("mdc.row.downloads"), FormatCount(model.downloads)),
                    new Row(L.T("mdc.row.likes"), model.likes.ToString()),
                    new Row(L.T("mdc.row.updated"), FormatDate(model.lastModified)),
                    new Row(L.T("mdc.row.hardware"), string.IsNullOrEmpty(hardware) ? "-" : hardware),
                    new Row(L.T("mdc.row.source"), sourceName ?? ""),
                    new Row(L.T("mdc.row.url"), downloadUrl ?? ""),
                    new Row(L.T("mdc.row.savedir"), saveDir ?? "")
                };

                var table = new TableLayoutPanel
                {
                    Location = new Point(14, 44),
                    Size = new Size(612, rows.Length * RowHeight),
                    ColumnCount = 2,
                    RowCount = rows.Length
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92f));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

                var tip = new ToolTip();
                for (int i = 0; i < rows.Length; i++)
                {
                    table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

                    var lblName = new Label
                    {
                        Text = rows[i].Name + L.T("mdc.colon"),
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleLeft,
                        ForeColor = Color.Gray
                    };
                    var lblValue = new Label
                    {
                        Text = rows[i].Value,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoEllipsis = true
                    };
                    // 长内容（路径 / URL）悬停显示完整文本
                    tip.SetToolTip(lblValue, rows[i].Value);

                    table.Controls.Add(lblName, 0, i);
                    table.Controls.Add(lblValue, 1, i);
                }

                int tableBottom = 44 + rows.Length * RowHeight;

                // 分片提醒：仅分片模型显示（橙色警示）；换行后可能 3~4 行，给足高度
                var lblShard = new Label
                {
                    Location = new Point(14, tableBottom + 6),
                    Size = new Size(612, 72),
                    ForeColor = Color.FromArgb(180, 90, 0),
                    Text = shardHint ?? ""
                };

                // 底部按钮：确定 / 取消（取消在最右）
                int btnY = 500 - 34 - 14;
                var btnOk = new Button
                {
                    Text = L.T("mdc.ok"),
                    Size = new Size(90, 32),
                    DialogResult = DialogResult.OK
                };
                var btnCancel = new Button
                {
                    Text = L.T("mdc.cancel"),
                    Size = new Size(90, 32),
                    DialogResult = DialogResult.Cancel
                };
                btnCancel.Location = new Point(640 - 16 - 90, btnY);
                btnOk.Location = new Point(640 - 16 - 90 - 10 - 90, btnY);

                f.Controls.Add(lblTitle);
                f.Controls.Add(table);
                if (!string.IsNullOrEmpty(shardHint)) f.Controls.Add(lblShard);
                f.Controls.Add(btnOk);
                f.Controls.Add(btnCancel);
                f.AcceptButton = btnOk;
                f.CancelButton = btnCancel;
                f.ActiveControl = btnOk;

                return f.ShowDialog(owner);
            }
        }

        private const int RowHeight = 28;

        private sealed class Row
        {
            public string Name { get; private set; }
            public string Value { get; private set; }

            public Row(string name, string value)
            {
                Name = name;
                Value = value;
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";
            const double KB = 1024, MB = KB * 1024, GB = MB * 1024;
            if (bytes >= GB) return (bytes / GB).ToString("0.00") + " GB";
            if (bytes >= MB) return (bytes / MB).ToString("0.0") + " MB";
            if (bytes >= KB) return (bytes / KB).ToString("0") + " KB";
            return bytes + " B";
        }

        private static string FormatCount(long n)
        {
            if (n >= 1000000) return (n / 1000000.0).ToString("0.0") + "M";
            if (n >= 1000) return (n / 1000.0).ToString("0.0") + "K";
            return n.ToString();
        }

        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "-";
            return DateTime.TryParse(iso, out var dt)
                ? dt.ToString("yyyy-MM-dd")
                : (iso.Length > 10 ? iso.Substring(0, 10) : iso);
        }
    }
}
