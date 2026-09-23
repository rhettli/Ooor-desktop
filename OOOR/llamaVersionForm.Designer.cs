namespace ooor
{
    partial class llamaVersionForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components;

        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem miDisableVersion;
        private System.Windows.Forms.ToolStripMenuItem miOpenDir;
        private System.Windows.Forms.ToolStripMenuItem miRefresh;
        private System.Windows.Forms.ToolStripMenuItem miInstallNew;
        private System.Windows.Forms.ToolStripMenuItem miDeleteVersion;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.ColumnHeader columnHeaderVersion;
        private System.Windows.Forms.ColumnHeader columnHeaderPath;
        private System.Windows.Forms.ColumnHeader columnHeaderSize;
        private System.Windows.Forms.ColumnHeader columnHeaderState;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.listView1 = new System.Windows.Forms.ListView();
            this.columnHeaderVersion = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderPath = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeaderState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miRefresh = new System.Windows.Forms.ToolStripMenuItem();
            this.miInstallNew = new System.Windows.Forms.ToolStripMenuItem();
            this.miOpenDir = new System.Windows.Forms.ToolStripMenuItem();
            this.miDisableVersion = new System.Windows.Forms.ToolStripMenuItem();
            this.miDeleteVersion = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripBtnRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnInstall = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripBtnDisable = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnOpenDir = new System.Windows.Forms.ToolStripButton();
            this.toolStripBtnDelete = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderVersion,
            this.columnHeaderPath,
            this.columnHeaderSize,
            this.columnHeaderState});
            this.listView1.ContextMenuStrip = this.contextMenuStrip1;
            this.listView1.FullRowSelect = true;
            this.listView1.GridLines = true;
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(12, 43);
            this.listView1.MultiSelect = false;
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(682, 299);
            this.listView1.TabIndex = 1;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            // 
            // columnHeaderVersion
            // 
            this.columnHeaderVersion.Text = "版本名称";
            this.columnHeaderVersion.Width = 260;
            // 
            // columnHeaderPath
            // 
            this.columnHeaderPath.Text = "安装目录";
            this.columnHeaderPath.Width = 230;
            // 
            // columnHeaderSize
            // 
            this.columnHeaderSize.Text = "大小";
            this.columnHeaderSize.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.columnHeaderSize.Width = 90;
            // 
            // columnHeaderState
            // 
            this.columnHeaderState.Text = "状态";
            this.columnHeaderState.Width = 80;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miRefresh,
            this.miInstallNew,
            this.miOpenDir,
            this.miDisableVersion,
            this.miDeleteVersion});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(189, 154);
            this.contextMenuStrip1.Opening += new System.ComponentModel.CancelEventHandler(this.contextMenuStrip1_Opening);
            // 
            // miRefresh
            // 
            this.miRefresh.Name = "miRefresh";
            this.miRefresh.Size = new System.Drawing.Size(188, 30);
            this.miRefresh.Text = "刷新";
            this.miRefresh.Click += new System.EventHandler(this.miRefresh_Click);
            // 
            // miInstallNew
            // 
            this.miInstallNew.Name = "miInstallNew";
            this.miInstallNew.Size = new System.Drawing.Size(188, 30);
            this.miInstallNew.Text = "安装新版本";
            this.miInstallNew.Click += new System.EventHandler(this.miInstallNew_Click);
            // 
            // miOpenDir
            // 
            this.miOpenDir.Name = "miOpenDir";
            this.miOpenDir.Size = new System.Drawing.Size(188, 30);
            this.miOpenDir.Text = "打开安装目录";
            this.miOpenDir.Click += new System.EventHandler(this.miOpenDir_Click);
            // 
            // miDisableVersion
            // 
            this.miDisableVersion.Name = "miDisableVersion";
            this.miDisableVersion.Size = new System.Drawing.Size(188, 30);
            this.miDisableVersion.Text = "禁用此版本";
            this.miDisableVersion.Click += new System.EventHandler(this.miDisableVersion_Click);
            // 
            // miDeleteVersion
            // 
            this.miDeleteVersion.Name = "miDeleteVersion";
            this.miDeleteVersion.Size = new System.Drawing.Size(188, 30);
            this.miDeleteVersion.Text = "删除此版本";
            this.miDeleteVersion.Click += new System.EventHandler(this.miDeleteVersion_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripBtnRefresh,
            this.toolStripBtnInstall,
            this.toolStripSeparator1,
            this.toolStripBtnDisable,
            this.toolStripBtnOpenDir,
            this.toolStripBtnDelete});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(706, 33);
            this.toolStrip1.TabIndex = 3;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripBtnRefresh
            // 
            this.toolStripBtnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnRefresh.Name = "toolStripBtnRefresh";
            this.toolStripBtnRefresh.Size = new System.Drawing.Size(50, 28);
            this.toolStripBtnRefresh.Text = "刷新";
            this.toolStripBtnRefresh.ToolTipText = "重新扫描已安装的 llama.cpp 版本";
            this.toolStripBtnRefresh.Click += new System.EventHandler(this.miRefresh_Click);
            // 
            // toolStripBtnInstall
            // 
            this.toolStripBtnInstall.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnInstall.Name = "toolStripBtnInstall";
            this.toolStripBtnInstall.Size = new System.Drawing.Size(104, 28);
            this.toolStripBtnInstall.Text = "安装新版本";
            this.toolStripBtnInstall.ToolTipText = "从 GitHub 下载新版本安装包";
            this.toolStripBtnInstall.Click += new System.EventHandler(this.miInstallNew_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 33);
            // 
            // toolStripBtnDisable
            // 
            this.toolStripBtnDisable.CheckOnClick = true;
            this.toolStripBtnDisable.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnDisable.Name = "toolStripBtnDisable";
            this.toolStripBtnDisable.Size = new System.Drawing.Size(50, 28);
            this.toolStripBtnDisable.Text = "禁用";
            this.toolStripBtnDisable.ToolTipText = "切换当前选中版本的启用/禁用状态";
            this.toolStripBtnDisable.Click += new System.EventHandler(this.miDisableVersion_Click);
            // 
            // toolStripBtnOpenDir
            // 
            this.toolStripBtnOpenDir.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnOpenDir.Name = "toolStripBtnOpenDir";
            this.toolStripBtnOpenDir.Size = new System.Drawing.Size(86, 28);
            this.toolStripBtnOpenDir.Text = "打开目录";
            this.toolStripBtnOpenDir.ToolTipText = "在资源管理器中打开当前选中版本的安装目录";
            this.toolStripBtnOpenDir.Click += new System.EventHandler(this.miOpenDir_Click);
            // 
            // toolStripBtnDelete
            // 
            this.toolStripBtnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripBtnDelete.Name = "toolStripBtnDelete";
            this.toolStripBtnDelete.Size = new System.Drawing.Size(86, 28);
            this.toolStripBtnDelete.Text = "删除版本";
            this.toolStripBtnDelete.ToolTipText = "把当前选中版本目录删除到回收站（可还原）";
            this.toolStripBtnDelete.Click += new System.EventHandler(this.miDeleteVersion_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 28);
            // 
            // llamaVersionForm
            // 
            this.ClientSize = new System.Drawing.Size(706, 354);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.listView1);
            this.Name = "llamaVersionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "llama版本管理";
            this.contextMenuStrip1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton toolStripBtnRefresh;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton toolStripBtnInstall;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton toolStripBtnDisable;
        private System.Windows.Forms.ToolStripButton toolStripBtnOpenDir;
        private System.Windows.Forms.ToolStripButton toolStripBtnDelete;
    }
}