namespace DotNet.HalconUI
{
    partial class HEditModelUI
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

            // 放在 base.Dispose 之后：子控件 HDisplayUI 释放时会取消挂起的绘制会话，
            // 其续体 (DrawROIAsync) 还要读这些字段，先释放它们就成了在已释放句柄上做运算。
            if (disposing)
            {
                ReleaseModelResources();
            }
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.display = new DotNet.HalconUI.HDisplayUI();
            this.panel2 = new System.Windows.Forms.Panel();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.CB_LockCenter = new System.Windows.Forms.CheckBox();
            this.but_ModyfyCenter = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.but_ApplyRegion = new System.Windows.Forms.Button();
            this.CB_ApplyColor = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.CB_ApplyLineWidth = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btn_deleteRegion = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.but_addRegion = new System.Windows.Forms.Button();
            this.CB_ModifyShape = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 220F));
            this.tableLayoutPanel1.Controls.Add(this.display, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.panel2, 1, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(623, 486);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // display
            // 
            this.display.Dock = System.Windows.Forms.DockStyle.Fill;
            this.display.Location = new System.Drawing.Point(3, 3);
            this.display.Name = "display";
            this.display.Size = new System.Drawing.Size(397, 480);
            this.display.TabIndex = 2;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.groupBox3);
            this.panel2.Controls.Add(this.groupBox2);
            this.panel2.Controls.Add(this.groupBox1);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(403, 0);
            this.panel2.Margin = new System.Windows.Forms.Padding(0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(220, 486);
            this.panel2.TabIndex = 1;
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.CB_LockCenter);
            this.groupBox3.Controls.Add(this.but_ModyfyCenter);
            this.groupBox3.Location = new System.Drawing.Point(11, 344);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(200, 108);
            this.groupBox3.TabIndex = 354;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "修改模版中心";
            // 
            // CB_LockCenter
            // 
            this.CB_LockCenter.AutoSize = true;
            this.CB_LockCenter.Location = new System.Drawing.Point(20, 22);
            this.CB_LockCenter.Name = "CB_LockCenter";
            this.CB_LockCenter.Size = new System.Drawing.Size(72, 16);
            this.CB_LockCenter.TabIndex = 353;
            this.CB_LockCenter.Text = "锁定中心";
            this.CB_LockCenter.UseVisualStyleBackColor = true;
            // 
            // but_ModyfyCenter
            // 
            this.but_ModyfyCenter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.but_ModyfyCenter.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.but_ModyfyCenter.Font = new System.Drawing.Font("宋体", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.but_ModyfyCenter.ForeColor = System.Drawing.Color.White;
            this.but_ModyfyCenter.Location = new System.Drawing.Point(13, 50);
            this.but_ModyfyCenter.Margin = new System.Windows.Forms.Padding(2);
            this.but_ModyfyCenter.Name = "but_ModyfyCenter";
            this.but_ModyfyCenter.Size = new System.Drawing.Size(90, 35);
            this.but_ModyfyCenter.TabIndex = 350;
            this.but_ModyfyCenter.Text = "修改中心";
            this.but_ModyfyCenter.UseVisualStyleBackColor = false;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.but_ApplyRegion);
            this.groupBox2.Controls.Add(this.CB_ApplyColor);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.label1);
            this.groupBox2.Controls.Add(this.CB_ApplyLineWidth);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(11, 180);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(200, 155);
            this.groupBox2.TabIndex = 352;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "涂抹模版区域";
            // 
            // but_ApplyRegion
            // 
            this.but_ApplyRegion.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.but_ApplyRegion.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.but_ApplyRegion.Font = new System.Drawing.Font("宋体", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.but_ApplyRegion.ForeColor = System.Drawing.Color.White;
            this.but_ApplyRegion.Location = new System.Drawing.Point(13, 102);
            this.but_ApplyRegion.Margin = new System.Windows.Forms.Padding(2);
            this.but_ApplyRegion.Name = "but_ApplyRegion";
            this.but_ApplyRegion.Size = new System.Drawing.Size(90, 35);
            this.but_ApplyRegion.TabIndex = 351;
            this.but_ApplyRegion.Text = "擦除区域";
            this.but_ApplyRegion.UseVisualStyleBackColor = false;
            this.but_ApplyRegion.Click += new System.EventHandler(this.but_ApplyRegion_Click);
            // 
            // CB_ApplyColor
            // 
            this.CB_ApplyColor.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CB_ApplyColor.FormattingEnabled = true;
            this.CB_ApplyColor.Items.AddRange(new object[] {
            "white",
            "green",
            "red",
            "blue",
            "orange",
            "pink",
            "yellow",
            "black",
            "gray",
            "coral",
            "cyan",
            "magenta",
            "dim gray",
            "light gray",
            "cadet blue",
            "medium slate blue",
            "slate blue",
            "spring green",
            "dark olive green",
            "orange red",
            "forest green"});
            this.CB_ApplyColor.Location = new System.Drawing.Point(72, 62);
            this.CB_ApplyColor.Name = "CB_ApplyColor";
            this.CB_ApplyColor.Size = new System.Drawing.Size(106, 23);
            this.CB_ApplyColor.TabIndex = 355;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(20, 66);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(43, 15);
            this.label2.TabIndex = 354;
            this.label2.Text = "颜色：";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(20, 30);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(43, 15);
            this.label1.TabIndex = 353;
            this.label1.Text = "线宽：";
            // 
            // CB_ApplyLineWidth
            // 
            this.CB_ApplyLineWidth.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CB_ApplyLineWidth.FormattingEnabled = true;
            this.CB_ApplyLineWidth.Location = new System.Drawing.Point(72, 25);
            this.CB_ApplyLineWidth.Name = "CB_ApplyLineWidth";
            this.CB_ApplyLineWidth.Size = new System.Drawing.Size(106, 23);
            this.CB_ApplyLineWidth.TabIndex = 352;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.btn_deleteRegion);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.but_addRegion);
            this.groupBox1.Controls.Add(this.CB_ModifyShape);
            this.groupBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.ForeColor = System.Drawing.Color.Black;
            this.groupBox1.Location = new System.Drawing.Point(11, 11);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(200, 160);
            this.groupBox1.TabIndex = 347;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "修改模版区域";
            // 
            // btn_deleteRegion
            // 
            this.btn_deleteRegion.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.btn_deleteRegion.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btn_deleteRegion.Font = new System.Drawing.Font("宋体", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btn_deleteRegion.ForeColor = System.Drawing.Color.White;
            this.btn_deleteRegion.Location = new System.Drawing.Point(13, 110);
            this.btn_deleteRegion.Margin = new System.Windows.Forms.Padding(2);
            this.btn_deleteRegion.Name = "btn_deleteRegion";
            this.btn_deleteRegion.Size = new System.Drawing.Size(90, 35);
            this.btn_deleteRegion.TabIndex = 349;
            this.btn_deleteRegion.Text = "删除区域";
            this.btn_deleteRegion.UseVisualStyleBackColor = false;
            this.btn_deleteRegion.Click += new System.EventHandler(this.btn_deleteRegion_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(20, 26);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(43, 15);
            this.label3.TabIndex = 357;
            this.label3.Text = "形状：";
            // 
            // but_addRegion
            // 
            this.but_addRegion.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.but_addRegion.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.but_addRegion.Font = new System.Drawing.Font("宋体", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.but_addRegion.ForeColor = System.Drawing.Color.White;
            this.but_addRegion.Location = new System.Drawing.Point(13, 62);
            this.but_addRegion.Margin = new System.Windows.Forms.Padding(2);
            this.but_addRegion.Name = "but_addRegion";
            this.but_addRegion.Size = new System.Drawing.Size(90, 35);
            this.but_addRegion.TabIndex = 348;
            this.but_addRegion.Text = "添加区域";
            this.but_addRegion.UseVisualStyleBackColor = false;
            this.but_addRegion.Click += new System.EventHandler(this.but_addRegion_Click);
            // 
            // CB_ModifyShape
            // 
            this.CB_ModifyShape.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CB_ModifyShape.FormattingEnabled = true;
            this.CB_ModifyShape.Location = new System.Drawing.Point(72, 24);
            this.CB_ModifyShape.Name = "CB_ModifyShape";
            this.CB_ModifyShape.Size = new System.Drawing.Size(106, 23);
            this.CB_ModifyShape.TabIndex = 356;
            // 
            // HEditForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(623, 486);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "HEditForm";
            this.Text = "HEditForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.HEditForm_FormClosing);
            this.Load += new System.EventHandler(this.HEditForm_Load);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox CB_LockCenter;
        private System.Windows.Forms.Button but_ModyfyCenter;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button but_ApplyRegion;
        private System.Windows.Forms.ComboBox CB_ApplyColor;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox CB_ApplyLineWidth;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btn_deleteRegion;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button but_addRegion;
        private System.Windows.Forms.ComboBox CB_ModifyShape;
        private HDisplayUI display;
    }
}