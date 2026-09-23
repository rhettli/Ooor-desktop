using System;
using System.Collections.Generic;
using System.IO;

namespace OoorFunc.Core
{
    /// <summary>
    /// 本地 Agent 运行参数。
    ///
    /// 安全默认：AllowWrite=false / AllowCommand=false → 工具集只读；
    /// 高危工具（write/delete/run_command）单独受 Configuration 字段约束，启用也建议由 UI 在调用前弹窗确认。
    /// AllowedRoots 为白名单目录的绝对路径集合：所有文件/目录参数解析后必须落在某个 root 之下或相等，
    /// 否则视为越权并拒绝（用规范化路径比对，忽略大小写与分隔符差异）。
    /// </summary>
    public sealed class AgentOptions
    {
        /// <summary>
        /// 内置默认系统提示词文本：AgentOptions 实例的 SystemPrompt 默认初始化为它；
        /// AgentEditForm 的只读「系统提示词」框直接展示它。
        /// </summary>
        public static string DefaultSystemPrompt
        {
            get
            {

                return "你是一个运行在用户 Windows 电脑上的本地 AI 助手，可以调用工具真实地查看与操作文件。" +
            "不要声称自己无法访问文件系统：需要信息时直接调用对应工具（list_roots / list_directory / read_file / get_app_info 等）。" +
            "所有文件路径最终都会落到底层 Windows 文件系统，请只用 UTF-8 文本读写。" +
            "只能访问沙盒白名单目录，越权会被拒绝；开始文件操作前先调 list_roots 查看白名单目录有哪些。" +
            $"给文件的 path 参数若只写文件名或相对路径（如 sum.py、out\\log.txt），会自动落到白名单内的临时工作目录 \"{CoreEnv.ConfigRoot}\\temp\" 下，" +
            "工具返回值里会给出实际落盘的完整路径，请以返回的路径为准。" +
            "需要读写白名单外的位置时，先请用户用界面上的「添加目录」加入白名单。" +
            "写文件/删文件/执行命令与脚本属于高危操作，工具会弹出确认框让用户审批：" +
            "默认一律弹窗，除非用户开启了「AI 自行判断」，此时你在有把握的操作（例如自己刚创建的临时脚本）上可传 confirm=false 跳过确认，" +
            "不确定或涉及用户既有文件时必须保持确认。执行前先用一句话说明你要做什么。" +
            "如果当前有工具 web_search / fetch_url（用户开启了「允许联网」），你可以联网搜索与打开网页获取实时信息；" +
            "没有这两个工具时不要编造网上的内容，应告知用户可在界面开启联网。";
            }
        }

        /// <summary>
        /// 本 Agent 实例的系统提示词：每个新会话注入一次；默认取 DefaultSystemPrompt，
        /// 宿主可覆盖（如 CLI 的自定义人设），ReplaceSystemPrompt / ImportHistoryJson 也会改写它。
        /// </summary>
        public string SystemPrompt = DefaultSystemPrompt;

        /// <summary>沙盒白名单（绝对路径）。</summary>
        public List<string> AllowedRoots = new List<string>();

        /// <summary>true：注册 write_file / create_file / delete_file 工具；高危，需 UI 配合弹窗确认。</summary>
        public bool AllowWrite;

        /// <summary>true：注册 run_command / execute_script 工具；高危，需 UI 配合弹窗确认 + 命令黑名单。</summary>
        public bool AllowCommand;

        /// <summary>
        /// true：允许模型自行判断（工具参数 confirm=false 时）跳过写/删/执行类工具的确认弹窗。
        /// false（默认）：无论模型传什么，高危操作一律弹窗，确认权只在用户手里。
        /// </summary>
        public bool TrustAiJudgment;

        /// <summary>true：注册联网工具（web_search / fetch_url）；会访问公网，默认关闭。</summary>
        public bool AllowInternet;

        /// <summary>单轮最大步数（assistant+tool 算一步；含最终无 tool_calls 的那一步）。</summary>
        public int MaxSteps = 12;

        /// <summary>read_file 单次读取上限（字节），超过报错而不读，避免把大模型挤爆。</summary>
        public int ReadMaxBytes = 256 * 1024;

        /// <summary>list_dir 返回的最大条目数；超过会标注 truncated。</summary>
        public int ListMaxEntries = 200;

        /// <summary>run_command 单次最长执行时间；超时强制结束。</summary>
        public TimeSpan CommandTimeout = TimeSpan.FromSeconds(15);

