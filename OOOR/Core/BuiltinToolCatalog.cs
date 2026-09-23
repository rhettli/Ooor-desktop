using System.Collections.Generic;

namespace ooor.Core
{
    /// <summary>
    /// 内置工具目录：列出 OoorFunc 内置的全部可用工具（不论当前 AgentOptions 是否开启）。
    /// 供「绑定函数」界面展示勾选；真正聊天时按绑定结果与全局开关共同决定是否注册给模型。
    /// 此处名称必须与 OoorFunc.Core.AgentToolRegistry 中各 IAgentTool.Name 完全一致。
    /// </summary>
    public static class BuiltinToolCatalog
    {
        /// <summary>工具名 → 一句话描述</summary>
        public static readonly Dictionary<string, string> All = new Dictionary<string, string>
        {
            // —— 只读 / 环境类（始终可用）——
            { "list_roots",      "列出沙盒白名单根目录" },
            { "list_directory",  "列出目录内容（文件/子目录/大小/时间）" },
            { "read_file",       "读取文本文件内容" },
            { "search_files",    "在目录中递归搜索文件" },
            { "get_time",        "获取当前本地时间与 UTC 时间" },
            { "get_environment", "获取操作系统/CPU/内存/机器名等环境信息" },
            { "get_app_paths",   "获取主程序相关路径（配置/模型/llama 目录等）" },
            { "get_app_info",    "获取主程序版本、工具清单、目录树状结构" },

            // —— 写入类（需 AllowWrite）——
            { "write_file",      "覆盖写入文件内容" },
            { "create_file",     "创建新文件（不覆盖已存在文件）" },
            { "delete_file",     "删除文件" },

            // —— 执行类（需 AllowCommand）——
            { "run_command",     "在 cmd.exe 中执行一条命令" },
            { "execute_script",  "执行脚本文件（.py/.ps1/.bat/.sh 等）" },

            // —— 联网类（需 AllowInternet）——
            { "web_search",      "使用搜索引擎进行关键词搜索" },
            { "fetch_url",       "打开指定 URL 并读取其内容" },
        };

        /// <summary>全部工具名（按 All 的插入顺序）</summary>
        public static IEnumerable<string> Names => All.Keys;
    }
}
