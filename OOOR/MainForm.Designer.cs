namespace ooor
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// 注意：llama-server 由 ServerManager 全局单例持有，不随窗口 Dispose
        /// （关窗不停服务，下次开窗从 run_state.conf 恢复状态）。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // 只解绑事件，避免窗口关闭后回调打到已释放的控件
                    _server.LineReceived -= OnServerLine;
                    _server.ProcessExited -= OnServerExited;
                }
                catch { }
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所必需的方法，不要修改。
        /// 所有界面控件的声明、属性、布局都在此文件，可在 VS 设计器中可视化预览/调整。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.panelTop = new System.Windows.Forms.Panel();
            this.buttonChooseRunModel = new System.Windows.Forms.Button();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.btnMoreParams = new System.Windows.Forms.Button();
            this.txtHost = new System.Windows.Forms.TextBox();
            this.lblHost = new System.Windows.Forms.Label();
            this.txtNgl = new System.Windows.Forms.TextBox();
            this.lblNgl = new System.Windows.Forms.Label();
            this.cmbNPred = new System.Windows.Forms.ComboBox();
            this.lblNPred = new System.Windows.Forms.Label();
            this.cmbCtx = new System.Windows.Forms.ComboBox();
            this.lblCtx = new System.Windows.Forms.Label();
            this.cmbMmproj = new System.Windows.Forms.ComboBox();
            this.chkMmproj = new System.Windows.Forms.CheckBox();
            this.txtModel = new System.Windows.Forms.TextBox();
            this.lblModel = new System.Windows.Forms.Label();
            this.btnOpenChooseLlama = new System.Windows.Forms.Button();
            this.txtLlamaDir = new System.Windows.Forms.TextBox();
            this.lblDir = new System.Windows.Forms.Label();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripButtonRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.ToolStripMenuItemOpenConsole = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            this.btnStart = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSplitButton3 = new System.Windows.Forms.ToolStripSplitButton();
            this.ToolStripMenuItemConsoleTalkManager = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripButtonSaveSlu = new System.Windows.Forms.ToolStripButton();
            this.cmbProfiles = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripButtonDownloadForm = new System.Windows.Forms.ToolStripButton();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panelProgress = new System.Windows.Forms.Panel();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.llama服务ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.启动服务ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.复制OpenAPI地址ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
            this.llama原生控制台启动ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            this.管理版本ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.下载新版本ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            this.清空日志ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.模型ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.管理ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.下载新模型ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.控制台对话管理ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.agent管理ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.控制台ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.打开控制台AI助手ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.打开控制台AI助手继续聊ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.打开控制台AI助手开放权限ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            this.打开窗口AI助手ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.打开网页ToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.方案ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.保存方案ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.管理方案ToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.帮助ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.下载管理ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.ToolStripMenuItemSetting = new System.Windows.Forms.ToolStripMenuItem();
            this.关于ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelTop.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.panelProgress.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.buttonChooseRunModel);
            this.panelTop.Controls.Add(this.txtPort);
            this.panelTop.Controls.Add(this.lblPort);
            this.panelTop.Controls.Add(this.btnMoreParams);
            this.panelTop.Controls.Add(this.txtHost);
            this.panelTop.Controls.Add(this.lblHost);
            this.panelTop.Controls.Add(this.txtNgl);
            this.panelTop.Controls.Add(this.lblNgl);
            this.panelTop.Controls.Add(this.cmbNPred);
            this.panelTop.Controls.Add(this.lblNPred);
            this.panelTop.Controls.Add(this.cmbCtx);
            this.panelTop.Controls.Add(this.lblCtx);
            this.panelTop.Controls.Add(this.cmbMmproj);
            this.panelTop.Controls.Add(this.chkMmproj);
            this.panelTop.Controls.Add(this.txtModel);
            this.panelTop.Controls.Add(this.lblModel);
            this.panelTop.Controls.Add(this.btnOpenChooseLlama);
            this.panelTop.Controls.Add(this.txtLlamaDir);
            this.panelTop.Controls.Add(this.lblDir);
            this.panelTop.Location = new System.Drawing.Point(0, 71);
            this.panelTop.Margin = new System.Windows.Forms.Padding(4);
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(15, 12, 15, 10);
            this.panelTop.Size = new System.Drawing.Size(1138, 270);
            this.panelTop.TabIndex = 0;
            // 
            // buttonChooseRunModel
            // 
            this.buttonChooseRunModel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonChooseRunModel.Location = new System.Drawing.Point(907, 105);
            this.buttonChooseRunModel.Margin = new System.Windows.Forms.Padding(4);
            this.buttonChooseRunModel.Name = "buttonChooseRunModel";
            this.buttonChooseRunModel.Size = new System.Drawing.Size(114, 31);
            this.buttonChooseRunModel.TabIndex = 20;
            this.buttonChooseRunModel.Text = "选择模型";
            this.buttonChooseRunModel.UseVisualStyleBackColor = true;
            this.buttonChooseRunModel.Click += new System.EventHandler(this.buttonChooseRunModel_Click);
            // 
            // txtPort
            // 
            this.txtPort.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtPort.Location = new System.Drawing.Point(623, 213);
            this.txtPort.Margin = new System.Windows.Forms.Padding(4);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(135, 31);
            this.txtPort.TabIndex = 19;
            this.txtPort.Text = "6080";
            // 
            // lblPort
            // 
            this.lblPort.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPort.Location = new System.Drawing.Point(620, 185);
            this.lblPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(138, 24);
            this.lblPort.TabIndex = 18;
            this.lblPort.Text = "端口:";
            this.lblPort.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnMoreParams
            // 
            this.btnMoreParams.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnMoreParams.Location = new System.Drawing.Point(766, 213);
            this.btnMoreParams.Margin = new System.Windows.Forms.Padding(4);
            this.btnMoreParams.Name = "btnMoreParams";
            this.btnMoreParams.Size = new System.Drawing.Size(115, 31);
            this.btnMoreParams.TabIndex = 21;
            this.btnMoreParams.Text = "更多";
            this.btnMoreParams.UseVisualStyleBackColor = true;
            this.btnMoreParams.Click += new System.EventHandler(this.btnMoreParams_Click);
            // 
            // txtHost
            // 
            this.txtHost.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtHost.Location = new System.Drawing.Point(475, 213);
            this.txtHost.Margin = new System.Windows.Forms.Padding(4);
            this.txtHost.Name = "txtHost";
            this.txtHost.Size = new System.Drawing.Size(140, 31);
            this.txtHost.TabIndex = 17;
            this.txtHost.Text = "127.0.0.1";
            // 
            // lblHost
            // 
            this.lblHost.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblHost.Location = new System.Drawing.Point(472, 185);
            this.lblHost.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblHost.Name = "lblHost";
            this.lblHost.Size = new System.Drawing.Size(140, 24);
            this.lblHost.TabIndex = 16;
            this.lblHost.Text = "主机:";
            this.lblHost.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtNgl
            // 
            this.txtNgl.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtNgl.Location = new System.Drawing.Point(349, 213);
            this.txtNgl.Margin = new System.Windows.Forms.Padding(4);
            this.txtNgl.Name = "txtNgl";
            this.txtNgl.Size = new System.Drawing.Size(118, 31);
            this.txtNgl.TabIndex = 15;
            this.txtNgl.Text = "0";
            // 
            // lblNgl
            // 
            this.lblNgl.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblNgl.Location = new System.Drawing.Point(346, 185);
            this.lblNgl.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblNgl.Name = "lblNgl";
            this.lblNgl.Size = new System.Drawing.Size(105, 24);
            this.lblNgl.TabIndex = 14;
            this.lblNgl.Text = "GPU层 -ngl:";
            this.lblNgl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbNPred
            // 
            this.cmbNPred.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbNPred.FormattingEnabled = true;
            this.cmbNPred.Items.AddRange(new object[] {
            "--默认--",
            "1024",
            "2048",
            "4096",
            "8192",
            "16384"});
            this.cmbNPred.Location = new System.Drawing.Point(175, 213);
            this.cmbNPred.Margin = new System.Windows.Forms.Padding(4);
            this.cmbNPred.Name = "cmbNPred";
            this.cmbNPred.Size = new System.Drawing.Size(166, 33);
            this.cmbNPred.TabIndex = 13;
            this.cmbNPred.Text = "--默认--";
            // 
            // lblNPred
            // 
            this.lblNPred.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblNPred.Location = new System.Drawing.Point(172, 185);
            this.lblNPred.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblNPred.Name = "lblNPred";
            this.lblNPred.Size = new System.Drawing.Size(166, 24);
            this.lblNPred.TabIndex = 12;
            this.lblNPred.Text = "预测长度 -n:";
            this.lblNPred.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbCtx
            // 
            this.cmbCtx.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbCtx.FormattingEnabled = true;
            this.cmbCtx.Items.AddRange(new object[] {
            "--默认--",
            "4096",
            "8192",
            "16384",
            "32768",
            "65536",
            "131072"});
            this.cmbCtx.Location = new System.Drawing.Point(19, 213);
            this.cmbCtx.Margin = new System.Windows.Forms.Padding(4);
            this.cmbCtx.Name = "cmbCtx";
            this.cmbCtx.Size = new System.Drawing.Size(148, 33);
            this.cmbCtx.TabIndex = 11;
            this.cmbCtx.Text = "--默认--";
            // 
            // lblCtx
            // 
            this.lblCtx.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCtx.Location = new System.Drawing.Point(15, 185);
            this.lblCtx.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblCtx.Name = "lblCtx";
            this.lblCtx.Size = new System.Drawing.Size(152, 24);
            this.lblCtx.TabIndex = 10;
            this.lblCtx.Text = "上下文 -c:";
            this.lblCtx.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbMmproj
            // 
            this.cmbMmproj.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMmproj.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbMmproj.FormattingEnabled = true;
            this.cmbMmproj.Location = new System.Drawing.Point(245, 140);
            this.cmbMmproj.Margin = new System.Windows.Forms.Padding(4);
            this.cmbMmproj.Name = "cmbMmproj";
            this.cmbMmproj.Size = new System.Drawing.Size(636, 33);
            this.cmbMmproj.TabIndex = 9;
            this.cmbMmproj.Visible = false;
            this.cmbMmproj.SelectedIndexChanged += new System.EventHandler(this.cmbMmproj_SelectedIndexChanged);
            // 
            // chkMmproj
            // 
            this.chkMmproj.Enabled = false;
            this.chkMmproj.Location = new System.Drawing.Point(18, 146);
            this.chkMmproj.Margin = new System.Windows.Forms.Padding(4);
            this.chkMmproj.Name = "chkMmproj";
            this.chkMmproj.Size = new System.Drawing.Size(219, 26);
            this.chkMmproj.TabIndex = 8;
            this.chkMmproj.Text = "多模态投影 (mmproj)";
            this.chkMmproj.UseVisualStyleBackColor = true;
            this.chkMmproj.CheckedChanged += new System.EventHandler(this.chkMmproj_CheckedChanged);
            // 
            // txtModel
            // 
            this.txtModel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtModel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtModel.Location = new System.Drawing.Point(18, 103);
            this.txtModel.Margin = new System.Windows.Forms.Padding(4);
            this.txtModel.Name = "txtModel";
            this.txtModel.ReadOnly = true;
            this.txtModel.Size = new System.Drawing.Size(863, 31);
            this.txtModel.TabIndex = 5;
            this.txtModel.TabStop = false;
            // 
            // lblModel
            // 
            this.lblModel.Location = new System.Drawing.Point(19, 75);
            this.lblModel.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(123, 24);
            this.lblModel.TabIndex = 4;
            this.lblModel.Text = "模型:";
            this.lblModel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnOpenChooseLlama
            // 
            this.btnOpenChooseLlama.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpenChooseLlama.Location = new System.Drawing.Point(907, 41);
            this.btnOpenChooseLlama.Margin = new System.Windows.Forms.Padding(4);
            this.btnOpenChooseLlama.Name = "btnOpenChooseLlama";
            this.btnOpenChooseLlama.Size = new System.Drawing.Size(114, 31);
            this.btnOpenChooseLlama.TabIndex = 3;
            this.btnOpenChooseLlama.Text = "选择llama";
            this.btnOpenChooseLlama.UseVisualStyleBackColor = true;
            this.btnOpenChooseLlama.Click += new System.EventHandler(this.btnOpenChooseLlama_Click);
            // 
            // txtLlamaDir
            // 
            this.txtLlamaDir.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLlamaDir.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtLlamaDir.Location = new System.Drawing.Point(18, 39);
            this.txtLlamaDir.Margin = new System.Windows.Forms.Padding(4);
            this.txtLlamaDir.Name = "txtLlamaDir";
            this.txtLlamaDir.ReadOnly = true;
            this.txtLlamaDir.Size = new System.Drawing.Size(863, 31);
            this.txtLlamaDir.TabIndex = 1;
            this.txtLlamaDir.TabStop = false;
            // 
            // lblDir
            // 
            this.lblDir.Location = new System.Drawing.Point(19, 12);
            this.lblDir.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDir.Name = "lblDir";
            this.lblDir.Size = new System.Drawing.Size(127, 23);
            this.lblDir.TabIndex = 0;
            this.lblDir.Text = "llama版本:";
            this.lblDir.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtLog
            // 
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.BackColor = System.Drawing.Color.Black;
            this.txtLog.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.txtLog.ForeColor = System.Drawing.Color.Silver;
            this.txtLog.Location = new System.Drawing.Point(4, 4);
            this.txtLog.Margin = new System.Windows.Forms.Padding(4);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtLog.Size = new System.Drawing.Size(1235, 520);
            this.txtLog.TabIndex = 1;
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 877);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 18, 0);
            this.statusStrip.Size = new System.Drawing.Size(1252, 31);
            this.statusStrip.TabIndex = 2;
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(1233, 24);
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "就绪";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonRefresh,
            this.toolStripSeparator2,
            this.ToolStripMenuItemOpenConsole,
            this.toolStripSeparator6,
            this.btnStart,
            this.toolStripSeparator1,
            this.toolStripSplitButton3,
            this.toolStripSeparator4,
            this.toolStripButtonSaveSlu,
            this.cmbProfiles,
            this.toolStripSeparator3,
            this.toolStripButtonDownloadForm});
            this.toolStrip1.Location = new System.Drawing.Point(0, 32);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.Size = new System.Drawing.Size(1252, 33);
            this.toolStrip1.TabIndex = 25;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButtonRefresh
            // 
            this.toolStripButtonRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonRefresh.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonRefresh.Name = "toolStripButtonRefresh";
            this.toolStripButtonRefresh.Size = new System.Drawing.Size(50, 28);
            this.toolStripButtonRefresh.Text = "刷新";
            this.toolStripButtonRefresh.Click += new System.EventHandler(this.btnReload_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 33);
            // 
            // ToolStripMenuItemOpenConsole
            // 
            this.ToolStripMenuItemOpenConsole.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.ToolStripMenuItemOpenConsole.Image = ((System.Drawing.Image)(resources.GetObject("ToolStripMenuItemOpenConsole.Image")));
            this.ToolStripMenuItemOpenConsole.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ToolStripMenuItemOpenConsole.Name = "ToolStripMenuItemOpenConsole";
            this.ToolStripMenuItemOpenConsole.Size = new System.Drawing.Size(158, 28);
            this.ToolStripMenuItemOpenConsole.Text = "打开控制台AI助手";
            this.ToolStripMenuItemOpenConsole.ToolTipText = "服务启动成功后才可点击";
            this.ToolStripMenuItemOpenConsole.Click += new System.EventHandler(this.ToolStripMenuItemOpenCli_Click);
            // 
            // toolStripSeparator6
            // 
            this.toolStripSeparator6.Name = "toolStripSeparator6";
            this.toolStripSeparator6.Size = new System.Drawing.Size(6, 33);
            // 
            // btnStart
            // 
            this.btnStart.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnStart.Image = ((System.Drawing.Image)(resources.GetObject("btnStart.Image")));
            this.btnStart.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(86, 28);
            this.btnStart.Text = "启动服务";
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 33);
            // 
            // toolStripSplitButton3
            // 
            this.toolStripSplitButton3.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripSplitButton3.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ToolStripMenuItemConsoleTalkManager});
            this.toolStripSplitButton3.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripSplitButton3.Name = "toolStripSplitButton3";
            this.toolStripSplitButton3.Size = new System.Drawing.Size(120, 28);
            this.toolStripSplitButton3.Text = "Agent管理";
            this.toolStripSplitButton3.ButtonClick += new System.EventHandler(this.toolStripSplitButton3_ButtonClick);
            // 
            // ToolStripMenuItemConsoleTalkManager
            // 
            this.ToolStripMenuItemConsoleTalkManager.Name = "ToolStripMenuItemConsoleTalkManager";
            this.ToolStripMenuItemConsoleTalkManager.Size = new System.Drawing.Size(236, 34);
            this.ToolStripMenuItemConsoleTalkManager.Text = "控制台对话管理";
            this.ToolStripMenuItemConsoleTalkManager.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(6, 33);
            // 
            // toolStripButtonSaveSlu
            // 
            this.toolStripButtonSaveSlu.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonSaveSlu.Image = ((System.Drawing.Image)(resources.GetObject("toolStripButtonSaveSlu.Image")));
            this.toolStripButtonSaveSlu.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonSaveSlu.Name = "toolStripButtonSaveSlu";
            this.toolStripButtonSaveSlu.Size = new System.Drawing.Size(86, 28);
            this.toolStripButtonSaveSlu.Text = "保存方案";
            this.toolStripButtonSaveSlu.ToolTipText = "把当前启动参数保存为方案：未选方案=另存为，已选方案=覆盖";
            this.toolStripButtonSaveSlu.Click += new System.EventHandler(this.toolStripButtonSaveSlu_Click);
            // 
            // cmbProfiles
            // 
            this.cmbProfiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProfiles.Name = "cmbProfiles";
            this.cmbProfiles.Size = new System.Drawing.Size(150, 33);
            this.cmbProfiles.ToolTipText = "选择参数方案，自动填入下方启动参数";
            this.cmbProfiles.SelectedIndexChanged += new System.EventHandler(this.cmbProfiles_SelectedIndexChanged);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 33);
            // 
            // toolStripButtonDownloadForm
            // 
            this.toolStripButtonDownloadForm.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonDownloadForm.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonDownloadForm.Name = "toolStripButtonDownloadForm";
            this.toolStripButtonDownloadForm.Size = new System.Drawing.Size(86, 28);
            this.toolStripButtonDownloadForm.Text = "下载任务";
            this.toolStripButtonDownloadForm.Click += new System.EventHandler(this.toolStripButtonDownloadForm_Click);
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.ForeColor = System.Drawing.Color.Black;
            this.progressBar1.Location = new System.Drawing.Point(32, 21);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(1183, 78);
            this.progressBar1.Step = 5;
            this.progressBar1.TabIndex = 26;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.panelProgress);
            this.panel1.Controls.Add(this.txtLog);
            this.panel1.Location = new System.Drawing.Point(0, 346);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1252, 528);
            this.panel1.TabIndex = 27;
            // 
            // panelProgress
            // 
            this.panelProgress.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelProgress.Controls.Add(this.progressBar1);
            this.panelProgress.Location = new System.Drawing.Point(4, 4);
            this.panelProgress.Name = "panelProgress";
            this.panelProgress.Size = new System.Drawing.Size(1245, 120);
            this.panelProgress.TabIndex = 27;
            // 
            // menuStrip1
            // 
            this.menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.llama服务ToolStripMenuItem,
            this.模型ToolStripMenuItem,
            this.控制台ToolStripMenuItem,
            this.方案ToolStripMenuItem,
            this.帮助ToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(1252, 32);
            this.menuStrip1.TabIndex = 28;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // llama服务ToolStripMenuItem
            // 
            this.llama服务ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.启动服务ToolStripMenuItem,
            this.复制OpenAPI地址ToolStripMenuItem,
            this.toolStripSeparator10,
            this.llama原生控制台启动ToolStripMenuItem,
            this.toolStripSeparator13,
            this.管理版本ToolStripMenuItem,
            this.下载新版本ToolStripMenuItem,
            this.toolStripSeparator12,
            this.清空日志ToolStripMenuItem});
            this.llama服务ToolStripMenuItem.Name = "llama服务ToolStripMenuItem";
            this.llama服务ToolStripMenuItem.Size = new System.Drawing.Size(98, 28);
            this.llama服务ToolStripMenuItem.Text = "Llama(&L)";
            // 
            // 启动服务ToolStripMenuItem
            // 
            this.启动服务ToolStripMenuItem.Name = "启动服务ToolStripMenuItem";
            this.启动服务ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F9;
            this.启动服务ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.启动服务ToolStripMenuItem.Text = "启动服务";
            this.启动服务ToolStripMenuItem.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // 复制OpenAPI地址ToolStripMenuItem
            // 
            this.复制OpenAPI地址ToolStripMenuItem.Name = "复制OpenAPI地址ToolStripMenuItem";
            this.复制OpenAPI地址ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.C)));
            this.复制OpenAPI地址ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.复制OpenAPI地址ToolStripMenuItem.Text = "复制OpenAPI地址";
            this.复制OpenAPI地址ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemCopyApiAddr_Click);
            // 
            // toolStripSeparator10
            // 
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            this.toolStripSeparator10.Size = new System.Drawing.Size(401, 6);
            // 
            // llama原生控制台启动ToolStripMenuItem
            // 
            this.llama原生控制台启动ToolStripMenuItem.Name = "llama原生控制台启动ToolStripMenuItem";
            this.llama原生控制台启动ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.N)));
            this.llama原生控制台启动ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.llama原生控制台启动ToolStripMenuItem.Text = "llama原生控制台启动";
            this.llama原生控制台启动ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemRunCli_Click);
            // 
            // toolStripSeparator13
            // 
            this.toolStripSeparator13.Name = "toolStripSeparator13";
            this.toolStripSeparator13.Size = new System.Drawing.Size(401, 6);
            // 
            // 管理版本ToolStripMenuItem
            // 
            this.管理版本ToolStripMenuItem.Name = "管理版本ToolStripMenuItem";
            this.管理版本ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt) 
            | System.Windows.Forms.Keys.V)));
            this.管理版本ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.管理版本ToolStripMenuItem.Text = "管理版本";
            this.管理版本ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonDownloadLlama_Click);
            // 
            // 下载新版本ToolStripMenuItem
            // 
            this.下载新版本ToolStripMenuItem.Name = "下载新版本ToolStripMenuItem";
            this.下载新版本ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.V)));
            this.下载新版本ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.下载新版本ToolStripMenuItem.Text = "下载新版本";
            this.下载新版本ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemDownloadNewLlama_Click);
            // 
            // toolStripSeparator12
            // 
            this.toolStripSeparator12.Name = "toolStripSeparator12";
            this.toolStripSeparator12.Size = new System.Drawing.Size(401, 6);
            // 
            // 清空日志ToolStripMenuItem
            // 
            this.清空日志ToolStripMenuItem.Name = "清空日志ToolStripMenuItem";
            this.清空日志ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.L)));
            this.清空日志ToolStripMenuItem.Size = new System.Drawing.Size(404, 34);
            this.清空日志ToolStripMenuItem.Text = "清空日志";
            this.清空日志ToolStripMenuItem.Click += new System.EventHandler(this.btnClearLog_Click);
            // 
            // 模型ToolStripMenuItem
            // 
            this.模型ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.管理ToolStripMenuItem,
            this.下载新模型ToolStripMenuItem,
            this.toolStripSeparator7,
            this.控制台对话管理ToolStripMenuItem,
            this.toolStripSeparator5,
            this.agent管理ToolStripMenuItem});
            this.模型ToolStripMenuItem.Name = "模型ToolStripMenuItem";
            this.模型ToolStripMenuItem.Size = new System.Drawing.Size(92, 28);
            this.模型ToolStripMenuItem.Text = "模型(&M)";
            // 
            // 管理ToolStripMenuItem
            // 
            this.管理ToolStripMenuItem.Name = "管理ToolStripMenuItem";
            this.管理ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.M)));
            this.管理ToolStripMenuItem.Size = new System.Drawing.Size(356, 34);
            this.管理ToolStripMenuItem.Text = "管理";
            this.管理ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonManageModels_Click);
            // 
            // 下载新模型ToolStripMenuItem
            // 
            this.下载新模型ToolStripMenuItem.Name = "下载新模型ToolStripMenuItem";
            this.下载新模型ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.下载新模型ToolStripMenuItem.Size = new System.Drawing.Size(356, 34);
            this.下载新模型ToolStripMenuItem.Text = "下载新模型";
            this.下载新模型ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonDownloadModels_Click);
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            this.toolStripSeparator7.Size = new System.Drawing.Size(353, 6);
            // 
            // 控制台对话管理ToolStripMenuItem
            // 
            this.控制台对话管理ToolStripMenuItem.Name = "控制台对话管理ToolStripMenuItem";
            this.控制台对话管理ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.H)));
            this.控制台对话管理ToolStripMenuItem.Size = new System.Drawing.Size(356, 34);
            this.控制台对话管理ToolStripMenuItem.Text = "控制台对话管理";
            this.控制台对话管理ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItem_Click);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(353, 6);
            // 
            // agent管理ToolStripMenuItem
            // 
            this.agent管理ToolStripMenuItem.Name = "agent管理ToolStripMenuItem";
            this.agent管理ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.J)));
            this.agent管理ToolStripMenuItem.Size = new System.Drawing.Size(356, 34);
            this.agent管理ToolStripMenuItem.Text = "Agent管理";
            this.agent管理ToolStripMenuItem.Click += new System.EventHandler(this.toolStripSplitButton3_ButtonClick);
            // 
            // 控制台ToolStripMenuItem
            // 
            this.控制台ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.打开控制台AI助手ToolStripMenuItem,
            this.打开控制台AI助手继续聊ToolStripMenuItem,
            this.打开控制台AI助手开放权限ToolStripMenuItem,
            this.toolStripSeparator14,
            this.打开窗口AI助手ToolStripMenuItem,
            this.toolStripSeparator11,
            this.打开网页ToolStripMenuItem1});
            this.控制台ToolStripMenuItem.Name = "控制台ToolStripMenuItem";
            this.控制台ToolStripMenuItem.Size = new System.Drawing.Size(104, 28);
            this.控制台ToolStripMenuItem.Text = "控制台(&C)";
            // 
            // 打开控制台AI助手ToolStripMenuItem
            // 
            this.打开控制台AI助手ToolStripMenuItem.Name = "打开控制台AI助手ToolStripMenuItem";
            this.打开控制台AI助手ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.A)));
            this.打开控制台AI助手ToolStripMenuItem.Size = new System.Drawing.Size(483, 34);
            this.打开控制台AI助手ToolStripMenuItem.Text = "打开控制台AI助手";
            this.打开控制台AI助手ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemOpenCli_Click);
            // 
            // 打开控制台AI助手继续聊ToolStripMenuItem
            // 
            this.打开控制台AI助手继续聊ToolStripMenuItem.Name = "打开控制台AI助手继续聊ToolStripMenuItem";
            this.打开控制台AI助手继续聊ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Alt) 
            | System.Windows.Forms.Keys.A)));
            this.打开控制台AI助手继续聊ToolStripMenuItem.Size = new System.Drawing.Size(483, 34);
            this.打开控制台AI助手继续聊ToolStripMenuItem.Text = "打开控制台AI助手（继续聊）";
            this.打开控制台AI助手继续聊ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemOpenConsoleUseAgent_Click);
            // 
            // 打开控制台AI助手开放权限ToolStripMenuItem
            // 
            this.打开控制台AI助手开放权限ToolStripMenuItem.Name = "打开控制台AI助手开放权限ToolStripMenuItem";
            this.打开控制台AI助手开放权限ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.O)));
            this.打开控制台AI助手开放权限ToolStripMenuItem.Size = new System.Drawing.Size(483, 34);
            this.打开控制台AI助手开放权限ToolStripMenuItem.Text = "打开控制台AI助手（开放权限）";
            this.打开控制台AI助手开放权限ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemOpenAllPer_Click);
            // 
            // toolStripSeparator14
            // 
            this.toolStripSeparator14.Name = "toolStripSeparator14";
            this.toolStripSeparator14.Size = new System.Drawing.Size(480, 6);
            // 
            // 打开窗口AI助手ToolStripMenuItem
            // 
            this.打开窗口AI助手ToolStripMenuItem.Name = "打开窗口AI助手ToolStripMenuItem";
            this.打开窗口AI助手ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.W)));
            this.打开窗口AI助手ToolStripMenuItem.Size = new System.Drawing.Size(483, 34);
            this.打开窗口AI助手ToolStripMenuItem.Text = "打开窗口AI 助手";
            this.打开窗口AI助手ToolStripMenuItem.Click += new System.EventHandler(this.ToolStripMenuItemRunAgent_Click);
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            this.toolStripSeparator11.Size = new System.Drawing.Size(480, 6);
            // 
            // 打开网页ToolStripMenuItem1
            // 
            this.打开网页ToolStripMenuItem1.Name = "打开网页ToolStripMenuItem1";
            this.打开网页ToolStripMenuItem1.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.G)));
            this.打开网页ToolStripMenuItem1.Size = new System.Drawing.Size(483, 34);
            this.打开网页ToolStripMenuItem1.Text = "打开网页";
            this.打开网页ToolStripMenuItem1.Click += new System.EventHandler(this.btnOpenWeb_Click);
            // 
            // 方案ToolStripMenuItem
            // 
            this.方案ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.保存方案ToolStripMenuItem,
            this.管理方案ToolStripMenuItem1});
            this.方案ToolStripMenuItem.Name = "方案ToolStripMenuItem";
            this.方案ToolStripMenuItem.Size = new System.Drawing.Size(85, 28);
            this.方案ToolStripMenuItem.Text = "方案(&P)";
            // 
            // 保存方案ToolStripMenuItem
            // 
            this.保存方案ToolStripMenuItem.Name = "保存方案ToolStripMenuItem";
            this.保存方案ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.保存方案ToolStripMenuItem.Size = new System.Drawing.Size(247, 34);
            this.保存方案ToolStripMenuItem.Text = "保存方案";
            this.保存方案ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonSaveSlu_Click);
            // 
            // 管理方案ToolStripMenuItem1
            // 
            this.管理方案ToolStripMenuItem1.Name = "管理方案ToolStripMenuItem1";
            this.管理方案ToolStripMenuItem1.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.P)));
            this.管理方案ToolStripMenuItem1.Size = new System.Drawing.Size(247, 34);
            this.管理方案ToolStripMenuItem1.Text = "管理方案";
            this.管理方案ToolStripMenuItem1.Click += new System.EventHandler(this.toolStripButtonManageProfiles_Click);
            // 
            // 帮助ToolStripMenuItem
            // 
            this.帮助ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.下载管理ToolStripMenuItem,
            this.toolStripSeparator8,
            this.ToolStripMenuItemSetting,
            this.关于ToolStripMenuItem});
            this.帮助ToolStripMenuItem.Name = "帮助ToolStripMenuItem";
            this.帮助ToolStripMenuItem.Size = new System.Drawing.Size(88, 28);
            this.帮助ToolStripMenuItem.Text = "更多(&H)";
            // 
            // 下载管理ToolStripMenuItem
            // 
            this.下载管理ToolStripMenuItem.Name = "下载管理ToolStripMenuItem";
            this.下载管理ToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.F)));
            this.下载管理ToolStripMenuItem.Size = new System.Drawing.Size(306, 34);
            this.下载管理ToolStripMenuItem.Text = "下载管理";
            this.下载管理ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonDownloadForm_Click);
            // 
            // toolStripSeparator8
            // 
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            this.toolStripSeparator8.Size = new System.Drawing.Size(303, 6);
            // 
            // ToolStripMenuItemSetting
            // 
            this.ToolStripMenuItemSetting.Name = "ToolStripMenuItemSetting";
            this.ToolStripMenuItemSetting.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Oemcomma)));
            this.ToolStripMenuItemSetting.Size = new System.Drawing.Size(306, 34);
            this.ToolStripMenuItemSetting.Text = "设置";
            this.ToolStripMenuItemSetting.Click += new System.EventHandler(this.ToolStripMenuItemSetting_Click);
            // 
            // 关于ToolStripMenuItem
            // 
            this.关于ToolStripMenuItem.Name = "关于ToolStripMenuItem";
            this.关于ToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.F1;
            this.关于ToolStripMenuItem.Size = new System.Drawing.Size(306, 34);
            this.关于ToolStripMenuItem.Text = "关于";
            this.关于ToolStripMenuItem.Click += new System.EventHandler(this.toolStripButtonAbout_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1252, 908);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(919, 493);
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "欧尔模型坞 (Ooor.cc) - Ver(0.0.2.5)";
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panelProgress.ResumeLayout(false);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Label lblDir;
        private System.Windows.Forms.TextBox txtLlamaDir;
        private System.Windows.Forms.Button btnOpenChooseLlama;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.TextBox txtModel;
        private System.Windows.Forms.CheckBox chkMmproj;
        private System.Windows.Forms.ComboBox cmbMmproj;
        private System.Windows.Forms.Label lblCtx;
        private System.Windows.Forms.ComboBox cmbCtx;
        private System.Windows.Forms.Label lblNPred;
        private System.Windows.Forms.ComboBox cmbNPred;
        private System.Windows.Forms.Label lblNgl;
        private System.Windows.Forms.TextBox txtNgl;
        private System.Windows.Forms.Label lblHost;
        private System.Windows.Forms.TextBox txtHost;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Button btnMoreParams;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripComboBox cmbProfiles;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.Button buttonChooseRunModel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripButton toolStripButtonDownloadForm;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
        private System.Windows.Forms.ToolStripSplitButton toolStripSplitButton3;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemConsoleTalkManager;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panelProgress;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 控制台ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem llama服务ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 模型ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 方案ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 帮助ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemSetting;
        private System.Windows.Forms.ToolStripMenuItem 关于ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 管理版本ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 下载新版本ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 管理ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 下载新模型ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 启动服务ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem llama原生控制台启动ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 清空日志ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator10;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
        private System.Windows.Forms.ToolStripMenuItem 复制OpenAPI地址ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
        private System.Windows.Forms.ToolStripButton btnStart;
        private System.Windows.Forms.ToolStripButton toolStripButtonSaveSlu;
        private System.Windows.Forms.ToolStripMenuItem 保存方案ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 管理方案ToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem agent管理ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 控制台对话管理ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
        private System.Windows.Forms.ToolStripMenuItem 打开控制台AI助手ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 打开控制台AI助手继续聊ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 打开窗口AI助手ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 打开网页ToolStripMenuItem1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private System.Windows.Forms.ToolStripMenuItem 打开控制台AI助手开放权限ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
        private System.Windows.Forms.ToolStripButton ToolStripMenuItemOpenConsole;
        private System.Windows.Forms.ToolStripMenuItem 下载管理ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripButton toolStripButtonRefresh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
    }
}
