using System;
using System.Collections.Generic;
using System.IO;

namespace ooor.Core
{
    /// <summary>
    /// AgentRecord 创建/复制工厂：集中所有"如何构造一个 Agent"的业务规则。
    ///
    /// 之所以单例：
    ///   - 数据层（AgentStore）已经单例，但只管 CRUD；"建一个默认 agent 时字段怎么填"在调用方各写一份
    ///     会导致字段新增时容易漏同步（如 DuplicateSelected 曾漏 WriteDirs）。
    ///   - 把"默认值"语义收敛到这一个文件里，未来新增字段（Tags / Avatar / ...）只需改一处。
    ///
    /// 命名空间与 AgentStore / AgentRecord 同级（ooor.Core），方便任意 UI 窗体直接调用。
    /// </summary>
    public static class AgentFactory
    {
        /// <summary>
        /// 新建「默认助手」：使用系统提示词（UseSystemPrompt=true，由聊天下发处按 AgentOptions.En/Zh 取内置提示词），
        /// 自定义提示词（SystemPrompt）留空；主窗口当前选中的模型 + 全部内置工具 + 全部 MCP + 不开完全开发权限。
        /// 调用方一般紧接着 AgentStore.Add(它) 落库。
        ///
        /// 与 AgentEditForm 提交时的"合并下发"规则对齐：
        ///   UseSystemPrompt=true + SystemPrompt="" → 聊天下发处仅下发系统默认提示词（En/Zh）。
        ///   自定义 prompt 字段留空，避免新用户面对一个非空的多行文本框不知道是什么。
        ///
        /// 参数：
        ///   L                 —— 用于取翻译键（agent.defaultName）。
        ///   defaultModelPath  —— 主窗口当前选中模型的完整路径；为 null/空 时 DefaultModel 字段留 null，
        ///                        由 Ooor-cli 启动时回退到全局默认模型。
        /// </summary>
        public static AgentRecord CreateDefault(LanguageManager L, string defaultModelPath = null)
        {
            if (L == null) L = LanguageManager.Instance;

            // 内置工具全选 —— 与 AgentEditForm 新建时"全选内置工具"的语义一致
            var boundTools = new List<string>();
            try
            {
                foreach (var k in BuiltinToolCatalog.All.Keys) boundTools.Add(k);
            }
            catch { }

            // MCP 全选 —— 与 AgentEditForm 提交时取 Scan().Id 一致
            var boundMcp = new List<string>();
            try
            {
                foreach (var m in McpScanner.Scan()) boundMcp.Add(m.Id);
            }
            catch { }

            // 默认白名单目录：「白名单跟 agent 走」，不在全局预置。
            // 默认就一个 {ConfigRoot}\temp —— 临时工作区，与 Ooor-cli 的 TempDir 路径完全一致，
            // 模型写文件落不到 temp 时会自动按"路径越权"被拒，由用户在 Agent 管理里按需再加。
            var writeDirs = new List<string>();
            try
            {
                string cfg = LlamaRuntime.ConfigRoot;
                if (!string.IsNullOrEmpty(cfg))
                {
                    string tempDir = Path.Combine(cfg, "temp");
                    try { tempDir = Path.GetFullPath(tempDir).TrimEnd('\\', '/'); } catch { }
                    if (tempDir.Length > 0) writeDirs.Add(tempDir);
                }
            }
            catch { }

            return new AgentRecord
            {
                Name = SafeLocalize(L, "agent.defaultName", "默认助手"),
                FullDevPermission = false,           // 默认 Agent 仍受沙盒保护，避免模型一上来就 --trust
                UseSystemPrompt = true,              // 勾选：聊天下发处会按 CurrentLanguage 取 AgentOptions.En/Zh 内置默认提示词
                SystemPrompt = "",                   // 自定义提示词留空：避免新用户看到一个非空的多行文本框不知道是啥
                DefaultModel = string.IsNullOrEmpty(defaultModelPath) ? null : defaultModelPath,
                BoundTools = boundTools,
                BoundMcp = boundMcp,
                WriteDirs = writeDirs                // 白名单跟 agent 走：默认仅 {ConfigRoot}\temp
            };
        }

        /// <summary>
        /// 按源记录复制一个新 AgentRecord，所有字段（含 WriteDirs）都深拷贝一份新 List。
        /// 修 DuplicateSelected 漏拷 WriteDirs 的 bug —— 复制后白名单仍生效。
        ///
        /// 注意：本方法只构造 record，**不落库**；调用方负责 AgentStore.Add(...)。
        /// </summary>
        /// <param name="source">源 agent；为 null 时直接返回 null（不抛异常）。</param>
        /// <param name="newName">新名称；为 null/空时回退到 source.Name。</param>
        public static AgentRecord Clone(AgentRecord source, string newName)
        {
            if (source == null) return null;

            string name = string.IsNullOrEmpty(newName) ? (source.Name ?? "") : newName;

            return new AgentRecord
            {
                Name = name,
                FullDevPermission = source.FullDevPermission,
                SystemPrompt = source.SystemPrompt,
                UseSystemPrompt = source.UseSystemPrompt,
                DefaultModel = source.DefaultModel,
                BoundTools = source.BoundTools != null ? new List<string>(source.BoundTools) : new List<string>(),
                BoundMcp = source.BoundMcp != null ? new List<string>(source.BoundMcp) : new List<string>(),
                WriteDirs = source.WriteDirs != null ? new List<string>(source.WriteDirs) : new List<string>()
                // CreatedAt / UpdatedAt / Id 由 AgentStore.Add 在写入时补齐（自动生成 Guid + 时间戳）
            };
        }

        /// <summary>取翻译键失败时回退到字面值，避免 i18n 漏翻译导致 UI 出现键名。</summary>
        private static string SafeLocalize(LanguageManager L, string key, string fallback)
        {
            try
            {
                string v = L.T(key);
                if (string.IsNullOrEmpty(v) || v == key) return fallback;
                return v;
            }
            catch { return fallback; }
        }
    }
}
