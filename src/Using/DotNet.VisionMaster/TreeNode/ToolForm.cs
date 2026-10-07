using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DotNet.HalconCore;
using DotNet.VisionRuntime;
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
        private readonly AlgoCatalog _catalog;

        // 供 WinForms Designer 使用；运行时使用带目录的构造函数。
        public ToolForm() : this(AlgoCatalog.Load(null)) { }

        public ToolForm(AlgoCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            InitializeComponent();
            InitImageList();
            treeView_Tool.NodeMouseDoubleClick += treeView_Tool_NodeMouseDoubleClick;
            treeView_Tool.KeyDown += treeView_Tool_KeyDown;
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
