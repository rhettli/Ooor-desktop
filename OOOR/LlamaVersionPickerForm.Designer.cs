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
            this.listVersions = new System.Windows.Forms.ListView();
            this.colVersionName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionDir = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionStatus = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colVersionState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panelBottom = new System.Windows.Forms.Panel();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
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
            this.listVersions.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.listVersions.FullRowSelect = true;
            this.listVersions.HideSelection = false;
            this.listVersions.Location = new System.Drawing.Point(0, 0);
            this.listVersions.MultiSelect = false;
            this.listVersions.Name = "listVersions";
            this.listVersions.ShowItemToolTips = true;
            this.listVersions.Size = new System.Drawing.Size(960, 412);
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
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.btnOk);
            this.panelBottom.Controls.Add(this.btnCancel);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 418);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(960, 68);
            this.panelBottom.TabIndex = 1;
            this.panelBottom.Resize += new System.EventHandler(this.LayoutButtons);
            // 
            // btnOk
            // 
            this.btnOk.Location = new System.Drawing.Point(645, 14);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(133, 42);
            this.btnOk.TabIndex = 0;
            this.btnOk.Text = "确定";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.ConfirmSelection);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(816, 14);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(133, 42);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // LlamaVersionPickerForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(960, 486);
            this.Controls.Add(this.listVersions);
            this.Controls.Add(this.panelBottom);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(660, 360);
            this.Name = "LlamaVersionPickerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "选择 llama 版本";
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listVersions;
        private System.Windows.Forms.ColumnHeader colVersionName;
        private System.Windows.Forms.ColumnHeader colVersionDir;
        private System.Windows.Forms.ColumnHeader colVersionStatus;
        private System.Windows.Forms.ColumnHeader colVersionState;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
    }
}
