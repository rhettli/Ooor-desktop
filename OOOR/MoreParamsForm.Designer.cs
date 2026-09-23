namespace ooor
{
    partial class MoreParamsForm
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
            this.lblTip = new System.Windows.Forms.Label();
            this.listParams = new System.Windows.Forms.ListView();
            this.colParamName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colParamValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colParamDesc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuEditValue = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.新建参数ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.编辑参数信息ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.删除参数ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTip
            // 
            this.lblTip.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTip.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTip.ForeColor = System.Drawing.Color.Black;
            this.lblTip.Location = new System.Drawing.Point(0, 0);
            this.lblTip.Name = "lblTip";
            this.lblTip.Padding = new System.Windows.Forms.Padding(12, 0, 12, 0);
            this.lblTip.Size = new System.Drawing.Size(1299, 44);
            this.lblTip.TabIndex = 0;
            this.lblTip.Text = "双击参数行（或右击「修改值」）修改参数值；值为空 = 启动时不传该参数。主界面的上下文 / 预测 / GPU 层数 / 主机 / 端口不在此列。";
            this.lblTip.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // listParams
            // 
            this.listParams.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listParams.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colParamName,
            this.colParamValue,
            this.colParamDesc});
            this.listParams.ContextMenuStrip = this.contextMenuStrip;
            this.listParams.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listParams.FullRowSelect = true;
            this.listParams.GridLines = true;
            this.listParams.HideSelection = false;
            this.listParams.Location = new System.Drawing.Point(0, 44);
            this.listParams.MultiSelect = false;
            this.listParams.Name = "listParams";
            this.listParams.ShowItemToolTips = true;
            this.listParams.Size = new System.Drawing.Size(1299, 499);
            this.listParams.TabIndex = 1;
            this.listParams.UseCompatibleStateImageBehavior = false;
            this.listParams.View = System.Windows.Forms.View.Details;
            this.listParams.ItemActivate += new System.EventHandler(this.ListParams_ItemActivate);
            this.listParams.MouseUp += new System.Windows.Forms.MouseEventHandler(this.ListParams_MouseUp);
            // 
            // colParamName
            // 
            this.colParamName.Text = "参数名称";
            this.colParamName.Width = 160;
            // 
            // colParamValue
            // 
            this.colParamValue.Text = "参数值";
            this.colParamValue.Width = 150;
            // 
            // colParamDesc
            // 
            this.colParamDesc.Text = "参数描述";
            this.colParamDesc.Width = 546;
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuEditValue,
            this.toolStripSeparator1,
            this.新建参数ToolStripMenuItem,
            this.编辑参数信息ToolStripMenuItem,
            this.删除参数ToolStripMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(189, 130);
            this.contextMenuStrip.Opening += new System.ComponentModel.CancelEventHandler(this.ContextMenuStrip_Opening);
            // 
            // menuEditValue
            // 
            this.menuEditValue.Name = "menuEditValue";
            this.menuEditValue.Size = new System.Drawing.Size(188, 30);
            this.menuEditValue.Text = "修改值";
            this.menuEditValue.Click += new System.EventHandler(this.MenuEditValue_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(185, 6);
            // 
            // 新建参数ToolStripMenuItem
            // 
            this.新建参数ToolStripMenuItem.Name = "新建参数ToolStripMenuItem";
            this.新建参数ToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            this.新建参数ToolStripMenuItem.Text = "新建参数";
            this.新建参数ToolStripMenuItem.Click += new System.EventHandler(this.MenuNewParam_Click);
            // 
            // 编辑参数信息ToolStripMenuItem
            // 
            this.编辑参数信息ToolStripMenuItem.Name = "编辑参数信息ToolStripMenuItem";
            this.编辑参数信息ToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            this.编辑参数信息ToolStripMenuItem.Text = "编辑参数信息";
            this.编辑参数信息ToolStripMenuItem.Click += new System.EventHandler(this.MenuEditInfo_Click);
            // 
            // 删除参数ToolStripMenuItem
            // 
            this.删除参数ToolStripMenuItem.Name = "删除参数ToolStripMenuItem";
            this.删除参数ToolStripMenuItem.Size = new System.Drawing.Size(188, 30);
            this.删除参数ToolStripMenuItem.Text = "删除参数";
            this.删除参数ToolStripMenuItem.Click += new System.EventHandler(this.MenuDeleteParam_Click);
            // 
            // MoreParamsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1299, 545);
            this.Controls.Add(this.listParams);
            this.Controls.Add(this.lblTip);
            this.MinimumSize = new System.Drawing.Size(640, 380);
            this.Name = "MoreParamsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "更多参数";
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblTip;
        private System.Windows.Forms.ListView listParams;
        private System.Windows.Forms.ColumnHeader colParamName;
        private System.Windows.Forms.ColumnHeader colParamValue;
        private System.Windows.Forms.ColumnHeader colParamDesc;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuEditValue;
        private System.Windows.Forms.ToolStripMenuItem 新建参数ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 编辑参数信息ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 删除参数ToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
    }
}
