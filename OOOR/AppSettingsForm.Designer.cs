namespace ooor
{
    /// <summary>
    /// 应用设置窗（设计类）：3 个自启动相关复选框 + 确定/取消按钮。
    /// 控件布局在此声明，逻辑与事件绑定在 AppSettingsForm.cs。
    /// </summary>
    internal sealed partial class AppSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        internal System.Windows.Forms.CheckBox chkAutoStart;
        internal System.Windows.Forms.CheckBox chkAutoStartModel;
        internal System.Windows.Forms.CheckBox chkAutoStartHide;
        internal System.Windows.Forms.Label lblLanguage;
        internal System.Windows.Forms.ComboBox cmbLanguage;
        internal System.Windows.Forms.Button btnOk;
        internal System.Windows.Forms.Button btnCancel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.chkAutoStart = new System.Windows.Forms.CheckBox();
            this.chkAutoStartModel = new System.Windows.Forms.CheckBox();
            this.chkAutoStartHide = new System.Windows.Forms.CheckBox();
            this.lblLanguage = new System.Windows.Forms.Label();
            this.cmbLanguage = new System.Windows.Forms.ComboBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // chkAutoStart
            //
            this.chkAutoStart.AutoSize = true;
            this.chkAutoStart.CheckAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.chkAutoStart.Location = new System.Drawing.Point(24, 22);
            this.chkAutoStart.Name = "chkAutoStart";
            this.chkAutoStart.Size = new System.Drawing.Size(160, 28);
            this.chkAutoStart.Text = "开机自启动";
            //
            // chkAutoStartModel
            //
            this.chkAutoStartModel.AutoSize = true;
            this.chkAutoStartModel.CheckAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.chkAutoStartModel.Location = new System.Drawing.Point(24, 60);
            this.chkAutoStartModel.Name = "chkAutoStartModel";
            this.chkAutoStartModel.Size = new System.Drawing.Size(320, 28);
            this.chkAutoStartModel.Text = "自启动后自动启动之前运行的模型";
            //
            // chkAutoStartHide
            //
            this.chkAutoStartHide.AutoSize = true;
            this.chkAutoStartHide.CheckAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.chkAutoStartHide.Location = new System.Drawing.Point(24, 98);
            this.chkAutoStartHide.Name = "chkAutoStartHide";
            this.chkAutoStartHide.Size = new System.Drawing.Size(320, 28);
            this.chkAutoStartHide.Text = "自启动后不显示主窗口";
            //
            // lblLanguage
            //
            this.lblLanguage.AutoSize = true;
            this.lblLanguage.Location = new System.Drawing.Point(24, 138);
            this.lblLanguage.Name = "lblLanguage";
            this.lblLanguage.Size = new System.Drawing.Size(60, 18);
            this.lblLanguage.Text = "语言：";
            //
            // cmbLanguage
            //
            this.cmbLanguage.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLanguage.Location = new System.Drawing.Point(90, 134);
            this.cmbLanguage.Name = "cmbLanguage";
            this.cmbLanguage.Size = new System.Drawing.Size(160, 26);
            //
            // btnOk
            //
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(254, 188);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(88, 30);
            this.btnOk.Text = "保存";
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(354, 188);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(88, 30);
            this.btnCancel.Text = "取消";
            //
            // AppSettingsForm
            //
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(456, 236);
            this.Controls.Add(this.chkAutoStart);
            this.Controls.Add(this.chkAutoStartModel);
            this.Controls.Add(this.chkAutoStartHide);
            this.Controls.Add(this.lblLanguage);
            this.Controls.Add(this.cmbLanguage);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AppSettingsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "应用设置";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
