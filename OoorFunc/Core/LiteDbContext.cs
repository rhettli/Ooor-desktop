using System;
using System.IO;
using LiteDB;

namespace OoorFunc.Core
{
    /// <summary>
    /// 共享 LiteDB 单例（Ooor-cli 与 OOOR 主程序共用）。
    /// 数据库文件：{CoreEnv.ConfigRoot}\ooor.db。
    /// 使用 Connection=shared 模式：支持 Ooor-cli 进程与 OOOR 进程同时读写同一份 db
    /// （例如 CLI 正在保存聊天记录、主程序同时打开聊天记录窗口浏览）。
    /// 各业务模块通过 Db.GetCollection<T>("name") 取集合，不要自行再开连接。
    /// </summary>
    public static class LiteDbContext
    {
        private static readonly object _lock = new object();
        private static LiteDatabase _db;

        public static LiteDatabase Db
        {
            get
            {
                if (_db == null)
                {
                    lock (_lock)
                    {
                        if (_db == null) _db = Open();
                    }
                }
                return _db;
            }
        }

        private static LiteDatabase Open()
        {
            string dir;
            try { dir = CoreEnv.ConfigRoot; }
            catch { dir = AppDomain.CurrentDomain.BaseDirectory; }
            try { Directory.CreateDirectory(dir); } catch { }

            string dbPath = Path.Combine(dir, "ooor.db");
            // shared 模式：跨进程共享；InitialSize 预留一点空间减少碎片
            return new LiteDatabase("Filename=" + dbPath + ";Connection=shared");
        }

        /// <summary>程序退出时调用，释放锁文件。不调用也无害，但能更干净地关闭。</summary>
        public static void Close()
        {
            lock (_lock)
            {
                try { _db?.Dispose(); } catch { }
                _db = null;
            }
        }
    }
}
