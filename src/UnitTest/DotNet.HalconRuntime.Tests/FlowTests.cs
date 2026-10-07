using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace DotNet.HalconRuntime.Tests
{
    /// <summary>
    /// <see cref="FlowRunner"/>：顺序执行、只给上游、当前图像随图像工具前进、失败策略、运行前校验。
    /// </summary>
    [TestClass]
    public class FlowRunnerTests : HalconTestBase
    {
        private sealed class StepPara : DisplayOptions
        {
            public SourceRef Input { get; set; } = SourceRef.Local;
        }

        /// <summary> 记录自己看到的上游与当前图像；可选地产出图像 </summary>
        private sealed class Step : ParaStrategyBase<StepPara>, IImageProducer
        {
            public RunStatus Outcome = RunStatus.Ok;
            public IReadOnlyList<IParaStrategy> SeenUpstream;
            public HObject SeenImage;
            public HObject Produce;
            public OutEnum InputType = OutEnum.Image;
            public int Runs;

            public Step(string name) { Name = name; }

            public HObject Image => Produce;

            protected override void DeclareParams(ParamBuilder p)
                => p.Source("输入", () => inPara.Input, v => inPara.Input = v, InputType);

            protected override void DeclareOutputs(OutputBuilder o)
            {
                o.Image("图像", () => Produce).Number("次数", () => Runs);
            }

            protected override void ResetOutputs() { }

            protected override RunResult Execute(RunContext context)
            {
                Runs++;
                SeenUpstream = context.Upstream;
                SeenImage = context.CurrentImage;
                switch (Outcome)
                {
                    case RunStatus.Error: return RunResult.Fail(Name + " 失败");
                    case RunStatus.Warning: return RunResult.Warn(Name + " 警告");
                    default: return RunResult.Ok();
                }
            }
        }

        private HObject _initial, _produced;

        [TestInitialize]
        public void SetUp()
        {
            _initial = ConstImage(10, 10, 1);
            _produced = ConstImage(10, 10, 2);
        }

        [TestCleanup]
        public void TearDown()
        {
            _initial.Dispose();
            _produced.Dispose();
        }

        [TestMethod]
        public void Run_EachToolSeesOnlyUpstream()
        {
            var a = new Step("A");
            var b = new Step("B");
            var c = new Step("C");
            var result = new FlowRunner(new IParaStrategy[] { a, b, c }).Run(_initial);

            Assert.IsTrue(result.AllOk);
            Assert.AreEqual(3, result.Steps.Count);
            Assert.AreEqual(0, a.SeenUpstream.Count);
            CollectionAssert.AreEqual(new[] { a }, b.SeenUpstream.ToArray());
            CollectionAssert.AreEqual(new IParaStrategy[] { a, b }, c.SeenUpstream.ToArray(), "不含自己, 也不含下游");
        }

        [TestMethod]
        public void Run_CurrentImageFollowsImageProducers()
        {
            var a = new Step("A");
            var producer = new Step("取像") { Produce = _produced };
            var c = new Step("C");

            new FlowRunner(new IParaStrategy[] { a, producer, c }).Run(_initial);

            Assert.AreSame(_initial, a.SeenImage);
            Assert.AreSame(_produced, c.SeenImage, "其后工具的本地图像取图像工具的输出");
        }

        [TestMethod]
        public void Run_FailedProducer_DoesNotAdvanceCurrentImage()
        {
            var producer = new Step("取像") { Produce = _produced, Outcome = RunStatus.Error };
            var c = new Step("C");

            new FlowRunner(new IParaStrategy[] { producer, c }) { OnFailure = FlowFailurePolicy.Continue }.Run(_initial);

            Assert.AreSame(_initial, c.SeenImage);
        }

        [TestMethod]
        public void Run_StopPolicy_StopsAtFirstError()
        {
            var a = new Step("A");
            var b = new Step("B") { Outcome = RunStatus.Error };
            var c = new Step("C");

            var result = new FlowRunner(new IParaStrategy[] { a, b, c }).Run(_initial);

            Assert.IsTrue(result.Stopped);
            Assert.AreEqual(2, result.Steps.Count);
            Assert.AreSame(b, result.FirstError.Tool);
            Assert.AreEqual("B 失败", result.FirstError.Result.Message);
            Assert.AreEqual(0, c.Runs);
        }

        [TestMethod]
        public void Run_ContinuePolicy_RunsEverything_WarningsDoNotStop()
        {
            var a = new Step("A") { Outcome = RunStatus.Warning };
            var b = new Step("B") { Outcome = RunStatus.Error };
            var c = new Step("C");

            var result = new FlowRunner(new IParaStrategy[] { a, b, c }) { OnFailure = FlowFailurePolicy.Continue }.Run(_initial);

            Assert.IsFalse(result.Stopped);
            Assert.AreEqual(3, result.Steps.Count);
            Assert.IsFalse(result.AllOk);
            Assert.AreEqual(1, c.Runs);
        }

        [TestMethod]
        public void Run_FromMiddle_UsesPreviousProducerOutput()
        {
            var producer = new Step("取像") { Produce = _produced };
            var b = new Step("B");
            var c = new Step("C");
            var runner = new FlowRunner(new IParaStrategy[] { producer, b, c });

            var result = runner.Run(_initial, from: 1);

            Assert.AreEqual(0, producer.Runs, "从中间开始时前面的工具不重新执行");
            Assert.AreEqual(2, result.Steps.Count);
            Assert.AreSame(_produced, b.SeenImage, "沿用前面图像工具上一轮的输出");
        }

        [TestMethod]
        public void RunStep_RunsOnlyOneTool()
        {
            var a = new Step("A");
            var b = new Step("B");
            var step = new FlowRunner(new IParaStrategy[] { a, b }).RunStep(1, _initial);

            Assert.AreSame(b, step.Tool);
            Assert.AreEqual(0, a.Runs);
            Assert.AreEqual(1, b.Runs);
        }

        [TestMethod]
        public void Validate_FlagsMissingDownstreamAndMismatchedSources()
        {
            var a = new Step("A");
            var later = new Step("后面");
            var gone = new Step("已删除");
            var b = new Step("B");
            var c = new Step("C");
            var d = new Step("D") { InputType = OutEnum.Region };
            var e = new Step("E");
            b.inPara.Input = gone.Ref("图像");        // 引用的工具不存在
            c.inPara.Input = later.Ref("图像");       // 引用排在后面的工具
            d.inPara.Input = a.Ref("图像");           // 类型不匹配: 要区域, 给的是图像
            e.inPara.Input = a.Ref("不存在");          // 输出不存在

            var issues = new FlowRunner(new IParaStrategy[] { a, b, c, d, e, later }).Validate();

            Assert.AreEqual(4, issues.Count);
            StringAssert.Contains(issues.Single(i => i.Tool == b).Message, "不存在");
            StringAssert.Contains(issues.Single(i => i.Tool == c).Message, "之前");
            StringAssert.Contains(issues.Single(i => i.Tool == d).Message, "类型");
            StringAssert.Contains(issues.Single(i => i.Tool == e).Message, "没有输出");
            Assert.AreEqual("输入", issues[0].Label);
        }

        private sealed class ListPara : DisplayOptions
        {
            public List<SourceRef> Inputs { get; set; } = new List<SourceRef>();
        }

        private sealed class ListStep : ParaStrategyBase<ListPara>
        {
            public ListStep(string name) { Name = name; }
            protected override void DeclareParams(ParamBuilder p)
                => p.SourceList("输入", () => inPara.Inputs, v => inPara.Inputs = v.ToList(), OutEnum.Image);
            protected override void DeclareOutputs(OutputBuilder o) { }
            protected override void ResetOutputs() { }
            protected override RunResult Execute(RunContext context) => RunResult.Ok();
        }

        /// <summary> 一个"插件"输出的自定义类型 </summary>
        public sealed class Gauge
        {
            public double Value;
        }

        private sealed class GaugeStep : ParaStrategyBase<StepPara>
        {
            public GaugeStep(string name) { Name = name; }
            protected override void DeclareParams(ParamBuilder p) { }
            protected override void DeclareOutputs(OutputBuilder o) => o.Value("量规", () => new Gauge { Value = 42 });
            protected override void ResetOutputs() { }
            protected override RunResult Execute(RunContext context) => RunResult.Ok();
        }

        /// <summary> 另一个"插件"按 CLR 类型声明来源，接收上面的自定义类型 </summary>
        private sealed class GaugeReader : ParaStrategyBase<StepPara>
        {
            public double Read;
            public GaugeReader(string name) { Name = name; }
            protected override void DeclareParams(ParamBuilder p) => p.Source<Gauge>("量规来源", () => inPara.Input, v => inPara.Input = v);
            protected override void DeclareOutputs(OutputBuilder o) { }
            protected override void ResetOutputs() => Read = 0;
            protected override RunResult Execute(RunContext context)
            {
                Read = context.Resolve<Gauge>(inPara.Input).Value;
                return RunResult.Ok();
            }
        }

        /// <summary> 插件之间可以传自定义类型：按 CLR 类型的可赋值关系校验，种类只决定图标 </summary>
        [TestMethod]
        public void CustomType_FlowsBetweenPlugins_ValidatedByValueType()
        {
            var gauge = new GaugeStep("量规");
            var reader = new GaugeReader("读取");
            reader.inPara.Input = gauge.Ref("量规");
            var runner = new FlowRunner(new IParaStrategy[] { gauge, reader });

            Assert.AreEqual(0, runner.Validate().Count);
            using (var result = runner.Run(_initial))
            {
                Assert.IsTrue(result.AllOk, result.FirstError?.Result.Message);
                Assert.AreEqual(42, reader.Read);
            }

            var a = new Step("A");
            reader.inPara.Input = a.Ref("次数");
            var issues = new FlowRunner(new IParaStrategy[] { a, reader }).Validate();
            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains(issues[0].Message, "需要 Gauge");
        }

        /// <summary> 来源列表里的每一项都是一条输入依赖，逐项校验 </summary>
        [TestMethod]
        public void Validate_SourceList_ChecksEachEntry()
        {
            var a = new Step("A");
            var list = new ListStep("L");
            var c = new Step("C");
            list.inPara.Inputs.Add(a.Ref("图像"));
            list.inPara.Inputs.Add(c.Ref("图像"));      // 下游
            list.inPara.Inputs.Add(a.Ref("次数"));      // 类型不对

            var issues = new FlowRunner(new IParaStrategy[] { a, list, c }).Validate();

            CollectionAssert.AreEqual(new[] { "输入[1]", "输入[2]" }, issues.Select(i => i.Label).ToArray());
            StringAssert.Contains(issues[0].Message, "必须排在本工具之前");
            StringAssert.Contains(issues[1].Message, "需要 Image");
        }

        [TestMethod]
        public void Validate_LocalAndValidSources_NoIssues()
        {
            var a = new Step("A");
            var b = new Step("B");
            b.inPara.Input = a.Ref("图像");
            Assert.AreEqual(0, new FlowRunner(new IParaStrategy[] { a, b, new Step("C") }).Validate().Count);
        }

        /// <summary> 端到端：创建ROI → 合并区域（跟随 + 引用 ROI 的区域输出），按 Id 串起来 </summary>
        [TestMethod]
        public void Run_RealTools_ChainedById()
        {
            using (var roi = new CreateROIStrategy())
            using (var merge = new MergeRegionStrategy())
            {
                roi.inPara.HoRect.Dispose();
                roi.inPara.HoRect = NewRegion(RectEnum.Rectangle, 10, 10, 20, 20);
                merge.inPara.RegionSources.Add(roi.Ref("区域"));

                var result = new FlowRunner(new IParaStrategy[] { roi, merge }).Run(_initial);

                Assert.IsTrue(result.AllOk, string.Join("; ", result.Steps.Select(s => s.ToString())));
                Assert.AreEqual(20, merge.Coord.X, 0.6);
                Assert.AreEqual(20, merge.Coord.Y, 0.6);

                roi.Name = "改了名";
                Assert.IsTrue(new FlowRunner(new IParaStrategy[] { roi, merge }).Run(_initial).AllOk, "改名不断开引用");
            }
        }
    }

    /// <summary>
    /// <see cref="FlowScheme"/>：保存后重新打开，参数、ROI、模板、工具之间的引用全部还原；插件缺失时配置不丢。
    /// </summary>
    [TestClass]
    public class FlowSchemeTests : HalconTestBase
    {
        private string _root;
        private AlgoCatalog _catalog;

        [TestInitialize]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "DotNet.HalconRuntime.Tests", Guid.NewGuid().ToString("N"));
            _catalog = AlgoCatalog.Load(new[] { typeof(FileImageStrategy).Assembly });
        }

        [TestCleanup]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static void DisposeAll(IEnumerable<IParaStrategy> tools)
        {
            foreach (var t in tools) t.Dispose();
        }

        [TestMethod]
        public void SaveLoad_RoundTripsIdsNamesParamsAndReferences()
        {
            var roi = (CreateROIStrategy)_catalog.Create("region.create-roi");
            var merge = (MergeRegionStrategy)_catalog.Create("region.merge");
            var fit = (FitLineStrategy)_catalog.Create("fit.line");
            roi.Name = "定位框";
            roi.inPara.HoRect.Dispose();
            roi.inPara.HoRect = NewRegion(RectEnum.Rectangle, 10, 20, 30, 40);
            merge.inPara.RegionSources.Add(roi.Ref("区域"));
            merge.inPara.CoordIn = roi.Ref("坐标系");
            merge.inPara.TmplPoint = new Point2d(1, 2);
            fit.inPara.Transition = Transition.All;
            fit.inPara.TrimEnds = false;
            var tools = new List<IParaStrategy> { roi, merge, fit };

            string dir = Path.Combine(_root, "方案A");
            FlowScheme.Save(dir, tools);
            var loaded = FlowScheme.Load(dir, _catalog);
            try
            {
                CollectionAssert.AreEqual(tools.Select(t => t.Id).ToArray(), loaded.Select(t => t.Id).ToArray());
                CollectionAssert.AreEqual(tools.Select(t => t.Name).ToArray(), loaded.Select(t => t.Name).ToArray());
                CollectionAssert.AreEqual(tools.Select(t => t.GetType()).ToArray(), loaded.Select(t => t.GetType()).ToArray());

                var roi2 = (CreateROIStrategy)loaded[0];
                Assert.AreEqual(roi.inPara.HoRect.Bounds, roi2.inPara.HoRect.Bounds);
                Assert.IsTrue(roi2.inPara.HoRect.HoRegion.IsUsableRegion(), "ROI 区域随参数落盘");

                var merge2 = (MergeRegionStrategy)loaded[1];
                CollectionAssert.AreEqual(new[] { roi.Ref("区域") }, merge2.inPara.RegionSources);
                Assert.AreEqual(roi.Ref("坐标系"), merge2.inPara.CoordIn);
                Assert.AreEqual(new Point2d(1, 2), merge2.inPara.TmplPoint);

                var fit2 = (FitLineStrategy)loaded[2];
                Assert.AreEqual(Transition.All, fit2.inPara.Transition);
                Assert.IsFalse(fit2.inPara.TrimEnds);

                Assert.AreEqual(FlowScheme.ToolDir(dir, roi.Id), roi2.DataDir);
                Assert.AreEqual(0, new FlowRunner(loaded).Validate().Count, "引用全部还原");
            }
            finally
            {
                DisposeAll(tools);
                DisposeAll(loaded);
            }
        }

        [TestMethod]
        public void SaveLoad_MatchingTemplateSurvives()
        {
            var shape = (ShapeModelStrategy)_catalog.Create("match.shape");
            shape.DataDir = Path.Combine(_root, "临时", shape.Id.ToString("N"));
            using (var image = ConstImage(200, 160, 0))
            using (var bar = Rectangle1(40, 50, 90, 60))
            using (var bar2 = Rectangle1(80, 50, 90, 100))
            using (var s1 = Paint(image, bar, 255))
            using (var lImage = Paint(s1, bar2, 255))
            {
                var host = new FakeInteractionHost
                {
                    OnDraw = r =>
                    {
                        r.Bounds = new Rect2d(35.0, 25.0, 80.0, 80.0);
                        r.RebuildRegion();
                    },
                };
                host.FakeDisplay.SetImage(lImage);
                shape.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();
                Assert.IsTrue(shape.HasModel);

                string dir = Path.Combine(_root, "方案B");
                FlowScheme.Save(dir, new IParaStrategy[] { shape });
                Assert.AreEqual(FlowScheme.ToolDir(dir, shape.Id), shape.DataDir, "保存后数据目录迁到方案目录下");

                var loaded = FlowScheme.Load(dir, _catalog);
                var shape2 = (ShapeModelStrategy)loaded.Single();
                try
                {
                    shape2.Init(new FakeInteractionHost());
                    Assert.IsTrue(shape2.HasModel, "模型文件随方案保存, 重新打开即可用");
                    Assert.IsTrue(File.Exists(shape2.GetTemplateView().ModelPath));
                    Assert.AreEqual(shape.inPara.TmplPoint, shape2.inPara.TmplPoint);

                    shape2.inPara.HoRect.Dispose();
                    shape2.inPara.HoRect = NewRegion(RectEnum.Rectangle, 0, 0, 199, 159);
                    Assert.IsTrue(shape2.Run(RunContext.ForImage(lImage), null).IsOk);
                    Assert.AreEqual(shape.inPara.TmplPoint.Value.X, shape2.Coord.X, 1.0);
                }
                finally
                {
                    shape.Dispose();
                    DisposeAll(loaded);
                }
            }
        }

        /// <summary> 插件缺失时用占位工具保留原始配置，再次保存原样写回 </summary>
        [TestMethod]
        public void Load_MissingAlgorithm_KeepsRawConfig_AndWritesItBack()
        {
            string dir = Path.Combine(_root, "方案C");
            Directory.CreateDirectory(dir);
            var id = Guid.NewGuid();
            var root = new JObject
            {
                ["Version"] = 1,
                ["Tools"] = new JArray
                {
                    new JObject { ["AlgoKey"] = "image.file", ["Id"] = Guid.NewGuid(), ["Name"] = "取像", ["Para"] = new JObject { ["ImageFolder"] = @"C:\img" } },
                    new JObject { ["AlgoKey"] = "plugin.gone", ["Id"] = id, ["Name"] = "第三方", ["Para"] = new JObject { ["Secret"] = 42 } },
                },
            };
            File.WriteAllText(Path.Combine(dir, FlowScheme.FileName), root.ToString());

            var loaded = FlowScheme.Load(dir, _catalog);
            try
            {
                Assert.AreEqual(2, loaded.Count, "缺失的工具不丢, 顺序不乱");
                Assert.AreEqual(@"C:\img", ((FileImageStrategy)loaded[0]).inPara.ImageFolder);
                var missing = (MissingTool)loaded[1];
                Assert.AreEqual(id, missing.Id);
                Assert.AreEqual("第三方", missing.Name);
                StringAssert.Contains(missing.Problem, "plugin.gone");

                var result = missing.Run(new RunContext(null), null);
                Assert.AreEqual(RunStatus.Error, result.Status);

                string again = Path.Combine(_root, "方案C2");
                FlowScheme.Save(again, loaded);
                var saved = JObject.Parse(File.ReadAllText(Path.Combine(again, FlowScheme.FileName)));
                Assert.AreEqual(42, saved["Tools"][1]["Para"]["Secret"].Value<int>(), "原始参数原样写回");
                Assert.AreEqual("plugin.gone", saved["Tools"][1]["AlgoKey"].Value<string>());
            }
            finally
            {
                DisposeAll(loaded);
            }
        }

        /// <summary> 取消编辑用的快照：与方案同一套序列化，ROI 一并还原，换上的是新的参数实例 </summary>
        [TestMethod]
        public void CaptureRestorePara_RestoresParamsAndRoi()
        {
            var roi = (CreateROIStrategy)_catalog.Create("region.create-roi");
            try
            {
                roi.inPara.HoRect.Dispose();
                roi.inPara.HoRect = NewRegion(RectEnum.Rectangle, 10, 20, 30, 40);
                roi.inPara.DispRegion = true;
                var snapshot = FlowScheme.CapturePara(roi);

                var edited = roi.inPara;
                edited.DispRegion = false;
                edited.HoRect.Dispose();
                edited.HoRect = NewRegion(RectEnum.Rectangle, 0, 0, 5, 5);

                FlowScheme.RestorePara(roi, snapshot);

                Assert.AreNotSame(edited, roi.inPara);
                Assert.IsFalse(edited.HoRect.HoRegion.IsUsableRegion(), "被换下的参数实例里的 ROI 句柄已释放");
                Assert.IsTrue(roi.inPara.DispRegion);
                Assert.AreEqual(new Rect2d(10.0, 20.0, 30.0, 40.0), roi.inPara.HoRect.Bounds);
                Assert.IsTrue(roi.inPara.HoRect.HoRegion.IsUsableRegion(), "ROI 句柄按快照重建");
            }
            finally { roi.Dispose(); }
        }

        [TestMethod]
        public void CapturePara_MissingTool_ReturnsNull()
        {
            string dir = Path.Combine(_root, "方案E");
            Directory.CreateDirectory(dir);
            var root = new JObject
            {
                ["Version"] = 1,
                ["Tools"] = new JArray { new JObject { ["AlgoKey"] = "plugin.gone", ["Id"] = Guid.NewGuid(), ["Para"] = new JObject() } },
            };
            File.WriteAllText(Path.Combine(dir, FlowScheme.FileName), root.ToString());

            var loaded = FlowScheme.Load(dir, _catalog);
            try { Assert.IsNull(FlowScheme.CapturePara(loaded.Single())); }
            finally { DisposeAll(loaded); }
        }

        [TestMethod]
        public void Load_NewerFormat_Rejected()
        {
            string dir = Path.Combine(_root, "方案D");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, FlowScheme.FileName), "{\"Version\": 99, \"Tools\": []}");

            Assert.ThrowsException<InvalidDataException>(() => FlowScheme.Load(dir, _catalog));
        }
    }
}
