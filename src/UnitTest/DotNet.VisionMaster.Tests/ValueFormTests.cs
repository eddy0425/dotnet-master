using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="ValueForm"/>：按上游工具生成输出变量树、按来源预选节点、双击时按类型过滤并返回按 Id 定位的 <see cref="SourceRef"/>。
    /// </summary>
    /// <remarks>
    /// <c>Pick</c> 以 <c>ShowDialog</c> 结尾；这里直接调 <c>Prepare</c> 生成树与预选，再调双击处理方法。
    /// </remarks>
    [TestClass]
    public class ValueFormTests
    {
        private static FakeStrategy Line0() => new FakeStrategy("直线查找0")
        {
            Outs = o => o.Region("区域", () => null).Line("直线", () => null).Number("角度", () => 0),
        };

        private static FakeStrategy Match0() => new FakeStrategy("形状匹配0")
        {
            Outs = o => o.Image("图像", () => null).Coord("坐标", () => new DotNet.Drawing.CvCoord(), () => null).Number("分数", () => 0),
        };

        private static void Run(Action<ValueForm, TreeView, List<IParaStrategy>> body) =>
            Sta.Run(() =>
            {
                using (var form = new ValueForm(null))
                {
                    var tree = Priv.Get<TreeView>(form, "treeView1");
                    // 双击按鼠标位置命中节点，节点的 Bounds 要有句柄才算得出来；只建句柄、不显示窗体
                    GC.KeepAlive(form.Handle);
                    GC.KeepAlive(tree.Handle);
                    var upstream = new List<IParaStrategy> { Line0(), Match0() };
                    body(form, tree, upstream);
                }
            });

        private static TreeNode Find(TreeView tree, string path)
        {
            var parts = path.Split('/');
            var node = tree.Nodes.Cast<TreeNode>().Single(n => n.Text == parts[0]);
            foreach (var part in parts.Skip(1))
                node = node.Nodes.Cast<TreeNode>().Single(n => n.Text == part);
            return node;
        }

        /// <summary>选中 <paramref name="path"/> 后双击，返回 (是否确认, Picked)。</summary>
        private static Tuple<bool, SourceRef> DoubleClick(ValueForm form, TreeView tree, string path)
        {
            form.DialogResult = DialogResult.None;
            var node = Find(tree, path);
            tree.SelectedNode = node;
            node.EnsureVisible();
            DoubleClickAt(form, tree, Center(node.Bounds));
            return Tuple.Create(form.DialogResult == DialogResult.OK, form.Picked);
        }

        private static Point Center(Rectangle r) => new Point(r.X + r.Width / 2, r.Y + r.Height / 2);

        private static void DoubleClickAt(ValueForm form, TreeView tree, Point location) =>
            Priv.Call(form, "treeView1_MouseDoubleClick", tree, new MouseEventArgs(MouseButtons.Left, 2, location.X, location.Y, 0));

        private static string[] Roots(TreeView tree) => tree.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray();

        public sealed class Gauge { }

        private sealed class GaugePara : DisplayOptions
        {
            public SourceRef Input { get; set; } = SourceRef.Local;
        }

        private sealed class GaugeReader : ParaStrategyBase<GaugePara>
        {
            protected override void DeclareParams(ParamBuilder p) => p.Source<Gauge>("量规来源", () => inPara.Input, v => inPara.Input = v);
            protected override void DeclareOutputs(OutputBuilder o) { }
            protected override void ResetOutputs() { }
            protected override RunResult Execute(RunContext context) => RunResult.Ok();
        }

        /// <summary> 选择窗与运行前校验是同一条兼容规则：按类型声明的来源只能选值能赋过去的输出 </summary>
        [TestMethod]
        public void DoubleClick_TypedSource_AcceptsOnlyAssignableOutputs()
        {
            Run((form, tree, upstream) =>
            {
                upstream.Add(new FakeStrategy("量规0") { Outs = o => o.Value("量规", () => new Gauge()).Number("读数", () => 1) });
                using (var reader = new GaugeReader())
                {
                    var param = (SourceParam)reader.DescribeParams().Single(i => i.Label == "量规来源");
                    form.Prepare(upstream, param.SourceType, param.AllowsLocal, param.Value, param.Accepts);

                    Assert.IsFalse(Roots(tree).Contains("默认"), "按类型声明的来源没有本地含义");
                    Assert.IsFalse(DoubleClick(form, tree, "量规0/读数").Item1);
                    Assert.IsFalse(DoubleClick(form, tree, "形状匹配0/图像").Item1);
                    Assert.IsTrue(DoubleClick(form, tree, "量规0/量规").Item1, "确认后窗口关闭, 放在最后");
                }
            });
        }

        #region 生成树

        [TestMethod]
        public void Tree_ListsLocalThenUpstreamTools_RootsCarryToolId()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, allowLocal: true, current: SourceRef.Local);

                CollectionAssert.AreEqual(new[] { "默认", "直线查找0", "形状匹配0" }, Roots(tree));
                Assert.AreSame(ValueForm.LocalTag, tree.Nodes[0].Tag);
                Assert.AreEqual(upstream[0].Id, tree.Nodes[1].Tag, "工具根节点记 Id, 选择结果按 Id 定位");
            });
        }

        [TestMethod]
        public void Tree_NoLocalForTypesWithoutLocalMeaning()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Line, allowLocal: false, current: SourceRef.Local);

                CollectionAssert.AreEqual(new[] { "直线查找0", "形状匹配0" }, Roots(tree));
            });
        }

        [TestMethod]
        public void Tree_PreparedAgain_ReplacesPreviousTree()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);
                form.Prepare(upstream.Take(1).ToList(), OutEnum.Region, true, SourceRef.Local);

                CollectionAssert.AreEqual(new[] { "默认", "直线查找0" }, Roots(tree), "同一窗体被 ParaForm 复用，每次打开都要重建");
            });
        }

        [TestMethod]
        public void Tree_ToolOutputsIncludeDerivedChildren()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Number, true, SourceRef.Local);

                Assert.IsNotNull(Find(tree, "直线查找0/直线/起点/行"), "线段自动带出起点 / 行");
                Assert.IsNotNull(Find(tree, "形状匹配0/坐标/原点/列"));
                Assert.IsNotNull(Find(tree, "形状匹配0/结果"), "基类追加的公共输出");
            });
        }

        #endregion

        #region 预选

        [TestMethod]
        public void Select_Local()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);
                Assert.AreSame(tree.Nodes[0], tree.SelectedNode);
            });
        }

        [DataTestMethod]
        [DataRow(0, "区域", "直线查找0/区域")]
        [DataRow(0, "直线/起点/行", "直线查找0/直线/起点/行")]
        [DataRow(1, "坐标", "形状匹配0/坐标")]
        public void Select_PreviousOutputById(int tool, string output, string expectedPath)
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, new SourceRef(upstream[tool].Id, output));

                Assert.AreSame(Find(tree, expectedPath), tree.SelectedNode);
            });
        }

        /// <summary> 按 Id 预选：工具改了名照样选得中 </summary>
        [TestMethod]
        public void Select_RenamedTool_StillFound()
        {
            Run((form, tree, upstream) =>
            {
                upstream[0].Name = "改了名";
                form.Prepare(upstream, OutEnum.Region, true, new SourceRef(upstream[0].Id, "区域"));

                Assert.AreSame(Find(tree, "改了名/区域"), tree.SelectedNode);
            });
        }

        /// <summary>上游工具还在、只是那个输出没了：退而选中最深的现存祖先，方便用户就近重选。</summary>
        [TestMethod]
        public void Select_UnknownOutput_SelectsDeepestExistingAncestor()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, new SourceRef(upstream[0].Id, "直线/已删除"));

                Assert.AreSame(Find(tree, "直线查找0/直线"), tree.SelectedNode);
            });
        }

        [TestMethod]
        public void Select_UnknownTool_SelectsNothing()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, new SourceRef(Guid.NewGuid(), "区域"));

                Assert.IsNull(tree.SelectedNode, "上游工具已删除时不应选中任何节点");
            });
        }

        #endregion

        #region 双击选择

        [TestMethod]
        public void DoubleClick_MatchingType_ReturnsRefById()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);

                var r = DoubleClick(form, tree, "直线查找0/区域");

                Assert.IsTrue(r.Item1);
                Assert.AreEqual(new SourceRef(upstream[0].Id, "区域"), r.Item2);
            });
        }

        [TestMethod]
        public void DoubleClick_NestedNumber_ReturnsFullOutputPath()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Number, true, SourceRef.Local);

                var r = DoubleClick(form, tree, "直线查找0/直线/起点/行");

                Assert.IsTrue(r.Item1);
                Assert.AreEqual("直线/起点/行", r.Item2.Output);
            });
        }

        [TestMethod]
        public void DoubleClick_MismatchedType_Rejected()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);

                Assert.IsFalse(DoubleClick(form, tree, "直线查找0/直线").Item1, "要区域却选了直线，不应确认");
            });
        }

        /// <summary> 图像来源不再接受区域（原规则允许，但区域当图像用必然在后续算子里出错） </summary>
        [TestMethod]
        public void DoubleClick_ImageTarget_RejectsRegion()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Image, true, SourceRef.Local);

                Assert.IsFalse(DoubleClick(form, tree, "直线查找0/区域").Item1);
            });
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Image, true, SourceRef.Local);

                Assert.IsTrue(DoubleClick(form, tree, "形状匹配0/图像").Item1);
            });
        }

        [TestMethod]
        public void DoubleClick_Local_AcceptedWhenAllowed()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Coord, true, new SourceRef(upstream[1].Id, "坐标"));

                var r = DoubleClick(form, tree, "默认");

                Assert.IsTrue(r.Item1);
                Assert.AreEqual(SourceRef.Local, r.Item2);
            });
        }

        [TestMethod]
        public void DoubleClick_ToolRootNode_Rejected()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);

                Assert.IsFalse(DoubleClick(form, tree, "直线查找0").Item1, "工具分组节点本身不是变量");
            });
        }

        [DataTestMethod]
        [DataRow("直线查找0/直线/起点/行", true)]     // Number
        [DataRow("直线查找0/区域", false)]           // Region
        [DataRow("形状匹配0/图像", false)]           // Image
        public void DoubleClick_StringTarget_AcceptsScalarsOnly(string path, bool accepted)
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.String, false, SourceRef.Local);

                Assert.AreEqual(accepted, DoubleClick(form, tree, path).Item1);
            });
        }

        /// <summary>
        /// 双击树的空白处不算选择：左键点空白不会改变选中项，若直接取选中项会把上一次单击的节点确认掉。
        /// </summary>
        [TestMethod]
        public void DoubleClick_BlankArea_Ignored()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);
                tree.SelectedNode = Find(tree, "直线查找0/区域");
                form.DialogResult = DialogResult.None;

                DoubleClickAt(form, tree, new Point(5, tree.ClientSize.Height - 5));

                Assert.AreNotEqual(DialogResult.OK, form.DialogResult);
                Assert.AreEqual(SourceRef.Local, form.Picked, "没选到东西就不该动结果");
            });
        }

        #endregion

        #region 交互

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;

        /// <summary>焦点在树上时按 Esc 关闭引用窗；取消时 <see cref="ValueForm.Pick"/> 返回 null。</summary>
        [TestMethod]
        public void Escape_WhileTreeFocused_CancelsPick()
        {
            Run((form, tree, upstream) =>
            {
                SourceRef s = SourceRef.Local;
                var param = (SourceParam)new ParamBuilder().Source("区域", () => s, v => s = v, OutEnum.Region).Items[0];
                SourceRef? picked = new SourceRef(Guid.NewGuid(), "x");

                using (WindowHost.RespondWhenShown(form, () =>
                {
                    tree.Focus();
                    SendMessage(tree.Handle, WM_KEYDOWN, (IntPtr)Keys.Escape, IntPtr.Zero);
                    SendMessage(tree.Handle, WM_KEYUP, (IntPtr)Keys.Escape, IntPtr.Zero);
                }))
                {
                    picked = form.Pick(upstream, param);
                }

                Assert.IsNull(picked);
            });
        }

        [TestMethod]
        public void ContextMenu_ExpandAll_ThenCollapseAll()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Number, true, SourceRef.Local);
                var point = Find(tree, "直线查找0/直线/起点");

                Priv.Click(form, "全部展开ToolStripMenuItem_Click");
                Assert.IsTrue(point.IsExpanded && point.Parent.IsExpanded);

                Priv.Click(form, "全部折叠ToolStripMenuItem_Click");
                Assert.IsFalse(tree.Nodes.Cast<TreeNode>().Any(n => n.IsExpanded));
            });
        }

        [TestMethod]
        public void MouseDown_LeftSelectsNodeUnderCursor_RightAttachesContextMenu()
        {
            Run((form, tree, upstream) =>
            {
                form.Prepare(upstream, OutEnum.Region, true, SourceRef.Local);
                var target = Find(tree, "形状匹配0");
                var at = Center(target.Bounds);

                Priv.Call(form, "treeView1_MouseDown", tree, new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
                Assert.AreSame(target, tree.SelectedNode);

                Priv.Call(form, "treeView1_MouseDown", tree, new MouseEventArgs(MouseButtons.Right, 1, at.X, at.Y, 0));
                Assert.AreSame(Priv.Get<ContextMenuStrip>(form, "contextMenuStrip1"), tree.ContextMenuStrip);
            });
        }

        #endregion

        #region 弹出位置

        private static readonly Rectangle WorkArea = new Rectangle(0, 0, 1920, 1040);
        private static readonly Size DialogSize = new Size(223, 571);

        [TestMethod]
        public void Placement_BesideOwner_WhenItFits()
        {
            Assert.AreEqual(new Point(900, 100),
                DialogPlacement.Beside(new Rectangle(100, 100, 800, 600), DialogSize, WorkArea));
        }

        /// <summary>主窗靠右、靠下或在屏幕外时，引用窗不能跑出屏幕。</summary>
        [DataTestMethod]
        [DataRow(1200, 100, 1697, 100)]    // 右侧放不下：贴屏幕右缘
        [DataRow(100, 800, 900, 469)]      // 下方放不下：贴屏幕下缘
        [DataRow(-3000, -3000, 0, 0)]      // 主窗在屏幕外
        public void Placement_ClampedToWorkingArea(int ownerX, int ownerY, int expectedX, int expectedY)
        {
            Assert.AreEqual(new Point(expectedX, expectedY),
                DialogPlacement.Beside(new Rectangle(ownerX, ownerY, 800, 600), DialogSize, WorkArea));
        }

        [TestMethod]
        public void Placement_NoOwner_UsesDefaultPoint()
        {
            Assert.AreEqual(new Point(500, 300), DialogPlacement.Beside(null, DialogSize, WorkArea));
        }

        /// <summary>默认位置按工作区偏移：副屏上的主窗最大化时，弹窗留在副屏。</summary>
        [TestMethod]
        public void Placement_NoOwner_DefaultIsRelativeToWorkingArea()
        {
            var secondary = new Rectangle(1920, 0, 1920, 1040);
            Assert.AreEqual(new Point(2420, 300), DialogPlacement.Beside(null, DialogSize, secondary));
        }

        [TestMethod]
        public void Placement_MaximizedOwnerForm_UsesDefaultPoint()
        {
            Sta.Run(() =>
            {
                using (var owner = new Form { WindowState = FormWindowState.Maximized })
                    Assert.AreEqual(DialogPlacement.Beside(null, DialogSize, Screen.FromControl(owner).WorkingArea),
                        DialogPlacement.Beside(owner, DialogSize));
                Assert.AreEqual(DialogPlacement.Beside(null, DialogSize, Screen.FromPoint(Cursor.Position).WorkingArea),
                    DialogPlacement.Beside((Form)null, DialogSize));
            });
        }

        [TestMethod]
        public void Placement_OwnerForm_StaysInsideOwnersScreen()
        {
            Sta.Run(() =>
            {
                var area = Screen.PrimaryScreen.WorkingArea;
                using (var owner = new Form { StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(area.Right - 400, area.Top, 400, 300) })
                {
                    var at = DialogPlacement.Beside(owner, DialogSize);

                    Assert.IsTrue(new Rectangle(at, DialogSize).Right <= area.Right, "贴右缘的主窗旁边放不下，应挪回屏幕内");
                    Assert.AreEqual(area.Top, at.Y);
                }
            });
        }

        [TestMethod]
        public void Shown_WithOwner_PlacedBesideOwner()
        {
            Run((form, tree, upstream) =>
            {
                using (var owner = new Form { StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(100, 100, 400, 300), ShowInTaskbar = false })
                {
                    owner.Show();
                    Point? shownAt = null;
                    using (WindowHost.RespondWhenShown(form, () => shownAt = form.Location))
                        form.ShowDialog(owner);

                    Assert.AreEqual(DialogPlacement.Beside(owner, form.Size), shownAt);
                }
            });
        }

        #endregion
    }
}
