namespace ooor
{
    partial class FileDownloadForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing) components?.Dispose();
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所必需的方法，不要修改。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuProxy = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextMenuRow = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripBtnStart = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnRedownload = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnAbout = new System.Windows.Forms.ToolStripButton();
            this.toolStripButtonCreate = new System.Windows.Forms.ToolStripButton();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.linkLabelProxy = new System.Windows.Forms.ToolStripLabel();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.flpRows = new ooor.Controls.NoAutoScrollPanel();
            this.toolStrip1.SuspendLayout();
            this.statusStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuProxy
            // 
            this.contextMenuProxy.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuProxy.Name = "contextMenuProxy";
            this.contextMenuProxy.Size = new System.Drawing.Size(61, 4);
            this.contextMenuProxy.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuProxy_Opening);
            // 
            // contextMenuRow
            // 
            this.contextMenuRow.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuRow.Name = "contextMenuRow";
            this.contextMenuRow.Size = new System.Drawing.Size(61, 4);
            this.contextMenuRow.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuRow_Opening);
            // 
            // toolStripBtnStart
            // 
            this.toolStripBtnStart.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnStart.Enabled = false;
            this.toolStripBtnStart.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripBtnStart.Name = "toolStripBtnStart";
            this.toolStripBtnStart.Size = new System.Drawing.Size(86, 28);
            this.toolStripBtnStart.Text = "开始下载";
            this.toolStripBtnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // toolStripBtnRedownload
            // 
            this.toolStripBtnRedownload.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnRedownload.Enabled = false;
            this.toolStripBtnRedownload.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripBtnRedownload.Name = "toolStripBtnRedownload";
            this.toolStripBtnRedownload.Size = new System.Drawing.Size(86, 28);
            this.toolStripBtnRedownload.Text = "重新下载";
            this.toolStripBtnRedownload.Click += new System.EventHandler(this.btnRedownload_Click);
            // 
            // toolStripBtnAbout
            // 
            this.toolStripBtnAbout.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnAbout.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripBtnAbout.Name = "toolStripBtnAbout";
            this.toolStripBtnAbout.Size = new System.Drawing.Size(50, 28);
            this.toolStripBtnAbout.Text = "关于";
            this.toolStripBtnAbout.Visible = false;
            // 
            // toolStripButtonCreate
            // 
            this.toolStripButtonCreate.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonCreate.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonCreate.Name = "toolStripButtonCreate";
            this.toolStripButtonCreate.Size = new System.Drawing.Size(50, 28);
            this.toolStripButtonCreate.Text = "新建";
            this.toolStripButtonCreate.Click += new System.EventHandler(this.toolStripButtonCreate_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonCreate,
            this.toolStripBtnStart,
            this.toolStripBtnRedownload,
            this.toolStripSeparator1,
            this.linkLabelProxy,
            this.toolStripSeparator2,
            this.toolStripBtnAbout});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1322, 33);
            this.toolStrip1.TabIndex = 17;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 33);
            // 
            // linkLabelProxy
            // 
            this.linkLabelProxy.IsLink = true;
            this.linkLabelProxy.Name = "linkLabelProxy";
            this.linkLabelProxy.Size = new System.Drawing.Size(163, 28);
            this.linkLabelProxy.Text = "加速：github.com";
            this.linkLabelProxy.Click += new System.EventHandler(this.linkLabelProxy_LinkClicked);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 33);
            // 
            // statusStrip1
            // 
            this.statusStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel1});
            this.statusStrip1.Location = new System.Drawing.Point(0, 558);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(1322, 31);
            this.statusStrip1.TabIndex = 21;
            this.statusStrip1.Text = "statusStrip2";
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(46, 24);
            this.toolStripStatusLabel1.Text = "就绪";
            // 
            // flpRows
            // 
            this.flpRows.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flpRows.AutoScroll = true;
            this.flpRows.BackColor = System.Drawing.Color.White;
            this.flpRows.Location = new System.Drawing.Point(0, 33);
            this.flpRows.Name = "flpRows";
            this.flpRows.Padding = new System.Windows.Forms.Padding(0, 4, 0, 8);
            this.flpRows.Size = new System.Drawing.Size(1322, 522);
            this.flpRows.TabIndex = 22;
            // 
            // FileDownloadForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1322, 589);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.flpRows);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(720, 320);
            this.Name = "FileDownloadForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "下载文件管理器";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.LlamaFileDownloadForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FileDownloadForm_FormClosed);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ooor.Controls.NoAutoScrollPanel flpRows;
        private System.Windows.Forms.ContextMenuStrip contextMenuProxy;
        private System.Windows.Forms.ContextMenuStrip contextMenuRow;
        private System.Windows.Forms.ToolStripButton toolStripButtonCreate;
        private System.Windows.Forms.ToolStripButton toolStripBtnStart;
        private System.Windows.Forms.ToolStripButton toolStripBtnRedownload;
        private System.Windows.Forms.ToolStripButton toolStripBtnAbout;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel linkLabelProxy;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
    }
}