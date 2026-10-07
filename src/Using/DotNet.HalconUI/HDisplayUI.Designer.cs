namespace DotNet.HalconUI
{
    partial class HDisplayUI
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
            if (disposing)
            {
                ReleaseDisplayResources();

                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel_tool = new System.Windows.Forms.TableLayoutPanel();
            this.but_IsCross = new System.Windows.Forms.Button();
            this.btn_ReSetPart = new System.Windows.Forms.Button();
            this.lbl_result = new System.Windows.Forms.Label();
            this.tableLayoutPanel_floor = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.hWindowControl = new HalconDotNet.HWindowControl();
            this.tableLayoutPanel_tool.SuspendLayout();
            this.tableLayoutPanel_floor.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel_tool
            // 
            this.tableLayoutPanel_tool.BackColor = System.Drawing.Color.Gainsboro;
            this.tableLayoutPanel_tool.ColumnCount = 3;
            this.tableLayoutPanel_tool.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel_tool.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel_tool.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_tool.Controls.Add(this.but_IsCross, 0, 0);
            this.tableLayoutPanel_tool.Controls.Add(this.btn_ReSetPart, 0, 0);
            this.tableLayoutPanel_tool.Controls.Add(this.lbl_result, 2, 0);
            this.tableLayoutPanel_tool.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel_tool.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel_tool.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel_tool.Name = "tableLayoutPanel_tool";
            this.tableLayoutPanel_tool.RowCount = 1;
            this.tableLayoutPanel_tool.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_tool.Size = new System.Drawing.Size(512, 20);
            this.tableLayoutPanel_tool.TabIndex = 0;
            // 
            // but_IsCross
            // 
            this.but_IsCross.Dock = System.Windows.Forms.DockStyle.Fill;
            this.but_IsCross.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.but_IsCross.Font = new System.Drawing.Font("宋体", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.but_IsCross.Location = new System.Drawing.Point(21, 1);
            this.but_IsCross.Margin = new System.Windows.Forms.Padding(1);
            this.but_IsCross.Name = "but_IsCross";
            this.but_IsCross.Size = new System.Drawing.Size(18, 18);
            this.but_IsCross.TabIndex = 66;
            this.but_IsCross.Text = "十";
            this.but_IsCross.UseVisualStyleBackColor = true;
            this.but_IsCross.Click += new System.EventHandler(this.but_IsCross_Click);
            // 
            // btn_ReSetPart
            // 
            this.btn_ReSetPart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btn_ReSetPart.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_ReSetPart.Font = new System.Drawing.Font("宋体", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_ReSetPart.Location = new System.Drawing.Point(1, 1);
            this.btn_ReSetPart.Margin = new System.Windows.Forms.Padding(1);
            this.btn_ReSetPart.Name = "btn_ReSetPart";
            this.btn_ReSetPart.Size = new System.Drawing.Size(18, 18);
            this.btn_ReSetPart.TabIndex = 65;
            this.btn_ReSetPart.Text = "适";
            this.btn_ReSetPart.UseVisualStyleBackColor = true;
            this.btn_ReSetPart.Click += new System.EventHandler(this.btn_ReSetPart_Click);
            // 
            // lbl_result
            // 
            this.lbl_result.AutoSize = true;
            this.lbl_result.Font = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lbl_result.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.lbl_result.Location = new System.Drawing.Point(60, 3);
            this.lbl_result.Margin = new System.Windows.Forms.Padding(20, 3, 3, 3);
            this.lbl_result.Name = "lbl_result";
            this.lbl_result.Size = new System.Drawing.Size(101, 12);
            this.lbl_result.TabIndex = 64;
            this.lbl_result.Text = "行:- 列:- 灰度:-";
            this.lbl_result.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanel_floor
            // 
            this.tableLayoutPanel_floor.ColumnCount = 1;
            this.tableLayoutPanel_floor.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_floor.Controls.Add(this.tableLayoutPanel_tool, 0, 0);
            this.tableLayoutPanel_floor.Controls.Add(this.panel1, 0, 1);
            this.tableLayoutPanel_floor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel_floor.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel_floor.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel_floor.Name = "tableLayoutPanel_floor";
            this.tableLayoutPanel_floor.RowCount = 2;
            this.tableLayoutPanel_floor.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel_floor.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_floor.Size = new System.Drawing.Size(512, 424);
            this.tableLayoutPanel_floor.TabIndex = 70;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.SlateGray;
            this.panel1.Controls.Add(this.hWindowControl);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 20);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(512, 404);
            this.panel1.TabIndex = 1;
            // 
            // hWindowControl
            // 
            this.hWindowControl.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.hWindowControl.BackColor = System.Drawing.Color.Black;
            this.hWindowControl.BorderColor = System.Drawing.Color.Black;
            this.hWindowControl.ImagePart = new System.Drawing.Rectangle(0, 0, 640, 480);
            this.hWindowControl.Location = new System.Drawing.Point(0, 0);
            this.hWindowControl.Margin = new System.Windows.Forms.Padding(0);
            this.hWindowControl.Name = "hWindowControl";
            this.hWindowControl.Size = new System.Drawing.Size(512, 404);
            this.hWindowControl.TabIndex = 1;
            this.hWindowControl.WindowSize = new System.Drawing.Size(512, 404);
            // 
            // DisplayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel_floor);
            this.Name = "DisplayForm";
            this.Size = new System.Drawing.Size(512, 424);
            this.tableLayoutPanel_tool.ResumeLayout(false);
            this.tableLayoutPanel_tool.PerformLayout();
            this.tableLayoutPanel_floor.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel_tool;
        public System.Windows.Forms.Label lbl_result;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel_floor;
        private System.Windows.Forms.Button btn_ReSetPart;
        public HalconDotNet.HWindowControl hWindowControl;
        private System.Windows.Forms.Button but_IsCross;
        public System.Windows.Forms.Panel panel1;
    }
}