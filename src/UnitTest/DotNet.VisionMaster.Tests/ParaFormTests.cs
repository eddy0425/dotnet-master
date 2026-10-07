using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconCore;
using DotNet.HalconUI;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="ParaForm"/>：按声明生成参数页、来源选择、绘制入口的类型选择与重入闸门、模板编辑窗、销毁时释放游离窗体。
    /// </summary>
    /// <remarks>
    /// 绘制入口都是 <c>async void</c>；<see cref="FakeStrategy"/> 返回的任务由测试决定何时完成，
    /// 从而能在"绘制进行中"这个窗口里观察闸门。
    /// </remarks>
    [TestClass]
    public class ParaFormTests : HalconTestBase
    {
        private sealed class Ctx
        {
            public HDisplayUI Display;
            public ParaForm Para;
            public Form Host;
            /// <summary>当前工具为 <see cref="FakeStrategy"/> 时才有值。</summary>
            public FakeStrategy Strategy;
            public PromptLog Prompts;
        }

        private static void Run(Action<Ctx> body) => Run(new FakeStrategy(), body);

        /// <param name="tool">当前工具；null 表示宿主还没选过任何工具。</param>
        /// <param name="flow">整个流程；默认只有当前工具。</param>
        private static void Run(IParaStrategy tool, Action<Ctx> body, IReadOnlyList<IParaStrategy> flow = null) =>
            Sta.Run(() =>
            {
                var display = new HDisplayUI();
                var para = new ParaForm(display);
                using (var prompts = new PromptLog())
                using (var host = WindowHost.ShowOffscreen(display, para))
                {
                    if (tool != null) para.ShowTool(tool, flow ?? new[] { tool });
                    var fake = tool as FakeStrategy;
                    var ctx = new Ctx { Display = display, Para = para, Host = host, Strategy = fake, Prompts = prompts };
                    try { body(ctx); }
                    finally
                    {
                        // 不收尾的话，挂起的 async void 续体会在窗体销毁后才跑
                        fake?.FinishDraw();
                        WindowHost.Pump();
                        foreach (var t in flow ?? (tool == null ? new IParaStrategy[0] : new[] { tool })) t.Dispose();
                    }
                }
            });

        private static void Finish(Ctx ctx)
        {
            ctx.Strategy.FinishDraw();
            WindowHost.PumpUntil(() => !ctx.Para.IsDrawBusy);
        }

        private static string[] Tabs(Ctx ctx)
            => Priv.Get<TabControl>(ctx.Para, "tabControl1").TabPages.Cast<TabPage>().Select(p => p.Text).ToArray();

        [TestMethod]
        public void IsDrawBusy_InitiallyFalse()
        {
            Run(ctx => Assert.IsFalse(ctx.Para.IsDrawBusy));
        }

        #region 参数页

        /// <summary> 宿主只认识能力接口与参数声明：标签页由"声明了哪些页 + 实现了哪些能力"决定，不按算法分支 </summary>
        [DataTestMethod]
        [DataRow(typeof(FileImageStrategy), "文件图像|显示输出")]
        [DataRow(typeof(CreateROIStrategy), "区域设置|显示输出")]
        [DataRow(typeof(MergeRegionStrategy), "基本参数|显示输出")]
        [DataRow(typeof(FitLineStrategy), "基本参数|区域设置|显示输出")]
        [DataRow(typeof(ShapeModelStrategy), "基本参数|区域设置|模版设置|显示输出")]
        public void ShowTool_TabsFollowDeclarationsAndCapabilities(Type type, string expected)
        {
            Run((IParaStrategy)Activator.CreateInstance(type), ctx => CollectionAssert.AreEqual(expected.Split('|'), Tabs(ctx)));
        }

        [TestMethod]
        public void ShowTool_EachPanelGetsItsTabsItems()
        {
            Run(ctx =>
            {
                CollectionAssert.AreEqual(new[] { "图像来源" }, ctx.Para.PanelOf(TabPageEnum.Parameter).Items.Select(i => i.Label).ToArray());
                CollectionAssert.AreEqual(new[] { "跟随坐标" }, ctx.Para.PanelOf(TabPageEnum.Region).Items.Select(i => i.Label).ToArray());
                CollectionAssert.AreEqual(new[] { "开关", "显示文本", "文本X", "文本Y", "字号" },
                    ctx.Para.PanelOf(TabPageEnum.Display).Items.Select(i => i.Label).ToArray());
            });
        }

        [TestMethod]
        public void ShowTool_ShowsRoiOfRoiTools()
        {
            Run(ctx => Assert.AreEqual(1, ctx.Strategy.DispRoiCount));
        }

        /// <summary> 面板写回后通知策略 —— 只通知真的变了的项 </summary>
        [TestMethod]
        public void PanelCommit_ForwardsChangedItemsToTool()
        {
            Run(ctx =>
            {
                var panel = ctx.Para.PanelOf(TabPageEnum.Display);
                var flag = panel.Items.Single(i => i.Label == "开关");

                ((CheckBox)panel.EditorOf(flag)).Checked = true;

                Assert.IsTrue(ctx.Strategy.inPara.Flag);
                CollectionAssert.AreEqual(new[] { "开关" }, ctx.Strategy.ChangedLabels);
            });
        }

        /// <summary> 验收：宿主的参数页里没有任何算法槽位控件 </summary>
        [TestMethod]
        public void NoSlotControls()
        {
            var slots = typeof(ParaForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(f => f.Name.StartsWith("cmb_1") || f.Name.StartsWith("btn_1") || f.Name.StartsWith("lbl_1") ||
                            f.Name.StartsWith("ckb_disp") || f.Name.StartsWith("CB_Font"))
                .Select(f => f.Name).ToArray();
            CollectionAssert.AreEqual(new string[0], slots);
        }

        #endregion

        #region 来源选择

        /// <summary>
        /// 点来源按钮：弹出变量树，只列出本工具之前的工具，按来源类型过滤；选中后按 Id 写回并通知策略。
        /// </summary>
        [TestMethod]
        public void Source_PicksUpstreamOutputById()
        {
            var upstream = new FakeStrategy("上游") { Outs = o => o.Image("图像", () => null) };
            var current = new FakeStrategy("当前");
            var downstream = new FakeStrategy("下游") { Outs = o => o.Image("图像", () => null) };

            Run(current, ctx =>
            {
                var panel = ctx.Para.PanelOf(TabPageEnum.Parameter);
                var item = panel.Items.Single(i => i.Label == "图像来源");
                var dialog = Priv.Get<ValueForm>(ctx.Para, "_valueForm");
                string[] roots = null;
                OutEnum seenType = OutEnum.Undefined;

                using (WindowHost.RespondWhenShown(dialog, () =>
                {
                    var tree = Priv.Get<TreeView>(dialog, "treeView1");
                    roots = tree.Nodes.Cast<TreeNode>().Select(n => n.Text).ToArray();
                    seenType = dialog.ValueType;
                    var node = tree.Nodes[1].Nodes.Cast<TreeNode>().Single(n => n.Text == "图像");
                    tree.SelectedNode = node;
                    var at = new Point(node.Bounds.X + node.Bounds.Width / 2, node.Bounds.Y + node.Bounds.Height / 2);
                    Priv.Call(dialog, "treeView1_MouseDoubleClick", tree, new MouseEventArgs(MouseButtons.Left, 2, at.X, at.Y, 0));
                }))
                {
                    panel.ButtonOf(item).PerformClick();
                }

                CollectionAssert.AreEqual(new[] { "默认", "上游" }, roots, "只能引用排在当前工具之前的输出");
                Assert.AreEqual(OutEnum.Image, seenType);
                Assert.AreEqual(upstream.Ref("图像"), current.inPara.Image);
                Assert.AreEqual("上游/图像", panel.EditorOf(item).Text, "界面显示的名字按当前工具列表拼出");
                CollectionAssert.AreEqual(new[] { "图像来源" }, current.ChangedLabels);
            }, new IParaStrategy[] { upstream, current, downstream });
        }

        [TestMethod]
        public void Source_Cancelled_KeepsValue()
        {
            Run(ctx =>
            {
                var panel = ctx.Para.PanelOf(TabPageEnum.Parameter);
                var item = panel.Items.Single(i => i.Label == "图像来源");
                var dialog = Priv.Get<ValueForm>(ctx.Para, "_valueForm");

                using (WindowHost.RespondWhenShown(dialog, () => dialog.DialogResult = DialogResult.Cancel))
                {
                    panel.ButtonOf(item).PerformClick();
                }

                Assert.AreEqual(SourceRef.Local, ctx.Strategy.inPara.Image);
                Assert.AreEqual(0, ctx.Strategy.ChangedLabels.Count);
            });
        }

        #endregion

        #region 绘制类型与参数

        /// <summary> 新建 ROI 的默认形状由算法自己声明（[Algo(DefaultRoi = ...)]），宿主不按算法分支 </summary>
        [TestMethod]
        public void DrawRegion_UsesDeclaredDefaultShape()
        {
            Run(new FakeAffStrategy(), ctx =>
            {
                Priv.Click(ctx.Para, "btn_drawRegion_Click");

                Assert.AreEqual(Tuple.Create(RectEnum.AffRect, true), ctx.Strategy.RoiDraws.Single());
                Assert.IsTrue(ctx.Para.IsDrawBusy);
                Finish(ctx);
                Assert.IsFalse(ctx.Para.IsDrawBusy);
            });
            Run(ctx =>
            {
                Priv.Click(ctx.Para, "btn_drawRegion_Click");
                Assert.AreEqual(Tuple.Create(RectEnum.Rectangle, true), ctx.Strategy.RoiDraws.Single());
                Finish(ctx);
            });
        }

        [TestMethod]
        public void EditRegion_KeepsCheckedType_AndEditsExistingRoi()
        {
            Run(new FakeAffStrategy(), ctx =>
            {
                Priv.Click(ctx.Para, "btn_drawRegion_Click");   // 默认形状把单选切到仿射矩形
                Finish(ctx);

                Priv.Click(ctx.Para, "but_editRegion_Click");

                Assert.AreEqual(Tuple.Create(RectEnum.AffRect, false), ctx.Strategy.RoiDraws[1]);
                Finish(ctx);
            });
        }

        [TestMethod]
        public void NewModel_And_ModifyModel_UseCheckedModelType()
        {
            Run(ctx =>
            {
                Priv.Get<RadioButton>(ctx.Para, "btn_modelCircle").Checked = true;

                Priv.Click(ctx.Para, "btn_newModel_Click");
                Finish(ctx);
                Priv.Click(ctx.Para, "but_modifyModel_Click");
                Finish(ctx);

                CollectionAssert.AreEqual(new[]
                {
                    Tuple.Create(RectEnum.Circle, true),
                    Tuple.Create(RectEnum.Circle, false),
                }, ctx.Strategy.TemplateDraws);
                Assert.AreEqual(0, ctx.Strategy.RoiDraws.Count, "模板入口不该走 ROI 绘制");
            });
        }

        [TestMethod]
        public void RoiShown_FillsGeometryReadouts()
        {
            Run(ctx =>
            {
                using (var region = new CvRegion { Bounds = new Rect2d(10.0, 20.0, 30.0, 40.0) })
                {
                    ctx.Display.SetRectPara(region);
                }

                Assert.AreEqual("30.00", Priv.Get<TextBox>(ctx.Para, "txt_Width").Text);
                Assert.AreEqual("40.00", Priv.Get<TextBox>(ctx.Para, "txt_Height").Text);
                Assert.AreEqual("25.00;40.00", Priv.Get<TextBox>(ctx.Para, "txt_Center").Text, "读数由宿主自己填, 策略不碰控件");
            });
        }

        #endregion

        #region 重入闸门

        [DataTestMethod]
        [DataRow("btn_drawRegion_Click")]
        [DataRow("but_editRegion_Click")]
        [DataRow("btn_newModel_Click")]
        [DataRow("but_modifyModel_Click")]
        public void AnyDrawEntry_WhileBusy_IsIgnored(string first)
        {
            Run(ctx =>
            {
                Priv.Click(ctx.Para, first);
                Assert.IsTrue(ctx.Para.IsDrawBusy);

                foreach (var entry in new[] { "btn_drawRegion_Click", "but_editRegion_Click", "btn_newModel_Click", "but_modifyModel_Click" })
                    Priv.Click(ctx.Para, entry);

                Assert.AreEqual(1, ctx.Strategy.RoiDraws.Count + ctx.Strategy.TemplateDraws.Count,
                    "绘制进行中，其它入口都不应再发起绘制");

                Finish(ctx);
            });
        }

        [TestMethod]
        public void DrawEntry_AfterPreviousFinished_IsAllowedAgain()
        {
            Run(ctx =>
            {
                Priv.Click(ctx.Para, "btn_drawRegion_Click");
                Finish(ctx);

                Priv.Click(ctx.Para, "btn_drawRegion_Click");

                Assert.AreEqual(2, ctx.Strategy.RoiDraws.Count);
                Assert.IsTrue(ctx.Para.IsDrawBusy);
                Finish(ctx);
            });
        }

        [TestMethod]
        public void DrawEntry_EachRoundBumpsEpoch()
        {
            Run(ctx =>
            {
                int before = Priv.Get<int>(ctx.Para, "_drawEpoch");

                Priv.Click(ctx.Para, "btn_drawRegion_Click");
                Priv.Click(ctx.Para, "btn_drawRegion_Click");   // 被闸门挡掉，不应计数
                Finish(ctx);

                Assert.AreEqual(before + 1, Priv.Get<int>(ctx.Para, "_drawEpoch"));
            });
        }

        /// <summary>绘制失败时把原因告诉用户，并且放开闸门 —— 不然之后所有绘制入口都点不动了。</summary>
        [DataTestMethod]
        [DataRow("btn_drawRegion_Click")]
        [DataRow("but_editRegion_Click")]
        [DataRow("btn_newModel_Click")]
        [DataRow("but_modifyModel_Click")]
        public void DrawEntry_Failure_PromptsReason_AndReleasesGate(string entry)
        {
            Run(ctx =>
            {
                ctx.Strategy.DrawError = new InvalidOperationException("绘制失败原因");

                Priv.Click(ctx.Para, entry);
                WindowHost.PumpUntil(() => !ctx.Para.IsDrawBusy);

                CollectionAssert.AreEqual(new[] { "绘制失败原因" }, ctx.Prompts.Messages);
            });
        }

        /// <summary> 没有对应能力的工具点到绘制按钮：什么也不做（按钮只是不在可见页上） </summary>
        [TestMethod]
        public void DrawEntries_ToolWithoutCapability_DoNothing()
        {
            Run(new MergeRegionStrategy(), ctx =>
            {
                foreach (var entry in new[] { "btn_drawRegion_Click", "but_editRegion_Click", "btn_newModel_Click", "but_modifyModel_Click", "but_editModel_Click" })
                    Priv.Click(ctx.Para, entry);
                WindowHost.PumpUntil(() => !ctx.Para.IsDrawBusy);

                CollectionAssert.AreEqual(new string[0], ctx.Prompts.Messages);
            });
        }

        #endregion

        #region 尚未选择工具

        private static readonly string[] AllHandlers =
        {
            "btn_drawRegion_Click", "but_editRegion_Click", "btn_newModel_Click", "but_modifyModel_Click", "but_editModel_Click",
        };

        /// <summary>宿主启动后并不会自动选中一个工具，参数页的按钮在此之前就能点。</summary>
        [TestMethod]
        public void Handlers_BeforeAnyToolSelected_DoNothing()
        {
            Run((IParaStrategy)null, ctx =>
            {
                foreach (var handler in AllHandlers)
                    Priv.Click(ctx.Para, handler);
                Priv.Call(ctx.Para, "DrawDoneEvent", ctx.Display, new DrawModelUIArgs("", null, null, default(ModelResult)));
                WindowHost.Pump();

                CollectionAssert.AreEqual(new string[0], ctx.Prompts.Messages);
                Assert.IsFalse(ctx.Para.IsDrawBusy);
                Assert.AreEqual(0, Priv.Get<TabControl>(ctx.Para, "tabControl1").TabPages.Count);
            });
        }

        #endregion

        #region 编辑模板

        private static Form EditModelWindow(Ctx ctx) => Priv.Get<Form>(ctx.Para, "_editModel");

        [TestMethod]
        public void EditModel_WhileDrawing_PromptsAndDoesNotOpen()
        {
            Run(ctx =>
            {
                Priv.Click(ctx.Para, "btn_newModel_Click");

                Priv.Click(ctx.Para, "but_editModel_Click");

                Assert.AreEqual(1, ctx.Prompts.Messages.Count);
                StringAssert.Contains(ctx.Prompts.Messages[0], "正在绘制");
                Assert.IsFalse(EditModelWindow(ctx).Visible);
                Finish(ctx);
            });
        }

        [TestMethod]
        public void EditModel_WhileEditWindowDrawing_PromptsAndDoesNotReload()
        {
            Run(ctx =>
            {
                Priv.Set(EditModelWindow(ctx), "_drawBusy", true);
                try { Priv.Click(ctx.Para, "but_editModel_Click"); }
                finally { Priv.Set(EditModelWindow(ctx), "_drawBusy", false); }

                Assert.AreEqual(1, ctx.Prompts.Messages.Count);
                StringAssert.Contains(ctx.Prompts.Messages[0], "模板编辑窗正在绘制");
            });
        }

        /// <summary>还没建过模板就点"编辑模板"，应提示先建模板（而不是"索引超出范围"）。</summary>
        [TestMethod]
        public void EditModel_NoTemplateYet_PromptsToCreateOne()
        {
            Run(ctx =>
            {
                Priv.Click(ctx.Para, "but_editModel_Click");

                Assert.AreEqual(1, ctx.Prompts.Messages.Count);
                StringAssert.Contains(ctx.Prompts.Messages[0], "新建模板");
                Assert.IsFalse(EditModelWindow(ctx).Visible);
            });
        }

        /// <summary>模板建过、但最近一次运行没匹配上，应提示先匹配成功：编辑窗要拿最佳匹配的位姿摆放模板区域。</summary>
        [TestMethod]
        public void EditModel_NoMatchResult_PromptsToRunFirst()
        {
            var fake = new FakeStrategy { View = new TemplateView(typeof(ParaFormTests).Assembly.Location, null, null, null) };
            Run(fake, ctx =>
            {
                Priv.Click(ctx.Para, "but_editModel_Click");

                Assert.AreEqual(1, ctx.Prompts.Messages.Count);
                StringAssert.Contains(ctx.Prompts.Messages[0], "匹配");
                Assert.IsFalse(EditModelWindow(ctx).Visible);
            });
        }

        /// <summary>模板图在、也有匹配结果：打开编辑窗，不提示。</summary>
        [TestMethod]
        public void EditModel_TemplateAndMatchResult_OpensEditWindow()
        {
            string modelPath = Path.Combine(Path.GetTempPath(), "VisionMasterTests_" + Guid.NewGuid().ToString("N") + ".png");
            HOperatorSet.GenImageConst(out HObject image, "byte", 64, 64);
            try { HOperatorSet.WriteImage(image, "png", 0, modelPath); }
            finally { image.Dispose(); }

            try
            {
                var fake = new FakeStrategy { View = new TemplateView(modelPath, null, null, new ModelResult(32, 32, 0, 1)) };
                Run(fake, ctx =>
                {
                    Priv.Click(ctx.Para, "but_editModel_Click");

                    CollectionAssert.AreEqual(new string[0], ctx.Prompts.Messages);
                    Assert.IsTrue(EditModelWindow(ctx).Visible);
                    EditModelWindow(ctx).Hide();
                });
            }
            finally { File.Delete(modelPath); }
        }

        [TestMethod]
        public void DrawDone_NonTemplateTool_Ignored()
        {
            Run(new CreateROIStrategy(), ctx =>
            {
                ctx.Display.DrawDone("", null, null, default(ModelResult));

                CollectionAssert.AreEqual(new string[0], ctx.Prompts.Messages);
            });
        }

        #endregion

        #region 编辑会话

        private static Button Btn(Ctx ctx, string name) => Priv.Get<Button>(ctx.Para, name);

        /// <summary> 通过显示页的复选框改"开关"，走真实的写回路径 </summary>
        private static void SetFlag(Ctx ctx, bool value)
        {
            var panel = ctx.Para.PanelOf(TabPageEnum.Display);
            ((CheckBox)panel.EditorOf(panel.Items.Single(i => i.Label == "开关"))).Checked = value;
        }

        private static bool FlagShown(Ctx ctx)
        {
            var panel = ctx.Para.PanelOf(TabPageEnum.Display);
            return ((CheckBox)panel.EditorOf(panel.Items.Single(i => i.Label == "开关"))).Checked;
        }

        [TestMethod]
        public void EditSession_Initially_CleanAndActionsDisabled()
        {
            Run(ctx =>
            {
                Assert.IsFalse(ctx.Para.IsDirty);
                Assert.IsFalse(Btn(ctx, "btn_cancelEdit").Enabled);
                Assert.IsFalse(Btn(ctx, "btn_saveEdit").Enabled);
                Assert.IsFalse(Btn(ctx, "btn_runTest").Enabled, "宿主没注入运行入口");
            });
        }

        /// <summary> 取消编辑：参数回到进入工具时，面板跟着刷新；不再通知策略（否则会清掉还原出来的示教态） </summary>
        [TestMethod]
        public void CancelEdit_RestoresParams_AndRebindsPanel()
        {
            Run(ctx =>
            {
                var before = ctx.Strategy.inPara;
                SetFlag(ctx, true);
                Assert.IsTrue(ctx.Para.IsDirty);
                Assert.IsTrue(Btn(ctx, "btn_cancelEdit").Enabled);

                Priv.Click(ctx.Para, "btn_cancelEdit_Click");

                Assert.IsFalse(ctx.Strategy.inPara.Flag);
                Assert.AreNotSame(before, ctx.Strategy.inPara);
                Assert.IsFalse(FlagShown(ctx), "面板绑定到还原后的参数实例");
                Assert.IsFalse(ctx.Para.IsDirty);
                Assert.IsFalse(Btn(ctx, "btn_cancelEdit").Enabled);
                CollectionAssert.AreEqual(new[] { "开关" }, ctx.Strategy.ChangedLabels);
                Assert.AreSame(ctx.Strategy, ctx.Para.Tool);
            });
        }

        [TestMethod]
        public void SaveEdit_MovesCancelPoint()
        {
            Run(ctx =>
            {
                SetFlag(ctx, true);
                Priv.Click(ctx.Para, "btn_saveEdit_Click");
                Assert.IsFalse(ctx.Para.IsDirty);

                SetFlag(ctx, false);
                Priv.Click(ctx.Para, "btn_cancelEdit_Click");

                Assert.IsTrue(ctx.Strategy.inPara.Flag, "回到保存时的值, 而不是进入工具时的值");
            });
        }

        /// <summary> 同一工具重新显示（改名）保留编辑起点；换了工具才开始新一轮 </summary>
        [TestMethod]
        public void ShowTool_OnlySwitchingToolStartsNewSession()
        {
            var main = new FakeStrategy();
            var other = new FakeStrategy("other");
            Run(main, ctx =>
            {
                SetFlag(ctx, true);
                ctx.Para.ShowTool(ctx.Strategy, new IParaStrategy[] { ctx.Strategy, other });
                Assert.IsTrue(ctx.Para.IsDirty);

                ctx.Para.ShowTool(other, new IParaStrategy[] { ctx.Strategy, other });
                ctx.Para.ShowTool(ctx.Strategy, new IParaStrategy[] { ctx.Strategy, other });

                Assert.IsFalse(ctx.Para.IsDirty);
                Assert.IsTrue(ctx.Strategy.inPara.Flag, "切走即保留修改");
            }, new IParaStrategy[] { main, other });
        }

        /// <summary> 绘制期间三个按钮都不可用；绘制结束（不论确认还是取消）都算改过 </summary>
        [TestMethod]
        public void Draw_BlocksActions_ThenMarksDirty()
        {
            Run(ctx =>
            {
                int runs = 0;
                ctx.Para.TestRunner = () => runs++;
                Assert.IsTrue(Btn(ctx, "btn_runTest").Enabled);

                Priv.Click(ctx.Para, "btn_drawRegion_Click");
                Assert.IsFalse(Btn(ctx, "btn_runTest").Enabled);
                ctx.Para.RunTest();
                Assert.AreEqual(0, runs);

                Finish(ctx);
                Assert.IsTrue(ctx.Para.IsDirty);
                Assert.IsTrue(Btn(ctx, "btn_cancelEdit").Enabled);
                Assert.IsTrue(Btn(ctx, "btn_runTest").Enabled);
            });
        }

        /// <summary> 模板绘制写盘、换模型句柄，撤销不了：结束后以当前状态为新的编辑起点，而不是标记为改过 </summary>
        [DataTestMethod]
        [DataRow("btn_newModel_Click")]
        [DataRow("but_modifyModel_Click")]
        public void TemplateDraw_MovesCancelPoint(string entry)
        {
            Run(ctx =>
            {
                SetFlag(ctx, true);
                Assert.IsTrue(ctx.Para.IsDirty);

                Priv.Click(ctx.Para, entry);
                Finish(ctx);

                Assert.IsFalse(ctx.Para.IsDirty);
                Assert.IsFalse(Btn(ctx, "btn_cancelEdit").Enabled);

                SetFlag(ctx, false);
                Priv.Click(ctx.Para, "btn_cancelEdit_Click");
                Assert.IsTrue(ctx.Strategy.inPara.Flag, "回到模板绘制结束时的值");
            });
        }

        [TestMethod]
        public void RunTest_CallsHostRunner_UnlessHostBusy()
        {
            Run(ctx =>
            {
                int runs = 0;
                ctx.Para.TestRunner = () => runs++;

                Priv.Click(ctx.Para, "btn_runTest_Click");
                Assert.AreEqual(1, runs);

                ctx.Para.HostBusy = true;
                SetFlag(ctx, true);
                Priv.Click(ctx.Para, "btn_runTest_Click");
                Priv.Click(ctx.Para, "btn_cancelEdit_Click");
                Assert.AreEqual(1, runs);
                Assert.IsTrue(ctx.Strategy.inPara.Flag, "宿主运行期间不允许取消编辑");

                ctx.Para.HostBusy = false;
                ctx.Para.TestRunner = () => throw new InvalidOperationException("运行出错");
                Priv.Click(ctx.Para, "btn_runTest_Click");
                CollectionAssert.AreEqual(new[] { "运行出错" }, ctx.Prompts.Messages);
            });
        }

        /// <summary> 还没选过工具：注入了运行入口也不可用 </summary>
        [TestMethod]
        public void EditSession_NoTool_ActionsDisabled()
        {
            Run(null, ctx =>
            {
                ctx.Para.TestRunner = () => { };
                Assert.IsFalse(Btn(ctx, "btn_runTest").Enabled);
                Assert.IsFalse(Btn(ctx, "btn_cancelEdit").Enabled);
            });
        }

        #endregion

        #region 释放

        private static IEnumerable<Delegate> Subscribers(HDisplayUI display, string eventName)
        {
            var handler = (Delegate)typeof(HDisplayUI)
                .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(display);
            return handler?.GetInvocationList() ?? Enumerable.Empty<Delegate>();
        }

        [TestMethod]
        public void Construct_SubscribesDisplayEvents()
        {
            Run(ctx =>
            {
                Assert.IsTrue(Subscribers(ctx.Display, nameof(HDisplayUI.DrawDoneEvent)).Any(d => d.Target == ctx.Para));
                Assert.IsTrue(Subscribers(ctx.Display, nameof(HDisplayUI.RoiShown)).Any(d => d.Target == ctx.Para));
            });
        }

        [TestMethod]
        public void Dispose_ReleasesDetachedForms_AndUnsubscribes()
        {
            Run(ctx =>
            {
                var valueForm = Priv.Get<Form>(ctx.Para, "_valueForm");
                var editModel = Priv.Get<Form>(ctx.Para, "_editModel");

                ctx.Para.Dispose();

                Assert.IsTrue(valueForm.IsDisposed, "_valueForm 没有父容器，只能由 ParaForm 释放");
                Assert.IsTrue(editModel.IsDisposed, "_editModel 没有父容器，只能由 ParaForm 释放");
                Assert.IsFalse(Subscribers(ctx.Display, nameof(HDisplayUI.DrawDoneEvent)).Any(d => d.Target == ctx.Para),
                    "不退订的话 HDisplayUI 会一直引着已销毁的 ParaForm");
                Assert.IsFalse(Subscribers(ctx.Display, nameof(HDisplayUI.RoiShown)).Any(d => d.Target == ctx.Para));
            });
        }

        /// <summary>
        /// 句柄重建（控件仍存活）时 HandleDestroyed 也会发，此时不能释放游离窗体。
        /// 直接在控件存活时调 handler，验证的正是它开头那道 <c>Disposing</c> 判据。
        /// </summary>
        [TestMethod]
        public void HandleDestroyed_WhileAlive_DoesNotReleaseDetachedForms()
        {
            Run(ctx =>
            {
                var valueForm = Priv.Get<Form>(ctx.Para, "_valueForm");
                var editModel = Priv.Get<Form>(ctx.Para, "_editModel");

                Priv.Click(ctx.Para, "ParaForm_HandleDestroyed", ctx.Para);

                Assert.IsFalse(valueForm.IsDisposed);
                Assert.IsFalse(editModel.IsDisposed);
                Assert.IsTrue(Subscribers(ctx.Display, nameof(HDisplayUI.DrawDoneEvent)).Any(d => d.Target == ctx.Para));
            });
        }

        #endregion
    }
}
