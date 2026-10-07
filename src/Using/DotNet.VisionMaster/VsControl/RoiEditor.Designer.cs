namespace DotNet.VisionMaster
{
    partial class RoiEditor
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
            this.grb_RectInfo = new System.Windows.Forms.GroupBox();
            this.lbl_Fix0 = new System.Windows.Forms.Label();
            this.txt_Width = new System.Windows.Forms.TextBox();
            this.lbl_Fix1 = new System.Windows.Forms.Label();
            this.txt_Height = new System.Windows.Forms.TextBox();
            this.lbl_Fix2 = new System.Windows.Forms.Label();
            this.txt_TopLeft = new System.Windows.Forms.TextBox();
            this.lbl_Fix3 = new System.Windows.Forms.Label();
            this.txt_BottomRight = new System.Windows.Forms.TextBox();
            this.lbl_Fix4 = new System.Windows.Forms.Label();
            this.txt_Center = new System.Windows.Forms.TextBox();
            this.but_editRegion = new System.Windows.Forms.Button();
            this.btn_drawRegion = new System.Windows.Forms.Button();
            this.grb_Rect = new System.Windows.Forms.GroupBox();
            this.btn_rectRectangle = new System.Windows.Forms.RadioButton();
            this.btn_rectAffRect = new System.Windows.Forms.RadioButton();
            this.btn_rectCircle = new System.Windows.Forms.RadioButton();
            this.btn_rectEllipse = new System.Windows.Forms.RadioButton();
            this.btn_rectPolygon = new System.Windows.Forms.RadioButton();
            this.grb_RectInfo.SuspendLayout();
            this.grb_Rect.SuspendLayout();
            this.SuspendLayout();
            // 
            // grb_RectInfo
            // 
            this.grb_RectInfo.Controls.Add(this.lbl_Fix0);
            this.grb_RectInfo.Controls.Add(this.txt_Width);
            this.grb_RectInfo.Controls.Add(this.lbl_Fix1);
            this.grb_RectInfo.Controls.Add(this.txt_Height);
            this.grb_RectInfo.Controls.Add(this.lbl_Fix2);
            this.grb_RectInfo.Controls.Add(this.txt_TopLeft);
            this.grb_RectInfo.Controls.Add(this.lbl_Fix3);
            this.grb_RectInfo.Controls.Add(this.txt_BottomRight);
            this.grb_RectInfo.Controls.Add(this.lbl_Fix4);
            this.grb_RectInfo.Controls.Add(this.txt_Center);
            this.grb_RectInfo.ForeColor = System.Drawing.Color.White;
            this.grb_RectInfo.Location = new System.Drawing.Point(268, 0);
            this.grb_RectInfo.Name = "grb_RectInfo";
            this.grb_RectInfo.Size = new System.Drawing.Size(200, 160);
            this.grb_RectInfo.TabIndex = 0;
            this.grb_RectInfo.TabStop = false;
            this.grb_RectInfo.Text = "区域信息";
            // 
            // lbl_Fix0
            // 
            this.lbl_Fix0.AutoSize = true;
            this.lbl_Fix0.Location = new System.Drawing.Point(17, 24);
            this.lbl_Fix0.Name = "lbl_Fix0";
            this.lbl_Fix0.Size = new System.Drawing.Size(43, 15);
            this.lbl_Fix0.TabIndex = 0;
            this.lbl_Fix0.Text = "区域宽";
            // 
            // txt_Width
            // 
            this.txt_Width.BackColor = System.Drawing.Color.White;
            this.txt_Width.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txt_Width.Location = new System.Drawing.Point(80, 20);
            this.txt_Width.Name = "txt_Width";
            this.txt_Width.ReadOnly = true;
            this.txt_Width.Size = new System.Drawing.Size(98, 21);
            this.txt_Width.TabIndex = 1;
            // 
            // lbl_Fix1
            // 
            this.lbl_Fix1.AutoSize = true;
            this.lbl_Fix1.Location = new System.Drawing.Point(17, 51);
            this.lbl_Fix1.Name = "lbl_Fix1";
            this.lbl_Fix1.Size = new System.Drawing.Size(43, 15);
            this.lbl_Fix1.TabIndex = 2;
            this.lbl_Fix1.Text = "区域高";
            // 
            // txt_Height
            // 
            this.txt_Height.BackColor = System.Drawing.Color.White;
            this.txt_Height.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txt_Height.Location = new System.Drawing.Point(80, 47);
            this.txt_Height.Name = "txt_Height";
            this.txt_Height.ReadOnly = true;
            this.txt_Height.Size = new System.Drawing.Size(98, 21);
            this.txt_Height.TabIndex = 3;
            // 
            // lbl_Fix2
            // 
            this.lbl_Fix2.AutoSize = true;
            this.lbl_Fix2.Location = new System.Drawing.Point(17, 78);
            this.lbl_Fix2.Name = "lbl_Fix2";
            this.lbl_Fix2.Size = new System.Drawing.Size(43, 15);
            this.lbl_Fix2.TabIndex = 4;
            this.lbl_Fix2.Text = "左上角";
            // 
            // txt_TopLeft
            // 
            this.txt_TopLeft.BackColor = System.Drawing.Color.White;
            this.txt_TopLeft.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txt_TopLeft.Location = new System.Drawing.Point(80, 74);
            this.txt_TopLeft.Name = "txt_TopLeft";
            this.txt_TopLeft.ReadOnly = true;
            this.txt_TopLeft.Size = new System.Drawing.Size(98, 21);
            this.txt_TopLeft.TabIndex = 5;
            // 
            // lbl_Fix3
            // 
            this.lbl_Fix3.AutoSize = true;
            this.lbl_Fix3.Location = new System.Drawing.Point(17, 105);
            this.lbl_Fix3.Name = "lbl_Fix3";
            this.lbl_Fix3.Size = new System.Drawing.Size(43, 15);
            this.lbl_Fix3.TabIndex = 6;
            this.lbl_Fix3.Text = "右下角";
            // 
            // txt_BottomRight
            // 
            this.txt_BottomRight.BackColor = System.Drawing.Color.White;
            this.txt_BottomRight.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txt_BottomRight.Location = new System.Drawing.Point(80, 101);
            this.txt_BottomRight.Name = "txt_BottomRight";
            this.txt_BottomRight.ReadOnly = true;
            this.txt_BottomRight.Size = new System.Drawing.Size(98, 21);
            this.txt_BottomRight.TabIndex = 7;
            // 
            // lbl_Fix4
            // 
            this.lbl_Fix4.AutoSize = true;
            this.lbl_Fix4.Location = new System.Drawing.Point(17, 132);
            this.lbl_Fix4.Name = "lbl_Fix4";
            this.lbl_Fix4.Size = new System.Drawing.Size(43, 15);
            this.lbl_Fix4.TabIndex = 8;
            this.lbl_Fix4.Text = "中心点";
            // 
            // txt_Center
            // 
            this.txt_Center.BackColor = System.Drawing.Color.White;
            this.txt_Center.ForeColor = System.Drawing.SystemColors.WindowText;
            this.txt_Center.Location = new System.Drawing.Point(80, 128);
            this.txt_Center.Name = "txt_Center";
            this.txt_Center.ReadOnly = true;
            this.txt_Center.Size = new System.Drawing.Size(98, 21);
            this.txt_Center.TabIndex = 9;
            // 
            // but_editRegion
            // 
            this.but_editRegion.BackColor = System.Drawing.Color.Gainsboro;
            this.but_editRegion.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.but_editRegion.ForeColor = System.Drawing.Color.Black;
            this.but_editRegion.Location = new System.Drawing.Point(163, 50);
            this.but_editRegion.Name = "but_editRegion";
            this.but_editRegion.Size = new System.Drawing.Size(75, 36);
            this.but_editRegion.TabIndex = 1;
            this.but_editRegion.Text = "修改区域";
            this.but_editRegion.UseVisualStyleBackColor = false;
            this.but_editRegion.Click += new System.EventHandler(this.but_editRegion_Click);
            // 
            // btn_drawRegion
            // 
            this.btn_drawRegion.BackColor = System.Drawing.Color.Gainsboro;
            this.btn_drawRegion.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_drawRegion.ForeColor = System.Drawing.Color.Black;
            this.btn_drawRegion.Location = new System.Drawing.Point(163, 106);
            this.btn_drawRegion.Name = "btn_drawRegion";
            this.btn_drawRegion.Size = new System.Drawing.Size(75, 36);
            this.btn_drawRegion.TabIndex = 2;
            this.btn_drawRegion.Text = "新建区域";
            this.btn_drawRegion.UseVisualStyleBackColor = false;
            this.btn_drawRegion.Click += new System.EventHandler(this.btn_drawRegion_Click);
            // 
            // grb_Rect
            // 
            this.grb_Rect.Controls.Add(this.btn_rectRectangle);
            this.grb_Rect.Controls.Add(this.btn_rectAffRect);
            this.grb_Rect.Controls.Add(this.btn_rectCircle);
            this.grb_Rect.Controls.Add(this.btn_rectEllipse);
            this.grb_Rect.Controls.Add(this.btn_rectPolygon);
            this.grb_Rect.ForeColor = System.Drawing.Color.White;
            this.grb_Rect.Location = new System.Drawing.Point(9, 0);
            this.grb_Rect.Name = "grb_Rect";
            this.grb_Rect.Size = new System.Drawing.Size(124, 160);
            this.grb_Rect.TabIndex = 3;
            this.grb_Rect.TabStop = false;
            this.grb_Rect.Text = "区域形状";
            // 
            // btn_rectRectangle
            // 
            this.btn_rectRectangle.AutoSize = true;
            this.btn_rectRectangle.Checked = true;
            this.btn_rectRectangle.Location = new System.Drawing.Point(18, 22);
            this.btn_rectRectangle.Name = "btn_rectRectangle";
            this.btn_rectRectangle.Size = new System.Drawing.Size(49, 19);
            this.btn_rectRectangle.TabIndex = 0;
            this.btn_rectRectangle.TabStop = true;
            this.btn_rectRectangle.Text = "矩形";
            // 
            // btn_rectAffRect
            // 
            this.btn_rectAffRect.AutoSize = true;
            this.btn_rectAffRect.Location = new System.Drawing.Point(18, 49);
            this.btn_rectAffRect.Name = "btn_rectAffRect";
            this.btn_rectAffRect.Size = new System.Drawing.Size(49, 19);
            this.btn_rectAffRect.TabIndex = 1;
            this.btn_rectAffRect.Text = "仿矩";
            // 
            // btn_rectCircle
            // 
            this.btn_rectCircle.AutoSize = true;
            this.btn_rectCircle.Location = new System.Drawing.Point(18, 76);
            this.btn_rectCircle.Name = "btn_rectCircle";
            this.btn_rectCircle.Size = new System.Drawing.Size(37, 19);
            this.btn_rectCircle.TabIndex = 2;
            this.btn_rectCircle.Text = "圆";
            // 
            // btn_rectEllipse
            // 
            this.btn_rectEllipse.AutoSize = true;
            this.btn_rectEllipse.Location = new System.Drawing.Point(18, 103);
            this.btn_rectEllipse.Name = "btn_rectEllipse";
            this.btn_rectEllipse.Size = new System.Drawing.Size(49, 19);
            this.btn_rectEllipse.TabIndex = 3;
            this.btn_rectEllipse.Text = "椭圆";
            // 
            // btn_rectPolygon
            // 
            this.btn_rectPolygon.AutoSize = true;
            this.btn_rectPolygon.Location = new System.Drawing.Point(18, 130);
            this.btn_rectPolygon.Name = "btn_rectPolygon";
            this.btn_rectPolygon.Size = new System.Drawing.Size(61, 19);
            this.btn_rectPolygon.TabIndex = 4;
            this.btn_rectPolygon.Text = "多边形";
            // 
            // RoiEditor
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.Controls.Add(this.grb_RectInfo);
            this.Controls.Add(this.but_editRegion);
            this.Controls.Add(this.btn_drawRegion);
            this.Controls.Add(this.grb_Rect);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Size = new System.Drawing.Size(546, 224);
            this.Name = "RoiEditor";
            this.grb_RectInfo.ResumeLayout(false);
            this.grb_RectInfo.PerformLayout();
            this.grb_Rect.ResumeLayout(false);
            this.grb_Rect.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grb_RectInfo;
        private System.Windows.Forms.Label lbl_Fix0;
        private System.Windows.Forms.TextBox txt_Width;
        private System.Windows.Forms.Label lbl_Fix1;
        private System.Windows.Forms.TextBox txt_Height;
        private System.Windows.Forms.Label lbl_Fix2;
        private System.Windows.Forms.TextBox txt_TopLeft;
        private System.Windows.Forms.Label lbl_Fix3;
        private System.Windows.Forms.TextBox txt_BottomRight;
        private System.Windows.Forms.Label lbl_Fix4;
        private System.Windows.Forms.TextBox txt_Center;
        private System.Windows.Forms.Button but_editRegion;
        private System.Windows.Forms.Button btn_drawRegion;
        private System.Windows.Forms.GroupBox grb_Rect;
        private System.Windows.Forms.RadioButton btn_rectRectangle;
        private System.Windows.Forms.RadioButton btn_rectAffRect;
        private System.Windows.Forms.RadioButton btn_rectCircle;
        private System.Windows.Forms.RadioButton btn_rectEllipse;
        private System.Windows.Forms.RadioButton btn_rectPolygon;
    }
}
