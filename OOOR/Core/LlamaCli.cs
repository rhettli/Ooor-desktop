using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ooor.Core
{
    /// <summary>
    /// llama-cli 交互窗口：新开一个 cmd 窗口，在里面用命令行加载模型并与模型对话。
    ///
    /// 与 llama-server（LlamaServer/ServerManager）的区别：
    ///   - 不做进程接管、不写 run_state.conf、不进作业对象：窗口由用户自己关，
    ///     主程序退出也不影响它（用户可能想让它继续跑着聊）；
    ///   - 参数口径与「启动服务」一致（-m / -ngl / -c / -n / -mm），
    ///     但只带 llama-cli 也支持的附加参数（ServerParams.BuildCliExtraArgs），
    ///     否则纯服务端参数会让 llama-cli 以 "invalid argument" 直接退出；
    ///   - 用 `cmd /k` 启动：llama-cli 退出（含报错）后窗口保留，方便看报错内容；
    ///   - 先 chcp 65001 切 UTF-8，避免中文提示在 GBK 控制台下变乱码。
    ///
    /// 注：新版 llama.cpp 的 llama-cli 默认就是交互式对话（旧的 -i/--interactive、
    /// -cnv/--conversation 参数已被移除），因此这里不传这类参数。
    /// </summary>
    internal static class LlamaCli
    {
        /// <summary>
        /// 在新 cmd 窗口中启动 llama-cli 加载模型并进入交互对话。
        /// 返回实际执行的命令行文本（供界面日志展示）；启动失败抛异常由调用方提示。
        /// </summary>
        public static string Run(ModelInfo model, string mmprojPath, int gpuLayers,
            long contextSize, int predictTokens, string extraArgs = null)
        {
            if (model == null) throw new InvalidOperationException("未选择模型");

            string exe = LlamaRuntime.FindCliExe();
            if (exe == null)
                throw new InvalidOperationException("当前版本目录下没有 " + LlamaRuntime.CliExeName
                    + "：" + LlamaRuntime.BaseDir);

            string workDir = LlamaRuntime.BaseDir;

            var args = new StringBuilder();
            args.Append("-m ").Append(Quote(model.FullPath));
            if (!string.IsNullOrEmpty(mmprojPath))
                args.Append(" -mm ").Append(Quote(mmprojPath));
            args.Append(" -ngl ").Append(gpuLayers)
                .Append(" -c ").Append(contextSize)
                .Append(" -n ").Append(predictTokens);

            string extra = (extraArgs ?? "").Trim();
            if (extra.Length > 0) args.Append(' ').Append(extra);

            // /k + &&：窗口标题、切 UTF-8、切到版本目录（llama-cli 依赖同目录下的 dll），最后启动程序。
            // 命令串本身不套外层引号（cmd /k 会连引号一起当命令），路径各自带引号即可。
            var cmd = new StringBuilder();
            cmd.Append("/k chcp 65001>nul")
               .Append(" && title ").Append(SafeTitle("ooor CLI - " + Path.GetFileName(model.FullPath)))
               .Append(" && cd /d ").Append(Quote(workDir))
               .Append(" && ").Append(Quote(exe)).Append(' ').Append(args);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmd.ToString(),
                WorkingDirectory = workDir,
                UseShellExecute = true   // 需要系统分配新的控制台窗口
            };

            using (var p = Process.Start(psi))
            {
                if (p == null) throw new InvalidOperationException("cmd 窗口启动失败");
            }

            return Quote(exe) + " " + args;
        }

        /// <summary>单行窗口标题：去掉会破坏 cmd 命令串的字符（& | &lt; &gt; ^ " %）</summary>
        private static string SafeTitle(string title)
        {
            var sb = new StringBuilder();
            foreach (char c in title ?? "")
            {
                switch (c)
                {
                    case '&': case '|': case '<': case '>': case '^':
                    case '"': case '%':
                        sb.Append('_');
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString().Trim();
        }

        /// <summary>路径含空格时加引号（与 LlamaServer 同款口径）</summary>
        private static string Quote(string path)
        {
            return "\"" + (path ?? "").Replace("\"", "\\\"") + "\"";
        }
    }
}
