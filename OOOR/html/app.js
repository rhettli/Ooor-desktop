/* ooor AI 助手 —— Vue 2 页面侧（与 C# AgentChatForm 通过 WebView2 消息桥通信）
 *
 *   页面 → C#：window.chrome.webview.postMessage({ a: '动作', ... })
 *    C# → 页面：window.chrome.webview 'message' 事件（e.data 已是对象）
 *
 * 分工：
 *   - 模型调用全在页面：fetch llama-server 的 /v1/chat/completions（SSE 流式），
 *     好处是 DevTools 的「网络」面板能看到每个请求头/请求体/流式响应，排查问题不依赖 C# 日志；
 *   - 工具执行在 C#：页面把 tool_calls 发 {a:'tool', id, name, args}，
 *     C# 用沙盒 + 原生审批执行后回 {t:'toolResult', id, ok, text}；
 *   - 系统提示 / 工具 schema 由 C# 下发（{a:'cfg'} → {t:'cfg'}）。
 *
 * 页面整体由一个 Vue 实例驱动（DOM 内模板，见 index.html）。
 */
(function () {
  'use strict';

  var host = window.chrome && window.chrome.webview;

  // ==================== 文本 / Markdown ====================

  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  /** 极简 markdown：代码块 / 行内代码 / 粗体 / 标题 / 列表（先转义，安全） */
  function renderMd(src) {
    var blocks = [];
    var s = esc(src);
    s = s.replace(/```[a-zA-Z0-9_+-]*\n?([\s\S]*?)```/g, function (m, code) {
      blocks.push(code.replace(/\n$/, ''));
      return '\u0000' + (blocks.length - 1) + '\u0000';
    });
    s = s.replace(/`([^`\n]+)`/g, '<code>$1</code>');
    s = s.replace(/\*\*([^*\n]+)\*\*/g, '<strong>$1</strong>');
    s = s.replace(/^#{3,4}\s?(.+)$/gm, '<h4>$1</h4>');
    s = s.replace(/^##\s?(.+)$/gm, '<h3>$1</h3>');
    s = s.replace(/^\s*[-*]\s+(.+)$/gm, '<li>$1</li>');
    s = s.replace(/(?:<li>[\s\S]*?<\/li>\n?)+/g, function (m) { return '<ul>' + m.replace(/\n/g, '') + '</ul>'; });
    s = s.replace(/\n/g, '<br>');
    s = s.replace(/\u0000(\d+)\u0000/g, function (m, i) { return '<pre>' + blocks[+i] + '</pre>'; });
    return s;
  }

  /**
   * 流式 <think> 拆分：把 content 流里的 <think>…</think>（可跨块、可未闭合）路由到思考过程，
   * 其余进正文。服务端已用 reasoning_format 拆分过时，content 里没有标签，此路直通。
   */
  function makeThinkSplitter(onContent, onReasoning) {
    var inThink = false;
    var pend = '';

    function emit(reasoning, s) {
      if (!s) return;
      if (reasoning) onReasoning(s); else onContent(s);
    }

    return {
      feed: function (chunk) {
        var buf = pend + chunk;
        pend = '';
        var lower = buf.toLowerCase();
        var pos = 0;
        while (pos < buf.length) {
          if (inThink) {
            var close = lower.indexOf('</think>', pos);
            if (close < 0) {
              var keep = Math.min(8, buf.length - pos);      // 留住可能被切断的 "</think>"
              emit(true, buf.substr(pos, buf.length - pos - keep));
              pend = buf.substr(buf.length - keep);
              return;
            }
            emit(true, buf.substring(pos, close));
            inThink = false;
            pos = close + 8;
          } else {
            var open = lower.indexOf('<think>', pos);
            if (open < 0) {
              var keep2 = Math.min(7, buf.length - pos);     // 留住可能被切断的 "<think>"
              emit(false, buf.substr(pos, buf.length - pos - keep2));
              pend = buf.substr(buf.length - keep2);
              return;
            }
            emit(false, buf.substring(pos, open));
            inThink = true;
            pos = open + 7;
          }
        }
      },
      flush: function () {
        if (!pend) return;
        emit(inThink, pend);
        pend = '';
      }
    };
  }

  function nowTime() {
    var d = new Date();
    function p(n) { return (n < 10 ? '0' : '') + n; }
    return p(d.getHours()) + ':' + p(d.getMinutes()) + ':' + p(d.getSeconds());
  }

  // ==================== Vue 实例 ====================

  new Vue({
    el: '#app',

    data: {
      // —— 来自 C# 的状态 ——
      version: '-',
      running: false,
      baseUrl: '',
      roots: [],
      tools: [],

      // —— 工具开关（v-model 双向绑定）——
      allowWrite: false,
      allowCmd: false,
      allowInternet: false,
      trustAi: false,

      // —— 对话 ——
      msgs: [],
      busy: false,
      input: '',

      /** 流式输出中的气泡（思考过程 + 正文逐字上屏），一轮结束落账为正式消息 */
      stream: null,

      /** 交给模型的历史（OpenAI messages，思考过程不回灌） */
      history: [],

      /** C# 下发的 Agent 配置：{ system, tools, maxSteps, temperature } */
      cfg: null,

      /** llama-server 的模型 id（首次对话前从 /v1/models 取） */
      model: '',

      prompts: [
        '现在几点了？',
        '看看程序目录的结构',
        '列出下载目录里的文件',
        '联网搜一下今天的新闻',
        '写个 py 脚本算 1 到 100 的和并执行'
      ]
    },

    computed: {
      vueVersion: function () {
        return (window.Vue && window.Vue.version) || '?';
      },
      trustHint: function () {
        return this.trustAi
          ? '已开启：写 / 删 / 执行类操作若模型传 confirm=false 将不再弹窗（风险自负）。'
          : '未开启时：写 / 删 / 执行一律弹窗，由你点「是」才执行。';
      },
      showChips: function () {
        return !this.busy && this.msgs.length <= 1;
      },
      showThinking: function () {
        // 已有流式内容上屏时，不再显示「正在思考…」占位
        return this.busy && !(this.stream && (this.stream.reasoning || this.stream.text));
      }
    },

    created: function () {
      this._waiters = {};
      this._abort = null;
      this.pushMsg({
        role: 'system',
        text: '本地模型 Agent：模型可调用工具真实读写沙盒内文件（目录 / 读取 / 搜索 / 时间 / 环境 / 程序结构 / 创建 / 删除 / 执行脚本），开启「允许联网」后还可联网搜索与抓取网页。'
      });
    },

    mounted: function () {
      if (host) {
        host.addEventListener('message', this.onHostMessage);
        this.post({ a: 'ready' });
      } else {
        this.pushMsg({ role: 'error', text: '未检测到 WebView2 消息桥：请在本程序内的窗口中打开本页面。' });
      }
      this._timer = setInterval(this.checkServer, 5000);
      this.autoGrow();
    },

    methods: {
      // ==================== 与 C# 通信 ====================

      post: function (msg) {
        if (host) host.postMessage(msg);
      },

      /** 发一条消息并等 C# 回指定类型（key 为空表示不等回复） */
      waitFor: function (key, payload, timeoutMs) {
        var self = this;
        return new Promise(function (resolve, reject) {
          var timer = setTimeout(function () {
            delete self._waiters[key];
            reject(new Error('等待 C# 回复 ' + key + ' 超时'));
          }, timeoutMs || 10000);
          self._waiters[key] = {
            resolve: function (m) { clearTimeout(timer); resolve(m); }
          };
          if (payload) self.post(payload);
        });
      },

      onHostMessage: function (e) {
        var m = e.data;
        if (!m || !m.t) return;

        var key = m.t === 'toolResult' ? ('toolResult:' + m.id) : m.t;
        var w = this._waiters[key];
        if (w) { delete this._waiters[key]; w.resolve(m); }

        switch (m.t) {
          case 'state': this.applyState(m); break;
          case 'cfg': this.applyCfg(m); break;
          case 'toolResult': break;      // 由 callTool 的 waitFor 消费
          case 'msg':
            this.pushMsg({ role: m.role || 'system', text: m.text || '' });
            break;
          case 'clear': this.resetChat(); break;
        }
      },

      applyState: function (s) {
        this.version = s.version || '-';
        this.running = !!s.running;
        this.baseUrl = s.baseUrl || '';
        this.roots = (s.roots || []).filter(function (r) { return r; });
        this.tools = (s.tools || '').split(',').map(function (t) { return t.trim(); }).filter(Boolean);
        this.allowWrite = !!s.allowWrite;
        this.allowCmd = !!s.allowCmd;
        this.allowInternet = !!s.allowInternet;
        this.trustAi = !!s.trustAi;
      },

      /** llama-server 基地址（去掉尾斜杠） */
      base: function () {
        return (this.baseUrl || '').replace(/\/+$/, '');
      },

      // ==================== 对话主流程（页面侧 Agent 循环） ====================

      /** Enter 发送：输入法组词中的回车（isComposing / 229）不拦截，交给 IME 确认候选词 */
      onEnter: function (e) {
        if (!e) return;
        if (e.isComposing || e.keyCode === 229) return;
        if (e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
        e.preventDefault();
        this.send();
      },

      send: function () {
        var self = this;
        var text = (this.input || '').trim();
        if (!text || this.busy) return;

        if (!host) { this.pushMsg({ role: 'error', text: '未检测到 WebView2 消息桥，无法执行工具。' }); return; }
        if (!this.running) { this.pushMsg({ role: 'error', text: '本地服务未运行：请先回到主窗口点「启动服务」。' }); return; }
        if (!this.base()) { this.pushMsg({ role: 'error', text: '未取到服务地址：请点「检测服务」后重试。' }); return; }

        this.input = '';
        this.autoGrow();
        this.busy = true;
        this._abort = new AbortController();
        this.pushMsg({ role: 'user', text: text });
        this.history.push({ role: 'user', content: text });

        var run = (this.cfg ? Promise.resolve() : this.loadCfg())
          .then(function () { return self.ensureModel(); })
          .then(function () { return self.runLoop(); });

        run.catch(function (e) {
          if (e && e.name === 'AbortError') {
            self.pushMsg({ role: 'system', text: '已取消本次请求。' });
          } else {
            self.pushMsg({ role: 'error', text: '出错了：' + ((e && e.message) || e) });
          }
        }).then(function () {
          self.busy = false;
          self._abort = null;
          self.settleStream(false);   // 中途出错/取消时，已收到的内容落账
        });
      },

      cancel: function () {
        if (this._abort) {
          try { this._abort.abort(); } catch (e) { /* ignore */ }
        }
      },

      applyCfg: function (m) {
        this.cfg = {
          system: m.system || '',
          tools: m.tools || [],
          maxSteps: m.maxSteps || 12,
          temperature: m.temperature || 0.2
        };
        if (m.baseUrl) this.baseUrl = m.baseUrl;
      },

      /** 向 C# 要配置：系统提示 + 工具 schema + 最大步数 */
      loadCfg: function () {
        var self = this;
        return this.waitFor('cfg', { a: 'cfg' }).then(function (m) {
          if (!self.cfg) self.applyCfg(m);   // onHostMessage 已处理过则跳过
        });
      },

      /** 首次对话前 GET /v1/models 拿模型 id */
      ensureModel: function () {
        var self = this;
        if (this.model) return Promise.resolve();
        return fetch(this.base() + '/v1/models', { signal: this._abort.signal })
          .then(function (res) {
            if (!res.ok) throw new Error('获取模型列表失败 HTTP ' + res.status);
            return res.json();
          })
          .then(function (j) {
            var id = j && j.data && j.data[0] && j.data[0].id;
            if (!id) throw new Error('llama-server 未返回任何模型，请确认服务已加载模型');
            self.model = id;
          });
      },

      /** 工具循环：问模型 → 执行工具 → 回灌结果 → 再问，直到模型给出最终答复 */
      runLoop: function () {
        var self = this;
        var maxSteps = (this.cfg && this.cfg.maxSteps) || 12;
        var step = 0;

        function next() {
          if (step++ >= maxSteps) {
            self.pushMsg({ role: 'error', text: '达到最大步数（' + maxSteps + '）仍未结束对话，已停止。' });
            return Promise.resolve();
          }

          var body = {
            model: self.model,
            messages: self.history,
            stream: true,
            temperature: (self.cfg && self.cfg.temperature) || 0.2
          };
          if (self.cfg && self.cfg.tools && self.cfg.tools.length) {
            body.tools = self.cfg.tools;
            body.tool_choice = 'auto';
          }

          return self.streamChat(body).then(function (out) {
            // 思考过程不回灌历史（省 token，也避免服务端重复拼接）
            var asst = { role: 'assistant', content: out.text || '' };
            if (out.calls.length) asst.tool_calls = out.calls;
            self.history.push(asst);

            self.stream = null;   // 本轮流式气泡落账
            self.pushMsg({ role: 'assistant', text: out.text, reasoning: out.reasoning });

            if (!out.calls.length) return undefined;   // 终态

            var chain = Promise.resolve();
            out.calls.forEach(function (c) {
              chain = chain.then(function () { return self.runTool(c); });
            });
            return chain.then(next);
          });
        }

        return next();
      },

      /** 执行一个工具调用：交给 C#（沙盒 + 审批），并把它渲染成工具卡片 */
      runTool: function (call) {
        var self = this;
        // OpenAI 格式：{ id, type, function: { name, arguments } }，name/arguments 在 function 里
        var fn = call.function || {};
        var name = fn.name || '';
        var args = fn.arguments || '';
        return this.waitFor('toolResult:' + call.id,
                            { a: 'tool', id: call.id, name: name, args: args },
                            5 * 60 * 1000)
          .then(function (r) {
            self.pushMsg({
              role: 'tool',
              tool: {
                name: name,
                args: args,
                result: r.text || '',
                ok: !!r.ok,
                at: r.at || nowTime()
              }
            });
            self.history.push({ role: 'tool', tool_call_id: call.id, content: r.text || '' });
          });
      },

      /**
       * 流式请求 /v1/chat/completions（SSE）：返回 { text, reasoning, calls }
       * 每块增量实时写进 this.stream，由模板逐字渲染。
       */
      streamChat: function (body) {
        var self = this;
        var out = { text: '', reasoning: '', calls: [] };
        var byIndex = {};

        var splitter = makeThinkSplitter(
          function (s) { out.text += s; self.appendStream('content', s); },
          function (s) { out.reasoning += s; self.appendStream('reasoning', s); }
        );

        return fetch(this.base() + '/v1/chat/completions', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
          signal: this._abort.signal
        }).then(function (res) {
          if (!res.ok) {
            return res.text().then(function (t) {
              throw new Error('llama-server HTTP ' + res.status + '：' + String(t || '').slice(0, 500));
            });
          }
          if (!res.body || !res.body.getReader) throw new Error('当前内核不支持流式读取响应体');

          var reader = res.body.getReader();
          var dec = new TextDecoder('utf-8');
          var buf = '';

          function handleLine(line) {
            line = (line || '').trim();
            if (!line || line.indexOf('data:') !== 0) return;
            var payload = line.slice(5).trim();
            if (!payload || payload === '[DONE]') return;

            var obj;
            try { obj = JSON.parse(payload); } catch (e) { return; }
            var ch = obj.choices && obj.choices[0];
            var d = ch && (ch.delta || ch.message);
            if (!d) return;

            if (d.reasoning_content) { out.reasoning += d.reasoning_content; self.appendStream('reasoning', d.reasoning_content); }
            if (d.content) splitter.feed(d.content);

            var tcs = d.tool_calls;
            if (tcs && tcs.length) {
              tcs.forEach(function (tc) {
                var idx = (tc.index === undefined || tc.index === null) ? out.calls.length : tc.index;
                var call = byIndex[idx];
                if (!call) {
                  call = { id: '', type: 'function', function: { name: '', arguments: '' } };
                  byIndex[idx] = call;
                  out.calls[idx] = call;
                }
                if (tc.id) call.id = tc.id;
                var fn = tc.function || {};
                // name / arguments 都是分片增量，必须拼接（= 覆盖会只剩最后一个碎片 → 未知工具）
                if (fn.name) call.function.name += fn.name;
                if (fn.arguments) call.function.arguments += fn.arguments;
              });
            }
          }

          function pump() {
            return reader.read().then(function (r) {
              if (r.done) {
                if (buf) handleLine(buf);
                splitter.flush();
                return;
              }
              buf += dec.decode(r.value, { stream: true });
              var lines = buf.split('\n');
              buf = lines.pop();
              for (var i = 0; i < lines.length; i++) handleLine(lines[i]);
              return pump();
            });
          }

          return pump().then(function () {
            out.calls = out.calls.filter(function (c) { return c && c.function && c.function.name; });
            return out;
          });
        });
      },

      // ==================== 消息 / 渲染 ====================

      /** 追加一块流式增量到「正在输出」气泡 */
      appendStream: function (kind, piece) {
        if (!piece) return;
        var s = this.stream || (this.stream = { reasoning: '', text: '' });
        if (kind === 'reasoning') s.reasoning += piece; else s.text += piece;
        this.$nextTick(this.pin);
      },

      /** 流式气泡收尾：drop=true 表示完整消息已落账，直接丢弃；否则把已有内容转为消息 */
      settleStream: function (drop) {
        var s = this.stream;
        this.stream = null;
        if (s && !drop && (s.reasoning || s.text)) {
          this.pushMsg({ role: 'assistant', text: s.text, reasoning: s.reasoning });
        }
      },

      /** 追加一条消息并渲染 markdown；滚动位置按“用户是否在底部”决定是否跟随 */
      pushMsg: function (m) {
        var chat = this.$refs.chat;
        var stick = !chat || (chat.scrollHeight - chat.scrollTop - chat.clientHeight < 90);
        if (m.role === 'tool') {
          m.tool.open = m.tool.ok === false || (m.tool.result || '').length < 500;
        } else if (m.text) {
          m.html = renderMd(m.text);
        }
        this.msgs.push(m);

        var self = this;
        this.$nextTick(function () {
          var el = self.$refs.chat;
          if (el && (stick || m.role === 'user')) el.scrollTop = el.scrollHeight;
        });
      },

      /** 流式上屏时跟随滚动（用户往上翻了就暂不跟随） */
      pin: function () {
        var el = this.$refs.chat;
        if (el && el.scrollHeight - el.scrollTop - el.clientHeight < 120) el.scrollTop = el.scrollHeight;
      },

      /** 供模板渲染流式正文 markdown */
      renderMd: function (src) { return renderMd(src); },

      resetChat: function () {
        this.busy = false;
        this.stream = null;
        this.history = [];
        this.msgs = [];
        this.pushMsg({
          role: 'system',
          text: '对话已重置。模型可调用工具真实读写沙盒内文件。'
        });
      },

      clearChat: function () { this.post({ a: 'clear' }); },

      checkServer: function () { this.post({ a: 'checkServer' }); },

      usePrompt: function (p) {
        this.input = p;
        this.autoGrow();
        var t = this.$refs.input;
        if (t) t.focus();
      },

      autoGrow: function () {
        var t = this.$refs.input;
        if (!t) return;
        t.style.height = 'auto';
        t.style.height = Math.min(t.scrollHeight, 168) + 'px';
      },

      // ==================== 设置 ====================

      applyOptions: function () {
        this.post({
          a: 'options',
          allowWrite: this.allowWrite,
          allowCmd: this.allowCmd,
          allowInternet: this.allowInternet,
          trustAi: this.trustAi
        });
      },

      addRoot: function () { this.post({ a: 'addRoot' }); },

      removeRoot: function (r) { this.post({ a: 'removeRoot', path: r }); }
    }
  });
})();
