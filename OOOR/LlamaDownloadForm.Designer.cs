namespace ooor
{
    partial class LlamaDownloadForm
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
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.lstVersions = new System.Windows.Forms.ListBox();
            this.label_ok_cpu = new System.Windows.Forms.Label();
            this.label_ok_cudacuda = new System.Windows.Forms.Label();
            this.label_ok_rocm = new System.Windows.Forms.Label();
            this.label_ok_sycl = new System.Windows.Forms.Label();
            this.label_ok_vulkan = new System.Windows.Forms.Label();
            this.button5 = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.sepMirror = new System.Windows.Forms.ToolStripSeparator();
            this.lblMirror = new System.Windows.Forms.ToolStripLabel();
            this.cmbMirror = new System.Windows.Forms.ToolStripComboBox();
            this.btnRefresh = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.btnOpenInBrowser = new System.Windows.Forms.ToolStripButton();
            this.button3 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.lstVersions);
            this.splitContainer1.Panel1MinSize = 140;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label_ok_cpu);
            this.splitContainer1.Panel2.Controls.Add(this.label_ok_cudacuda);
            this.splitContainer1.Panel2.Controls.Add(this.label_ok_rocm);
            this.splitContainer1.Panel2.Controls.Add(this.label_ok_sycl);
            this.splitContainer1.Panel2.Controls.Add(this.label_ok_vulkan);
            this.splitContainer1.Panel2.Controls.Add(this.button5);
            this.splitContainer1.Panel2.Controls.Add(this.button4);
            this.splitContainer1.Panel2.Controls.Add(this.toolStrip);
            this.splitContainer1.Panel2.Controls.Add(this.button3);
            this.splitContainer1.Panel2.Controls.Add(this.button2);
            this.splitContainer1.Panel2.Controls.Add(this.button1);
            this.splitContainer1.Size = new System.Drawing.Size(942, 499);
            this.splitContainer1.SplitterDistance = 260;
            this.splitContainer1.SplitterWidth = 6;
            this.splitContainer1.TabIndex = 1;
            // 
            // lstVersions
            // 
            this.lstVersions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lstVersions.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.lstVersions.FormattingEnabled = true;
            this.lstVersions.IntegralHeight = false;
            this.lstVersions.ItemHeight = 27;
            this.lstVersions.Location = new System.Drawing.Point(0, 0);
            this.lstVersions.Margin = new System.Windows.Forms.Padding(4);
            this.lstVersions.Name = "lstVersions";
            this.lstVersions.Size = new System.Drawing.Size(260, 499);
            this.lstVersions.TabIndex = 0;
            this.lstVersions.SelectedIndexChanged += new System.EventHandler(this.lstVersions_SelectedIndexChanged);
            // 
            // label_ok_cpu
            // 
            this.label_ok_cpu.AutoSize = true;
            this.label_ok_cpu.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_ok_cpu.ForeColor = System.Drawing.Color.LimeGreen;
            this.label_ok_cpu.Location = new System.Drawing.Point(401, 110);
            this.label_ok_cpu.Name = "label_ok_cpu";
            this.label_ok_cpu.Size = new System.Drawing.Size(43, 30);
            this.label_ok_cpu.TabIndex = 7;
            this.label_ok_cpu.Text = "✔️";
            this.label_ok_cpu.Visible = false;
            // 
            // label_ok_cudacuda
            // 
            this.label_ok_cudacuda.AutoSize = true;
            this.label_ok_cudacuda.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_ok_cudacuda.ForeColor = System.Drawing.Color.LimeGreen;
            this.label_ok_cudacuda.Location = new System.Drawing.Point(401, 183);
            this.label_ok_cudacuda.Name = "label_ok_cudacuda";
            this.label_ok_cudacuda.Size = new System.Drawing.Size(43, 30);
            this.label_ok_cudacuda.TabIndex = 8;
            this.label_ok_cudacuda.Text = "✔️";
            this.label_ok_cudacuda.Visible = false;
            // 
            // label_ok_rocm
            // 
            this.label_ok_rocm.AutoSize = true;
            this.label_ok_rocm.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_ok_rocm.ForeColor = System.Drawing.Color.LimeGreen;
            this.label_ok_rocm.Location = new System.Drawing.Point(401, 258);
            this.label_ok_rocm.Name = "label_ok_rocm";
            this.label_ok_rocm.Size = new System.Drawing.Size(43, 30);
            this.label_ok_rocm.TabIndex = 9;
            this.label_ok_rocm.Text = "✔️";
            this.label_ok_rocm.Visible = false;
            // 
            // label_ok_sycl
            // 
            this.label_ok_sycl.AutoSize = true;
            this.label_ok_sycl.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_ok_sycl.ForeColor = System.Drawing.Color.LimeGreen;
            this.label_ok_sycl.Location = new System.Drawing.Point(401, 325);
            this.label_ok_sycl.Name = "label_ok_sycl";
            this.label_ok_sycl.Size = new System.Drawing.Size(43, 30);
            this.label_ok_sycl.TabIndex = 10;
            this.label_ok_sycl.Text = "✔️";
            this.label_ok_sycl.Visible = false;
            // 
            // label_ok_vulkan
            // 
            this.label_ok_vulkan.AutoSize = true;
            this.label_ok_vulkan.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_ok_vulkan.ForeColor = System.Drawing.Color.LimeGreen;
            this.label_ok_vulkan.Location = new System.Drawing.Point(401, 393);
            this.label_ok_vulkan.Name = "label_ok_vulkan";
            this.label_ok_vulkan.Size = new System.Drawing.Size(43, 30);
            this.label_ok_vulkan.TabIndex = 11;
            this.label_ok_vulkan.Text = "✔️";
            this.label_ok_vulkan.Visible = false;
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(93, 255);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(302, 42);
            this.button5.TabIndex = 6;
            this.button5.Text = "安装ROCm版本";
            this.button5.UseVisualStyleBackColor = true;
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(93, 322);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(302, 42);
            this.button4.TabIndex = 5;
            this.button4.Text = "安装SYCL版本";
            this.button4.UseVisualStyleBackColor = true;
            // 
            // toolStrip
            // 
            this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sepMirror,
            this.lblMirror,
            this.cmbMirror,
            this.btnRefresh,
            this.toolStripSeparator1,
            this.btnOpenInBrowser});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Size = new System.Drawing.Size(676, 33);
            this.toolStrip.TabIndex = 0;
            // 
            // sepMirror
            // 
            this.sepMirror.Name = "sepMirror";
            this.sepMirror.Size = new System.Drawing.Size(6, 33);
            // 
            // lblMirror
            // 
            this.lblMirror.Name = "lblMirror";
            this.lblMirror.Size = new System.Drawing.Size(82, 28);
            this.lblMirror.Text = "镜像源：";
            // 
            // cmbMirror
            // 
            this.cmbMirror.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMirror.Items.AddRange(new object[] {
            "GitHub",
            "Cloudflare"});
            this.cmbMirror.Name = "cmbMirror";
            this.cmbMirror.Size = new System.Drawing.Size(110, 33);
            // 
            // btnRefresh
            // 
            this.btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnRefresh.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(50, 28);
            this.btnRefresh.Text = "拉取";
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 33);
            // 
            // btnOpenInBrowser
            // 
            this.btnOpenInBrowser.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.btnOpenInBrowser.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.btnOpenInBrowser.Name = "btnOpenInBrowser";
            this.btnOpenInBrowser.Size = new System.Drawing.Size(122, 28);
            this.btnOpenInBrowser.Text = "在浏览器打开";
            this.btnOpenInBrowser.Click += new System.EventHandler(this.btnOpenInBrowser_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(93, 180);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(302, 42);
            this.button3.TabIndex = 4;
            this.button3.Text = "安装CPU+CUDA版本";
            this.button3.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(93, 390);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(302, 42);
            this.button2.TabIndex = 3;
            this.button2.Text = "安装Vulkan版本";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(93, 107);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(302, 42);
            this.button1.TabIndex = 2;
            this.button1.Text = "安装CPU版本";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.statusStrip.Location = new System.Drawing.Point(0, 502);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(942, 31);
            this.statusStrip.TabIndex = 2;
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(927, 24);
            this.lblStatus.Spring = true;
            this.lblStatus.Text = "正在加载…";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // LlamaDownloadForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(942, 533);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.splitContainer1);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(640, 320);
            this.Name = "LlamaDownloadForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "下载 llama.cpp 版本 (ggml-org/llama.cpp)";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListBox lstVersions;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton btnRefresh;
        private System.Windows.Forms.ToolStripButton btnOpenInBrowser;
        private System.Windows.Forms.ToolStripSeparator sepMirror;
        private System.Windows.Forms.ToolStripLabel lblMirror;
        private System.Windows.Forms.ToolStripComboBox cmbMirror;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.Label label_ok_cpu;
        private System.Windows.Forms.Label label_ok_cudacuda;
        private System.Windows.Forms.Label label_ok_rocm;
        private System.Windows.Forms.Label label_ok_sycl;
        private System.Windows.Forms.Label label_ok_vulkan;
    }
}