        /// <summary>单次 /v1/chat/completions 请求超时。</summary>
        public TimeSpan HttpTimeout = TimeSpan.FromMinutes(2);

        /// <summary>联网工具（web_search / fetch_url）单次请求超时。</summary>
        public TimeSpan WebTimeout = TimeSpan.FromSeconds(20);

        /// <summary>构造一份默认配置：只读 + 沙盒为内置 models 目录 + 配置根目录。</summary>
        public static AgentOptions Default()
        {
            var roots = new List<string>();
            try
            {
                string models = CoreEnv.ModelsDir;
                if (!string.IsNullOrEmpty(models)) roots.Add(models);
            }
            catch { }
            try
            {
                string cfg = CoreEnv.ConfigRoot;
                if (!string.IsNullOrEmpty(cfg)) roots.Add(cfg);
            }
            catch { }
            // 规范化去重
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < roots.Count; )
            {
                string n = NormalizeFullPath(roots[i]);
                if (n.Length == 0 || !seen.Add(n)) roots.RemoveAt(i);
                else { roots[i] = n; i++; }
            }

            var opt = new AgentOptions { AllowedRoots = roots };
            // 用户上次在「AI 助手」窗口里的开关与追加的沙盒目录（agent.conf）
            AgentSettings.Load(opt);
            return opt;
        }

        /// <summary>
        /// 高危操作是否需要弹窗确认：
        ///   aiAskedToSkip = 模型主动传的 confirm=false（它认为自己有把握）；
        /// 只有用户开启了「AI 自行判断」才会放行，否则一律确认。
        /// </summary>
        public bool NeedConfirm(bool aiAskedToSkip)
        {
            return !(TrustAiJudgment && aiAskedToSkip);
        }

        /// <summary>校验 path 是否位于任一白名单 root 之下（含等于）。失败抛异常。</summary>
        public void EnsurePathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("路径为空");
            string full = NormalizeFullPath(path);
            if (full.Length == 0)
                throw new InvalidOperationException("路径无效：" + path);

            foreach (string root in AllowedRoots)
            {
                string r = NormalizeFullPath(root);
                if (r.Length == 0) continue;
                if (string.Equals(full, r, StringComparison.OrdinalIgnoreCase)) return;
                if (full.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    full.StartsWith(r + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            throw new InvalidOperationException("路径超出沙盒：" + path);
        }

        /// <summary>
        /// 模型给相对路径 / 纯文件名时的落地目录：{ConfigRoot}\temp。
        /// 该目录在默认白名单内（ConfigRoot 本身就是白名单 root），无需额外放行。
        /// </summary>
        public string TempDir
        {
            get
            {
                try
                {
                    string cfg = CoreEnv.ConfigRoot;
                    if (!string.IsNullOrEmpty(cfg)) return Path.Combine(cfg, "temp");
                }
                catch { }
                return Path.Combine(Path.GetTempPath(), "ooor-agent-temp");
            }
        }

        /// <summary>
        /// 解析模型给的路径：绝对路径原样返回；相对路径（含纯文件名，如 "sum.py" / "out\log.txt"）
        /// 一律落到 {ConfigRoot}\temp 下。返回规范化后的绝对路径，供工具直接使用与回显。
        /// </summary>
        public string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string trimmed = path.Trim();
            if (trimmed.Length == 0) return path;

            bool rooted;
            try { rooted = Path.IsPathRooted(trimmed); }
            catch { return path; }

            try
            {
                string rel = trimmed;
                if (!rooted)
                {
                    // 模型常自带一层 "temp/xxx"：TempDir 本身已是 ...\temp，剥掉避免出现 temp\temp
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    while (rel.Length >= 5 &&
                           rel.StartsWith("temp" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        rel = rel.Substring(5);
                    if (string.Equals(rel, "temp", StringComparison.OrdinalIgnoreCase)) rel = "";
                }

                string full = rooted ? trimmed : (rel.Length > 0 ? Path.Combine(TempDir, rel) : TempDir);
                string norm = NormalizeFullPath(full);
                return norm.Length > 0 ? norm : full;
            }
            catch { return path; }
        }

        /// <summary>解析 + 白名单校验一步完成；返回可用的绝对路径（校验失败抛异常）。</summary>
        public string ResolveAllowed(string path)
        {
            string resolved = ResolvePath(path);
            EnsurePathAllowed(resolved);
            return resolved;
        }

        private static string NormalizeFullPath(string p)
        {
            if (string.IsNullOrEmpty(p)) return "";
            try
            {
                string s = Path.GetFullPath(p);
                return s.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch { return ""; }
        }
    }
}