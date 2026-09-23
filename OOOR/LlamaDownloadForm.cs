using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 下载 llama.cpp 版本窗口：
    ///   - 左侧 ListBox 列出最新 N 个版本；选中版本后右侧的「下载 xx 版本」按钮
    ///     与该版本的可下载资产（zip）匹配上才可用（win-cpu / win-cuda / win-vulkan…）
    ///   - 点击按钮把任务提交给 DownloadManager 单例队列（不再逐文件弹下载窗）：
    ///       · CPU 版本 = 主程序 zip，解压到 {llama-bin}\{zip 文件名去扩展名}
    ///       · CPU+CUDA 版本 = 主程序 zip + CUDA 运行库 zip 两个任务，
    ///         且 CUDA 运行库解压到 CPU 版本同一目录（llama-server 需要同目录 DLL）
    ///   - 下载进度在「下载任务管理器」窗口统一查看
    ///   - 版本列表数据来源：工具条「镜像源」下拉框选择 GitHub（默认，8 秒超时）
    ///     或 Cloudflare；首选源失败自动换另一个源重试。本地缓存 24h 有效。
    /// </summary>
    public partial class LlamaDownloadForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private const string RepoOwner = "ggml-org";
        private const string Repo = "llama.cpp";
        private const string ReleasesPageUrl = "https://github.com/" + RepoOwner + "/" + Repo + "/releases";

        /// <summary>左侧 ListBox 最多展示的版本数</summary>
        private const int MaxVersions = 5;

        /// <summary>GitHub releases API（未认证每小时 60 次；必须带 User-Agent 否则 403）</summary>
        private static string ReleasesUrl
        {
            get { return "https://api.github.com/repos/" + RepoOwner + "/" + Repo + "/releases?per_page=" + MaxVersions; }
        }

        // 复用单例 HttpClient。GitHub API 要求带 User-Agent，未认证每小时 60 次。
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>GitHub 直连短超时（秒）：失败快速回退 Cloudflare，不做长等</summary>
        private const int GithubTimeoutSec = 8;

        /// <summary>Cloudflare 镜像超时（秒）</summary>
        private const int CloudflareTimeoutSec = 30;

        /// <summary>版本列表镜像源</summary>
        private enum Mirror { Github = 0, Cloudflare = 1 }

        /// <summary>下拉框当前选中的镜像源</summary>
        private Mirror SelectedMirror
        {
            get { return cmbMirror.SelectedIndex == (int)Mirror.Cloudflare ? Mirror.Cloudflare : Mirror.Github; }
        }

        /// <summary>当前已加载的 release 列表（最新在前，按 API 返回顺序，最多 MaxVersions 个）</summary>
        private List<GithubRelease> _releases = new List<GithubRelease>();

        private CancellationTokenSource _cts;
        private bool _loading;

        /// <summary>最近一次拉取失败的原因（GitHub 直连失败时显示在状态栏，便于判断是否走了回退）</summary>
        private string _lastError;

        /// <summary>请求远程接口期间盖住所有安装按钮的「请稍等」遮罩（请求结束后隐藏）</summary>
        private readonly Label _lblButtonsLoading;

        public LlamaDownloadForm()
        {
            InitializeComponent();
            Load += LlamaDownloadForm_Load;
            FormClosing += LlamaDownloadForm_FormClosing;

            // 右侧下载按钮（布局在 Designer 中）：button1=CPU，button3=CPU+CUDA，
            // button5=ROCm，button4=SYCL，button2=Vulkan
            button1.Click += btnCpu_Click;
            button3.Click += btnCpuCuda_Click;
            button5.Click += btnRocm_Click;
            button4.Click += btnSycl_Click;
            button2.Click += btnVulkan_Click;
            SetVersionButtonsEnabled(false);

            // 「请稍等」遮罩：覆盖全部安装按钮（含右侧 ✔ 标签），请求期间显示，
            // 完成后在 LoadAsync 的 finally 中隐藏
            _lblButtonsLoading = new Label
            {
                Text = L.T("ldf.loading.wait"),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font(Font.FontFamily, 13F),
                ForeColor = System.Drawing.Color.FromArgb(96, 96, 96),
                BackColor = System.Drawing.Color.FromArgb(248, 248, 248),
                Dock=DockStyle.Fill,
                Visible = false
            };
            splitContainer1.Panel2.Controls.Add(_lblButtonsLoading);
            _lblButtonsLoading.BringToFront();

            // 镜像源下拉：默认 GitHub；切换后绕过缓存重新拉取
            cmbMirror.SelectedIndex = (int)Mirror.Github;
            cmbMirror.SelectedIndexChanged += cmbMirror_SelectedIndexChanged;
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、工具栏、安装按钮、遮罩等）</summary>
        protected override void ApplyLanguage()
        {
            Text = string.Format(L.T("ldf.title"), RepoOwner + "/" + Repo);

            // 工具栏
            lblMirror.Text = L.T("ldf.lbl.mirror");
            btnRefresh.Text = L.T("ldf.btn.refresh");
            btnOpenInBrowser.Text = L.T("ldf.btn.openInBrowser");

            // 右侧安装按钮（button1=CPU，button3=CPU+CUDA，button5=ROCm，button4=SYCL，button2=Vulkan）
            button1.Text = L.T("ldf.btn.cpu");
            button3.Text = L.T("ldf.btn.cpuCuda");
            button5.Text = L.T("ldf.btn.rocm");
            button4.Text = L.T("ldf.btn.sycl");
            button2.Text = L.T("ldf.btn.vulkan");

            // 动态创建的「请稍等」遮罩与初始状态栏文本
            if (_lblButtonsLoading != null) _lblButtonsLoading.Text = L.T("ldf.loading.wait");
            lblStatus.Text = L.T("ldf.status.initialLoading");
        }

        private void cmbMirror_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            _ = LoadAsync(true);   // 手动切源：忽略本地缓存，真正从所选镜像拉取
        }

        private async void LlamaDownloadForm_Load(object sender, EventArgs e)
        {
            await LoadAsync();
        }

        private void LlamaDownloadForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗口关闭时取消仍在进行的请求（已提交的下载任务在 DownloadManager 后台继续）
            if (_loading) _cts?.Cancel();
        }

        private async void btnRefresh_Click(object sender, EventArgs e) => await LoadAsync();

        private void btnOpenInBrowser_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ReleasesPageUrl,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.openBrowserFail"), ex.Message);
            }
        }

        /// <summary>
        /// 拉取 release 列表并填充到左侧 ListBox（按时间倒序保留最新 N 个）。
        /// 数据来源：本地缓存（24h，手动切源时可忽略）→ 下拉框首选镜像源，
        /// 失败自动换另一个源（GitHub ↔ Cloudflare）重试。
        /// </summary>
        private async Task LoadAsync(bool ignoreCache = false)
        {
            if (_loading) return;
            _loading = true;
            _cts = new CancellationTokenSource();
            btnRefresh.Enabled = false;
            SetVersionButtonsEnabled(false);

            // 清空旧数据
            _releases = new List<GithubRelease>();
            lstVersions.Items.Clear();

            string json = null;
            string origin = "";   // 状态栏的来源标签

            try
            {
                // 1) 先尝试读缓存（24h 内有效；手动切镜像源时 ignoreCache=true）
                DateTime cachedAt;
                json = ReleaseCache.TryRead(out cachedAt);
                if (ignoreCache) json = null;
                if (json != null)
                {
                    lblStatus.Text = string.Format(L.T("ldf.status.cache"),
                        cachedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                    origin = L.T("ldf.origin.cache");
                }

                // 2) 缓存未命中 → 首选下拉框选中的镜像源；失败自动换另一个源重试
                if (json == null)
                {
                    // 远程请求开始：显示「请稍等」遮罩盖住安装按钮（先绘制再 await，避免不刷新）
                    _lblButtonsLoading.Visible = true;
                    _lblButtonsLoading.BringToFront();
                    _lblButtonsLoading.Update();

                    Mirror first = SelectedMirror;
                    Mirror second = first == Mirror.Github ? Mirror.Cloudflare : Mirror.Github;

                    _lastError = null;
                    json = await TryFetchMirror(first);
                    if (json != null) origin = OriginLabel(first);
                    else
                    {
                        string firstErr = _lastError;
                        lblStatus.Text = string.Format(L.T("ldf.status.firstMirrorFail"),
                                       MirrorName(first), firstErr, MirrorName(second));
                        json = await TryFetchMirror(second);
                        if (json != null) origin = OriginLabel(second);
                    }

                    if (json != null) ReleaseCache.Write(json);   // 写缓存（写失败不影响主流程）
                }

                if (json == null)
                {
                    lblStatus.Text = string.Format(L.T("ldf.status.allFail"), _lastError);
                    return;
                }

                // 4) 反序列化 + 排序 + 取最新 N 个
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var releases = ser.Deserialize<List<GithubRelease>>(json) ?? new List<GithubRelease>();

                releases = releases
                    .OrderByDescending(r => ParseDateOrMin(r.published_at))
                    .Take(MaxVersions)
                    .ToList();

                _releases = releases;
                FillVersionList();

                int totalAssets = _releases.Sum(r => r.assets?.Count ?? 0);
                lblStatus.Text = string.Format(L.T("ldf.status.loaded"),
                               origin, _releases.Count, totalAssets);
            }
            catch (TaskCanceledException)
            {
                lblStatus.Text = L.T("ldf.status.cancelled");
            }
            catch (HttpRequestException ex)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.networkError"), ex.Message);
            }
            catch (InvalidOperationException ex) // JavaScriptSerializer 反序列化失败
            {
                lblStatus.Text = string.Format(L.T("ldf.status.jsonError"), ex.Message);
            }
            catch (ArgumentException ex) // JSON 不是合法 JSON
            {
                lblStatus.Text = string.Format(L.T("ldf.status.jsonError"), ex.Message);
            }
            catch (Exception ex)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.loadFail"), ex.Message);
            }
            finally
            {
                _loading = false;
                btnRefresh.Enabled = true;
                // 请求结束（成功/失败/取消）：收起「请稍等」遮罩
                _lblButtonsLoading.Visible = false;
            }
        }

        // ==================== 镜像源拉取 ====================

        /// <summary>镜像源在状态栏的显示名</summary>
        private static string MirrorName(Mirror m) { return m == Mirror.Github ? "GitHub" : "Cloudflare"; }

        /// <summary>镜像源成功后的状态栏来源前缀</summary>
        private static string OriginLabel(Mirror m) { return m == Mirror.Github ? "[GitHub] " : "[Cloudflare] "; }

        /// <summary>
        /// 配置的 Cloudflare 服务根地址（去尾斜杠）；未配置返回 null。
        /// </summary>
        private static string CloudflareBaseUrl()
        {
            var st = OoorSettings.Load();
            if (!st.HasServer) return null;
            return st.ServerUrl.TrimEnd('/');
        }

        /// <summary>
        /// 按指定镜像源拉取版本列表 JSON（失败返回 null，原因写 _lastError）：
        ///   - GitHub：直连，8 秒短超时；
        ///   - Cloudflare：先 GET /llama/refresh（每次都尝试，失败不致命），再 GET /llama/releases。
        /// </summary>
        private async Task<string> TryFetchMirror(Mirror m)
        {
            if (m == Mirror.Github)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.fetchingGithub"),
                               RepoOwner, Repo, GithubTimeoutSec);
                return await TryGetJsonAsync(ReleasesUrl, true, GithubTimeoutSec);
            }

            string baseUrl = CloudflareBaseUrl();
            if (baseUrl == null)
            {
                _lastError = L.T("ldf.error.noCloudflare");
                return null;
            }

            lblStatus.Text = L.T("ldf.status.fetchingCloudflare");
            //await TryCloudflareRefreshAsync(baseUrl);
            return await TryGetJsonAsync(
                baseUrl + "/api/v1/llama/releases?per_page=" + MaxVersions, false, CloudflareTimeoutSec);
        }

        /// <summary>
        /// 通知 Cloudflare 尝试从 GitHub 同步最新版本（GET，每次都尝试，成功才存，失败不致命）；
        /// 任何失败都不影响——后续 GET 仍会读到库里上一次成功保存的版本。
        /// </summary>
        private async Task<bool> TryCloudflareRefreshAsync(string baseUrl)
        {
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/api/v1/llama/refresh"))
                {
                    var st = OoorSettings.Load();
                    if (!string.IsNullOrEmpty(st.Token))
                        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", st.Token);

                    using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                    {
                        timeoutCts.CancelAfter(CloudflareTimeoutSec * 1000);
                        using (var resp = await _http.SendAsync(req, timeoutCts.Token))
                            return resp.IsSuccessStatusCode;
                    }
                }
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// 单次 GET 取 JSON 正文。
        ///   - 成功 → 返回正文；
        ///   - HTTP 非 2xx / 网络异常 / 请求超时 → 返回 null，原因写入 <see cref="_lastError"/>，
        ///     由调用方决定是否回退到另一个镜像源；
        ///   - 用户主动取消（窗口关闭）→ 仍抛 TaskCanceledException，交 LoadAsync 统一处理。
        /// </summary>
        private async Task<string> TryGetJsonAsync(string url, bool githubHeaders, int timeoutSec)
        {
            // 单次请求的超时（与窗口取消联动）：GitHub 短超时，Cloudflare 常规超时
            using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                timeoutCts.CancelAfter(timeoutSec * 1000);
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (githubHeaders)
                        {
                            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                            // GitHub 强制要求 User-Agent，否则 403
                            req.Headers.UserAgent.Add(new ProductInfoHeaderValue("ooor", "0.0001"));
                        }
                        else
                        {
                            // Cloudflare：带令牌（默认可匿名，令牌可空）
                            var st = OoorSettings.Load();
                            if (!string.IsNullOrEmpty(st.Token))
                                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", st.Token);
                        }

                        using (var resp = await _http.SendAsync(req, timeoutCts.Token))
                        {
                            if (!resp.IsSuccessStatusCode)
                            {
                                _lastError = (int)resp.StatusCode + " " + resp.ReasonPhrase;
                                return null;
                            }
                            return await resp.Content.ReadAsStringAsync();
                        }
                    }
                }
                catch (TaskCanceledException) when (!_cts.IsCancellationRequested)
                {
                    // 请求超时（非用户取消）：按可回退的失败处理
                    _lastError = string.Format(L.T("ldf.error.timeout"), timeoutSec);
                    return null;
                }
                catch (Exception ex)
                {
                    _lastError = ex.Message;
                    return null;
                }
            }
        }

        /// <summary>把已加载的 _releases 渲染到左侧 ListBox（最新在前），并默认选中第一项</summary>
        private void FillVersionList()
        {
            lstVersions.BeginUpdate();
            try
            {
                lstVersions.Items.Clear();
                foreach (var r in _releases)
                {
                    // 显示形如："b1234  (2026-04-12) [预发布]"
                    string dateStr = FormatDate(r.published_at);
                    string pre = r.prerelease ? L.T("ldf.prerelease") : "";
                    string label = string.IsNullOrEmpty(dateStr)
                        ? r.tag_name + pre
                        : r.tag_name + "  (" + dateStr + ")" + pre;
                    lstVersions.Items.Add(label);
                }

                if (lstVersions.Items.Count > 0)
                    lstVersions.SelectedIndex = 0; // 自动触发 SelectedIndexChanged → 刷新按钮可用性
            }
            finally
            {
                lstVersions.EndUpdate();
            }
        }

        // ==================== 版本选中 → 下载按钮可用性 ====================

        /// <summary>左侧当前选中的 release</summary>
        private GithubRelease SelectedRelease
        {
            get
            {
                int idx = lstVersions.SelectedIndex;
                return (idx >= 0 && idx < _releases.Count) ? _releases[idx] : null;
            }
        }

        private void lstVersions_SelectedIndexChanged(object sender, EventArgs e) => UpdateVersionButtons();

        private void SetVersionButtonsEnabled(bool on)
        {
            button1.Enabled = on;
            button2.Enabled = on;
            button3.Enabled = on;
            button4.Enabled = on;
            button5.Enabled = on;

            // 加载/禁用期间：所有匹配标签一并隐藏
            label_ok_cpu.Visible = false;
            label_ok_cudacuda.Visible = false;
            label_ok_rocm.Visible = false;
            label_ok_sycl.Visible = false;
            label_ok_vulkan.Visible = false;
        }

        /// <summary>按选中版本的资产名匹配启用/禁用各下载按钮（匹配上才可用，并在按钮右侧显示 ✔ 标签）</summary>
        private void UpdateVersionButtons()
        {
            var release = SelectedRelease;
            var assets = release?.assets;
            bool cpu = FindAsset(assets, "win-cpu", false) != null;
            bool cuda = cpu && FindAsset(assets, "win-cuda", true) != null;
            bool rocm = FindAsset(assets, "win-rocm", false) != null;
            bool sycl = FindAsset(assets, "win-sycl", false) != null;
            bool vulkan = FindAsset(assets, "win-vulkan", false) != null;

            button1.Enabled = cpu;
            button3.Enabled = cuda;
            button5.Enabled = rocm;
            button4.Enabled = sycl;
            button2.Enabled = vulkan;

            // 匹配成功的安装按钮右侧显示 ✔
            label_ok_cpu.Visible = cpu;
            label_ok_cudacuda.Visible = cuda;
            label_ok_rocm.Visible = rocm;
            label_ok_sycl.Visible = sycl;
            label_ok_vulkan.Visible = vulkan;

            if (release != null)
            {
                // 架构名为产品专有名（CPU / CUDA / ROCm / SYCL / Vulkan），不翻译
                string available = (cpu ? "CPU " : "") + (cuda ? "CPU+CUDA " : "")
                    + (rocm ? "ROCm " : "") + (sycl ? "SYCL " : "") + (vulkan ? "Vulkan" : "");
                lblStatus.Text = string.Format(L.T("ldf.status.selected"),
                    release.tag_name, available.Trim());
            }
        }

        /// <summary>
        /// 在资产列表里找 zip 包：cudart=false 匹配 llama 主程序包（排除 cudart 前缀的运行库），
        /// cudart=true 匹配 CUDA 运行库包（cudart-llama-bin-win-cuda-…）。
        /// 选优策略（llama.cpp 同时发 x64 / arm64 两份 Windows 桌面二进制）：
        ///   1) 优先选 x64 / amd64 / x86_64（Windows 桌面端默认架构）
        ///   2) 排除 arm64 / arm64ec（仅 ARM Windows 才需要；API 列表里通常排在 x64 之前，旧版
        ///      Contains("win-cpu") 会把 arm64 误中导致下到不能跑的二进制）
        ///   3) 实在没有 x64 时回退到非 arm64 的其他架构（兜底，目前不会出现）
        /// </summary>
        private static GithubAsset FindAsset(List<GithubAsset> assets, string nameContains, bool cudart)
        {
            if (assets == null) return null;
            GithubAsset x64 = null;        // 首选：x64 / amd64 / x86_64
            GithubAsset nonArm = null;     // 兜底：非 arm64 / 非 arm64ec 的其他架构
            foreach (var a in assets)
            {
                if (a == null || string.IsNullOrEmpty(a.name)) continue;
                string n = a.name.ToLowerInvariant();
                if (!n.EndsWith(".zip")) continue;               // 不支持 tar.gz
                if (n.StartsWith("cudart") != cudart) continue;
                if (!n.Contains(nameContains)) continue;
                if (n.Contains("arm64")) continue;                // 排除 arm64 / arm64ec
                if (n.Contains("x64") || n.Contains("amd64") || n.Contains("x86_64"))
                {
                    x64 = a;
                    break;                                          // 命中首选立即停
                }
                if (nonArm == null) nonArm = a;
            }
            return x64 ?? nonArm;   // 理论上 x64 一定命中（llama.cpp 当前总是同时发 x64）
        }

        // ==================== 各版本下载按钮 ====================

        private void btnCpu_Click(object sender, EventArgs e)
        {
            var release = SelectedRelease;
            var cpu = FindAsset(release?.assets, "win-cpu", false);
            if (cpu == null) return;

            string cpuDir = ExtractDirFor(cpu);

            // 先确认要下载的文件，确认后才入队
            var plan = new List<LlamaInstallConfirmForm.Item>
            {
                new LlamaInstallConfirmForm.Item(cpu.name, cpu.size, cpuDir)
            };
            if (LlamaInstallConfirmForm.Show(this, "CPU", release.tag_name, plan) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.downloadCancelled"), "CPU");
                return;
            }

            EnqueueAsset(release, cpu, cpuDir);
            lblStatus.Text = string.Format(L.T("ldf.status.cpuSubmitted"), cpuDir);
            FileDownloadForm.ShowSingle();
        }

        /// <summary>
        /// CPU+CUDA：提交两个任务（主程序 + CUDA 运行库），队列按 FIFO 串行，
        /// CUDA 运行库解压到 CPU 版本同一目录合并（llama-server 需要同目录 DLL）。
        /// </summary>
        private void btnCpuCuda_Click(object sender, EventArgs e)
        {
            var release = SelectedRelease;
            var cpu = FindAsset(release?.assets, "win-cpu", false);
            var cuda = FindAsset(release?.assets, "win-cuda", true);
            if (cpu == null || cuda == null) return;

            string cpuDir = ExtractDirFor(cpu);

            // 确认清单：CPU 主程序 + CUDA 运行库（均解压到 CPU 目录）
            var plan = new List<LlamaInstallConfirmForm.Item>
            {
                new LlamaInstallConfirmForm.Item(cpu.name, cpu.size, cpuDir),
                new LlamaInstallConfirmForm.Item(cuda.name, cuda.size, cpuDir)
            };
            if (LlamaInstallConfirmForm.Show(this, "CPU+CUDA", release.tag_name, plan) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.downloadCancelled"), "CPU+CUDA");
                return;
            }

            EnqueueAsset(release, cpu, cpuDir);
            EnqueueAsset(release, cuda, cpuDir);   // CUDA 运行库解压进 CPU 目录
            lblStatus.Text = string.Format(L.T("ldf.status.cpuCudaSubmitted"), cpuDir);
            FileDownloadForm.ShowSingle();
        }

        private void btnRocm_Click(object sender, EventArgs e)
        {
            var release = SelectedRelease;
            var a = FindAsset(release?.assets, "win-rocm", false);
            if (a == null) return;

            string dir = ExtractDirFor(a);
            var plan = new List<LlamaInstallConfirmForm.Item>
            {
                new LlamaInstallConfirmForm.Item(a.name, a.size, dir)
            };
            if (LlamaInstallConfirmForm.Show(this, "ROCm", release.tag_name, plan) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.downloadCancelled"), "ROCm");
                return;
            }

            EnqueueAsset(release, a, dir);
            lblStatus.Text = string.Format(L.T("ldf.status.taskSubmitted"), "ROCm", dir);
            FileDownloadForm.ShowSingle();
        }

        private void btnSycl_Click(object sender, EventArgs e)
        {
            var release = SelectedRelease;
            var a = FindAsset(release?.assets, "win-sycl", false);
            if (a == null) return;

            string dir = ExtractDirFor(a);
            var plan = new List<LlamaInstallConfirmForm.Item>
            {
                new LlamaInstallConfirmForm.Item(a.name, a.size, dir)
            };
            if (LlamaInstallConfirmForm.Show(this, "SYCL", release.tag_name, plan) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.downloadCancelled"), "SYCL");
                return;
            }

            EnqueueAsset(release, a, dir);
            lblStatus.Text = string.Format(L.T("ldf.status.taskSubmitted"), "SYCL", dir);
            FileDownloadForm.ShowSingle();
        }

        private void btnVulkan_Click(object sender, EventArgs e)
        {
            var release = SelectedRelease;
            var a = FindAsset(release?.assets, "win-vulkan", false);
            if (a == null) return;

            string dir = ExtractDirFor(a);
            var plan = new List<LlamaInstallConfirmForm.Item>
            {
                new LlamaInstallConfirmForm.Item(a.name, a.size, dir)
            };
            if (LlamaInstallConfirmForm.Show(this, "Vulkan", release.tag_name, plan) != DialogResult.OK)
            {
                lblStatus.Text = string.Format(L.T("ldf.status.downloadCancelled"), "Vulkan");
                return;
            }

            EnqueueAsset(release, a, dir);
            lblStatus.Text = string.Format(L.T("ldf.status.taskSubmitted"), "Vulkan", dir);
            FileDownloadForm.ShowSingle();
        }

        /// <summary>该 zip 解压后的目录：{llama-bin}\{zip 文件名去扩展名}（如 llama-b1234-bin-win-cpu-x64）</summary>
        private static string ExtractDirFor(GithubAsset a)
        {
            string stem = System.IO.Path.GetFileNameWithoutExtension(a.name ?? "");
            return System.IO.Path.Combine(LlamaRuntime.LlamaBinsDir, SanitizeDir(stem));
        }

        /// <summary>提交一个下载任务：压缩包先存 {配置根}\llama-temp，完成后解压到 extractDir</summary>
        private static void EnqueueAsset(GithubRelease release, GithubAsset a, string extractDir)
        {
            // 入队时即拍下当前选中的 GitHub 加速镜像（默认 github.com 直连）
            string proxy = DownloadManager.LastProxyUrl ?? GithubProxy.DirectMarker;
            DownloadManager.Instance.Enqueue(release?.tag_name ?? "", a.name, a.browser_download_url,
                a.size, LlamaRuntime.LlamaTempDir, extractDir, null, proxy);
        }

        // ========== 辅助方法 ==========

        private static string SanitizeDir(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static string FormatDate(string iso)
        {
            var dt = ParseDateOrMin(iso);
            if (dt == DateTime.MinValue) return "";
            return dt.ToString("yyyy-MM-dd");
        }

        private static DateTime ParseDateOrMin(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            {
                return dt;
            }
            return DateTime.MinValue;
        }
    }
}
