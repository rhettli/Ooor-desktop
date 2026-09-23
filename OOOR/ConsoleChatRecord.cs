using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ooor.Controls;
using ooor.Core;
using OoorFunc.Core;

namespace ooor
{
    /// <summary>
    /// 控制台聊天记录管理窗口（卡片式行列表，参考下载管理窗口的自定义行渲染）：
    ///   - 每条记录一个 ChatRecordRowControl（标题/Agent/时间 第一行，模型第二行，速度·备注第三行），
    ///     绝对定位 Y = i * RowHeight，放在可滚动的 NoAutoScrollPanel 里；
    ///   - 单击选中、双击「接着聊」；右键菜单：接着聊 / 刷新 / 修改标题 / 删除；
    ///   - 「接着聊」以 --session &lt;id&gt; 拉起 Ooor-cli.exe，由 CLI 从 db 恢复消息历史继续对话。
    /// </summary>
    public partial class ConsoleChatRecord : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private int _selectedIndex = -1;
        private ChatRecord _contextRecord;   // 右键菜单针对的记录

        public ConsoleChatRecord()
        {
            InitializeComponent();
            try { AgentHost.SyncCoreEnv(); } catch { }   // 确保 ChatStore 取到 OOOR 真实配置目录下的 ooor.db

            btnRefresh.Click += (s, e) => LoadRecords();
            refreshToolStripMenuItem.Click += (s, e) => LoadRecords();
            btnRename.Click += (s, e) => RenameSelected();
            renameToolStripMenuItem.Click += (s, e) => RenameSelected();
            btnDelete.Click += (s, e) => DeleteSelected();
            deleteToolStripMenuItem.Click += (s, e) => DeleteSelected();
            btnContinue.Click += (s, e) => ContinueRecord(SelectedRecord);
            continueToolStripMenuItem.Click += (s, e) => ContinueRecord(_contextRecord ?? SelectedRecord);

            // 右键菜单：命中哪行选哪行，并按是否命中刷新可用状态
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;

            // 容器宽度变化（含纵向滚动条出现/消失）→ 同步每行宽度
            panelRows.ClientSizeChanged += (s, e) => ReflowRowWidths();

            // 滚动条出现/消失时面板宽度也会变（AutoScroll 内部布局变化延迟），二次重排兜底
            panelRows.Padding = new Padding(0, 0, 4, 0);

            LoadRecords();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、工具栏按钮、右键菜单、空列表占位）</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("ccr.title");

            // 工具栏按钮
            btnRefresh.Text = L.T("ccr.btn.refresh");
            btnContinue.Text = L.T("ccr.btn.continue");
            btnRename.Text = L.T("ccr.btn.rename");
            btnDelete.Text = L.T("ccr.btn.delete");

            // 右键菜单（与工具栏按钮同词）
            continueToolStripMenuItem.Text = L.T("ccr.btn.continue");
            refreshToolStripMenuItem.Text = L.T("ccr.btn.refresh");
            renameToolStripMenuItem.Text = L.T("ccr.btn.rename");
            deleteToolStripMenuItem.Text = L.T("ccr.btn.delete");

            // 空列表占位
            lblEmpty.Text = L.T("ccr.empty");
        }

        // ==================== 行列表加载 ====================

        private void LoadRecords()
        {
            // 刷新前按记录 Id 保留选中（修改标题等操作后重建列表不丢选中）
            string selectedId = (_selectedIndex >= 0 && _selectedIndex < panelRows.Controls.Count)
                ? (panelRows.Controls[_selectedIndex] as ChatRecordRowControl)?.Record?.Id
                : null;

            panelRows.SuspendLayout();
            try
            {
                // lblEmpty 是常驻占位控件，不能 Dispose，先摘下
                if (lblEmpty.Parent == panelRows) panelRows.Controls.Remove(lblEmpty);
                foreach (Control c in panelRows.Controls) c.Dispose();
                panelRows.Controls.Clear();
                _selectedIndex = -1;

                var records = ChatStore.All();
                int idx = 0;
                foreach (var c in records)
                {
                    var rc = new ChatRecordRowControl { Tag = c };
                    rc.Bind(idx, c);
                    rc.SetRowParity((idx % 2) == 1);
                    rc.Location = new Point(0, idx * ChatRecordRowControl.RowHeight);
                    rc.RowSelected += SetSelectedFromRow;
                    rc.RowActivated += ContinueFromRow;
                    panelRows.Controls.Add(rc);
                    if (selectedId != null && c.Id == selectedId) SetSelected(idx);
                    idx++;
                }

                // 空列表占位
                if (idx == 0) panelRows.Controls.Add(lblEmpty);
            }
            finally { panelRows.ResumeLayout(true); }
            ReflowRowWidths();
            // 滚动条出现/消失会改变 ClientSize，但事件触发有延迟；句柄已创建才用 BeginInvoke 延迟到下一消息循环
            if (IsHandleCreated) BeginInvoke((MethodInvoker)ReflowRowWidths);
            else ReflowRowWidths();
            UpdateActionButtons();
        }

