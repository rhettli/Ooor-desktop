namespace ooor
{
    partial class ModelManagerForm
    {
        /// <summary>必需的设计器变量。</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理所有正在使用的资源。</summary>
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
        /// 设计器支持所需的方法 - 不要修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.listModels = new System.Windows.Forms.ListView();
            this.colModelName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelMmproj = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelDir = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelNote = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuEditNote = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLocateFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuLocateMmproj = new System.Windows.Forms.ToolStripMenuItem();
            this.menuAddDir = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.menuDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSoftDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.toolStripButtonDownloadModels = new System.Windows.Forms.ToolStripButton();
            this.btnAddDir = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.btnEditNote = new System.Windows.Forms.ToolStripButton();
            this.btnLocateFile = new System.Windows.Forms.ToolStripButton();
            this.btnLocateMmproj = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnDelete = new System.Windows.Forms.ToolStripButton();
            this.btnSoftDelete = new System.Windows.Forms.ToolStripButton();
            this.contextMenuStrip.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listModels
            // 
            this.listModels.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colModelName,
            this.colModelMmproj,
            this.colModelDir,
            this.colModelType,
            this.colModelSize,
            this.colModelNote,
            this.colModelTime});
            this.listModels.ContextMenuStrip = this.contextMenuStrip;
            this.listModels.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listModels.FullRowSelect = true;
            this.listModels.GridLines = true;
            this.listModels.HideSelection = false;
            this.listModels.Location = new System.Drawing.Point(0, 36);
            this.listModels.MultiSelect = false;
            this.listModels.Name = "listModels";
            this.listModels.ShowItemToolTips = true;
            this.listModels.Size = new System.Drawing.Size(1266, 524);
            this.listModels.TabIndex = 0;
            this.listModels.UseCompatibleStateImageBehavior = false;
            this.listModels.View = System.Windows.Forms.View.Details;
            this.listModels.SelectedIndexChanged += new System.EventHandler(this.ListModels_SelectedIndexChanged);
            this.listModels.MouseUp += new System.Windows.Forms.MouseEventHandler(this.ListModels_MouseUp);
            // 
            // colModelName
            // 
            this.colModelName.Text = "模型名称";
            this.colModelName.Width = 250;
            // 
            // colModelMmproj
            // 
            this.colModelMmproj.Text = "多模态投影文件";
            this.colModelMmproj.Width = 200;
            // 
            // colModelDir
            // 
            this.colModelDir.Text = "模型目录";
            this.colModelDir.Width = 88;
            // 
            // colModelType
            // 
            this.colModelType.Text = "类型";
            this.colModelType.Width = 46;
            // 
            // colModelSize
            // 
            this.colModelSize.Text = "尺寸";
            this.colModelSize.Width = 76;
            // 
            // colModelNote
            // 
            this.colModelNote.Text = "备注";
            this.colModelNote.Width = 150;
            // 
            // colModelTime
            // 
            this.colModelTime.Text = "日期时间";
            this.colModelTime.Width = 126;
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuEditNote,
            this.menuLocateFile,
            this.menuLocateMmproj,
            this.menuAddDir,
            this.menuSeparator,
            this.menuDelete,
            this.menuSoftDelete});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(189, 190);
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.Menu_Opening);
            // 
            // menuEditNote
            // 
            this.menuEditNote.Name = "menuEditNote";
            this.menuEditNote.Size = new System.Drawing.Size(188, 30);
            this.menuEditNote.Text = "修改备注";
            this.menuEditNote.Click += new System.EventHandler(this.MenuEditNote_Click);
            // 
            // menuLocateFile
            // 
            this.menuLocateFile.Name = "menuLocateFile";
            this.menuLocateFile.Size = new System.Drawing.Size(188, 30);
            this.menuLocateFile.Text = "定位模型文件";
            this.menuLocateFile.Click += new System.EventHandler(this.MenuLocateFile_Click);
            // 
            // menuLocateMmproj
            // 
            this.menuLocateMmproj.Name = "menuLocateMmproj";
            this.menuLocateMmproj.Size = new System.Drawing.Size(188, 30);
            this.menuLocateMmproj.Text = "定位投影文件";
            this.menuLocateMmproj.Click += new System.EventHandler(this.MenuLocateMmproj_Click);
            // 
            // menuAddDir
            // 
            this.menuAddDir.Name = "menuAddDir";
            this.menuAddDir.Size = new System.Drawing.Size(188, 30);
            this.menuAddDir.Text = "添加模型目录";
            this.menuAddDir.Click += new System.EventHandler(this.MenuAddDir_Click);
            // 
            // menuSeparator
            // 
            this.menuSeparator.Name = "menuSeparator";
            this.menuSeparator.Size = new System.Drawing.Size(185, 6);
            // 
            // menuDelete
            // 
            this.menuDelete.Name = "menuDelete";
            this.menuDelete.Size = new System.Drawing.Size(188, 30);
            this.menuDelete.Text = "删除";
            this.menuDelete.Click += new System.EventHandler(this.MenuDelete_Click);
            // 
            // menuSoftDelete
            // 
            this.menuSoftDelete.Name = "menuSoftDelete";
            this.menuSoftDelete.Size = new System.Drawing.Size(188, 30);
            this.menuSoftDelete.Text = "软删除";
            this.menuSoftDelete.Click += new System.EventHandler(this.MenuSoftDelete_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripButtonDownloadModels,
            this.btnAddDir,
            this.toolStripSeparator2,
            this.btnEditNote,
            this.btnLocateFile,
            this.btnLocateMmproj,
            this.toolStripSeparator1,
            this.btnDelete,
            this.btnSoftDelete});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1266, 33);
            this.toolStrip1.TabIndex = 2;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // toolStripButtonDownloadModels
            // 
            this.toolStripButtonDownloadModels.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButtonDownloadModels.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.toolStripButtonDownloadModels.Name = "toolStripButtonDownloadModels";
            this.toolStripButtonDownloadModels.Size = new System.Drawing.Size(86, 28);
            this.toolStripButtonDownloadModels.Text = "下载模型";
            this.toolStripButtonDownloadModels.Click += new System.EventHandler(this.toolStripButtonDownloadModels_Click);
            // 
            // btnAddDir
            // 
            this.btnAddDir.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnAddDir.Name = "btnAddDir";
            this.btnAddDir.Size = new System.Drawing.Size(122, 28);
            this.btnAddDir.Text = "添加模型目录";
            this.btnAddDir.Click += new System.EventHandler(this.MenuAddDir_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 33);
            // 
            // btnEditNote
            // 
            this.btnEditNote.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnEditNote.Name = "btnEditNote";
            this.btnEditNote.Size = new System.Drawing.Size(86, 28);
            this.btnEditNote.Text = "修改备注";
            this.btnEditNote.Click += new System.EventHandler(this.MenuEditNote_Click);
            // 
            // btnLocateFile
            // 
            this.btnLocateFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLocateFile.Name = "btnLocateFile";
            this.btnLocateFile.Size = new System.Drawing.Size(122, 28);
            this.btnLocateFile.Text = "定位模型文件";
            this.btnLocateFile.Click += new System.EventHandler(this.MenuLocateFile_Click);
            // 
            // btnLocateMmproj
            // 
            this.btnLocateMmproj.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnLocateMmproj.Name = "btnLocateMmproj";
            this.btnLocateMmproj.Size = new System.Drawing.Size(122, 28);
            this.btnLocateMmproj.Text = "定位投影文件";
            this.btnLocateMmproj.Click += new System.EventHandler(this.MenuLocateMmproj_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 33);
            // 
            // btnDelete
            // 
            this.btnDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(50, 28);
            this.btnDelete.Text = "删除";
            this.btnDelete.Click += new System.EventHandler(this.MenuDelete_Click);
            // 
            // btnSoftDelete
            // 
            this.btnSoftDelete.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnSoftDelete.Name = "btnSoftDelete";
            this.btnSoftDelete.Size = new System.Drawing.Size(68, 28);
            this.btnSoftDelete.Text = "软删除";
            this.btnSoftDelete.Click += new System.EventHandler(this.MenuSoftDelete_Click);
            // 
            // ModelManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1266, 560);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.listModels);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MinimumSize = new System.Drawing.Size(680, 380);
            this.Name = "ModelManagerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "模型管理";
            this.contextMenuStrip.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listModels;
        private System.Windows.Forms.ColumnHeader colModelName;
        private System.Windows.Forms.ColumnHeader colModelMmproj;
        private System.Windows.Forms.ColumnHeader colModelDir;
        private System.Windows.Forms.ColumnHeader colModelType;
        private System.Windows.Forms.ColumnHeader colModelSize;
        private System.Windows.Forms.ColumnHeader colModelNote;
        private System.Windows.Forms.ColumnHeader colModelTime;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuEditNote;
        private System.Windows.Forms.ToolStripMenuItem menuLocateFile;
        private System.Windows.Forms.ToolStripMenuItem menuLocateMmproj;
        private System.Windows.Forms.ToolStripMenuItem menuAddDir;
        private System.Windows.Forms.ToolStripSeparator menuSeparator;
        private System.Windows.Forms.ToolStripMenuItem menuDelete;
        private System.Windows.Forms.ToolStripMenuItem menuSoftDelete;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton btnEditNote;
        private System.Windows.Forms.ToolStripButton btnLocateFile;
        private System.Windows.Forms.ToolStripButton btnLocateMmproj;
        private System.Windows.Forms.ToolStripButton btnAddDir;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripButton btnDelete;
        private System.Windows.Forms.ToolStripButton btnSoftDelete;
        private System.Windows.Forms.ToolStripButton toolStripButtonDownloadModels;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
    }
}
