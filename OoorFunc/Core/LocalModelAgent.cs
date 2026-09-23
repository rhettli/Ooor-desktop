using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace OoorFunc.Core
{
    /// <summary>
    /// 本地模型 Agent：把 llama-server 的 OpenAI 兼容 /v1/chat/completions 包成一个
    /// "用户消息 → 模型可能调用工具 → 把工具结果回灌 → 再问 → 直到模型给出最终答复" 的循环。
    ///
    /// 不直接管理 llama-server 进程；调用方须先确保服务在跑（看 ServerManager.Server.IsRunning / LastRunState）。
    /// 多线程调用 SendAsync 由内部 SemaphoreSlim 串行化，避免 history 被并发改乱。
    ///
    /// 安全：默认只注册 list_directory / read_file / search_files；写/删/命令类受 Options.AllowWrite/AllowCommand 控制，
    /// 启用后仍由 Confirm 回调二次拦截（默认弹 MessageBox YesNo）。
    /// </summary>
    public sealed class LocalModelAgent : IDisposable
    {
        private static readonly JavaScriptSerializer _json = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 256
        };

        private readonly HttpClient _http;
        private readonly AgentToolRegistry _registry;
        private readonly AgentConfirm _confirm;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly List<AgentMessage> _history = new List<AgentMessage>();
        private string _model;

        public string BaseUrl { get; private set; }
        public AgentOptions Options { get; private set; }

        /// <summary>高危工具执行前的 UI 确认回调（可被 AgentHost.SetConfirm 替换）</summary>
        public AgentConfirm Confirm
        {
            get { return _confirm; }
        }

        /// <summary>对话历史条数（系统提示 + user + assistant + tool …）</summary>
        public int HistoryCount { get { lock (_history) { return _history.Count; } } }

        /// <summary>当前是否正在执行 SendAsync</summary>
        public bool IsBusy { get { return _gate.CurrentCount == 0; } }

        /// <summary>当前已注册的工具名（受 AllowWrite / AllowCommand 影响，UI 展示「AI 能做什么」用）</summary>
        public string[] ToolNames { get { return _registry.Names; } }

        /// <summary>本轮文件动作记录（每轮 SendAsync 开始时自动 Reset）</summary>
        public AgentTurnLog TurnLog { get { return _registry.TurnLog; } }

        /// <summary>
        /// 单步事件：每条 assistant 文本与每个 tool 调用结果都触发一次，回调在线程池线程上。
        /// UI 订阅时需自行 Control.BeginInvoke。
        /// </summary>
        public event EventHandler<AgentStep> Step;

        public LocalModelAgent(string baseUrl, AgentOptions options, AgentConfirm confirm = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl 不能为空", "baseUrl");

            BaseUrl = baseUrl.TrimEnd('/');
            Options = options ?? AgentOptions.Default();
            _confirm = confirm ?? AgentConfirmDefaults.MessageBoxYesNo;
            _registry = new AgentToolRegistry(Options, _confirm);

            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl + "/"),
                // 流式生成可能持续很久（思考型模型尤甚），不设总超时，由 CancellationToken 与 llama-server 控制
                Timeout = Timeout.InfiniteTimeSpan
            };
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        public void ResetHistory()
        {
            lock (_history) { _history.Clear(); }
        }

        // ==================== 历史持久化（供会话保存 / 恢复「接着聊」） ====================

        /// <summary>导出当前对话历史为 JSON（含系统提示词，保证「接着聊」时完整恢复）。</summary>
        public string ExportHistoryJson()
        {
            HistoryMsgDto[] dto;
            lock (_history)
            {
                var list = new List<HistoryMsgDto>(_history.Count);
                foreach (var m in _history)
                {
                    var d = new HistoryMsgDto
                    {
                        role = m.Role.ToString().ToLowerInvariant(),
                        content = m.Content ?? "",
                        tool_call_id = m.ToolCallId ?? "",
                        tool_name = m.ToolName ?? ""
                    };
                    if (m.ToolCalls != null && m.ToolCalls.Count > 0)
                    {
                        d.tool_calls = new List<HistoryToolCallDto>(m.ToolCalls.Count);
                        foreach (var t in m.ToolCalls)
                            d.tool_calls.Add(new HistoryToolCallDto
                            {
                                id = t.Id ?? "",
                                name = t.Name ?? "",
                                arguments = t.ArgumentsJson ?? ""
                            });
                    }
                    list.Add(d);
                }
                dto = list.ToArray();
            }
            return new JavaScriptSerializer().Serialize(dto);
        }

        /// <summary>
        /// 从 JSON 恢复对话历史（先清空现有历史），系统提示词一并恢复。
        /// 恢复的 system 消息会同步到 Options.SystemPrompt，保持「首条系统提示」与历史一致；
        /// JSON 中无 system 时保留宿主当前 Options.SystemPrompt（首次 SendAsync 时自动加为首条）。
        /// </summary>
        public void ImportHistoryJson(string json)
        {
            lock (_history)
            {
                _history.Clear();
                if (string.IsNullOrWhiteSpace(json)) return;
                HistoryMsgDto[] dto;
                try { dto = new JavaScriptSerializer().Deserialize<HistoryMsgDto[]>(json); }
                catch { return; }
                if (dto == null) return;
                string restoredSystem = null;
                foreach (var d in dto)
                {
                    if (d == null || string.IsNullOrEmpty(d.role)) continue;
                    AgentRole role;
                    switch (d.role.ToLowerInvariant())
                    {
                        case "system": role = AgentRole.System; restoredSystem = d.content ?? ""; break;
                        case "user": role = AgentRole.User; break;
                        case "assistant": role = AgentRole.Assistant; break;
                        case "tool": role = AgentRole.Tool; break;
                        default: continue;   // 未知角色忽略
                    }
                    var msg = new AgentMessage
                    {
                        Role = role,
                        Content = d.content ?? "",
                        ToolCallId = d.tool_call_id ?? "",
                        ToolName = d.tool_name ?? ""
                    };
                    if (d.tool_calls != null)
                    {
                        msg.ToolCalls = new List<AgentToolCall>(d.tool_calls.Count);
                        foreach (var t in d.tool_calls)
                        {
                            if (t == null) continue;
                            msg.ToolCalls.Add(new AgentToolCall
                            {
                                Id = t.id ?? "",
                                Name = t.name ?? "",
                                ArgumentsJson = t.arguments ?? ""
                            });
                        }
                    }
                    _history.Add(msg);
                }
                // 系统提示词以历史中恢复的为准（与发送给模型的首条消息保持一致）
                if (restoredSystem != null)
                {
                    try { Options.SystemPrompt = restoredSystem; } catch { }
                }
            }
        }

        /// <summary>获取当前对话历史的快照副本（含 system / user / assistant / tool 全部消息），供 UI 回放展示。</summary>
        public AgentMessage[] SnapshotHistory()
        {
            lock (_history) { return _history.ToArray(); }
        }

        /// <summary>
        /// 切换 Agent 人设：替换历史中的 system 消息（没有则插到最前），并同步 Options.SystemPrompt。
        /// 对话其余历史保留，实现「带着当前聊天记录切换到另一个 Agent 继续聊」。
        /// </summary>
        public void ReplaceSystemPrompt(string systemPrompt)
        {
            string sp = systemPrompt ?? "";
            lock (_history)
            {
                AgentMessage sys = null;
                for (int i = 0; i < _history.Count; i++)
                    if (_history[i].Role == AgentRole.System) { sys = _history[i]; break; }
                if (sys != null) sys.Content = sp;
                else _history.Insert(0, new AgentMessage { Role = AgentRole.System, Content = sp });
            }
            try { Options.SystemPrompt = sp; } catch { }
        }

        // 历史序列化 DTO（字段名与 OpenAI 消息风格一致，JSON 可读）
        private sealed class HistoryMsgDto
        {
            public string role;
            public string content;
            public string tool_call_id;
            public string tool_name;
            public List<HistoryToolCallDto> tool_calls;
        }
        private sealed class HistoryToolCallDto
        {
            public string id;
            public string name;
            public string arguments;
        }

        /// <summary>
        /// 替换底层 BaseUrl（适合「启动服务」后端口变化时 AgentHost.Rebind 调过来）。
        /// 不重置 history 与 _model（_model 名称一般不变）。
        /// </summary>
        public void RebindBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl 不能为空", "baseUrl");
            BaseUrl = baseUrl.TrimEnd('/');
            try { _http.BaseAddress = new Uri(BaseUrl + "/"); }
            catch { /* 失败时下一次 PostAsync 会抛，由调用方处理 */ }
        }

        public async Task<AgentResult> SendAsync(string userInput, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userInput)) userInput = "（空消息）";

            await _gate.WaitAsync(ct);
            var result = new AgentResult();
            try
            {
                // 新一轮：清空上一轮的文件动作记录
                _registry.TurnLog.Reset();

                await EnsureModelAsync(ct);

                lock (_history)
                {
                    if (_history.Count == 0 && !string.IsNullOrEmpty(Options.SystemPrompt))
                        _history.Add(new AgentMessage { Role = AgentRole.System, Content = Options.SystemPrompt });
                    _history.Add(new AgentMessage { Role = AgentRole.User, Content = userInput });
                }

                for (int step = 0; step < Options.MaxSteps; step++)
                {
                    ct.ThrowIfCancellationRequested();

                    object req;
                    lock (_history) { req = BuildRequestLocked(); }

                    ChatTurnResult turn;
                    try
                    {
                        // 流式：思考过程与正文逐块收到、逐块推给 UI（见 FireDelta）
                        turn = await PostChatStreamAsync(req, ct);
                    }
                    catch (TaskCanceledException) { throw; }
                    catch (InvalidOperationException) { throw; }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("调用 llama-server 失败：" + ex.Message, ex);
                    }

                    string contentStr = turn.Content.TrimStart();
                    string reasoningStr = (turn.Reasoning ?? "").Trim();
                    List<AgentToolCall> calls = turn.Calls;

                    // assistant 消息写回历史
                    lock (_history)
                    {
                        _history.Add(new AgentMessage { Role = AgentRole.Assistant, Content = contentStr, ToolCalls = calls });
                    }

                    // 触发事件
                    var stepEvt = new AgentStep { At = DateTime.Now, Role = AgentRole.Assistant, Text = contentStr, Reasoning = reasoningStr };
                    result.Trace.Add(stepEvt);
                    result.Steps++;
                    try { Step?.Invoke(this, stepEvt); } catch { /* 订阅方异常不影响主循环 */ }

                    // 没有 tool_calls → 终态
                    if (calls == null || calls.Count == 0)
                    {
                        result.Text = contentStr ?? "";
                        if (result.Text.Length == 0 && reasoningStr.Length == 0)
                            result.Text = "（模型本轮没有返回任何内容，可能是服务正在加载或流式响应异常，请重试；必要时查看 ooor 日志区 llama-server 输出）";
                        return result;
                    }

                    // 执行每个 tool_call
                    foreach (AgentToolCall c in calls)
                    {
                        ct.ThrowIfCancellationRequested();

                        IDictionary<string, object> parsedArgs;
                        AgentToolResult r;
                        try
                        {
                            if (string.IsNullOrEmpty(c.ArgumentsJson) || c.ArgumentsJson == "null")
                                parsedArgs = new Dictionary<string, object>();
                            else
                                parsedArgs = _json.DeserializeObject(c.ArgumentsJson) as IDictionary<string, object>
                                              ?? new Dictionary<string, object>();
                        }
                        catch (Exception ex)
                        {
                            r = AgentToolResult.Err("参数 JSON 解析失败：" + ex.Message);
                            EmitTool(result, c.Name, c.ArgumentsJson, r);
                            lock (_history) { _history.Add(new AgentMessage { Role = AgentRole.Tool, ToolCallId = c.Id, ToolName = c.Name, Content = r.Text }); }
                            continue;
                        }

                        if (!_registry.TryGet(c.Name, out IAgentTool tool))
                        {
                            r = AgentToolResult.Err("未知工具：" + c.Name);
                        }
                        else
                        {
                            try { r = tool.Execute(parsedArgs); }
                            catch (Exception ex) { r = AgentToolResult.Err("工具执行异常：" + ex.Message); }
                        }

                        EmitTool(result, c.Name, c.ArgumentsJson, r);
                        lock (_history)
                        {
                            _history.Add(new AgentMessage { Role = AgentRole.Tool, ToolCallId = c.Id, ToolName = c.Name, Content = r.Text });
                        }
                    }
                }

                // 达到 MaxSteps：返回最后一条 assistant 的文本作为兜底
                AgentMessage[] snap;
                lock (_history) { snap = _history.ToArray(); }
                for (int i = snap.Length - 1; i >= 0; i--)
                {
                    if (snap[i].Role == AgentRole.Assistant) { result.Text = snap[i].Content ?? ""; break; }
                }
                if (result.Text.Length == 0) result.Text = "（达到 MaxSteps=" + Options.MaxSteps + " 仍未结束对话）";
                return result;
            }
            finally
            {
                // 无论正常结束 / MaxSteps / 异常，都把本轮文件动作汇总进结果
                result.WrittenFiles.AddRange(_registry.TurnLog.WrittenFiles);
                result.ExecutedFiles.AddRange(_registry.TurnLog.ExecutedFiles);
                _gate.Release();
            }
        }

        /// <summary>首次对话前 GET /v1/models，取第一个 id 缓存为 _model。</summary>
        private async Task EnsureModelAsync(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_model)) return;

            HttpResponseMessage resp;
            try { resp = await _http.GetAsync("v1/models", ct); }
            catch (Exception ex) { throw new InvalidOperationException("获取模型列表失败：" + ex.Message, ex); }

            string body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException("获取模型列表返回 " + (int)resp.StatusCode + "：" + Truncate(body, 300));

            IDictionary<string, object> parsed;
            try { parsed = (IDictionary<string, object>)_json.DeserializeObject(body); }
            catch (Exception ex) { throw new InvalidOperationException("模型列表解析失败：" + ex.Message, ex); }

            IList data = parsed["data"] as IList;
            if (data == null || data.Count == 0)
                throw new InvalidOperationException("llama-server 未返回任何模型，请确认服务已加载模型");

            IDictionary<string, object> first = data[0] as IDictionary<string, object>;
            _model = first != null && first.TryGetValue("id", out object id) ? id?.ToString() : "";
            if (string.IsNullOrEmpty(_model))
                throw new InvalidOperationException("模型列表项缺少 id 字段");
        }

        /// <summary>构造一次 /v1/chat/completions 请求体（调用前须持有 _history 锁）</summary>
        private object BuildRequestLocked()
        {
            var msgs = new List<object>();
            foreach (AgentMessage m in _history)
            {
                var d = new Dictionary<string, object> { { "role", RoleToStr(m.Role) } };
                if (m.Role == AgentRole.Tool)
                {
                    d["tool_call_id"] = m.ToolCallId ?? "";
                    d["content"] = m.Content ?? "";
                }
                else if (m.Role == AgentRole.Assistant)
                {
                    d["content"] = m.Content ?? "";
                    if (m.ToolCalls != null && m.ToolCalls.Count > 0)
                    {
                        var tcs = new List<object>();
                        foreach (AgentToolCall c in m.ToolCalls)
                        {
                            tcs.Add(new Dictionary<string, object>
                            {
                                { "id", c.Id ?? "" },
                                { "type", "function" },
                                { "function", new Dictionary<string, object>
                                    {
                                        { "name", c.Name ?? "" },
                                        { "arguments", c.ArgumentsJson ?? "" }
                                    }
                                }
                            });
                        }
                        d["tool_calls"] = tcs;
                    }
                }
                else
                {
                    d["content"] = m.Content ?? "";
                }
                msgs.Add(d);
            }

            return new Dictionary<string, object>
            {
                { "model", _model ?? "" },
                { "messages", msgs },
                { "tools", _registry.BuildOpenAIToolsArray() },
                { "tool_choice", "auto" },
                { "stream", true },
                { "temperature", 0.2 }
            };
        }

        private sealed class ChatTurnResult
        {
            public string Content = "";
            public string Reasoning = "";
            public List<AgentToolCall> Calls;
        }

        /// <summary>
        /// 流式调用 /v1/chat/completions（SSE）：
        ///   - delta.reasoning_content → Reasoning（llama-server reasoning_format=auto 的思考过程）
        ///   - delta.content → 若含 &lt;think&gt;…&lt;/think&gt;（未开启拆分时）由 ThinkSplitter 路由到 Reasoning，否则进 Content
        ///   - delta.tool_calls → 按 index 增量拼装出完整工具调用
        /// 每一块增量都通过 FireDelta 实时推给 UI 订阅者。
        /// </summary>
        private async Task<ChatTurnResult> PostChatStreamAsync(object req, CancellationToken ct)
        {
            var httpReq = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
            {
                Content = new StringContent(_json.Serialize(req), new UTF8Encoding(false), "application/json")
            };

            using (HttpResponseMessage resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                if (!resp.IsSuccessStatusCode)
                {
                    string errBody = "";
                    try { errBody = await resp.Content.ReadAsStringAsync(); } catch { }
                    throw new InvalidOperationException("llama-server 返回 " + (int)resp.StatusCode + " " + resp.ReasonPhrase + "：" + Truncate(errBody, 500));
                }

                var result = new ChatTurnResult();
                var callsByIndex = new SortedDictionary<int, AgentToolCall>();
                var splitter = new ThinkSplitter();

                Action<AgentStreamKind, string> onPiece = delegate (AgentStreamKind kind, string piece)
                {
                    if (piece == null || piece.Length == 0) return;
                    if (kind == AgentStreamKind.Reasoning) result.Reasoning += piece;
                    else result.Content += piece;
                    FireDelta(kind, piece);
                };

                using (Stream net = await resp.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(net, new UTF8Encoding(false)))
                {
                    string line;
                    // Ctrl+C 取消时 HttpClient 会 Abort 连接，阻塞中的同步 ReadLine 可能抛
                    // IOException/ObjectDisposedException；统一转成 TaskCanceledException，
                    // 让上层显示"已中断"而非"出错了"，同时 using 会 Dispose 响应确保连接彻底关闭，
                    // llama-server 检测到断连即停止生成（释放 CPU）
                    try
                    {
                    while (!ct.IsCancellationRequested && (line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0 || !line.StartsWith("data:", StringComparison.Ordinal)) continue;

                        string payload = line.Substring(5).Trim();
                        if (payload == "[DONE]") break;

                        IDictionary<string, object> obj;
                        try { obj = _json.DeserializeObject(payload) as IDictionary<string, object>; }
                        catch { continue; }
                        if (obj == null) continue;

                        IList choices = obj["choices"] as IList;
                        if (choices == null || choices.Count == 0) continue;

                        // choices[0] 是 choice 对象（{"index":0,"delta":{...}}），
                        // 真正的增量字段在其 delta 里；非流式兜底 message；都拿不到才退回 choice 本身
                        IDictionary<string, object> choice = choices[0] as IDictionary<string, object>;
                        if (choice == null) continue;

                        IDictionary<string, object> delta = null;
                        if (choice.TryGetValue("delta", out object dObj))
                            delta = dObj as IDictionary<string, object>;
                        if (delta == null && choice.TryGetValue("message", out object mObj))
                            delta = mObj as IDictionary<string, object>;
                        if (delta == null)
                            delta = choice;

                        if (delta.TryGetValue("reasoning_content", out object rc) && rc != null)
                            onPiece(AgentStreamKind.Reasoning, rc.ToString());

                        if (delta.TryGetValue("content", out object cc) && cc != null)
                            splitter.Feed(cc.ToString(), onPiece);

                        if (delta.TryGetValue("tool_calls", out object rawTcs) && rawTcs is IList tl)
                        {
                            foreach (object it in tl)
                            {
                                if (!(it is IDictionary<string, object> td)) continue;
                                int idx = td.TryGetValue("index", out object ri) ? Convert.ToInt32(ri) : callsByIndex.Count;
                                if (!callsByIndex.TryGetValue(idx, out AgentToolCall call))
                                    callsByIndex[idx] = call = new AgentToolCall();

                                if (td.TryGetValue("id", out object rid) && rid != null && rid.ToString().Length > 0)
                                    call.Id = rid.ToString();
                                if (td.TryGetValue("function", out object rf) && rf is IDictionary<string, object> fn)
                                {
                                    // 注意：name / arguments 都是分片增量，必须拼接（name 被 llama.cpp 逐 token 流出时
                                    // 会被切成 "get_app" + "_info" 等碎片，用 = 覆盖会只剩最后一个碎片 → 未知工具）
                                    if (fn.TryGetValue("name", out object n) && n != null)
                                        call.Name += n.ToString();
                                    if (fn.TryGetValue("arguments", out object a) && a != null)
                                        call.ArgumentsJson += a.ToString();
                                }
                            }
                        }
                    }
                    }
                    catch (Exception) when (ct.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                }

                ct.ThrowIfCancellationRequested();
                splitter.Flush(onPiece);

                if (callsByIndex.Count > 0)
                {
                    var calls = new List<AgentToolCall>();
                    foreach (AgentToolCall c in callsByIndex.Values)
                    {
                        if (!string.IsNullOrEmpty(c.Name)) calls.Add(c);   // 丢弃空名残片
                    }
                    if (calls.Count > 0) result.Calls = calls;
                }
                return result;
            }
        }

        /// <summary>把一块流式增量推给 Step 订阅者（Stream != None 表示增量，而非完整步）</summary>
        private void FireDelta(AgentStreamKind kind, string piece)
        {
            if (string.IsNullOrEmpty(piece)) return;
            var s = new AgentStep { At = DateTime.Now, Role = AgentRole.Assistant, Stream = kind, Delta = piece };
            try { Step?.Invoke(this, s); } catch { /* 订阅方异常不影响主循环 */ }
        }

        /// <summary>
        /// 流式 &lt;think&gt; 拆分器：把 content 流里的 &lt;think&gt;…&lt;/think&gt; 段（可跨块、可为未闭合）路由到 Reasoning，
        /// 其余路由到 Content。reasoning_format=auto 时 content 本就不含 think 标签，此路直通无损耗。
        /// </summary>
        private sealed class ThinkSplitter
        {
            private bool _inThink;
            private string _pend = "";

            public void Feed(string chunk, Action<AgentStreamKind, string> emit)
            {
                string buf = _pend + chunk;
                _pend = "";
                int pos = 0;
                while (pos < buf.Length)
                {
                    if (_inThink)
                    {
                        int close = buf.IndexOf("</think>", pos, StringComparison.OrdinalIgnoreCase);
                        if (close < 0)
                        {
                            int keep = Math.Min(8, buf.Length - pos);   // 留住可能被切断的 "</think>"
                            Emit(AgentStreamKind.Reasoning, buf, pos, buf.Length - pos - keep, emit);
                            _pend = buf.Substring(buf.Length - keep);
                            return;
                        }
                        Emit(AgentStreamKind.Reasoning, buf, pos, close - pos, emit);
                        _inThink = false;
                        pos = close + 8;
                    }
                    else
                    {
                        int open = buf.IndexOf("<think>", pos, StringComparison.OrdinalIgnoreCase);
                        if (open < 0)
                        {
                            int keep = Math.Min(7, buf.Length - pos);   // 留住可能被切断的 "<think>"
                            Emit(AgentStreamKind.Content, buf, pos, buf.Length - pos - keep, emit);
                            _pend = buf.Substring(buf.Length - keep);
                            return;
                        }
                        Emit(AgentStreamKind.Content, buf, pos, open - pos, emit);
                        _inThink = true;
                        pos = open + 7;
                    }
                }
            }

            public void Flush(Action<AgentStreamKind, string> emit)
            {
                if (_pend.Length == 0) return;
                Emit(_inThink ? AgentStreamKind.Reasoning : AgentStreamKind.Content, _pend, 0, _pend.Length, emit);
                _pend = "";
            }

            private static void Emit(AgentStreamKind kind, string buf, int pos, int len, Action<AgentStreamKind, string> emit)
            {
                if (len <= 0) return;
                emit(kind, buf.Substring(pos, len));
            }
        }

        private void EmitTool(AgentResult result, string name, string args, AgentToolResult r)
        {
            var s = new AgentStep
            {
                At = DateTime.Now,
                Role = AgentRole.Tool,
                ToolName = name ?? "",
                ToolArgs = args ?? "",
                ToolResult = r.Text ?? "",
                ToolOk = r.Success
            };
            result.Trace.Add(s);
            try { Step?.Invoke(this, s); } catch { /* 订阅方异常不影响主循环 */ }
        }

        private static string RoleToStr(AgentRole r)
        {
            switch (r)
            {
                case AgentRole.System: return "system";
                case AgentRole.User: return "user";
                case AgentRole.Assistant: return "assistant";
                case AgentRole.Tool: return "tool";
            }
            return "user";
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        public void Dispose()
        {
            try { _http.Dispose(); } catch { }
            try { _gate.Dispose(); } catch { }
        }
    }
}