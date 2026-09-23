using System;
using System.Windows.Forms;
using ooor.Core;

namespace ooor
{
    /// <summary>
    /// 应用设置窗（逻辑层）：3 个自启动复选框 + 语言下拉 → 持久化到 app.conf + 注册表。
    /// 继承 LocalizedForm，语言切换时 ApplyLanguage 自动刷新自身文本。
    /// 控件树见 AppSettingsForm.Designer.cs。
    /// </summary>
    internal sealed partial class AppSettingsForm : LocalizedForm
    {
        public AppSettingsForm()
        {
            InitializeComponent();
            btnOk.Click += BtnOk_Click;

            // 载入当前设置回填复选框 + 语言下拉（基类 Load 会再调一次 ApplyLanguage 翻译文本）
            LoadSettings();
        }

        /// <summary>载入设置回填复选框与语言下拉（不涉及翻译）</summary>
        private void LoadSettings()
        {
            var s = AppSettings.Load();
            chkAutoStart.Checked = s.AutoStart;
            chkAutoStartModel.Checked = s.AutoStartModel;
            chkAutoStartHide.Checked = s.AutoStartHide;
            UpdateEnabled();

            cmbLanguage.Items.Clear();
            foreach (var kv in LanguageManager.Instance.AvailableLanguages)
                cmbLanguage.Items.Add(kv.Key + "|" + kv.Value);
            string cur = s.Language ?? "zh";
            for (int i = 0; i < cmbLanguage.Items.Count; i++)
            {
                string item = (string)cmbLanguage.Items[i];
                if (item.StartsWith(cur + "|")) { cmbLanguage.SelectedIndex = i; break; }
            }
            if (cmbLanguage.SelectedIndex < 0) cmbLanguage.SelectedIndex = 0;
        }

        /// <summary>按当前语言刷新窗口自身文本</summary>
        protected override void ApplyLanguage()
        {
            var L = LanguageManager.Instance;
            Text = L.T("settings.title");
            chkAutoStart.Text = L.T("settings.autoStart");
            chkAutoStartModel.Text = L.T("settings.autoStartModel");
            chkAutoStartHide.Text = L.T("settings.autoStartHide");
            lblLanguage.Text = L.T("settings.language");
            btnOk.Text = L.T("common.save");
            btnCancel.Text = L.T("common.cancel");
        }

        /// <summary>勾选联动：未勾选「自启动」时，后两项灰显</summary>
        private void UpdateEnabled()
        {
            bool on = chkAutoStart.Checked;
            chkAutoStartModel.Enabled = on;
            chkAutoStartHide.Enabled = on;
        }

        /// <summary>保存：写 app.conf + 切注册表自启项 + 切换语言</summary>
        private void BtnOk_Click(object sender, EventArgs e)
        {
            string code = "zh";
            if (cmbLanguage.SelectedIndex >= 0)
            {
                string item = (string)cmbLanguage.Items[cmbLanguage.SelectedIndex];
                int bar = item.IndexOf('|');
                if (bar > 0) code = item.Substring(0, bar);
            }

            var s = new AppSettings
            {
                AutoStart = chkAutoStart.Checked,
                AutoStartModel = chkAutoStartModel.Checked,
                AutoStartHide = chkAutoStartHide.Checked,
                Language = code
            };
            s.Save();
            s.ApplyAutoStart();

            // 切换语言（触发 LanguageChanged，所有 LocalizedForm 子类即时刷新）
            if (!string.Equals(LanguageManager.Instance.CurrentLanguage, code, StringComparison.Ordinal))
                LanguageManager.Instance.LoadLanguage(code);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
