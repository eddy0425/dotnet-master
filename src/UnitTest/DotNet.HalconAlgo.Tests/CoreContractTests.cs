using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 程序集依赖边界：契约（Core）与内置算法（HalconAlgo）只依赖允许的程序集。
    /// 目录扫描 / 插件加载的测试在 DotNet.VisionRuntime.Tests。
    /// </summary>
    [TestClass]
    public class ArchitectureTests
    {
        private static readonly Assembly Core = typeof(IParaStrategy).Assembly;
        private static readonly Assembly BuiltIn = typeof(FileImageStrategy).Assembly;

        /// <summary> 契约层只依赖 BCL、几何与 HALCON、Newtonsoft；不认识 WinForms，也不认识宿主运行时 </summary>
        [TestMethod]
        public void Core_ReferencesOnlyBclDrawingHalconAndJson()
        {
            var allowed = new[] { "mscorlib", "System", "System.Core", "Microsoft.CSharp", "DotNet.Drawing", "halcondotnet", "Newtonsoft.Json" };
            var actual = Core.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
            CollectionAssert.DoesNotContain(actual, "System.Windows.Forms");
        }

        /// <summary> 架构守卫：内置算法与外置插件走同一条路 —— 只依赖契约、几何与 HALCON，不认识任何 UI </summary>
        [TestMethod]
        public void HalconAlgo_ReferencesOnlyContractAndHalcon()
        {
            var allowed = new[] { "mscorlib", "System", "System.Core", "DotNet.Drawing", "DotNet.HalconCore", "DotNet.HalconKit", "halcondotnet" };
            var actual = BuiltIn.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
        }

        /// <summary> 通用积木和插件站在同一层：只依赖契约、几何与 HALCON，不认识 WinForms，也不认识宿主运行时与内置算法 </summary>
        [TestMethod]
        public void HalconKit_ReferencesOnlyContractAndHalcon()
        {
            var allowed = new[] { "mscorlib", "System", "System.Core", "DotNet.Drawing", "DotNet.HalconCore", "halcondotnet" };
            var actual = typeof(DotNet.HalconKit.RoiEditing).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
        }

        /// <summary>
        /// 页名是字符串：拼错（多空格、大小写不同）会悄悄拆出一个新页签。内置算法里不允许出现"去掉空白、统一大小写后相同"的两个页名。
        /// </summary>
        [TestMethod]
        public void BuiltIn_PageNames_HaveNoNearDuplicates()
        {
            var pages = new List<string>();
            foreach (var type in BuiltIn.GetExportedTypes().Where(t => AlgoAttribute.Of(t) != null))
            {
                using (var tool = (IParaStrategy)Activator.CreateInstance(type))
                    pages.AddRange(((IParaBinding)tool).DescribeParams().Select(i => i.Page));
            }
            var distinct = pages.Distinct().ToList();
            var collisions = distinct.GroupBy(p => new string(p.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant())
                .Where(g => g.Count() > 1).Select(g => string.Join(" / ", g)).ToList();

            CollectionAssert.AreEqual(new string[0], collisions);
            CollectionAssert.IsSubsetOf(new[] { Pages.Parameter, Pages.Region, Pages.Display }, distinct);
        }

        /// <summary> 契约层与算法层没有可写的公开静态状态（原 AlgoPaths.ProjectDir / UIBlock） </summary>
        [TestMethod]
        public void CoreAndAlgo_HaveNoWritablePublicStatics()
        {
            var offenders = new List<string>();
            foreach (var type in Core.GetExportedTypes().Concat(BuiltIn.GetExportedTypes()))
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!field.IsInitOnly && !field.IsLiteral) offenders.Add(type.Name + "." + field.Name);
                }
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Static))
                {
                    if (prop.SetMethod != null && prop.SetMethod.IsPublic) offenders.Add(type.Name + "." + prop.Name);
                }
            }
            CollectionAssert.AreEqual(new string[0], offenders);
        }
    }

    [TestClass]
    public class ParamBuilderTests
    {
        private enum Mode { A, B }

        [TestMethod]
        public void DuplicateLabelOnSameTab_Throws()
        {
            var p = new ParamBuilder().Flag("x", () => true, v => { });
            Assert.ThrowsException<ArgumentException>(() => p.Flag("x", () => true, v => { }));
            p.Page(Pages.Display).Flag("x", () => true, v => { });   // 别的页可以同名
        }

        [TestMethod]
        public void Page_DefaultIsParameter_Trimmed_BlankThrows()
        {
            Assert.AreEqual(Pages.Parameter, new ParamBuilder().Flag("a", () => true, v => { }).Items[0].Page, "默认页");

            var p = new ParamBuilder().Page("  标定 ").Flag("a", () => true, v => { });
            Assert.AreEqual("标定", p.Items[0].Page, "首尾空格去掉, 免得拆出一个新页签");
            Assert.ThrowsException<ArgumentNullException>(() => p.Page(" "));
        }

        [TestMethod]
        public void Action_PressRunsAction_OtherValuesDoNot()
        {
            int pressed = 0;
            var item = (ActionParam)new ParamBuilder().Action("重新示教", () => pressed++).Items.Single();

            Assert.AreEqual(ParamKind.Action, item.Kind);
            Assert.IsNull(item.GetValue());
            Assert.IsFalse(item.TrySetValue(null));
            Assert.IsTrue(item.TrySetValue(ActionParam.Press), "按一次算一次改动, 宿主据此通知 ParamsChanged");
            Assert.IsTrue(item.TrySetValue(ActionParam.Press));
            Assert.AreEqual(2, pressed);
        }

        [TestMethod]
        public void SourceList_ComparesByContent()
        {
            var a = new SourceRef(Guid.NewGuid(), "区域");
            var b = new SourceRef(Guid.NewGuid(), "区域");
            IReadOnlyList<SourceRef> value = new List<SourceRef> { a };
            int writes = 0;
            var item = (SourceListParam)new ParamBuilder()
                .SourceList("输入", () => value, v => { value = v; writes++; }, OutEnum.Region, maxCount: 3).Items.Single();

            Assert.IsFalse(item.TrySetValue(new[] { a }), "内容相同的新实例不算改动");
            Assert.IsTrue(item.TrySetValue(new[] { a, b }));
            CollectionAssert.AreEqual(new[] { a, b }, value.ToArray());
            Assert.AreEqual(1, writes);
            Assert.AreEqual(3, item.MaxCount);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new ParamBuilder().SourceList("x", () => value, v => { }, OutEnum.Region, -1));
        }

        [TestMethod]
        public void SourceList_NullGetter_ReadsAsEmpty()
        {
            var item = (SourceListParam)new ParamBuilder().SourceList("输入", () => null, v => { }, OutEnum.Region).Items.Single();
            Assert.AreEqual(0, item.Value.Count);
            Assert.IsFalse(item.TrySetValue(new SourceRef[0]));
        }

        [TestMethod]
        public void TextAndFile_Kinds_DefaultFilter()
        {
            string text = "a", file = null;
            var items = new ParamBuilder()
                .Text("条码", () => text, v => text = v)
                .File("模型", () => file, v => file = v)
                .File("图像", () => file, v => file = v, "图像|*.bmp;*.png").Items;

            Assert.AreEqual(ParamKind.Text, items[0].Kind);
            Assert.AreEqual(ParamKind.File, items[1].Kind);
            Assert.AreEqual("所有文件|*.*", ((FileParam)items[1]).Filter);
            Assert.AreEqual("图像|*.bmp;*.png", ((FileParam)items[2]).Filter);
            Assert.IsTrue(items[0].TrySetValue(" b "));
            Assert.AreEqual(" b ", text, "文本原样保存");
        }

