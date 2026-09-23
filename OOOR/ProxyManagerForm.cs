using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// GitHub 加速代理结构化管理窗口：
    ///   - 列表展示所有代理 URL（☑ 表示启用，☐ 表示禁用；测速列显示延迟）
    ///   - 操作：新增 / 编辑 / 删除 / 启用-禁用 / 上移 / 下移
    ///           / 测速选中 / 测速全部 / 恢复内置默认 / 打开配置文件
    ///   - 保存：按 ListView 当前顺序写回 {ConfigRoot}\github-proxy.txt
    ///           启用项正常行；禁用项加 "# " 前缀；文件头追加生成注释
    ///
    /// 界面布局（InitializeComponent）见 ProxyManagerForm.Designer.cs；
    /// 事件绑定与业务逻辑在本文件——lambda / async 处理器无法被设计器序列化，
    /// 因此统一保留在构造函数中，便于设计器重排布局时不会丢失事件。
    /// </summary>
    internal partial class ProxyManagerForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>代理条目</summary>
        private class Item
        {
            public string Url;        // 原始 URL
            public bool Enabled;      // 是否启用
            public long PingMs = -1;  // 测速毫秒；-1 = 未测
            public bool PingError;    // 测速失败
        }

        public ProxyManagerForm()
        {
            InitializeComponent();

            // 事件
            listProxies.SelectedIndexChanged += (s, e) => UpdateButtonsEnabled();
            listProxies.ItemChecked += Lv_ItemChecked;
            btnEnable.Click += (s, e) => SetSelectedEnabled(true);
            btnDisable.Click += (s, e) => SetSelectedEnabled(false);
            btnUp.Click += (s, e) => MoveSelected(-1);
            btnDown.Click += (s, e) => MoveSelected(+1);
            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnDel.Click += BtnDel_Click;
            btnPingSel.Click += async (s, e) => await PingSelectedAsync();
            btnPingAll.Click += async (s, e) => await PingAllAsync();
            btnReset.Click += BtnReset_Click;
            btnOpen.Click += BtnOpen_Click;

            // 初始加载
            LoadItems();
            UpdateSummary();
            UpdateButtonsEnabled();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、列头、按钮）与动态行内容（状态列、测速列、汇总）</summary>
        protected override void ApplyLanguage()
        {
            // 标题栏
            Text = L.T("pxy.title");

            // 列头
            colState.Text = L.T("pxy.col.state");
            colPing.Text = L.T("pxy.col.ping");
            columnHeader1.Text = L.T("pxy.col.desc");

            // 按钮
            btnEnable.Text = L.T("pxy.btn.enable");
            btnDisable.Text = L.T("pxy.btn.disable");
            btnUp.Text = L.T("pxy.btn.up");
            btnDown.Text = L.T("pxy.btn.down");
            btnAdd.Text = L.T("pxy.btn.add");
            btnEdit.Text = L.T("pxy.btn.edit");
            btnDel.Text = L.T("pxy.btn.delete");
            btnPingSel.Text = L.T("pxy.btn.pingSelected");
            btnPingAll.Text = L.T("pxy.btn.pingAll");
            btnReset.Text = L.T("pxy.btn.reset");
            btnOpen.Text = L.T("pxy.btn.open");
            btnOk.Text = L.T("pxy.btn.save");
            btnCancel.Text = L.T("pxy.btn.cancel");

            // 动态行：状态列与测速列按保存的状态重刷
            foreach (ListViewItem lvi in listProxies.Items)
            {
                var it = (Item)lvi.Tag;
                lvi.SubItems[1].Text = it.Enabled ? L.T("pxy.state.enabled") : L.T("pxy.state.disabled");
                lvi.SubItems[2].Text = it.PingError
                    ? L.T("pxy.ping.failed")
                    : (it.PingMs >= 0 ? it.PingMs + " ms" : "-");
            }

            // 底部汇总
            UpdateSummary();
        }

        // ============ 数据加载与显示 ============

        private void LoadItems()
        {
            listProxies.Items.Clear();
            var proxies = GithubProxy.Proxies;
            if (proxies == null || proxies.Count == 0)
                proxies = GithubProxy.BuiltinProxies; // 兜底
            foreach (var url in proxies)
            {
                AddRow(new Item { Url = url, Enabled = true });
            }
            if (listProxies.Items.Count > 0)
            {
                listProxies.Items[0].Selected = true;
                listProxies.Focus();
            }
        }

        private ListViewItem AddRow(Item it)
        {
            var lvi = new ListViewItem(it.Url) { Checked = it.Enabled, Tag = it };
            lvi.SubItems.Add(it.Enabled ? L.T("pxy.state.enabled") : L.T("pxy.state.disabled"));
            lvi.SubItems.Add("-");
            // 禁用项用灰色显示
            if (!it.Enabled)
                lvi.ForeColor = SystemColors.GrayText;
            listProxies.Items.Add(lvi);
            return lvi;
        }

        private void UpdateSummary()
        {
            int total = listProxies.Items.Count;
            int enabled = listProxies.Items.Cast<ListViewItem>().Count(i => i.Checked);
            lblSummary.Text = string.Format(L.T("pxy.summary"), total, enabled);
        }

        private void UpdateButtonsEnabled()
        {
            bool hasSel = listProxies.SelectedIndices.Count > 0;
            btnEnable.Enabled = btnDisable.Enabled = btnEdit.Enabled = btnDel.Enabled = btnPingSel.Enabled = hasSel;
            if (hasSel)
            {
                int idx = listProxies.SelectedIndices[0];
                btnUp.Enabled = idx > 0;
                btnDown.Enabled = idx < listProxies.Items.Count - 1;
            }
            else
            {
                btnUp.Enabled = btnDown.Enabled = false;
            }
        }

        private void Lv_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            var it = (Item)e.Item.Tag;
            it.Enabled = e.Item.Checked;
            e.Item.SubItems[1].Text = it.Enabled ? L.T("pxy.state.enabled") : L.T("pxy.state.disabled");
            e.Item.ForeColor = it.Enabled ? listProxies.ForeColor : SystemColors.GrayText;
            UpdateSummary();
        }

        // ============ 操作 ============

        private void SetSelectedEnabled(bool en)
        {
            foreach (int idx in listProxies.SelectedIndices)
            {
                listProxies.Items[idx].Checked = en;
            }
        }

        private void MoveSelected(int delta)
        {
            if (listProxies.SelectedIndices.Count == 0) return;
            int idx = listProxies.SelectedIndices[0];
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= listProxies.Items.Count) return;

            var item = listProxies.Items[idx];
            listProxies.BeginUpdate();
            listProxies.Items.RemoveAt(idx);
            listProxies.Items.Insert(newIdx, item);
            listProxies.EndUpdate();
            item.Selected = true;
            item.Focused = true;
            listProxies.Focus();
            UpdateButtonsEnabled();
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string url;
            url = PromptUrl(L.T("pxy.prompt.addTitle"), "");
            if (string.IsNullOrWhiteSpace(url)) return;
            url = NormalizeUrl(url);
            if (!IsValidUrl(url))
            {
                MessageBox.Show(this, string.Format(L.T("pxy.msg.invalidUrl"), url),
                    L.T("pxy.caption.notice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (HasDuplicate(url, null))
            {
                MessageBox.Show(this, L.T("pxy.msg.duplicate"),
                    L.T("pxy.caption.notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var lvi = AddRow(new Item { Url = url, Enabled = true });
            lvi.Selected = true;
            lvi.Focused = true;
            listProxies.Focus();
            UpdateSummary();
            UpdateButtonsEnabled();
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            if (listProxies.SelectedIndices.Count == 0) return;
            int idx = listProxies.SelectedIndices[0];
            var lvi = listProxies.Items[idx];
            var it = (Item)lvi.Tag;
            string url = PromptUrl(L.T("pxy.prompt.editTitle"), it.Url);
            if (string.IsNullOrWhiteSpace(url)) return;
            url = NormalizeUrl(url);
            if (!IsValidUrl(url))
            {
                MessageBox.Show(this, string.Format(L.T("pxy.msg.invalidUrl"), url),
                    L.T("pxy.caption.notice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (HasDuplicate(url, lvi))
            {
                MessageBox.Show(this, L.T("pxy.msg.duplicate"),
                    L.T("pxy.caption.notice"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            it.Url = url;
            lvi.Text = url;
        }

        private void BtnDel_Click(object sender, EventArgs e)
        {
            if (listProxies.SelectedIndices.Count == 0) return;
            var lvi = listProxies.Items[listProxies.SelectedIndices[0]];
            if (MessageBox.Show(this, string.Format(L.T("pxy.msg.deleteConfirm"), lvi.Text),
                    L.T("pxy.caption.delete"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
                != DialogResult.OK) return;
            listProxies.Items.Remove(lvi);
            UpdateSummary();
            UpdateButtonsEnabled();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                    L.T("pxy.msg.resetConfirm"),
                    L.T("pxy.caption.reset"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
                != DialogResult.OK) return;
            listProxies.Items.Clear();
            foreach (var url in GithubProxy.BuiltinProxies)
            {
                AddRow(new Item { Url = url, Enabled = true });
            }
            if (listProxies.Items.Count > 0)
            {
                listProxies.Items[0].Selected = true;
                listProxies.Focus();
            }
            UpdateSummary();
            UpdateButtonsEnabled();
        }

        private void BtnOpen_Click(object sender, EventArgs e)
        {
            try
            {
                var path = GithubProxy.ConfigPath;
                if (!File.Exists(path)) File.WriteAllText(path, "");
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("pxy.msg.openFail"), ex.Message),
                    L.T("pxy.caption.notice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ============ 测速 ============

        private async Task PingSelectedAsync()
        {
            if (listProxies.SelectedIndices.Count == 0) return;
            var lvi = listProxies.Items[listProxies.SelectedIndices[0]];
            var it = (Item)lvi.Tag;
            Cursor = Cursors.WaitCursor;
            var (ms, ok) = await PingAsync(it.Url);
            Cursor = Cursors.Default;
            ApplyPingResult(lvi, it, ms, ok);
        }

        private async Task PingAllAsync()
        {
            Cursor = Cursors.WaitCursor;
            btnPingAll.Enabled = false;
            try
            {
                foreach (ListViewItem lvi in listProxies.Items)
                {
                    var it = (Item)lvi.Tag;
                    var (ms, ok) = await PingAsync(it.Url);
                    ApplyPingResult(lvi, it, ms, ok);
                    Application.DoEvents();   // 让 UI 有机会重绘、保留取消能力
                }
            }
            finally
            {
                btnPingAll.Enabled = true;
                Cursor = Cursors.Default;
            }
        }

        private void ApplyPingResult(ListViewItem lvi, Item it, long ms, bool ok)
        {
            it.PingMs = ms;
            it.PingError = !ok;
            lvi.SubItems[2].Text = ok ? (ms + " ms") : L.T("pxy.ping.failed");
            // 颜色：绿 < 800ms / 黄 < 2000ms / 红 ≥ 2000ms 或失败
            if (!ok) lvi.SubItems[2].ForeColor = Color.Red;
            else if (ms < 800) lvi.SubItems[2].ForeColor = Color.Green;
            else if (ms < 2000) lvi.SubItems[2].ForeColor = Color.OrangeRed;
            else lvi.SubItems[2].ForeColor = Color.DarkRed;
        }

        /// <summary>
        /// HEAD 测速（部分镜像不支持 HEAD → 自动回落 GET）。
        /// 直连占位（github.com）按 0ms/成功处理（不需要测速）。
        /// </summary>
        private static Task<(long ms, bool ok)> PingAsync(string url)
        {
            if (string.IsNullOrEmpty(url) || GithubProxy.IsGithub(url))
                return Task.FromResult((0L, true));

            return Task.Run<(long, bool)>(() =>
            {
                // 第一次：HEAD
                if (TryProbe(url, "HEAD", 5000, out long ms)) return (ms, true);
                // 第二次：GET
                if (TryProbe(url, "GET", 5000, out ms)) return (ms, true);
                return (0L, false);
            });
        }

        private static bool TryProbe(string url, string method, int timeoutMs, out long elapsedMs)
        {
            elapsedMs = 0;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = method;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = "ooor-Ping";
                if (method == "GET") req.AllowAutoRedirect = false;
                var sw = Stopwatch.StartNew();
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    sw.Stop();
                    elapsedMs = sw.ElapsedMilliseconds;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // ============ 工具 ============

        private string PromptUrl(string title, string defaultValue)
        {
            string text = defaultValue ?? "";
            if (!InputBox.Show(this, title, L.T("pxy.prompt.urlLabel"), ref text))
                return null;
            return text;
        }

        private static string NormalizeUrl(string url)
        {
            url = (url ?? "").Trim();
            // 自动补 https://
            if (url.Length > 0
                && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            // 去掉末尾的 / —— 拼装时也会 TrimEnd('/')，但这里展示统一
            url = url.TrimEnd('/');
            return url;
        }

        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            return u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps;
        }

        private bool HasDuplicate(string url, ListViewItem exclude)
        {
            foreach (ListViewItem lvi in listProxies.Items)
            {
                if (lvi == exclude) continue;
                var u = (lvi.Tag as Item)?.Url;
                if (!string.IsNullOrEmpty(u)
                    && string.Equals(u, url, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // ============ 入口（保持与旧版签名一致） ============

        /// <summary>
        /// 弹出管理窗口。用户点"保存" → 回调 onSaved(写入文件的完整文本)。
        /// 第二参数（旧的 currentText）已不再使用——结构化 UI 自行维护列表数据。
        /// </summary>
        public static bool ShowDialog(IWin32Window owner, string ignoredCurrentText, Action<string> onSaved)
        {
            using (var f = new ProxyManagerForm())
            {
                if (f.ShowDialog(owner) != DialogResult.OK) return false;

                var sb = new StringBuilder();
                sb.AppendLine(L.T("pxy.file.header1"));
                sb.AppendLine(L.T("pxy.file.header2"));
                sb.AppendLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                foreach (ListViewItem lvi in f.listProxies.Items)
                {
                    var it = (Item)lvi.Tag;
                    if (string.IsNullOrEmpty(it.Url)) continue;
                    string line = it.Url;
                    if (!it.Enabled) line = "# " + line;
                    sb.AppendLine(line);
                }

                onSaved?.Invoke(sb.ToString());
                return true;
            }
        }
    }
}
