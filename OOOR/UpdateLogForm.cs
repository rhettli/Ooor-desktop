using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ooor.Controls;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 更新日志窗口：GET /api/v1/releases/changelog?limit=50，渲染日志列表。
    /// 每行一个 UpdateLogRowControl（版本 + 通道 + 时间 + 日志正文）。
    /// </summary>
    public partial class UpdateLogForm : LocalizedForm
    {
        private static LanguageManager L => LanguageManager.Instance;

        private const int DefaultLimit = 50;
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private CancellationTokenSource _cts;

        public UpdateLogForm()
        {
            InitializeComponent();
            FormClosed += (s, e) => { try { _cts?.Cancel(); } catch { } };
            Shown += (s, e) => _ = LoadAsync();
            pnlList.ClientSizeChanged += (s, e) => ReflowRowWidths();
        }

        protected override void ApplyLanguage()
        {
            Text = L.T("ulog.title");
            tsbRefresh.Text = L.T("ulog.refresh");
        }

        private void tsbRefresh_Click(object sender, EventArgs e) => _ = LoadAsync();

        // ==================== 数据加载 ====================

        private async Task LoadAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            tslStatus.Text = L.T("ulog.loading");
            tslStatus.ForeColor = SystemColors.ControlText;
            ClearRows();
            tsbRefresh.Enabled = false;

            try
            {
                var result = await FetchChangelogAsync(DefaultLimit, ct);
                if (IsDisposed || ct.IsCancellationRequested) return;
                RenderResult(result);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                tslStatus.Text = string.Format(L.T("ulog.error"), ex.Message);
                tslStatus.ForeColor = Color.FromArgb(0xC0, 0x39, 0x2B);
            }
            finally
            {
                if (!IsDisposed) tsbRefresh.Enabled = true;
            }
        }

        // ==================== API 调用 ====================

        private static async Task<ChangelogResult> FetchChangelogAsync(int limit, CancellationToken ct)
        {
            var set = OoorSettings.Load();
            string url = set.ServerUrl.TrimEnd('/') + "/api/v1/releases/changelog?limit=" + limit;

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

        private static ChangelogResult ParseResult(string json)
        {
            var d = Ser.Deserialize<Dictionary<string, object>>(json);
            if (d == null) throw new Exception("invalid response");

            var result = new ChangelogResult
            {
                Total = GetLong(d, "total"),
                Count = GetLong(d, "count"),
            };

            var logs = new List<ChangelogEntry>();
            if (d.TryGetValue("logs", out var cObj) && cObj is System.Collections.IEnumerable arr)
            {
                foreach (var item in arr)
                {
                    var cd = ToDict(item);
                    if (cd == null) continue;
                    logs.Add(new ChangelogEntry
                    {
                        Version = GetStr(cd, "version"),
                        Channel = GetStr(cd, "channel"),
                        Notes = GetStr(cd, "notes"),
                        Force = GetBool(cd, "force"),
                        FileDeleted = GetBool(cd, "file_deleted"),
                        PublishedAt = GetStr(cd, "published_at"),
                    });
                }
            }
            result.Logs = logs;
            return result;
        }

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

        private static bool GetBool(IDictionary<string, object> d, string key)
        {
            if (d.TryGetValue(key, out var v) && v != null)
            {
                if (v is bool b) return b;
                return string.Equals(v.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        // ==================== 渲染 ====================

        private void RenderResult(ChangelogResult result)
        {
            tslStatus.Text = result.Logs.Count > 0
                ? string.Format(L.T("ulog.summary"), result.Total, result.Logs.Count)
                : L.T("ulog.empty");
            tslStatus.ForeColor = SystemColors.ControlText;

            if (result.Logs.Count == 0)
            {
                ClearRows();
                return;
            }

            ClearRows();
            pnlList.SuspendLayout();
            for (int i = 0; i < result.Logs.Count; i++)
            {
                var log = result.Logs[i];
                var row = new UpdateLogRowControl
                {
                    Location = new Point(0, i * UpdateLogRowControl.RowHeight),
                    Width = pnlList.ClientSize.Width,
                };
                row.Bind(log.Version, log.Channel, log.Notes,
                    log.Force, log.FileDeleted, FormatPublishedAt(log.PublishedAt));
                pnlList.Controls.Add(row);
            }
            pnlList.ResumeLayout(false);
            ReflowRowWidths();
            pnlList.PerformLayout();
        }

        private void ClearRows()
        {
            pnlList.SuspendLayout();
            foreach (Control ctrl in pnlList.Controls)
            {
                if (ctrl is UpdateLogRowControl) ctrl.Dispose();
            }
            pnlList.Controls.Clear();
            pnlList.ResumeLayout(false);
        }

        private void ReflowRowWidths()
        {
            int w = pnlList.ClientSize.Width;
            foreach (Control ctrl in pnlList.Controls)
            {
                if (ctrl is UpdateLogRowControl rc)
                {
                    rc.Width = w;
                    rc.ReflowWidth();
                }
            }
        }

        // ==================== 工具 ====================

        /// <summary>ISO UTC → 本地时间 yyyy-MM-dd HH:mm</summary>
        private static string FormatPublishedAt(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso)) return "";
            DateTime t;
            if (DateTime.TryParse(iso.Trim(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out t))
                return t.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return iso;
        }
    }

    // ==================== 数据模型 ====================

    internal class ChangelogResult
    {
        public long Total;
        public long Count;
        public List<ChangelogEntry> Logs = new List<ChangelogEntry>();
    }

    internal class ChangelogEntry
    {
        public string Version;
        public string Channel;
        public string Notes;
        public bool Force;
        public bool FileDeleted;
        public string PublishedAt;
    }
}
