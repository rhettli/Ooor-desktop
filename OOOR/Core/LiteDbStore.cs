using System;
using LiteDB;
using OoorFunc.Core;

namespace ooor.Core
{
    /// <summary>
    /// LiteDB 单例入口（OOOR 侧）。
    /// 实际连接由 OoorFunc.Core.LiteDbContext 统一管理（shared 模式，ooor.db），
    /// 这里仅做一层薄封装，保持 AgentStore 等调用方的 LiteDbStore.Instance.Db 写法不变。
    /// 这样 Ooor-cli 进程写聊天记录、OOOR 进程浏览/管理时可同时访问同一份 db。
    /// </summary>
    public sealed class LiteDbStore : IDisposable
    {
        public static readonly LiteDbStore Instance = new LiteDbStore();

        /// <summary>直接返回 OoorFunc 共享的 LiteDatabase（勿自行 Dispose）。</summary>
        public LiteDatabase Db
        {
            get
            {
                EnsureEnvSynced();
                return LiteDbContext.Db;
            }
        }

        private LiteDbStore() { }

        /// <summary>
        /// 确保 CoreEnv.ConfigRoot 已按 OOOR 的 LlamaRuntime 同步，
        /// 避免在 AgentHost 尚未初始化时取库，路径退回到按 exe 推导的默认值而连到另一份 ooor.db。
        /// </summary>
        private static void EnsureEnvSynced()
        {
            try { AgentHost.SyncCoreEnv(); } catch { }
        }

        public void Dispose()
        {
            // 连接归 OoorFunc 管理，这里不释放；仅提供 IDisposable 接口占位
        }
    }
}
