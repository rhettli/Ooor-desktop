using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace ooor.Core
{
    /// <summary>
    /// 一个 Agent 配置记录。
    /// 持久化到 LiteDB 的 agents 集合；Id 为 Guid 字符串作为主键。
    /// BoundTools：绑定的内置工具名（来自 OoorFunc 内置工具注册表）。
    /// BoundMcp：绑定的 MCP 服务器标识（格式「名称|可执行路径」，程序启动时按需拉起）。
    /// </summary>
    public class AgentRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool FullDevPermission { get; set; }
        public string SystemPrompt { get; set; }
        /// <summary>是否使用系统提示词：true=使用系统 prompt（用户可在 SystemPrompt 补充）；false=仅用用户输入的 prompt</summary>
        public bool UseSystemPrompt { get; set; } = true;
        public string DefaultModel { get; set; }
        public List<string> BoundTools { get; set; } = new List<string>();
        public List<string> BoundMcp { get; set; } = new List<string>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Agent 记录的 CRUD 封装（单例，底层走 LiteDbStore）。
    /// 线程安全：LiteDatabase 自带锁，这里不加额外锁。
    /// </summary>
    public static class AgentStore
    {
        private const string ColName = "agents";

        private static ILiteCollection<AgentRecord> Col =>
            LiteDbStore.Instance.Db.GetCollection<AgentRecord>(ColName);

        static AgentStore()
        {
            try
            {
                // 建索引：名称唯一（便于去重与查找），创建时间排序
                var col = Col;
                col.EnsureIndex(x => x.Name);
                col.EnsureIndex(x => x.CreatedAt);
            }
            catch { /* 首次建表失败不影响后续 */ }
        }

        /// <summary>全部 Agent（按创建时间倒序）</summary>
        public static List<AgentRecord> All()
        {
            try
            {
                return Col.FindAll().OrderByDescending(a => a.CreatedAt).ToList();
            }
            catch { return new List<AgentRecord>(); }
        }

        public static AgentRecord Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return Col.FindById(id); }
            catch { return null; }
        }

        /// <summary>新增；Id 为空时自动生成 Guid。返回写入后的记录（含 Id / 时间戳）。</summary>
        public static AgentRecord Add(AgentRecord a)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (string.IsNullOrEmpty(a.Id)) a.Id = Guid.NewGuid().ToString("N");
            DateTime now = DateTime.Now;
            a.CreatedAt = now;
            a.UpdatedAt = now;
            if (a.BoundTools == null) a.BoundTools = new List<string>();
            if (a.BoundMcp == null) a.BoundMcp = new List<string>();
            Col.Insert(a.Id, a);
            return a;
        }

        /// <summary>更新（按 Id）；自动刷新 UpdatedAt。返回是否成功。</summary>
        public static bool Update(AgentRecord a)
        {
            if (a == null || string.IsNullOrEmpty(a.Id)) return false;
            a.UpdatedAt = DateTime.Now;
            if (a.BoundTools == null) a.BoundTools = new List<string>();
            if (a.BoundMcp == null) a.BoundMcp = new List<string>();
            try { return Col.Update(a.Id, a); }
            catch { return false; }
        }

        public static bool Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            try { return Col.Delete(id); }
            catch { return false; }
        }

        /// <summary>按名称查重（新增/编辑时用）</summary>
        public static bool NameExists(string name, string exceptId = null)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                return Col.Exists(a => a.Name == name && a.Id != exceptId);
            }
            catch { return false; }
        }
    }
}
