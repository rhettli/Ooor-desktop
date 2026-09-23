using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 参数方案管理窗口：左侧方案名称列表（新建/重命名/删除），
    /// 右侧选中方案的参数 ListView（双击行修改参数值），改动即时写回 model_profiles.conf。
    /// 界面布局见 ProfileManagerForm.Designer.cs。
    /// </summary>
    internal sealed partial class ProfileManagerForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private List<RunProfile> _profiles;
        private bool _loading;

        /// <summary>方案列表有变动（调用方应刷新「参数方案」下拉框）</summary>
        public bool Changed { get; private set; }

        /// <summary>新建方案时使用的默认参数（取自主窗口当前参数）</summary>
        private readonly RunProfile _defaults;

        /// <summary>设计器用构造函数（无默认参数，新建方案时按 RunProfile 默认值）</summary>
        public ProfileManagerForm() : this(null)
        {
        }

        public ProfileManagerForm(RunProfile defaults)
        {
            _defaults = defaults ?? new RunProfile { Name = "" };
            InitializeComponent();
            ReloadProfiles();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、按钮、列头、提示），并重填右侧参数列表刷新说明列</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("prof.title");

            // 左侧按钮
            btnNew.Text = L.T("prof.btn.new");
            btnRename.Text = L.T("prof.btn.rename");
            btnDelete.Text = L.T("prof.btn.delete");

            // 右侧列头与提示
            colParam.Text = L.T("prof.col.param");
            colValue.Text = L.T("prof.col.value");
            colDesc.Text = L.T("prof.col.desc");
            lblTip.Text = L.T("prof.tip");

            // 参数说明列为动态内容，语言切换后重填
            ShowParams(Selected);
        }

        /// <summary>当前选中的方案；未选中为 null</summary>
        private RunProfile Selected
        {
            get { return listProfiles.SelectedItem as RunProfile; }
        }

        /// <summary>重新读取文件并填充左侧列表（保留原选中）</summary>
        private void ReloadProfiles()
        {
            _loading = true;
            try
            {
                string keepName = listProfiles.SelectedItem is RunProfile k ? k.Name : null;
                _profiles = ProfileStore.LoadProfiles();

                listProfiles.BeginUpdate();
                listProfiles.Items.Clear();
                foreach (var p in _profiles) listProfiles.Items.Add(p);
                listProfiles.EndUpdate();

                if (listProfiles.Items.Count > 0)
                {
                    int idx = 0;
                    if (keepName != null)
                    {
                        for (int i = 0; i < listProfiles.Items.Count; i++)
                        {
                            var p = listProfiles.Items[i] as RunProfile;
                            if (p != null && p.Name == keepName) { idx = i; break; }
                        }
                    }
                    listProfiles.SelectedIndex = idx;
                }
            }
            finally
            {
                _loading = false;
            }

            // 上面设置选中项时事件被 _loading 屏蔽（选中项相同时也根本不会触发事件），
            // 因此这里统一刷新右侧参数，保证打开窗口即有内容显示。
            ShowParams(Selected);
        }

        /// <summary>左侧选中变化 → 右侧显示该方案参数</summary>
        private void List_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            ShowParams(Selected);
        }

        /// <summary>把方案参数填入右侧 ListView</summary>
        private void ShowParams(RunProfile p)
        {
            listParams.BeginUpdate();
            try
            {
                listParams.Items.Clear();
                if (p == null) return;

                AddParam("-c", p.Ctx.ToString(), L.T("prof.desc.ctx"));
                AddParam("-n", p.NPred.ToString(), L.T("prof.desc.npred"));
                AddParam("-ngl", p.Ngl.ToString(), L.T("prof.desc.ngl"));
                AddParam("--host", p.Host, L.T("prof.desc.host"));
                AddParam("--port", p.Port.ToString(), L.T("prof.desc.port"));
            }
            finally
            {
                listParams.EndUpdate();
            }
        }

        private void AddParam(string arg, string value, string desc)
        {
            var item = new ListViewItem(arg) { Tag = arg };
            item.SubItems.Add(value);
            item.SubItems.Add(desc);
            listParams.Items.Add(item);
        }

        /// <summary>双击参数行 → 输入框修改值（数字项校验后写回并保存）</summary>
        private void Params_DoubleClick(object sender, EventArgs e)
        {
            var p = Selected;
            if (p == null || listParams.SelectedItems.Count == 0) return;

            string paramName = listParams.SelectedItems[0].Tag as string;
            string old = listParams.SelectedItems[0].SubItems[1].Text;
            string desc = listParams.SelectedItems[0].SubItems[2].Text;

            string value = old;
            string label = string.Format(L.T("prof.editParam.label"), paramName, desc);
            if (!InputBox.Show(this, L.T("prof.editParam.title"), label, ref value)) return;
            value = (value ?? "").Trim();

            try
            {
                switch (paramName)
                {
                    case "-c":
                        p.Ctx = long.Parse(value);
                        break;
                    case "-n":
                        p.NPred = int.Parse(value);
                        break;
                    case "-ngl":
                        p.Ngl = int.Parse(value);
                        break;
                    case "--host":
                        if (value.Length == 0)
                        {
                            MessageBox.Show(this, L.T("prof.msg.hostEmpty"), L.T("prof.caption.paramError"));
                            return;
                        }
                        p.Host = value;
                        break;
                    case "--port":
                        p.Port = int.Parse(value);
                        break;
                    default:
                        return;
                }
            }
            catch
            {
                MessageBox.Show(this, L.T("prof.msg.invalidValue"), L.T("prof.caption.paramError"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Save()) return;
            ShowParams(p);
        }

        private void BtnNew_Click(object sender, EventArgs e)
        {
            string name = L.T("prof.defaultName.new");
            if (!InputBox.Show(this, L.T("prof.caption.new"), L.T("prof.prompt.name"), ref name)) return;
            name = (name ?? "").Trim();
            if (name.Length == 0) return;

            if (_profiles.Find(x => x.Name == name) != null)
            {
                MessageBox.Show(this, string.Format(L.T("prof.msg.duplicate"), name), L.T("prof.caption.new"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            

            // 新方案以主窗口当前参数为初始值
            var p = new RunProfile
            {
                Name = name,
                Ctx = _defaults.Ctx,
                NPred = _defaults.NPred,
                Ngl = _defaults.Ngl,
                Host = _defaults.Host,
                Port = _defaults.Port
            };
            _profiles.Add(p);

            if (!Save()) return;
            Changed = true;
            ReloadProfiles();
            for (int i = 0; i < listProfiles.Items.Count; i++)
            {
                if ((listProfiles.Items[i] as RunProfile) == p) { listProfiles.SelectedIndex = i; break; }
            }
        }

        private void BtnRename_Click(object sender, EventArgs e)
        {
            var p = Selected;
            if (p == null) return;

            string name = p.Name;
            if (!InputBox.Show(this, L.T("prof.caption.rename"), L.T("prof.prompt.name"), ref name)) return;
            name = (name ?? "").Trim();
            if (name.Length == 0 || name == p.Name) return;

            if (_profiles.Find(x => x != p && x.Name == name) != null)
            {
                MessageBox.Show(this, string.Format(L.T("prof.msg.duplicate"), name), L.T("prof.caption.rename"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            p.Name = name;
            if (!Save()) return;
            Changed = true;
            ReloadProfiles();
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            var p = Selected;
            if (p == null) return;

            if (MessageBox.Show(this, string.Format(L.T("prof.msg.deleteConfirm"), p.Name), L.T("prof.caption.delete"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            _profiles.Remove(p);
            if (!Save()) return;
            Changed = true;
            ReloadProfiles();
        }

        /// <summary>写回配置文件；失败弹窗提示</summary>
        private bool Save()
        {
            try
            {
                ProfileStore.SaveProfiles(_profiles);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("prof.msg.saveFail"), ex.Message), L.T("prof.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
