namespace ooor
{
    partial class ModelPickerForm
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
            this.colModelSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelNote = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colModelTime = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.选择双击ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ToolStripMenuItemManagerAllModel = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // listModels
            // 
            this.listModels.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colModelName,
            this.colModelSize,
            this.colModelNote,
            this.colModelTime});
            this.listModels.ContextMenuStrip = this.contextMenuStrip1;
            this.listModels.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listModels.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listModels.FullRowSelect = true;
            this.listModels.HideSelection = false;
            this.listModels.Location = new System.Drawing.Point(0, 0);
            this.listModels.MultiSelect = false;
            this.listModels.Name = "listModels";
            this.listModels.ShowItemToolTips = true;
            this.listModels.Size = new System.Drawing.Size(1242, 528);
            this.listModels.TabIndex = 0;
            this.listModels.UseCompatibleStateImageBehavior = false;
            this.listModels.View = System.Windows.Forms.View.Details;
            this.listModels.ItemActivate += new System.EventHandler(this.ConfirmSelection);
            // 
            // colModelName
            // 
            this.colModelName.Text = "模型名称";
            this.colModelName.Width = 250;
            // 
            // colModelSize
            // 
            this.colModelSize.Text = "尺寸";
            this.colModelSize.Width = 76;
            // 
            // colModelNote
            // 
            this.colModelNote.Text = "备注";
            this.colModelNote.Width = 280;
            // 
            // colModelTime
            // 
            this.colModelTime.Text = "日期时间";
            this.colModelTime.Width = 126;
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.选择双击ToolStripMenuItem,
            this.ToolStripMenuItemManagerAllModel});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(189, 64);
            // 
            // 选择双击ToolStripMenuItem
            // 
            this.选择双击ToolStripMenuItem.Name = "选择双击ToolStripMenuItem";
            this.选择双击ToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            this.选择双击ToolStripMenuItem.Text = "选择（双击）";
            this.选择双击ToolStripMenuItem.Click += new System.EventHandler(this.ConfirmSelection);
            // 
            // ToolStripMenuItemManagerAllModel
            // 
            this.ToolStripMenuItemManagerAllModel.Name = "ToolStripMenuItemManagerAllModel";
            this.ToolStripMenuItemManagerAllModel.Size = new System.Drawing.Size(188, 30);
            this.ToolStripMenuItemManagerAllModel.Text = "管理所有模型";
            this.ToolStripMenuItemManagerAllModel.Click += new System.EventHandler(this.ToolStripMenuItemManagerAllModel_Click);
            // 
            // ModelPickerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1242, 528);
            this.Controls.Add(this.listModels);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(660, 360);
            this.Name = "ModelPickerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "选择模型";
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listModels;
        private System.Windows.Forms.ColumnHeader colModelName;
        private System.Windows.Forms.ColumnHeader colModelSize;
        private System.Windows.Forms.ColumnHeader colModelNote;
        private System.Windows.Forms.ColumnHeader colModelTime;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 选择双击ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ToolStripMenuItemManagerAllModel;
    }
}
