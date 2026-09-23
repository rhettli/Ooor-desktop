using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// llama 版本选择窗口：ListView 列出 llama-bin 下含 llama-server.exe 的版本，
    /// 列：版本 / 安装目录 / 状态（使用中）。
    /// 双击一个行即可选择；也可选中后点「确定」或按回车确认。
    /// 界面布局见 LlamaVersionPickerForm.Designer.cs。
    /// </summary>
    internal sealed partial class LlamaVersionPickerForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>构造时传入的当前使用版本（语言切换刷新「使用中」列时复用）</summary>
        private VersionInfo _current;

        /// <summary>用户最终选中的版本；取消/关闭窗口时为 null</summary>
        public VersionInfo Selected { get; private set; }

        /// <summary>设计器用构造函数（无版本数据，设计态预览用）</summary>
        public LlamaVersionPickerForm() : this(null, null)
        {
        }

        /// <param name="versions">全部可用版本（LlamaRuntime.ScanLlamaVersions 的结果）</param>
        /// <param name="current">当前正在使用的版本（默认选中对应行；可为 null）</param>
        public LlamaVersionPickerForm(List<VersionInfo> versions, VersionInfo current)
        {
            InitializeComponent();
            LoadVersions(versions, current);
        }

        /// <summary>填充版本列表，并默认选中当前正在使用的版本（否则选第一个非禁用版本）</summary>
        private void LoadVersions(List<VersionInfo> versions, VersionInfo current)
        {
            _current = current;
            listVersions.BeginUpdate();
            try
            {
                listVersions.Items.Clear();

                foreach (var v in versions ?? new List<VersionInfo>())
                {
                    string full = v.FullPath ?? "";
                    bool disabled = LlamaRuntime.IsVersionDisabled(v.Name);

                    var item = new ListViewItem(v.Name)
                    {
                        ToolTipText = full   // 悬停显示完整路径
                    };
                    // 禁用行灰显，跟正常版本做视觉区分
                    item.ForeColor = disabled ? System.Drawing.SystemColors.GrayText
                                              : System.Drawing.SystemColors.WindowText;

                    item.SubItems.Add(full);
                    item.SubItems.Add(disabled ? L.T("lvp.state.disabled") : L.T("lvp.state.enabled"));
                    item.SubItems.Add(IsCurrent(v, current) ? L.T("lvp.state.inUse") : "");
                    item.Tag = v;
                    listVersions.Items.Add(item);
                }
            }
            finally
            {
                listVersions.EndUpdate();
            }

            // 默认选中：当前使用的版本（按名称匹配）→ 否则第一个非禁用版本 → 都不命中则不选
            bool selected = false;
            if (current != null)
            {
                foreach (ListViewItem it in listVersions.Items)
                {
                    if (IsCurrent(it.Tag as VersionInfo, current))
                    {
                        it.Selected = true;
                        it.EnsureVisible();
                        selected = true;
                        break;
                    }
                }
            }
            if (!selected)
            {
                foreach (ListViewItem it in listVersions.Items)
                {
                    var v = it.Tag as VersionInfo;
                    if (v != null && !LlamaRuntime.IsVersionDisabled(v.Name))
                    {
                        it.Selected = true;
                        it.EnsureVisible();
                        selected = true;
                        break;
                    }
                }
            }
        }

        /// <summary>是否同一版本（按名称，忽略大小写）</summary>
        private static bool IsCurrent(VersionInfo v, VersionInfo current)
        {
            return v != null && current != null &&
                   string.Equals(v.Name, current.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>按当前语言刷新标题、列头、按钮及列表中的状态/使用中文本</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("lvp.title");
            colVersionName.Text = L.T("lvp.col.version");
            colVersionDir.Text = L.T("lvp.col.dir");
            colVersionStatus.Text = L.T("lvp.col.status");
            colVersionState.Text = L.T("lvp.col.inUse");
            btnOk.Text = L.T("lvp.btn.ok");
            btnCancel.Text = L.T("lvp.btn.cancel");

            // 状态列 / 使用中列为动态内容，按行 Tag 就地重算
            foreach (ListViewItem it in listVersions.Items)
            {
                var v = it.Tag as VersionInfo;
                if (v == null) continue;
                bool disabled = LlamaRuntime.IsVersionDisabled(v.Name);
                it.SubItems[2].Text = disabled ? L.T("lvp.state.disabled") : L.T("lvp.state.enabled");
                it.SubItems[3].Text = IsCurrent(v, _current) ? L.T("lvp.state.inUse") : "";
            }
        }

        /// <summary>确认当前选择（确定按钮 / 双击行 / 回车）；被禁用版本不允许选</summary>
        private void ConfirmSelection(object sender, EventArgs e)
        {
            if (listVersions.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, L.T("lvp.msg.selectFirst"), L.T("lvp.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            VersionInfo v = listVersions.SelectedItems[0].Tag as VersionInfo;
            if (v == null) return;

            if (LlamaRuntime.IsVersionDisabled(v.Name))
            {
                MessageBox.Show(this,
                    string.Format(L.T("lvp.msg.disabled"), v.Name),
                    L.T("lvp.caption"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Selected = v;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>底部「确定 / 取消」按钮保持右对齐（间距 12px）</summary>
        private void LayoutButtons(object sender, EventArgs e)
        {
            int y = (panelBottom.Height - btnOk.Height) / 2;
            btnOk.Location = new Point(panelBottom.Width - btnOk.Width - btnCancel.Width - 24, y);
            btnCancel.Location = new Point(panelBottom.Width - btnCancel.Width - 12, y);
        }
    }
}
