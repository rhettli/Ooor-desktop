using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 模型管理窗口：ListView 列出全部模型（含软删除），列：
    /// 模型名称 / 多模态投影文件 / 模型目录 / 类型（内置·引用）/ 尺寸 / 备注 / 日期时间（修改时间）；
    /// 列表按模型所在目录分组显示（每个目录一个分组标题，内置 models 目录排在前面）。
    /// 顶部工具栏与右击菜单共用：修改备注、定位模型文件、定位投影文件、添加模型目录、删除（进回收站）、软删除/取消软删除；
    /// 工具栏按钮与右键菜单项走同一组 Click 处理函数，按当前选中行同步可用状态（无选中时除"添加模型目录"外全部禁用）。
    /// 修改备注后创建 remark/模型名称_字节数.remark 备注文件（空备注删除文件）；
    /// 被软删除的模型整行灰色显示（扫描时不出现，选择窗口亦不可选）。
    /// 界面布局见 ModelManagerForm.Designer.cs。
    /// </summary>
    internal sealed partial class ModelManagerForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>本次会话中备注/引用/删除等有过改动（调用方应重新加载模型列表）</summary>
        public bool Changed { get; private set; }

        /// <summary>列表里每个模型匹配到的投影文件（模型完整路径 → mmproj 完整路径）；未匹配到的模型不在此表</summary>
        private readonly Dictionary<string, string> _mmprojOf =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ModelManagerForm()
        {
            InitializeComponent();
            ReloadList();
            UpdateRowActionsState();
        }

        /// <summary>按当前语言刷新窗口静态文本（列头、右键菜单、工具栏按钮、分组标题、类型列等）</summary>
        protected override void ApplyLanguage()
        {
            // 列头
            colModelName.Text = L.T("mmf.col.name");
            colModelMmproj.Text = L.T("mmf.col.mmproj");
            colModelDir.Text = L.T("mmf.col.dir");
            colModelType.Text = L.T("mmf.col.type");
            colModelSize.Text = L.T("mmf.col.size");
            colModelNote.Text = L.T("mmf.col.note");
            colModelTime.Text = L.T("mmf.col.time");

            // 右键菜单（与工具栏按钮共用同一组文案 key）
            menuEditNote.Text = L.T("mmf.btn.editNote");
            menuLocateFile.Text = L.T("mmf.btn.locateFile");
            menuLocateMmproj.Text = L.T("mmf.btn.locateMmproj");
            menuAddDir.Text = L.T("mmf.btn.addDir");
            menuDelete.Text = L.T("mmf.btn.delete");
            menuSoftDelete.Text = L.T("mmf.btn.softDelete");

            // 工具栏按钮
            toolStripButtonDownloadModels.Text = L.T("mmf.btn.downloadModels");
            btnAddDir.Text = L.T("mmf.btn.addDir");
            btnEditNote.Text = L.T("mmf.btn.editNote");
            btnLocateFile.Text = L.T("mmf.btn.locateFile");
            btnLocateMmproj.Text = L.T("mmf.btn.locateMmproj");
            btnDelete.Text = L.T("mmf.btn.delete");
            btnSoftDelete.Text = L.T("mmf.btn.softDelete");

            // 分组标题（分组 Tag 存的是目录路径）
            foreach (ListViewGroup g in listModels.Groups)
            {
                string dir = g.Tag as string ?? "";
                string head = dir.Length == 0 ? L.T("mmf.group.unknownDir") : dir;
                g.Header = string.Format(L.T("mmf.group.header"), head, g.Items.Count);
            }

            // 类型列：引用 / 内置
            foreach (ListViewItem item in listModels.Items)
            {
                var info = item.Tag as ModelInfo;
                item.SubItems[colModelType.Index].Text =
                    info != null && info.IsExternal ? L.T("mmf.type.external") : L.T("mmf.type.builtin");
            }

            // 标题栏；软删除按钮文字随选中行状态变化，统一重算一次
            UpdateTitle();
            UpdateRowActionsState();
        }

        /// <summary>
        /// 按当前语言刷新标题栏（模型总数 / 目录数 / 软删除数）。
        /// 重载列表时已统计软删除数可直接传入，语言切换时不传则现算。
        /// </summary>
        private void UpdateTitle(int? softDeletedCount = null)
        {
            int softDeleted = softDeletedCount ?? CountSoftDeleted();
            string suffix = softDeleted > 0
                ? string.Format(L.T("mmf.title.softDeletedSuffix"), softDeleted)
                : "";
            Text = string.Format(L.T("mmf.title"),
                listModels.Items.Count, listModels.Groups.Count, suffix);
        }

        /// <summary>统计列表中软删除模型行数</summary>
        private int CountSoftDeleted()
        {
            int n = 0;
            foreach (ListViewItem it in listModels.Items)
            {
                var info = it.Tag as ModelInfo;
                if (info != null && info.SoftDeleted) n++;
            }
            return n;
        }

        /// <summary>当前选中的模型；未选中为 null</summary>
        private ModelInfo SelectedModel
        {
            get { return listModels.SelectedItems.Count == 0 ? null : listModels.SelectedItems[0].Tag as ModelInfo; }
        }

        /// <summary>右键命中行时先选中该行，保证菜单作用于可见的选中行</summary>
        private void ListModels_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            var hit = listModels.HitTest(e.Location);
            if (hit.Item != null) hit.Item.Selected = true;
        }

        /// <summary>
        /// 重新扫描并填充列表（含软删除，软删除行灰色显示）。
        /// 按模型所在目录分组：每个目录一个 ListViewGroup（标题为目录路径 + 数量），
        /// 内置 models 目录排在其他引用目录之前，组内按文件名排序。
        /// </summary>
        private void ReloadList()
        {
            listModels.BeginUpdate();
            try
            {
                listModels.Items.Clear();
                listModels.Groups.Clear();
                _mmprojOf.Clear();

                // 0) 多模态投影文件（mmproj-*.gguf）批量匹配：一次扫描建索引，
                //    避免逐个模型调用 FindMmproj 反复遍历磁盘
                var mmprojIndex = LlamaRuntime.MmprojIndex.Build();

                // 1) 先按所在目录归集（同时记录目录出现顺序）
                var byDir = new Dictionary<string, List<ModelInfo>>(StringComparer.OrdinalIgnoreCase);
                var dirs = new List<string>();
                foreach (var m in LlamaRuntime.ScanModels(includeSoftDeleted: true))
                {
                    string dir = Path.GetDirectoryName(m.FullPath ?? "") ?? "";
                    List<ModelInfo> bucket;
                    if (!byDir.TryGetValue(dir, out bucket))
                    {
                        bucket = new List<ModelInfo>();
                        byDir[dir] = bucket;
                        dirs.Add(dir);
                    }
                    bucket.Add(m);
                }

                // 2) 分组顺序：内置目录在前，其余按路径比较
                dirs.Sort(delegate(string a, string b)
                {
                    bool ea = byDir[a][0].IsExternal;
                    bool eb = byDir[b][0].IsExternal;
                    if (ea != eb) return ea ? 1 : -1;
                    return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                });

                // 3) 每个目录建一个分组，组内按文件名排序后填入
                int softDeleted = 0;
                foreach (string dir in dirs)
                {
                    var models = byDir[dir];
                    models.Sort(delegate(ModelInfo a, ModelInfo b)
                    {
                        return string.Compare(Path.GetFileName(a.FullPath ?? ""),
                                              Path.GetFileName(b.FullPath ?? ""),
                                              StringComparison.OrdinalIgnoreCase);
                    });

                    string head = dir.Length == 0 ? L.T("mmf.group.unknownDir") : dir;
                    var group = new ListViewGroup(
                        string.Format(L.T("mmf.group.header"), head, models.Count),
                        HorizontalAlignment.Left)
                    {
                        Tag = dir
                    };
                    listModels.Groups.Add(group);

                    foreach (var m in models)
                    {
                        string full = m.FullPath ?? "";
                        var item = new ListViewItem(Path.GetFileName(full))
                        {
                            ToolTipText = full,
                            Tag = m,
                            Group = group
                        };
                        if (m.SoftDeleted) { item.ForeColor = Color.Gray; softDeleted++; }   // 软删除 → 灰色

                        // 多模态投影文件：同级目录只有一个 mmproj 时直接采用（如 Bonsai-27B-Q1_0.gguf ↔
                        // mmproj-Bonsai-27B-BF16.gguf）；有多个时必须严格同名（mmproj- 后面的名称与模型名一致）；
                        // 匹配不到留空
                        string proj = mmprojIndex.Find(full);
                        if (!string.IsNullOrEmpty(proj) && full.Length > 0) _mmprojOf[full] = proj;
                        item.SubItems.Add(string.IsNullOrEmpty(proj) ? "" : Path.GetFileName(proj));

                        item.SubItems.Add(Path.GetDirectoryName(full) ?? "");
                        item.SubItems.Add(m.IsExternal ? L.T("mmf.type.external") : L.T("mmf.type.builtin"));
                        item.SubItems.Add(FormatSize(m.SizeBytes));
                        item.SubItems.Add(m.Note ?? "");
                        item.SubItems.Add(GetWriteTimeText(full));
                        listModels.Items.Add(item);
                    }
                }

                // 状态栏式的标题：显示总数（含分组数）与软删除数
                UpdateTitle(softDeleted);
            }
            finally
            {
                listModels.EndUpdate();
            }
        }

        /// <summary>菜单打开前/列表选中变化时：根据当前选中行同步菜单与工具栏按钮的可用状态与软删除按钮文字</summary>
        private void UpdateRowActionsState()
        {
            var m = SelectedModel;
            bool has = m != null;
            bool hasMmproj = has && !string.IsNullOrEmpty(MmprojPathOf(m));

            // 右键菜单
            menuEditNote.Enabled = has;
            menuLocateFile.Enabled = has;
            menuLocateMmproj.Enabled = hasMmproj;
            menuDelete.Enabled = has;
            menuSoftDelete.Enabled = has;
            if (has) menuSoftDelete.Text = m.SoftDeleted ? L.T("mmf.btn.undoSoftDelete") : L.T("mmf.btn.softDelete");

            // 顶部工具栏按钮（添加模型目录不需要选中行）
            btnEditNote.Enabled = has;
            btnLocateFile.Enabled = has;
            btnLocateMmproj.Enabled = hasMmproj;
            btnAddDir.Enabled = true;
            btnDelete.Enabled = has;
            btnSoftDelete.Enabled = has;
            if (has) btnSoftDelete.Text = m.SoftDeleted ? L.T("mmf.btn.undoSoftDelete") : L.T("mmf.btn.softDelete");
        }

        /// <summary>右键菜单打开时同步菜单/工具栏按钮可用状态</summary>
        private void Menu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateRowActionsState();
        }

        /// <summary>列表选中行变化时（点击/重载）刷新菜单和工具栏可用状态</summary>
        private void ListModels_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateRowActionsState();
        }

        /// <summary>修改备注：弹输入框编辑，写入 remark/模型名称_字节数.remark 文件（空备注删除文件）</summary>
        private void MenuEditNote_Click(object sender, EventArgs e)
        {
            var m = SelectedModel;
            if (m == null) return;

            string note = m.Note ?? "";
            if (!ShowNoteDialog(m, ref note)) return;

            try
            {
                LlamaRuntime.SaveRemark(m.RemarkPath, note);
                Changed = true;
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.saveNoteFail"), ex.Message), L.T("mmf.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>定位模型文件：在资源管理器中打开所在目录并选中该文件</summary>
        private void MenuLocateFile_Click(object sender, EventArgs e)
        {
            var m = SelectedModel;
            if (m == null) return;

            string full = m.FullPath ?? "";
            if (full.Length == 0 || !File.Exists(full))
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.modelFileMissing"), full), L.T("mmf.caption.locateFile"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                RevealInExplorer(full);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.locateFileFail"), ex.Message), L.T("mmf.caption.locateFile"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>定位投影文件：在资源管理器中打开所在目录并选中匹配到的 mmproj 文件</summary>
        private void MenuLocateMmproj_Click(object sender, EventArgs e)
        {
            var m = SelectedModel;
            if (m == null) return;

            string proj = MmprojPathOf(m);
            if (string.IsNullOrEmpty(proj))
            {
                MessageBox.Show(this, L.T("mmf.msg.noMmproj"),
                    L.T("mmf.caption.locateMmproj"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!File.Exists(proj))
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.mmprojMissing"), proj), L.T("mmf.caption.locateMmproj"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                RevealInExplorer(proj);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.locateMmprojFail"), ex.Message), L.T("mmf.caption.locateMmproj"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>该模型匹配到的投影文件完整路径（列表刷新时算好）；未匹配返回 null</summary>
        private string MmprojPathOf(ModelInfo m)
        {
            if (m == null) return null;

            string path;
            return _mmprojOf.TryGetValue(m.FullPath ?? "", out path) ? path : null;
        }

        /// <summary>在资源管理器中打开文件所在目录并选中该文件（explorer /select）</summary>
        private static void RevealInExplorer(string full)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + full + "\"")
            {
                UseShellExecute = true
            });
        }

        /// <summary>添加模型目录：选择文件夹写入 ref_models.conf 引用（与「导入模型」一致）</summary>
        private void MenuAddDir_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = L.T("mmf.folderDlg.description");
                dlg.ShowNewFolderButton = false;

                try
                {
                    var refs = LlamaRuntime.GetModelReferences();
                    if (refs.Count > 0 && Directory.Exists(refs[refs.Count - 1]))
                        dlg.SelectedPath = refs[refs.Count - 1];
                }
                catch { /* 起始目录取不到不影响添加 */ }

                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var before = LlamaRuntime.GetModelReferences();
                    var after = LlamaRuntime.AddModelReferences(new[] { dlg.SelectedPath });
                    if (after.Count > before.Count)
                    {
                        Changed = true;
                        ReloadList();
                    }
                    else
                    {
                        MessageBox.Show(this, L.T("mmf.msg.noNewReference"),
                            L.T("mmf.caption.addDir"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, string.Format(L.T("mmf.msg.addDirFail"), ex.Message), L.T("mmf.caption"),
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>删除：把模型文件移入回收站（可还原），并清理其元数据</summary>
        private void MenuDelete_Click(object sender, EventArgs e)
        {
            var m = SelectedModel;
            if (m == null) return;

            if (MessageBox.Show(this,
                    string.Format(L.T("mmf.msg.deleteConfirm"), m.FullPath ?? ""),
                    L.T("mmf.caption.delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes) return;

            try
            {
                LlamaRuntime.DeleteModelFileToRecycleBin(m.FullPath);
                LlamaRuntime.RemoveModelMeta(m.FullPath);
                LlamaRuntime.DeleteRemark(m.RemarkPath);
                Changed = true;
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.deleteFail"), ex.Message), L.T("mmf.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>软删除/取消：只做标记（model_meta.conf），不动文件；软删除的模型扫描时排除</summary>
        private void MenuSoftDelete_Click(object sender, EventArgs e)
        {
            var m = SelectedModel;
            if (m == null) return;

            try
            {
                LlamaRuntime.SetModelSoftDeleted(m.FullPath, !m.SoftDeleted);
                Changed = true;
                ReloadList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("mmf.msg.actionFail"), ex.Message), L.T("mmf.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 备注编辑对话框（模型名只读展示 + 多行备注框 + 确定/取消）。
        /// 属运行时临时弹窗（非本窗口设计的一部分），故保留在逻辑代码中。
        /// </summary>
        private static bool ShowNoteDialog(ModelInfo model, ref string note)
        {
            using (var f = new Form())
            {
                f.Text = L.T("mmf.noteDialog.title");
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowInTaskbar = false;
                f.MinimizeBox = false;
                f.MaximizeBox = false;
                f.ClientSize = new Size(460, 220);
                f.FormBorderStyle = FormBorderStyle.FixedDialog;

                var lbl = new Label
                {
                    Text = string.Format(L.T("mmf.noteDialog.modelLabel"), Path.GetFileName(model.FullPath ?? "")),
                    AutoSize = true,
                    Location = new Point(12, 12)
                };
                var txt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Text = note ?? "",
                    Location = new Point(12, 40),
                    Size = new Size(436, 100)
                };
                var ok = new Button { Text = L.T("mmf.common.ok"), DialogResult = DialogResult.OK, Size = new Size(88, 30) };
                var cancel = new Button { Text = L.T("mmf.common.cancel"), DialogResult = DialogResult.Cancel, Size = new Size(88, 30) };
                ok.Location = new Point(436 - 88 - 12 - 88 - 12, 170);
                cancel.Location = new Point(436 - 88 - 12, 170);

                f.Controls.Add(lbl);
                f.Controls.Add(txt);
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog() != DialogResult.OK) return false;
                note = txt.Text;
                return true;
            }
        }

        /// <summary>模型文件的最后修改时间（yyyy-MM-dd HH:mm）；失败返回空字符串</summary>
        private static string GetWriteTimeText(string full)
        {
            try { return File.GetLastWriteTime(full).ToString("yyyy-MM-dd HH:mm"); }
            catch { return ""; }
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

        private void toolStripButtonDownloadModels_Click(object sender, EventArgs e)
        {
            // 弹出独立窗口（用 using 确保 Dispose），避免重复打开
            using (var f = new ModelDownloadForm())
            {
                f.ShowDialog(this);
            }
        }
    }
}
