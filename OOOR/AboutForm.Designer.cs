namespace ooor
{
    partial class AboutForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblVersion = new System.Windows.Forms.Label();
            this.lblDesc = new System.Windows.Forms.Label();
            this.lblDirs = new System.Windows.Forms.Label();
            this.lblUpdateState = new System.Windows.Forms.Label();
            this.lblUpdateInfo = new System.Windows.Forms.Label();
            this.lnkLatest = new System.Windows.Forms.LinkLabel();
            this.btnOk = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(16, 16);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(385, 36);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "本地大语言模型管理 (Ooor.cc)";
            // 
            // lblVersion
            // 
            this.lblVersion.AutoSize = true;
            this.lblVersion.ForeColor = System.Drawing.Color.Gray;
            this.lblVersion.Location = new System.Drawing.Point(18, 48);
            this.lblVersion.Name = "lblVersion";
            this.lblVersion.Size = new System.Drawing.Size(80, 18);
            this.lblVersion.TabIndex = 1;
            this.lblVersion.Text = "当前版本";
            // 
            // lblDesc
            // 
            this.lblDesc.AutoSize = true;
            this.lblDesc.Location = new System.Drawing.Point(18, 82);
            this.lblDesc.MaximumSize = new System.Drawing.Size(446, 0);
            this.lblDesc.Name = "lblDesc";
            this.lblDesc.Size = new System.Drawing.Size(440, 180);
            this.lblDesc.TabIndex = 2;
            this.lblDesc.Text = "llama.cpp && GGUF 模型管理软件：纯本地运行，轻量便携，管理必备，动态下载 llama 运行版本与模型文件，一键启动模型（OpenAI 兼容接口）" +
    "。\r\n\r\n识别多个 llama.cpp 发行版，一键启动，并实时显示日志；支持模型管理（备注、软删除、引用目录）与参数方案。\r\n\r\n服务进程独立于窗口：关闭窗口" +
    "不停止服务，重开窗口自动恢复运行。";
            // 
            // lblDirs
            // 
            this.lblDirs.AutoSize = true;
            this.lblDirs.ForeColor = System.Drawing.Color.Gray;
            this.lblDirs.Location = new System.Drawing.Point(18, 244);
            this.lblDirs.MaximumSize = new System.Drawing.Size(446, 0);
            this.lblDirs.Name = "lblDirs";
            this.lblDirs.Size = new System.Drawing.Size(98, 18);
            this.lblDirs.TabIndex = 3;
            this.lblDirs.Text = "配置目录：";
            // 
            // lblUpdateState
            // 
            this.lblUpdateState.AutoSize = true;
            this.lblUpdateState.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblUpdateState.Location = new System.Drawing.Point(18, 335);
            this.lblUpdateState.Name = "lblUpdateState";
            this.lblUpdateState.Size = new System.Drawing.Size(148, 25);
            this.lblUpdateState.TabIndex = 4;
            this.lblUpdateState.Text = "正在检查更新…";
            // 
            // lblUpdateInfo
            // 
            this.lblUpdateInfo.AutoSize = true;
            this.lblUpdateInfo.Location = new System.Drawing.Point(18, 322);
            this.lblUpdateInfo.MaximumSize = new System.Drawing.Size(446, 0);
            this.lblUpdateInfo.Name = "lblUpdateInfo";
            this.lblUpdateInfo.Size = new System.Drawing.Size(0, 18);
            this.lblUpdateInfo.TabIndex = 5;
            // 
            // lnkLatest
            // 
            this.lnkLatest.AutoSize = true;
            this.lnkLatest.Location = new System.Drawing.Point(18, 412);
            this.lnkLatest.Name = "lnkLatest";
            this.lnkLatest.Size = new System.Drawing.Size(152, 18);
            this.lnkLatest.TabIndex = 6;
            this.lnkLatest.TabStop = true;
            this.lnkLatest.Text = "前往官网查看更新";
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(376, 440);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(88, 30);
            this.btnOk.TabIndex = 7;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // AboutForm
            // 
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnOk;
            this.ClientSize = new System.Drawing.Size(480, 484);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblVersion);
            this.Controls.Add(this.lblDesc);
            this.Controls.Add(this.lblDirs);
            this.Controls.Add(this.lblUpdateState);
            this.Controls.Add(this.lblUpdateInfo);
            this.Controls.Add(this.lnkLatest);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AboutForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "关于";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblVersion;
        private System.Windows.Forms.Label lblDesc;
        private System.Windows.Forms.Label lblDirs;
        private System.Windows.Forms.Label lblUpdateState;
        private System.Windows.Forms.Label lblUpdateInfo;
        private System.Windows.Forms.LinkLabel lnkLatest;
        private System.Windows.Forms.Button btnOk;
    }
}
