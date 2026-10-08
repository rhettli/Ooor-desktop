using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ooor.Core;
using OoorFunc.Core;

namespace ooor
{
    /// <summary>
    /// Agent 新建/编辑对话框：名称、完全开发权限、使用系统提示词、系统默认提示词（只读）、
    /// 用户自定义系统提示词、默认模型、绑定的内置工具与 MCP 服务器。
    /// 设计代码见 AgentEditForm.Designer.cs；此处只保留加载/校验/提交逻辑。
    /// 提示词下发合并规则（聊天下发处实现）：
    ///   chkUseSystemPrompt 勾选 + 有自定义 → 系统默认 + 用户自定义 合并下发
    ///   chkUseSystemPrompt 勾选 + 无自定义 → 仅系统默认
    ///   不勾选 + 有自定义 → 仅用户自定义
    ///   不勾选 + 无自定义 → 不下发系统 prompt
    /// </summary>
    public partial class AgentEditForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private readonly AgentRecord _record;
        private readonly bool _isNew;

        // mcp 目录是否扫描到可执行文件（为空时列表中只有一行占位提示，语言切换时需刷新该行）
        private bool _hasMcp;

        public AgentRecord Result => _record;

        public AgentEditForm(AgentRecord existing = null)
        {
            _isNew = existing == null;
            _record = existing ?? new AgentRecord();

            InitializeComponent();

            Text = _isNew ? L.T("age.title.new") : L.T("age.title.edit");

            // txtPrompt 只读，显示内置系统默认提示词
            txtPrompt.ReadOnly = true;
            try { txtPrompt.Text = AgentOptions.DefaultSystemPrompt ?? ""; }
            catch { txtPrompt.Text = ""; }

            // 确定按钮：校验失败时不关闭窗口
            btnOk.Click += (s, e) => { if (!ValidateInput()) DialogResult = DialogResult.None; };

            // 白名单右键菜单
            tsmiAddDir.Click += (s, e) => AddWriteDir();
            tsmiRemoveDir.Click += (s, e) => RemoveWriteDir();
            tsmiLocateDir.Click += (s, e) => LocateWriteDir();
            tsmiCopyDir.Click += (s, e) => CopyWriteDir();
            // 列表鼠标按下时按"是否选中行"动态启用/禁用定位+复制；没选中时右键不亮
            listViewWriteDir.MouseDown += (s, e) => UpdateWriteDirMenuState();
            cmsWriteDir.Opening += (s, e) => UpdateWriteDirMenuState();

            LoadData();
        }

        /// <summary>按当前语言刷新窗口所有静态文本（标题、标签、复选框、按钮）及 MCP 空列表占位提示</summary>
        protected override void ApplyLanguage()
        {
            Text = _isNew ? L.T("age.title.new") : L.T("age.title.edit");

            lblName.Text = L.T("age.lbl.name");
            chkFullDev.Text = L.T("age.chk.fullDev");
            chkUseSystemPrompt.Text = L.T("age.chk.useSystemPrompt");
            lblModel.Text = L.T("age.lbl.model");
            lblPrompt.Text = L.T("age.lbl.systemPrompt");
            label1.Text = L.T("age.lbl.userPrompt");
            lblTools.Text = L.T("age.lbl.tools");
            lblMcp.Text = L.T("age.lbl.mcp");
            label2.Text = L.T("age.lbl.writeDir");
            tsmiAddDir.Text = L.T("age.cm.addDir");
            tsmiRemoveDir.Text = L.T("age.cm.removeDir");
            tsmiLocateDir.Text = L.T("age.cm.locateDir");
            tsmiCopyDir.Text = L.T("age.cm.copyDir");
            btnOk.Text = L.T("age.btn.ok");
            btnCancel.Text = L.T("age.btn.cancel");

            // 白名单列表列头随语言刷新
            if (listViewWriteDir.Columns.Count > 0)
                listViewWriteDir.Columns[0].Text = L.T("age.col.dirPath");

            // MCP 列表为空时唯一一项是占位提示
            if (!_hasMcp && clbMcp.Items.Count > 0)
                clbMcp.Items[0] = L.T("age.mcp.empty");

            // 内置函数描述随语言变化，重建列表并保留勾选
            var checkedTools = new List<string>();
            for (int i = 0; i < clbTools.Items.Count; i++)
            {
                if (!clbTools.GetItemChecked(i)) continue;
                string s = clbTools.Items[i].ToString();
                int sep = s.IndexOf(" — ", StringComparison.Ordinal);
                checkedTools.Add(sep > 0 ? s.Substring(0, sep) : s);
            }
            clbTools.BeginUpdate();
            try
            {
                clbTools.Items.Clear();
                foreach (var kv in BuiltinToolCatalog.All)
                {
                    int idx = clbTools.Items.Add(kv.Key + " — " + L.T("bts.tool." + kv.Key));
                    if (checkedTools.Contains(kv.Key)) clbTools.SetItemChecked(idx, true);
                }
            }
            finally { clbTools.EndUpdate(); }
        }

