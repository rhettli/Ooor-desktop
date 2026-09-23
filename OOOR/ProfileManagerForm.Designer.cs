namespace ooor
{
    partial class ProfileManagerForm
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
            this.panelLeft = new System.Windows.Forms.Panel();
            this.listProfiles = new System.Windows.Forms.ListBox();
            this.btnNew = new System.Windows.Forms.Button();
            this.btnRename = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.panelRight = new System.Windows.Forms.Panel();
            this.listParams = new System.Windows.Forms.ListView();
            this.colParam = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colValue = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colDesc = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblTip = new System.Windows.Forms.Label();
            this.panelLeft.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelLeft
            // 
            this.panelLeft.Controls.Add(this.btnRename);
            this.panelLeft.Controls.Add(this.btnNew);
            this.panelLeft.Controls.Add(this.listProfiles);
            this.panelLeft.Controls.Add(this.btnDelete);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 0);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(10);
            this.panelLeft.Size = new System.Drawing.Size(312, 590);
            this.panelLeft.TabIndex = 0;
            // 
            // listProfiles
            // 
            this.listProfiles.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.listProfiles.IntegralHeight = false;
            this.listProfiles.ItemHeight = 25;
            this.listProfiles.Location = new System.Drawing.Point(10, 10);
            this.listProfiles.Name = "listProfiles";
            this.listProfiles.Size = new System.Drawing.Size(292, 508);
            this.listProfiles.TabIndex = 0;
            this.listProfiles.SelectedIndexChanged += new System.EventHandler(this.List_SelectedIndexChanged);
            // 
            // btnNew
            // 
            this.btnNew.AutoSize = true;
            this.btnNew.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnNew.Location = new System.Drawing.Point(122, 534);
            this.btnNew.Name = "btnNew";
            this.btnNew.Size = new System.Drawing.Size(60, 34);
            this.btnNew.TabIndex = 0;
            this.btnNew.Text = "新建";
            this.btnNew.UseVisualStyleBackColor = true;
            this.btnNew.Click += new System.EventHandler(this.BtnNew_Click);
            // 
            // btnRename
            // 
            this.btnRename.AutoSize = true;
            this.btnRename.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRename.Location = new System.Drawing.Point(31, 534);
            this.btnRename.Name = "btnRename";
            this.btnRename.Size = new System.Drawing.Size(75, 34);
            this.btnRename.TabIndex = 1;
            this.btnRename.Text = "重命名";
            this.btnRename.UseVisualStyleBackColor = true;
            this.btnRename.Click += new System.EventHandler(this.BtnRename_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.AutoSize = true;
            this.btnDelete.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnDelete.Location = new System.Drawing.Point(200, 534);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(60, 34);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "删除";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.BtnDelete_Click);
            // 
            // panelRight
            // 
            this.panelRight.Controls.Add(this.listParams);
            this.panelRight.Controls.Add(this.lblTip);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(312, 0);
            this.panelRight.Name = "panelRight";
            this.panelRight.Padding = new System.Windows.Forms.Padding(10);
            this.panelRight.Size = new System.Drawing.Size(853, 590);
            this.panelRight.TabIndex = 1;
            // 
            // listParams
            // 
            this.listParams.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colParam,
            this.colValue,
            this.colDesc});
            this.listParams.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listParams.Font = new System.Drawing.Font("Microsoft YaHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.listParams.FullRowSelect = true;
            this.listParams.HideSelection = false;
            this.listParams.Location = new System.Drawing.Point(10, 40);
            this.listParams.MultiSelect = false;
            this.listParams.Name = "listParams";
            this.listParams.Size = new System.Drawing.Size(833, 540);
            this.listParams.TabIndex = 1;
            this.listParams.UseCompatibleStateImageBehavior = false;
            this.listParams.View = System.Windows.Forms.View.Details;
            this.listParams.DoubleClick += new System.EventHandler(this.Params_DoubleClick);
            // 
            // colParam
            // 
            this.colParam.Text = "参数";
            this.colParam.Width = 110;
            // 
            // colValue
            // 
            this.colValue.Text = "值";
            this.colValue.Width = 180;
            // 
            // colDesc
            // 
            this.colDesc.Text = "说明";
            this.colDesc.Width = 539;
            // 
            // lblTip
            // 
            this.lblTip.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblTip.ForeColor = System.Drawing.Color.Gray;
            this.lblTip.Location = new System.Drawing.Point(10, 10);
            this.lblTip.Name = "lblTip";
            this.lblTip.Size = new System.Drawing.Size(833, 30);
            this.lblTip.TabIndex = 0;
            this.lblTip.Text = "选中左侧方案查看参数；双击参数行可修改值。";
            // 
            // ProfileManagerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1165, 590);
            this.Controls.Add(this.panelRight);
            this.Controls.Add(this.panelLeft);
            this.MinimumSize = new System.Drawing.Size(620, 380);
            this.Name = "ProfileManagerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "参数方案管理";
            this.panelLeft.ResumeLayout(false);
            this.panelLeft.PerformLayout();
            this.panelRight.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.ListBox listProfiles;
        private System.Windows.Forms.Button btnNew;
        private System.Windows.Forms.Button btnRename;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Panel panelRight;
        private System.Windows.Forms.ListView listParams;
        private System.Windows.Forms.ColumnHeader colParam;
        private System.Windows.Forms.ColumnHeader colValue;
        private System.Windows.Forms.ColumnHeader colDesc;
        private System.Windows.Forms.Label lblTip;
    }
}
