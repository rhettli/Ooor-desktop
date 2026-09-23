using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// "通用下载"对话框：用户手动输入 URL + 保存/解压目录，
    /// 点确定 → 调用 DownloadManager.Enqueue 入队（FIFO / 自动套代理 / 同 URL 去重）。
    ///
    /// 字段命名沿用 Designer 里 textBox1/textBox2/button1/checkBox1 等，
    /// 不重构 Designer（避免触发其他 Designer 生成的代码飘移）。
    /// </summary>
    public partial class FileDownloadBefore : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private bool _fillingName;        // 正在由程序回填文件名（抑制"用户已编辑"标记）
        private bool _nameUserEdited;     // 用户手动改过文件名 → 不再被 URL 自动覆盖
        private bool _nameHintOk = true;  // 文件名标签当前是否为"识别成功"态（语言切换时据此重译）

        public FileDownloadBefore()
        {
            InitializeComponent();

            // 默认目录：压缩包暂存到 LlamaTempDir；解压到当前 llama 版本目录
            if (string.IsNullOrWhiteSpace(txtSaveDir.Text))
                txtSaveDir.Text = LlamaRuntime.LlamaTempDir;
            if (string.IsNullOrWhiteSpace(txtExtractDir.Text))
                txtExtractDir.Text = LlamaRuntime.BaseDir;

            // 默认不自动解压 → 解压目录相关控件禁用（保留文本方便切换）
            checkBox1.Checked = false;
            UpdateExtractEnabled();

            // Enter 直接触发"确定"；Esc 由 X 按钮关闭（DialogResult.Cancel 默认）
            this.AcceptButton = button1;

            // 多线程分块下载线程数：只允许从列表选（不可手输），默认沿用全局 LastThreadCount（5）
            comboBoxMultiThread.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxMultiThread.Items.Clear();
            foreach (int n in new[] { 1, 2, 3, 5, 8, 10, 15, 20, 25, 30 })
                comboBoxMultiThread.Items.Add(n);
            int def = Math.Max(1, DownloadManager.LastThreadCount);
            int defIdx = comboBoxMultiThread.Items.IndexOf(def);
            comboBoxMultiThread.SelectedIndex = defIdx >= 0 ? defIdx : comboBoxMultiThread.Items.IndexOf(5);

            // 事件订阅
            btnBrowseSave.Click += BtnBrowseSave_Click;
            btnBrowseExtract.Click += BtnBrowseExtract_Click;
            textBoxUrl.TextChanged += TextBoxUrl_TextChanged;
            textBoxFilename.TextChanged += TextBoxFilename_TextChanged;
            checkBox1.CheckedChanged += (s, e) => UpdateExtractEnabled();
            button1.Click += BtnOk_Click;

            // 打开时把光标放到 URL 输入框
            this.Shown += (s, e) => textBoxUrl.Focus();
        }

        /// <summary>按当前语言刷新窗口静态文本（标题、标签、按钮、复选框等）</summary>
        protected override void ApplyLanguage()
        {
            Text = L.T("fdb.title");
            label1.Text = L.T("fdb.lbl.url");
            lblSaveDir.Text = L.T("fdb.lbl.saveDir");
            lblExtractDir.Text = L.T("fdb.lbl.extractDir");
            label3.Text = L.T("fdb.lbl.threads");
            checkBox1.Text = L.T("fdb.chk.autoExtract");
            btnBrowseSave.Text = L.T("fdb.btn.browse");
            btnBrowseExtract.Text = L.T("fdb.btn.browse");
            button1.Text = L.T("fdb.btn.ok");
            SetNameHint(_nameHintOk);
        }

        /// <summary>自动解压开关切换时启用/禁用解压目录相关控件（保留文本，不清空）</summary>
        private void UpdateExtractEnabled()
        {
            bool en = checkBox1.Checked;
            txtExtractDir.Enabled = en;
            btnBrowseExtract.Enabled = en;
            lblExtractDir.Enabled = en;
        }

        // ============ 目录选择（按钮弹固定目录菜单，不直接弹浏览框） ============

        /// <summary>菜单项：显示名 + 延迟解析的目录路径</summary>
        private sealed class DirItem
        {
            public readonly string Text;
            public readonly Func<string> Resolve;
            public DirItem(string text, Func<string> resolve) { Text = text; Resolve = resolve; }
        }

        /// <summary>保存目录菜单：默认缓存 / 默认模型 / 下载 / 桌面 / 用户 / 自定义</summary>
        private void BtnBrowseSave_Click(object sender, EventArgs e)
        {
            ShowDirMenu(btnBrowseSave, txtSaveDir, L.T("fdb.menu.chooseSaveDir"), new[]
            {
                new DirItem(L.T("fdb.menu.defaultCacheDir"),  () => LlamaRuntime.LlamaTempDir),
                new DirItem(L.T("fdb.menu.defaultModelsDir"), () => LlamaRuntime.ModelsDir),
                new DirItem(L.T("fdb.menu.downloadsDir"),     UserDownloadsDir),
                new DirItem(L.T("fdb.menu.desktopDir"),       () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                new DirItem(L.T("fdb.menu.userDir"),          () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            });
        }

        /// <summary>解压目录菜单：默认模型 / 下载 / 桌面 / 自定义</summary>
        private void BtnBrowseExtract_Click(object sender, EventArgs e)
        {
            ShowDirMenu(btnBrowseExtract, txtExtractDir, L.T("fdb.menu.chooseExtractDir"), new[]
            {
                new DirItem(L.T("fdb.menu.defaultModelsDir"), () => LlamaRuntime.ModelsDir),
                new DirItem(L.T("fdb.menu.downloadsDir"),     UserDownloadsDir),
                new DirItem(L.T("fdb.menu.desktopDir"),       () => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            });
        }

        /// <summary>
        /// 在按钮下方弹出目录快捷菜单；选中后把路径写入 target。
        /// 菜单项悬停显示完整路径；末项「自定义目录…」才打开浏览框。
        /// </summary>
        private void ShowDirMenu(Control anchor, TextBox target, string browseDesc, DirItem[] items)
        {
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                ShowItemToolTips = true,
            };

            foreach (var it in items)
            {
                string path = it.Resolve();
                var mi = new ToolStripMenuItem(it.Text) { ToolTipText = path };
                if (string.IsNullOrEmpty(path))
                {
                    mi.Enabled = false;      // 解析失败（如系统目录取不到）→ 置灰
                }
                else
                {
                    string captured = path;
                    mi.Click += (s, e) => SetDir(target, captured);
                }
                menu.Items.Add(mi);
            }

            menu.Items.Add(new ToolStripSeparator());
            var custom = new ToolStripMenuItem(L.T("fdb.menu.customDir"));
            custom.Click += (s, e) => BrowseDir(target, browseDesc);
            menu.Items.Add(custom);

            // 关闭后再释放，避免下拉项仍在处理消息时被 Dispose
            menu.Closed += (s, e) => menu.BeginInvoke(new Action(() => menu.Dispose()));
            menu.Show(anchor, new System.Drawing.Point(0, anchor.Height));
        }

        /// <summary>写入目录文本（不改磁盘，目录不存在由「确定」时统一询问创建）</summary>
        private static void SetDir(TextBox target, string dir)
        {
            target.Text = dir;
            target.SelectionStart = target.TextLength;
            target.ScrollToCaret();
        }

        /// <summary>「自定义目录…」：打开浏览框</summary>
        private void BrowseDir(TextBox target, string desc)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = desc;
                dlg.ShowNewFolderButton = true;
                if (Directory.Exists(target.Text))
                    dlg.SelectedPath = target.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    SetDir(target, dlg.SelectedPath);
            }
        }

        /// <summary>用户「下载」文件夹；注册表取不到时回退 %USERPROFILE%\Downloads</summary>
        private static string UserDownloadsDir()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"))
                {
                    if (k != null
                        && k.GetValue("{374DE290-123F-4565-9164-39C4925E467B}") is string s
                        && !string.IsNullOrWhiteSpace(s))
                        return Environment.ExpandEnvironmentVariables(s);
                }
            }
            catch { }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        // ============ URL 扩展名 → 预选保存目录 ============

        /// <summary>
        /// URL 变化时按扩展名预选保存目录：.gguf → 默认模型目录；.zip → 默认缓存目录。
        /// 仅当保存目录为空、或仍是这两个预设值时才改写，避免覆盖用户已选的其它目录。
        /// </summary>
        private void TextBoxUrl_TextChanged(object sender, EventArgs e)
        {
            string url = (textBoxUrl.Text ?? "").Trim();

            // 1) 文件名：识别成功自动填充；识别失败清空并提示手动填写
            ApplyAutoFileName(url);

            if (url.Length == 0) return;

            // 2) 保存目录：.gguf → 默认模型目录；.zip → 默认缓存目录
            string want;
            switch (UrlExtension(url))
            {
                case ".gguf": want = LlamaRuntime.ModelsDir; break;
                case ".zip": want = LlamaRuntime.LlamaTempDir; break;
                default: return;
            }

            string cur = (txtSaveDir.Text ?? "").Trim();
            if (cur.Length == 0 || Eq(cur, LlamaRuntime.LlamaTempDir) || Eq(cur, LlamaRuntime.ModelsDir))
                txtSaveDir.Text = want;
        }

        // ============ 文件名自动填充 ============

        /// <summary>纯路径段里不代表文件名的占位词（/download、/resolve/main 之类）</summary>
        private static readonly string[] NonFileNames =
        {
            "download", "downloads", "file", "files", "blob", "raw",
            "resolve", "view", "get", "main", "latest", "release",
        };

        /// <summary>
        /// 识别成功 → 回填文件名（用户改过则不动）；识别失败 → 清空并提示手动填写。
        /// </summary>
        private void ApplyAutoFileName(string url)
        {
            if (url.Length == 0)
            {
                SetNameHint(true);
                if (!_nameUserEdited) SetFileName("");
                return;
            }

            string name;
            if (TryDeriveFileName(url, out name))
            {
                SetNameHint(true);
                if (!_nameUserEdited) SetFileName(name);
            }
            else
            {
                SetNameHint(false);
                if (!_nameUserEdited) SetFileName("");
            }
        }

        /// <summary>回填文件名（不触发"用户已编辑"标记）</summary>
        private void SetFileName(string name)
        {
            if (textBoxFilename.Text == name) return;
            _fillingName = true;
            try { textBoxFilename.Text = name; }
            finally { _fillingName = false; }
        }

        /// <summary>用户手动编辑文件名后，不再被 URL 自动覆盖</summary>
        private void TextBoxFilename_TextChanged(object sender, EventArgs e)
        {
            if (_fillingName) return;
            _nameUserEdited = (textBoxFilename.Text ?? "").Trim().Length > 0;
        }

        /// <summary>文件名标签提示：识别失败时标红，明确要求手动填写</summary>
        private void SetNameHint(bool ok)
        {
            _nameHintOk = ok;
            label2.Text = ok ? L.T("fdb.lbl.filename") : L.T("fdb.lbl.filenameFail");
            label2.ForeColor = ok
                ? System.Drawing.SystemColors.ControlText
                : System.Drawing.Color.FromArgb(0xC0, 0x39, 0x2B);
        }

        /// <summary>
        /// 从 URL 推断文件名：路径末段 → query 的 filename/file/name/download → 失败。
        /// 末段为空、或只是 download 这类占位词且无扩展名时视为失败。
        /// </summary>
        private static bool TryDeriveFileName(string url, out string name)
        {
            name = "";
            if (string.IsNullOrWhiteSpace(url)) return false;

            string path = url;
            string query = "";
            try
            {
                var u = new Uri(url, UriKind.Absolute);
                path = u.AbsolutePath ?? "";
                query = u.Query ?? "";
            }
            catch
            {
                int cut = path.IndexOfAny(new[] { '?', '#' });
                if (cut >= 0)
                {
                    query = path.Substring(cut);
                    path = path.Substring(0, cut);
                }
            }

            // 1) 路径末段
            int slash = path.LastIndexOf('/');
            string seg = Decode(slash >= 0 ? path.Substring(slash + 1) : path);
            if (IsUsableName(seg)) { name = seg; return true; }

            // 2) query 参数（如 HuggingFace 的 ?filename=model.gguf）
            foreach (string key in new[] { "filename", "file", "name", "download" })
            {
                string v = Decode(QueryValue(query, key));
                int s2 = v.LastIndexOf('/');                 // 值里可能还带路径
                if (s2 >= 0) v = v.Substring(s2 + 1);
                if (IsUsableName(v)) { name = v; return true; }
            }
            return false;
        }

        /// <summary>末段可用性：非空、非 . / ..；无扩展名时排除 download 之类占位词</summary>
        private static bool IsUsableName(string seg)
        {
            if (string.IsNullOrWhiteSpace(seg)) return false;
            seg = seg.Trim();
            if (seg == "." || seg == "..") return false;
            if (Path.GetExtension(seg).Length > 0) return true;
            foreach (string bad in NonFileNames)
                if (string.Equals(seg, bad, StringComparison.OrdinalIgnoreCase)) return false;
            return true;    // 形如 /abc123 的不透明 id：先填上，用户可自行改
        }

        /// <summary>百分号解码（中文/空格文件名）；解码失败保留原文</summary>
        private static string Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOf('%') < 0) return s;
            try { return Uri.UnescapeDataString(s).Trim(); }
            catch { return s; }
        }

        /// <summary>取 query 中指定参数的值（忽略大小写）；无则返回 ""</summary>
        private static string QueryValue(string query, string key)
        {
            if (string.IsNullOrEmpty(query)) return "";
            foreach (string pair in query.TrimStart('?', '#').Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                if (string.Equals(pair.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase))
                    return pair.Substring(eq + 1);
            }
            return "";
        }

        /// <summary>取 URL 路径部分的扩展名（小写，含点）；无扩展名返回 ""</summary>
        private static string UrlExtension(string url)
        {
            string path;
            try { path = new Uri(url, UriKind.Absolute).AbsolutePath; }
            catch
            {
                path = url;
                int cut = path.IndexOfAny(new[] { '?', '#' });
                if (cut >= 0) path = path.Substring(0, cut);
            }
            return Path.GetExtension(path).ToLowerInvariant();
        }

        private static bool Eq(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string url = (textBoxUrl.Text ?? "").Trim();
            string fileName = (textBoxFilename.Text ?? "").Trim();
            string saveDir = (txtSaveDir.Text ?? "").Trim();
            string extractDir = (txtExtractDir.Text ?? "").Trim();

            // 1. URL 校验
            if (string.IsNullOrEmpty(url))
            {
                Warn(L.T("fdb.warn.urlEmpty"));
                textBoxUrl.Focus();
                return;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)
                || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            {
                Warn(string.Format(L.T("fdb.warn.urlInvalid"), url));
                textBoxUrl.Focus();
                return;
            }

            // 2. 文件名：仍为空则再尝试从 URL 推断；都失败 → 要求手填（不再静默用 "download"，
            //    否则丢了扩展名，自动解压/模型类型识别都会跟着错）
            if (string.IsNullOrEmpty(fileName))
            {
                string guess;
                if (TryDeriveFileName(url, out guess))
                {
                    fileName = guess;
                    SetFileName(fileName);      // 走回填通道，不误标"用户已编辑"
                }
                else
                {
                    Warn(L.T("fdb.warn.fileNameGuess"));
                    SetNameHint(false);
                    textBoxFilename.Focus();
                    return;
                }
            }
            char[] invalid = Path.GetInvalidFileNameChars();
            if (fileName.Any(c => invalid.Contains(c)))
            {
                Warn(string.Format(L.T("fdb.warn.invalidChars"), fileName));
                textBoxFilename.Focus();
                return;
            }

            // 3. 保存目录校验（不存在 → 询问是否创建）
            if (string.IsNullOrEmpty(saveDir))
            {
                Warn(L.T("fdb.warn.saveDirEmpty"));
                txtSaveDir.Focus();
                return;
            }
            if (!Directory.Exists(saveDir)
                && !ConfirmCreateDir(saveDir, L.T("fdb.dirLabel.save"))) return;

            // 4. 解压目录校验（仅勾选自动解压时）
            bool autoExtract = checkBox1.Checked;
            if (autoExtract)
            {
                if (string.IsNullOrEmpty(extractDir))
                {
                    Warn(L.T("fdb.warn.extractDirEmpty"));
                    txtExtractDir.Focus();
                    return;
                }
                if (!Directory.Exists(extractDir)
                    && !ConfirmCreateDir(extractDir, L.T("fdb.dirLabel.extract"))) return;
            }

            // 5. 入队
            //   - expectedSize=0 → 下载器 HEAD 探测真实大小
            //   - proxyUrl=null  → 走 LastProxyUrl（GitHub 任务自动套代理）
            //   - threads        → 分块下载线程数（服务器不支持 Range / 文件 < 8MB 自动回退单线程）
            //   - 同 URL + 同 SaveDir 的未完成任务会被 Enqueue 内部去重
            int threads = 1;
            if (comboBoxMultiThread.SelectedItem != null
                && !int.TryParse(comboBoxMultiThread.SelectedItem.ToString(), out threads))
                threads = 1;
            if (threads < 1) threads = 1;
            DownloadManager.LastThreadCount = threads;   // 记住本次选择，作为下次默认值
            try
            {
                DownloadManager.Instance.Enqueue(
                    tag: "",
                    fileName: fileName,
                    url: url,
                    expectedSize: 0,
                    saveDir: saveDir,
                    extractDir: autoExtract ? extractDir : null,
                    bearerToken: null,
                    proxyUrl: null,
                    threads: threads);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, string.Format(L.T("fdb.warn.enqueueFail"), ex.Message), L.T("fdb.msg.titleError"),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 6. 关闭对话框；FileDownloadForm 已订阅 TaskAdded，会自动追加到 listView1
            DialogResult = DialogResult.OK;
            Close();
        }

        // ============ 工具 ============

        private void Warn(string msg)
        {
            MessageBox.Show(this, msg, L.T("fdb.msg.titleWarn"),
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>目录不存在时弹"是否创建"对话框；返回 true = 已存在或已创建</summary>
        private bool ConfirmCreateDir(string dir, string label)
        {
            if (MessageBox.Show(this,
                    string.Format(L.T("fdb.confirm.createDir"), label, dir),
                    L.T("fdb.msg.titleWarn"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return false;
            try
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            catch (Exception ex)
            {
                Warn(string.Format(L.T("fdb.warn.createDirFail"), ex.Message));
                return false;
            }
        }
    }
}