        private void LoadData()
        {
            txtName.Text = _record.Name ?? "";
            chkFullDev.Checked = _record.FullDevPermission;
            chkUseSystemPrompt.Checked = _record.UseSystemPrompt;
            txtUserPrompt.Text = _record.SystemPrompt ?? "";
            cmbModel.Text = _record.DefaultModel ?? "";

            // 模型下拉：扫描已安装模型 + 允许手输
            try
            {
                var models = LlamaRuntime.ScanModels();
                foreach (var m in models ?? new List<ModelInfo>())
                    cmbModel.Items.Add(m.FullPath);
            }
            catch { }

            // 内置工具
            clbTools.Items.Clear();
            foreach (var kv in BuiltinToolCatalog.All)
                {
                    int idx = clbTools.Items.Add(kv.Key + " — " + L.T("bts.tool." + kv.Key));
                if (_record.BoundTools != null && _record.BoundTools.Contains(kv.Key))
                    clbTools.SetItemChecked(idx, true);
            }

            // MCP 服务器
            clbMcp.Items.Clear();
            var mcps = McpScanner.Scan();
            foreach (var m in mcps)
            {
                int idx = clbMcp.Items.Add(m.Name + "  —  " + m.Path);
                if (_record.BoundMcp != null && _record.BoundMcp.Contains(m.Id))
                    clbMcp.SetItemChecked(idx, true);
            }
            if (mcps.Count == 0)
            {
                _hasMcp = false;
                clbMcp.Items.Add(L.T("age.mcp.empty"));
            }
            else
            {
                _hasMcp = true;
            }

            // 白名单目录
            listViewWriteDir.BeginUpdate();
            try
            {
                listViewWriteDir.Items.Clear();
                if (_record.WriteDirs != null)
                {
                    foreach (string d in _record.WriteDirs)
                    {
                        if (string.IsNullOrWhiteSpace(d)) continue;
                        listViewWriteDir.Items.Add(d);
                    }
                }
                if (listViewWriteDir.Columns.Count == 0)
                    listViewWriteDir.Columns.Add(L.T("age.col.dirPath"), 500);
                else
                    listViewWriteDir.Columns[0].Text = L.T("age.col.dirPath");
                listViewWriteDir.Columns[0].Width = 520;
            }
            finally { listViewWriteDir.EndUpdate(); }
        }

