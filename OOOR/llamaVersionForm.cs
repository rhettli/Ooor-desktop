using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// llama 版本管理窗口：
    ///   - 列表展示 llama-bin 下已安装的 llama.cpp 版本（含 llama-server.exe 的子目录），
    ///     显示版本名 / 安装目录 / 目录大小 / 是否当前使用中
    ///   - 右键菜单：使用此版本 / 打开安装目录 / 刷新 / 安装新版本 / 删除此版本（删到回收站）
    /// </summary>
    internal partial class llamaVersionForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>最近一次扫描到的版本列表（供 ApplyLanguage 刷新标题）</summary>
        private List<VersionInfo> _versions = new List<VersionInfo>();

        public llamaVersionForm()
        {
            InitializeComponent();
            Load += llamaVersionForm_Load;
            // 每次窗口激活时重扫（下载窗口装完新版本切回来即可看到，无需手动刷新）
            Activated += llamaVersionForm_Activated;
            // 选中行变化 → 同步工具栏按钮的启用/勾选态（跟右键菜单打开时的逻辑一致）
            listView1.SelectedIndexChanged += (s, e) => UpdateToolbarButtons();
        }

        private void llamaVersionForm_Load(object sender, EventArgs e)
        {
            LoadVersions();
        }

        private void llamaVersionForm_Activated(object sender, EventArgs e)
        {
            LoadVersions();
        }

        /// <summary>按当前语言刷新窗口静态文本（列头、菜单、工具栏、提示）及动态标题/状态列</summary>
        protected override void ApplyLanguage()
        {
            // 列头
            columnHeaderVersion.Text = L.T("lvf.col.version");
            columnHeaderPath.Text = L.T("lvf.col.path");
            columnHeaderSize.Text = L.T("lvf.col.size");
            columnHeaderState.Text = L.T("lvf.col.state");

            // 右键菜单
            miRefresh.Text = L.T("lvf.refresh");
            miInstallNew.Text = L.T("lvf.menu.install");
            miOpenDir.Text = L.T("lvf.menu.openDir");
            miDisableVersion.Text = L.T("lvf.menu.disable");
            miDeleteVersion.Text = L.T("lvf.menu.delete");

            // 工具栏按钮
            toolStripBtnRefresh.Text = L.T("lvf.refresh");
            toolStripBtnRefresh.ToolTipText = L.T("lvf.tip.refresh");
            toolStripBtnInstall.Text = L.T("lvf.menu.install");
            toolStripBtnInstall.ToolTipText = L.T("lvf.tip.install");
            toolStripBtnDisable.Text = L.T("lvf.btn.disable");
            toolStripBtnDisable.ToolTipText = L.T("lvf.tip.disable");
            toolStripBtnOpenDir.Text = L.T("lvf.btn.openDir");
            toolStripBtnOpenDir.ToolTipText = L.T("lvf.tip.openDir");
            toolStripBtnDelete.Text = L.T("lvf.btn.delete");
            toolStripBtnDelete.ToolTipText = L.T("lvf.tip.delete");

            // 标题与「状态」列为动态内容，按缓存数据就地刷新（不重新扫描磁盘）
            Text = string.Format(L.T("lvf.title.count"), _versions.Count, LlamaRuntime.LlamaBinsDir);
            string current = LlamaRuntime.SelectedVersion;
            foreach (ListViewItem it in listView1.Items)
            {
                var vv = it.Tag as VersionInfo;
                if (vv != null) it.SubItems[3].Text = VersionStateText(vv, current);
            }
        }

        // ==================== 列表加载 ====================

        /// <summary>扫描本地已安装版本，填充到 ListView</summary>
        private void LoadVersions()
        {
            List<VersionInfo> versions = LlamaRuntime.ScanLlamaVersions();
            string current = LlamaRuntime.SelectedVersion;
            _versions = versions ?? new List<VersionInfo>();

            listView1.BeginUpdate();
            try
            {
                listView1.Items.Clear();
                foreach (VersionInfo v in versions)
                {
                    var item = new ListViewItem(v.Name) { Tag = v };
                    item.SubItems.Add(v.FullPath);
                    item.SubItems.Add(FormatSize(GetDirSize(v.FullPath)));
                    item.SubItems.Add(VersionStateText(v, current));
                    listView1.Items.Add(item);
                }
            }
            finally
            {
                listView1.EndUpdate();
            }

            Text = string.Format(L.T("lvf.title.count"), versions.Count, LlamaRuntime.LlamaBinsDir);
            UpdateToolbarButtons();   // 重扫后：工具栏按钮的启用/禁用 + 「禁用」勾选态跟新数据同步
        }

        /// <summary>
        /// 同步顶部工具栏按钮的启用态与「禁用」勾选态（跟右键菜单 Opening 时同样的判定）：
        ///   - 选中行时：禁用/打开目录/删除版本 可用；「禁用」勾选 = IsVersionDisabled(v.Name)
        ///   - 未选中行：上述三个按钮灰显；刷新/安装新版本 始终可用
        /// 复用了 miXxx_Click 的所有 Click handler，所以按钮和菜单项永远走同一条逻辑。
        /// </summary>
        private void UpdateToolbarButtons()
        {
            VersionInfo v = SelectedVersionInfo;
            bool has = v != null;
            bool disabled = has && LlamaRuntime.IsVersionDisabled(v.Name);

            toolStripBtnDisable.Enabled = has;
            toolStripBtnDisable.Checked = disabled;   // 「禁用」按钮的勾选态 = 当前是否禁用（CheckOnClick=true）

            toolStripBtnOpenDir.Enabled = has;
            toolStripBtnDelete.Enabled = has;

            // 刷新 / 安装新版本 无需选中行
            toolStripBtnRefresh.Enabled = true;
            toolStripBtnInstall.Enabled = true;
        }

        /// <summary>当前选中的版本（未选中返回 null）</summary>
        private VersionInfo SelectedVersionInfo
        {
            get
            {
                var item = listView1.SelectedItems.Count > 0 ? listView1.SelectedItems[0] : null;
                return item != null ? item.Tag as VersionInfo : null;
            }
        }

        // ==================== 右键菜单 ====================

        /// <summary>右键菜单弹出前：无选中行时禁用针对具体版本的操作</summary>
        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            VersionInfo v = SelectedVersionInfo;
            bool has = v != null;
            bool disabled = has && LlamaRuntime.IsVersionDisabled(v.Name);

            // 「禁用」项：勾选 = 当前已禁用；没选行就禁用
            miDisableVersion.Enabled = has;
            miDisableVersion.Checked = disabled;

            miOpenDir.Enabled = has;
            miDeleteVersion.Enabled = has;
        }

        /// <summary>使用此版本：写入运行时选中状态（主程序下拉框下次重扫时跟随）</summary>
        private void miUseVersion_Click(object sender, EventArgs e)
        {
            VersionInfo v = SelectedVersionInfo;
            if (v == null) return;
            if (LlamaRuntime.IsVersionDisabled(v.Name)) return;   // 禁用版本不允许切换为选中

            LlamaRuntime.SetSelectedVersion(v.Name);
            LoadVersions();
        }

        /// <summary>
        /// 右键菜单「禁用」：切换该版本的启用/禁用状态并立即落盘。
        /// 禁用当前使用中版本时 LlamaRuntime 会同步清空选中状态。
        /// </summary>
        private void miDisableVersion_Click(object sender, EventArgs e)
        {
            VersionInfo v = SelectedVersionInfo;
            if (v == null) return;

            bool disabled = !LlamaRuntime.IsVersionDisabled(v.Name);
            LlamaRuntime.SetVersionDisabled(v.Name, disabled);

            // 菜单项勾选状态由 contextMenuStrip1_Opening 在下次弹出时按 IsVersionDisabled 重新计算
            LoadVersions();
        }

        /// <summary>「状态」列文字：禁用优先；否则按是否是当前选中版本显示使用中；都无则空字符串</summary>
        private static string VersionStateText(VersionInfo v, string current)
        {
            if (LlamaRuntime.IsVersionDisabled(v.Name)) return L.T("lvf.state.disabled");
            return string.Equals(v.Name, current, StringComparison.OrdinalIgnoreCase) ? L.T("lvf.state.inUse") : "";
        }

        /// <summary>打开安装目录（资源管理器）</summary>
        private void miOpenDir_Click(object sender, EventArgs e)
        {
            VersionInfo v = SelectedVersionInfo;
            if (v == null) return;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = v.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("lvf.msg.openDirFail"), ex.Message), L.T("lvf.caption.openDir"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>刷新：重新扫描 llama-bin</summary>
        private void miRefresh_Click(object sender, EventArgs e)
        {
            LoadVersions();
        }

        /// <summary>安装新版本：打开发布版下载窗口</summary>
        private void miInstallNew_Click(object sender, EventArgs e)
        {
            new LlamaDownloadForm().Show(this);
        }

        /// <summary>删除此版本：删除到回收站（可还原）；删除的是当前使用版本时同时清除选中状态</summary>
        private void miDeleteVersion_Click(object sender, EventArgs e)
        {
            VersionInfo v = SelectedVersionInfo;
            if (v == null) return;

            if (MessageBox.Show(this,
                    string.Format(L.T("lvf.msg.deleteConfirm"), v.Name, v.FullPath),
                    L.T("lvf.caption.deleteConfirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(v.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("lvf.msg.deleteFail"), ex.Message), L.T("lvf.caption.delete"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (string.Equals(LlamaRuntime.SelectedVersion, v.Name, StringComparison.OrdinalIgnoreCase))
                LlamaRuntime.SetSelectedVersion(null);

            LoadVersions();
        }

        // ==================== 辅助 ====================

        /// <summary>统计目录大小（递归；枚举失败的部分按 0 计）</summary>
        private static long GetDirSize(string dir)
        {
            long total = 0;
            try
            {
                foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; }
                    catch { /* 单文件读取失败忽略 */ }
                }
            }
            catch
            {
                // 目录被占用/无权限：返回已统计到的部分
            }
            return total;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            const long KB = 1024, MB = KB * 1024, GB = MB * 1024;
            if (bytes >= GB) return (bytes / (double)GB).ToString("0.##") + " GB";
            if (bytes >= MB) return (bytes / (double)MB).ToString("0.##") + " MB";
            if (bytes >= KB) return (bytes / (double)KB).ToString("0.##") + " KB";
            return bytes + " B";
        }
    }
}
