namespace ooor
{
    partial class ModelDownloadForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
        /// 所有界面控件的声明、属性、布局都在此文件，可在 VS 设计器中可视化预览/调整。
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle8 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.cmbSource = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.txtSearch = new System.Windows.Forms.ToolStripComboBox();
            this.toolStripButton2 = new System.Windows.Forms.ToolStripButton();
            this.dgvModels = new System.Windows.Forms.DataGridView();
            this.colRepoId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDownloads = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLikes = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTask = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colGated = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colUpdated = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dgvFiles = new System.Windows.Forms.DataGridView();
            this.colFileName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFileSize = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFileType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colHardware = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFileAction = new System.Windows.Forms.DataGridViewButtonColumn();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvModels)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).BeginInit();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Margin = new System.Windows.Forms.Padding(4);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.toolStrip1);
            this.splitContainer.Panel1.Controls.Add(this.dgvModels);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.dgvFiles);
            this.splitContainer.Size = new System.Drawing.Size(1404, 699);
            this.splitContainer.SplitterDistance = 367;
            this.splitContainer.SplitterWidth = 6;
            this.splitContainer.TabIndex = 1;
            // 
            // toolStrip1
            // 
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cmbSource,
            this.toolStripSeparator1,
            this.txtSearch,
            this.toolStripButton2});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(1404, 33);
            this.toolStrip1.TabIndex = 1;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // cmbSource
            // 
            this.cmbSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSource.Name = "cmbSource";
            this.cmbSource.Size = new System.Drawing.Size(200, 38);
            this.cmbSource.SelectedIndexChanged += new System.EventHandler(this.cmbSource_SelectedIndexChanged);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 38);
            // 
            // txtSearch
            // 
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(260, 38);
            this.txtSearch.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSearch_KeyDown);
            // 
            // toolStripButton2
            // 
            this.toolStripButton2.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.toolStripButton2.Name = "toolStripButton2";
            this.toolStripButton2.Size = new System.Drawing.Size(50, 33);
            this.toolStripButton2.Text = "搜索";
            this.toolStripButton2.Click += new System.EventHandler(this.btnSearch_Click);
            // 
            // dgvModels
            // 
            this.dgvModels.AllowUserToAddRows = false;
            this.dgvModels.AllowUserToDeleteRows = false;
            this.dgvModels.AllowUserToResizeRows = false;
            this.dgvModels.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvModels.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvModels.BorderStyle = System.Windows.Forms.BorderStyle.None;
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(232)))), ((int)(((byte)(232)))));
            dataGridViewCellStyle5.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(232)))), ((int)(((byte)(232)))));
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvModels.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle5;
            this.dgvModels.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvModels.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colRepoId,
            this.colDownloads,
            this.colLikes,
            this.colTask,
            this.colGated,
            this.colUpdated});
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle6.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle6.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle6.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle6.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle6.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            dataGridViewCellStyle6.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvModels.DefaultCellStyle = dataGridViewCellStyle6;
            this.dgvModels.EnableHeadersVisualStyles = false;
            this.dgvModels.Location = new System.Drawing.Point(4, 37);
            this.dgvModels.Margin = new System.Windows.Forms.Padding(4);
            this.dgvModels.MultiSelect = false;
            this.dgvModels.Name = "dgvModels";
            this.dgvModels.ReadOnly = true;
            this.dgvModels.RowHeadersVisible = false;
            this.dgvModels.RowHeadersWidth = 62;
            this.dgvModels.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvModels.Size = new System.Drawing.Size(1396, 328);
            this.dgvModels.TabIndex = 0;
            this.dgvModels.SelectionChanged += new System.EventHandler(this.dgvModels_SelectionChanged);
            // 
            // colRepoId
            // 
            this.colRepoId.HeaderText = "仓库";
            this.colRepoId.MinimumWidth = 8;
            this.colRepoId.Name = "colRepoId";
            this.colRepoId.ReadOnly = true;
            // 
            // colDownloads
            // 
            this.colDownloads.HeaderText = "下载量";
            this.colDownloads.MinimumWidth = 8;
            this.colDownloads.Name = "colDownloads";
            this.colDownloads.ReadOnly = true;
            // 
            // colLikes
            // 
            this.colLikes.HeaderText = "点赞";
            this.colLikes.MinimumWidth = 8;
            this.colLikes.Name = "colLikes";
            this.colLikes.ReadOnly = true;
            // 
            // colTask
            // 
            this.colTask.HeaderText = "任务";
            this.colTask.MinimumWidth = 8;
            this.colTask.Name = "colTask";
            this.colTask.ReadOnly = true;
            // 
            // colGated
            // 
            this.colGated.HeaderText = "受限";
            this.colGated.MinimumWidth = 8;
            this.colGated.Name = "colGated";
            this.colGated.ReadOnly = true;
            // 
            // colUpdated
            // 
            this.colUpdated.HeaderText = "最近更新";
            this.colUpdated.MinimumWidth = 8;
            this.colUpdated.Name = "colUpdated";
            this.colUpdated.ReadOnly = true;
            // 
            // dgvFiles
            // 
            this.dgvFiles.AllowUserToAddRows = false;
            this.dgvFiles.AllowUserToDeleteRows = false;
            this.dgvFiles.AllowUserToResizeRows = false;
            this.dgvFiles.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFiles.BackgroundColor = System.Drawing.SystemColors.Window;
            this.dgvFiles.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle7.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(232)))), ((int)(((byte)(232)))));
            dataGridViewCellStyle7.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle7.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            dataGridViewCellStyle7.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(232)))), ((int)(((byte)(232)))));
            dataGridViewCellStyle7.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle7.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dgvFiles.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle7;
            this.dgvFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFiles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colFileName,
            this.colFileSize,
            this.colFileType,
            this.colHardware,
            this.colFileAction});
            dataGridViewCellStyle8.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle8.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle8.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            dataGridViewCellStyle8.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle8.SelectionBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            dataGridViewCellStyle8.SelectionForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(32)))), ((int)(((byte)(32)))));
            dataGridViewCellStyle8.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dgvFiles.DefaultCellStyle = dataGridViewCellStyle8;
            this.dgvFiles.EnableHeadersVisualStyles = false;
            this.dgvFiles.Location = new System.Drawing.Point(0, 0);
            this.dgvFiles.Margin = new System.Windows.Forms.Padding(4);
            this.dgvFiles.MultiSelect = false;
            this.dgvFiles.Name = "dgvFiles";
            this.dgvFiles.ReadOnly = true;
            this.dgvFiles.RowHeadersVisible = false;
            this.dgvFiles.RowHeadersWidth = 62;
            this.dgvFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvFiles.Size = new System.Drawing.Size(1400, 322);
            this.dgvFiles.TabIndex = 0;
            this.dgvFiles.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvFiles_CellContentClick);
            // 
            // colFileName
            // 
            this.colFileName.FillWeight = 34F;
            this.colFileName.HeaderText = "文件";
            this.colFileName.MinimumWidth = 8;
            this.colFileName.Name = "colFileName";
            this.colFileName.ReadOnly = true;
            // 
            // colFileSize
            // 
            this.colFileSize.FillWeight = 10F;
            this.colFileSize.HeaderText = "大小";
            this.colFileSize.MinimumWidth = 8;
            this.colFileSize.Name = "colFileSize";
            this.colFileSize.ReadOnly = true;
            // 
            // colFileType
            // 
            this.colFileType.FillWeight = 14F;
            this.colFileType.HeaderText = "类型";
            this.colFileType.MinimumWidth = 8;
            this.colFileType.Name = "colFileType";
            this.colFileType.ReadOnly = true;
            // 
            // colHardware
            // 
            this.colHardware.FillWeight = 30F;
            this.colHardware.HeaderText = "适配硬件";
            this.colHardware.MinimumWidth = 8;
            this.colHardware.Name = "colHardware";
            this.colHardware.ReadOnly = true;
            // 
            // colFileAction
            // 
            this.colFileAction.FillWeight = 12F;
            this.colFileAction.HeaderText = "操作";
            this.colFileAction.MinimumWidth = 8;
            this.colFileAction.Name = "colFileAction";
            this.colFileAction.ReadOnly = true;
            this.colFileAction.Text = "下载";
            this.colFileAction.UseColumnTextForButtonValue = true;
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 699);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(1, 0, 19, 0);
            this.statusStrip.Size = new System.Drawing.Size(1404, 31);
            this.statusStrip.TabIndex = 2;
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(1384, 24);
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "就绪";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ModelDownloadForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1404, 730);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.statusStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(700, 420);
            this.Name = "ModelDownloadForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "模型市场 · 搜索与下载 GGUF";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ModelDownloadForm_FormClosing);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvModels)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFiles)).EndInit();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.DataGridView dgvModels;
        private System.Windows.Forms.DataGridViewTextBoxColumn colRepoId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDownloads;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLikes;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTask;
        private System.Windows.Forms.DataGridViewTextBoxColumn colGated;
        private System.Windows.Forms.DataGridViewTextBoxColumn colUpdated;
        private System.Windows.Forms.DataGridView dgvFiles;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFileName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFileSize;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFileType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colHardware;
        private System.Windows.Forms.DataGridViewButtonColumn colFileAction;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripComboBox cmbSource;
        private System.Windows.Forms.ToolStripComboBox txtSearch;
        private System.Windows.Forms.ToolStripButton toolStripButton2;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    }
}
