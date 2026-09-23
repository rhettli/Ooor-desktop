namespace ooor
{
    /// <summary>
    /// Agent 新建/编辑对话框的设计器分部类：
    /// 控件全部手动创建（不依赖 VS Designer 生成），便于随逻辑一起维护。
    /// 布局：名称 / 完全开发权限 / 使用系统提示词 / 默认模型 / 系统提示词 / 绑定内置函数 / 绑定 MCP / 确定·取消。
    /// </summary>
    partial class AgentEditForm
    {
        /// <summary>Required designer variable.</summary>
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>设计器支持所需方法 - 不要用代码编辑器改这里的内容。</summary>
        private void InitializeComponent()
        {
            this.lblName = new System.Windows.Forms.Label();
            this.txtName = new System.Windows.Forms.TextBox();
            this.chkFullDev = new System.Windows.Forms.CheckBox();
            this.chkUseSystemPrompt = new System.Windows.Forms.CheckBox();
            this.lblModel = new System.Windows.Forms.Label();
            this.cmbModel = new System.Windows.Forms.ComboBox();
            this.lblPrompt = new System.Windows.Forms.Label();
            this.txtPrompt = new System.Windows.Forms.TextBox();
            this.lblTools = new System.Windows.Forms.Label();
            this.clbTools = new System.Windows.Forms.CheckedListBox();
            this.lblMcp = new System.Windows.Forms.Label();
            this.clbMcp = new System.Windows.Forms.CheckedListBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.txtUserPrompt = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // lblName
            // 
            this.lblName.AutoSize = true;
            this.lblName.Location = new System.Drawing.Point(16, 15);
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(64, 24);
            this.lblName.TabIndex = 0;
            this.lblName.Text = "名称：";
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(20, 47);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(544, 30);
            this.txtName.TabIndex = 1;
            // 
            // chkFullDev
            // 
            this.chkFullDev.Location = new System.Drawing.Point(20, 83);
            this.chkFullDev.Name = "chkFullDev";
            this.chkFullDev.Size = new System.Drawing.Size(544, 30);
            this.chkFullDev.TabIndex = 2;
            this.chkFullDev.Text = "完全开发权限（允许写文件 / 执行命令 / 联网等高危工具）";
            // 
            // chkUseSystemPrompt
            // 
            this.chkUseSystemPrompt.Checked = true;
            this.chkUseSystemPrompt.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUseSystemPrompt.Location = new System.Drawing.Point(20, 119);
            this.chkUseSystemPrompt.Name = "chkUseSystemPrompt";
            this.chkUseSystemPrompt.Size = new System.Drawing.Size(544, 25);
            this.chkUseSystemPrompt.TabIndex = 3;
            this.chkUseSystemPrompt.Text = "使用系统提示词（勾选则下发系统 prompt，可在下方补充；不勾选仅用用户输入）";
            this.chkUseSystemPrompt.CheckStateChanged += new System.EventHandler(this.chkUseSystemPrompt_CheckStateChanged);
            // 
            // lblModel
            // 
            this.lblModel.AutoSize = true;
            this.lblModel.Location = new System.Drawing.Point(16, 162);
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(100, 24);
            this.lblModel.TabIndex = 4;
            this.lblModel.Text = "默认模型：";
            // 
            // cmbModel
            // 
            this.cmbModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbModel.Location = new System.Drawing.Point(20, 189);
            this.cmbModel.Name = "cmbModel";
            this.cmbModel.Size = new System.Drawing.Size(544, 32);
            this.cmbModel.TabIndex = 5;
            // 
            // lblPrompt
            // 
            this.lblPrompt.AutoSize = true;
            this.lblPrompt.Location = new System.Drawing.Point(582, 20);
            this.lblPrompt.Name = "lblPrompt";
            this.lblPrompt.Size = new System.Drawing.Size(118, 24);
            this.lblPrompt.TabIndex = 6;
            this.lblPrompt.Text = "系统提示词：";
            // 
            // txtPrompt
            // 
            this.txtPrompt.Location = new System.Drawing.Point(586, 47);
            this.txtPrompt.Multiline = true;
            this.txtPrompt.Name = "txtPrompt";
            this.txtPrompt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtPrompt.Size = new System.Drawing.Size(544, 310);
            this.txtPrompt.TabIndex = 7;
            // 
            // lblTools
            // 
            this.lblTools.AutoSize = true;
            this.lblTools.Location = new System.Drawing.Point(16, 238);
            this.lblTools.Name = "lblTools";
            this.lblTools.Size = new System.Drawing.Size(136, 24);
            this.lblTools.TabIndex = 8;
            this.lblTools.Text = "绑定内置函数：";
            // 
            // clbTools
            // 
            this.clbTools.CheckOnClick = true;
            this.clbTools.Location = new System.Drawing.Point(20, 270);
            this.clbTools.Name = "clbTools";
            this.clbTools.Size = new System.Drawing.Size(544, 220);
            this.clbTools.TabIndex = 9;
            // 
            // lblMcp
            // 
            this.lblMcp.AutoSize = true;
            this.lblMcp.Location = new System.Drawing.Point(16, 509);
            this.lblMcp.Name = "lblMcp";
            this.lblMcp.Size = new System.Drawing.Size(169, 24);
            this.lblMcp.TabIndex = 10;
            this.lblMcp.Text = "绑定 MCP 服务器：";
            // 
            // clbMcp
            // 
            this.clbMcp.CheckOnClick = true;
            this.clbMcp.Location = new System.Drawing.Point(20, 542);
            this.clbMcp.Name = "clbMcp";
            this.clbMcp.Size = new System.Drawing.Size(544, 193);
            this.clbMcp.TabIndex = 11;
            // 
            // btnOk
            // 
            this.btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOk.Location = new System.Drawing.Point(764, 700);
            this.btnOk.Name = "btnOk";
            this.btnOk.Size = new System.Drawing.Size(80, 30);
            this.btnOk.TabIndex = 12;
            this.btnOk.Text = "确定";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(888, 700);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(80, 30);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "取消";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(582, 373);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(172, 24);
            this.label1.TabIndex = 14;
            this.label1.Text = "自定义系统提示词：";
            // 
            // txtUserPrompt
            // 
            this.txtUserPrompt.Location = new System.Drawing.Point(586, 400);
            this.txtUserPrompt.Multiline = true;
            this.txtUserPrompt.Name = "txtUserPrompt";
            this.txtUserPrompt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtUserPrompt.Size = new System.Drawing.Size(544, 256);
            this.txtUserPrompt.TabIndex = 15;
            // 
            // AgentEditForm
            // 
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(1147, 752);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtUserPrompt);
            this.Controls.Add(this.lblName);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.chkFullDev);
            this.Controls.Add(this.chkUseSystemPrompt);
            this.Controls.Add(this.lblModel);
            this.Controls.Add(this.cmbModel);
            this.Controls.Add(this.lblPrompt);
            this.Controls.Add(this.txtPrompt);
            this.Controls.Add(this.lblTools);
            this.Controls.Add(this.clbTools);
            this.Controls.Add(this.lblMcp);
            this.Controls.Add(this.clbMcp);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.btnCancel);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AgentEditForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        // 字段
        private System.Windows.Forms.Label lblName;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.CheckBox chkFullDev;
        private System.Windows.Forms.CheckBox chkUseSystemPrompt;
        private System.Windows.Forms.Label lblModel;
        private System.Windows.Forms.ComboBox cmbModel;
        private System.Windows.Forms.Label lblPrompt;
        private System.Windows.Forms.TextBox txtPrompt;
        private System.Windows.Forms.Label lblTools;
        private System.Windows.Forms.CheckedListBox clbTools;
        private System.Windows.Forms.Label lblMcp;
        private System.Windows.Forms.CheckedListBox clbMcp;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtUserPrompt;
    }
}
