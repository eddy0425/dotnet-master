namespace DotNet.VisionMaster
{
    partial class InfoForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(InfoForm));
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.mnu_clear = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_desc = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_asc = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_pause = new System.Windows.Forms.ToolStripMenuItem();
            this.mnu_history = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.tsb_tip = new System.Windows.Forms.ToolStripButton();
            this.tsb_warn = new System.Windows.Forms.ToolStripButton();
            this.tsb_error = new System.Windows.Forms.ToolStripButton();
            this.tsb_alarm = new System.Windows.Forms.ToolStripButton();
            this.lst_log = new System.Windows.Forms.ListView();
            this.col_time = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.col_message = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.contextMenuStrip1.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // contextMenuStrip1
            //
            this.contextMenuStrip1.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.mnu_clear,
            this.mnu_desc,
            this.mnu_asc,
            this.mnu_pause,
            this.mnu_history});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(125, 114);
            //
            // mnu_clear
            //
            this.mnu_clear.Name = "mnu_clear";
            this.mnu_clear.Size = new System.Drawing.Size(124, 22);
            this.mnu_clear.Text = "清除";
            this.mnu_clear.Click += new System.EventHandler(this.mnu_clear_Click);
            //
            // mnu_desc
            //
            this.mnu_desc.Checked = true;
            this.mnu_desc.CheckState = System.Windows.Forms.CheckState.Checked;
            this.mnu_desc.Name = "mnu_desc";
            this.mnu_desc.Size = new System.Drawing.Size(124, 22);
            this.mnu_desc.Text = "降序";
            this.mnu_desc.Click += new System.EventHandler(this.mnu_desc_Click);
            //
            // mnu_asc
            //
            this.mnu_asc.Name = "mnu_asc";
            this.mnu_asc.Size = new System.Drawing.Size(124, 22);
            this.mnu_asc.Text = "升序";
            this.mnu_asc.Click += new System.EventHandler(this.mnu_asc_Click);
            //
            // mnu_pause
            //
            this.mnu_pause.CheckOnClick = true;
            this.mnu_pause.Name = "mnu_pause";
            this.mnu_pause.Size = new System.Drawing.Size(124, 22);
            this.mnu_pause.Text = "停止刷新";
            this.mnu_pause.Click += new System.EventHandler(this.mnu_pause_Click);
            //
            // mnu_history
            //
            this.mnu_history.Name = "mnu_history";
            this.mnu_history.Size = new System.Drawing.Size(124, 22);
            this.mnu_history.Text = "历史日志";
            this.mnu_history.Click += new System.EventHandler(this.mnu_history_Click);
            //
            // toolStrip1
            //
            this.toolStrip1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.toolStrip1.Font = new System.Drawing.Font("KaiTi", 10.8F);
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsb_tip,
            this.tsb_warn,
            this.tsb_error,
            this.tsb_alarm});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip1.Size = new System.Drawing.Size(634, 27);
            this.toolStrip1.TabIndex = 0;
            //
            // tsb_tip
            //
            this.tsb_tip.CheckOnClick = true;
            this.tsb_tip.ForeColor = System.Drawing.SystemColors.Control;
            this.tsb_tip.Image = ((System.Drawing.Image)(resources.GetObject("tsb_tip.Image")));
            this.tsb_tip.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsb_tip.Name = "tsb_tip";
            this.tsb_tip.Size = new System.Drawing.Size(87, 24);
            this.tsb_tip.Text = "提示(0)";
            this.tsb_tip.ToolTipText = "只看提示";
            this.tsb_tip.Click += new System.EventHandler(this.tsb_filter_Click);
            //
            // tsb_warn
            //
            this.tsb_warn.CheckOnClick = true;
            this.tsb_warn.ForeColor = System.Drawing.SystemColors.Control;
            this.tsb_warn.Image = ((System.Drawing.Image)(resources.GetObject("tsb_warn.Image")));
            this.tsb_warn.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsb_warn.Name = "tsb_warn";
            this.tsb_warn.Size = new System.Drawing.Size(87, 24);
            this.tsb_warn.Text = "警告(0)";
            this.tsb_warn.ToolTipText = "只看警告";
            this.tsb_warn.Click += new System.EventHandler(this.tsb_filter_Click);
            //
            // tsb_error
            //
            this.tsb_error.CheckOnClick = true;
            this.tsb_error.ForeColor = System.Drawing.SystemColors.Control;
            this.tsb_error.Image = ((System.Drawing.Image)(resources.GetObject("tsb_error.Image")));
            this.tsb_error.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsb_error.Name = "tsb_error";
            this.tsb_error.Size = new System.Drawing.Size(87, 24);
            this.tsb_error.Text = "错误(0)";
            this.tsb_error.ToolTipText = "只看错误";
            this.tsb_error.Click += new System.EventHandler(this.tsb_filter_Click);
            //
            // tsb_alarm
            //
            this.tsb_alarm.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.tsb_alarm.CheckOnClick = true;
            this.tsb_alarm.ForeColor = System.Drawing.SystemColors.Control;
            this.tsb_alarm.Image = ((System.Drawing.Image)(resources.GetObject("tsb_alarm.Image")));
            this.tsb_alarm.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.tsb_alarm.Name = "tsb_alarm";
            this.tsb_alarm.Size = new System.Drawing.Size(87, 24);
            this.tsb_alarm.Text = "报警(0)";
            this.tsb_alarm.ToolTipText = "报警历史";
            this.tsb_alarm.Click += new System.EventHandler(this.tsb_filter_Click);
            //
            // lst_log
            //
            this.lst_log.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.lst_log.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lst_log.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.col_time,
            this.col_message});
            this.lst_log.ContextMenuStrip = this.contextMenuStrip1;
            this.lst_log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lst_log.Font = new System.Drawing.Font("KaiTi", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lst_log.ForeColor = System.Drawing.Color.White;
            this.lst_log.FullRowSelect = true;
            this.lst_log.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lst_log.HideSelection = false;
            this.lst_log.LabelWrap = false;
            this.lst_log.Location = new System.Drawing.Point(0, 27);
            this.lst_log.Name = "lst_log";
            this.lst_log.Size = new System.Drawing.Size(634, 243);
            this.lst_log.TabIndex = 1;
            this.lst_log.UseCompatibleStateImageBehavior = false;
            this.lst_log.View = System.Windows.Forms.View.Details;
            this.lst_log.Resize += new System.EventHandler(this.lst_log_Resize);
            //
            // col_time
            //
            this.col_time.Text = "时间";
            this.col_time.Width = 80;
            //
            // col_message
            //
            this.col_message.Text = "信息";
            this.col_message.Width = 540;
            //
            // InfoForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lst_log);
            this.Controls.Add(this.toolStrip1);
            this.Name = "InfoForm";
            this.Size = new System.Drawing.Size(634, 270);
            this.contextMenuStrip1.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem mnu_clear;
        private System.Windows.Forms.ToolStripMenuItem mnu_desc;
        private System.Windows.Forms.ToolStripMenuItem mnu_asc;
        private System.Windows.Forms.ToolStripMenuItem mnu_pause;
        private System.Windows.Forms.ToolStripMenuItem mnu_history;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton tsb_tip;
        private System.Windows.Forms.ToolStripButton tsb_warn;
        private System.Windows.Forms.ToolStripButton tsb_error;
        private System.Windows.Forms.ToolStripButton tsb_alarm;
        private System.Windows.Forms.ListView lst_log;
        private System.Windows.Forms.ColumnHeader col_time;
        private System.Windows.Forms.ColumnHeader col_message;
    }
}
