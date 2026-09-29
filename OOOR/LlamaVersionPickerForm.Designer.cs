namespace ooor
{
    partial class LlamaVersionPickerForm
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
            this.listVersions = new System.Windows.Forms.ListView();
            this.colVersionName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionDir = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.miSelect = new System.Windows.Forms.ToolStripMenuItem();
            this.miManageAll = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenu.SuspendLayout();
            this.SuspendLayout();
            //
            // contextMenu
            //
            this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miSelect,
            this.miManageAll});
            this.contextMenu.Name = "contextMenu";
            this.contextMenu.Size = new System.Drawing.Size(200, 48);
            //
            // miSelect
            //
            this.miSelect.Name = "miSelect";
            this.miSelect.Size = new System.Drawing.Size(199, 22);
            this.miSelect.Click += new System.EventHandler(this.ConfirmSelection);
            //
            // miManageAll
            //
            this.miManageAll.Name = "miManageAll";
            this.miManageAll.Size = new System.Drawing.Size(199, 22);
            this.miManageAll.Click += new System.EventHandler(this.miManageAll_Click);
            //
            // listVersions
            //
            this.listVersions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listVersions.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colVersionName,
            this.colVersionDir,
            this.colVersionStatus,
            this.colVersionState});
            this.listVersions.ContextMenuStrip = this.contextMenu;
            this.listVersions.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listVersions.FullRowSelect = true;
            this.listVersions.GridLines = true;
            this.listVersions.HideSelection = false;
            this.listVersions.Location = new System.Drawing.Point(0, 0);
            this.listVersions.MultiSelect = false;
            this.listVersions.Name = "listVersions";
            this.listVersions.ShowItemToolTips = true;
            this.listVersions.Size = new System.Drawing.Size(960, 486);
            this.listVersions.TabIndex = 0;
            this.listVersions.UseCompatibleStateImageBehavior = false;
            this.listVersions.View = System.Windows.Forms.View.Details;
            this.listVersions.ItemActivate += new System.EventHandler(this.ConfirmSelection);
            //
            // colVersionName
            //
            this.colVersionName.Text = "版本";
            this.colVersionName.Width = 260;
            //
            // colVersionDir
            //
            this.colVersionDir.Text = "安装目录";
            this.colVersionDir.Width = 200;
            //
            // colVersionStatus
            //
            this.colVersionStatus.Text = "状态";
            this.colVersionStatus.Width = 80;
            //
            // colVersionState
            //
            this.colVersionState.Text = "使用中";
            this.colVersionState.Width = 90;
            //
            // LlamaVersionPickerForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(960, 486);
            this.Controls.Add(this.listVersions);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(660, 360);
            this.Name = "LlamaVersionPickerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "选择 llama 版本";
            this.contextMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listVersions;
        private System.Windows.Forms.ColumnHeader colVersionName;
        private System.Windows.Forms.ColumnHeader colVersionDir;
        private System.Windows.Forms.ColumnHeader colVersionStatus;
        private System.Windows.Forms.ColumnHeader colVersionState;
        private System.Windows.Forms.ContextMenuStrip contextMenu;
        private System.Windows.Forms.ToolStripMenuItem miSelect;
        private System.Windows.Forms.ToolStripMenuItem miManageAll;
    }
}
