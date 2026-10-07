namespace DotNet.VisionMaster
{
    partial class TemplateEditor
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.but_editModel = new System.Windows.Forms.Button();
            this.but_modifyModel = new System.Windows.Forms.Button();
            this.btn_newModel = new System.Windows.Forms.Button();
            this.grb_ModeRect = new System.Windows.Forms.GroupBox();
            this.btn_modelRectangle = new System.Windows.Forms.RadioButton();
            this.btn_modelAffRect = new System.Windows.Forms.RadioButton();
            this.btn_modelCircle = new System.Windows.Forms.RadioButton();
            this.btn_modelEllipse = new System.Windows.Forms.RadioButton();
            this.btn_modelPolygon = new System.Windows.Forms.RadioButton();
            this.grb_ModeRect.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Location = new System.Drawing.Point(268, 9);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(260, 160);
            this.panel1.TabIndex = 0;
            // 
            // but_editModel
            // 
            this.but_editModel.BackColor = System.Drawing.Color.Gainsboro;
            this.but_editModel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.but_editModel.ForeColor = System.Drawing.Color.Black;
            this.but_editModel.Location = new System.Drawing.Point(163, 111);
            this.but_editModel.Name = "but_editModel";
            this.but_editModel.Size = new System.Drawing.Size(75, 36);
            this.but_editModel.TabIndex = 1;
            this.but_editModel.Text = "编辑模板";
            this.but_editModel.UseVisualStyleBackColor = false;
            this.but_editModel.Click += new System.EventHandler(this.but_editModel_Click);
            // 
            // but_modifyModel
            // 
            this.but_modifyModel.BackColor = System.Drawing.Color.Gainsboro;
            this.but_modifyModel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.but_modifyModel.ForeColor = System.Drawing.Color.Black;
            this.but_modifyModel.Location = new System.Drawing.Point(163, 63);
            this.but_modifyModel.Name = "but_modifyModel";
            this.but_modifyModel.Size = new System.Drawing.Size(75, 36);
            this.but_modifyModel.TabIndex = 2;
            this.but_modifyModel.Text = "修改模版";
            this.but_modifyModel.UseVisualStyleBackColor = false;
            this.but_modifyModel.Click += new System.EventHandler(this.but_modifyModel_Click);
            // 
            // btn_newModel
            // 
            this.btn_newModel.BackColor = System.Drawing.Color.Gainsboro;
            this.btn_newModel.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.btn_newModel.ForeColor = System.Drawing.Color.Black;
            this.btn_newModel.Location = new System.Drawing.Point(163, 15);
            this.btn_newModel.Name = "btn_newModel";
            this.btn_newModel.Size = new System.Drawing.Size(75, 36);
            this.btn_newModel.TabIndex = 3;
            this.btn_newModel.Text = "新建模版";
            this.btn_newModel.UseVisualStyleBackColor = false;
            this.btn_newModel.Click += new System.EventHandler(this.btn_newModel_Click);
            // 
            // grb_ModeRect
            // 
            this.grb_ModeRect.Controls.Add(this.btn_modelRectangle);
            this.grb_ModeRect.Controls.Add(this.btn_modelAffRect);
            this.grb_ModeRect.Controls.Add(this.btn_modelCircle);
            this.grb_ModeRect.Controls.Add(this.btn_modelEllipse);
            this.grb_ModeRect.Controls.Add(this.btn_modelPolygon);
            this.grb_ModeRect.ForeColor = System.Drawing.Color.White;
            this.grb_ModeRect.Location = new System.Drawing.Point(9, 9);
            this.grb_ModeRect.Name = "grb_ModeRect";
            this.grb_ModeRect.Size = new System.Drawing.Size(124, 160);
            this.grb_ModeRect.TabIndex = 4;
            this.grb_ModeRect.TabStop = false;
            this.grb_ModeRect.Text = "区域形状";
            // 
            // btn_modelRectangle
            // 
            this.btn_modelRectangle.AutoSize = true;
            this.btn_modelRectangle.Checked = true;
            this.btn_modelRectangle.Location = new System.Drawing.Point(18, 22);
            this.btn_modelRectangle.Name = "btn_modelRectangle";
            this.btn_modelRectangle.Size = new System.Drawing.Size(56, 19);
            this.btn_modelRectangle.TabIndex = 0;
            this.btn_modelRectangle.TabStop = true;
            this.btn_modelRectangle.Text = "矩形1";
            // 
            // btn_modelAffRect
            // 
            this.btn_modelAffRect.AutoSize = true;
            this.btn_modelAffRect.Location = new System.Drawing.Point(18, 49);
            this.btn_modelAffRect.Name = "btn_modelAffRect";
            this.btn_modelAffRect.Size = new System.Drawing.Size(56, 19);
            this.btn_modelAffRect.TabIndex = 1;
            this.btn_modelAffRect.Text = "矩形2";
            // 
            // btn_modelCircle
            // 
            this.btn_modelCircle.AutoSize = true;
            this.btn_modelCircle.Location = new System.Drawing.Point(18, 76);
            this.btn_modelCircle.Name = "btn_modelCircle";
            this.btn_modelCircle.Size = new System.Drawing.Size(37, 19);
            this.btn_modelCircle.TabIndex = 2;
            this.btn_modelCircle.Text = "圆";
            // 
            // btn_modelEllipse
            // 
            this.btn_modelEllipse.AutoSize = true;
            this.btn_modelEllipse.Location = new System.Drawing.Point(18, 103);
            this.btn_modelEllipse.Name = "btn_modelEllipse";
            this.btn_modelEllipse.Size = new System.Drawing.Size(49, 19);
            this.btn_modelEllipse.TabIndex = 3;
            this.btn_modelEllipse.Text = "椭圆";
            // 
            // btn_modelPolygon
            // 
            this.btn_modelPolygon.AutoSize = true;
            this.btn_modelPolygon.Location = new System.Drawing.Point(18, 130);
            this.btn_modelPolygon.Name = "btn_modelPolygon";
            this.btn_modelPolygon.Size = new System.Drawing.Size(61, 19);
            this.btn_modelPolygon.TabIndex = 4;
            this.btn_modelPolygon.Text = "多边形";
            // 
            // TemplateEditor
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.but_editModel);
            this.Controls.Add(this.but_modifyModel);
            this.Controls.Add(this.btn_newModel);
            this.Controls.Add(this.grb_ModeRect);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Size = new System.Drawing.Size(540, 224);
            this.Name = "TemplateEditor";
            this.grb_ModeRect.ResumeLayout(false);
            this.grb_ModeRect.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button but_editModel;
        private System.Windows.Forms.Button but_modifyModel;
        private System.Windows.Forms.Button btn_newModel;
        private System.Windows.Forms.GroupBox grb_ModeRect;
        private System.Windows.Forms.RadioButton btn_modelRectangle;
        private System.Windows.Forms.RadioButton btn_modelAffRect;
        private System.Windows.Forms.RadioButton btn_modelCircle;
        private System.Windows.Forms.RadioButton btn_modelEllipse;
        private System.Windows.Forms.RadioButton btn_modelPolygon;
    }
}
