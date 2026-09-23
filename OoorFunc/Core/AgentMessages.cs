using System;
using System.Collections.Generic;

namespace OoorFunc.Core
{
    /// <summary>对话角色（OpenAI 兼容 /v1/chat/completions 的 messages.role 取值）</summary>
    public enum AgentRole
    {
        System,
        User,
        Assistant,
        Tool
    }

    /// <summary>流式增量的种类：None=完整步（终态文本/工具事件），其余表示 Delta 是一块流式增量</summary>
    public enum AgentStreamKind
    {
        None,
        /// <summary>思考过程增量（reasoning_content / content 里的 &lt;think&gt; 段）</summary>
        Reasoning,
        /// <summary>正文增量</summary>
        Content
    }

    /// <summary>一轮对话消息：assistant 消息可携带 ToolCalls；tool 消息携带 ToolCallId + 文本结果</summary>
    public sealed class AgentMessage
    {
        public AgentRole Role;
        public string Content = "";

        /// <summary>仅 role=tool 时使用：对应 assistant.tool_calls[].id</summary>
        public string ToolCallId = "";

        /// <summary>仅 role=tool 时使用：被调用的工具名（便于追踪）</summary>
        public string ToolName = "";

        /// <summary>仅 role=assistant 时使用：模型发起的工具调用</summary>
        public List<AgentToolCall> ToolCalls;
    }

    /// <summary>助手消息里的一个工具调用（OpenAI 规范：id/type="function"/function.name/function.arguments）</summary>
    public sealed class AgentToolCall
    {
        public string Id = "";
        public string Name = "";
        /// <summary>模型输出的 JSON 字符串（OpenAI 约定 arguments 是 stringified JSON，不是 object）</summary>
        public string ArgumentsJson = "";
    }

    /// <summary>一次循环中的中间事件（给 UI/日志订阅用，回调在线程池线程上）</summary>
    public sealed class AgentStep
    {
        public DateTime At;
        /// <summary>System/User/Assistant/Tool；终态步为 Assistant</summary>
        public AgentRole Role;
        /// <summary>assistant 步：模型返回文本（可能为空，表示纯工具调用）</summary>
        public string Text = "";
        /// <summary>assistant 步：思考过程（reasoning_content 或 content 里的 &lt;think&gt; 段），仅展示用、不回灌历史</summary>
        public string Reasoning = "";
        /// <summary>Stream != None 时本条是流式增量：Delta 为文本片段，Text/Reasoning 无意义</summary>
        public AgentStreamKind Stream = AgentStreamKind.None;
        /// <summary>Stream != None 时：本块增量文本</summary>
        public string Delta = "";
        /// <summary>tool 步：被调用的工具名</summary>
        public string ToolName = "";
        /// <summary>tool 步：执行参数（JSON 字符串）</summary>
        public string ToolArgs = "";
        /// <summary>tool 步：执行结果文本</summary>
        public string ToolResult = "";
        /// <summary>tool 步：执行是否成功</summary>
        public bool ToolOk = true;
    }

    /// <summary>SendAsync 的最终返回：最终助手文本 + 步数 + 全过程追踪 + 本轮写入/执行的文件清单</summary>
    public sealed class AgentResult
    {
        public string Text = "";
        public int Steps;
        public List<AgentStep> Trace = new List<AgentStep>();

        /// <summary>本轮模型通过 write_file/create_file 写入的文件（绝对路径，按首次出现顺序）</summary>
        public List<string> WrittenFiles = new List<string>();

        /// <summary>本轮模型通过 run_command/execute_script 执行或打开的文件（绝对路径）</summary>
        public List<string> ExecutedFiles = new List<string>();
    }
}