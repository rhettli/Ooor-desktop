using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace OoorFunc.Core
{
    /// <summary>
    /// Agent 配置的只读视图（Ooor-cli 侧使用）。
    /// 与 OOOR 主程序的 ooor.Core.AgentRecord 对应同一份文档（agents 集合），
    /// CLI 读取名称 / 系统提示词 / 默认模型 / 沙盒白名单；其余字段 LiteDB 自动忽略。
    /// </summary>
    public class AgentInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SystemPrompt { get; set; }
        public string DefaultModel { get; set; }
        /// <summary>沙盒白名单目录（绝对路径）。白名单与 agent 走：CLI 启动时按它的值替换 AllowedRoots。</summary>
        public List<string> WriteDirs { get; set; } = new List<string>();
    }

    /// <summary>从共享 LiteDB 读取 Agent 列表（只读；写操作仍由 OOOR 主程序的 AgentStore 负责）。</summary>
    public static class AgentCatalog
    {
        private const string ColName = "agents";

        private static ILiteCollection<AgentInfo> Col
        {
            get { return LiteDbContext.Db.GetCollection<AgentInfo>(ColName); }
        }

        /// <summary>全部 Agent（按名称排序，便于菜单展示）。</summary>
        public static List<AgentInfo> All()
        {
            try { return Col.FindAll().OrderBy(a => a.Name).ToList(); }
            catch { return new List<AgentInfo>(); }
        }

        public static AgentInfo GetByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                string n = name.Trim();
                return Col.FindAll().FirstOrDefault(a => string.Equals(a.Name, n, StringComparison.OrdinalIgnoreCase));
            }
            catch { return null; }
        }
    }
}
