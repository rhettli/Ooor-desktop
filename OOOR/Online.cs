using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ooor.Controls;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 在线设备窗口：GET /api/v1/clients/online?limit=20，渲染终端式列表。
    /// 每行一个 ClientRowControl（3 行布局），顶部 ToolStrip 状态栏显示汇总。
    /// </summary>
    public partial class Online : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private const int DefaultLimit = 20;
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private CancellationTokenSource _cts;

        public Online()
        {
            InitializeComponent();
            FormClosed += (s, e) => { try { _cts?.Cancel(); } catch { } };
            Shown += (s, e) => _ = LoadAsync();
            pnlList.ClientSizeChanged += (s, e) => ReflowRowWidths();
        }

        protected override void ApplyLanguage()
        {
            Text = L.T("online.title");
            tsbRefresh.Text = L.T("online.refresh");
        }

        // ==================== 工具栏 ====================

        private void tsbRefresh_Click(object sender, EventArgs e) => _ = LoadAsync();

        // ==================== 数据加载 ====================

        private async Task LoadAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            tslStatus.Text = L.T("online.loading");
            tslStatus.ForeColor = SystemColors.ControlText;
            ClearRows();
            tsbRefresh.Enabled = false;

            try
            {
                var result = await FetchOnlineAsync(DefaultLimit, ct);
                if (IsDisposed || ct.IsCancellationRequested) return;
                RenderResult(result);
            }
            catch (OperationCanceledException)
            {
                // 窗口关闭或刷新覆盖：忽略
            }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                tslStatus.Text = string.Format(L.T("online.error"), ex.Message);
                tslStatus.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
            }
            finally
            {
                if (!IsDisposed) tsbRefresh.Enabled = true;
            }
        }

        // ==================== API 调用 ====================

        private static async Task<OnlineResult> FetchOnlineAsync(int limit, CancellationToken ct)
        {
            var set = OoorSettings.Load();
            string url = set.ServerUrl.TrimEnd('/') + "/api/v1/clients/online?limit=" + limit;

            // 用 using 保证释放；每次新建避免连接池缓存失效 DNS
            using (var req = new HttpClient { Timeout = TimeSpan.FromSeconds(8) })
            {
                req.DefaultRequestHeaders.Add("Ooor", DEF.ver);

                ct.ThrowIfCancellationRequested();
                string json = await req.GetStringAsync(url);
                return ParseResult(json);
            }
        }

        // ==================== JSON 解析 ====================

        private static readonly JavaScriptSerializer Ser =
            new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        private static OnlineResult ParseResult(string json)
        {
            var d = Ser.Deserialize<Dictionary<string, object>>(json);
            if (d == null) throw new Exception("invalid response");

            var result = new OnlineResult
            {
                Total = GetLong(d, "total"),
                Count = GetLong(d, "count"),
                ServerTime = GetLong(d, "server_time"),
            };

            var clients = new List<OnlineClient>();
            // JavaScriptSerializer 对 JSON 数组可能返回 object[] 或 ArrayList，统一用 IEnumerable 遍历
            if (d.TryGetValue("clients", out var cObj) && cObj is System.Collections.IEnumerable arr)
            {
                foreach (var item in arr)
                {
                    var cd = ToDict(item);
                    if (cd == null) continue;
                    clients.Add(new OnlineClient
                    {
                        Uuid = GetStr(cd, "uuid"),
                        Cpu = GetStr(cd, "cpu"),
                        Cores = (int)GetLong(cd, "cores"),
                        MemMb = GetLong(cd, "mem_mb"),
                        Hostname = GetStr(cd, "hostname"),
                        Ver = GetStr(cd, "ver"),
                        UpdatedIp = GetStr(cd, "updated_ip"),
                        LastAt = GetLong(cd, "last_at"),
                    });
                }
            }
            result.Clients = clients;
            return result;
        }

        /// <summary>把 JSON 对象统一转成 IDictionary&lt;string,object&gt;（兼容 Dictionary 和 Hashtable）</summary>
        private static IDictionary<string, object> ToDict(object o)
        {
            if (o is IDictionary<string, object> d) return d;
            if (o is System.Collections.IDictionary idict)
            {
                var dict = new Dictionary<string, object>();
                foreach (System.Collections.DictionaryEntry de in idict)
                    dict[de.Key.ToString()] = de.Value;
                return dict;
            }
            return null;
        }

        private static string GetStr(IDictionary<string, object> d, string key)
            => d.TryGetValue(key, out var v) && v != null ? v.ToString() : "";

        private static long GetLong(IDictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
            {
                if (v is int i) return i;
                if (v is long l) return l;
                long n;
                if (long.TryParse(v.ToString(), out n)) return n;
            }
            return 0;
        }

        // ==================== 渲染 ====================

        private void RenderResult(OnlineResult result)
        {
            // 状态栏汇总
            string time = FormatUnix(result.ServerTime);
            tslStatus.Text = result.Total > 0
                ? string.Format(L.T("online.summary"), result.Total, result.Count, DefaultLimit, time)
                : L.T("online.empty");
            tslStatus.ForeColor = SystemColors.ControlText;

            // 空列表
            if (result.Clients.Count == 0)
            {
                ClearRows();
                return;
            }

            ClearRows();
            pnlList.SuspendLayout();
            for (int i = 0; i < result.Clients.Count; i++)
            {
                var c = result.Clients[i];
                var row = new ClientRowControl
                {
                    Location = new Point(0, i * ClientRowControl.RowHeight),
                    Width = pnlList.ClientSize.Width,
                };
                row.Bind(i + 1, c.Hostname, c.Cpu, c.Cores, c.MemMb,
                    c.Ver, c.UpdatedIp, c.LastAt, result.ServerTime, c.Uuid);
                pnlList.Controls.Add(row);
            }
            pnlList.ResumeLayout(false);
            ReflowRowWidths();
        }

        private void ClearRows()
        {
            pnlList.SuspendLayout();
            foreach (Control ctrl in pnlList.Controls)
            {
                if (ctrl is ClientRowControl) ctrl.Dispose();
            }
            pnlList.Controls.Clear();
            pnlList.ResumeLayout(false);
        }

        private void ReflowRowWidths()
        {
            int w = pnlList.ClientSize.Width;
            foreach (Control ctrl in pnlList.Controls)
            {
                if (ctrl is ClientRowControl rc)
                {
                    rc.Width = w;
                    rc.ReflowWidth();
                }
            }
        }

        // ==================== 工具 ====================

        /// <summary>Unix 秒 → 本地时间 yyyy-MM-dd HH:mm</summary>
        private static string FormatUnix(long sec)
        {
            try
            {
                var t = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(sec);
                return t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch { return ""; }
        }
    }

    // ==================== 数据模型 ====================

    internal class OnlineResult
    {
        public long Total;
        public long Count;
        public long ServerTime;
        public List<OnlineClient> Clients = new List<OnlineClient>();
    }

    internal class OnlineClient
    {
        public string Uuid;
        public string Cpu;
        public int Cores;
        public long MemMb;
        public string Hostname;
        public string Ver;
        public string UpdatedIp;
        public long LastAt;
    }
}
