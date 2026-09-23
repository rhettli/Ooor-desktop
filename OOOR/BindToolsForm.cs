using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>快速绑定内置函数（勾选即写回 AgentRecord.BoundTools）。</summary>
    public class BindToolsForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private readonly AgentRecord _agent;
        private readonly CheckedListBox _clb;
        private readonly Label _lbl;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;

        public BindToolsForm(AgentRecord agent)
        {
            _agent = agent ?? throw new ArgumentNullException("agent");
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(520, 420);
            Font = new Font("Microsoft YaHei UI", 9f);

            _lbl = new Label
            {
                Location = new Point(12, 12),
                Size = new Size(496, 36)
            };
            Controls.Add(_lbl);

            _clb = new CheckedListBox
            {
                Location = new Point(12, 52),
                Size = new Size(496, 320),
                CheckOnClick = true
            };
            Controls.Add(_clb);

            _btnOk = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(340, 382), Size = new Size(80, 28)
            };
            _btnCancel = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(428, 382), Size = new Size(80, 28)
            };
            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            FillTools(_agent.BoundTools ?? new List<string>());

            _btnOk.Click += (s, e) => Commit();
        }

        /// <summary>按当前语言填充内置工具列表（描述走 bts.tool.&lt;工具名&gt; 翻译），并恢复勾选</summary>
        private void FillTools(List<string> bound)
        {
            _clb.BeginUpdate();
            try
            {
                _clb.Items.Clear();
                foreach (var kv in BuiltinToolCatalog.All)
                {
                    int idx = _clb.Items.Add(kv.Key + " — " + L.T("bts.tool." + kv.Key));
                    if (bound.Contains(kv.Key)) _clb.SetItemChecked(idx, true);
                }
            }
            finally
            {
                _clb.EndUpdate();
            }
        }

        /// <summary>按当前语言刷新标题、说明、按钮；重建工具列表并保留勾选状态</summary>
        protected override void ApplyLanguage()
        {
            Text = string.Format(L.T("bts.title"), _agent.Name);
            _lbl.Text = L.T("bts.lbl.tip");
            _btnOk.Text = L.T("bts.btn.ok");
            _btnCancel.Text = L.T("bts.btn.cancel");

            // 描述文本随语言变化，重建列表（先记下当前勾选的工具名）
            var checkedKeys = new List<string>();
            for (int i = 0; i < _clb.Items.Count; i++)
            {
                if (_clb.GetItemChecked(i)) checkedKeys.Add(GetToolKey(_clb.Items[i]));
            }
            FillTools(checkedKeys);
        }

        /// <summary>从列表项文本（工具名 — 描述）解析工具名</summary>
        private static string GetToolKey(object item)
        {
            string s = item.ToString();
            int sep = s.IndexOf(" — ", StringComparison.Ordinal);
            return sep > 0 ? s.Substring(0, sep) : s;
        }

        private void Commit()
        {
            var list = new List<string>();
            for (int i = 0; i < _clb.Items.Count; i++)
            {
                if (_clb.GetItemChecked(i)) list.Add(GetToolKey(_clb.Items[i]));
            }
            _agent.BoundTools = list;
        }
    }
}
