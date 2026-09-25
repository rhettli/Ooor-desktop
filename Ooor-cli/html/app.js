/* ooor AI 助手 · 网页控制台（Vue 2）
 *
 * 视觉与交互高仿 Windows 控制台：黑底、等宽字、块状光标、纯键盘；
 * 页面是一个「只追加的行终端」：lines 为已定稿的行，live 为流式中的当前行
 *（先逐字裸出，换行后整行按终端语义重新渲染，与原生 Ooor-cli 的策略一致）。
 *
 * 通信分工（与旧版气泡界面相同）：
 *   页面 → C#：window.chrome.webview.postMessage({ a: '动作', ... })
 *   C# → 页面：window.chrome.webview 'message' 事件
 *   - 模型调用在页面：直连 llama-server /v1/chat/completions（SSE 流式）
 *   - 工具执行在 C#：{a:'tool'} → {t:'toolResult'}（沙盒 + 原生 MessageBox 审批）
 */
(function () {
  'use strict';

  var host = window.chrome && window.chrome.webview;

  // ==================== 文本处理 ====================

  function esc(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  /** 转义正则元字符（把文件路径/文件名安全拼进 RegExp） */
  function escRe(s) {
    return String(s).replace(/[\\^$.*+?()[\]{}|]/g, '\\$&');
  }

  /** 行内样式（输入已先转义）：行内代码 → 粗体 → 斜体（允许同层嵌套后再套外层） */
  function inline(s) {
    s = s.replace(/`([^`\n]+)`/g, '<span class="icode">$1</span>');
    s = s.replace(/\*\*([^*\n]+)\*\*/g, '<b>$1</b>');
    s = s.replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<i>$2</i>');
    return s;
  }

  /**
   * 单行 Markdown → 终端行 HTML。顺序刻意与原生 CliUi 对齐：
   * 先判「整行斜体/加粗」（如 * 哈哈 **hello** 好的 *，避免行首 "* " 被列表抢走），
   * 再标题、列表、引用，最后普通段落。入参 t 必须已经过转义（文件链接替换也已完成）。
   */
  function renderStructured(t) {
    var m;
    // 整行加粗：** xxx **
    m = /^\*\*\s+([\s\S]*?)\s+\*\*$/.exec(t);
    if (m) return '<b>' + inline(m[1]) + '</b>';
    // 整行斜体：* xxx *
    m = /^\*\s+([\s\S]*?)\s+\*$/.exec(t);
    if (m) return '<i>' + inline(m[1]) + '</i>';
    // 标题
    m = /^(#{1,6})\s+(.+?)\s*#*$/.exec(t);
    if (m) return '<span class="heading">■ ' + inline(m[2]) + '</span>';
    // 无序列表
    m = /^\s*[-*]\s+(.+)$/.exec(t);
    if (m) return '<span class="deco-cyan">  • </span>' + inline(m[1]);
    // 有序列表：保留原序号
    m = /^\s*(\d+)\.\s+(.+)$/.exec(t);
    if (m) return '  <span class="deco-cyan">' + m[1] + '.</span> ' + inline(m[2]);
    // 引用
    m = /^\s*&gt;\s?(.*)$/.exec(t);
    if (m) return '<span class="quote">  │ </span>' + inline(m[1]);
    return inline(t);
  }

  /**
   * 流式 <think> 拆分：把 content 流里的 <think>…</think>（可跨块、可未闭合）路由到思考通道。
   * 服务端已用 reasoning_format 拆分过时 content 中没有标签，此路直通。
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
              var keep = Math.min(8, buf.length - pos);
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
              var keep2 = Math.min(7, buf.length - pos);
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

  // ==================== 斜杠命令表 ====================

  var COMMANDS = [
    { cmd: '/help',        hint: '查看全部命令' },
    { cmd: '/clear',       hint: '清空屏幕与对话历史' },
    { cmd: '/tools',       hint: '查看当前已启用的工具' },
    { cmd: '/roots',       hint: '查看沙盒白名单目录' },
    { cmd: '/root',        hint: '/root add [路径] | /root rm <序号>：管理白名单' },
    { cmd: '/option',      hint: '/option write|cmd|net|trust on|off：切换权限' },
    { cmd: '/file',        hint: '/file <路径>：在终端查看文件内容' },
    { cmd: '/new-console', hint: '再开一个控制台窗口' },
    { cmd: '/exit',        hint: '关闭窗口' }
  ];

  var MAX_LINES = 2000;

  // ==================== Vue 终端 ====================

  new Vue({
    el: '#term',

    data: {
      // —— C# 下发的状态 ——
      version: '-',
      running: false,
      baseUrl: '',
      modelId: '',
      roots: [],
      toolNames: '',
      allowWrite: false,
      allowCmd: false,
      allowInternet: false,
      trustAi: false,

      // —— 对话 ——
      history: [],        // OpenAI messages（思考过程不回灌）
      cfg: null,          // { system, tools, maxSteps, temperature }
      busy: false,

      // —— 终端 ——
      lines: [],          // 已定稿的行 { cls, html }
      live: [],           // 流式中的行 { kind, raw, cls, html, rendered }
      inFence: false,     // 代码围栏跨行状态

      // —— 模型创建的文件（write_file/create_file 成功后登记）——
      createdFiles: [],   // 整个会话累计（去重，绝对路径）：正文里提到时渲染成超链接
      turnFiles: [],      // 本轮（一次 send）新增：回复结束后打印链接清单

      // —— 输入 ——
      input: '',
      caretPos: 0,
      composing: false,
      cmdHistory: [],
      histIdx: -1,

      // —— 斜杠菜单 ——
      menuIdx: 0
    },

    computed: {
      viewLines: function () { return this.lines.concat(this.live); },

      /** 斜杠菜单候选：按第一个 token 前缀过滤 */
      menuItems: function () {
        var tok = (this.input || '').trim().split(/\s+/)[0] || '';
        if (tok.charAt(0) !== '/') return [];
        var out = COMMANDS.filter(function (c) { return c.cmd !== tok && c.cmd.indexOf(tok) === 0; });
        return out.slice(0, 20);
      },

      /** 菜单当前选中项下标（menuIdx 越界时夹回最后一项，保证始终有高亮） */
      menuSel: function () {
        var n = this.menuItems.length;
        return n ? Math.min(this.menuIdx, n - 1) : 0;
      },

      /**
       * 已创建文件的链接匹配规格：
       * 每个文件注册三种写法（绝对路径反斜杠 / 正斜杠 / 裸文件名），长的优先；
       * 返回 { re: 全局正则（第 1 组为文件名前导边界，需原样保留）, map: 命中串小写 → 绝对路径 }。
       */
      fileLinkSpec: function () {
        var map = Object.create(null);
        this.createdFiles.forEach(function (p) {
          var add = function (k) { var key = k.toLowerCase(); if (!(key in map)) map[key] = p; };
          add(p);
          add(p.replace(/\\/g, '/'));
          add(p.split(/[\\/]/).pop());
        });
        var keys = Object.keys(map).sort(function (a, b) { return b.length - a.length; });
        if (!keys.length) return null;
        var re = new RegExp(
          // 前导边界：行首或非单词/点/斜杠字符（避免匹配到更长路径的尾部）
          '(^|[^\\w.\\\\/\\-])(?:' + keys.map(escRe).join('|') + ')(?![\\w])',
          'gi'
        );
        return { re: re, map: map };
      },

      /** 输入行画面：在光标位置插入块状光标 */
      inputView: function () {
        var v = this.input || '';
        var i = Math.max(0, Math.min(this.caretPos, v.length));
        var before = esc(v.slice(0, i));
        var after = esc(v.slice(i));
        return before + '<span class="cur">&nbsp;</span>' + after;
      }
    },

    created: function () {
      // Vue 2 不会代理 _ 开头的字段，这些非响应式运行时变量在 created 直接挂实例
      this._waiters = {};
      this._abort = null;
      this._cur = null;       // 当前流式行
      this._spinner = null;
      this._banner = false;
      this.line('sys', '正在连接本地模型服务…');
    },

    mounted: function () {
      var self = this;
      if (host) {
        host.addEventListener('message', this.onHostMessage);
        this.post({ a: 'ready' });
        this.post({ a: 'cfg' });     // 主动拉一次配置，用于启动横幅
      } else {
        this.line('err', '未检测到 WebView2 消息桥：请从 ooor 主程序打开本窗口。');
      }
      this.focusInput();
      this.$refs.keys.addEventListener('blur', function () {
        // 控制台窗口点击别处也保持可输入状态
        setTimeout(function () { try { self.$refs.keys.focus(); } catch (e) {} }, 0);
      });
      // 事件委托：点击正文中的文件链接 / 结尾文件清单 → 通知 C# 用默认程序打开
      this.$refs.output.addEventListener('click', function (e) {
        var a = e.target && e.target.closest ? e.target.closest('a.filelink') : null;
        if (a) {
          e.preventDefault();
          self.post({ a: 'openPath', path: a.getAttribute('data-path') || '' });
        }
      });
    },

    methods: {
      // ==================== 与 C# 通信 ====================

      post: function (msg) { if (host) host.postMessage(msg); },

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

        var key = (m.t === 'toolResult' || m.t === 'fileResult') ? (m.t + ':' + m.id) : m.t;
        var w = this._waiters[key];
        if (w) { delete this._waiters[key]; w.resolve(m); }

        switch (m.t) {
          case 'state': this.applyState(m); break;
          case 'cfg': this.applyCfg(m); break;
          case 'msg': this.line(m.role === 'error' ? 'err' : 'sys', m.text || ''); break;
          case 'clear': this.lines = []; this.history = []; break;
          case 'fileResult': break;   // 由命令的 waitFor 消费
          case 'toolResult': break;
        }
      },

      applyState: function (s) {
        this.version = s.version || '-';
        this.running = !!s.running;
        this.baseUrl = s.baseUrl || '';
        if (s.modelId) this.modelId = s.modelId;
        this.roots = (s.roots || []).filter(Boolean);
        this.toolNames = s.tools || '';
        this.allowWrite = !!s.allowWrite;
        this.allowCmd = !!s.allowCmd;
        this.allowInternet = !!s.allowInternet;
        this.trustAi = !!s.trustAi;
        this.tryBanner();
      },

      applyCfg: function (m) {
        this.cfg = {
          system: m.system || '',
          tools: m.tools || [],
          maxSteps: m.maxSteps || 12,
          temperature: m.temperature || 0.2
        };
        if (m.baseUrl) this.baseUrl = m.baseUrl;
        if (m.modelId) this.modelId = m.modelId;
        this.running = !!m.running || this.running;
        this.tryBanner();
      },

      base: function () { return (this.baseUrl || '').replace(/\/+$/, ''); },

      // ==================== 终端行 ====================

      /** 追加一条已定稿的行 */
      line: function (cls, html) {
        this.lines.push({ cls: cls, html: html == null ? '' : String(html) });
        if (this.lines.length > MAX_LINES) this.lines.splice(0, this.lines.length - MAX_LINES);
        this.pin();
        return this.lines[this.lines.length - 1];
      },

      /**
       * 把原始行里「已创建文件」的路径/文件名替换成超链接（其余文本照常 HTML 转义）。
       * 链接点击由 #output 上的事件委托统一处理（发 openPath 给 C#）。
       */
      linkify: function (src) {
        var spec = this.fileLinkSpec;
        if (!spec) return esc(src);

        var out = '';
        var last = 0;
        var re = new RegExp(spec.re.source, 'gi');
        var m;
        while ((m = re.exec(src))) {
          if (m.index === re.lastIndex) { re.lastIndex++; continue; }
          var pre = m[1] || '';
          var hit = m[0].slice(pre.length);
          var abs = spec.map[hit.toLowerCase()];
          out += esc(src.slice(last, m.index)) + esc(pre);
          out += '<a class="filelink" href="#" data-path="' + esc(abs) + '" title="' + esc(abs) + '">' + esc(hit) + '</a>';
          last = m.index + m[0].length;
        }
        out += esc(src.slice(last));
        return out;
      },

      /** 正文行定稿：先转义+文件链接，再走 Markdown 终端化结构渲染 */
      renderLine: function (raw) {
        return renderStructured(this.linkify(raw));
      },

      /** 把一条原始行按终端语义定稿（代码围栏 / 思考灰行 / Markdown 行） */
      finalize: function (kind, raw) {
        if (kind === 'reasoning') {
          this.line('reasoning', '<span class="rmark">  │ </span>' + esc(raw));
          return;
        }
        if (/^\s*```/.test(raw)) {
          this.inFence = !this.inFence;
          this.line('fence', '<span class="fmark">▌</span> ' + esc(raw));
          return;
        }
        if (this.inFence) {
          this.line('fence', '<span class="fmark">▌</span>' + (raw ? ' ' + esc(raw) : ''));
          return;
        }
        this.line('assistant', this.renderLine(raw));
      },

      /** 流式增量：写入对应通道（content/reasoning）的当前行，遇换行定稿 */
      appendStream: function (kind, piece) {
        if (!piece) return;
        if (this._spinner) {
          var si = this.live.indexOf(this._spinner);
          if (si >= 0) this.live.splice(si, 1);
          this._spinner = null;
        }

        var cur = this._cur;
        // 通道切换（正文 ↔ 思考）：先封存另一条通道尚未换行的行
        if (cur && cur.kind !== kind) { this.sealLive(cur, false); cur = null; }

        var parts = piece.split('\n');
        for (var k = 0; k < parts.length; k++) {
          if (k > 0) {
            // 收到换行：当前行定稿（空行也保留），后续另起一行
            this.sealLive(cur, true);
            cur = null;
          }
          var seg = parts[k];
          var isLast = (k === parts.length - 1);
          if (seg === '') {
            if (!isLast) this.finalize(kind, '');   // 段落间空行
            continue;
          }
          if (!cur) cur = this.newLive(kind);
          cur.raw += seg;
          cur.html = (kind === 'reasoning' ? '<span class="rmark">  │ </span>' : '') + esc(cur.raw);
        }
        this._cur = cur;
        this.pin();
      },

      newLive: function (kind) {
        var ln = { kind: kind, raw: '', cls: kind === 'reasoning' ? 'reasoning' : 'assistant',
                   html: '', rendered: false };
        this.live.push(ln);
        this._cur = ln;
        return ln;
      },

      /** 一条流式行定稿：pushEmpty 时空行也保留（换行产生的空行） */
      sealLive: function (ln, keepEmpty) {
        if (!ln) return;
        if (ln.raw === '' && !keepEmpty) {
          var i = this.live.indexOf(ln);
          if (i >= 0) this.live.splice(i, 1);
          return;
        }
        this.finalize(ln.kind, ln.raw);
        var j = this.live.indexOf(ln);
        if (j >= 0) this.live.splice(j, 1);
      },

      /** 一轮回复结束：剩余 live 行落账（忽略尾部空行） */
      sealAll: function () {
        var self = this;
        this.live.forEach(function (ln) {
          if (ln.raw) self.finalize(ln.kind, ln.raw);
        });
        this.live = [];
        this._cur = null;
        this.inFence = false;
      },

      pin: function () {
        var self = this;
        Vue.nextTick(function () {
          var el = self.$refs.output;
          if (el) el.scrollTop = el.scrollHeight;
        });
      },

      // ==================== 输入 / 键盘 ====================

      focusInput: function () {
        var t = this.$refs.keys;
        if (t) t.focus();
      },

      onInput: function () {
        var t = this.$refs.keys;
        this.input = t.value;
        this.caretPos = t.selectionStart || 0;
        this.menuIdx = 0;
      },

      syncCaret: function () {
        var t = this.$refs.keys;
        this.caretPos = t ? (t.selectionStart || 0) : 0;
      },

      onKeydown: function (e) {
        if (this.composing || e.isComposing || e.keyCode === 229) return;

        var menu = this.menuItems;

        if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.altKey) {
          e.preventDefault();
          if (menu.length) { this.pickMenu(this.menuSel); return; }
          this.runLine();
          return;
        }
        if (e.key === 'ArrowUp') {
          e.preventDefault();
          if (menu.length) { this.menuIdx = (this.menuSel - 1 + menu.length) % menu.length; return; }
          this.recallHistory(1);
          return;
        }
        if (e.key === 'ArrowDown') {
          e.preventDefault();
          if (menu.length) { this.menuIdx = (this.menuSel + 1) % menu.length; return; }
          this.recallHistory(-1);
          return;
        }
        if (e.key === 'Tab' && menu.length) {
          e.preventDefault();
          this.pickMenu(this.menuSel);
          return;
        }
        if (e.key === 'Escape') {
          e.preventDefault();
          if (menu.length) { this.setInput(''); return; }
          if (this.busy && this._abort) { try { this._abort.abort(); } catch (x) {} }
          return;
        }
        // Ctrl+C：单击中断生成（同 Esc），500ms 内连按两次退出窗口（仿 cmd 连按 Ctrl+C 关窗）
        if ((e.ctrlKey || e.metaKey) && (e.key === 'c' || e.key === 'C')) {
          e.preventDefault();
          var now = Date.now();
          if (now - (this._lastCtrlC || 0) < 500) { this.post({ a: 'exit' }); return; }
          this._lastCtrlC = now;
          if (this.busy && this._abort) { try { this._abort.abort(); } catch (x) {} }
          return;
        }
        // 其它键（←→ Home End 等）松开后同步光标位置
        var self = this;
        setTimeout(function () { self.syncCaret(); }, 0);
      },

      pickMenu: function (i) {
        var mi = this.menuItems[i];
        if (!mi) return;
        this.setInput(mi.cmd + ' ');
        this.focusInput();
      },

      setInput: function (v) {
        this.input = v;
        this.caretPos = v.length;
        var t = this.$refs.keys;
        if (t) { t.value = v; try { t.setSelectionRange(v.length, v.length); } catch (e) {} }
      },

      recallHistory: function (dir) {
        if (!this.cmdHistory.length) return;
        if (this.histIdx === -1 && dir > 0) this.histIdx = this.cmdHistory.length - 1;
        else if (this.histIdx === 0 && dir > 0) this.histIdx = 0;
        else if (dir < 0) {
          this.histIdx++;
          if (this.histIdx >= this.cmdHistory.length) { this.histIdx = -1; this.setInput(''); return; }
        } else if (dir > 0) this.histIdx--;
        if (this.histIdx >= 0) this.setInput(this.cmdHistory[this.histIdx]);
      },

      // ==================== 命令 / 发送 ====================

      runLine: function () {
        var text = (this.input || '').trim();
        this.setInput('');
        if (!text) return;

        // 回显用户输入
        this.line('user', '<span class="prompt">Ooor&gt;</span> ' + esc(text));
        if (this.histIdx === -1 || this.cmdHistory[this.cmdHistory.length - 1] !== text)
          this.cmdHistory.push(text);
        this.histIdx = -1;

        if (text.charAt(0) === '/') { this.handleCommand(text); return; }
        this.send(text);
      },

      handleCommand: function (text) {
        var toks = text.split(/\s+/);
        var cmd = toks[0];
        var arg = toks.slice(1);
        var self = this;

        switch (cmd) {
          case '/help':
            this.line('banner', '命令一览：');
            COMMANDS.forEach(function (c) {
              self.line('sys', '  <span class="deco-cyan">' + esc(c.cmd) + '</span>' +
                spaces(16 - c.cmd.length) + esc(c.hint));
            });
            break;

          case '/clear':
            this.lines = [];
            this.history = [];
            this.createdFiles = [];
            this.turnFiles = [];
            this.line('sys', '已清屏并重置对话。');
            break;

          case '/exit':
            this.post({ a: 'exit' });
            break;

          case '/tools':
            if (this.toolNames)
              this.line('sys', '已启用工具：' + this.toolNames);
            else
              this.line('sys', '当前没有可用工具（先 /option write/cmd/net on 开启）。');
            break;

          case '/roots':
            this.printRoots();
            break;

          case '/root':
            this.cmdRoot(arg);
            break;

          case '/option':
            this.cmdOption(arg);
            break;

          case '/file':
            this.cmdFile(arg.join(' '));
            break;

          case '/new-console':
            this.post({ a: 'newWindow' });
            break;

          default:
            this.line('err', '未知命令：' + esc(cmd) + '（输入 /help 查看全部命令）');
        }
      },

      printRoots: function () {
        if (!this.roots.length) { this.line('sys', '白名单为空。'); return; }
        this.line('banner', '沙盒白名单（' + this.roots.length + '）：');
        var self = this;
        this.roots.forEach(function (r, i) {
          self.line('sys', '  [' + i + '] ' + esc(r));
        });
      },

      cmdRoot: function (arg) {
        var sub = (arg[0] || '').toLowerCase();
        if (sub === 'add') {
          var p = arg.slice(1).join(' ');
          if (!p) { this.post({ a: 'addRoot' }); this.line('sys', '请选择要加入白名单的目录…'); }
          else { this.post({ a: 'rootAdd', path: p }); }
        } else if (sub === 'rm') {
          var n = parseInt(arg[1], 10);
          if (isNaN(n) || !this.roots[n]) { this.line('err', '用法：/root rm <序号>（/roots 查看序号）'); return; }
          this.post({ a: 'removeRoot', path: this.roots[n] });
        } else {
          this.line('err', '用法：/root add [路径] 或 /root rm <序号>');
        }
      },

      cmdOption: function (arg) {
        var key = (arg[0] || '').toLowerCase();
          var val = (arg[1] || '').toLowerCase();
          var map = { write: 'allowWrite', cmd: 'allowCmd', net: 'allowInternet', trust: 'trustAi' };
          var field = map[key];
          if (!field || (val !== 'on' && val !== 'off')) {
            this.line('err', '用法：/option write|cmd|net|trust on|off');
            this.line('sys', '当前：write=' + this.allowWrite + ' cmd=' + this.allowCmd +
              ' net=' + this.allowInternet + ' trust=' + this.trustAi);
            return;
          }
          this[field] = val === 'on';
          this.post({
            a: 'options',
            allowWrite: this.allowWrite,
            allowCmd: this.allowCmd,
            allowInternet: this.allowInternet,
            trustAi: this.trustAi
          });
          this.line('sys', '已设置 ' + key + '=' + val + '（工具集变化时对话会自动重置）。');
      },

      cmdFile: function (path) {
        if (!path) { this.line('err', '用法：/file <路径>'); return; }
        var self = this;
        var id = 'f' + Date.now();
        this.waitFor('fileResult:' + id, { a: 'fileRead', id: id, path: path }, 30000)
          .then(function (r) {
            if (!r.ok) { self.line('err', r.text || '读取失败'); return; }
            var t = r.text || '';
            self.line('sys', '── 文件：' + esc(r.path) + '（' + t.length + ' 字符）──');
            if (t.length > 6000) t = t.slice(0, 6000);
            String(t).split('\n').forEach(function (l) {
              self.line('fence', '<span class="fmark">▌</span> ' + esc(l.replace(/\r$/, '')));
            });
          })
          .catch(function (e) { self.line('err', String((e && e.message) || e)); });
      },

      // ==================== 对话主流程（页面侧 Agent 循环） ====================

      send: function (text) {
        var self = this;
        if (this.busy) { this.line('sys', '（正在回复中，完成后再发；Esc / Ctrl+C 可中断，连按两次 Ctrl+C 退出）'); return; }
        if (!host) { this.line('err', '未检测到 WebView2 消息桥，无法执行工具。'); return; }
        if (!this.running) { this.line('err', '本地服务未运行：请先回到 ooor 主程序启动服务。'); return; }
        if (!this.base()) { this.line('err', '未取到服务地址。'); return; }

        this.busy = true;
        this._abort = new AbortController();
        this.turnFiles = [];   // 本轮新建文件清单，回复结束后汇总打印
        this.history.push({ role: 'user', content: text });

        var run = (this.cfg ? Promise.resolve() : this.loadCfg())
          .then(function () { return self.ensureModel(); })
          .then(function () { return self.runLoop(); });

        run.catch(function (e) {
          if (e && e.name === 'AbortError') self.line('sys', '已中断本次请求。');
          else self.line('err', '出错了：' + ((e && e.message) || e));
        }).then(function () {
          self.sealAll();
          self.busy = false;
          self._abort = null;
        });
      },

      loadCfg: function () {
        var self = this;
        return this.waitFor('cfg', { a: 'cfg' }).then(function (m) {
          if (!self.cfg) self.applyCfg(m);
        });
      },

      /** 模型 id 优先用 C# 探活结果；没有再 GET /v1/models */
      ensureModel: function () {
        var self = this;
        if (this.modelId) { this.model = this.modelId; return Promise.resolve(); }
        return fetch(this.base() + '/v1/models', { signal: this._abort.signal })
          .then(function (res) {
            if (!res.ok) throw new Error('获取模型列表失败 HTTP ' + res.status);
            return res.json();
          })
          .then(function (j) {
            var id = j && j.data && j.data[0] && j.data[0].id;
            if (!id) throw new Error('llama-server 未返回任何模型，请确认服务已加载模型');
            self.model = id;
            self.modelId = id;
          });
      },

      runLoop: function () {
        var self = this;
        var maxSteps = (this.cfg && this.cfg.maxSteps) || 12;
        var step = 0;

        function next() {
          if (step++ >= maxSteps) {
            self.line('err', '达到最大步数（' + maxSteps + '）仍未结束，已停止。');
            return Promise.resolve();
          }

          var body = {
            model: self.model || self.modelId,
            messages: self.history,
            stream: true,
            temperature: (self.cfg && self.cfg.temperature) || 0.2
          };
          if (self.cfg && self.cfg.tools && self.cfg.tools.length) {
            body.tools = self.cfg.tools;
            body.tool_choice = 'auto';
          }

          self._spinner = { kind: 'content', raw: '', cls: 'spinner', html: '… 正在思考…', rendered: true };
          self.live.push(self._spinner);

          return self.streamChat(body).then(function (out) {
            if (self._spinner) {
              var si = self.live.indexOf(self._spinner);
              if (si >= 0) self.live.splice(si, 1);
              self._spinner = null;
            }
            self.sealAll();

            var asst = { role: 'assistant', content: out.text || '' };
            if (out.calls.length) asst.tool_calls = out.calls;
            self.history.push(asst);

            if (!out.calls.length) {
              // 自然结束（不再调工具）：把本轮模型创建/修改的文件以链接清单打印出来
              self.printCreatedFiles();
              return undefined;
            }

            var chain = Promise.resolve();
            out.calls.forEach(function (c) {
              chain = chain.then(function () { return self.runTool(c); });
            });
            return chain.then(next);
          });
        }

        return next();
      },

      runTool: function (call) {
        var self = this;
        var fn = call.function || {};
        var name = fn.name || '';
        var args = fn.arguments || '';
        var brief = args.length > 160 ? args.slice(0, 160) + '…' : args;
        var head = this.line('toolhead',
          '<span class="tname">⚙ ' + esc(name) + '</span> ' + esc(brief) + ' <span class="ok">…</span>');

        return this.waitFor('toolResult:' + call.id,
                            { a: 'tool', id: call.id, name: name, args: args },
                            5 * 60 * 1000)
          .then(function (r) {
            head.html = '<span class="tname">⚙ ' + esc(name) + '</span> ' + esc(brief) +
              ' <span class="' + (r.ok ? 'ok' : 'fail') + '">' + (r.ok ? 'OK' : 'FAIL') + '</span>';
            var txt = r.text || '';
            var shown = txt.length > 1500 ? txt.slice(0, 1500) + '\n…（已截断）' : txt;
            self.line('toolbody', esc(shown));
            self.history.push({ role: 'tool', tool_call_id: call.id, content: txt });

            // 登记/注销模型创建的文件，供正文链接化与结尾清单使用
            var p = self.extractToolPath(txt, args);
            if (r.ok && (name === 'write_file' || name === 'create_file') && p) self.registerCreatedFile(p);
            else if (r.ok && name === 'delete_file' && p) self.unregisterCreatedFile(p);
          });
      },

      /**
       * 从工具结果文本提取绝对路径（成功消息都带解析后的完整路径，如
       * "已创建文件：C:\tmp\index.html（123 字符）"）；提取不到再回退看参数里的绝对路径。
       */
      extractToolPath: function (text, argsJson) {
        var p = '';
        var m = /[A-Za-z]:[\\/][^\s（）()]+/.exec(text || '');
        if (m) p = m[0].replace(/[.,，。;；、`'"]+$/, '');
        if (!p && argsJson) {
          try {
            var o = JSON.parse(argsJson);
            if (o && typeof o.path === 'string' && /^[A-Za-z]:[\\/]/.test(o.path)) p = o.path;
          } catch (e) { /* 参数可能还是分片 JSON，忽略 */ }
        }
        return p;
      },

      /** 登记一个模型创建/覆盖的文件（会话去重 + 本轮去重，大小写不敏感） */
      registerCreatedFile: function (p) {
        var low = p.toLowerCase();
        var exists = this.createdFiles.some(function (x) { return x.toLowerCase() === low; });
        if (!exists) this.createdFiles.push(p);
        var inTurn = this.turnFiles.some(function (x) { return x.toLowerCase() === low; });
        if (!inTurn) this.turnFiles.push(p);
      },

      /** 文件被删除/回收站后从链接集合移除 */
      unregisterCreatedFile: function (p) {
        var low = p.toLowerCase();
        var drop = function (arr) {
          for (var i = arr.length - 1; i >= 0; i--)
            if (arr[i].toLowerCase() === low || arr[i].split(/[\\/]/).pop().toLowerCase() === low) arr.splice(i, 1);
        };
        drop(this.createdFiles);
        drop(this.turnFiles);
      },

      /** 回复结束：把本轮创建/修改的文件打印成链接清单（点击由 C# 用默认程序打开） */
      printCreatedFiles: function () {
        if (!this.turnFiles.length) return;
        this.line('sys', '');
        this.line('banner', '── 本次创建 / 修改的文件（点击用默认程序打开）──');
        this.turnFiles.forEach(function (p) {
          var base = p.split(/[\\/]/).pop();
          this.line('filelist',
            '  <a class="filelink" href="#" data-path="' + esc(p) + '" title="' + esc(p) + '">' + esc(base) + '</a>' +
            '  <span class="fpath">' + esc(p) + '</span>');
        }, this);
      },

      /** 流式请求 /v1/chat/completions（SSE），返回 { text, reasoning, calls } */
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
                var f = tc.function || {};
                // name / arguments 为分片增量，必须拼接（覆盖会只剩最后一个碎片 → 未知工具）
                if (f.name) call.function.name += f.name;
                if (f.arguments) call.function.arguments += f.arguments;
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
              var ls = buf.split('\n');
              buf = ls.pop();
              for (var i = 0; i < ls.length; i++) handleLine(ls[i]);
              return pump();
            });
          }

          return pump().then(function () {
            out.calls = out.calls.filter(function (c) { return c && c.function && c.function.name; });
            return out;
          });
        });
      },

      // ==================== 启动横幅 ====================

      tryBanner: function () {
        if (this._banner) return;
        // state 与 cfg 都到齐（至少 baseUrl 已知）再打
        if (!this.baseUrl) return;
        this._banner = true;

        this.lines = [];
        this.line('banner', 'ooor 本地 AI 助手 · 控制台窗口' + (this.version ? '  v' + this.version : ''));
        this.line('sys', '服务 ' + this.baseUrl + (this.modelId ? '   模型 ' + this.modelId : '') +
          (this.running ? '   <span class="deco-cyan">[已连接]</span>' : '   <span style="color:#e74856">[未连接]</span>'));
        if (this.toolNames) this.line('sys', '工具 ' + this.toolNames);
        if (this.roots.length) {
          this.line('sys', '白名单：');
          var self = this;
          this.roots.forEach(function (r) { self.line('sys', '  [' + r + ']'); });
        }
        this.line('sys', '直接输入文字与模型对话；输入 /help 查看命令，Esc / Ctrl+C 中断生成，连按两次 Ctrl+C 退出。');
        this.line('sys', '');
      }
    }
  });

  function spaces(n) {
    n = Math.max(1, n);
    var s = '';
    while (n-- > 0) s += ' ';
    return s;
  }
})();