#pragma warning disable 618 // 验证旧写法的映射
        [TestMethod]
        public void Tab_Obsolete_MapsToPageNames()
        {
            var p = new ParamBuilder()
                .Tab(TabPageEnum.Parameter).Flag("a", () => true, v => { })
                .Tab(TabPageEnum.Region).Flag("b", () => true, v => { })
                .Tab(TabPageEnum.Display).Flag("c", () => true, v => { });
            CollectionAssert.AreEqual(new[] { Pages.Parameter, Pages.Region, Pages.Display }, p.Items.Select(i => i.Page).ToArray());
        }
#pragma warning restore 618

        [TestMethod]
        public void When_WithoutPrecedingItem_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => new ParamBuilder().When(() => true));
        }

        [TestMethod]
        public void Choice_UnknownValue_SelectedIndexIsMinusOne()
        {
            var mode = (Mode)7;
            var item = (ChoiceParam)new ParamBuilder()
                .Choice("模式", () => mode, v => mode = v, Option.Of(Mode.A, "甲"), Option.Of(Mode.B, "乙")).Items[0];

            Assert.AreEqual(-1, item.SelectedIndex, "旧配置写了非法值时不假装选中某一项");
            Assert.IsTrue(item.TrySetValue(Mode.B));
            Assert.AreEqual(1, item.SelectedIndex);
        }

        [TestMethod]
        public void TrySetValue_SameValue_DoesNotCallSetter()
        {
            int calls = 0, value = 5;
            var item = new ParamBuilder().Int("n", () => value, v => { value = v; calls++; }).Items[0];

            Assert.IsFalse(item.TrySetValue(5));
            Assert.AreEqual(0, calls);
            Assert.IsTrue(item.TrySetValue(6));
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public void Number_ParsesAndChecksRange()
        {
            double value = 0;
            var item = (NumberParam)new ParamBuilder().Double("d", () => value, v => value = v, min: 0, max: 1).Items[0];

            Assert.IsTrue(item.TryParse(" 0.25 ", out object parsed, out _));
            Assert.AreEqual(0.25, parsed);
            Assert.IsFalse(item.TryParse("2", out _, out string error));
            StringAssert.Contains(error, "不能大于");
            Assert.IsFalse(item.TryParse("NaN", out _, out _));
            Assert.IsFalse(item.TryParse("", out _, out _));
        }

        [TestMethod]
        public void Source_AllowsLocalOnlyForTypesWithLocalMeaning()
        {
            SourceRef s = SourceRef.Local;
            var b = new ParamBuilder()
                .Source("图像", () => s, v => s = v, OutEnum.Image)
                .Source("直线", () => s, v => s = v, OutEnum.Line);
            Assert.IsTrue(((SourceParam)b.Items[0]).AllowsLocal);
            Assert.IsFalse(((SourceParam)b.Items[1]).AllowsLocal);
        }
    }

    [TestClass]
    public class OutputAndContextTests : HalconTestBase
    {
        [TestMethod]
        public void Outputs_ChildrenDerivedFromOneDeclaration()
        {
            var stub = new StubStrategy("S")
                .Point("点", () => new Point2d(3, 4))
                .Line("线", () => new CvLine(1, 2, 3, 4))
                .CoordOut("坐标系", () => new CvCoord(5, 6), () => new Point2d(7, 8));

            Assert.AreEqual(4.0, stub.FindOutput("点/行").GetValue());
            Assert.AreEqual(3.0, stub.FindOutput("点/列").GetValue());
            Assert.AreEqual(new Point2d(3, 4), stub.FindOutput("线/终点").GetValue());
            Assert.AreEqual(2.0, stub.FindOutput("线/起点/行").GetValue());
            Assert.AreEqual(new Point2d(5, 6), stub.FindOutput("坐标系/原点").GetValue());
            Assert.IsTrue(stub.FindOutput("坐标系").TryGetTemplate(out Point2d tmpl));
            Assert.AreEqual(new Point2d(7, 8), tmpl);
            Assert.IsNotNull(stub.FindOutput("结果"), "基类追加的公共输出");
            Assert.IsNotNull(stub.FindOutput("文本显示"));
        }

        /// <summary> 新的输出类型：圆 / 文本 / 开关 / 数组 / 自定义类型；每个输出都带 CLR 类型 </summary>
        [TestMethod]
        public void Outputs_NewKinds_CarryValueTypes()
        {
            var o = new OutputBuilder()
                .Circle("圆", () => new CvCircle(new Point2d(3, 4), 5))
                .Text("条码", () => "ABC")
                .Flag("合格", () => true)
                .Numbers("得分", () => new[] { 0.9, 0.8 })
                .Value("标定", () => new Calib { Scale = 2 }, OutEnum.HTuple);
            var index = o.Roots.ToDictionary(r => r.Path);

            Assert.AreEqual(OutEnum.Circle, index["圆"].Type);
            Assert.AreEqual(typeof(CvCircle), index["圆"].ValueType);
            CollectionAssert.AreEqual(new[] { "圆/圆心", "圆/半径" }, index["圆"].Children.Select(c => c.Path).ToArray());
            Assert.AreEqual(4.0, index["圆"].Children[0].Children[0].GetValue(), "圆心/行");
            Assert.AreEqual(5.0, index["圆"].Children[1].GetValue());
            Assert.AreEqual(typeof(string), index["条码"].ValueType);
            Assert.AreEqual(OutEnum.Result, index["合格"].Type);
            Assert.AreEqual(OutEnum.Array, index["得分"].Type);
            Assert.AreEqual(typeof(IReadOnlyList<double>), index["得分"].ValueType);
            Assert.AreEqual(typeof(Calib), index["标定"].ValueType);
            Assert.AreEqual(OutEnum.HTuple, index["标定"].Type, "种类只决定图标");
        }

        public class Calib { public double Scale; }
        public sealed class FineCalib : Calib { }

        [TestMethod]
        public void Compatibility_KindStillDistinguishesImageFromRegion()
        {
            var o = new OutputBuilder().Image("图像", () => null).Region("区域", () => null).Number("数", () => 1).Roots;

            Assert.IsTrue(SourceCompatibility.Accepts(OutEnum.Region, null, o[1]));
            Assert.IsFalse(SourceCompatibility.Accepts(OutEnum.Region, null, o[0]), "图像和区域都是 HObject, 只看类型会接错");
            Assert.IsTrue(SourceCompatibility.Accepts(OutEnum.String, null, o[2]), "文本来源接受标量");
            Assert.IsFalse(SourceCompatibility.Accepts(OutEnum.String, null, o[0]));
            Assert.IsTrue(SourceCompatibility.Accepts(OutEnum.CalOrOut, null, o[2]));
        }

        [TestMethod]
        public void Compatibility_TypedSource_UsesAssignability()
        {
            var o = new OutputBuilder()
                .Value("标定", () => new FineCalib())
                .Value("别的", () => "x")
                .Roots;
            SourceRef value = SourceRef.Local;
            var source = (SourceParam)new ParamBuilder().Source<Calib>("标定来源", () => value, v => value = v).Items.Single();

            Assert.AreEqual(OutEnum.Undefined, source.SourceType);
            Assert.AreEqual(typeof(Calib), source.ValueType);
            Assert.IsFalse(source.AllowsLocal);
            Assert.IsTrue(source.Accepts(o[0]), "子类型可以赋给来源要求的类型");
            Assert.IsFalse(source.Accepts(o[1]));
        }

        [TestMethod]
        public void Outputs_InvalidNames_Throw()
        {
            Assert.ThrowsException<ArgumentException>(() => new StubStrategy("S").Number("a/b", () => 0).Outputs.Count);
            Assert.ThrowsException<ArgumentException>(() => new StubStrategy("S").Number("a", () => 0).Number("a", () => 1).Outputs.Count);
        }

        [TestMethod]
        public void SourceRef_LocalAndJsonRoundTrip()
        {
            Assert.IsTrue(SourceRef.Local.IsLocal);
            Assert.AreEqual(SourceRef.Local, new SourceRef(Guid.Empty, "被忽略"), "本地引用不带输出路径");

            var r = new SourceRef(Guid.NewGuid(), "坐标系/原点");
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<SourceRef>(Newtonsoft.Json.JsonConvert.SerializeObject(r));
            Assert.AreEqual(r, back);
            Assert.AreEqual(r.GetHashCode(), back.GetHashCode());
        }

        [TestMethod]
        public void Describe_UsesCurrentNames_AndFlagsDanglingRefs()
        {
            var stub = new StubStrategy("定位").CoordOut("坐标系", () => new CvCoord(), () => null);
            var tools = new IParaStrategy[] { stub };
            var r = stub.Ref("坐标系");

            Assert.AreEqual("定位/坐标系", tools.Describe(r));
            stub.Name = "改名";
            Assert.AreEqual("改名/坐标系", tools.Describe(r), "引用按 Id 保存, 改名不断开");
            Assert.AreEqual("默认", tools.Describe(SourceRef.Local));
            StringAssert.StartsWith(new IParaStrategy[0].Describe(r), "<已失效>");
        }

        [TestMethod]
        public void RunContext_OnlySeesUpstream()
        {
            using (var region = Rectangle1(0, 0, 9, 9))
            {
                var a = new StubStrategy("A").Region("区域", () => region);
                var ctx = new RunContext(null, new IParaStrategy[0]);

                Assert.IsFalse(ctx.TryResolveRegion(a.Ref("区域"), out _), "不在上游里的工具一律按找不到处理");
                Assert.ThrowsException<AlgoOutputNotFoundException>(() => ctx.ResolveRegion(a.Ref("区域"), null));
                Assert.ThrowsException<AlgoOutputNotFoundException>(() => new RunContext(null, new IParaStrategy[] { a }).Resolve<CvCoord>(a.Ref("区域")),
                    "类型不匹配");
            }
        }

        [TestMethod]
        public void RunContext_LocalSources()
        {
            var ctx = new RunContext(null);
            var ex = Assert.ThrowsException<InvalidOperationException>(() => ctx.ResolveImage(SourceRef.Local));
            StringAssert.Contains(ex.Message, "图像来源为空");
            ex = Assert.ThrowsException<InvalidOperationException>(() => ctx.ResolveRegion(SourceRef.Local, new CvRegion()));
            StringAssert.Contains(ex.Message, "尚未绘制 ROI");
            Assert.IsFalse(ctx.ResolveCoord(SourceRef.Local).IsActive, "本地坐标系 = 恒等跟随");
        }

        [TestMethod]
        public void CoordFollow_RotatesAroundTemplateThenMoves()
        {
            var follow = new CoordFollow(new Point2d(100, 100), CvCoord.FromDegrees(130, 120, 90));

            var p = follow.TransPoint(new Point2d(110, 100));   // 模板原点右侧 10
            // TransPoint 走 affine_trans_pixel（像素中心约定），旋转时会偏出最多 1px，与 CreateROI 的既有说明一致
            Assert.AreEqual(130, p.X, 1.0);
            Assert.AreEqual(110, p.Y, 1.0, "逆时针 90°（行向下）后落到新原点正下方");
            Assert.AreEqual(90, follow.TransAngle(Angle.Zero).Degrees, 1e-9);

            Assert.AreEqual(new Point2d(1, 2), CoordFollow.None.TransPoint(new Point2d(1, 2)));
        }
    }

    /// <summary> 基类的执行模板：失败转结果、失败后输出复位、状态文本规则、取消与释放 </summary>
    [TestClass]
    public class StrategyBaseTests : HalconTestBase
    {
        internal sealed class ProbePara : DisplayOptions { }

        internal sealed class Probe : ParaStrategyBase<ProbePara>
        {
            public Func<RunContext, RunResult> Body = c => RunResult.Ok("完成");
            public int Value;
            public int Resets;

            protected override void DeclareParams(ParamBuilder p) { }
            protected override void DeclareOutputs(OutputBuilder o) => o.Number("值", () => Value);
            protected override void ResetOutputs() { Value = 0; Resets++; }
            protected override RunResult Execute(RunContext context)
            {
                Value = 42;
                return Body(context);
            }
        }

        [TestMethod]
        public void Exception_BecomesFail_OutputsReset_Logged()
        {
            var probe = new Probe { Body = c => throw new InvalidOperationException("坏了") };
            var display = new FakeDisplay();

            using (var log = new CapturingLogger())
            {
                var result = probe.Run(new RunContext(null), display);

                Assert.AreEqual(RunStatus.Error, result.Status);
                Assert.AreEqual("坏了", result.Message);
                Assert.AreEqual(0, probe.Value, "失败后输出必须是默认值");
                Assert.AreEqual(1, log.Messages(LogLevel.Warn).Count());
            }
            Assert.AreEqual("Probe : 坏了", display.LastText);
            Assert.AreEqual(HColor.Red.Name, display.Texts[0].ColorName);
            Assert.AreSame(probe.LastResult, probe.LastResult);
        }

        [TestMethod]
        public void ReturnedFail_AlsoResetsOutputs()
        {
            var probe = new Probe { Body = c => RunResult.Fail("没找到") };
            Assert.AreEqual(RunStatus.Error, probe.Run(new RunContext(null), null).Status);
            Assert.AreEqual(0, probe.Value);
            Assert.AreEqual(2, probe.Resets, "开头一次 + 失败后一次");
        }

        [TestMethod]
        public void StatusText_OkOnlyWhenDispText_WarnAlways()
        {
            var probe = new Probe();
            probe.inPara.DispText = false;
            var display = new FakeDisplay();

            Assert.IsTrue(probe.Run(new RunContext(null), display).IsOk);
            Assert.AreEqual(0, display.Texts.Count, "成功文本受显示开关控制");

            probe.Body = c => RunResult.Warn("降级");
            probe.Run(new RunContext(null), display);
            Assert.AreEqual("Probe : 降级", display.LastText, "警告 / 失败始终显示");
            Assert.AreEqual(HColor.Red.Name, display.Texts[0].ColorName);
        }

        [TestMethod]
        public void Elapsed_IsMeasured_ResultExposedAsOutput()
        {
            var probe = new Probe { Body = c => { Thread.Sleep(20); return RunResult.Ok("完成"); } };
            var result = probe.Run(new RunContext(null), null);

            Assert.IsTrue(result.Elapsed >= TimeSpan.FromMilliseconds(15));
            Assert.AreEqual(true, probe.FindOutput("结果").GetValue());
            Assert.AreEqual("完成", probe.FindOutput("文本显示").GetValue());
        }

        [TestMethod]
        public void Cancellation_Propagates()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            var probe = new Probe();

            Assert.ThrowsException<OperationCanceledException>(() => probe.Run(new RunContext(null, null, cts.Token), null));
            Assert.AreEqual(0, probe.Value);
        }

        [TestMethod]
        public void Disposed_RunThrows_DisposeIdempotent()
        {
            var probe = new Probe();
            probe.Dispose();
            probe.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => probe.Run(new RunContext(null), null));
        }

        [TestMethod]
        public void DisplayParams_AppendedByBase()
        {
            CollectionAssert.AreEqual(new[] { "显示文本", "文本X", "文本Y", "字号" },
                new Probe().DescribeParams().Where(i => i.Page == Pages.Display).Select(i => i.Label).ToArray());
        }

        [TestMethod]
        public void Name_DefaultsToTypeNameWithoutAttribute_DataDirFollowsId()
        {
            var probe = new Probe();
            Assert.AreEqual(nameof(Probe), probe.Name);
            StringAssert.EndsWith(probe.DataDir, probe.Id.ToString("N"));
            probe.DataDir = @"X:\scheme\tool";
            Assert.AreEqual(@"X:\scheme\tool", probe.DataDir);
        }
    }
}