        private bool ValidateInput()
        {
            string name = txtName.Text.Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, L.T("age.msg.nameRequired"), L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (AgentStore.NameExists(name, _isNew ? null : _record.Id))
            {
                MessageBox.Show(this, L.T("age.msg.nameExists"), L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        /// <summary>把控件值写回 _record（由调用方在 DialogResult.OK 时调用 Save）。</summary>
        public void Commit()
        {
            _record.Name = txtName.Text.Trim();
            _record.FullDevPermission = chkFullDev.Checked;
            _record.UseSystemPrompt = chkUseSystemPrompt.Checked;
            _record.SystemPrompt = txtUserPrompt.Text;
            _record.DefaultModel = cmbModel.Text.Trim();

            _record.BoundTools = new List<string>();
            for (int i = 0; i < clbTools.Items.Count; i++)
            {
                if (!clbTools.GetItemChecked(i)) continue;
                string item = clbTools.Items[i].ToString();
                int sep = item.IndexOf(" — ", StringComparison.Ordinal);
                _record.BoundTools.Add(sep > 0 ? item.Substring(0, sep) : item);
            }

            _record.BoundMcp = new List<string>();
            var mcps = McpScanner.Scan();
            for (int i = 0; i < clbMcp.Items.Count && i < mcps.Count; i++)
            {
                if (clbMcp.GetItemChecked(i))
                    _record.BoundMcp.Add(mcps[i].Id);
            }

            // 白名单目录
            _record.WriteDirs = new List<string>();
            foreach (ListViewItem item in listViewWriteDir.Items)
            {
                string d = (item.Text ?? "").Trim();
                if (d.Length > 0) _record.WriteDirs.Add(d);
            }
        }

        /// <summary>新增白名单目录：弹 FolderBrowserDialog，选目录后加到列表（去重）</summary>
        private void AddWriteDir()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                try { dlg.Description = L.T("age.dlg.addDirDesc"); } catch { }
                dlg.ShowNewFolderButton = false;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string path;
                try { path = Path.GetFullPath(dlg.SelectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                foreach (ListViewItem ex in listViewWriteDir.Items)
                {
                    if (string.Equals(ex.Text, path, StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(this, L.T("age.msg.dirExists"), L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                }
                listViewWriteDir.Items.Add(path);
                listViewWriteDir.EnsureVisible(listViewWriteDir.Items.Count - 1);
            }
        }

        /// <summary>移除选中白名单目录（支持多选；没选中时禁用或提示）</summary>
        private void RemoveWriteDir()
        {
            if (listViewWriteDir.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, L.T("age.msg.selectDirFirst"), L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            listViewWriteDir.BeginUpdate();
            try
            {
                for (int i = listViewWriteDir.SelectedItems.Count - 1; i >= 0; i--)
                    listViewWriteDir.Items.Remove(listViewWriteDir.SelectedItems[i]);
            }
            finally { listViewWriteDir.EndUpdate(); }
        }

        /// <summary>按当前选中行数动态启用/禁用定位+复制菜单（右键弹出前 + 列表鼠标按下时调用）</summary>
        private void UpdateWriteDirMenuState()
        {
            bool hasSel = listViewWriteDir.SelectedItems.Count > 0;
            tsmiLocateDir.Enabled = hasSel;
            tsmiCopyDir.Enabled = hasSel;
        }

        /// <summary>在资源管理器中定位白名单目录（多选时只定位第一个）</summary>
        private void LocateWriteDir()
        {
            if (listViewWriteDir.SelectedItems.Count == 0) return;
            string path = (listViewWriteDir.SelectedItems[0].Text ?? "").Trim();
            if (path.Length == 0) return;
            try
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show(this,
                        string.Format(L.T("age.msg.dirNotExist"), path),
                        L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                // /select 把目录高亮在资源管理器中，更直观
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>把白名单路径复制到剪贴板（多选时按行拼接）</summary>
        private void CopyWriteDir()
        {
            if (listViewWriteDir.SelectedItems.Count == 0) return;
            var lines = new List<string>();
            foreach (ListViewItem item in listViewWriteDir.SelectedItems)
            {
                string p = (item.Text ?? "").Trim();
                if (p.Length > 0) lines.Add(p);
            }
            if (lines.Count == 0) return;
            try
            {
                System.Windows.Forms.Clipboard.SetText(string.Join(Environment.NewLine, lines));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L.T("age.caption.prompt"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void chkUseSystemPrompt_CheckStateChanged(object sender, EventArgs e)
        {
            txtPrompt.Enabled=chkUseSystemPrompt.Checked = chkUseSystemPrompt.Checked;
        }
    }
}
