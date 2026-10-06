using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="MainForm"/>：工具箱来自算法目录、流程增删排序改名、切换工具、运行、方案读写、释放。
    /// </summary>
    [TestClass]
    public class MainFormTests : HalconTestBase
    {
        private static readonly AlgoCatalog BuiltIn = AlgoCatalog.Load(new[] { typeof(FileImageStrategy).Assembly });

        private static void Run(Action<MainForm> body, AlgoCatalog catalog = null, bool defaultFlow = true) =>
            Sta.Run(() =>
            {
                using (var form = new MainForm(catalog ?? BuiltIn, defaultFlow))
                {
                    WindowHost.ShowOffscreen(form);
                    body(form);
                }
            });

        private static List<IParaStrategy> Tools(MainForm form) => Priv.Get<List<IParaStrategy>>(form, "_tools");

        private static ParaForm Para(MainForm form) => Priv.Get<ParaForm>(form, "_formPara");

        private static string Status(MainForm form) => Priv.Get<Label>(form, "lbl_status").Text;

        /// <summary> 工具箱的树只在 Load 时生成；测试不显示工具箱，这里手动生成 </summary>
        private static ToolForm Toolbox(MainForm form)
        {
            var toolbox = Priv.Get<ToolForm>(form, "_formTool");
            toolbox.GenerateTree();
            return toolbox;
        }

        private static TreeNode ToolboxNode(ToolForm toolbox, string group, string displayName) =>
            Priv.Get<TreeView>(toolbox, "treeView_Tool").Nodes.Cast<TreeNode>().Single(n => n.Text == group)
                .Nodes.Cast<TreeNode>().Single(n => n.Text == displayName);

        #region 工具箱

        /// <summary> 新增算法不改宿主：初始流程与工具箱都来自目录扫描（含原先漏注册的三个） </summary>
        [TestMethod]
        public void Constructor_OneToolPerAlgorithm_FromCatalog()
        {
            Run(form =>
            {
                var tools = Tools(form);
                Assert.AreEqual(11, tools.Count);
                CollectionAssert.AreEquivalent(BuiltIn.Algorithms.Select(a => a.Type).ToArray(), tools.Select(t => t.GetType()).ToArray());
                CollectionAssert.IsSubsetOf(new[] { typeof(RotateImageStrategy), typeof(LineRotImageStrategy), typeof(MergeRegionStrategy) },
                    tools.Select(t => t.GetType()).ToArray());
                Assert.AreEqual(tools.Count, tools.Select(t => t.Id).Distinct().Count(), "每个工具一个 Id");
                Assert.IsInstanceOfType(tools[0], typeof(FileImageStrategy), "按 [Algo] 的 Order 排: 取像在最前");
                Assert.AreEqual(-1, form.SelectedIndex, "启动时不自动选中");
            });
        }

        [TestMethod]
        public void Toolbox_TreeUsesCatalogKeys_AndActivationAddsTool()
        {
            Run(form =>
            {
                var toolbox = Toolbox(form);
                var tree = Priv.Get<TreeView>(toolbox, "treeView_Tool");
                var images = Priv.Get<ImageList>(toolbox, "imageList1");
                Assert.AreSame(images, tree.ImageList);
                Assert.IsTrue(images.Images.IndexOfKey("调试.png") >= 0, "图标不能在初始化时丢失");
                Assert.IsTrue(tree.Nodes.Cast<TreeNode>().All(n => n.ImageIndex >= 0), "每个分组都有图标");
                var nodes = tree.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).ToArray();
                CollectionAssert.AreEqual(BuiltIn.Algorithms.GroupBy(a => a.Group).Select(g => g.Key).ToArray(),
                    tree.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray());
                CollectionAssert.AreEquivalent(BuiltIn.Algorithms.Select(a => a.Key).ToArray(), nodes.Select(n => n.Name).ToArray());
                Assert.IsTrue(nodes.All(n => ReferenceEquals(n.Tag, BuiltIn.Find(n.Name))));

                toolbox.ActivateTool(tree.Nodes[0]);
                Assert.AreEqual(0, form.Tools.Count, "分类节点不能添加工具");
                var info = BuiltIn.Algorithms[0];
                toolbox.ActivateTool(nodes.Single(n => n.Name == info.Key));
                Assert.AreEqual(1, form.Tools.Count);
                Assert.AreEqual(info.Type, form.CurrentTool.GetType());
                Assert.AreEqual(info.DisplayName, form.CurrentTool.Name);
            }, defaultFlow: false);
        }

        [TestMethod]
        public void Toolbox_DisposedWithHost()
        {
            Sta.Run(() =>
            {
                var form = new MainForm(BuiltIn, createDefaultFlow: false);
                var toolbox = Priv.Get<ToolForm>(form, "_formTool");
                form.Dispose();
                Assert.IsTrue(toolbox.IsDisposed);
            });
        }

        [TestMethod]
        public void Toolbox_GroupedByCatalogGroups()
        {
            Run(form =>
            {
                var tree = Priv.Get<TreeView>(Toolbox(form), "treeView_Tool");
                var groups = tree.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray();
                CollectionAssert.AreEquivalent(new[] { "图像", "区域", "测量", "定位" }, groups);
                var measure = tree.Nodes.Cast<TreeNode>().Single(n => n.Text == "测量");
                CollectionAssert.AreEqual(new[] { "拟合直线", "圆弧中点" }, measure.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray());
            });
        }

        [TestMethod]
        public void ToolboxActivate_InsertsAfterCurrent_AndSelectsIt()
        {
            Run(form =>
            {
                form.SelectTool(2);
                var toolbox = Toolbox(form);

                toolbox.ActivateTool(ToolboxNode(toolbox, "测量", "拟合直线"));

                Assert.AreEqual(12, Tools(form).Count);
                Assert.AreEqual(3, form.SelectedIndex);
                Assert.IsInstanceOfType(form.CurrentTool, typeof(FitLineStrategy));
                Assert.AreEqual("拟合直线1", form.CurrentTool.Name, "同名时自动编号");
                Assert.AreSame(form.CurrentTool, Para(form).Tool);
            }, BuiltIn);
        }

        /// <summary>
        /// 验收：只引用 Drawing + HalconCore 编译出的插件，出现在工具箱里，参数页按它的声明生成。
        /// </summary>
        [TestMethod]
        public void Plugin_AppearsInToolbox_AndItsParamsAreShown()
        {
            string dir = Path.Combine(Path.GetTempPath(), "VisionMasterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.Copy(SamplePlugin.Dll(), Path.Combine(dir, "DotNet.SamplePlugin.dll"));
                var catalog = AlgoCatalog.Load(new[] { typeof(FileImageStrategy).Assembly }, dir);

                Run(form =>
                {
                    var toolbox = Toolbox(form);
                    var sample = Priv.Get<TreeView>(toolbox, "treeView_Tool").Nodes.Cast<TreeNode>().Single(n => n.Text == "示例");
                    toolbox.ActivateTool(sample.Nodes[0]);

                    Assert.AreEqual("sample.region-area", AlgoInfo.Of(form.CurrentTool).Key);
                    CollectionAssert.AreEqual(new[] { "区域来源", "最小面积" },
                        Para(form).PanelOf(TabPageEnum.Parameter).Items.Select(i => i.Label).ToArray());
                }, catalog, defaultFlow: false);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        #endregion

        #region 流程编辑

        [TestMethod]
        public void SelectTool_SyncsParaForm()
        {
            Run(form =>
            {
                for (int i = 0; i < Tools(form).Count; i++)
                {
                    form.SelectTool(i);
                    Assert.AreEqual(i, form.SelectedIndex);
                    Assert.AreSame(Tools(form)[i], Para(form).Tool);
                    Assert.AreEqual(i, Priv.Get<ListBox>(form, "lst_tools").SelectedIndex);
                }
            });
        }

        /// <summary> 绘制进行中切换工具：提示用户，宿主与参数页都停在原工具上 </summary>
        [TestMethod]
        public void SelectTool_WhileDrawing_PromptsAndStays()
        {
            Run(form =>
            {
                form.SelectTool(2);
                using (var prompts = new PromptLog())
                {
                    Priv.Set(Para(form), "_drawBusy", true);
                    try { form.SelectTool(4); }
                    finally { Priv.Set(Para(form), "_drawBusy", false); }

                    Assert.AreEqual(1, prompts.Messages.Count);
                    StringAssert.Contains(prompts.Messages[0], "正在绘制");
                }
                Assert.AreEqual(2, form.SelectedIndex);
                Assert.AreSame(Tools(form)[2], Para(form).Tool);
            });
        }

        [TestMethod]
        public void RemoveTool_DisposesAndSelectsNeighbour()
        {
            Run(form =>
            {
                var removed = new FakeStrategy("待删");
                Tools(form).Insert(1, removed);
                form.SelectTool(1);

                form.RemoveTool(1);

                Assert.IsTrue(removed.Disposed);
                CollectionAssert.DoesNotContain(Tools(form), removed);
                Assert.AreEqual(1, form.SelectedIndex);
            });
        }

        /// <summary> 下移后引用排到了自己后面：运行前校验把它标出来 </summary>
        [TestMethod]
        public void MoveTool_BreakingOrder_IsFlagged()
        {
            Run(form =>
            {
                var source = new FakeStrategy("来源") { Outs = o => o.Image("图像", () => null) };
                var user = new FakeStrategy("使用者");
                user.inPara.Image = source.Ref("图像");
                Tools(form).AddRange(new IParaStrategy[] { source, user });

                Assert.AreEqual(0, form.ValidateFlow(false).Count);

                form.MoveTool(0, +1);

                var issues = form.ValidateFlow(false);
                Assert.AreEqual(1, issues.Count);
                Assert.AreSame(user, issues[0].Tool);
                StringAssert.Contains(Priv.Get<ListBox>(form, "lst_tools").Items[0].ToString(), "引用错误");
            }, defaultFlow: false);
        }

        [TestMethod]
        public void RenameTool_KeepsReferences()
        {
            Run(form =>
            {
                var source = new FakeStrategy("来源") { Outs = o => o.Image("图像", () => null) };
                var user = new FakeStrategy("使用者");
                user.inPara.Image = source.Ref("图像");
                Tools(form).AddRange(new IParaStrategy[] { source, user });

                form.RenameTool(0, "新名字");

                Assert.AreEqual("新名字", source.Name);
                Assert.AreEqual(0, form.ValidateFlow(false).Count, "引用按 Id 保存, 改名不断开");
                Assert.AreEqual("新名字/图像", Tools(form).Describe(user.inPara.Image));
            }, defaultFlow: false);
        }

        #endregion

        #region 运行

        [TestMethod]
        public void RunCurrent_RunsSelectedToolOnly_AndShowsStatus()
        {
            Run(form =>
            {
                var a = new FakeStrategy("A");
                var b = new FakeStrategy("B") { RunError = new InvalidOperationException("运行失败原因") };
                Tools(form).AddRange(new IParaStrategy[] { a, b });
                form.SelectTool(1);

                var result = form.RunCurrent();

                Assert.AreEqual(RunStatus.Error, result.Status);
                Assert.AreEqual(0, a.Runs);
                Assert.AreEqual(1, b.Runs);
                StringAssert.Contains(Status(form), "B: 失败 运行失败原因");
                StringAssert.EndsWith(Priv.Get<ListBox>(form, "lst_tools").Items[1].ToString(), "✘", "失败的工具在列表里标出来");
            }, defaultFlow: false);
        }

        [TestMethod]
        public void RunFlow_StopsAtFirstFailure()
        {
            Run(form =>
            {
                var a = new FakeStrategy("A");
                var b = new FakeStrategy("B") { RunError = new InvalidOperationException("坏了") };
                var c = new FakeStrategy("C");
                Tools(form).AddRange(new IParaStrategy[] { a, b, c });

                var result = form.RunFlow();

                Assert.IsTrue(result.Stopped);
                Assert.AreEqual(1, a.Runs);
                Assert.AreEqual(0, c.Runs);
                StringAssert.Contains(Status(form), "失败: B: 坏了");
            }, defaultFlow: false);
        }

        [TestMethod]
        public void RunButtons_NoTool_DoNothing()
        {
            Run(form =>
            {
                using (var prompts = new PromptLog())
                {
                    Priv.Click(form, "but_Run_Click");
                    Priv.Click(form, "but_RunFlow_Click");
                    CollectionAssert.AreEqual(new string[0], prompts.Messages);
                }
            }, defaultFlow: false);
        }

        #endregion

        #region 方案

        [TestMethod]
        public void SaveThenOpen_RestoresFlow()
        {
            string dir = Path.Combine(Path.GetTempPath(), "VisionMasterTests_" + Guid.NewGuid().ToString("N"));
            try
            {
                Guid[] ids = null;
                string[] names = null;
                Run(form =>
                {
                    form.RenameTool(0, "主相机");
                    ids = Tools(form).Select(t => t.Id).ToArray();
                    names = Tools(form).Select(t => t.Name).ToArray();
                    form.SchemeDirPicker = s => dir;
                    Priv.Click(form, "btn_save_Click");
                    StringAssert.Contains(Status(form), "方案已保存");
                });

                Run(form =>
                {
                    form.SchemeDirPicker = s => dir;
                    Priv.Click(form, "btn_open_Click");

                    CollectionAssert.AreEqual(ids, Tools(form).Select(t => t.Id).ToArray());
                    CollectionAssert.AreEqual(names, Tools(form).Select(t => t.Name).ToArray());
                    Assert.AreEqual(0, form.SelectedIndex, "打开后选中第一个工具");
                    Assert.AreEqual(Path.Combine(dir, ids[0].ToString("N")), Tools(form)[0].DataDir);
                }, defaultFlow: false);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void Open_FolderWithoutScheme_Prompts()
        {
            Run(form =>
            {
                form.SchemeDirPicker = s => Path.GetTempPath();
                using (var prompts = new PromptLog())
                {
                    Priv.Click(form, "btn_open_Click");
                    StringAssert.Contains(prompts.Messages.Single(), FlowScheme.FileName);
                }
                Assert.AreEqual(11, Tools(form).Count, "流程不变");
            });
        }

        #endregion

        #region 释放

        [TestMethod]
        public void Dispose_DisposesAllTools()
        {
            Sta.Run(() =>
            {
                List<IParaStrategy> tools;
                using (var form = new MainForm(BuiltIn, true))
                {
                    WindowHost.ShowOffscreen(form);
                    Tools(form).Add(new FakeStrategy());
                    tools = Tools(form).ToList();
                }

                Assert.IsTrue(((FakeStrategy)tools.Last()).Disposed);
                Assert.IsTrue(tools.OfType<FileImageStrategy>().Single().Image == null || !tools.OfType<FileImageStrategy>().Single().Image.IsInitialized());
            });
        }

        /// <summary>某个工具释放失败时，其余工具照样释放，窗体销毁不被打断。</summary>
        [TestMethod]
        public void Dispose_OneToolThrows_OthersStillDisposed()
        {
            Sta.Run(() =>
            {
                var after = new FakeStrategy();
                var thrower = new ThrowOnDisposeStrategy();
                using (var form = new MainForm(BuiltIn, false))
                {
                    WindowHost.ShowOffscreen(form);
                    Tools(form).Add(thrower);
                    Tools(form).Add(after);
                }

                Assert.IsTrue(thrower.DisposeCalled);
                Assert.IsTrue(after.Disposed);
            });
        }

        private sealed class ThrowOnDisposeStrategy : IParaStrategy
        {
            public bool DisposeCalled;

            public void Dispose()
            {
                DisposeCalled = true;
                throw new InvalidOperationException("释放失败");
            }

            public Guid Id { get; set; } = Guid.NewGuid();
            public string Name { get; set; } = "throw";
            public RunResult LastResult => null;
            public object Para { get; set; }
            public string DataDir { get; set; }
            public IReadOnlyList<OutputItem> Outputs => new OutputItem[0];
            public OutputItem FindOutput(string path) => null;
            public RunResult Run(RunContext context, IHDisplay display) => RunResult.Ok();
            public void Init(IRoiHost host) { }
            public void Close(IRoiHost host) { }
        }

        #endregion
    }
}
