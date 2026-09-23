using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 关于窗口：名称 / 当前版本 / 功能说明 / 配置目录，
    /// 打开时自动向服务端拉取最新版本（GET /api/v1/releases/latest?current=当前版本），
    /// 有新版本则展示版本信息，并可点击链接跳转 https://ooor.cc。
    ///
    /// 窗口尺寸、控件与位置全部在本类的设计文件 AboutForm.Designer.cs；
    /// 本文件只保留数据填充、异步更新检查与链接跳转等逻辑。
    /// </summary>
    internal sealed partial class AboutForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        /// <summary>正在进行的更新检查；关闭窗口时取消，避免请求回调操作已释放的控件</summary>
        private CancellationTokenSource _cts;

        // 最近一次更新检查结果（语言切换时在不重新请求的前提下重渲染更新区）
        private bool _hasResult;
        private OoorRelease _release;
        private string _errorMessage;
        private string _errorUrl;

        public AboutForm()
        {
            InitializeComponent();

            // 更新区：先给占位状态，Shown 后异步拉取真实结果（不阻塞窗口弹出）
            lblUpdateInfo.Text = "";
            lnkLatest.Visible = false;      // 检查出结果前不显示，避免空跳转

            lnkLatest.LinkClicked += (s, e) => OpenHome();
            lnkGitHub.LinkClicked += (s, e) => OpenGitHub();
            Shown += (s, e) => _ = CheckUpdateAsync();
            FormClosed += (s, e) => { try { _cts?.Cancel(); } catch { } };
        }

        /// <summary>按当前语言刷新全部静态文本，并按最近一次检查结果重渲染更新区</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("abt.title");
            lblTitle.Text = L.T("abt.appTitle");
            lblVersion.Text = string.Format(L.T("abt.version"), DEF.ver);
            lblDesc.Text = L.T("abt.desc");
            lblDirs.Text = string.Format(L.T("abt.dirs"), LlamaRuntime.ConfigRoot);
            btnOk.Text = L.T("abt.ok");

            lnkLatest.Text = string.Format(L.T("abt.link"), OoorUpdate.HomeUrl);
            lnkLatest.LinkArea = new LinkArea(0, lnkLatest.Text.Length);

            lnkGitHub.Text = L.T("abt.github");
            lnkGitHub.LinkArea = new LinkArea(0, lnkGitHub.Text.Length);

            RenderUpdateSection();
        }

        // ==================== 更新检查 ====================

        private async Task CheckUpdateAsync()
        {
            _cts = new CancellationTokenSource();
            try
            {
                var r = await OoorUpdate.CheckLatestAsync(DEF.ver, _cts.Token);
                if (IsDisposed) return;
                _release = r;
                _hasResult = true;
                ShowRelease(r);
            }
            catch (OperationCanceledException)
            {
                // 窗口已关闭，放弃
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _errorUrl = OoorUpdate.CurrentServerUrl();
                _errorMessage = ex.Message;
                ShowError(_errorMessage, _errorUrl);
            }
        }

        /// <summary>按缓存的检查结果重渲染更新区（语言切换时调用，不重新发请求）</summary>
        private void RenderUpdateSection()
        {
            if (_errorUrl != null)
            {
                ShowError(_errorMessage, _errorUrl);
            }
            else if (_hasResult)
            {
                ShowRelease(_release);
            }
            else
            {
                SetUpdateState(L.T("abt.update.checking"), SystemColors.ControlText);
                lblUpdateInfo.Text = "";
                lnkLatest.Visible = false;
            }
        }

        private void ShowError(string message, string url)
        {
            SetUpdateState(L.T("abt.update.failed"), WarnColor);
            lblUpdateInfo.Text = message
                + "\r\n" + string.Format(L.T("abt.update.requestUrl"), url)
                + "\r\n" + FailureHint(url);
            lnkLatest.Visible = true;
        }

        private void ShowRelease(OoorRelease r)
        {
            string date = FormatPublishedAt(r.PublishedAt);
            string size = DownloadManager.FormatSize(r.Size);

            if (r.HasUpdate)
            {
                string state = string.Format(L.T("abt.update.newVersion"), r.Version);
                if (r.Force) state += L.T("abt.update.force");
                SetUpdateState(state, WarnColor);

                var sb = new StringBuilder();
                sb.Append(string.Format(L.T("abt.update.channel"),
                    string.IsNullOrEmpty(r.Channel) ? "stable" : r.Channel));
                if (date.Length > 0) sb.Append(string.Format(L.T("abt.update.published"), date));
                if (size.Length > 0 && r.Size > 0) sb.Append(string.Format(L.T("abt.update.size"), size));
                if (r.FileName.Length > 0) sb.Append(string.Format(L.T("abt.update.package"), r.FileName));
                string notes = FirstLines(r.Notes, 2);
                if (notes.Length > 0) sb.Append(string.Format(L.T("abt.update.notes"), notes));
                lblUpdateInfo.Text = sb.ToString();
            }
            else
            {
                SetUpdateState(string.Format(L.T("abt.update.latest"), DEF.ver), OkColor);
                var sb = new StringBuilder();
                sb.Append(string.Format(L.T("abt.update.onlineVersion"), r.Version));
                if (date.Length > 0) sb.Append(string.Format(L.T("abt.update.published"), date));
                lblUpdateInfo.Text = sb.ToString();
            }

            lnkLatest.Visible = true;
        }

        private void SetUpdateState(string text, Color color)
        {
            lblUpdateState.Text = text;
            lblUpdateState.ForeColor = color;
        }

        /// <summary>打开官网（关于窗口的跳转目标固定为 OoorUpdate.HomeUrl）</summary>
        private void OpenHome()
        {
            try
            {
                Process.Start(OoorUpdate.HomeUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("abt.msg.openBrowserFail"), ex.Message), L.T("abt.msg.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>打开 GitHub 仓库</summary>
        private void OpenGitHub()
        {
            try
            {
                Process.Start(OoorUpdate.GitHubUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("abt.msg.openBrowserFail"), ex.Message), L.T("abt.msg.caption"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================== 小工具 ====================

        private static readonly Color WarnColor = Color.FromArgb(0xC0, 0x39, 0x2B);
        private static readonly Color OkColor = Color.FromArgb(0x2E, 0x7D, 0x32);

        /// <summary>
        /// 失败时的排障提示。地址指向本地却连不通，多半是编译期默认值（Debug = 本地网关）
        /// 且网关没启动 —— 直接指明去哪改成官网域名。
        /// </summary>
        private static string FailureHint(string url)
        {
            if (url.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
                return string.Format(L.T("abt.hint.localGateway"), OoorUpdate.HomeUrl);
            return L.T("abt.hint.retry");
        }

        /// <summary>ISO UTC（2026-09-17T08:44:49.870Z）→ 本地时间 yyyy-MM-dd HH:mm</summary>
        private static string FormatPublishedAt(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "";
            DateTime t;
            if (DateTime.TryParse(iso.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t))
                return t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return iso;
        }

        /// <summary>取更新日志的前 n 条非空行，用 " / " 连接（关于窗口只显示开头）</summary>
        private static string FirstLines(string text, int n)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var sb = new StringBuilder();
            int used = 0;
            foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (used > 0) sb.Append(" / ");
                sb.Append(line);
                if (++used >= n) break;
            }
            return sb.ToString();
        }
    }
}
