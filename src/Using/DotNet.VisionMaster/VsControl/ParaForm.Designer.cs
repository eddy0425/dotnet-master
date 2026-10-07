namespace DotNet.VisionMaster
{
    partial class ParaForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ParaForm));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.pnl_actions = new System.Windows.Forms.Panel();
            this.btn_saveEdit = new System.Windows.Forms.Button();
            this.btn_runTest = new System.Windows.Forms.Button();
            this.btn_cancelEdit = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl1.SuspendLayout();
            this.pnl_actions.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(560, 263);
            this.tabControl1.TabIndex = 0;
            // 
            // pnl_actions
            // 
            this.pnl_actions.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.pnl_actions.Controls.Add(this.btn_saveEdit);
            this.pnl_actions.Controls.Add(this.btn_runTest);
            this.pnl_actions.Controls.Add(this.btn_cancelEdit);
            this.pnl_actions.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnl_actions.Location = new System.Drawing.Point(0, 263);
            this.pnl_actions.Name = "pnl_actions";
            this.pnl_actions.Padding = new System.Windows.Forms.Padding(2);
            this.pnl_actions.Size = new System.Drawing.Size(560, 27);
            this.pnl_actions.TabIndex = 1;
            // 
            // btn_saveEdit
            // 
            this.btn_saveEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_saveEdit.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.btn_saveEdit.FlatAppearance.BorderSize = 0;
            this.btn_saveEdit.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(90)))), ((int)(((byte)(70)))));
            this.btn_saveEdit.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(65)))), ((int)(((byte)(50)))));
            this.btn_saveEdit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_saveEdit.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_saveEdit.ForeColor = System.Drawing.Color.White;
            this.btn_saveEdit.Image = ((System.Drawing.Image)(resources.GetObject("btn_saveEdit.Image")));
            this.btn_saveEdit.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btn_saveEdit.Location = new System.Drawing.Point(462, -1);
            this.btn_saveEdit.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.btn_saveEdit.Name = "btn_saveEdit";
            this.btn_saveEdit.Size = new System.Drawing.Size(96, 24);
            this.btn_saveEdit.TabIndex = 1;
            this.btn_saveEdit.Text = "保存参数";
            this.btn_saveEdit.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.toolTip1.SetToolTip(this.btn_saveEdit, "确认当前修改，此后取消编辑回到这里。\r\n写入磁盘请用 方案 → 保存。");
            this.btn_saveEdit.UseVisualStyleBackColor = false;
            this.btn_saveEdit.Click += new System.EventHandler(this.btn_saveEdit_Click);
            // 
            // btn_runTest
            // 
            this.btn_runTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btn_runTest.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.btn_runTest.FlatAppearance.BorderSize = 0;
            this.btn_runTest.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(90)))), ((int)(((byte)(70)))));
            this.btn_runTest.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(65)))), ((int)(((byte)(50)))));
            this.btn_runTest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_runTest.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_runTest.ForeColor = System.Drawing.Color.White;
            this.btn_runTest.Image = ((System.Drawing.Image)(resources.GetObject("btn_runTest.Image")));
            this.btn_runTest.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btn_runTest.Location = new System.Drawing.Point(360, -1);
            this.btn_runTest.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.btn_runTest.Name = "btn_runTest";
            this.btn_runTest.Size = new System.Drawing.Size(96, 24);
            this.btn_runTest.TabIndex = 0;
            this.btn_runTest.Text = "运行测试";
            this.btn_runTest.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.toolTip1.SetToolTip(this.btn_runTest, "按当前参数只运行本工具，上游沿用上一轮的结果。");
            this.btn_runTest.UseVisualStyleBackColor = false;
            this.btn_runTest.Click += new System.EventHandler(this.btn_runTest_Click);
            // 
            // btn_cancelEdit
            // 
            this.btn_cancelEdit.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.btn_cancelEdit.Enabled = false;
            this.btn_cancelEdit.FlatAppearance.BorderSize = 0;
            this.btn_cancelEdit.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(90)))), ((int)(((byte)(70)))));
            this.btn_cancelEdit.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(65)))), ((int)(((byte)(50)))));
            this.btn_cancelEdit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_cancelEdit.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_cancelEdit.ForeColor = System.Drawing.Color.White;
            this.btn_cancelEdit.Image = ((System.Drawing.Image)(resources.GetObject("btn_cancelEdit.Image")));
            this.btn_cancelEdit.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btn_cancelEdit.Location = new System.Drawing.Point(2, -1);
            this.btn_cancelEdit.Name = "btn_cancelEdit";
            this.btn_cancelEdit.Size = new System.Drawing.Size(96, 23);
            this.btn_cancelEdit.TabIndex = 0;
            this.btn_cancelEdit.Text = "取消编辑";
            this.btn_cancelEdit.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
            this.toolTip1.SetToolTip(this.btn_cancelEdit, "撤销进入本工具或上次保存参数之后的参数与 ROI 修改，并切回信息窗口。\r\n模板文件新建 / 修改后立即生效，不在撤销范围内。");
            this.btn_cancelEdit.UseVisualStyleBackColor = false;
            this.btn_cancelEdit.Click += new System.EventHandler(this.btn_cancelEdit_Click);
            // 
            // ParaForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.pnl_actions);
            this.Name = "ParaForm";
            this.Size = new System.Drawing.Size(560, 290);
            this.Load += new System.EventHandler(this.ParaForm_Load);
            this.tabControl1.ResumeLayout(false);
            this.pnl_actions.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.Panel pnl_actions;
        private System.Windows.Forms.Button btn_cancelEdit;
        private System.Windows.Forms.Button btn_saveEdit;
        private System.Windows.Forms.Button btn_runTest;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
