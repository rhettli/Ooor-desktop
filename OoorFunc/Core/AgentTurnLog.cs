using System;
using System.Collections.Generic;

namespace OoorFunc.Core
{
    /// <summary>
    /// 单轮对话（一次 SendAsync）的文件动作记录：
    ///   - WrittenFiles：模型通过 write_file / create_file 实际写入（成功）的文件完整路径；
    ///   - ExecutedFiles：模型通过 execute_script / run_command 实际执行或打开的文件完整路径。
    /// 去重（忽略大小写，按首次出现顺序保留）；每轮开始 Reset。
    /// 由 AgentToolRegistry 持有，各工具上报，SendAsync 结束时汇总进 AgentResult。
    /// </summary>
    public sealed class AgentTurnLog
    {
        private readonly List<string> _writtenFiles = new List<string>();
        private readonly List<string> _executedFiles = new List<string>();

        /// <summary>本轮写入的文件（按首次出现顺序）</summary>
        public IReadOnlyList<string> WrittenFiles { get { return _writtenFiles; } }

        /// <summary>本轮执行/运行的文件（按首次出现顺序）</summary>
        public IReadOnlyList<string> ExecutedFiles { get { return _executedFiles; } }

        /// <summary>记录一个写入的文件（空值/重复忽略）</summary>
        public void RecordWritten(string path)
        {
            AddDistinct(_writtenFiles, path);
        }

        /// <summary>记录一个执行/运行的文件（空值/重复忽略）</summary>
        public void RecordExecuted(string path)
        {
            AddDistinct(_executedFiles, path);
        }

        /// <summary>新一轮开始：清空全部记录</summary>
        public void Reset()
        {
            _writtenFiles.Clear();
            _executedFiles.Clear();
        }

        private static void AddDistinct(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string p = path.Trim();
            if (p.Length == 0) return;
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], p, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(p);
        }
    }
}
