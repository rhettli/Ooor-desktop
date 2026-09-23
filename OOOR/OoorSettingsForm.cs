using System;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// ooor 加速源设置窗：服务地址 + 登录令牌（写入 {配置根}\ooor.conf）。
    /// M1 网关默认允许匿名下载，令牌可留空；关闭鉴权开关后需填写。
    /// </summary>
    public sealed class OoorSettingsForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private readonly TextBox _txtServer;
        private readonly TextBox _txtToken;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly Label _lblServer;
        private readonly Label _lblToken;
        private readonly Label _lblHint;

        public OoorSettingsForm()
        {
            Text = L.T("osf.title");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(460, 210);
            Font = new Font("Microsoft YaHei UI", 9F);

            _lblServer = new Label { Text = L.T("osf.server"), AutoSize = true, Location = new Point(14, 18) };
            _txtServer = new TextBox { Location = new Point(96, 15), Width = 340 };

            _lblToken = new Label { Text = L.T("osf.token"), AutoSize = true, Location = new Point(14, 54) };
            _txtToken = new TextBox { Location = new Point(96, 51), Width = 340 };

            _lblHint = new Label
            {
                Text = L.T("osf.hint"),
                AutoSize = false,
                Location = new Point(14, 90),
                Size = new Size(425, 40),
                ForeColor = SystemColors.GrayText
            };

            _btnOk = new Button { Text = L.T("osf.save"), DialogResult = DialogResult.None, Location = new Point(250, 138), Width = 90 };
            _btnCancel = new Button { Text = L.T("osf.cancel"), DialogResult = DialogResult.Cancel, Location = new Point(350, 138), Width = 90 };

            _btnOk.Click += (s, e) =>
            {
                string server = _txtServer.Text.Trim().TrimEnd('/');
                if (!string.IsNullOrEmpty(server) &&
                    !server.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, L.T("osf.msg.badUrl"), L.T("osf.msg.caption"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var s2 = OoorSettings.Load();
                s2.ServerUrl = server;
                s2.Token = _txtToken.Text.Trim();
                s2.Save();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.AddRange(new Control[] { _lblServer, _txtServer, _lblToken, _txtToken, _lblHint, _btnOk, _btnCancel });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            // 预填当前配置
            var cur = OoorSettings.Load();
            _txtServer.Text = cur.ServerUrl;
            _txtToken.Text = cur.Token;
        }

        /// <summary>按当前语言刷新窗口标题与全部控件文本</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("osf.title");
            _lblServer.Text = L.T("osf.server");
            _lblToken.Text = L.T("osf.token");
            _lblHint.Text = L.T("osf.hint");
            _btnOk.Text = L.T("osf.save");
            _btnCancel.Text = L.T("osf.cancel");
        }
    }
}
