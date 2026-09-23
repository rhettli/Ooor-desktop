using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 更多参数窗口：ListView 列出 llama-server 全部可选启动参数，
    /// 列：参数名称 / 参数值 / 参数描述。
    /// 双击行或右击「修改值」编辑；值即时写入 server_params.conf，
    /// 启动服务时按目录顺序追加到命令行（空值 = 不传该参数）。
    /// 界面布局见 MoreParamsForm.Designer.cs。
    /// </summary>
    internal sealed partial class MoreParamsForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        public MoreParamsForm()
        {
            InitializeComponent();
            ReloadList();
        }

        /// <summary>语言切换时刷新设计器控件文本，并通过 ReloadList() 刷新动态内容</summary>
        protected override void ApplyLanguage()
        {
            lblTip.Text = L.T("mpf.tip");

            colParamName.Text = L.T("mpf.col.name");
            colParamValue.Text = L.T("mpf.col.value");
            colParamDesc.Text = L.T("mpf.col.desc");

            menuEditValue.Text = L.T("mpf.menu.editValue");
            新建参数ToolStripMenuItem.Text = L.T("mpf.menu.newParam");
            编辑参数信息ToolStripMenuItem.Text = L.T("mpf.menu.editInfo");
            删除参数ToolStripMenuItem.Text = L.T("mpf.menu.deleteParam");

            // 窗口标题与列表内动态文本（含「（未设置）」）一并刷新
            ReloadList();
        }

        /// <summary>当前选中的参数定义；未选中为 null</summary>
        private ServerParam SelectedParam
        {
            get { return listParams.SelectedItems.Count == 0 ? null : listParams.SelectedItems[0].Tag as ServerParam; }
        }

        /// <summary>右键命中行时先选中该行，保证菜单作用于可见的选中行</summary>
        private void ListParams_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = listParams.HitTest(e.Location);
            if (hit.Item != null) hit.Item.Selected = true;
        }

        /// <summary>双击行直接编辑</summary>
        private void ListParams_ItemActivate(object sender, EventArgs e)
        {
            EditSelected();
        }

        /// <summary>右击菜单「修改值」</summary>
        private void MenuEditValue_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        /// <summary>右键菜单弹出前：选中内置参数时禁用「编辑参数信息」「删除参数」</summary>
        private void ContextMenuStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var p = SelectedParam;
            bool custom = p != null && p.IsCustom;
            编辑参数信息ToolStripMenuItem.Enabled = custom;
            删除参数ToolStripMenuItem.Enabled = custom;
        }

        /// <summary>右击菜单「新建参数」</summary>
        private void MenuNewParam_Click(object sender, EventArgs e)
        {
            string name, desc;
            ParamKind kind;
            string[] options;
            if (!ShowNewParamDialog(out name, out desc, out kind, out options)) return;

            try
            {
                ServerParams.AddCustomParam(name, desc, kind, options);
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mpf.newFail"), ex.Message), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>右击菜单「编辑参数信息」（仅自定义参数可改名称/备注，类型不可改）</summary>
        private void MenuEditInfo_Click(object sender, EventArgs e)
        {
            var p = SelectedParam;
            if (p == null)
            {
                MessageBox.Show(this, L.T("mpf.selectFirst"), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!p.IsCustom)
            {
                MessageBox.Show(this, L.T("mpf.builtinNoEdit"), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string newName, desc;
            if (!ShowEditInfoDialog(p, out newName, out desc)) return;

            try
            {
                ServerParams.UpdateCustomParam(p, newName, desc);
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mpf.saveInfoFail"), ex.Message), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>右击菜单「删除参数」（仅自定义参数可删，含确认提示）</summary>
        private void MenuDeleteParam_Click(object sender, EventArgs e)
        {
            var p = SelectedParam;
            if (p == null)
            {
                MessageBox.Show(this, L.T("mpf.selectFirst"), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!p.IsCustom)
            {
                MessageBox.Show(this, L.T("mpf.builtinNoDelete"), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this, string.Format(L.T("mpf.deleteConfirm"), p.Name),
                    L.T("mpf.deleteTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                ServerParams.RemoveCustomParam(p);
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mpf.deleteFail"), ex.Message), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>新建参数对话框：输入名称、类型、候选值（Enum）、备注</summary>
        private static bool ShowNewParamDialog(out string name, out string desc, out ParamKind kind, out string[] options)
        {
            name = null; desc = null; kind = ParamKind.Text; options = null;

            using (var f = new Form())
            {
                f.Text = L.T("mpf.dlg.newTitle");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(460, 270);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;

                var lblName = new Label { Text = L.T("mpf.lbl.name"), AutoSize = true, Location = new Point(12, 14) };
                var txtName = new TextBox { Location = new Point(100, 12), Size = new Size(348, 31) };
                var tip = new Label
                {
                    Text = L.T("mpf.nameHint"),
                    AutoSize = true, ForeColor = Color.Gray, Location = new Point(100, 40)
                };

                var lblKind = new Label { Text = L.T("mpf.lbl.kind"), AutoSize = true, Location = new Point(12, 64) };
                var cmbKind = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(100, 62), Size = new Size(348, 31)
                };
                cmbKind.Items.AddRange(new object[] {
                    L.T("mpf.kind.bool"), L.T("mpf.kind.int"), L.T("mpf.kind.float"),
                    L.T("mpf.kind.text"), L.T("mpf.kind.enum") });
                cmbKind.SelectedIndex = 3;

                var lblOpts = new Label { Text = L.T("mpf.lbl.options"), AutoSize = true, Location = new Point(12, 114) };
                var txtOpts = new TextBox { Location = new Point(100, 112), Size = new Size(348, 31) };
                var optsTip = new Label
                {
                    Text = L.T("mpf.optionsHint"),
                    AutoSize = true, ForeColor = Color.Gray, Location = new Point(100, 140)
                };

                var lblDesc = new Label { Text = L.T("mpf.lbl.desc"), AutoSize = true, Location = new Point(12, 164) };
                var txtDesc = new TextBox { Location = new Point(100, 162), Size = new Size(348, 31) };

                Action syncOpts = () =>
                {
                    bool isEnum = cmbKind.SelectedIndex == 4;
                    txtOpts.Enabled = isEnum;
                    if (!isEnum) txtOpts.Text = "";
                };
                cmbKind.SelectedIndexChanged += (s, ev) => syncOpts();
                syncOpts();

                var ok = new Button { Text = L.T("mpf.common.ok"), DialogResult = DialogResult.OK, Size = new Size(88, 30) };
                var cancel = new Button { Text = L.T("mpf.common.cancel"), DialogResult = DialogResult.Cancel, Size = new Size(88, 30) };
                ok.Location = new Point(460 - 88 - 12 - 88 - 12, 220);
                cancel.Location = new Point(460 - 88 - 12, 220);

                f.Controls.AddRange(new Control[] { lblName, txtName, tip, lblKind, cmbKind,
                    lblOpts, txtOpts, optsTip, lblDesc, txtDesc, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog() != DialogResult.OK) return false;

                string n = (txtName.Text ?? "").Trim();
                if (n.Length == 0)
                {
                    MessageBox.Show(f, L.T("mpf.nameEmpty"), L.T("mpf.common.error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                name = n;
                desc = (txtDesc.Text ?? "").Trim();
                kind = IndexToKind(cmbKind.SelectedIndex);
                if (kind == ParamKind.Enum)
                {
                    string raw = (txtOpts.Text ?? "").Trim();
                    options = raw.Length > 0
                        ? raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray()
                        : new string[0];
                    if (options.Length == 0)
                    {
                        MessageBox.Show(f, L.T("mpf.enumNeedOption"), L.T("mpf.common.error"),
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
                else
                {
                    options = new string[0];
                }
                return true;
            }
        }

        /// <summary>编辑参数信息对话框：仅名称 + 备注可改，类型只读显示</summary>
        private static bool ShowEditInfoDialog(ServerParam p, out string newName, out string desc)
        {
            newName = null; desc = null;

            using (var f = new Form())
            {
                f.Text = L.T("mpf.dlg.editInfoTitle");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(460, 210);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;

                var lblName = new Label { Text = L.T("mpf.lbl.name"), AutoSize = true, Location = new Point(12, 14) };
                var txtName = new TextBox { Text = p.Name, Location = new Point(100, 12), Size = new Size(348, 31) };

                var lblKind = new Label { Text = L.T("mpf.lbl.kind"), AutoSize = true, Location = new Point(12, 50) };
                var lblKindVal = new Label
                {
                    Text = KindToText(p.Kind), AutoSize = true,
                    ForeColor = Color.Gray, Location = new Point(100, 50)
                };

                var lblDesc = new Label { Text = L.T("mpf.lbl.desc"), AutoSize = true, Location = new Point(12, 86) };
                var txtDesc = new TextBox { Text = p.Desc, Location = new Point(100, 84), Size = new Size(348, 31) };

                var ok = new Button { Text = L.T("mpf.common.ok"), DialogResult = DialogResult.OK, Size = new Size(88, 30) };
                var cancel = new Button { Text = L.T("mpf.common.cancel"), DialogResult = DialogResult.Cancel, Size = new Size(88, 30) };
                ok.Location = new Point(460 - 88 - 12 - 88 - 12, 160);
                cancel.Location = new Point(460 - 88 - 12, 160);

                f.Controls.AddRange(new Control[] { lblName, txtName, lblKind, lblKindVal, lblDesc, txtDesc, ok, cancel });
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog() != DialogResult.OK) return false;

                newName = (txtName.Text ?? "").Trim();
                if (newName.Length == 0)
                {
                    MessageBox.Show(f, L.T("mpf.nameEmpty"), L.T("mpf.common.error"),
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                desc = (txtDesc.Text ?? "").Trim();
                return true;
            }
        }

        private static ParamKind IndexToKind(int idx)
        {
            switch (idx)
            {
                case 0: return ParamKind.Bool;
                case 1: return ParamKind.Int;
                case 2: return ParamKind.Float;
                case 3: return ParamKind.Text;
                case 4: return ParamKind.Enum;
                default: return ParamKind.Text;
            }
        }

        private static string KindToText(ParamKind k)
        {
            switch (k)
            {
                case ParamKind.Bool: return L.T("mpf.kind.bool");
                case ParamKind.Int: return L.T("mpf.kind.int");
                case ParamKind.Float: return L.T("mpf.kind.float");
                case ParamKind.Text: return L.T("mpf.kind.text");
                case ParamKind.Enum: return L.T("mpf.kind.enum");
                default: return k.ToString();
            }
        }

        /// <summary>按目录顺序填充列表（值列显示当前值，空值灰色显示「（未设置）」）</summary>
        private void ReloadList()
        {
            var values = ServerParams.LoadValues();

            listParams.BeginUpdate();
            try
            {
                listParams.Items.Clear();
                foreach (var p in ServerParams.Catalog)
                {
                    string value;
                    values.TryGetValue(p.Name, out value);
                    bool set = !string.IsNullOrEmpty(value);

                    var item = new ListViewItem(p.Name)
                    {
                        UseItemStyleForSubItems = true,
                        Tag = p,
                        ToolTipText = p.Desc
                    };

                    if (!set)
                    {
                        item.ForeColor = Color.Black;
                    }
                    else
                    {
                        item.ForeColor = Color.Green;
                    }

                    item.SubItems.Add(set ? value : L.T("mpf.notSet"));
                    item.SubItems.Add(p.Desc);
                    listParams.Items.Add(item);
                }

                int enabled = 0;
                foreach (var pv in ServerParams.Catalog)
                {
                    string v;
                    if (values.TryGetValue(pv.Name, out v) && !string.IsNullOrEmpty(v)) enabled++;
                }
                Text = string.Format(L.T("mpf.titleCount"), enabled, listParams.Items.Count);
            }
            finally
            {
                listParams.EndUpdate();
            }
        }

        /// <summary>双击 / 右击「修改值」：按参数类型弹出对应编辑器并保存</summary>
        private void EditSelected()
        {
            var p = SelectedParam;
            if (p == null)
            {
                MessageBox.Show(this, L.T("mpf.selectFirst"), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string current;
            ServerParams.LoadValues().TryGetValue(p.Name, out current);

            string value;
            if (!ShowEditDialog(p, current ?? "", out value)) return;

            try
            {
                ServerParams.SetValue(p.Name, value);
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mpf.saveFail"), ex.Message), L.T("mpf.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 按类型弹出编辑对话框；返回 false 表示取消。
        /// 属运行时临时弹窗（控件随参数类型变化，非本窗口设计的一部分），故保留在逻辑代码中。
        /// </summary>
        private static bool ShowEditDialog(ServerParam p, string current, out string value)
        {
            value = null;

            using (var f = new Form())
            {
                f.Text = L.T("mpf.dlg.editValueTitle");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(460, 210);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;

                var lblName = new Label
                {
                    Text = string.Format(L.T("mpf.lbl.param"), p.Name),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    Location = new Point(12, 12)
                };
                var lblDesc = new Label
                {
                    Text = p.Desc,
                    AutoSize = false,
                    Size = new Size(436, 40),
                    ForeColor = Color.Gray,
                    Location = new Point(12, 40)
                };

                Control input;

                if (p.Kind == ParamKind.Bool)
                {
                    // 开关：三选一（未启用 / 开 / 关）
                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new Point(12, 88),
                        Size = new Size(436, 31)
                    };
                    combo.Items.Add(L.T("mpf.notEnabled"));
                    combo.Items.Add("true");
                    combo.Items.Add("false");
                    combo.SelectedIndex = current == "true" ? 1 :
                                          current == "false" ? 2 : 0;
                    input = combo;
                }
                else if (p.Kind == ParamKind.Enum)
                {
                    // 枚举：候选下拉 + 不启用
                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Location = new Point(12, 88),
                        Size = new Size(436, 31)
                    };
                    combo.Items.Add(L.T("mpf.notEnabled"));
                    foreach (var opt in p.Options) combo.Items.Add(opt);
                    int idx = 0;
                    for (int i = 0; i < p.Options.Length; i++)
                        if (string.Equals(p.Options[i], current, StringComparison.Ordinal)) idx = i + 1;
                    combo.SelectedIndex = idx;
                    input = combo;
                }
                else
                {
                    // Int / Float / Text：文本框
                    var txt = new TextBox
                    {
                        Text = current,
                        Location = new Point(12, 88),
                        Size = new Size(436, 31)
                    };
                    input = txt;
                }

                var ok = new Button { Text = L.T("mpf.common.ok"), DialogResult = DialogResult.OK, Size = new Size(88, 30) };
                var cancel = new Button { Text = L.T("mpf.common.cancel"), DialogResult = DialogResult.Cancel, Size = new Size(88, 30) };
                ok.Location = new Point(436 - 88 - 12 - 88 - 12, 160);
                cancel.Location = new Point(436 - 88 - 12, 160);

                f.Controls.Add(lblName);
                f.Controls.Add(lblDesc);
                f.Controls.Add(input);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog() != DialogResult.OK) return false;

                string raw;
                if (input is ComboBox)
                {
                    int idx = ((ComboBox)input).SelectedIndex;
                    if (idx <= 0) raw = "";   // 「未启用」
                    else raw = (string)((ComboBox)input).Items[idx];
                }
                else
                {
                    raw = ((TextBox)input).Text;
                }
                raw = (raw ?? "").Trim();

                // 数值类型校验
                if (raw.Length > 0)
                {
                    if (p.Kind == ParamKind.Int)
                    {
                        long l;
                        if (!long.TryParse(raw, out l))
                        {
                            MessageBox.Show(f, L.T("mpf.mustInt"), L.T("mpf.common.error"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                    }
                    else if (p.Kind == ParamKind.Float)
                    {
                        double d;
                        if (!double.TryParse(raw, out d))
                        {
                            MessageBox.Show(f, L.T("mpf.mustNumber"), L.T("mpf.common.error"),
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return false;
                        }
                    }
                }

                value = raw;
                return true;
            }
        }
    }
}
