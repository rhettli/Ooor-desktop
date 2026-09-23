using System;
using System.Windows.Forms;

namespace ooor.Core
{
    /// <summary>
    /// 多语言窗口基类：构造时自动订阅 LanguageManager.LanguageChanged 事件，
    /// 语言切换时回调 ApplyLanguage()；窗口关闭时自动退订，避免内存泄漏。
    /// 子类只需 override ApplyLanguage() 翻译自身控件文本即可。
    /// </summary>
    public class LocalizedForm : Form
    {
        protected LocalizedForm()
        {
            // 窗口首次显示时订阅（此时控件已初始化），关闭时退订
            Load += (s, e) =>
            {
                ApplyLanguage();
                LanguageManager.Instance.LanguageChanged += OnLanguageChanged;
            };
            FormClosed += (s, e) =>
            {
                LanguageManager.Instance.LanguageChanged -= OnLanguageChanged;
            };
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // 语言切换事件在 UI 线程触发（设置窗口保存时同步调用），但保险起见封送回 UI 线程
            if (InvokeRequired)
                BeginInvoke((MethodInvoker)ApplyLanguage);
            else
                ApplyLanguage();
        }

        /// <summary>子类 override：按当前语言刷新自身所有控件文本</summary>
        protected virtual void ApplyLanguage() { }
    }
}
