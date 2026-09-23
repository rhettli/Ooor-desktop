namespace ooor
{
    partial class ProxyManagerForm
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
            this.listProxies = new System.Windows.Forms.ListView();
            this.colUrl = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colState = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.colPing = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.lblSummary = new System.Windows.Forms.Label();
            this.btnEnable = new System.Windows.Forms.Button();
            this.btnDisable = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnDel = new System.Windows.Forms.Button();
            this.btnPingSel = new System.Windows.Forms.Button();
            this.btnPingAll = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SuspendLayout();
            // 
            // listProxies
            // 
            this.listProxies.CheckBoxes = true;
            this.listProxies.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colUrl,
            this.colState,
            this.colPing,
            this.columnHeader1});
            this.listProxies.FullRowSelect = true;
            this.listProxies.GridLines = true;
            this.listProxies.HideSelection = false;
            this.listProxies.Location = new System.Drawing.Point(12, 12);
            this.listProxies.MultiSelect = false;
            this.listProxies.Name = "listProxies";
            this.listProxies.Size = new System.Drawing.Size(1177, 335);
            this.listProxies.TabIndex = 0;
            this.listProxies.UseCompatibleStateImageBehavior = false;
            this.listProxies.View = System.Windows.Forms.View.Details;
            // 
            // colUrl
            // 
            this.colUrl.Text = "URL";
            this.colUrl.Width = 340;
            // 
            // colState
            // 
            this.colState.Text = "状态";
            this.colState.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.colState.Width = 70;
            // 
            // colPing
            // 
            this.colPing.Text = "测速";
            this.colPing.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.colPing.Width = 70;
            // 
            // lblSummary
            // 
            this.lblSummary.Location = new System.Drawing.Point(12, 474);
            this.lblSummary.Name = "lblSummary";
            this.lblSummary.Size = new System.Drawing.Size(842, 37);
            this.lblSummary.TabIndex = 1;
            // 
            // btnEnable
            // 
            this.btnEnable.Location = new System.Drawing.Point(12, 353);
            this.btnEnable.Name = "btnEnable";
            this.btnEnable.Size = new System.Drawing.Size(214, 30);
            this.btnEnable.TabIndex = 2;
            this.btnEnable.Text = "启用";
            this.btnEnable.UseVisualStyleBackColor = true;
            // 
            // btnDisable
            // 
            this.btnDisable.Location = new System.Drawing.Point(12, 389);
            this.btnDisable.Name = "btnDisable";
            this.btnDisable.Size = new System.Drawing.Size(214, 30);
            this.btnDisable.TabIndex = 3;
            this.btnDisable.Text = "禁用";
            this.btnDisable.UseVisualStyleBackColor = true;
            // 
            // btnUp
            // 
            this.btnUp.Location = new System.Drawing.Point(232, 353);
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(214, 30);
            this.btnUp.TabIndex = 4;
            this.btnUp.Text = "上移";
            this.btnUp.UseVisualStyleBackColor = true;
            // 
            // btnDown
            // 
            this.btnDown.Location = new System.Drawing.Point(232, 389);
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(214, 30);
            this.btnDown.TabIndex = 5;
            this.btnDown.Text = "下移";
            this.btnDown.UseVisualStyleBackColor = true;
            // 
            // btnAdd
            // 
            this.btnAdd.Location = new System.Drawing.Point(452, 353);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(214, 30);
            this.btnAdd.TabIndex = 6;
            this.btnAdd.Text = "新增 URL";
            this.btnAdd.UseVisualStyleBackColor = true;
            // 
            // btnEdit
            // 
            this.btnEdit.Location = new System.Drawing.Point(452, 389);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(214, 30);
            this.btnEdit.TabIndex = 7;
            this.btnEdit.Text = "编辑 URL";
            this.btnEdit.UseVisualStyleBackColor = true;
            // 
            // btnDel
            // 
            this.btnDel.Location = new System.Drawing.Point(12, 426);
            this.btnDel.Name = "btnDel";
            this.btnDel.Size = new System.Drawing.Size(214, 30);
            this.btnDel.TabIndex = 8;
            this.btnDel.Text = "删除";
            this.btnDel.UseVisualStyleBackColor = true;
            // 
            // btnPingSel
            // 
            this.btnPingSel.Location = new System.Drawing.Point(672, 389);
            this.btnPingSel.Name = "btnPingSel";
            this.btnPingSel.Size = new System.Drawing.Size(214, 30);
            this.btnPingSel.TabIndex = 9;
            this.btnPingSel.Text = "测速选中";
            this.btnPingSel.UseVisualStyleBackColor = true;
            // 
            // btnPingAll
            // 
            this.btnPingAll.Location = new System.Drawing.Point(672, 353);
            this.btnPingAll.Name = "btnPingAll";
            this.btnPingAll.Size = new System.Drawing.Size(214, 30);
            this.btnPingAll.TabIndex = 10;
            this.btnPingAll.Text = "测速全部";
            this.btnPingAll.UseVisualStyleBackColor = true;
            // 
            // btnReset
            // 
            this.btnReset.Location = new System.Drawing.Point(452, 426);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(214, 30);
            this.btnReset.TabIndex = 11;
            this.btnReset.Text = "恢复内置默认";
            this.btnReset.UseVisualStyleBackColor = true;
            // 
            // btnOpen
            // 
            this.btnOpen.Location = new System.Drawing.Point(232, 426);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(214, 30);
            this.btnOpen.TabIndex = 12;
            this.btnOpen.Text = "打开配置文件";
            this.btnOpen.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(983, 424);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(90, 32);
            this.btnOk.TabIndex = 13;
            this.btnOk.Text = "保存";
            this.btnOk.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(1081, 424);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 32);
            this.btnCancel.TabIndex = 14;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "描述";
            this.columnHeader1.Width = 280;
            // 
            // ProxyManagerForm
            // 
            this.AcceptButton = this.btnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(1201, 525);
            this.Controls.Add(this.listProxies);
            this.Controls.Add(this.lblSummary);
            this.Controls.Add(this.btnEnable);
            this.Controls.Add(this.btnDisable);
            this.Controls.Add(this.btnUp);
            this.Controls.Add(this.btnDown);
            this.Controls.Add(this.btnAdd);
            this.Controls.Add(this.btnEdit);
            this.Controls.Add(this.btnDel);
            this.Controls.Add(this.btnPingSel);
            this.Controls.Add(this.btnPingAll);
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProxyManagerForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "管理 GitHub 加速代理";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView listProxies;
        private System.Windows.Forms.ColumnHeader colUrl;
        private System.Windows.Forms.ColumnHeader colState;
        private System.Windows.Forms.ColumnHeader colPing;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnEnable;
        private System.Windows.Forms.Button btnDisable;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDel;
        private System.Windows.Forms.Button btnPingSel;
        private System.Windows.Forms.Button btnPingAll;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ColumnHeader columnHeader1;
    }
}
