using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DotNet.HalconCore;
using DotNet.HalconRuntime;
using Sunny.UI;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 工具箱：按算法目录的分组和顺序展示内置算法及插件。
    /// 节点保存算法元数据，添加和拖拽使用稳定键，不依赖显示名。
    /// </summary>
    public partial class ToolForm : UIForm
    {
        internal const string AlgorithmDragFormat = "DotNet.VisionMaster.AlgorithmKey";
        private static readonly Color SelectedBackColor = Color.FromArgb(0, 122, 204);
        private static readonly Color GlyphColor = Color.FromArgb(170, 175, 190);
        private static readonly Color ToolTextColor = Color.FromArgb(215, 218, 225);
        private readonly AlgoCatalog _catalog;
        private Font _groupFont;

        // 供 WinForms Designer 使用；运行时使用带目录的构造函数。
        public ToolForm() : this(AlgoCatalog.Load(null)) { }

        public ToolForm(AlgoCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            InitImageList();
            treeView_Tool.NodeMouseDoubleClick += treeView_Tool_NodeMouseDoubleClick;
            treeView_Tool.KeyDown += treeView_Tool_KeyDown;
            treeView_Tool.FontChanged += (s, e) => ResetGroupFont();
            Disposed += (s, e) => ResetGroupFont();
            StartPosition = FormStartPosition.Manual;

            // 当前 SunnyUI 没有标题栏扩展按钮，使用标准菜单保留置顶开关。
            var topMostItem = new ToolStripMenuItem("置顶") { CheckOnClick = true, Checked = TopMost };
            topMostItem.CheckedChanged += (s, e) => TopMost = topMostItem.Checked;
            contextMenuStrip1.Opening += (s, e) => topMostItem.Checked = TopMost;
            contextMenuStrip1.Items.Add(new ToolStripSeparator());
            contextMenuStrip1.Items.Add(topMostItem);
        }

        /// <summary> 用户双击工具或按 Enter 时通知宿主；工具实例由宿主创建。 </summary>
        public event Action<string> ToolSelected;

        /// <summary>
        /// 使用窗体自己的图标，避免共享 ImageList 的生命周期相互影响。
        /// 不要在这里改 ColorDepth/ImageSize：图标来自 ImageStream，重建句柄会把它们全部丢掉；要改请在 Designer 里改。
        /// </summary>
        internal void InitImageList()
        {
            treeView_Tool.ImageList = imageList1;
        }

        internal void GenerateTree()
        {
            treeView_Tool.BeginUpdate();
            try
            {
                treeView_Tool.Nodes.Clear();
                foreach (var group in _catalog.Algorithms.GroupBy(a => a.Group))
                {
                    int icon = GroupIcon(group.Key);
                    var root = treeView_Tool.Nodes.Add(group.Key, group.Key, icon, icon);
                    foreach (var info in group)
                    {
                        var node = root.Nodes.Add(info.Key, info.DisplayName, icon, icon);
                        node.Tag = info;
                        node.ToolTipText = info.Key;
                    }
                }
                treeView_Tool.ShowNodeToolTips = true;
                label_Note.Text = "提示：双击或按 Enter 添加工具，也可拖到流程列表。";
            }
            finally { treeView_Tool.EndUpdate(); }
        }

        /// <summary>
        /// 画分组图标，流程列表与工具箱共用同一套；没有对应图标时不画。
        /// 不用 Images[i]：每次取都会新建一个 Bitmap，放在重绘里会一直漏句柄。
        /// </summary>
        internal void DrawGroupIcon(Graphics g, Rectangle bounds, string group)
        {
            int icon = GroupIcon(group);
            if (icon >= 0) imageList1.Draw(g, bounds.X, bounds.Y, bounds.Width, bounds.Height, icon);
        }

        private int GroupIcon(string group)
        {
            string key;
            switch (group)
            {
                case "图像": key = "图像相关.png"; break;
                case "定位": key = "匹配.png"; break;
                case "区域": key = "区域处理.png"; break;
                case "测量": key = "几何测量.png"; break;
                default: key = "调试.png"; break;
            }
            return imageList1.Images.IndexOfKey(key);
        }

        private void ToolFrm_Load(object sender, EventArgs e) => GenerateTree();

        private void ResetGroupFont()
        {
            _groupFont?.Dispose();
            _groupFont = null;
        }

        /// <summary>
        /// 自绘节点：整行选中背景、实线连接线、三角展开符、分组名加粗。
        /// 展开符和图标仍按原生布局摆放（按层级 × Indent），点击命中由原生 TreeView 判定，位置必须对得上。
        /// 连接线颜色取 Designer 里的 LineColor；ShowLines 在自绘模式下不起作用。
        /// </summary>
        private void treeView_Tool_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            // 节点不可见时会收到空 Bounds
            if (e.Bounds.IsEmpty) return;
            var tree = treeView_Tool;
            var g = e.Graphics;
            var node = e.Node;
            var row = new Rectangle(0, e.Bounds.Y, tree.ClientSize.Width, e.Bounds.Height);
            // 用 IsSelected 而不是 e.State：失焦时 State 不带 Selected，配合 HideSelection = false 保持高亮
            bool selected = node.IsSelected;
            int indent = tree.Indent;
            int midY = row.Y + row.Height / 2;
            int imageWidth = tree.ImageList?.ImageSize.Width ?? 0;
            int iconX = node.Bounds.X - imageWidth - 2;

            using (var back = new SolidBrush(selected ? SelectedBackColor : tree.BackColor))
                g.FillRectangle(back, row);

            using (var pen = new Pen(tree.LineColor))
            {
                // 祖先还有后续兄弟时，竖线要穿过本行
                for (var p = node.Parent; p?.Parent != null; p = p.Parent)
                    if (p.NextNode != null)
                    {
                        int px = (p.Level - 1) * indent + indent / 2;
                        g.DrawLine(pen, px, row.Y, px, row.Bottom);
                    }
                if (node.Parent != null)
                {
                    int x = (node.Level - 1) * indent + indent / 2;
                    g.DrawLine(pen, x, row.Y, x, node.NextNode != null ? row.Bottom : midY);
                    g.DrawLine(pen, x, midY, iconX - 3, midY);
                }
                // 展开的分组从展开符下方接出竖线，连到第一个子节点
                if (node.IsExpanded && node.Nodes.Count > 0)
                {
                    int x = node.Level * indent + indent / 2;
                    g.DrawLine(pen, x, midY + 6, x, row.Bottom);
                }
            }

            if (node.Nodes.Count > 0)
            {
                int cx = node.Level * indent + indent / 2;
                Point[] glyph = node.IsExpanded
                    ? new[] { new Point(cx - 4, midY - 2), new Point(cx + 4, midY - 2), new Point(cx, midY + 3) }
                    : new[] { new Point(cx - 2, midY - 4), new Point(cx - 2, midY + 4), new Point(cx + 3, midY) };
                var oldMode = g.SmoothingMode;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(selected ? Color.White : GlyphColor))
                    g.FillPolygon(brush, glyph);
                g.SmoothingMode = oldMode;
            }

            if (tree.ImageList != null && node.ImageIndex >= 0)
                tree.ImageList.Draw(g, iconX, midY - tree.ImageList.ImageSize.Height / 2, node.ImageIndex);

            Font font = tree.Font;
            if (node.Parent == null)
                font = _groupFont ?? (_groupFont = new Font(tree.Font, FontStyle.Bold));
            var textRect = new Rectangle(node.Bounds.X, row.Y, Math.Max(0, row.Right - node.Bounds.X), row.Height);
            Color textColor = selected || node.Parent == null ? Color.White : ToolTextColor;
            TextRenderer.DrawText(g, node.Text, font, textRect, textColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void treeView_Tool_MouseDown(object sender, MouseEventArgs e)
        {
            var node = treeView_Tool.GetNodeAt(e.Location);
            if (node != null) treeView_Tool.SelectedNode = node;
        }

        private void treeView_Tool_AfterSelect(object sender, TreeViewEventArgs e)
        {
            var info = e.Node?.Tag as AlgoInfo;
            Text = info?.DisplayName ?? "工具箱";
            if (info == null)
            {
                label_Note.Text = "提示：选择工具，双击或按 Enter 添加。";
                return;
            }
            var description = (DescriptionAttribute)Attribute.GetCustomAttribute(info.Type, typeof(DescriptionAttribute));
            label_Note.Text = !string.IsNullOrWhiteSpace(description?.Description)
                ? description.Description
                : $"{info.Group}：{info.DisplayName}\r\n算法键：{info.Key}";
        }

        internal void ActivateTool(TreeNode node)
        {
            if (node?.Tag is AlgoInfo info && ReferenceEquals(_catalog.Find(info.Key), info))
                ToolSelected?.Invoke(info.Key);
        }

        private void treeView_Tool_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ActivateTool(e.Node);
        }

        private void treeView_Tool_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            ActivateTool(treeView_Tool.SelectedNode);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void treeView_Tool_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !(e.Item is TreeNode node) || !(node.Tag is AlgoInfo info)) return;
            var data = new DataObject();
            data.SetData(AlgorithmDragFormat, info.Key);
            treeView_Tool.DoDragDrop(data, DragDropEffects.Copy);
        }

        private void Form_Tool_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 用户关闭只隐藏，主窗销毁 / 程序退出必须允许正常释放。
            if (e.CloseReason != CloseReason.UserClosing) return;
            Hide();
            e.Cancel = true;
        }

        private void 全部展开ToolStripMenuItem_Click(object sender, EventArgs e) => treeView_Tool.ExpandAll();

        private void 全部折叠ToolStripMenuItem_Click(object sender, EventArgs e) => treeView_Tool.CollapseAll();

        private void Form_Tool_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible) Location = DialogPlacement.Beside(Owner, Size);
        }
    }
}
