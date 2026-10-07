using System.Linq;
using System.Windows.Forms;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="TreeVisualizer"/> / <see cref="TreeBranch"/>：流式 API 构建输出树，节点 Name 记录输出类型。
    /// </summary>
    [TestClass]
    public class TreeVisualizerTests
    {
        private static string[] Texts(TreeNodeCollection nodes) => nodes.Cast<TreeNode>().Select(n => n.Text).ToArray();

        [TestMethod]
        public void Branches_AddsTopLevelNodes_InOrder()
        {
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    var v = new TreeVisualizer(tree);
                    Assert.AreSame(v, v.Branches("输入", "输出"), "应返回自身以便链式调用");

                    CollectionAssert.AreEqual(new[] { "输入", "输出" }, Texts(tree.Nodes));
                }
            });
        }

        [TestMethod]
        public void Node_SetsNameToOutType_AndSupportsNesting()
        {
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    new TreeVisualizer(tree).Branch("输出", b => b
                        .Node("区域", OutEnum.Region)
                        .Node("圆", OutEnum.Circle, c => c
                            .Node("半径", OutEnum.Number))
                        .Branch("分组", g => g.Node("角度", OutEnum.Angle)));

                    var root = tree.Nodes[0];
                    CollectionAssert.AreEqual(new[] { "区域", "圆", "分组" }, Texts(root.Nodes));
                    Assert.AreEqual("Region", root.Nodes[0].Name);
                    Assert.AreEqual("Circle", root.Nodes[1].Name);
                    Assert.AreEqual("Number", root.Nodes[1].Nodes[0].Name);
                    Assert.AreEqual("", root.Nodes[2].Name, "Branch 只是分组，不带类型");
                    Assert.AreEqual("Angle", root.Nodes[2].Nodes[0].Name);
                }
            });
        }

        [TestMethod]
        public void ReusePointStructure_AddsRowAndColumnChildren()
        {
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    new TreeVisualizer(tree).Branch("输出", b => b.ReusePointStructure("中心"));

                    var point = tree.Nodes[0].Nodes[0];
                    Assert.AreEqual("中心", point.Text);
                    Assert.AreEqual("Point", point.Name);
                    CollectionAssert.AreEqual(new[] { "行", "列" }, Texts(point.Nodes));
                    Assert.IsTrue(point.Nodes.Cast<TreeNode>().All(n => n.Name == "Number"));
                }
            });
        }

        [TestMethod]
        public void CommonNodes_AppendsResultAndText()
        {
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    new TreeVisualizer(tree).Branch("输出", b => b.Node("距离", OutEnum.Distance).CommonNodes());

                    var nodes = tree.Nodes[0].Nodes;
                    CollectionAssert.AreEqual(new[] { "距离", "结果", "文本显示" }, Texts(nodes));
                    Assert.AreEqual("Result", nodes[1].Name);
                    Assert.AreEqual("String", nodes[2].Name);
                }
            });
        }

        [TestMethod]
        public void Node_WithoutConfig_ReturnsParentForChaining()
        {
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    ITreeBranch captured = null;
                    ITreeBranch returned = null;
                    new TreeVisualizer(tree).Branch("r", b =>
                    {
                        captured = b;
                        returned = b.Node("a", OutEnum.String);
                    });

                    Assert.AreSame(captured, returned);
                    Assert.AreEqual(0, tree.Nodes[0].Nodes[0].Nodes.Count);
                }
            });
        }

        [TestMethod]
        public void Ctor_NullTree_Throws()
        {
            Assert.ThrowsException<System.ArgumentNullException>(() => new TreeVisualizer(null));
            Assert.ThrowsException<System.ArgumentNullException>(() => new TreeBranch(null));
        }

        [TestMethod]
        public void Branch_WithoutConfig_AddsEmptyBranch()
        {
            // 与 Node 的 config 可选保持一致：原先 config 为 null 时节点已加上才抛 NullReferenceException
            Sta.Run(() =>
            {
                using (var tree = new TreeView())
                {
                    var v = new TreeVisualizer(tree);
                    Assert.AreSame(v, v.Branch("输出", null));
                    v.Branch("嵌套", b => Assert.AreSame(b, b.Branch("子", null)));

                    CollectionAssert.AreEqual(new[] { "输出", "嵌套" }, Texts(tree.Nodes));
                    CollectionAssert.AreEqual(new[] { "子" }, Texts(tree.Nodes[1].Nodes));
                }
            });
        }
    }
}
