using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>快速绑定 MCP 服务器（扫描 mcp 目录，勾选即写回 AgentRecord.BoundMcp）。</summary>
    public class BindMcpForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private readonly AgentRecord _agent;
        private readonly CheckedListBox _clb;
        private readonly Label _lbl;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private readonly List<McpServerInfo> _mcps;

        public BindMcpForm(AgentRecord agent)
        {
            _agent = agent ?? throw new ArgumentNullException("agent");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 360);
            Font = new Font("Microsoft YaHei UI", 9f);

            _lbl = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(536, 36)
            };
            Controls.Add(_lbl);

            _clb = new CheckedListBox
            {
                Location = new Point(12, 52),
                Size = new Size(536, 250),
                CheckOnClick = true
            };
            Controls.Add(_clb);

            _btnOk = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(380, 312), Size = new Size(80, 28)
            };
            _btnCancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(468, 312), Size = new Size(80, 28)
            };
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            _mcps = McpScanner.Scan();
            var bound = _agent.BoundMcp ?? new List<string>();
            if (_mcps.Count == 0)
            {
                _clb.Items.Add(L.T("bmc.empty"));
                _clb.Enabled = false;
            }
            else
            {
                foreach (var m in _mcps)
                {
                    int idx = _clb.Items.Add(m.Name + "  —  " + m.Path);
                    if (bound.Contains(m.Id)) _clb.SetItemChecked(idx, true);
                }
            }

            _btnOk.Click += (s, e) => Commit();
        }

        /// <summary>按当前语言刷新标题、说明、按钮；无 MCP 时同步占位提示文本</summary>
        protected override void ApplyLanguage()
        {
            Text = string.Format(L.T("bmc.title"), _agent.Name);
            _lbl.Text = L.T("bmc.lbl.tip");
            _btnOk.Text = L.T("bmc.btn.ok");
            _btnCancel.Text = L.T("bmc.btn.cancel");

            if (_mcps.Count == 0 && _clb.Items.Count > 0)
                _clb.Items[0] = L.T("bmc.empty");
        }

        private void Commit()
        {
            if (_mcps.Count == 0) return;
            var list = new List<string>();
            for (int i = 0; i < _clb.Items.Count && i < _mcps.Count; i++)
            {
                if (_clb.GetItemChecked(i)) list.Add(_mcps[i].Id);
            }
            _agent.BoundMcp = list;
        }
    }
}
