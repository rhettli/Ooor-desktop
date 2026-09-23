using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ooor_sqlite_mcp
{
    // 这是ooor mcp服务，需要给程序提供调用sqlite的功能

    internal static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }

        // 给模型提供执行sqlite脚本的能力
        public static void execSql(string db_path,string sql)
        {
            // 连接字符串，文件名不存在会自动创建   
            string connStr = $"Data Source={db_path};";

            // 1. 创建表
        //    using (var conn = new SqliteConnection(connStr))
        //    {
        //        conn.Open();
        //        string createSql = @"
        //CREATE TABLE IF NOT EXISTS user(
        //    id INTEGER PRIMARY KEY AUTOINCREMENT,
        //    name TEXT NOT NULL,
        //    age INTEGER
        //);";
        //        using var cmd = new SqliteCommand(createSql, conn);
        //        cmd.ExecuteNonQuery();
        //    }

        //    // 2. 插入数据（参数化，防SQL注入）
        //    using (var conn = new SqliteConnection(connStr))
        //    {
        //        conn.Open();
        //        string insertSql = "INSERT INTO user(name,age) VALUES(@name,@age);";
        //        using var cmd = new SqliteCommand(insertSql, conn);
        //        cmd.Parameters.AddWithValue("@name", "张三");
        //        cmd.Parameters.AddWithValue("@age", 25);
        //        int rows = cmd.ExecuteNonQuery();
        //        Console.WriteLine($"插入行数：{rows}");
        //    }

        //    // 3. 查询
        //    using (var conn = new SqliteConnection(connStr))
        //    {
        //        conn.Open();
        //        string sql = "SELECT id,name,age FROM user;";
        //        using var cmd = new SqliteCommand(sql, conn);
        //        using var reader = cmd.ExecuteReader();
        //        while (reader.Read())
        //        {
        //            long id = reader.GetInt64(0);
        //            string name = reader.GetString(1);
        //            int age = reader.GetInt32(2);
        //            Console.WriteLine($"{id} | {name} | {age}");
        //        }
        //    }
        }
    }
}
