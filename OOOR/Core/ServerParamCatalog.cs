using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ooor.Core
{
    /// <summary>参数值类型</summary>
    internal enum ParamKind
    {
        /// <summary>开关：值 true 时只传参数名，false/空 不传</summary>
        Bool,

        /// <summary>整数</summary>
        Int,

        /// <summary>浮点数</summary>
        Float,

        /// <summary>自由文本（含空格时启动时自动加引号）</summary>
        Text,

        /// <summary>固定候选（下拉选择）</summary>
        Enum
    }

    /// <summary>一个可配置的 llama-server 启动参数定义</summary>
    internal sealed class ServerParam
    {
        public string Name { get; internal set; }
        public string Desc { get; internal set; }
        public ParamKind Kind { get; private set; }
        /// <summary>Enum 类型的候选值</summary>
        public string[] Options { get; private set; }
        /// <summary>是否为用户自定义参数（自定义参数可改名/改备注，内置不可改）</summary>
        public bool IsCustom { get; private set; }

        public ServerParam(string name, string desc, ParamKind kind, string[] options = null)
            : this(name, desc, kind, options, false) { }

        private ServerParam(string name, string desc, ParamKind kind, string[] options, bool isCustom)
        {
            Name = name;
            Desc = desc;
            Kind = kind;
            Options = options ?? new string[0];
            IsCustom = isCustom;
        }

        /// <summary>构造自定义参数（IsCustom=true；名称/备注可后续修改）</summary>
        public static ServerParam CreateCustom(string name, string desc, ParamKind kind, string[] options)
        {
            return new ServerParam(name, desc, kind, options, true);
        }
    }

    /// <summary>
    /// llama-server 可选启动参数目录（主界面已管理的 -m/-c/-n/-ngl/--host/--port 与
    /// 空值 = 不传该参数；启动时按目录顺序拼接到命令行末尾。
    /// </summary>
    internal static class ServerParams
    {
        public const string ParamsFileName = "server_params.conf";

        public static string ParamsConfPath
        {
            get { return Path.Combine(LlamaRuntime.ConfigRoot, ParamsFileName); }
        }

        /// <summary>自定义参数定义文件（name/kind/options/desc，由「更多参数」窗口管理）</summary>
        public const string CustomParamsFileName = "llama-run-params.conf";

        public static string CustomParamsConfPath
        {
            get { return Path.Combine(LlamaRuntime.ConfigRoot, CustomParamsFileName); }
        }

        // 自定义参数加载幂等控制（首次访问时从 llama-run-params.conf 读入 Catalog 末尾）
        private static bool _customLoaded;
        private static readonly object _customLoadLock = new object();

        /// <summary>全部可选参数（顺序即启动时拼接顺序，按用途分组）</summary>
        public static readonly List<ServerParam> Catalog = new List<ServerParam>
        {
            // ---------- 采样 ----------
            new ServerParam("--temp", "采样温度，越高越随机（默认 0.8）", ParamKind.Float),
            new ServerParam("--top-k", "Top-K 采样：每步只保留概率最高的 K 个候选（0 = 关闭）", ParamKind.Int),
            new ServerParam("--top-p", "Nucleus 采样：累计概率阈值（1.0 = 关闭）", ParamKind.Float),
            new ServerParam("--min-p", "Min-P 采样：相对最高概率的最小阈值（0 = 关闭）", ParamKind.Float),
            new ServerParam("--repeat-penalty", "重复惩罚系数（1.0 = 关闭）", ParamKind.Float),
            new ServerParam("--repeat-last-n", "重复惩罚回看的最近 token 数", ParamKind.Int),
            new ServerParam("--seed", "随机种子（-1 = 每次随机）", ParamKind.Int),
            new ServerParam("--samplers", "采样链顺序，如 top_p,top_k,min_p,temp", ParamKind.Text),

            // ---------- 性能 / 显存 ----------
            new ServerParam("-fa", "Flash Attention：更快、显存更省（KV 量化需要）", ParamKind.Bool),
            new ServerParam("--cache-type-k", "K 缓存数据类型（需要 -fa，可省显存）", ParamKind.Enum,
                new[] { "f32", "f16", "bf16", "q8_0", "q4_0", "q4_1", "iq4_nl" }),
            new ServerParam("--cache-type-v", "V 缓存数据类型（需要 -fa，可省显存）", ParamKind.Enum,
                new[] { "f32", "f16", "bf16", "q8_0", "q4_0", "q4_1", "iq4_nl" }),
            new ServerParam("-t", "生成时 CPU 线程数（0 = 自动）", ParamKind.Int),
            new ServerParam("-tb", "批处理 CPU 线程数（0 = 自动）", ParamKind.Int),
            new ServerParam("-np", "并发槽位数：可同时服务多个请求（配合 -cb）", ParamKind.Int),
            new ServerParam("-cb", "连续批处理：配合 -np 提高吞吐", ParamKind.Bool),
            new ServerParam("--no-mmap", "禁用内存映射，整模型直接读入内存", ParamKind.Bool),
            new ServerParam("--mlock", "锁定内存，防止模型被换出到磁盘", ParamKind.Bool),
            new ServerParam("--swa-full", "SWA 模型扩展缓存（部分模型需要，显存占用增加）", ParamKind.Bool),

            // ---------- 服务 / 网络 ----------
            new ServerParam("--api-key", "API 鉴权密钥：调用接口需携带 Authorization: Bearer", ParamKind.Text),
            new ServerParam("--threads-http", "HTTP 服务线程数（默认 16）", ParamKind.Int),
            new ServerParam("--keep-alive", "模型在内存中保持的秒数（-1 = 常驻，OpenAI 兼容接口）", ParamKind.Int),
            new ServerParam("--metrics", "输出 Prometheus 指标（/metrics）", ParamKind.Bool),
            new ServerParam("--log-disable", "关闭日志文件输出", ParamKind.Bool),
            new ServerParam("--verbose", "详细输出（调试用）", ParamKind.Bool),

            // ---------- 功能模式 ----------
            new ServerParam("--embeddings", "Embedding 向量服务模式", ParamKind.Bool),
            new ServerParam("--pooling", "Embedding 池化模式", ParamKind.Enum,
                new[] { "none", "mean", "cls", "last" }),
            new ServerParam("--reranking", "重排序（rerank）服务模式", ParamKind.Bool),
            new ServerParam("--chat-template", "聊天模板名或路径（如 chatml）", ParamKind.Text),
            new ServerParam("--reasoning-format", "思考链输出格式", ParamKind.Enum,
                new[] { "deepseek", "auto", "none" }),

            new ServerParam("--jinja", "启用 GGUF 内置 Jinja chat_template（对话模板）", ParamKind.Bool),
            new ServerParam("--webui-mcp-proxy", "开启 llama-server 内置的 CORS 代理，解决浏览器 WebUI 跨域问题", ParamKind.Bool),


        };

        /// <summary>读取参数值配置：参数名 → 值（仅含有值的参数）</summary>
        public static Dictionary<string, string> LoadValues()
        {
            EnsureCustomLoaded();   // 保证 Catalog 含自定义项（值拼接/写回都基于 Catalog）
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            string path = ParamsConfPath;
            if (!File.Exists(path)) return dict;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return dict; }

            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string name = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (name.Length == 0) continue;
                // 参数值可以包含 '='，取第一个等号后的全部；后写的行覆盖先写的
                dict[name] = value;
            }
            return dict;
        }

        /// <summary>写回参数值配置（保序；空值不写行 = 不传该参数）</summary>
        public static void SaveValues(Dictionary<string, string> values)
        {
            var lines = new List<string>();
            // 按目录顺序写，便于人工查看
            foreach (var p in Catalog)
            {
                string value;
                if (values != null && values.TryGetValue(p.Name, out value) &&
                    !string.IsNullOrEmpty(value))
                {
                    lines.Add(p.Name + "=" + value);
                }
            }

            Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
            File.WriteAllLines(ParamsConfPath, lines, new UTF8Encoding(false));
        }

        /// <summary>设置单个参数的值（空值 = 移除该参数）</summary>
        public static void SetValue(string name, string value)
        {
            var values = LoadValues();
            value = (value ?? "").Trim();
            if (value.Length == 0) values.Remove(name);
            else values[name] = value;
            SaveValues(values);
        }

        // ==================== 自定义参数管理（llama-run-params.conf） ====================

        /// <summary>确保自定义参数已从 llama-run-params.conf 载入 Catalog（幂等，线程安全）</summary>
        public static void EnsureCustomLoaded()
        {
            if (_customLoaded) return;
            lock (_customLoadLock)
            {
                if (_customLoaded) return;
                LoadCustomCatalogCore();
                _customLoaded = true;
            }
        }

        /// <summary>读取 llama-run-params.conf，把自定义参数 append 到 Catalog 末尾（不覆盖同名内置）</summary>
        private static void LoadCustomCatalogCore()
        {
            string path = CustomParamsConfPath;
            if (!File.Exists(path)) return;

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch { return; }

            var exist = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in Catalog) exist.Add(p.Name);

            foreach (string raw in lines)
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                // 格式：name<TAB>kind<TAB>options(逗号分隔,可空)<TAB>desc
                string[] parts = line.Split('\t');
                if (parts.Length < 2) continue;
                string name = parts[0].Trim();
                if (name.Length == 0 || exist.Contains(name)) continue;
                ParamKind kind;
                if (!Enum.TryParse<ParamKind>(parts[1].Trim(), out kind)) continue;
                string optsStr = parts.Length > 2 ? parts[2].Trim() : "";
                string desc = parts.Length > 3 ? parts[3].Trim() : "";
                string[] opts = optsStr.Length > 0
                    ? optsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    : new string[0];
                Catalog.Add(ServerParam.CreateCustom(name, desc, kind, opts));
                exist.Add(name);
            }
        }

        /// <summary>把 Catalog 中自定义参数写回 llama-run-params.conf</summary>
        private static void SaveCustomCatalog()
        {
            var lines = new List<string>();
            lines.Add("# 自定义启动参数（由「更多参数」窗口管理；格式：name<TAB>kind<TAB>options<TAB>desc）");
            foreach (var p in Catalog)
            {
                if (!p.IsCustom) continue;
                string opts = p.Options != null && p.Options.Length > 0 ? string.Join(",", p.Options) : "";
                lines.Add(p.Name + "\t" + p.Kind + "\t" + opts + "\t" + p.Desc);
            }
            Directory.CreateDirectory(LlamaRuntime.ConfigRoot);
            File.WriteAllLines(CustomParamsConfPath, lines, new UTF8Encoding(false));
        }

        /// <summary>新建自定义参数并落盘（重名抛异常；成功后 append 到 Catalog 末尾）</summary>
        public static void AddCustomParam(string name, string desc, ParamKind kind, string[] options)
        {
            EnsureCustomLoaded();
            name = (name ?? "").Trim();
            if (name.Length == 0) throw new ArgumentException("参数名不能为空。");
            if (Catalog.Any(p => p.Name == name)) throw new ArgumentException("参数名已存在：" + name);
            Catalog.Add(ServerParam.CreateCustom(name, desc, kind, options));
            SaveCustomCatalog();
        }

        /// <summary>更新自定义参数的名称/备注（仅自定义可改；改名时同步迁移 server_params.conf 的值）</summary>
        public static void UpdateCustomParam(ServerParam param, string newName, string desc)
        {
            if (param == null || !param.IsCustom) throw new ArgumentException("仅自定义参数可编辑。");
            newName = (newName ?? "").Trim();
            if (newName.Length == 0) throw new ArgumentException("参数名不能为空。");

            string oldName = param.Name;
            if (newName != oldName && Catalog.Any(p => p.Name == newName))
                throw new ArgumentException("参数名已存在：" + newName);

            param.Name = newName;
            param.Desc = desc;

            if (newName != oldName)
            {
                var values = LoadValues();
                if (values.TryGetValue(oldName, out string v))
                {
                    values.Remove(oldName);
                    values[newName] = v;
                    SaveValues(values);
                }
            }
            SaveCustomCatalog();
        }

        /// <summary>删除自定义参数（仅自定义可删；同时从 server_params.conf 移除其值）</summary>
        public static void RemoveCustomParam(ServerParam param)
        {
            if (param == null || !param.IsCustom) throw new ArgumentException("仅自定义参数可删除。");

            string name = param.Name;
            Catalog.Remove(param);

            var values = LoadValues();
            if (values.Remove(name)) SaveValues(values);

            SaveCustomCatalog();
        }

        /// <summary>已启用（启动时会实际传入）的参数个数</summary>
        public static int EnabledCount()
        {
            var values = LoadValues();
            int n = 0;
            foreach (var p in Catalog)
            {
                string value;
                if (values.TryGetValue(p.Name, out value) && !string.IsNullOrEmpty(value)) n++;
            }
            return n;
        }

        /// <summary>
        /// 把已启用的参数拼成一段命令行（如 "--temp 0.8 -fa --api-key sk-xxx"），
        /// 追加到固定参数之后。值含空格时自动加引号。
        /// </summary>
        public static string BuildExtraArgs()
        {
            return BuildFrom(LoadValues(), null);
        }

        /// <summary>
        /// 仅供 llama-server 使用、llama-cli 不支持的参数：命令行交互窗口启动时跳过，
        /// 否则 llama-cli 会以「invalid argument」直接退出（采样/性能类参数照常传入）。
        /// </summary>
        private static readonly HashSet<string> ServerOnlyParams = new HashSet<string>(StringComparer.Ordinal)
        {
            "-np", "-cb",                    // 服务并发槽位 / 连续批处理
            "--api-key", "--threads-http", "--keep-alive", "--metrics", "--log-disable",
            "--verbose",
            "--embeddings", "--pooling", "--reranking",   // 向量 / 重排服务模式
            "--swa-full"
        };

        /// <summary>与 BuildExtraArgs 相同，但滤掉服务专用参数（cmd 窗口跑 llama-cli 时用）</summary>
        public static string BuildCliExtraArgs()
        {
            return BuildFrom(LoadValues(), p => !ServerOnlyParams.Contains(p.Name));
        }

        /// <summary>按目录顺序把已启用的参数拼成命令行；filter 返回 false 的参数跳过（null = 全部保留）</summary>
        private static string BuildFrom(Dictionary<string, string> values, Func<ServerParam, bool> filter)
        {
            var sb = new StringBuilder();
            foreach (var p in Catalog)
            {
                if (filter != null && !filter(p)) continue;

                string value;
                if (!values.TryGetValue(p.Name, out value)) continue;
                value = (value ?? "").Trim();
                if (value.Length == 0) continue;

                // Bool：true 只传开关名，false / 其他值不传
                if (p.Kind == ParamKind.Bool)
                {
                    if (!bool.TryParse(value, out bool on) || !on) continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(p.Name);
                    continue;
                }

                if (sb.Length > 0) sb.Append(' ');
                sb.Append(p.Name).Append(' ').Append(Quote(value));
            }
            return sb.ToString();
        }

        private static string Quote(string value)
        {
            if (value.Length == 0) return "\"\"";
            if (value.IndexOf(' ') < 0 && value.IndexOf('"') < 0) return value;
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
