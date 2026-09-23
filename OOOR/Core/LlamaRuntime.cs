using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ooor.Core
{
    /// <summary>
    /// llama.cpp 运行环境（纯文件系统探测，无 UI 依赖）。
    ///
    /// 目录约定（与 DockerExt/TasksMaster 等插件的 ../config 约定一致）：
    ///   配置根目录：{主程序启动目录}/../config/ooor
    ///   llama 版本目录：{配置根}/llama-bin/{版本子目录}    ← 用户放置多个 llama.cpp 发行版，可下拉选择
    ///   模型目录：{配置根}/models                          ← 与 llama 版本无关，统一管理
    ///
    /// 当前选中的 llama 版本保存在 SelectedVersion 中，所有依赖版本目录的属性（BaseDir/ServerExePath 等）
    /// 都基于它计算。修改版本后调用方需自行重检 ServerPresent 与模型列表。
    /// </summary>
    internal static class LlamaRuntime
    {
        public const string ConfigDirName = "";
        public const string LlamaBinsDirName = "llama-bin";
        public const string LlamaTempDirName = "llama-temp";
        public const string ServerExeName = "llama-server.exe";
        public const string ModelsDirName = "models";
        public const string RefModelsFileName = "ref_models.conf";
        public const string ModelMetaFileName = "model_meta.conf";

        /// <summary>插件配置根目录（bin\config）</summary>
        public static string ConfigRoot
        {
            get
            {
                // proxy.exe 启动插件时工作目录已对齐主程序目录（cfg.BaseDir）
                return Path.GetFullPath(Path.Combine(
                    Application.StartupPath, "..", "config"));
            }
        }

        /// <summary>llama 版本容器目录（{ConfigRoot}\llama-bin），子目录即版本</summary>
        public static string LlamaBinsDir => Path.Combine(ConfigRoot, LlamaBinsDirName);

        /// <summary>下载临时目录（{ConfigRoot}\llama-temp），下载的压缩包暂存此处</summary>
        public static string LlamaTempDir => Path.Combine(ConfigRoot, LlamaTempDirName);

        /// <summary>模型目录（{ConfigRoot}\models，独立于 llama 版本）</summary>
        public static string ModelsDir => Path.Combine(ConfigRoot, ModelsDirName);

        // ==================== WebView2 页面资源目录 ====================

        /// <summary>页面资源目录名（WebView2 宿主窗口加载的 html/css/js）</summary>
        public const string HtmlDirName = "html";

        /// <summary>
        /// html 资源目录（跟随编译输出，NSIS 整目录打包）。
        /// 兼容三种运行位置：独立运行（exe 同级）、插件目录（DLL\ooor\html）、主程序启动目录。
        /// 全部探测不到时返回 exe 同级路径，由调用方给出明确报错。
        /// </summary>
        public static string HtmlDir
        {
            get
            {
                var candidates = new List<string>();
                try
                {
                    string asmDir = Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().Location);
                    if (!string.IsNullOrEmpty(asmDir))
                        candidates.Add(Path.Combine(asmDir, HtmlDirName));
                }
                catch { }
                try { candidates.Add(Path.Combine(Application.StartupPath, HtmlDirName)); } catch { }
                try { candidates.Add(Path.Combine(Application.StartupPath, "DLL", HtmlDirName)); } catch { }

                foreach (string c in candidates)
                {
                    try { if (Directory.Exists(c)) return c; } catch { }
                }
                return candidates.Count > 0 ? candidates[0] : HtmlDirName;
            }
        }

        /// <summary>外部模型引用配置（{ConfigRoot}\ref_models.conf），每行一条被引用的目录/模型文件路径</summary>
        public static string RefModelsConfPath => Path.Combine(ConfigRoot, RefModelsFileName);

        /// <summary>模型元数据配置（{ConfigRoot}\model_meta.conf）：每行一条「路径|备注」，软删除行以「deleted|」开头</summary>
        public static string ModelMetaPath => Path.Combine(ConfigRoot, ModelMetaFileName);

        /// <summary>模型备注目录（{ConfigRoot}\remark）：每个有备注的模型一个「模型名称_字节数.remark」纯文本文件</summary>
        public static string RemarkDir => Path.Combine(ConfigRoot, "remark");

        /// <summary>当前选中的 llama 版本名（llama-bin 下的子目录名），空表示未选中</summary>
        public static string SelectedVersion { get; private set; } = "";

        /// <summary>当前选中的 llama 版本目录（{LlamaBinsDir}\{SelectedVersion}），未选中时返回 LlamaBinsDir 本身</summary>
        public static string BaseDir
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedVersion)) return LlamaBinsDir;
                return Path.Combine(LlamaBinsDir, SelectedVersion);
            }
        }

        /// <summary>当前选中版本下的 llama-server.exe 完整路径</summary>
        public static string ServerExePath => Path.Combine(BaseDir, ServerExeName);

        /// <summary>当前选中版本的 llama-server.exe 是否已部署</summary>
        public static bool ServerPresent => File.Exists(ServerExePath);

        // ==================== 命令行交互程序（llama-cli） ====================

        /// <summary>交互式命令行程序名（新版 llama.cpp 发行包）</summary>
        public const string CliExeName = "llama-cli.exe";

        /// <summary>交互式命令行程序名（旧版 llama.cpp 发行包，兼容用）</summary>
        public const string LegacyCliExeName = "main.exe";

        /// <summary>
        /// 当前选中版本下可用于命令行交互的程序完整路径；找不到返回 null。
        /// 新版发行包为 llama-cli.exe；旧版发行包（b2xxx 及更早）叫 main.exe。
        /// </summary>
        public static string FindCliExe()
        {
            string dir = BaseDir;
            if (!Directory.Exists(dir)) return null;

            string cli = Path.Combine(dir, CliExeName);
            if (File.Exists(cli)) return cli;

            string legacy = Path.Combine(dir, LegacyCliExeName);
            if (File.Exists(legacy)) return legacy;

            return null;
        }

        /// <summary>当前选中版本是否带命令行交互程序（llama-cli.exe / main.exe）</summary>
        public static bool CliPresent => FindCliExe() != null;

        /// <summary>
        /// 切换选中的 llama 版本（传入 llama-bin 下的子目录名）。
        /// 切换后调用方应重检 ServerPresent / 重新加载模型列表。
        /// 传 null/空 表示清除当前选择。
        /// </summary>
        public static void SetSelectedVersion(string versionName)
        {
            SelectedVersion = string.IsNullOrEmpty(versionName) ? "" : versionName.Trim();
        }

        // ==================== 版本禁用（持久化：disabled_versions.conf） ====================

        /// <summary>被禁用的 llama 版本名配置文件名（{ConfigRoot}\disabled_versions.conf，每行一个版本名）</summary>
        public const string DisabledVersionsFileName = "disabled_versions.conf";

        /// <summary>被禁用的 llama 版本名配置文件完整路径</summary>
        public static string DisabledVersionsPath => Path.Combine(ConfigRoot, DisabledVersionsFileName);

        private static bool _disabledLoaded;

        /// <summary>被禁用的 llama 版本名集合（按版本名，忽略大小写）</summary>
        private static readonly HashSet<string> _disabledVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 当前被禁用的 llama 版本名集合的快照（按读取顺序，懒加载）。
        /// 仅用于 UI 展示与拦截，不会改变主流程。
        /// </summary>
        public static IReadOnlyCollection<string> DisabledVersionNames
        {
            get
            {
                EnsureDisabledLoaded();
                return _disabledVersions.ToArray();
            }
        }

        /// <summary>指定 llama 版本名是否被禁用（按 llama-bin 下的子目录名，忽略大小写；懒加载配置）</summary>
        public static bool IsVersionDisabled(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            EnsureDisabledLoaded();
            return _disabledVersions.Contains(name.Trim());
        }

        /// <summary>
        /// 设置/取消 llama 版本的禁用状态并立即落盘到 disabled_versions.conf。
        /// 禁用的是当前选中版本时同步清空选中状态，避免「禁用又使用中」的矛盾状态。
        /// </summary>
        public static void SetVersionDisabled(string name, bool disabled)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            EnsureDisabledLoaded();
            name = name.Trim();

            if (disabled) _disabledVersions.Add(name);
            else _disabledVersions.Remove(name);

            SaveDisabledVersions();

            if (disabled && string.Equals(SelectedVersion, name, StringComparison.OrdinalIgnoreCase))
                SetSelectedVersion(null);
        }

        /// <summary>从 disabled_versions.conf 重新加载到内存。可在外部修改了文件后显式调用刷新。</summary>
        public static void LoadDisabledVersions()
        {
            _disabledVersions.Clear();
            _disabledLoaded = true;
            string p = DisabledVersionsPath;
            if (!File.Exists(p)) return;
            try
            {
                foreach (string raw in File.ReadAllLines(p))
                {
                    string t = (raw ?? "").Trim();
                    if (t.Length == 0 || t[0] == '#') continue;
                    _disabledVersions.Add(t);
                }
            }
            catch
            {
                // 读取失败按"无禁用"处理，不影响主流程
            }
        }

        private static void EnsureDisabledLoaded()
        {
            if (!_disabledLoaded) LoadDisabledVersions();
        }

        private static void SaveDisabledVersions()
        {
            try
            {
                Directory.CreateDirectory(ConfigRoot);
                var lines = _disabledVersions
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                File.WriteAllLines(DisabledVersionsPath, lines, new System.Text.UTF8Encoding(false));
            }
            catch
            {
                // 保存失败忽略，下一次仍按内存里的状态生效
            }
        }

        /// <summary>
        /// 扫描 llama-bin 下的子目录作为可选版本列表。
        /// 只返回目录内含 llama-server.exe 的版本（不完整的版本目录直接过滤掉，不进下拉框）。
        /// </summary>
        public static List<VersionInfo> ScanLlamaVersions()
        {
            var result = new List<VersionInfo>();

            string root = LlamaBinsDir;
            if (!Directory.Exists(root)) return result;

            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(root);
            }
            catch
            {
                return result;
            }

            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name)) continue;
                if (!File.Exists(Path.Combine(dir, ServerExeName))) continue;   // 无 llama-server.exe → 过滤

                result.Add(new VersionInfo
                {
                    Name = name,
                    FullPath = dir,
                    HasServerEx = true
                });
            }

            return result.OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// 扫描全部可选的 .gguf 模型（排除 mmproj-* 多模态投影文件，与 Start.bat 的扫描口径一致）：
        ///   1. 先扫描内置 models 目录（递归），显示名为相对 models 的路径（内置模型优先排在前面）；
        ///   2. 再扫描 ref_models.conf 中引用的外部目录/文件，显示名为完整路径。
        /// 同一文件被多处覆盖时按完整路径去重（忽略大小写）。
        /// includeSoftDeleted=true 时（模型管理窗口用）连软删除的模型一并返回；默认排除。
        /// 备注为空时模型不产生 .remark 文件；修改备注后在 remark/ 目录创建「模型名.remark」。
        /// </summary>
        public static List<ModelInfo> ScanModels(bool includeSoftDeleted = false)
        {
            var all = new List<ModelInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var meta = GetModelMeta();
            MigrateLegacyNotesIfNeeded(meta);   // 旧版存于 model_meta.conf 的备注一次性迁移到 remark/*.remark

            // 1. 内置 models 目录
            string root = ModelsDir;
            if (Directory.Exists(root))
            {
                foreach (string full in SafeEnumerate(root, "*.gguf", SearchOption.AllDirectories))
                {
                    if (IsMmprojFile(full)) continue;
                    if (!seen.Add(NormalizeFullPath(full))) continue;
                    all.Add(BuildModelInfo(full, root, false, meta));
                }
            }

            // 2. ref_models.conf 引用的外部目录/文件
            foreach (string reference in GetModelReferences())
            {
                foreach (string full in EnumerateReferenceFiles(reference))
                {
                    if (IsMmprojFile(full)) continue;
                    if (!seen.Add(NormalizeFullPath(full))) continue;
                    all.Add(BuildModelInfo(full, root, true, meta));
                }
            }

            // 备注文件名：「模型名称_字节数.remark」（尺寸天然区分不同目录下的同名模型）
            foreach (var m in all)
            {
                m.RemarkPath = Path.Combine(RemarkDir, MakeRemarkFileName(m.FullPath, m.SizeBytes));
                m.Note = TryReadRemark(m.RemarkPath);
            }

            // 内置模型优先，正常在前、软删除在后，其次按显示路径排序
            return all
                .Where(m => includeSoftDeleted || !m.SoftDeleted)
                .OrderBy(m => m.SoftDeleted)
                .ThenBy(m => m.IsExternal)
                .ThenBy(m => m.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>构造 ModelInfo：相对路径 + 从 model_meta.conf 读取软删除状态（备注在扫描尾部统一读 .remark 文件）</summary>
        private static ModelInfo BuildModelInfo(string full, string modelsRoot, bool isExternal,
            Dictionary<string, ModelMetaEntry> meta)
        {
            ModelMetaEntry entry;
            meta.TryGetValue(NormalizeFullPath(full), out entry);

            return new ModelInfo
            {
                FullPath = full,
                RelativePath = isExternal
                    ? full
                    : full.Substring(modelsRoot.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                SizeBytes = TryGetSize(full),
                IsExternal = isExternal,
                SoftDeleted = entry != null && entry.SoftDeleted
            };
        }

        /// <summary>
        /// 读取 ref_models.conf 中配置的外部模型引用（每行一条：目录或单个 .gguf 文件）。
        /// 忽略空行与以 '#' 开头的注释行；返回规范化后的绝对路径列表（去重、保序）。
        /// </summary>
        public static List<string> GetModelReferences()
        {
            var result = new List<string>();
            string conf = RefModelsConfPath;
            if (!File.Exists(conf)) return result;

            string[] lines;
            try { lines = File.ReadAllLines(conf); }
            catch { return result; }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                string full;
                try { full = Path.GetFullPath(line); }
                catch { continue; }   // 非法路径行直接跳过，不影响其它引用

                if (seen.Add(full)) result.Add(full);
            }
            return result;
        }

        /// <summary>
        /// 把「导入模型」选中的外部模型位置写入 ref_models.conf 作为引用。
        /// 参数可传文件夹路径（直接引用该文件夹），也可传单个模型文件路径（取其所在目录）；
        /// 位于 models 目录内的位置自动忽略（内置目录本就会被扫描）。返回写入后的完整引用列表。
        /// </summary>
        public static List<string> AddModelReferences(IEnumerable<string> modelPaths)
        {
            var refs = GetModelReferences();
            var seen = new HashSet<string>(refs, StringComparer.OrdinalIgnoreCase);

            foreach (string path in modelPaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                string dir;
                try
                {
                    string full = Path.GetFullPath(path.Trim());
                    // 传的是文件则引用其所在目录；传的是文件夹则直接引用该文件夹
                    dir = File.Exists(full) ? Path.GetDirectoryName(full) : full;
                }
                catch { continue; }
                if (string.IsNullOrEmpty(dir)) continue;

                // 已在 models 目录内：扫描内置目录时本就会收录，无需引用
                if (IsUnderDir(dir, ModelsDir)) continue;

                if (seen.Add(dir)) refs.Add(dir);
            }

            SaveModelReferences(refs);
            return refs;
        }

        /// <summary>写回引用列表（去重、按路径排序；配置目录不存在时自动创建）</summary>
        public static void SaveModelReferences(IEnumerable<string> references)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string r in references ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                string full;
                try { full = Path.GetFullPath(r.Trim()); }
                catch { continue; }
                if (seen.Add(full)) list.Add(full);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);

            Directory.CreateDirectory(ConfigRoot);
            File.WriteAllLines(RefModelsConfPath, list, new System.Text.UTF8Encoding(false));
        }

        // ==================== 模型元数据（备注 / 软删除） ====================

        /// <summary>读取 model_meta.conf：完整路径 → 备注 + 软删除标记（忽略大小写）</summary>
        public static Dictionary<string, ModelMetaEntry> GetModelMeta()
        {
            var dict = new Dictionary<string, ModelMetaEntry>(StringComparer.OrdinalIgnoreCase);
            string path = ModelMetaPath;
            if (!File.Exists(path)) return dict;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return dict; }

            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                bool deleted = false;
                if (line.StartsWith("deleted|", StringComparison.OrdinalIgnoreCase))
                {
                    deleted = true;
                    line = line.Substring("deleted|".Length);
                }

                int sep = line.IndexOf('|');
                string full = sep < 0 ? line : line.Substring(0, sep);
                string note = sep < 0 ? "" : line.Substring(sep + 1);
                if (full.Length == 0) continue;

                try { full = Path.GetFullPath(full.Trim()); }
                catch { continue; }

                // 后写的行覆盖先写的
                dict[full] = new ModelMetaEntry { Note = note, SoftDeleted = deleted };
            }
            return dict;
        }

        /// <summary>写回 model_meta.conf（保序；备注里的换行替换为空格，避免破坏单行格式）</summary>
        public static void SaveModelMeta(Dictionary<string, ModelMetaEntry> meta)
        {
            var lines = new List<string>();
            foreach (var kv in meta ?? new Dictionary<string, ModelMetaEntry>())
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                string note = (kv.Value.Note ?? "").Replace("\r", " ").Replace("\n", " ").Replace("|", "／").Trim();
                lines.Add((kv.Value.SoftDeleted ? "deleted|" : "") + kv.Key + "|" + note);
            }

            Directory.CreateDirectory(ConfigRoot);
            File.WriteAllLines(ModelMetaPath, lines, new System.Text.UTF8Encoding(false));
        }

        /// <summary>按完整路径取一条元数据（没有则返回 null）</summary>
        public static ModelMetaEntry GetModelMetaEntry(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            ModelMetaEntry entry;
            GetModelMeta().TryGetValue(NormalizeFullPath(fullPath), out entry);
            return entry;
        }

        // ==================== 模型备注文件（remark/*.remark） ====================

        /// <summary>生成备注文件名：模型名称_字节数.remark（尺寸天然区分不同目录下的同名模型）</summary>
        public static string MakeRemarkFileName(string modelFullPath, long sizeBytes)
        {
            string stem = SafeRemarkStem(modelFullPath);
            return stem + "_" + (sizeBytes > 0 ? sizeBytes : 0).ToString() + ".remark";
        }

        /// <summary>取模型文件名（去扩展名），并把文件名非法字符替换为下划线</summary>
        private static string SafeRemarkStem(string modelFullPath)
        {
            string stem = Path.GetFileNameWithoutExtension(modelFullPath ?? "") ?? "";
            if (stem.Length == 0) stem = "model";
            foreach (char c in Path.GetInvalidFileNameChars()) stem = stem.Replace(c, '_');
            return stem;
        }

        /// <summary>读取备注文件（多行纯文本）；文件不存在时返回空字符串</summary>
        public static string TryReadRemark(string remarkPath)
        {
            if (string.IsNullOrEmpty(remarkPath) || !File.Exists(remarkPath)) return "";
            try { return File.ReadAllText(remarkPath).TrimEnd('\r', '\n'); }
            catch { return ""; }
        }

        /// <summary>
        /// 保存备注到 remark/*.remark（多行纯文本）。
        /// 备注非空时创建/覆盖对应文件；备注为空时删除文件（保持「默认无备注文件」）。
        /// </summary>
        public static void SaveRemark(string remarkPath, string note)
        {
            if (string.IsNullOrEmpty(remarkPath)) throw new ArgumentException("备注文件路径为空", "remarkPath");

            string text = (note ?? "").Trim();
            if (text.Length == 0)
            {
                if (File.Exists(remarkPath)) File.Delete(remarkPath);
                return;
            }

            Directory.CreateDirectory(RemarkDir);
            File.WriteAllText(remarkPath, text, new System.Text.UTF8Encoding(false));
        }

        /// <summary>删除模型的备注文件（文件不存在则忽略）</summary>
        public static void DeleteRemark(string remarkPath)
        {
            try { if (!string.IsNullOrEmpty(remarkPath) && File.Exists(remarkPath)) File.Delete(remarkPath); }
            catch { /* 删除备注失败不阻断主流程 */ }
        }

        /// <summary>
        /// 一次性迁移：旧版存于 model_meta.conf 的「路径|备注」迁移为 remark/*.remark 文件，
        /// 迁移成功后清空 meta 中的备注（软删除标记保留在 model_meta.conf）。
        /// </summary>
        private static void MigrateLegacyNotesIfNeeded(Dictionary<string, ModelMetaEntry> meta)
        {
            if (_legacyNotesMigrated) return;
            _legacyNotesMigrated = true;

            bool dirty = false;
            foreach (var kv in meta)
            {
                if (string.IsNullOrEmpty(kv.Value.Note)) continue;

                try
                {
                    string remarkPath = Path.Combine(RemarkDir,
                        MakeRemarkFileName(kv.Key, TryGetSize(kv.Key)));
                    if (!File.Exists(remarkPath))
                        SaveRemark(remarkPath, kv.Value.Note);
                }
                catch { /* 迁移单条失败继续下一条 */ }

                kv.Value.Note = "";
                dirty = true;
            }

            if (dirty) SaveModelMeta(meta);
        }

        private static bool _legacyNotesMigrated;

        /// <summary>设置/取消软删除（取消且备注为空时自动移除条目）</summary>
        public static void SetModelSoftDeleted(string fullPath, bool deleted)
        {
            UpdateModelMeta(fullPath, entry => entry.SoftDeleted = deleted);
        }

        /// <summary>彻底移除某模型的元数据条目（如文件被删除后清理）</summary>
        public static void RemoveModelMeta(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            string key = NormalizeFullPath(fullPath);

            var meta = GetModelMeta();
            if (meta.Remove(key)) SaveModelMeta(meta);
        }

        /// <summary>读取 → 改一条 → 落盘（空备注 + 未软删除的条目不保留）</summary>
        private static void UpdateModelMeta(string fullPath, Action<ModelMetaEntry> update)
        {
            if (string.IsNullOrEmpty(fullPath)) return;
            string key = NormalizeFullPath(fullPath);

            var meta = GetModelMeta();
            ModelMetaEntry entry;
            if (!meta.TryGetValue(key, out entry))
            {
                entry = new ModelMetaEntry();
                meta[key] = entry;
            }

            update(entry);

            if (!entry.SoftDeleted && string.IsNullOrEmpty(entry.Note)) meta.Remove(key);
            SaveModelMeta(meta);
        }

        /// <summary>把模型文件删除到回收站（安全删除，可从回收站还原）</summary>
        public static void DeleteModelFileToRecycleBin(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                throw new FileNotFoundException("模型文件不存在", fullPath);

            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(fullPath,
                Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
        }

        /// <summary>枚举一条引用下的全部 .gguf：引用为目录则递归枚举，引用为文件且存在则只返回该文件</summary>
        private static List<string> EnumerateReferenceFiles(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return new List<string>();

            try
            {
                if (Directory.Exists(reference))
                    return SafeEnumerate(reference, "*.gguf", SearchOption.AllDirectories);

                if (File.Exists(reference) &&
                    reference.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    return new List<string> { reference };
            }
            catch
            {
                // 引用目录不可访问（离线磁盘/无权限）：按“无模型”处理
            }

            return new List<string>();
        }

        /// <summary>mmproj-* 多模态投影文件走自动检测，不作为可选主模型</summary>
        private static bool IsMmprojFile(string path)
        {
            string name = Path.GetFileName(path);
            return !string.IsNullOrEmpty(name) &&
                   name.StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>规范化完整路径（去掉结尾分隔符），用于跨来源去重</summary>
        private static string NormalizeFullPath(string path)
        {
            try { return TrimSep(Path.GetFullPath(path)); }
            catch { return path ?? ""; }
        }

        /// <summary>path 是否位于 dir 之下（含与 dir 相等）</summary>
        private static bool IsUnderDir(string path, string dir)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(dir)) return false;
            try
            {
                string p = TrimSep(Path.GetFullPath(path));
                string d = TrimSep(Path.GetFullPath(dir));
                return p.Equals(d, StringComparison.OrdinalIgnoreCase)
                    || p.StartsWith(d + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// 查找模型对应的多模态投影文件（文件名以 "mmproj-" 开头的 .gguf），判定“对应”规则：
        /// 1. 模型同级目录只有一个 mmproj → 直接使用（同一目录即同一模型的常见打包布局）；
        /// 2. 同级目录有多个 → 必须严格同名：mmproj- 后面的字符串与模型名完全一致
        ///    （忽略扩展名、大小写与 -/_/. 等分隔符差异，如 Bonsai-27B-Q1_0.gguf ↔ mmproj-Bonsai-27B-Q1_0.gguf）；
        /// 3. 同级目录没有命中时全局搜索（models 目录 + ref_models.conf 引用的外部目录），同样要求严格同名，
        ///    绝不“取第一个”；匹配不到返回 null（调用方据此不勾选、按纯文本模式启动）。
        /// </summary>
        public static string FindMmproj(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath)) return null;

            string modelDir = Path.GetDirectoryName(modelPath);

            // 1. 模型同级目录
            if (!string.IsNullOrEmpty(modelDir) && Directory.Exists(modelDir))
            {
                var local = SafeEnumerate(modelDir, "mmproj-*.gguf", SearchOption.TopDirectoryOnly);
                string hit = PickMatching(modelPath, local, allowSingleDirect: true);
                if (hit != null) return hit;
            }

            // 2. 全局（models 目录 + ref_models.conf 引用的外部目录）
            var all = new List<string>();
            foreach (string root in EnumerateModelRoots())
                all.AddRange(SafeEnumerate(root, "mmproj-*.gguf", SearchOption.AllDirectories));

            return PickMatching(modelPath, all, allowSingleDirect: false);
        }

        /// <summary>
        /// mmproj（多模态投影文件，文件名以 "mmproj-" 开头）批量匹配索引：
        /// 一次性枚举全部模型根目录（内置 models + ref_models.conf 引用）下的 mmproj-*.gguf 缓存到内存，
        /// 之后对任意模型匹配都不再访问磁盘（模型管理窗口要一次列出所有模型的投影文件，
        /// 逐个模型调用 FindMmproj 会反复遍历磁盘）。匹配规则与 FindMmproj 完全一致。
        /// 磁盘内容变化后重新 Build 即可得到最新结果。
        /// </summary>
        public sealed class MmprojIndex
        {
            /// <summary>索引中的全部投影文件（去重，按扫描顺序）</summary>
            private readonly List<string> _all;

            private MmprojIndex(List<string> all)
            {
                _all = all;
            }

            /// <summary>扫描全部模型根目录建立索引（每次调用都会重新枚举磁盘）</summary>
            public static MmprojIndex Build()
            {
                var all = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string root in EnumerateModelRoots())
                {
                    foreach (string f in SafeEnumerate(root, "mmproj-*.gguf", SearchOption.AllDirectories))
                    {
                        if (seen.Add(NormalizeFullPath(f))) all.Add(f);
                    }
                }

                return new MmprojIndex(all);
            }

            /// <summary>索引中的全部投影文件（副本）</summary>
            public List<string> All
            {
                get { return new List<string>(_all); }
            }

            /// <summary>
            /// 挑出该模型对应的投影文件（规则同 FindMmproj）：先看模型同级目录（该目录只有一个时直接采用），
            /// 再全局严格同名兜底；找不到返回 null。
            /// </summary>
            public string Find(string modelPath)
            {
                if (string.IsNullOrEmpty(modelPath)) return null;

                // 1) 模型同级目录
                string hit = PickMatching(modelPath, LocalOf(Path.GetDirectoryName(modelPath)), allowSingleDirect: true);
                if (hit != null) return hit;

                // 2) 全局兜底：必须严格同名
                return PickMatching(modelPath, _all, allowSingleDirect: false);
            }

            /// <summary>
            /// 该模型可选的投影文件（下拉框用）：严格同名的排最前，其次同级目录的，
            /// 其余按路径排序（仍全部列出，供手动指定）。
            /// </summary>
            public List<string> Candidates(string modelPath)
            {
                if (_all.Count == 0) return new List<string>();
                if (string.IsNullOrEmpty(modelPath)) return new List<string>(_all);

                string modelDirTrim = TrimSep(Path.GetDirectoryName(modelPath));

                var ranked = new List<KeyValuePair<string, int>>();
                foreach (string f in _all)
                {
                    int rank;
                    if (IsMmprojNameMatch(modelPath, f)) rank = 0;                      // 严格同名
                    else if (!string.IsNullOrEmpty(modelDirTrim) &&
                             string.Equals(TrimSep(Path.GetDirectoryName(f)), modelDirTrim,
                                 StringComparison.OrdinalIgnoreCase)) rank = 1;     // 同级目录
                    else rank = 2;                                                      // 其它

                    ranked.Add(new KeyValuePair<string, int>(f, rank));
                }

                ranked.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
                {
                    if (a.Value != b.Value) return a.Value - b.Value;
                    return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                });

                var result = new List<string>();
                foreach (var kv in ranked) result.Add(kv.Key);
                return result;
            }

            /// <summary>索引中位于指定目录（不含子目录）的投影文件——等价于对该目录做 TopDirectoryOnly 枚举</summary>
            private List<string> LocalOf(string dir)
            {
                var local = new List<string>();
                if (string.IsNullOrEmpty(dir)) return local;

                foreach (string f in _all)
                {
                    if (string.Equals(TrimSep(Path.GetDirectoryName(f)), TrimSep(dir), StringComparison.OrdinalIgnoreCase))
                        local.Add(f);
                }
                return local;
            }
        }

        /// <summary>
        /// 所有模型根目录：内置 models 目录 + ref_models.conf 中引用的外部目录
        /// （引用为单个文件时取其所在目录；已被 models 目录覆盖的引用自动跳过）。
        /// </summary>
        private static IEnumerable<string> EnumerateModelRoots()
        {
            var roots = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (Directory.Exists(ModelsDir))
            {
                string m = NormalizeFullPath(ModelsDir);
                if (seen.Add(m)) roots.Add(m);
            }

            foreach (string reference in GetModelReferences())
            {
                if (IsUnderDir(reference, ModelsDir)) continue;   // models 目录本身已覆盖

                string dir;
                try
                {
                    if (Directory.Exists(reference)) dir = reference;
                    else if (File.Exists(reference)) dir = Path.GetDirectoryName(reference);
                    else continue;
                }
                catch { continue; }

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                dir = NormalizeFullPath(dir);
                if (seen.Add(dir)) roots.Add(dir);
            }

            return roots;
        }

        /// <summary>
        /// 从候选 mmproj 中挑出与模型严格同名的那个（判定见 IsMmprojNameMatch）。
        /// allowSingleDirect=true 且候选仅一个时直接采用（同级目录单文件布局，与 Start.bat 行为一致）。
        /// </summary>
        private static string PickMatching(string modelPath, List<string> candidates, bool allowSingleDirect)
        {
            if (candidates == null || candidates.Count == 0) return null;

            if (allowSingleDirect && candidates.Count == 1)
                return candidates[0];

            foreach (string cand in candidates)
            {
                if (IsMmprojNameMatch(modelPath, cand)) return cand;
            }

            return null;
        }

        /// <summary>
        /// 投影文件是否与模型严格同名：mmproj- 后面的字符串必须与模型文件名完全一致
        /// （均去掉扩展名，忽略大小写与 -/_/. 等分隔符差异）。
        /// 例：Bonsai-27B-Q1_0.gguf ↔ mmproj-Bonsai-27B-Q1_0.gguf。
        /// </summary>
        private static bool IsMmprojNameMatch(string modelPath, string mmprojPath)
        {
            string norm = NormalizeName(StripMmprojPrefix(Path.GetFileNameWithoutExtension(mmprojPath)));
            if (norm.Length == 0) return false;

            return norm == NormalizeName(Path.GetFileNameWithoutExtension(modelPath));
        }

        /// <summary>去掉投影文件名前缀（mmproj- / mmproj_），返回其后的字符串</summary>
        private static string StripMmprojPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            if (name.StartsWith("mmproj-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("mmproj_", StringComparison.OrdinalIgnoreCase))
                return name.Substring(7);

            return name;
        }

        /// <summary>小写并只保留 a-z0-9（Bonsai-27B_Q1 → bonsai27bq1）</summary>
        private static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }

        private static string TrimSep(string p)
        {
            return (p ?? "").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static List<string> SafeEnumerate(string dir, string pattern, SearchOption option)
        {
            var list = new List<string>();
            try
            {
                foreach (string f in Directory.EnumerateFiles(dir, pattern, option))
                    list.Add(f);
            }
            catch
            {
                // 枚举期间目录被改动/无权限：按“没找到”处理
            }
            return list;
        }

        private static long TryGetSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }
    }

    /// <summary>一个可启动的 .gguf 模型</summary>
    internal sealed class ModelInfo
    {
        public string FullPath { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }

        /// <summary>true = 来自 ref_models.conf 引用的外部目录（RelativePath 即完整路径）</summary>
        public bool IsExternal { get; set; }

        /// <summary>用户备注（remark/ 目录下对应的 .remark 纯文本文件，可多行）；无备注为空字符串</summary>
        public string Note { get; set; } = "";

        /// <summary>对应备注文件的完整路径（{ConfigRoot}\remark\模型名称_字节数.remark；由扫描时计算）</summary>
        public string RemarkPath { get; set; }

        /// <summary>true = 已软删除（存于 model_meta.conf）：扫描默认排除，管理窗口灰色显示</summary>
        public bool SoftDeleted { get; set; }

        /// <summary>文件名含 IQ2 的高压缩模型（Start.bat 约定：用小上下文，适配小内存）</summary>
        public bool IsIq2
        {
            get { return RelativePath != null && RelativePath.IndexOf("IQ2", StringComparison.OrdinalIgnoreCase) >= 0; }
        }

        public override string ToString()
        {
            return RelativePath;
        }
    }

    /// <summary>模型的用户元数据（model_meta.conf 中的一条）：软删除标记（Note 仅用于旧版备注一次性迁移）</summary>
    internal sealed class ModelMetaEntry
    {
        public string Note { get; set; } = "";
        public bool SoftDeleted { get; set; }
    }

    /// <summary>llama-bin 下的一个版本（子目录）</summary>
    internal sealed class VersionInfo
    {
        /// <summary>版本名（即 llama-bin 下的子目录名）</summary>
        public string Name { get; set; }

        /// <summary>该版本目录的完整路径</summary>
        public string FullPath { get; set; }

        /// <summary>该版本目录是否含 llama-server.exe（=可直接启动）</summary>
        public bool HasServerEx { get; set; }

        public override string ToString()
        {
            return HasServerEx ? Name : (Name + "  [缺 llama-server.exe]");
        }
    }
}
