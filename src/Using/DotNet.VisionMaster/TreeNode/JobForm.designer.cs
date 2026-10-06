namespace DotNet.VisionMaster
{
    partial class JobForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(JobForm));
            this.tbc_jobs = new System.Windows.Forms.TabControl();
            this.tab_flow = new System.Windows.Forms.TabPage();
            this.lst_tools = new System.Windows.Forms.ListBox();
            this.txt_name = new System.Windows.Forms.TextBox();
            this.pnl_bottom = new System.Windows.Forms.Panel();
            this.pnl_toolOps = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_add = new System.Windows.Forms.Button();
            this.btn_remove = new System.Windows.Forms.Button();
            this.btn_rename = new System.Windows.Forms.Button();
            this.pnl_sep1 = new System.Windows.Forms.Panel();
            this.pnl_sep2 = new System.Windows.Forms.Panel();
            this.pnl_sep3 = new System.Windows.Forms.Panel();
            this.pnl_run = new System.Windows.Forms.Panel();
            this.btn_runLoop = new System.Windows.Forms.Button();
            this.btn_runOnce = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.cms_tool = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnu_editPara = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_rename = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_runCurrent = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_sep1 = new System.Windows.Forms.ToolStripSeparator();
            this.mnu_delete = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_sep2 = new System.Windows.Forms.ToolStripSeparator();
            this.mnu_up = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_down = new System.Windows.Forms.ToolStripMenuItem();
            this.tbc_jobs.SuspendLayout();
            this.tab_flow.SuspendLayout();
            this.lst_tools.SuspendLayout();
            this.pnl_bottom.SuspendLayout();
            this.pnl_toolOps.SuspendLayout();
            this.pnl_run.SuspendLayout();
            this.cms_tool.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbc_jobs
            // 
            this.tbc_jobs.Controls.Add(this.tab_flow);
            this.tbc_jobs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbc_jobs.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tbc_jobs.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tbc_jobs.ItemSize = new System.Drawing.Size(80, 22);
            this.tbc_jobs.Location = new System.Drawing.Point(0, 0);
            this.tbc_jobs.Margin = new System.Windows.Forms.Padding(0);
            this.tbc_jobs.Name = "tbc_jobs";
            this.tbc_jobs.SelectedIndex = 0;
            this.tbc_jobs.Size = new System.Drawing.Size(278, 667);
            this.tbc_jobs.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tbc_jobs.TabIndex = 0;
            this.tbc_jobs.TabStop = false;
            this.tbc_jobs.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.tbc_jobs_DrawItem);
            // 
            // tab_flow
            // 
            this.tab_flow.BackColor = System.Drawing.Color.Gainsboro;
            this.tab_flow.Controls.Add(this.lst_tools);
            this.tab_flow.Location = new System.Drawing.Point(4, 26);
            this.tab_flow.Name = "tab_flow";
            this.tab_flow.Padding = new System.Windows.Forms.Padding(2);
            this.tab_flow.Size = new System.Drawing.Size(270, 637);
            this.tab_flow.TabIndex = 0;
            this.tab_flow.Text = "流程";
            // 
            // lst_tools
            // 
            this.lst_tools.AllowDrop = true;
            this.lst_tools.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(33)))), ((int)(((byte)(42)))));
            this.lst_tools.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lst_tools.Controls.Add(this.txt_name);
            this.lst_tools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lst_tools.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.lst_tools.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lst_tools.ForeColor = System.Drawing.Color.White;
            this.lst_tools.IntegralHeight = false;
            this.lst_tools.ItemHeight = 17;
            this.lst_tools.Location = new System.Drawing.Point(2, 2);
            this.lst_tools.Name = "lst_tools";
            this.lst_tools.Size = new System.Drawing.Size(266, 633);
            this.lst_tools.TabIndex = 0;
            this.lst_tools.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.lst_tools_DrawItem);
            this.lst_tools.SelectedIndexChanged += new System.EventHandler(this.lst_tools_SelectedIndexChanged);
            this.lst_tools.DragDrop += new System.Windows.Forms.DragEventHandler(this.lst_tools_DragDrop);
            this.lst_tools.DragEnter += new System.Windows.Forms.DragEventHandler(this.lst_tools_DragEnter);
            this.lst_tools.MouseDown += new System.Windows.Forms.MouseEventHandler(this.lst_tools_MouseDown);
            // 
            // txt_name
            // 
            this.txt_name.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txt_name.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txt_name.Location = new System.Drawing.Point(30, 4);
            this.txt_name.Name = "txt_name";
            this.txt_name.Size = new System.Drawing.Size(200, 23);
            this.txt_name.TabIndex = 1;
            this.txt_name.Visible = false;
            this.txt_name.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txt_name_KeyDown);
            this.txt_name.Leave += new System.EventHandler(this.txt_name_Leave);
            // 
            // pnl_bottom
            // 
            this.pnl_bottom.BackColor = System.Drawing.Color.White;
            this.pnl_bottom.Controls.Add(this.pnl_toolOps);
            this.pnl_bottom.Controls.Add(this.pnl_run);
            this.pnl_bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnl_bottom.Location = new System.Drawing.Point(0, 667);
            this.pnl_bottom.Name = "pnl_bottom";
            this.pnl_bottom.Size = new System.Drawing.Size(278, 45);
            this.pnl_bottom.TabIndex = 1;
            // 
            // pnl_toolOps
            // 
            this.pnl_toolOps.Controls.Add(this.btn_add);
            this.pnl_toolOps.Controls.Add(this.btn_remove);
            this.pnl_toolOps.Controls.Add(this.btn_rename);
            this.pnl_toolOps.Controls.Add(this.pnl_sep1);
            this.pnl_toolOps.Controls.Add(this.pnl_sep2);
            this.pnl_toolOps.Controls.Add(this.pnl_sep3);
            this.pnl_toolOps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnl_toolOps.Location = new System.Drawing.Point(0, 0);
            this.pnl_toolOps.Name = "pnl_toolOps";
            this.pnl_toolOps.Padding = new System.Windows.Forms.Padding(4, 7, 0, 0);
            this.pnl_toolOps.Size = new System.Drawing.Size(114, 45);
            this.pnl_toolOps.TabIndex = 0;
            this.pnl_toolOps.WrapContents = false;
            // 
            // btn_add
            // 
            this.btn_add.BackgroundImage = global::DotNet.VisionMaster.Properties.Resources.FlowAdd;
            this.btn_add.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btn_add.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_add.FlatAppearance.BorderSize = 0;
            this.btn_add.FlatAppearance.MouseOverBackColor = System.Drawing.Color.AliceBlue;
            this.btn_add.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_add.Location = new System.Drawing.Point(6, 9);
            this.btn_add.Margin = new System.Windows.Forms.Padding(2, 2, 4, 2);
            this.btn_add.Name = "btn_add";
            this.btn_add.Size = new System.Drawing.Size(22, 28);
            this.btn_add.TabIndex = 0;
            this.toolTip1.SetToolTip(this.btn_add, "添加工具（打开工具箱）");
            this.btn_add.UseVisualStyleBackColor = true;
            this.btn_add.Click += new System.EventHandler(this.btn_add_Click);
            // 
            // btn_remove
            // 
            this.btn_remove.BackgroundImage = global::DotNet.VisionMaster.Properties.Resources.FlowDelete;
            this.btn_remove.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btn_remove.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_remove.FlatAppearance.BorderSize = 0;
            this.btn_remove.FlatAppearance.MouseOverBackColor = System.Drawing.Color.AliceBlue;
            this.btn_remove.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_remove.Location = new System.Drawing.Point(34, 9);
            this.btn_remove.Margin = new System.Windows.Forms.Padding(2, 2, 4, 2);
            this.btn_remove.Name = "btn_remove";
            this.btn_remove.Size = new System.Drawing.Size(22, 28);
            this.btn_remove.TabIndex = 1;
            this.toolTip1.SetToolTip(this.btn_remove, "删除当前工具");
            this.btn_remove.UseVisualStyleBackColor = true;
            this.btn_remove.Click += new System.EventHandler(this.btn_remove_Click);
            // 
            // btn_rename
            // 
            this.btn_rename.BackgroundImage = global::DotNet.VisionMaster.Properties.Resources.FlowRename;
            this.btn_rename.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btn_rename.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_rename.FlatAppearance.BorderSize = 0;
            this.btn_rename.FlatAppearance.MouseOverBackColor = System.Drawing.Color.AliceBlue;
            this.btn_rename.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_rename.Location = new System.Drawing.Point(62, 9);
            this.btn_rename.Margin = new System.Windows.Forms.Padding(2, 2, 4, 2);
            this.btn_rename.Name = "btn_rename";
            this.btn_rename.Size = new System.Drawing.Size(22, 28);
            this.btn_rename.TabIndex = 2;
            this.toolTip1.SetToolTip(this.btn_rename, "重命名当前工具");
            this.btn_rename.UseVisualStyleBackColor = true;
            this.btn_rename.Click += new System.EventHandler(this.btn_rename_Click);
            // 
            // pnl_sep1
            // 
            this.pnl_sep1.BackColor = System.Drawing.Color.Silver;
            this.pnl_sep1.Location = new System.Drawing.Point(94, 13);
            this.pnl_sep1.Margin = new System.Windows.Forms.Padding(6, 6, 0, 0);
            this.pnl_sep1.Name = "pnl_sep1";
            this.pnl_sep1.Size = new System.Drawing.Size(1, 18);
            this.pnl_sep1.TabIndex = 3;
            // 
            // pnl_sep2
            // 
            this.pnl_sep2.BackColor = System.Drawing.Color.Silver;
            this.pnl_sep2.Location = new System.Drawing.Point(98, 13);
            this.pnl_sep2.Margin = new System.Windows.Forms.Padding(3, 6, 0, 0);
            this.pnl_sep2.Name = "pnl_sep2";
            this.pnl_sep2.Size = new System.Drawing.Size(1, 18);
            this.pnl_sep2.TabIndex = 4;
            // 
            // pnl_sep3
            // 
            this.pnl_sep3.BackColor = System.Drawing.Color.Silver;
            this.pnl_sep3.Location = new System.Drawing.Point(102, 13);
            this.pnl_sep3.Margin = new System.Windows.Forms.Padding(3, 6, 0, 0);
            this.pnl_sep3.Name = "pnl_sep3";
            this.pnl_sep3.Size = new System.Drawing.Size(1, 18);
            this.pnl_sep3.TabIndex = 5;
            // 
            // pnl_run
            // 
            this.pnl_run.Controls.Add(this.btn_runLoop);
            this.pnl_run.Controls.Add(this.btn_runOnce);
            this.pnl_run.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnl_run.Location = new System.Drawing.Point(114, 0);
            this.pnl_run.Name = "pnl_run";
            this.pnl_run.Size = new System.Drawing.Size(164, 45);
            this.pnl_run.TabIndex = 1;
            // 
            // btn_runLoop
            // 
            this.btn_runLoop.BackColor = System.Drawing.Color.Transparent;
            this.btn_runLoop.BackgroundImage = global::DotNet.VisionMaster.Properties.Resources.RunButton;
            this.btn_runLoop.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btn_runLoop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_runLoop.FlatAppearance.BorderSize = 0;
            this.btn_runLoop.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.btn_runLoop.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.btn_runLoop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_runLoop.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_runLoop.ForeColor = System.Drawing.Color.White;
            this.btn_runLoop.Location = new System.Drawing.Point(2, 6);
            this.btn_runLoop.Name = "btn_runLoop";
            this.btn_runLoop.Size = new System.Drawing.Size(76, 34);
            this.btn_runLoop.TabIndex = 0;
            this.btn_runLoop.Text = "连续运行";
            this.toolTip1.SetToolTip(this.btn_runLoop, "反复运行整个流程，失败时自动停止");
            this.btn_runLoop.UseVisualStyleBackColor = false;
            this.btn_runLoop.Click += new System.EventHandler(this.btn_runLoop_Click);
            // 
            // btn_runOnce
            // 
            this.btn_runOnce.BackColor = System.Drawing.Color.Transparent;
            this.btn_runOnce.BackgroundImage = global::DotNet.VisionMaster.Properties.Resources.RunButton;
            this.btn_runOnce.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.btn_runOnce.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btn_runOnce.FlatAppearance.BorderSize = 0;
            this.btn_runOnce.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.btn_runOnce.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.btn_runOnce.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_runOnce.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_runOnce.ForeColor = System.Drawing.Color.White;
            this.btn_runOnce.Location = new System.Drawing.Point(84, 6);
            this.btn_runOnce.Name = "btn_runOnce";
            this.btn_runOnce.Size = new System.Drawing.Size(76, 34);
            this.btn_runOnce.TabIndex = 1;
            this.btn_runOnce.Text = "单次运行";
            this.toolTip1.SetToolTip(this.btn_runOnce, "按顺序运行一次整个流程");
            this.btn_runOnce.UseVisualStyleBackColor = false;
            this.btn_runOnce.Click += new System.EventHandler(this.btn_runOnce_Click);
            // 
            // cms_tool
            // 
            this.cms_tool.BackColor = System.Drawing.Color.White;
            this.cms_tool.Font = new System.Drawing.Font("楷体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cms_tool.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnu_editPara,
            this.mnu_rename,
            this.mnu_runCurrent,
            this.mnu_sep1,
            this.mnu_delete,
            this.mnu_sep2,
            this.mnu_up,
            this.mnu_down});
            this.cms_tool.Name = "cms_tool";
            this.cms_tool.Size = new System.Drawing.Size(131, 148);
            // 
            // mnu_editPara
            // 
            this.mnu_editPara.Name = "mnu_editPara";
            this.mnu_editPara.Size = new System.Drawing.Size(130, 22);
            this.mnu_editPara.Text = "编辑参数";
            this.mnu_editPara.Click += new System.EventHandler(this.mnu_editPara_Click);
            // 
            // mnu_rename
            // 
            this.mnu_rename.Name = "mnu_rename";
            this.mnu_rename.Size = new System.Drawing.Size(130, 22);
            this.mnu_rename.Text = "重命名";
            this.mnu_rename.Click += new System.EventHandler(this.btn_rename_Click);
            // 
            // mnu_runCurrent
            // 
            this.mnu_runCurrent.Name = "mnu_runCurrent";
            this.mnu_runCurrent.Size = new System.Drawing.Size(130, 22);
            this.mnu_runCurrent.Text = "运行当前";
            this.mnu_runCurrent.Click += new System.EventHandler(this.mnu_runCurrent_Click);
            // 
            // mnu_sep1
            // 
            this.mnu_sep1.Name = "mnu_sep1";
            this.mnu_sep1.Size = new System.Drawing.Size(127, 6);
            // 
            // mnu_delete
            // 
            this.mnu_delete.Image = global::DotNet.VisionMaster.Properties.Resources.MenuDelete;
            this.mnu_delete.Name = "mnu_delete";
            this.mnu_delete.Size = new System.Drawing.Size(130, 22);
            this.mnu_delete.Text = "删除";
            this.mnu_delete.Click += new System.EventHandler(this.btn_remove_Click);
            // 
            // mnu_sep2
            // 
            this.mnu_sep2.Name = "mnu_sep2";
            this.mnu_sep2.Size = new System.Drawing.Size(127, 6);
            // 
            // mnu_up
            // 
            this.mnu_up.Image = global::DotNet.VisionMaster.Properties.Resources.FlowUp;
            this.mnu_up.Name = "mnu_up";
            this.mnu_up.Size = new System.Drawing.Size(130, 22);
            this.mnu_up.Text = "上移";
            this.mnu_up.Click += new System.EventHandler(this.mnu_up_Click);
            // 
            // mnu_down
            // 
            this.mnu_down.Image = global::DotNet.VisionMaster.Properties.Resources.FlowDown;
            this.mnu_down.Name = "mnu_down";
            this.mnu_down.Size = new System.Drawing.Size(130, 22);
            this.mnu_down.Text = "下移";
            this.mnu_down.Click += new System.EventHandler(this.mnu_down_Click);
            // 
            // JobForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(278, 712);
            this.Controls.Add(this.tbc_jobs);
            this.Controls.Add(this.pnl_bottom);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "JobForm";
            this.Text = "流程编辑器";
            this.Load += new System.EventHandler(this.Form_Job_Load);
            this.tbc_jobs.ResumeLayout(false);
            this.tab_flow.ResumeLayout(false);
            this.lst_tools.ResumeLayout(false);
            this.lst_tools.PerformLayout();
            this.pnl_bottom.ResumeLayout(false);
            this.pnl_toolOps.ResumeLayout(false);
            this.pnl_run.ResumeLayout(false);
            this.cms_tool.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tbc_jobs;
        private System.Windows.Forms.TabPage tab_flow;
        private System.Windows.Forms.ListBox lst_tools;
        private System.Windows.Forms.TextBox txt_name;
        private System.Windows.Forms.Panel pnl_bottom;
        private System.Windows.Forms.FlowLayoutPanel pnl_toolOps;
        private System.Windows.Forms.Button btn_add;
        private System.Windows.Forms.Button btn_remove;
        private System.Windows.Forms.Button btn_rename;
        private System.Windows.Forms.Panel pnl_sep1;
        private System.Windows.Forms.Panel pnl_sep2;
        private System.Windows.Forms.Panel pnl_sep3;
        private System.Windows.Forms.Panel pnl_run;
        internal System.Windows.Forms.Button btn_runLoop;
        internal System.Windows.Forms.Button btn_runOnce;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.ContextMenuStrip cms_tool;
        private System.Windows.Forms.ToolStripMenuItem mnu_editPara;
        private System.Windows.Forms.ToolStripMenuItem mnu_rename;
        private System.Windows.Forms.ToolStripMenuItem mnu_runCurrent;
        private System.Windows.Forms.ToolStripSeparator mnu_sep1;
        private System.Windows.Forms.ToolStripMenuItem mnu_delete;
        private System.Windows.Forms.ToolStripSeparator mnu_sep2;
        private System.Windows.Forms.ToolStripMenuItem mnu_up;
        private System.Windows.Forms.ToolStripMenuItem mnu_down;
    }
}
