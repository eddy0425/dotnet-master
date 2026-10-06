namespace DotNet.VisionMaster
{
    partial class ToolForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ToolForm));
            this.label_Note = new System.Windows.Forms.Label();
            this.treeView_Tool = new System.Windows.Forms.TreeView();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.全部展开ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.全部折叠ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label_Note
            // 
            this.label_Note.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(40)))), ((int)(((byte)(30)))));
            this.label_Note.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.label_Note.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_Note.Location = new System.Drawing.Point(0, 480);
            this.label_Note.Name = "label_Note";
            this.label_Note.Size = new System.Drawing.Size(251, 45);
            this.label_Note.TabIndex = 1;
            this.label_Note.Text = "提示：";
            // 
            // treeView_Tool
            // 
            this.treeView_Tool.AllowDrop = false;
            this.treeView_Tool.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(40)))));
            this.treeView_Tool.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeView_Tool.ContextMenuStrip = this.contextMenuStrip1;
            this.treeView_Tool.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeView_Tool.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.treeView_Tool.ForeColor = System.Drawing.Color.White;
            this.treeView_Tool.FullRowSelect = true;
            this.treeView_Tool.ItemHeight = 33;
            this.treeView_Tool.LineColor = System.Drawing.Color.DodgerBlue;
            this.treeView_Tool.Location = new System.Drawing.Point(0, 29);
            this.treeView_Tool.Margin = new System.Windows.Forms.Padding(3, 25, 3, 3);
            this.treeView_Tool.Name = "treeView_Tool";
            this.treeView_Tool.ShowLines = false;
            this.treeView_Tool.Size = new System.Drawing.Size(251, 451);
            this.treeView_Tool.TabIndex = 2;
            this.treeView_Tool.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.treeView_Tool_ItemDrag);
            this.treeView_Tool.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.treeView_Tool_AfterSelect);
            this.treeView_Tool.MouseDown += new System.Windows.Forms.MouseEventHandler(this.treeView_Tool_MouseDown);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.BackColor = System.Drawing.Color.White;
            this.contextMenuStrip1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.全部展开ToolStripMenuItem,
            this.全部折叠ToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(137, 48);
            // 
            // 全部展开ToolStripMenuItem
            // 
            this.全部展开ToolStripMenuItem.Name = "全部展开ToolStripMenuItem";
            this.全部展开ToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.全部展开ToolStripMenuItem.Text = "全部展开";
            this.全部展开ToolStripMenuItem.Click += new System.EventHandler(this.全部展开ToolStripMenuItem_Click);
            // 
            // 全部折叠ToolStripMenuItem
            // 
            this.全部折叠ToolStripMenuItem.Name = "全部折叠ToolStripMenuItem";
            this.全部折叠ToolStripMenuItem.Size = new System.Drawing.Size(136, 22);
            this.全部折叠ToolStripMenuItem.Text = "全部折叠";
            this.全部折叠ToolStripMenuItem.Click += new System.EventHandler(this.全部折叠ToolStripMenuItem_Click);
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList1.Images.SetKeyName(0, "图像00.png");
            this.imageList1.Images.SetKeyName(1, "图像获取.png");
            this.imageList1.Images.SetKeyName(2, "图像旋转.png");
            this.imageList1.Images.SetKeyName(3, "模板查找.png");
            this.imageList1.Images.SetKeyName(4, "圆形mask.png");
            this.imageList1.Images.SetKeyName(5, "方形mask.png");
            this.imageList1.Images.SetKeyName(6, "字符识别.png");
            this.imageList1.Images.SetKeyName(7, "缺陷检测.png");
            this.imageList1.Images.SetKeyName(8, "一维码识别.png");
            this.imageList1.Images.SetKeyName(9, "二维码识别.png");
            this.imageList1.Images.SetKeyName(10, "AIs识别.png");
            this.imageList1.Images.SetKeyName(11, "尺寸查找.png");
            this.imageList1.Images.SetKeyName(12, "尺寸测量.png");
            this.imageList1.Images.SetKeyName(13, "电机轴.png");
            this.imageList1.Images.SetKeyName(14, "多边形mask.png");
            this.imageList1.Images.SetKeyName(15, "标定1.png");
            this.imageList1.Images.SetKeyName(16, "调试.png");
            this.imageList1.Images.SetKeyName(17, "几何定位.png");
            this.imageList1.Images.SetKeyName(18, "矫正1.png");
            this.imageList1.Images.SetKeyName(19, "图片保存.png");
            this.imageList1.Images.SetKeyName(20, "图像相关.png");
            this.imageList1.Images.SetKeyName(21, "预处理.png");
            this.imageList1.Images.SetKeyName(22, "圆形定位.png");
            this.imageList1.Images.SetKeyName(23, "外部通信.png");
            this.imageList1.Images.SetKeyName(24, "角度查找.png");
            this.imageList1.Images.SetKeyName(25, "匹配.png");
            this.imageList1.Images.SetKeyName(26, "匹配-几何.png");
            this.imageList1.Images.SetKeyName(27, "匹配-圆形.png");
            this.imageList1.Images.SetKeyName(28, "匹配-blob.png");
            this.imageList1.Images.SetKeyName(29, "匹配-形状.png");
            this.imageList1.Images.SetKeyName(30, "匹配-灰度.png");
            this.imageList1.Images.SetKeyName(31, "区域处理.png");
            this.imageList1.Images.SetKeyName(32, "区域处理-阵列.png");
            this.imageList1.Images.SetKeyName(33, "深度学习.png");
            this.imageList1.Images.SetKeyName(34, "几何测量.png");
            this.imageList1.Images.SetKeyName(35, "几何测量_建立坐标系.png");
            this.imageList1.Images.SetKeyName(36, "几何测量_直线拟合_normal.png");
            this.imageList1.Images.SetKeyName(37, "几何测量_圆拟合.png");
            this.imageList1.Images.SetKeyName(38, "几何测量_直线测量.png");
            this.imageList1.Images.SetKeyName(39, "边缘查找.png");
            this.imageList1.Images.SetKeyName(40, "边缘查找_直线查找.png");
            this.imageList1.Images.SetKeyName(41, "边缘查找_圆查找.png");
            this.imageList1.Images.SetKeyName(42, "边缘查找_角度查找.png");
            this.imageList1.Images.SetKeyName(43, "边缘查找_直边轮廓.png");
            this.imageList1.Images.SetKeyName(44, "边缘查找_圆弧轮廓.png");
            // 
            // Form_Tool
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(40)))));
            this.ClientSize = new System.Drawing.Size(251, 525);
            this.Controls.Add(this.treeView_Tool);
            this.Controls.Add(this.label_Note);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ForeColor = System.Drawing.SystemColors.Control;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Form_Tool";
            this.Padding = new System.Windows.Forms.Padding(0, 29, 0, 0);
            this.Style = Sunny.UI.UIStyle.Custom;
            this.Text = "工具箱";
            this.TitleHeight = 29;
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form_Tool_FormClosing);
            this.Load += new System.EventHandler(this.ToolFrm_Load);
            this.VisibleChanged += new System.EventHandler(this.Form_Tool_VisibleChanged);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label label_Note;
        private System.Windows.Forms.TreeView treeView_Tool;
        private System.Windows.Forms.ImageList imageList1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 全部展开ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem 全部折叠ToolStripMenuItem;
    }
}
