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
            btnOk.Text = L.T("age.btn.ok");
            btnCancel.Text = L.T("age.btn.cancel");

            // MCP 列表为空时唯一一项是占位提示
            if (!_hasMcp && clbMcp.Items.Count > 0)
                clbMcp.Items[0] = L.T("age.mcp.empty");
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
                int idx = clbTools.Items.Add(kv.Key + " — " + kv.Value);
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
        }

        private void chkUseSystemPrompt_CheckStateChanged(object sender, EventArgs e)
        {
            txtPrompt.Enabled=chkUseSystemPrompt.Checked = chkUseSystemPrompt.Checked;
        }
    }
}
