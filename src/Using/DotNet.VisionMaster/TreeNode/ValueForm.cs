using Sunny.UI;
using System;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.HalconAlgo;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;


namespace DotNet.VisionMaster
{
    public partial class ValueForm : UIForm
    {
        int _runIndex;
        public string StrReturn;
        public OutEnum ValueType;

        char varSplit = '/';

        IWin32Window _owner;

        public ValueForm(IWin32Window owner)
        {
            InitializeComponent();
            _owner = owner;
        }
        public void setValueForm(int runIndex, List<IParaStrategy> _strategys, string strOrg, OutEnum type)
        {
            _runIndex = runIndex;
            StrReturn = strOrg;
            ValueType = type;

            GenerateTree(runIndex, _strategys);
            Fun_setSelectNode(StrReturn);
            this.ShowDialog(_owner);
        }
        /// <summary>
        /// 按上次选中的变量路径预选节点。
        /// </summary>
        /// <remarks>
        /// 逐段往下找，找不到的那一段就停在最深的现存祖先上，方便用户就近重选；
        /// 连根节点(上游工具)都没有时不选任何节点。路径层数不受限 —— 此前只比较前 4 段。
        /// "默认" 总是第一个根节点，不能遇到它就 return，否则任何路径都预选不上。
        /// </remarks>
        private void Fun_setSelectNode(string strIn)      //更新程序树选中节点
        {
            if (string.IsNullOrWhiteSpace(strIn)) return;

            TreeNode node = null;
            TreeNodeCollection level = treeView1.Nodes;
            foreach (string part in strIn.Split(varSplit))
            {
                TreeNode next = null;
                foreach (TreeNode item in level)
                {
                    if (item.Text == part)
                    {
                        next = item;
                        break;
                    }
                }
                if (next == null) break;
                node = next;
                level = node.Nodes;
            }

            if (node == null) return;
            treeView1.SelectedNode = node;
            node.EnsureVisible();
        }

        private void GenerateTree(int index, List<IParaStrategy> paraStrategies)    //生成输出变量节点
        {
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(new TreeNode("默认"));

            if(index >= paraStrategies.Count) return;
            TreeVisualizer treeVisualizer = new TreeVisualizer(treeView1);
            for (int i = 0; i < index; i++)
            {
                if (paraStrategies[i] is ITreeNodeProvider provider)
                    provider.GenTreeNode(treeVisualizer);
            }
        }
 
        public string Fun_getText(TreeNode node, string str)
        {
            str = varSplit + node.Text + str;
            if (node.Level > 0)
            {
                return Fun_getText(node.Parent, str);  // 递归处理父节点
            }
            return str;  // 返回最终结果
        }

        private void 全部展开ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.ExpandAll();
        }
        private void 全部折叠ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            treeView1.CollapseAll();
        }
        private void treeView1_MouseDown(object sender, MouseEventArgs e)  //当鼠标指针在组件上方并按下鼠标按钮时发生
        {
            if (e.Button == MouseButtons.Left)  //鼠标左键获取选的节点
            {
                TreeNode SelectedNode = treeView1.GetNodeAt(e.Location);

                if (SelectedNode is TreeNode)  //判断是否为节点
                {
                    treeView1.SelectedNode = SelectedNode;
                }
            }
            else if (e.Button == MouseButtons.Right) //鼠标右键获取选的节点
            {
                treeView1.ContextMenuStrip = contextMenuStrip1;// 添加右键菜单             
            }
        }
        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)  //当需要绘制节点时，在所有者描述模式下发生
        {
            ////绘制文字      
            //int cmdIndex = schemePara.defaultJob.ToolInfos.FindIndex(item => item.Text.Equals(e.Node.Text));
            //string text = (e.Node.Level == 0 && e.Node.Text != "默认") ? cmdIndex.ToString() + ". " + e.Node.Text : e.Node.Text;
            //e.Graphics.DrawString(text, treeView1.Font, new SolidBrush(Color.Black), e.Node.Bounds.X, e.Node.Bounds.Top + (e.Node.Bounds.Height - treeView1.Font.Height) / 2);
        }
        private void treeView1_BeforeExpand(object sender, TreeViewCancelEventArgs e) //在将要展开节点时发生
        {
            treeView1.Invalidate();
        }
        private void treeView1_MouseDoubleClick(object sender, MouseEventArgs e)  //用鼠标双击控件时发生 //来源 chatgpt
        {
            // 只认双击落在的那个节点: 左键点空白不会改变 SelectedNode, 若直接取 SelectedNode,
            // 双击空白会把上一次单击选中的节点当成结果确认掉。没选到东西就不动 StrReturn。
            TreeNode currentNode = treeView1.GetNodeAt(e.Location);
            if (currentNode == null || currentNode != treeView1.SelectedNode) return;
            StrReturn = "";

            // 检查根节点及类型
            if (currentNode.Level == 0)
            {
                if (currentNode != treeView1.Nodes[0]) return;
                if (ValueType != OutEnum.Image && ValueType != OutEnum.Region && ValueType != OutEnum.Coord) return;
            }

            // 检查节点名称和类型匹配
            if (currentNode.Name != ValueType.ToString() && currentNode != treeView1.Nodes[0])
            {
                switch (ValueType)
                {
                    case OutEnum.String:
                        if (currentNode.Name == nameof(OutEnum.HTuple) ||
                            currentNode.Name == nameof(OutEnum.Outline) ||
                            currentNode.Name == nameof(OutEnum.Image) ||
                            currentNode.Name == nameof(OutEnum.Region)) return;
                        break;

                    case OutEnum.CalOrOut:
                        if (currentNode.Name != nameof(OutEnum.Angle) &&
                            currentNode.Name != nameof(OutEnum.Number) &&
                            currentNode.Name != nameof(OutEnum.String)) return;
                        break;

                    case OutEnum.Angle:
                        if (currentNode.Name != nameof(OutEnum.CalOrOut)) return;
                        break;

                    case OutEnum.Array:
                        if (currentNode.Name != nameof(OutEnum.Array)) return;
                        break;

                    case OutEnum.Image:
                        if (currentNode.Name != nameof(OutEnum.Region)) return;
                        break;

                    default:
                        return;
                }
            }

            // 获取节点文本并关闭窗口
            StrReturn = Fun_getText(currentNode, StrReturn).Substring(1);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ValueForm_KeyUp(object sender, KeyEventArgs e)  //在释放时发生
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
        private void ValueForm_VisibleChanged(object sender, EventArgs e)
        {
            if (this.Visible)
            {
                this.Location = DialogPlacement.Beside(this.Owner, this.Size);
            }
        }
        private void ValueForm_ExtendBoxClick(object sender, EventArgs e)
        {
            //TopMost = !TopMost;

            //if (TopMost)
            //    ExtendSymbol = 61475;
            //else
            //    ExtendSymbol = 61758;
        }

    }
}
