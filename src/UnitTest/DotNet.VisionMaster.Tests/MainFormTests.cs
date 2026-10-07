using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconCore;
using DotNet.HalconRuntime;
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

        /// <summary> 运行在执行会话里: 泵消息直到任务（含 UI 线程上的显示）完成 </summary>
        private static T Wait<T>(Task<T> task)
        {
            WindowHost.PumpUntil(() => task.IsCompleted);
            return task.GetAwaiter().GetResult();
        }

        private static void Wait(Task task)
        {
            WindowHost.PumpUntil(() => task.IsCompleted);
            task.GetAwaiter().GetResult();
        }

        /// <summary> 等主窗彻底空闲：会话跑完、显示完、停止通知处理完 </summary>
        private static void WaitIdle(MainForm form) => WindowHost.PumpUntil(() => !form.IsBusy && !Para(form).HostBusy);

        private static ParaForm Para(MainForm form) => Priv.Get<ParaForm>(form, "_formPara");

        private static string Status(MainForm form) => Priv.Get<Label>(form, "lbl_status").Text;

        private static InfoForm Info(MainForm form) => Priv.Get<InfoForm>(form, "_formInfo");

        /// <summary> 主窗右侧嵌入的流程窗口 </summary>
        private static JobForm Jobs(MainForm form) => Priv.Get<JobForm>(form, "_formJob");

        private static ListBox ToolList(JobForm jobs) => Priv.Get<ListBox>(jobs, "lst_tools");

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
        /// <summary> 坏插件只拒绝它自己：加载报告写进信息窗口、状态栏提一句，不弹框，好插件照常可用 </summary>
        [TestMethod]
        public void Plugin_Rejected_ReportedInInfoWindow_WithoutPrompt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "VisionMasterTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(dir, "DotNet.SamplePlugin"));
            try
            {
                File.Copy(SamplePlugin.Dll(), Path.Combine(dir, "DotNet.SamplePlugin", "DotNet.SamplePlugin.dll"));
                File.WriteAllBytes(Path.Combine(dir, "broken.dll"), new byte[] { 0x4D, 0x5A, 0, 0 });
                var catalog = AlgoCatalog.Load(new[] { typeof(FileImageStrategy).Assembly }, dir);

                using (var prompts = new PromptLog())
                {
                    Run(form =>
                    {
                        Assert.IsNotNull(form.Catalog.Find("sample.region-area"), "好插件照常可用");
                        Assert.AreEqual("错误(1)", Priv.Get<ToolStripButton>(Info(form), "tsb_error").Text);
                        StringAssert.Contains(Status(form), "1 个插件未加载");
                    }, catalog, defaultFlow: false);
                    CollectionAssert.AreEqual(new string[0], prompts.Messages, "不再弹大对话框");
                }
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

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
                        Para(form).PanelOf(Pages.Parameter).Items.Select(i => i.Label).ToArray());
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
                    Assert.AreEqual(i, ToolList(Jobs(form)).SelectedIndex);
                }
            });
        }

        /// <summary> 下方区域同一时间只显示一页：启动时是信息窗口，选中工具切到参数页，视图菜单可来回切换 </summary>
        [TestMethod]
        public void BottomPages_SwitchBetweenParametersAndInfo()
        {
            Run(form =>
            {
                Assert.IsTrue(Info(form).Visible);
                Assert.IsFalse(Para(form).Visible);

                form.SelectTool(0);
                Assert.IsTrue(Para(form).Visible);
                Assert.IsFalse(Info(form).Visible);

                Priv.Click(form, "mnu_viewInfo_Click");
                Assert.IsTrue(Info(form).Visible);
                Assert.IsFalse(Para(form).Visible);

                Priv.Click(Jobs(form), "mnu_editPara_Click");
                Assert.IsTrue(Para(form).Visible, "右键编辑参数切回参数页");

                Priv.Click(Para(form), "btn_cancelEdit_Click");
                Assert.IsTrue(Info(form).Visible, "取消编辑切回信息窗口");
                Assert.IsFalse(Para(form).Visible);

                Priv.Click(Jobs(form), "lst_tools_DoubleClick");
                Assert.IsTrue(Para(form).Visible, "双击已选中的工具切回参数页");
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
                StringAssert.Contains(ToolList(Jobs(form)).Items[0].ToString(), "引用错误");
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

                var result = Wait(form.RunCurrentAsync());

                Assert.AreEqual(RunStatus.Error, result.Status);
                Assert.AreEqual(0, a.Runs);
                Assert.AreEqual(1, b.Runs);
                StringAssert.Contains(Status(form), "B: 失败 运行失败原因");
                StringAssert.EndsWith(ToolList(Jobs(form)).Items[1].ToString(), "✘", "失败的工具在列表里标出来");
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

                var result = Wait(form.RunFlowAsync());

                Assert.IsTrue(result.Stopped);
                Assert.AreEqual(1, a.Runs);
                Assert.AreEqual(0, c.Runs);
                StringAssert.Contains(Status(form), "失败: B: 坏了");
                Assert.AreEqual("错误(1)", Priv.Get<ToolStripButton>(Info(form), "tsb_error").Text, "失败记入信息窗口");
            }, defaultFlow: false);
        }

        [TestMethod]
        public void RunButtons_NoTool_DoNothing()
        {
            Run(form =>
            {
                using (var prompts = new PromptLog())
                {
                    Priv.Click(Jobs(form), "mnu_runCurrent_Click");
                    Priv.Click(Jobs(form), "btn_runOnce_Click");
                    CollectionAssert.AreEqual(new string[0], prompts.Messages);
                }
            }, defaultFlow: false);
        }

        #endregion

        [TestMethod]
        public void RunLoop_StopsOnFailure_AndToggles()
        {
            Run(form =>
            {
                // 执行要花点时间: 否则会话线程可能在下一行断言"正在运行"之前就已经因失败停下
                var tool = new FakeStrategy("失败工具") { RunError = new InvalidOperationException("失败"), RunDelayMs = 300 };
                Tools(form).Add(tool);
                form.ShowParameters();
                Priv.Click(Jobs(form), "btn_runLoop_Click");
                Assert.IsTrue(form.IsLoopRunning);
                Priv.Click(Jobs(form), "btn_runOnce_Click");

                WindowHost.PumpUntil(() => !form.IsLoopRunning && Info(form).Visible);
                WaitIdle(form);
                Assert.AreEqual(1, tool.Runs, "失败后自动停下; 连续运行期间单次运行不生效");
                Assert.IsTrue(Info(form).Visible, "因失败停下时切到信息窗口");
                StringAssert.Contains(Status(form), "失败: 失败工具: 失败", "失败的那一帧也显示出来");

                tool.RunError = null;
                tool.RunDelayMs = 0;
                Priv.Click(Jobs(form), "btn_runLoop_Click");
                WindowHost.PumpUntil(() => tool.Runs >= 3);
                Assert.IsTrue(form.IsLoopRunning, "成功时一直跑下去");
                Priv.Click(Jobs(form), "btn_runLoop_Click");
                Assert.IsFalse(form.IsLoopRunning, "再点一次停止");
                WaitIdle(form);
                int stoppedAt = tool.Runs;
                WindowHost.PumpUntil(() => true, 300);
                Thread.Sleep(300);
                Assert.AreEqual(stoppedAt, tool.Runs, "停下之后不再执行");
            }, defaultFlow: false);
        }

        /// <summary> 参数页的"运行测试"只跑当前工具；连续运行期间不可用 </summary>
        [TestMethod]
        public void ParaRunTest_RunsCurrentTool_DisabledWhileLooping()
        {
            Run(form =>
            {
                var a = new FakeStrategy("A");
                var b = new FakeStrategy("B");
                Tools(form).AddRange(new IParaStrategy[] { a, b });
                form.SelectTool(1);

                Priv.Click(Para(form), "btn_runTest_Click");
                WaitIdle(form);
                Assert.AreEqual(0, a.Runs);
                Assert.AreEqual(1, b.Runs);
                StringAssert.Contains(Status(form), "B: OK");

                form.StartLoop();
                Assert.IsTrue(Para(form).HostBusy);
                Assert.IsFalse(Priv.Get<Button>(Para(form), "btn_runTest").Enabled);
                Assert.IsFalse(Priv.Get<Button>(Para(form).Editor<RoiEditor>(), "btn_drawRegion").Enabled, "会话忙时不能绘制 ROI");

                form.StopLoop();
                WaitIdle(form);
                Assert.IsFalse(Para(form).HostBusy);
                Assert.IsTrue(Priv.Get<Button>(Para(form).Editor<RoiEditor>(), "btn_drawRegion").Enabled);
            }, defaultFlow: false);
        }

        /// <summary> 流程在执行会话的工作线程上跑：算子耗时期间 UI 线程照常处理消息 </summary>
        [TestMethod]
        public void RunFlow_RunsOffUiThread_UiStaysResponsive()
        {
            Run(form =>
            {
                var slow = new FakeStrategy("慢") { RunDelayMs = 400 };
                Tools(form).Add(slow);
                int uiThread = Thread.CurrentThread.ManagedThreadId;
                int ticks = 0;
                using (var timer = new System.Windows.Forms.Timer { Interval = 20 })
                {
                    timer.Tick += (s, e) => ticks++;
                    timer.Start();

                    var run = form.RunFlowAsync();
                    Assert.IsFalse(run.IsCompleted, "运行请求立即返回, 不在 UI 线程上执行");
                    Assert.IsTrue(form.IsBusy);
                    Wait(run);

                    Assert.AreNotEqual(uiThread, slow.ExecuteThreadId, "工具在会话线程上执行");
                    Assert.IsTrue(ticks >= 5, $"运行期间 UI 消息照常处理 (定时器只跳了 {ticks} 次)");
                }
                Assert.IsFalse(form.IsBusy);
            }, defaultFlow: false);
        }

        /// <summary> 会话忙时不能改流程结构 </summary>
        [TestMethod]
        public void StructureChanges_WhileRunning_Prompt()
        {
            Run(form =>
            {
                var slow = new FakeStrategy("慢") { RunDelayMs = 300 };
                Tools(form).Add(slow);
                var run = form.RunFlowAsync();
                using (var prompts = new PromptLog())
                {
                    form.RemoveTool(0);
                    form.AddTool(BuiltIn.Algorithms[0].Key, select: false);
                    Assert.AreEqual(2, prompts.Messages.Count);
                    StringAssert.Contains(prompts.Messages[0], "正在运行");
                }
                Assert.AreEqual(1, Tools(form).Count, "流程不变");
                Wait(run);
            }, defaultFlow: false);
        }

        /// <summary> 连续运行中改参数：排进会话，下一帧生效，不用停机 </summary>
        [TestMethod]
        public void ParamWrite_WhileLooping_QueuedIntoSession()
        {
            Run(form =>
            {
                var tool = new FakeStrategy("A") { RunDelayMs = 30 };
                Tools(form).Add(tool);
                form.SelectTool(0);
                var flag = Para(form).ParamItems.Single(i => i.Label == "开关");
                var check = (CheckBox)Para(form).PanelOf(Pages.Display).EditorOf(flag);

                form.StartLoop();
                WindowHost.PumpUntil(() => tool.Runs >= 2);
                check.Checked = !check.Checked;
                WindowHost.PumpUntil(() => tool.ChangedLabels.Contains("开关"));

                Assert.IsTrue(form.IsLoopRunning, "改参数不用停机");
                Assert.IsTrue(tool.inPara.Flag == check.Checked, "写回已经生效");
                Assert.IsTrue(Para(form).IsDirty);
                form.StopLoop();
                WaitIdle(form);
            }, defaultFlow: false);
        }

        #region 流程窗口

        /// <summary> 流程窗口在运行时嵌进主窗右侧，主窗 Designer 里不放列表和按钮 </summary>
        [TestMethod]
        public void MainForm_EmbedsJobForm()
        {
            Run(form =>
            {
                var jobs = Jobs(form);
                Assert.IsFalse(jobs.TopLevel);
                Assert.AreSame(Priv.Get<Panel>(form, "panel3"), jobs.Parent);
                Assert.IsTrue(jobs.Visible);
                Assert.AreEqual(Tools(form).Count, ToolList(jobs).Items.Count);
            });
        }

        [TestMethod]
        public void JobForm_Unbound_DisablesRunButtons()
        {
            Sta.Run(() =>
            {
                using (var jobs = new JobForm())
                {
                    Assert.IsFalse(jobs.btn_runOnce.Enabled);
                    Assert.IsFalse(jobs.btn_runLoop.Enabled);
                    jobs.RunOnce();
                    Priv.Click(jobs, "btn_runLoop_Click");
                    Assert.IsFalse(jobs.IsLoopRunning);
                }
            });
        }

        [TestMethod]
        public void JobForm_RunAndSelect_UseHostFlow()
        {
            Run(form =>
            {
                var a = new FakeStrategy("A");
                var b = new FakeStrategy("B");
                Tools(form).AddRange(new IParaStrategy[] { a, b });
                using (var jobs = new JobForm(form))
                {
                    var list = ToolList(jobs);
                    Assert.AreEqual(2, list.Items.Count);
                    list.SelectedIndex = 1;
                    Assert.AreEqual(1, form.SelectedIndex);
                    Wait(jobs.RunOnce());
                    WindowHost.Pump();
                    Assert.AreEqual(1, a.Runs);
                    Assert.AreEqual(1, b.Runs);
                    StringAssert.EndsWith(list.Items[1].ToString(), "✔", "成功的工具也要标出来，行尾 ✔ 才会重画");
                }
            }, defaultFlow: false);
        }

        [TestMethod]
        public void JobForm_SharesHostLoop()
        {
            Run(form =>
            {
                // 执行要花点时间: 否则会话线程可能在下一行断言"正在运行"之前就已经因失败停下
                var tool = new FakeStrategy("失败工具") { RunError = new InvalidOperationException("失败"), RunDelayMs = 300 };
                Tools(form).Add(tool);
                using (var jobs = new JobForm(form))
                {
                    WindowHost.ShowOffscreen(jobs);
                    Priv.Click(jobs, "btn_runLoop_Click");
                    Assert.IsTrue(form.IsLoopRunning, "流程窗口开的是主窗那一套循环");
                    Assert.IsFalse(jobs.btn_runOnce.Enabled);
                    Assert.AreEqual("停止运行", jobs.btn_runLoop.Text);
                    WaitIdle(form);
                    Assert.IsFalse(jobs.IsLoopRunning, "失败后自动停下");
                    Assert.IsTrue(jobs.btn_runOnce.Enabled, "主窗停下后流程窗口的按钮跟着恢复");
                    Assert.AreEqual(1, tool.Runs);

                    tool.RunError = null;
                    tool.RunDelayMs = 0;
                    form.StartLoop();
                    Assert.IsTrue(jobs.IsLoopRunning, "主窗开的循环流程窗口也能看到");
                    jobs.Hide();
                    Assert.IsTrue(form.IsLoopRunning, "关流程窗口不影响主窗的循环");
                    Priv.Click(jobs, "btn_runLoop_Click");
                    Assert.IsFalse(form.IsLoopRunning, "流程窗口也能停");
                    WaitIdle(form);
                }
            }, defaultFlow: false);
        }

        [TestMethod]
        public void JobForm_FollowsHostFlowChanges_AndUnsubscribesOnDispose()
        {
            Run(form =>
            {
                Tools(form).AddRange(new IParaStrategy[] { new FakeStrategy("A"), new FakeStrategy("B") });
                var jobs = new JobForm(form);
                var list = ToolList(jobs);
                form.SelectTool(1);
                Assert.AreEqual(1, list.SelectedIndex, "主窗切换工具时同步选中");
                form.RemoveTool(0);
                Assert.AreEqual(1, list.Items.Count, "主窗删工具时同步列表");
                jobs.Dispose();
                form.RemoveTool(0);   // 已释放的流程窗口不再收到回调
                Assert.AreEqual(0, form.Tools.Count);
            }, defaultFlow: false);
        }

        [TestMethod]
        public void RemoveTool_Last_ClearsParameterBinding()
        {
            Run(form =>
            {
                var tool = new FakeStrategy();
                Tools(form).Add(tool);
                form.SelectTool(0);
                form.RemoveTool(0);
                Assert.IsTrue(tool.Disposed);
                Assert.IsNull(Para(form).Tool, "参数页不能留着已释放的工具");
            }, defaultFlow: false);
        }

        [TestMethod]
        public void JobForm_WhileDrawing_DoesNotRun()
        {
            Run(form =>
            {
                var tool = new FakeStrategy();
                Tools(form).Add(tool);
                using (var jobs = new JobForm(form))
                using (var prompts = new PromptLog())
                {
                    Priv.Set(Para(form), "_drawBusy", true);
                    try
                    {
                        jobs.RunOnce();
                        Priv.Click(jobs, "btn_runLoop_Click");
                        Assert.AreEqual(0, tool.Runs);
                        Assert.IsFalse(jobs.IsLoopRunning);
                        Assert.AreEqual(2, prompts.Messages.Count);
                    }
                    finally { Priv.Set(Para(form), "_drawBusy", false); }
                }
            }, defaultFlow: false);
        }

        [TestMethod]
        public void ClearFlow_DisposesToolsAndClearsParameterBinding()
        {
            Run(form =>
            {
                var tool = new FakeStrategy();
                Tools(form).Add(tool);
                form.SelectTool(0);
                form.ClearFlow();
                Assert.IsTrue(tool.Disposed);
                Assert.AreEqual(0, form.Tools.Count);
                Assert.AreEqual(-1, form.SelectedIndex);
                Assert.IsNull(Para(form).Tool);
                using (var jobs = new JobForm(form))
                {
                    Assert.IsFalse(jobs.btn_runOnce.Enabled);
                    Assert.IsFalse(jobs.btn_runLoop.Enabled);
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

        [TestMethod]
        public void Open_WhileLoopRunning_Prompts()
        {
            Run(form =>
            {
                Tools(form).Add(new FakeStrategy());
                form.StartLoop();
                bool picked = false;
                form.SchemeDirPicker = s => { picked = true; return null; };
                using (var prompts = new PromptLog())
                {
                    Priv.Click(form, "btn_open_Click");
                    StringAssert.Contains(prompts.Messages.Single(), "停止连续运行");
                }
                Assert.IsFalse(picked, "连续运行中不弹选择目录");
                Assert.IsTrue(form.IsLoopRunning);
                form.StopLoop();
                WaitIdle(form);
            }, defaultFlow: false);
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
            public RunResult Run(RunContext context, IOverlay overlay) => RunResult.Ok();
            public void Init(IInteractionHost host) { }
            public void Close(IInteractionHost host) { }
        }

        #endregion
    }
}
