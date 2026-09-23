using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

namespace OoorFunc.Core
{
    /// <summary>
    /// 一次控制台聊天会话记录。列与 OOOR 的 ConsoleChatRecord 列表视图对应，
    /// 额外存 Messages（完整消息历史 JSON）用于「接着聊」时恢复上下文。
    /// </summary>
    public class ChatRecord
    {
        public string Id { get; set; }
        public string Title { get; set; }              // 聊天标题
        public string Model { get; set; }              // 使用的模型路径
        public string Agent { get; set; }              // 绑定的 Agent 名称（可为空）
        public string Remark { get; set; }             // 备注
        public string LastReasoningSpeed { get; set; } // 最近一次思考速度（展示用字符串）
        public string LastContentSpeed { get; set; }   // 最近一次正文速度
        public string Messages { get; set; }           // 完整消息历史（JSON 字符串，供「接着聊」恢复）
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 聊天会话记录的 CRUD（底层走 LiteDbContext）。
    /// 线程安全：LiteDatabase shared 模式自带锁，这里不再额外加锁。
    /// </summary>
    public static class ChatStore
    {
        private const string ColName = "chat_sessions";

        private static ILiteCollection<ChatRecord> Col =>
            LiteDbContext.Db.GetCollection<ChatRecord>(ColName);

        static ChatStore()
        {
            try
            {
                var col = Col;
                col.EnsureIndex(x => x.UpdatedAt);
                col.EnsureIndex(x => x.Title);
            }
            catch { }
        }

        /// <summary>全部会话（按更新时间倒序，最近聊过的排最前）</summary>
        public static List<ChatRecord> All()
        {
            try { return Col.FindAll().OrderByDescending(c => c.UpdatedAt).ToList(); }
            catch { return new List<ChatRecord>(); }
        }

        public static ChatRecord Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            try { return Col.FindById(id); }
            catch { return null; }
        }

        /// <summary>新增会话；Id 为空时自动生成 Guid。返回含 Id/时间戳的记录。</summary>
        public static ChatRecord Add(ChatRecord c)
        {
            if (c == null) throw new ArgumentNullException("c");
            if (string.IsNullOrEmpty(c.Id)) c.Id = Guid.NewGuid().ToString("N");
            DateTime now = DateTime.Now;
            c.CreatedAt = now;
            c.UpdatedAt = now;
            if (c.Title == null) c.Title = "";
            Col.Insert(c.Id, c);
            return c;
        }

        /// <summary>更新（按 Id）；自动刷新 UpdatedAt。</summary>
        public static bool Update(ChatRecord c)
        {
            if (c == null || string.IsNullOrEmpty(c.Id)) return false;
            c.UpdatedAt = DateTime.Now;
            try { return Col.Update(c.Id, c); }
            catch { return false; }
        }

        public static bool Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            try { return Col.Delete(id); }
            catch { return false; }
        }
    }
}