        private void ReflowRowWidths()
        {
            int w = panelRows.ClientSize.Width;
            if (w <= 0) return;
            foreach (Control c in panelRows.Controls)
                if (c is ChatRecordRowControl rc) rc.SetRowWidth(w);
        }

        private void SetSelectedFromRow(int rowIndex) => SetSelected(rowIndex);

        private void SetSelected(int rowIndex)
        {
            if (rowIndex == _selectedIndex) return;
            if (_selectedIndex >= 0 && _selectedIndex < panelRows.Controls.Count)
                ((ChatRecordRowControl)panelRows.Controls[_selectedIndex]).Selected = false;
            if (rowIndex >= 0 && rowIndex < panelRows.Controls.Count)
                ((ChatRecordRowControl)panelRows.Controls[rowIndex]).Selected = true;
            _selectedIndex = rowIndex;
            UpdateActionButtons();
        }

        /// <summary>工具栏/右键项的可用状态：无选中行时只有「刷新」可用。</summary>
        private void UpdateActionButtons()
        {
            bool has = SelectedRecord != null;
            btnContinue.Enabled = has;
            btnRename.Enabled = has;
            btnDelete.Enabled = has;
        }

        private ChatRecord SelectedRecord =>
            (_selectedIndex >= 0 && _selectedIndex < panelRows.Controls.Count)
                ? (panelRows.Controls[_selectedIndex] as ChatRecordRowControl)?.Record
                : null;

        // ==================== 右键菜单 ====================

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // ListView 已移除，用鼠标坐标反推点到哪一行；滚动后需补 AutoScrollPosition 偏移
            var pos = panelRows.PointToClient(Cursor.Position);
            pos.Offset(panelRows.AutoScrollPosition);
            _contextRecord = null;
            for (int i = 0; i < panelRows.Controls.Count; i++)
            {
                if (panelRows.Controls[i].Bounds.Contains(pos))
                {
                    _contextRecord = (panelRows.Controls[i] as ChatRecordRowControl)?.Record;
                    SetSelected(i);
                    break;
                }
            }
            bool has = _contextRecord != null;
            continueToolStripMenuItem.Enabled = has;
            renameToolStripMenuItem.Enabled = has;
            deleteToolStripMenuItem.Enabled = has;
        }

        // ==================== 操作 ====================

        private void RenameSelected()
        {
            var c = SelectedRecord;
            if (c == null) return;
            string newTitle = c.Title ?? "";
            if (!InputBox.Show(this, L.T("ccr.rename.title"), L.T("ccr.rename.prompt"), ref newTitle)) return;
            c.Title = newTitle.Trim();
            ChatStore.Update(c);
            LoadRecords();
        }

        private void DeleteSelected()
        {
            var c = SelectedRecord;
            if (c == null) return;
            if (MessageBox.Show(this, string.Format(L.T("ccr.delete.confirm"), c.Title ?? L.T("ccr.untitled")),
                L.T("ccr.delete.caption"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ChatStore.Delete(c.Id);
            LoadRecords();
        }

        private void ContinueFromRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= panelRows.Controls.Count) return;
            ContinueRecord((panelRows.Controls[rowIndex] as ChatRecordRowControl)?.Record);
        }

        private void ContinueRecord(ChatRecord c)
        {
            if (c == null) return;
            LaunchCli("--session \"" + c.Id + "\"");
        }

        // ==================== 拉起 Ooor-cli ====================

        private void LaunchCli(string extraArgs)
        {
            string exe = Path.Combine(Application.StartupPath, "Ooor-cli.exe");
            if (!File.Exists(exe))
            {
                MessageBox.Show(this, string.Format(L.T("ccr.msg.cliMissing"), exe),
                    L.T("ccr.caption.continue"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string cwd;
            try { cwd = LlamaRuntime.ConfigRoot; }
            catch { cwd = Application.StartupPath; }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    // --session 恢复历史；--pause 服务未就绪时停窗不闪退；--root 把工作目录加入沙盒
                    Arguments = "--pause --root \"" + cwd + "\" " + extraArgs,
                    UseShellExecute = true,
                    WorkingDirectory = cwd
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L.T("ccr.msg.launchFail"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
