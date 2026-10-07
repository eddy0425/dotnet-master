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
    /// 插件契约：目录扫描、启动校验、稳定键、外置插件、程序集依赖边界。
    /// </summary>
    [TestClass]
    public class AlgoCatalogTests : HalconTestBase
    {
        private static readonly Assembly BuiltIn = typeof(FileImageStrategy).Assembly;
        private string _dir;

        [TestInitialize]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "DotNet.HalconAlgo.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void TearDown()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        /// <summary> 样例插件的编译输出（只引用 Drawing + HalconCore） </summary>
        internal static string SamplePluginDll()
        {
            string config = Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
            string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "DotNet.SamplePlugin", "bin", config, "DotNet.SamplePlugin.dll"));
            Assert.IsTrue(File.Exists(path), "前提：样例插件已编译 " + path);
            return path;
        }

        [TestMethod]
        public void BuiltIn_AllElevenAlgorithms_WithStableKeys()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn });

            CollectionAssert.AreEquivalent(new[]
            {
                "image.file", "image.rotate", "image.line-rotate",
                "region.create-roi", "region.merge",
                "fit.line", "fit.arc-midpoint",
                "match.shape", "match.ncc", "match.scaled", "match.generic",
            }, catalog.Algorithms.Select(a => a.Key).ToArray());
            Assert.AreEqual(typeof(FitArcMidpointStrategy), catalog.Find("fit.arc-midpoint").Type);
            Assert.AreEqual("测量", catalog.Find("fit.arc-midpoint").Group);
            Assert.IsNull(catalog.Find("不存在"));
        }

        [TestMethod]
        public void Create_SetsDisplayNameAndFreshId()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn });

            using (var a = catalog.Create("fit.line"))
            using (var b = catalog.Create("fit.line"))
            {
                Assert.IsInstanceOfType(a, typeof(FitLineStrategy));
                Assert.AreEqual("拟合直线", a.Name);
                Assert.AreNotEqual(Guid.Empty, a.Id);
                Assert.AreNotEqual(a.Id, b.Id, "每个实例一个 Id");
            }
            Assert.ThrowsException<KeyNotFoundException>(() => catalog.Create("不存在"));
        }

        /// <summary> 校验失败一次列出全部问题，而不是修一个报一个 </summary>
        [TestMethod]
        public void Load_InvalidTypes_ReportsAllProblems()
        {
            var ex = Assert.ThrowsException<AlgoCatalogException>(() => AlgoCatalog.Load(new[] { typeof(BadAlgos.Duplicate1).Assembly }));

            string all = string.Join("\n", ex.Problems);
            StringAssert.Contains(all, "bad.dup");
            StringAssert.Contains(all, "重复");
            StringAssert.Contains(all, nameof(BadAlgos.AbstractAlgo));
            StringAssert.Contains(all, nameof(BadAlgos.NoDefaultCtor));
            StringAssert.Contains(all, nameof(BadAlgos.NotAStrategy));
            StringAssert.Contains(all, "键为空");
            Assert.IsTrue(ex.Problems.Count >= 5);
        }

        [TestMethod]
        public void Plugin_LoadedFromPluginDir_CreatesAndRuns()
        {
            File.Copy(SamplePluginDll(), Path.Combine(_dir, "DotNet.SamplePlugin.dll"));

            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, _dir);

            var info = catalog.Find("sample.region-area");
            Assert.IsNotNull(info, "插件目录里的算法出现在目录里");
            Assert.AreEqual("区域面积", info.DisplayName);
            Assert.AreEqual(12, catalog.Algorithms.Count);

            using (var region = Rectangle1(0, 0, 9, 9))
            using (var tool = catalog.Create("sample.region-area"))
            {
                var upstream = new StubStrategy("上游").Region("区域", () => region);
                var source = (SourceParam)((IParaBinding)tool).DescribeParams().Single(p => p.Label == "区域来源");
                Assert.IsTrue(source.TrySetValue(upstream.Ref("区域")));

                var result = tool.Run(new RunContext(null, new IParaStrategy[] { upstream }), null);

                Assert.IsTrue(result.IsOk, result.Message);
                Assert.AreEqual(100.0, tool.FindOutput("面积").GetValue());
            }
        }

        [TestMethod]
        public void Plugin_DirWithSharedAssemblyCopy_Rejected()
        {
            File.Copy(typeof(AlgoCatalog).Assembly.Location, Path.Combine(_dir, "DotNet.HalconCore.dll"));

            var ex = Assert.ThrowsException<AlgoCatalogException>(() => AlgoCatalog.Load(new[] { BuiltIn }, _dir));

            StringAssert.Contains(ex.Message, "DotNet.HalconCore.dll");
            StringAssert.Contains(ex.Message, "副本");
        }

        [TestMethod]
        public void Plugin_NotADotNetAssembly_Rejected()
        {
            File.WriteAllBytes(Path.Combine(_dir, "native.dll"), new byte[] { 0x4D, 0x5A, 0, 0 });

            var ex = Assert.ThrowsException<AlgoCatalogException>(() => AlgoCatalog.Load(new[] { BuiltIn }, _dir));

            StringAssert.Contains(ex.Message, "native.dll");
        }

        [TestMethod]
        public void Plugin_MissingDir_OnlyBuiltIn()
        {
            var catalog = AlgoCatalog.Load(new[] { BuiltIn }, Path.Combine(_dir, "none"));
            Assert.AreEqual(11, catalog.Algorithms.Count);
        }

        /// <summary> 架构守卫：内置算法与外置插件走同一条路 —— 只依赖契约、几何与 HALCON，不认识任何 UI </summary>
        [TestMethod]
        public void HalconAlgo_ReferencesOnlyContractAndHalcon()
        {
            var allowed = new[] { "mscorlib", "System", "System.Core", "DotNet.Drawing", "DotNet.HalconCore", "halcondotnet" };
            var actual = BuiltIn.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
        }

        /// <summary> 契约层与算法层没有可写的公开静态状态（原 AlgoPaths.ProjectDir / UIBlock） </summary>
        [TestMethod]
        public void CoreAndAlgo_HaveNoWritablePublicStatics()
        {
            var offenders = new List<string>();
            foreach (var type in typeof(AlgoCatalog).Assembly.GetExportedTypes().Concat(BuiltIn.GetExportedTypes()))
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
            p.Tab(TabPageEnum.Display).Flag("x", () => true, v => { });   // 别的页可以同名
        }

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
                new Probe().DescribeParams().Where(i => i.Tab == TabPageEnum.Display).Select(i => i.Label).ToArray());
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

namespace DotNet.HalconAlgo.Tests.BadAlgos
{
    // 只在 Load_InvalidTypes_ReportsAllProblems 里被显式扫描: 目录不扫整个 AppDomain, 平时不会被登记

    public sealed class BadPara : DisplayOptions { }

    public abstract class BadBase : ParaStrategyBase<BadPara>
    {
        protected override void DeclareParams(ParamBuilder p) { }
        protected override void DeclareOutputs(OutputBuilder o) { }
        protected override void ResetOutputs() { }
        protected override RunResult Execute(RunContext context) => RunResult.Ok();
    }

    [Algo("bad.dup", "重复1")]
    public sealed class Duplicate1 : BadBase { }

    [Algo("bad.dup", "重复2")]
    public sealed class Duplicate2 : BadBase { }

    [Algo("bad.abstract", "抽象")]
    public abstract class AbstractAlgo : BadBase { }

    [Algo("bad.ctor", "无参构造缺失")]
    public sealed class NoDefaultCtor : BadBase
    {
        public NoDefaultCtor(int x) { }
    }

    [Algo("bad.not-strategy", "不是策略")]
    public sealed class NotAStrategy { }

    [Algo(" ", "空键")]
    public sealed class EmptyKey : BadBase { }
}
