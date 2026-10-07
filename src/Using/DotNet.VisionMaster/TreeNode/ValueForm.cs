using Sunny.UI;
using System;
using DotNet.HalconCore;
using System.Windows.Forms;
using System.Collections.Generic;


namespace DotNet.VisionMaster
{
    /// <summary>
    /// 来源选择窗：列出当前工具之前各工具的输出变量树，用户双击选中一个输出。
    /// </summary>
    /// <remarks>
    /// 树的根节点是工具，<see cref="TreeNode.Tag"/> 存工具的 <see cref="IAlgoStrategy.Id"/>：
    /// 选中结果按 Id 定位（<see cref="SourceRef"/>），重名、改名都不会选错工具。
    /// "默认"根节点只在该来源有本地含义（图像 / 区域 / 坐标系）时出现。
    /// </remarks>
    public partial class ValueForm : UIForm
    {
        /// <summary> "默认"根节点的标记 </summary>
        internal static readonly object LocalTag = new object();

        private const char Split = '/';
        private readonly IWin32Window _owner;

        public ValueForm(IWin32Window owner)
        {
            InitializeComponent();
            _owner = owner;
        }

        /// <summary> 要选的输出类型 </summary>
        public OutEnum ValueType { get; private set; }

        /// <summary> 是否允许选"默认"（本地） </summary>
        public bool AllowLocal { get; private set; }

        /// <summary> 最近一次确认的选择 </summary>
        public SourceRef Picked { get; private set; }

        /// <summary>
        /// 弹出选择窗。确认返回选中的来源，取消返回 null。
        /// </summary>
        /// <param name="upstream">当前工具之前的工具（只有它们的输出可选）。</param>
        public SourceRef? Pick(IReadOnlyList<IParaStrategy> upstream, SourceParam param)
        {
            Prepare(upstream, param.SourceType, param.AllowsLocal, param.Value);
            DialogResult = DialogResult.None;
            ShowDialog(_owner);
            return DialogResult == DialogResult.OK ? Picked : (SourceRef?)null;
        }

        /// <summary> 生成树并预选当前值（不弹窗） </summary>
        internal void Prepare(IReadOnlyList<IParaStrategy> upstream, OutEnum type, bool allowLocal, SourceRef current)
        {
            ValueType = type;
            AllowLocal = allowLocal;
            Picked = current;
            GenerateTree(upstream, allowLocal);
            SelectSource(current);
        }

        internal void GenerateTree(IReadOnlyList<IParaStrategy> upstream, bool allowLocal)
        {
            treeView1.Nodes.Clear();
            if (allowLocal) treeView1.Nodes.Add(new TreeNode("默认") { Tag = LocalTag });
            if (upstream == null) return;

            foreach (var tool in upstream)
            {
                if (tool == null) continue;
                // 工具根节点记下 Id: 选中结果按 Id 定位, 不按可重名 / 可改名的显示名
                var root = treeView1.Nodes.Add(tool.Name);
                root.Tag = tool.Id;
                foreach (var item in tool.Outputs) AddOutput(root.Nodes, item);
            }
        }

        /// <summary> 一个输出一个节点，<see cref="TreeNode.Name"/> 记输出类型（<see cref="Matches"/> 按它判断） </summary>
        private static void AddOutput(TreeNodeCollection nodes, OutputItem item)
        {
            var node = nodes.Add(item.Name);
            node.Name = item.Type.ToString();
            foreach (var child in item.Children) AddOutput(node.Nodes, child);
        }

        /// <summary>
        /// 按当前值预选节点：逐段往下找，找不到的那一段就停在最深的现存祖先上，方便用户就近重选。
        /// </summary>
        internal void SelectSource(SourceRef source)
        {
            TreeNode node = null;
            foreach (TreeNode root in treeView1.Nodes)
            {
                if (source.IsLocal ? ReferenceEquals(root.Tag, LocalTag) : root.Tag is Guid id && id == source.ToolId)
                {
                    node = root;
                    break;
                }
            }
            if (node != null && !source.IsLocal && !string.IsNullOrEmpty(source.Output))
            {
                foreach (string part in source.Output.Split(Split))
                {
                    TreeNode next = null;
                    foreach (TreeNode child in node.Nodes)
                    {
                        if (child.Text == part) { next = child; break; }
                    }
                    if (next == null) break;
                    node = next;
                }
            }

            if (node == null) return;
            treeView1.SelectedNode = node;
            node.EnsureVisible();
        }

        /// <summary> 节点能否作为结果：类型必须与 <see cref="ValueType"/> 相符 </summary>
        internal bool TryAccept(TreeNode node, out SourceRef picked)
        {
            picked = SourceRef.Local;
            if (node == null) return false;

            if (node.Level == 0)
                return ReferenceEquals(node.Tag, LocalTag) && AllowLocal;   // 工具根节点本身不是输出

            if (!Matches(node.Name)) return false;

            var root = node;
            while (root.Parent != null) root = root.Parent;
            if (!(root.Tag is Guid toolId)) return false;
            picked = new SourceRef(toolId, PathOf(node));
            return true;
        }

        private bool Matches(string nodeType)
        {
            if (nodeType == ValueType.ToString()) return true;
            switch (ValueType)
            {
                case OutEnum.String:
                    return nodeType != nameof(OutEnum.HTuple) && nodeType != nameof(OutEnum.Outline) &&
                           nodeType != nameof(OutEnum.Image) && nodeType != nameof(OutEnum.Region);
                case OutEnum.CalOrOut:
                    return nodeType == nameof(OutEnum.Angle) || nodeType == nameof(OutEnum.Number) || nodeType == nameof(OutEnum.String);
                default:
                    return false;
            }
        }

        /// <summary> 工具根节点之下的路径，例如 <c>坐标系/原点</c> </summary>
        private static string PathOf(TreeNode node)
        {
            var parts = new Stack<string>();
            for (var n = node; n.Parent != null; n = n.Parent) parts.Push(n.Text);
            return string.Join(Split.ToString(), parts);
        }

        private void 全部展开ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.ExpandAll();
        }
        private void 全部折叠ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.CollapseAll();
        }
        private void treeView1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                TreeNode node = treeView1.GetNodeAt(e.Location);
                if (node != null) treeView1.SelectedNode = node;
            }
            else if (e.Button == MouseButtons.Right)
            {
                treeView1.ContextMenuStrip = contextMenuStrip1;
            }
        }
        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
        }
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            treeView1.Invalidate();
        }
        private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // 只认双击落在的那个节点: 双击空白不能把上一次单击选中的节点当成结果确认掉
            TreeNode node = treeView1.GetNodeAt(e.Location);
            if (node == null || node != treeView1.SelectedNode) return;
            if (!TryAccept(node, out SourceRef picked)) return;

            Picked = picked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ValueForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }
        private void ValueForm_VisibleChanged(object sender, EventArgs e)
        {
            if (Visible) Location = DialogPlacement.Beside(Owner, Size);
        }
    }
}
