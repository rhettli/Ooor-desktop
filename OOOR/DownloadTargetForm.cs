using System;
using System.IO;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 下载前确认保存位置对话框（模型市场「下载」按钮弹出）：
    /// 选中默认目录或另存为其他目录，点「提交下载」后任务进入 DownloadManager 队列。
    /// </summary>
    public class DownloadTargetForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>用户确认的保存目录（DialogResult.OK 时有效）</summary>
        public string SaveDir { get; private set; }

        private readonly string _defaultDir;
        private readonly string _tag;
        private readonly string _fileName;
        private readonly long _size;
        private readonly Label _lblTitle;
        private readonly Label _lblSize;
        private readonly TextBox _txtDefault;
        private readonly TextBox _txtOther;
        private readonly RadioButton _rbDefault;
        private readonly RadioButton _rbOther;
        private readonly Button _btnBrowse;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public DownloadTargetForm(string tag, string fileName, long size, string defaultDir)
        {
            _defaultDir = defaultDir ?? "";
            _tag = tag ?? "";
            _fileName = fileName ?? "";
            _size = size;

            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new System.Drawing.Size(600, 268);
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);

            _lblTitle = new Label
            {
                AutoEllipsis = true,
                Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(14, 12),
                Size = new System.Drawing.Size(560, 24)
            };

            _lblSize = new Label
            {
                Location = new System.Drawing.Point(14, 40),
                AutoSize = true
            };

            _rbDefault = new RadioButton
            {
                Checked = true,
                Location = new System.Drawing.Point(14, 76),
                AutoSize = true,
                TabIndex = 0
            };
            _txtDefault = new TextBox
            {
                ReadOnly = true,
                Text = _defaultDir,
                Location = new System.Drawing.Point(36, 102),
                Size = new System.Drawing.Size(530, 26),
                TabIndex = 1
            };

            _rbOther = new RadioButton
            {
                Location = new System.Drawing.Point(14, 146),
                AutoSize = true,
                TabIndex = 2
            };
            _txtOther = new TextBox
            {
                Enabled = false,
                Location = new System.Drawing.Point(36, 172),
                Size = new System.Drawing.Size(430, 26),
                TabIndex = 3
            };
            _btnBrowse = new Button
            {
                Enabled = false,
                Location = new System.Drawing.Point(478, 170),
                Size = new System.Drawing.Size(88, 30),
                TabIndex = 4,
                UseVisualStyleBackColor = true
            };

            _btnOk = new Button
            {
                Location = new System.Drawing.Point(386, 218),
                Size = new System.Drawing.Size(94, 34),
                TabIndex = 5,
                UseVisualStyleBackColor = true
            };
            _btnCancel = new Button
            {
                Location = new System.Drawing.Point(492, 218),
                Size = new System.Drawing.Size(94, 34),
                TabIndex = 6,
                UseVisualStyleBackColor = true,
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] { _lblTitle, _lblSize, _rbDefault, _txtDefault,
                _rbOther, _txtOther, _btnBrowse, _btnOk, _btnCancel });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            _rbDefault.CheckedChanged += (s, e) => SyncInputState();
            _rbOther.CheckedChanged += (s, e) => SyncInputState();
            _txtOther.TextChanged += (s, e) => SyncInputState();
            _btnBrowse.Click += (s, e) => BrowseOther();
            _btnOk.Click += (s, e) => Submit();
            SyncInputState();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、说明、单选、按钮），并重填动态标题/大小行</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("dtf.title");

            _lblTitle.Text = string.Format(L.T("dtf.lbl.title"), _tag, Path.GetFileName(_fileName));
            _lblSize.Text = string.Format(L.T("dtf.lbl.size"), DownloadManager.FormatSize(_size));

            _rbDefault.Text = L.T("dtf.rb.default");
            _rbOther.Text = L.T("dtf.rb.other");
            _btnBrowse.Text = L.T("dtf.btn.browse");
            _btnOk.Text = L.T("dtf.btn.ok");
            _btnCancel.Text = L.T("dtf.btn.cancel");
        }

        /// <summary>单选与输入联动：只有「另存为」可编辑，提交按钮要求目录非空</summary>
        private void SyncInputState()
        {
            bool other = _rbOther.Checked;
            _txtOther.Enabled = other;
            _btnBrowse.Enabled = other;
            string dir = other ? _txtOther.Text.Trim() : _defaultDir;
            _btnOk.Enabled = !string.IsNullOrEmpty(dir) && dir.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }

        private void BrowseOther()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = L.T("dtf.browse.desc");
                string cur = _txtOther.Text.Trim();
                if (!string.IsNullOrEmpty(cur) && Directory.Exists(cur)) dlg.SelectedPath = cur;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtOther.Text = dlg.SelectedPath;
                    _rbOther.Checked = true;
                }
            }
        }

        private void Submit()
        {
            string dir = _rbOther.Checked ? _txtOther.Text.Trim() : _defaultDir;
            if (string.IsNullOrEmpty(dir))
            {
                MessageBox.Show(this, L.T("dtf.msg.dirEmpty"), L.T("dtf.caption.tip"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SaveDir = dir;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
