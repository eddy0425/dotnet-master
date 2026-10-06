namespace DotNet.VisionMaster
{
    partial class MainForm
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.lst_tools = new System.Windows.Forms.ListBox();
            this.lbl_flow = new System.Windows.Forms.Label();
            this.pnl_toolOps = new System.Windows.Forms.FlowLayoutPanel();
            this.btn_add = new System.Windows.Forms.Button();
            this.btn_remove = new System.Windows.Forms.Button();
            this.btn_up = new System.Windows.Forms.Button();
            this.btn_down = new System.Windows.Forms.Button();
            this.txt_name = new System.Windows.Forms.TextBox();
            this.pnl_run = new System.Windows.Forms.FlowLayoutPanel();
            this.but_Run = new System.Windows.Forms.Button();
            this.but_RunFlow = new System.Windows.Forms.Button();
            this.btn_save = new System.Windows.Forms.Button();
            this.btn_open = new System.Windows.Forms.Button();
            this.lbl_status = new System.Windows.Forms.Label();
            this.menu_add = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.pnl_toolOps.SuspendLayout();
            this.pnl_run.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanel1
            //
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 220F));
            this.tableLayoutPanel1.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.panel3, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(999, 829);
            this.tableLayoutPanel1.TabIndex = 0;
            //
            // panel1
            //
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(773, 523);
            this.panel1.TabIndex = 0;
            //
            // panel2
            //
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(3, 532);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(773, 294);
            this.panel2.TabIndex = 1;
            //
            // panel3
            //
            this.panel3.Controls.Add(this.lst_tools);
            this.panel3.Controls.Add(this.lbl_flow);
            this.panel3.Controls.Add(this.pnl_toolOps);
            this.panel3.Controls.Add(this.txt_name);
            this.panel3.Controls.Add(this.pnl_run);
            this.panel3.Controls.Add(this.lbl_status);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(782, 3);
            this.panel3.Name = "panel3";
            this.tableLayoutPanel1.SetRowSpan(this.panel3, 2);
            this.panel3.Size = new System.Drawing.Size(214, 823);
            this.panel3.TabIndex = 2;
            //
            // lst_tools
            //
            this.lst_tools.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lst_tools.IntegralHeight = false;
            this.lst_tools.ItemHeight = 12;
            this.lst_tools.Name = "lst_tools";
            this.lst_tools.TabIndex = 1;
            this.lst_tools.SelectedIndexChanged += new System.EventHandler(this.lst_tools_SelectedIndexChanged);
            //
            // lbl_flow
            //
            this.lbl_flow.Dock = System.Windows.Forms.DockStyle.Top;
            this.lbl_flow.Name = "lbl_flow";
            this.lbl_flow.Size = new System.Drawing.Size(214, 22);
            this.lbl_flow.TabIndex = 0;
            this.lbl_flow.Text = "流程";
            this.lbl_flow.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // pnl_toolOps
            //
            this.pnl_toolOps.AutoSize = true;
            this.pnl_toolOps.Controls.Add(this.btn_add);
            this.pnl_toolOps.Controls.Add(this.btn_remove);
            this.pnl_toolOps.Controls.Add(this.btn_up);
            this.pnl_toolOps.Controls.Add(this.btn_down);
            this.pnl_toolOps.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnl_toolOps.Name = "pnl_toolOps";
            this.pnl_toolOps.TabIndex = 2;
            //
            // btn_add
            //
            this.btn_add.Name = "btn_add";
            this.btn_add.Size = new System.Drawing.Size(46, 23);
            this.btn_add.TabIndex = 0;
            this.btn_add.Text = "添加";
            this.btn_add.UseVisualStyleBackColor = true;
            this.btn_add.Click += new System.EventHandler(this.btn_add_Click);
            //
            // btn_remove
            //
            this.btn_remove.Name = "btn_remove";
            this.btn_remove.Size = new System.Drawing.Size(46, 23);
            this.btn_remove.TabIndex = 1;
            this.btn_remove.Text = "删除";
            this.btn_remove.UseVisualStyleBackColor = true;
            this.btn_remove.Click += new System.EventHandler(this.btn_remove_Click);
            //
            // btn_up
            //
            this.btn_up.Name = "btn_up";
            this.btn_up.Size = new System.Drawing.Size(46, 23);
            this.btn_up.TabIndex = 2;
            this.btn_up.Text = "上移";
            this.btn_up.UseVisualStyleBackColor = true;
            this.btn_up.Click += new System.EventHandler(this.btn_up_Click);
            //
            // btn_down
            //
            this.btn_down.Name = "btn_down";
            this.btn_down.Size = new System.Drawing.Size(46, 23);
            this.btn_down.TabIndex = 3;
            this.btn_down.Text = "下移";
            this.btn_down.UseVisualStyleBackColor = true;
            this.btn_down.Click += new System.EventHandler(this.btn_down_Click);
            //
            // txt_name
            //
            this.txt_name.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txt_name.Name = "txt_name";
            this.txt_name.TabIndex = 3;
            this.txt_name.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txt_name_KeyDown);
            this.txt_name.Leave += new System.EventHandler(this.txt_name_Leave);
            //
            // pnl_run
            //
            this.pnl_run.AutoSize = true;
            this.pnl_run.Controls.Add(this.but_Run);
            this.pnl_run.Controls.Add(this.but_RunFlow);
            this.pnl_run.Controls.Add(this.btn_save);
            this.pnl_run.Controls.Add(this.btn_open);
            this.pnl_run.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnl_run.Name = "pnl_run";
            this.pnl_run.TabIndex = 4;
            //
            // but_Run
            //
            this.but_Run.Name = "but_Run";
            this.but_Run.Size = new System.Drawing.Size(96, 23);
            this.but_Run.TabIndex = 0;
            this.but_Run.Text = "当前运行";
            this.but_Run.UseVisualStyleBackColor = true;
            this.but_Run.Click += new System.EventHandler(this.but_Run_Click);
            //
            // but_RunFlow
            //
            this.but_RunFlow.Name = "but_RunFlow";
            this.but_RunFlow.Size = new System.Drawing.Size(96, 23);
            this.but_RunFlow.TabIndex = 1;
            this.but_RunFlow.Text = "流程运行";
            this.but_RunFlow.UseVisualStyleBackColor = true;
            this.but_RunFlow.Click += new System.EventHandler(this.but_RunFlow_Click);
            //
            // btn_save
            //
            this.btn_save.Name = "btn_save";
            this.btn_save.Size = new System.Drawing.Size(96, 23);
            this.btn_save.TabIndex = 2;
            this.btn_save.Text = "保存方案";
            this.btn_save.UseVisualStyleBackColor = true;
            this.btn_save.Click += new System.EventHandler(this.btn_save_Click);
            //
            // btn_open
            //
            this.btn_open.Name = "btn_open";
            this.btn_open.Size = new System.Drawing.Size(96, 23);
            this.btn_open.TabIndex = 3;
            this.btn_open.Text = "打开方案";
            this.btn_open.UseVisualStyleBackColor = true;
            this.btn_open.Click += new System.EventHandler(this.btn_open_Click);
            //
            // lbl_status
            //
            this.lbl_status.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.lbl_status.Name = "lbl_status";
            this.lbl_status.Size = new System.Drawing.Size(214, 64);
            this.lbl_status.TabIndex = 5;
            //
            // menu_add
            //
            this.menu_add.Name = "menu_add";
            //
            // MainForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(999, 829);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "MainForm";
            this.Text = "VisionMaster";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            this.pnl_toolOps.ResumeLayout(false);
            this.pnl_run.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.ListBox lst_tools;
        private System.Windows.Forms.Label lbl_flow;
        private System.Windows.Forms.FlowLayoutPanel pnl_toolOps;
        private System.Windows.Forms.Button btn_add;
        private System.Windows.Forms.Button btn_remove;
        private System.Windows.Forms.Button btn_up;
        private System.Windows.Forms.Button btn_down;
        private System.Windows.Forms.TextBox txt_name;
        private System.Windows.Forms.FlowLayoutPanel pnl_run;
        private System.Windows.Forms.Button but_Run;
        private System.Windows.Forms.Button but_RunFlow;
        private System.Windows.Forms.Button btn_save;
        private System.Windows.Forms.Button btn_open;
        private System.Windows.Forms.Label lbl_status;
        private System.Windows.Forms.ContextMenuStrip menu_add;
    }
}
