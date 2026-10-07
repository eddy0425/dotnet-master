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
            this.tabFile = new System.Windows.Forms.TabPage();
            this.filePanel = new DotNet.HalconUI.ParamPanel();
            this.tabParam = new System.Windows.Forms.TabPage();
            this.paramPanel = new DotNet.HalconUI.ParamPanel();
            this.tabRegion = new System.Windows.Forms.TabPage();
            this.pnl_roi = new System.Windows.Forms.Panel();
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
            this.regionPanel = new DotNet.HalconUI.ParamPanel();
            this.tabMatching = new System.Windows.Forms.TabPage();
            this.matchingPanel = new DotNet.HalconUI.ParamPanel();
            this.pnl_model = new System.Windows.Forms.Panel();
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
            this.tabDisplay = new System.Windows.Forms.TabPage();
            this.displayPanel = new DotNet.HalconUI.ParamPanel();
            this.pnl_actions = new System.Windows.Forms.Panel();
            this.btn_saveEdit = new System.Windows.Forms.Button();
            this.btn_runTest = new System.Windows.Forms.Button();
            this.btn_cancelEdit = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tabControl1.SuspendLayout();
            this.tabFile.SuspendLayout();
            this.tabParam.SuspendLayout();
            this.tabRegion.SuspendLayout();
            this.pnl_roi.SuspendLayout();
            this.grb_RectInfo.SuspendLayout();
            this.grb_Rect.SuspendLayout();
            this.tabMatching.SuspendLayout();
            this.pnl_model.SuspendLayout();
            this.grb_ModeRect.SuspendLayout();
            this.tabDisplay.SuspendLayout();
            this.pnl_actions.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabFile);
            this.tabControl1.Controls.Add(this.tabParam);
            this.tabControl1.Controls.Add(this.tabRegion);
            this.tabControl1.Controls.Add(this.tabMatching);
            this.tabControl1.Controls.Add(this.tabDisplay);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F);
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(560, 263);
            this.tabControl1.TabIndex = 0;
            // 
            // tabFile
            // 
            this.tabFile.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.tabFile.Controls.Add(this.filePanel);
            this.tabFile.Location = new System.Drawing.Point(4, 26);
            this.tabFile.Name = "tabFile";
            this.tabFile.Padding = new System.Windows.Forms.Padding(3);
            this.tabFile.Size = new System.Drawing.Size(552, 233);
            this.tabFile.TabIndex = 0;
            this.tabFile.Text = "文件图像";
            // 
            // filePanel
            // 
            this.filePanel.AutoScroll = true;
            this.filePanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.filePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filePanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.filePanel.ForeColor = System.Drawing.Color.White;
            this.filePanel.Location = new System.Drawing.Point(3, 3);
            this.filePanel.Name = "filePanel";
            this.filePanel.RowsPerColumn = 6;
            this.filePanel.Size = new System.Drawing.Size(546, 227);
            this.filePanel.TabIndex = 0;
            // 
            // tabParam
            // 
            this.tabParam.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.tabParam.Controls.Add(this.paramPanel);
            this.tabParam.Location = new System.Drawing.Point(4, 26);
            this.tabParam.Name = "tabParam";
            this.tabParam.Padding = new System.Windows.Forms.Padding(3);
            this.tabParam.Size = new System.Drawing.Size(552, 230);
            this.tabParam.TabIndex = 1;
            this.tabParam.Text = "基本参数";
            // 
            // paramPanel
            // 
            this.paramPanel.AutoScroll = true;
            this.paramPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.paramPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.paramPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.paramPanel.ForeColor = System.Drawing.Color.White;
            this.paramPanel.Location = new System.Drawing.Point(3, 3);
            this.paramPanel.Name = "paramPanel";
            this.paramPanel.RowsPerColumn = 6;
            this.paramPanel.Size = new System.Drawing.Size(546, 224);
            this.paramPanel.TabIndex = 0;
            // 
            // tabRegion
            // 
            this.tabRegion.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.tabRegion.Controls.Add(this.pnl_roi);
            this.tabRegion.Controls.Add(this.regionPanel);
            this.tabRegion.Location = new System.Drawing.Point(4, 26);
            this.tabRegion.Name = "tabRegion";
            this.tabRegion.Padding = new System.Windows.Forms.Padding(3);
            this.tabRegion.Size = new System.Drawing.Size(552, 230);
            this.tabRegion.TabIndex = 2;
            this.tabRegion.Text = "区域设置";
            // 
            // pnl_roi
            // 
            this.pnl_roi.Controls.Add(this.grb_RectInfo);
            this.pnl_roi.Controls.Add(this.but_editRegion);
            this.pnl_roi.Controls.Add(this.btn_drawRegion);
            this.pnl_roi.Controls.Add(this.grb_Rect);
            this.pnl_roi.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnl_roi.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnl_roi.Location = new System.Drawing.Point(3, 3);
            this.pnl_roi.Name = "pnl_roi";
            this.pnl_roi.Size = new System.Drawing.Size(546, 224);
            this.pnl_roi.TabIndex = 0;
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
            // regionPanel
            // 
            this.regionPanel.AutoScroll = true;
            this.regionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.regionPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.regionPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.regionPanel.ForeColor = System.Drawing.Color.White;
            this.regionPanel.Location = new System.Drawing.Point(3, 3);
            this.regionPanel.Name = "regionPanel";
            this.regionPanel.RowsPerColumn = 6;
            this.regionPanel.Size = new System.Drawing.Size(546, 0);
            this.regionPanel.TabIndex = 1;
            // 
            // tabMatching
            // 
            this.tabMatching.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.tabMatching.Controls.Add(this.matchingPanel);
            this.tabMatching.Controls.Add(this.pnl_model);
            this.tabMatching.Location = new System.Drawing.Point(4, 26);
            this.tabMatching.Name = "tabMatching";
            this.tabMatching.Padding = new System.Windows.Forms.Padding(3);
            this.tabMatching.Size = new System.Drawing.Size(552, 230);
            this.tabMatching.TabIndex = 3;
            this.tabMatching.Text = "模版设置";
            // 
            // matchingPanel
            // 
            this.matchingPanel.AutoScroll = true;
            this.matchingPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.matchingPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.matchingPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.matchingPanel.ForeColor = System.Drawing.Color.White;
            this.matchingPanel.Location = new System.Drawing.Point(543, 3);
            this.matchingPanel.Name = "matchingPanel";
            this.matchingPanel.RowsPerColumn = 6;
            this.matchingPanel.Size = new System.Drawing.Size(6, 224);
            this.matchingPanel.TabIndex = 0;
            // 
            // pnl_model
            // 
            this.pnl_model.Controls.Add(this.panel1);
            this.pnl_model.Controls.Add(this.but_editModel);
            this.pnl_model.Controls.Add(this.but_modifyModel);
            this.pnl_model.Controls.Add(this.btn_newModel);
            this.pnl_model.Controls.Add(this.grb_ModeRect);
            this.pnl_model.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnl_model.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnl_model.Location = new System.Drawing.Point(3, 3);
            this.pnl_model.Name = "pnl_model";
            this.pnl_model.Size = new System.Drawing.Size(540, 224);
            this.pnl_model.TabIndex = 1;
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
            // tabDisplay
            // 
            this.tabDisplay.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.tabDisplay.Controls.Add(this.displayPanel);
            this.tabDisplay.Location = new System.Drawing.Point(4, 26);
            this.tabDisplay.Name = "tabDisplay";
            this.tabDisplay.Padding = new System.Windows.Forms.Padding(3);
            this.tabDisplay.Size = new System.Drawing.Size(552, 230);
            this.tabDisplay.TabIndex = 4;
            this.tabDisplay.Text = "显示输出";
            // 
            // displayPanel
            // 
            this.displayPanel.AutoScroll = true;
            this.displayPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.displayPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.displayPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.displayPanel.ForeColor = System.Drawing.Color.White;
            this.displayPanel.Location = new System.Drawing.Point(3, 3);
            this.displayPanel.Name = "displayPanel";
            this.displayPanel.RowsPerColumn = 6;
            this.displayPanel.Size = new System.Drawing.Size(546, 224);
            this.displayPanel.TabIndex = 0;
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
            this.toolTip1.SetToolTip(this.btn_cancelEdit, "撤销进入本工具或上次保存参数之后的参数与 ROI 修改。\r\n模板文件新建 / 修改后立即生效，不在撤销范围内。");
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
            this.tabFile.ResumeLayout(false);
            this.tabParam.ResumeLayout(false);
            this.tabRegion.ResumeLayout(false);
            this.pnl_roi.ResumeLayout(false);
            this.grb_RectInfo.ResumeLayout(false);
            this.grb_RectInfo.PerformLayout();
            this.grb_Rect.ResumeLayout(false);
            this.grb_Rect.PerformLayout();
            this.tabMatching.ResumeLayout(false);
            this.pnl_model.ResumeLayout(false);
            this.grb_ModeRect.ResumeLayout(false);
            this.grb_ModeRect.PerformLayout();
            this.tabDisplay.ResumeLayout(false);
            this.pnl_actions.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabFile;
        private DotNet.HalconUI.ParamPanel filePanel;
        private System.Windows.Forms.TabPage tabParam;
        private DotNet.HalconUI.ParamPanel paramPanel;
        private System.Windows.Forms.TabPage tabRegion;
        private DotNet.HalconUI.ParamPanel regionPanel;
        private System.Windows.Forms.Panel pnl_roi;
        private System.Windows.Forms.GroupBox grb_Rect;
        private System.Windows.Forms.RadioButton btn_rectRectangle;
        private System.Windows.Forms.RadioButton btn_rectAffRect;
        private System.Windows.Forms.RadioButton btn_rectCircle;
        private System.Windows.Forms.RadioButton btn_rectEllipse;
        private System.Windows.Forms.RadioButton btn_rectPolygon;
        private System.Windows.Forms.Button btn_drawRegion;
        private System.Windows.Forms.Button but_editRegion;
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
        private System.Windows.Forms.TabPage tabMatching;
        private DotNet.HalconUI.ParamPanel matchingPanel;
        private System.Windows.Forms.Panel pnl_model;
        private System.Windows.Forms.GroupBox grb_ModeRect;
        private System.Windows.Forms.RadioButton btn_modelRectangle;
        private System.Windows.Forms.RadioButton btn_modelAffRect;
        private System.Windows.Forms.RadioButton btn_modelCircle;
        private System.Windows.Forms.RadioButton btn_modelEllipse;
        private System.Windows.Forms.RadioButton btn_modelPolygon;
        private System.Windows.Forms.Button btn_newModel;
        private System.Windows.Forms.Button but_modifyModel;
        private System.Windows.Forms.Button but_editModel;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.TabPage tabDisplay;
        private DotNet.HalconUI.ParamPanel displayPanel;
        private System.Windows.Forms.Panel pnl_actions;
        private System.Windows.Forms.Button btn_cancelEdit;
        private System.Windows.Forms.Button btn_saveEdit;
        private System.Windows.Forms.Button btn_runTest;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
