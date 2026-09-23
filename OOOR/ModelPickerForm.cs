using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 模型选择窗口：ListView 列出全部模型（内置 models 目录 + ref_models.conf 引用），
    /// 列：模型名称 / 模型目录 / 类型（内置·引用）/ 尺寸 / 备注 / 日期时间（修改时间）。
    /// 双击一个行即可选择；也可选中后点「确定」或按回车确认。
    /// 界面布局见 ModelPickerForm.Designer.cs。
    /// </summary>
    internal sealed partial class ModelPickerForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>用户最终选中的模型；取消/关闭窗口时为 null</summary>
        public ModelInfo Selected { get; private set; }

        /// <summary>设计器用构造函数（无模型数据，设计态预览用）</summary>
        public ModelPickerForm() : this(null, null)
        {
        }

        /// <param name="models">全部可选模型（LlamaRuntime.ScanModels 的结果）</param>
        /// <param name="current">当前正在使用的模型（默认选中对应行；可为 null）</param>
        public ModelPickerForm(List<ModelInfo> models, ModelInfo current)
        {
            InitializeComponent();
            LoadModels(models, current);
        }

        /// <summary>填充模型列表，并默认选中当前正在使用的模型（否则选第一行）</summary>
        private void LoadModels(List<ModelInfo> models, ModelInfo current)
        {
            listModels.BeginUpdate();
            try
            {
                listModels.Items.Clear();

                foreach (var m in models ?? new List<ModelInfo>())
                {
                    string full = m.FullPath ?? "";
                    var item = new ListViewItem(Path.GetFileName(full))
                    {
                        ToolTipText = full   // 悬停显示完整路径
                    };

                    item.SubItems.Add(FormatSize(m.SizeBytes));
                    item.SubItems.Add(m.Note ?? "");
                    item.SubItems.Add(GetWriteTimeText(full));
                    item.Tag = m;
                    listModels.Items.Add(item);
                }
            }
            finally
            {
                listModels.EndUpdate();
            }

            // 默认选中当前使用的模型（按完整路径匹配）；否则选第一行
            if (current != null)
            {
                foreach (ListViewItem it in listModels.Items)
                {
                    var m = it.Tag as ModelInfo;
                    if (m != null && string.Equals(m.FullPath, current.FullPath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        it.Selected = true;
                        it.EnsureVisible();
                        break;
                    }
                }
            }
            if (listModels.SelectedItems.Count == 0 && listModels.Items.Count > 0)
                listModels.Items[0].Selected = true;
        }

        /// <summary>按当前语言刷新标题、列头与右键菜单</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("mpk.title");
            colModelName.Text = L.T("mpk.col.name");
            colModelSize.Text = L.T("mpk.col.size");
            colModelNote.Text = L.T("mpk.col.note");
            colModelTime.Text = L.T("mpk.col.time");
            选择双击ToolStripMenuItem.Text = L.T("mpk.menu.select");
            ToolStripMenuItemManagerAllModel.Text = L.T("mpk.menu.manage");
        }

        /// <summary>确认当前选择（确定按钮 / 双击行 / 回车）</summary>
        private void ConfirmSelection(object sender, EventArgs e)
        {
            if (listModels.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, L.T("mpk.msg.selectFirst"), L.T("mpk.title"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Selected = listModels.SelectedItems[0].Tag as ModelInfo;
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>底部「确定 / 取消」按钮保持右对齐（间距 12px）</summary>
        private void LayoutButtons(object sender, EventArgs e)
        {
         
        }

        /// <summary>格式化文件大小（如 4.1 GB）；未知时返回空字符串</summary>
        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "";
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : "0.##") + " " + units[i];
        }

        /// <summary>模型文件的最后修改时间（yyyy-MM-dd HH:mm）；失败返回空字符串</summary>
        private static string GetWriteTimeText(string full)
        {
            try { return File.GetLastWriteTime(full).ToString("yyyy-MM-dd HH:mm"); }
            catch { return ""; }
        }

        private void ToolStripMenuItemManagerAllModel_Click(object sender, EventArgs e)
        {
            using (var f = new ModelManagerForm())
            {
                f.ShowDialog(this);
            }
        }
    }
}
