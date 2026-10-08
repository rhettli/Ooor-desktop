using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// Agent 管理窗口：
    ///   - 列表（ListView）展示所有 Agent，数据来自 LiteDB（AgentStore）
    ///   - 工具栏：新建 / 刷新
    ///   - 右键菜单：聊天 / 新建 / 编辑 / 绑定函数 / 复制 / 删除 / 刷新
    ///   - 双击行 = 编辑
    /// 「绑定函数」子菜单分两组：内置函数（BuiltinToolCatalog）、MCP 服务器（McpScanner 扫描 mcp 目录）。
    /// 勾选状态实时写回 AgentRecord.BoundTools / BoundMcp 并持久化。
    /// </summary>
    public partial class AgentManager : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        // 工具栏动态追加的「新建」按钮
        private ToolStripButton btnNew;
        // 右键菜单中动态追加的项
        private ToolStripMenuItem miNew;
        private ToolStripMenuItem miEdit;
        private ToolStripMenuItem miBindFuncs;   // 绑定函数（带子菜单）
        private ToolStripMenuItem miBindBuiltin; // 绑定内置函数（弹窗）
        private ToolStripMenuItem miBindMcp;     // 绑定 MCP（弹窗）

        public AgentManager()
        {
            InitializeComponent();
            WireExtraMenuItems();
            LoadAgents();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、列头、按钮、菜单），并重载列表刷新动态列</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("agm.title");

            // 工具栏
            btnNew.Text = L.T("agm.btn.new");
            toolStripButton1.Text = L.T("agm.btn.refresh");

            // 列头
            columnHeader1.Text = L.T("agm.col.name");
            columnHeader2.Text = L.T("agm.col.fullDev");
            columnHeader3.Text = L.T("agm.col.systemPrompt");
            columnHeader4.Text = L.T("agm.col.defaultModel");
            columnHeader5.Text = L.T("agm.col.createdAt");

            // 右键菜单（设计器项）
            聊天ToolStripMenuItem.Text = L.T("agm.menu.chat");
            刷新ToolStripMenuItem.Text = L.T("agm.menu.refresh");
            复制ToolStripMenuItem.Text = L.T("agm.menu.duplicate");
            删除ToolStripMenuItem.Text = L.T("agm.menu.delete");

            // 右键菜单（动态追加项）
            miNew.Text = L.T("agm.menu.new");
            miEdit.Text = L.T("agm.menu.edit");
            miBindFuncs.Text = L.T("agm.menu.bindFuncs");
            miBindBuiltin.Text = L.T("agm.menu.builtinFuncs");
            miBindMcp.Text = L.T("agm.menu.mcpServers");

            // 「是/否」等动态内容随语言重载
            LoadAgents();
        }

        // ==================== 动态补充菜单项 ====================

        private void WireExtraMenuItems()
        {
            // 工具栏追加「新建」
            btnNew = new ToolStripButton(L.T("agm.btn.new")) { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnNew.Click += (s, e) => NewAgent();
            toolStrip1.Items.Insert(0, btnNew);

            // 右键菜单：聊天 | 新建 | 编辑 | 绑定函数 ▸ | 复制 | 删除 | 刷新
            miNew = new ToolStripMenuItem(L.T("agm.menu.new"));
            miNew.Click += (s, e) => NewAgent();

            miEdit = new ToolStripMenuItem(L.T("agm.menu.edit"));
            miEdit.Click += (s, e) => EditSelected();

            miBindBuiltin = new ToolStripMenuItem(L.T("agm.menu.builtinFuncs"));
            miBindBuiltin.Click += (s, e) => BindBuiltinFunctions();

            miBindMcp = new ToolStripMenuItem(L.T("agm.menu.mcpServers"));
            miBindMcp.Click += (s, e) => BindMcpServers();

            miBindFuncs = new ToolStripMenuItem(L.T("agm.menu.bindFuncs"));
            miBindFuncs.DropDownItems.AddRange(new ToolStripItem[] { miBindBuiltin, miBindMcp });

            // 插入到「聊天」之后
            int insertAt = contextMenuStrip1.Items.IndexOf(聊天ToolStripMenuItem) + 1;
            contextMenuStrip1.Items.Insert(insertAt, miNew);
            contextMenuStrip1.Items.Insert(insertAt + 1, miEdit);
            contextMenuStrip1.Items.Insert(insertAt + 2, miBindFuncs);

            // 已有菜单接线
            刷新ToolStripMenuItem.Click += (s, e) => LoadAgents();
            删除ToolStripMenuItem.Click += (s, e) => DeleteSelected();
            聊天ToolStripMenuItem.Click += (s, e) => ChatWithSelected();
            复制ToolStripMenuItem.Click += (s, e) => DuplicateSelected();

            toolStripButton1.Click += (s, e) => LoadAgents();

            // 双击行 = 编辑
            listView1.DoubleClick += (s, e) => EditSelected();

            // 右键菜单打开时刷新各项可用状态
            contextMenuStrip1.Opening += (s, e) =>
            {
                bool has = listView1.SelectedItems.Count > 0;
                miEdit.Enabled = has;
                miBindFuncs.Enabled = has;
                聊天ToolStripMenuItem.Enabled = has;
                复制ToolStripMenuItem.Enabled = has;
                删除ToolStripMenuItem.Enabled = has;
            };
        }

        // ==================== 列表加载 ====================

        private void LoadAgents()
        {
            listView1.BeginUpdate();
            try
            {
                listView1.Items.Clear();
                foreach (var a in AgentStore.All())
                {
                    var item = new ListViewItem(a.Name ?? "") { Tag = a };
                    item.SubItems.Add(a.FullDevPermission ? L.T("agm.yes") : L.T("agm.no"));
                    item.SubItems.Add((a.SystemPrompt ?? "").Replace("\r", " ").Replace("\n", " "));
                    item.SubItems.Add(a.DefaultModel ?? "");
                    item.SubItems.Add(a.CreatedAt.ToString("yyyy-MM-dd"));
                    listView1.Items.Add(item);
                }
            }
            finally { listView1.EndUpdate(); }
        }

        private AgentRecord SelectedAgent =>
            listView1.SelectedItems.Count > 0
                ? listView1.SelectedItems[0].Tag as AgentRecord
                : null;

        // ==================== 操作 ====================

        private void NewAgent()
        {
            using (var dlg = new AgentEditForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                dlg.Commit();
                AgentStore.Add(dlg.Result);
                LoadAgents();
            }
        }

        private void EditSelected()
        {
            var a = SelectedAgent;
            if (a == null) return;
            using (var dlg = new AgentEditForm(a))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                dlg.Commit();
                AgentStore.Update(dlg.Result);
                LoadAgents();
            }
        }

        private void DeleteSelected()
        {
            var a = SelectedAgent;
            if (a == null) return;
            var r = MessageBox.Show(this, string.Format(L.T("agm.msg.deleteConfirm"), a.Name), L.T("agm.caption.delete"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            AgentStore.Delete(a.Id);
            LoadAgents();
        }

        private void DuplicateSelected()
        {
            var a = SelectedAgent;
            if (a == null) return;
            // 走 AgentFactory.Clone：所有字段（含 WriteDirs）一起深拷贝；
            // 之前手写曾漏 WriteDirs，导致复制的 agent 白名单丢失。
            AgentStore.Add(AgentFactory.Clone(a, a.Name + L.T("agm.copySuffix")));
            LoadAgents();
        }

        private void ChatWithSelected()
        {
            var a = SelectedAgent;
            if (a == null) return;

            string exe = Path.Combine(Application.StartupPath, "Ooor-cli.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, string.Format(L.T("ccr.msg.cliMissing"), exe),
                    L.T("agm.caption.chat"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cwd;
            try { cwd = LlamaRuntime.ConfigRoot; }
            catch { cwd = Application.StartupPath; }

            try
            {
                // 调试模式：读 app.conf，勾选时给 CLI 传 --debug，打印发给模型的消息和细节
                bool debug = false;
                try { debug = AppSettings.Load().DebugMode; } catch { }
                string debugArg = debug ? " --debug" : "";

                // 用 PowerShell 宿主 + -NoExit 启动 CLI：CLI 退出后窗口保留，便于查看输出；
                // --agent 按 Agent 名称拉起对应人设（CLI 侧 AgentCatalog.GetByName 加载系统提示词）；
                // --pause 服务未就绪时停窗不闪退；--root 把工作目录加入沙盒白名单。
                // 路径与参数值都用单引号包裹，避免 -Command 字符串里的双引号嵌套问题。
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoExit -NoProfile -Command \"& '" + exe + "' --pause --root '" + cwd + "' --agent '" + a.Name.Replace("'", "''") + "'" + debugArg + "\"",
                    UseShellExecute = true,
                    WorkingDirectory = cwd
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L.T("agm.caption.chat"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ==================== 绑定函数 ====================

        private void BindBuiltinFunctions()
        {
            var a = SelectedAgent;
            if (a == null) return;
            using (var dlg = new BindToolsForm(a))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AgentStore.Update(a);
                    LoadAgents();
                }
            }
        }

        private void BindMcpServers()
        {
            var a = SelectedAgent;
            if (a == null) return;
            using (var dlg = new BindMcpForm(a))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    AgentStore.Update(a);
                    LoadAgents();
                }
            }
        }
    }
}
