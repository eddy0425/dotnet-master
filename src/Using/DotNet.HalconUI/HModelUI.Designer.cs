namespace DotNet.HalconUI
{
    partial class HModelUI
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
            this.tableLayoutPanel_floor = new System.Windows.Forms.TableLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.hWindowControl = new HalconDotNet.HWindowControl();
            this.tableLayoutPanel_floor.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel_floor
            // 
            this.tableLayoutPanel_floor.ColumnCount = 1;
            this.tableLayoutPanel_floor.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_floor.Controls.Add(this.panel1, 0, 0);
            this.tableLayoutPanel_floor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel_floor.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel_floor.Margin = new System.Windows.Forms.Padding(0);
            this.tableLayoutPanel_floor.Name = "tableLayoutPanel_floor";
            this.tableLayoutPanel_floor.RowCount = 1;
            this.tableLayoutPanel_floor.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_floor.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel_floor.Size = new System.Drawing.Size(323, 255);
            this.tableLayoutPanel_floor.TabIndex = 70;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.SlateGray;
            this.panel1.Controls.Add(this.hWindowControl);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(323, 255);
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
            this.hWindowControl.Location = new System.Drawing.Point(11, 9);
            this.hWindowControl.Margin = new System.Windows.Forms.Padding(0);
            this.hWindowControl.Name = "hWindowControl";
            this.hWindowControl.Size = new System.Drawing.Size(300, 240);
            this.hWindowControl.TabIndex = 1;
            this.hWindowControl.WindowSize = new System.Drawing.Size(300, 240);
            // 
            // HModelForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.tableLayoutPanel_floor);
            this.Name = "HModelForm";
            this.Size = new System.Drawing.Size(323, 255);
            this.tableLayoutPanel_floor.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel_floor;
        public HalconDotNet.HWindowControl hWindowControl;
        public System.Windows.Forms.Panel panel1;
    }
}