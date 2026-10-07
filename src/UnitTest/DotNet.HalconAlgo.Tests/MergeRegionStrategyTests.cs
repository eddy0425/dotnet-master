using System.Collections.Generic;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class MergeRegionStrategyTests : HalconTestBase
    {
        private readonly List<HObject> _owned = new List<HObject>();
        private FakeDisplay _display;
        private MergeRegionStrategy _strategy;
        private StubStrategy _a, _b;

        /// <summary>两个不相交的 11×11 方块：A 中心 (15,15)，B 中心 (55,15)。</summary>
        [TestInitialize]
        public void SetUp()
        {
            _display = new FakeDisplay();
            _strategy = new MergeRegionStrategy();
            var ra = Own(Rectangle1(10, 10, 20, 20));
            var rb = Own(Rectangle1(10, 50, 20, 60));
            _a = new StubStrategy("A").Region("区域", () => ra);
            _b = new StubStrategy("B").Region("区域", () => rb);
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            foreach (var o in _owned) o.Dispose();
        }

        private HObject Own(HObject o)
        {
            _owned.Add(o);
            return o;
        }

        /// <summary> 直接写配置；可以带本地项（模拟旧方案里补齐用的空槽位） </summary>
        private void Sources(params SourceRef[] sources) => _strategy.inPara.RegionSources = sources.ToList();

        private SourceListParam SourceList => (SourceListParam)_strategy.Param("输入区域");

        [TestMethod]
        public void Identity()
        {
            Assert.AreEqual("region.merge", AlgoInfo.Of(_strategy).Key);
            Assert.AreEqual("区域合并", _strategy.Name);
            Assert.AreEqual(0, _strategy.inPara.RegionSources.Count, "新建时没有来源, 个数不再固定");
        }

        [TestMethod]
        public void NoSources_Fails_RedTextEvenIfTextDisabled()
        {
            _strategy.inPara.DispText = false;

            var result = _strategy.On(_display, _a, _b);

            Assert.AreEqual(RunStatus.Error, result.Status);
            Assert.AreEqual("区域合并 : 无有效输入区域", _display.LastText);
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.IsFalse(_strategy.Region.IsUsableRegion());
            Assert.IsNull(_strategy.inPara.TmplPoint);
        }

        [TestMethod]
        public void NullSourceArray_TreatedAsNoSources()
        {
            _strategy.inPara.RegionSources = null;
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display, _a).Status);
            Assert.AreEqual(0, _strategy.inPara.RegionSources.Count, "旧配置里的 null 当作空列表");
        }

        [TestMethod]
        public void AllSourcesMissing_Fails()
        {
            var gone = new StubStrategy("X").Region("区域", () => null);
            Sources(gone.Ref("区域"));
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display, _a, _b).Status);
            StringAssert.Contains(_display.LastText, "无有效输入区域");
        }

        [TestMethod]
        public void Union_AreaCenter_AndTeachesTmplPointOnce()
        {
            Sources(_a.Ref("区域"), SourceRef.Local, _b.Ref("区域"));

            Assert.IsTrue(_strategy.On(_display, _a, _b).IsOk);

            double area = AreaCenter(_strategy.Region, out Point2d center);
            Assert.AreEqual(2 * 121, area, "空槽位跳过，两块并在一起");
            Assert.AreEqual(35, center.X, 1e-6);
            Assert.AreEqual(15, center.Y, 1e-6);

            Assert.AreEqual(35, _strategy.Coord.X, 1e-6);
            Assert.AreEqual(15, _strategy.Coord.Y, 1e-6);
            Assert.AreEqual(0, _strategy.Coord.Angle.Radians);
            Assert.AreEqual(new Point2d(35, 15), _strategy.inPara.TmplPoint);

            Assert.AreEqual(1, _display.Objects.Count);
            Assert.AreSame(_strategy.Region, _display.Objects[0].Item);
            Assert.AreEqual("区域合并 : 合并数量:2 中心:(35.00,15.00)", _display.LastText);
            Assert.AreEqual(HColor.Green.Name, _display.Texts[0].ColorName);

            // 示教只发生一次：直接改配置 (不经面板) 时模板点仍是首轮的值
            Sources(_a.Ref("区域"), SourceRef.Local, SourceRef.Local);
            Assert.IsTrue(_strategy.On(_display, _a, _b).IsOk);
            Assert.AreEqual(new Point2d(35, 15), _strategy.inPara.TmplPoint);
            Assert.AreEqual(15, _strategy.Coord.X, 1e-6);
        }

        [TestMethod]
        public void Result_IsIndependentCopy()
        {
            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);
            var first = _strategy.Region;

            Assert.IsTrue(_strategy.On(_display, _a).IsOk);

            Assert.IsFalse(first.IsInitialized(), "上一轮结果被释放");
            Assert.IsTrue(_owned[0].IsInitialized(), "上游区域不能被释放");
        }

        [TestMethod]
        public void MissingSource_Warns_DoesNotTeach()
        {
            var gone = new StubStrategy("不存在").Region("区域", () => null);
            Sources(_a.Ref("区域"), gone.Ref("区域"));

            var result = _strategy.On(_display, _a, _b);

            Assert.AreEqual(RunStatus.Warning, result.Status, "有结果但降级");
            Assert.AreEqual(121, AreaCenter(_strategy.Region, out _));
            Assert.IsNull(_strategy.inPara.TmplPoint, "残缺重心不能当示教基准");
            StringAssert.Contains(_display.LastText, "合并数量:1 无效来源:1");
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
        }

        [TestMethod]
        public void EmptyUpstreamRegion_CountsAsMissing()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Own(empty);
            var c = new StubStrategy("C").Region("区域", () => empty);
            Sources(_a.Ref("区域"), c.Ref("区域"));

            Assert.AreEqual(RunStatus.Warning, _strategy.On(_display, _a, c).Status);

            StringAssert.Contains(_display.LastText, "无效来源:1");
        }

        [TestMethod]
        public void ZeroAreaUpstreamRegion_Fails_ClearsResult_DoesNotTeach()
        {
            // gen_empty_region：count_obj 为 1、能通过来源校验，但没有像素; area_center 给出的 (0,0) 不能当重心
            HOperatorSet.GenEmptyRegion(out HObject emptyRegion);
            Own(emptyRegion);
            var c = new StubStrategy("C").Region("区域", () => emptyRegion);
            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);
            _strategy.inPara.TmplPoint = null;

            Sources(c.Ref("区域"));
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display, c).Status);

            Assert.AreEqual("区域合并 : 无有效输入区域", _display.LastText);
            Assert.IsFalse(_strategy.Region.IsUsableRegion(), "上一轮结果不能留给下游");
            Assert.IsNull(_strategy.inPara.TmplPoint);
        }

        [TestMethod]
        public void CoordIn_Follow_TransformsResult_TeachesUntransformedCenter()
        {
            var locator = StubStrategy.Coord("定位", new Point2d(0, 0), new CvCoord(100, 50));
            Sources(_a.Ref("区域"), _b.Ref("区域"));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.IsTrue(_strategy.On(_display, _a, _b, locator).IsOk);

            AreaCenter(_strategy.Region, out Point2d center);
            Assert.AreEqual(135, center.X, 1e-6);
            Assert.AreEqual(65, center.Y, 1e-6);
            Assert.AreEqual(135, _strategy.Coord.X, 1e-6);
            Assert.AreEqual(new Point2d(35, 15), _strategy.inPara.TmplPoint, "示教取变换前的重心");
        }

        [TestMethod]
        public void CoordIn_Rotation_CoordCarriesInputAngle()
        {
            var locator = StubStrategy.Coord("定位", new Point2d(15, 15), CvCoord.FromDegrees(15, 15, 30));
            Sources(_a.Ref("区域"));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.IsTrue(_strategy.On(_display, _a, locator).IsOk);

            Assert.AreEqual(30, _strategy.Coord.AngleDegrees, 1e-9);
            Assert.AreEqual(15, _strategy.Coord.X, 0.5);
            Assert.AreEqual(15, _strategy.Coord.Y, 0.5);
        }

        [TestMethod]
        public void CoordIn_Unresolvable_FailsAndClearsResult()
        {
            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);
            var locator = StubStrategy.Coord("定位", new Point2d(), new CvCoord());
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.AreEqual(RunStatus.Error, _strategy.On(_display, _a).Status);

            Assert.IsFalse(_strategy.Region.IsUsableRegion());
            Assert.AreEqual(new CvCoord(), _strategy.Coord);
        }

        [TestMethod]
        public void Outputs_TemplateOnlyAfterTeaching()
        {
            var tree = FakeTree.Of(_strategy);
            CollectionAssert.IsSubsetOf(new[] { "区域合并/坐标系", "区域合并/坐标系/原点/行", "区域合并/坐标系/角度", "区域合并/区域" }, tree.Paths);

            var ctx = new RunContext(null, new IParaStrategy[] { _a, _strategy });
            Assert.ThrowsException<System.InvalidOperationException>(() => ctx.ResolveCoord(_strategy.Ref("坐标系")),
                "未示教时下游跟随应响亮失败");

            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);

            var follow = ctx.ResolveCoord(_strategy.Ref("坐标系"));
            Assert.AreEqual(new Point2d(15, 15), follow.Template);
            Assert.AreEqual(_strategy.Coord, follow.Current);
            Assert.AreEqual(15, ctx.Resolve<double>(_strategy.Ref("坐标系/原点/列")), 1e-6);
            Assert.AreSame(_strategy.Region, ctx.ResolveRegion(_strategy.Ref("区域"), null));
        }

        #region 参数

        /// <summary> 可变长来源列表取代原来的 6 个固定槽位 </summary>
        [TestMethod]
        public void Params_CoordInAndSourceList()
        {
            CollectionAssert.AreEqual(new[] { "跟随坐标", "输入区域" }, _strategy.Labels(Pages.Parameter));
            Assert.AreEqual(OutEnum.Coord, ((SourceParam)_strategy.Param("跟随坐标")).SourceType);
            Assert.AreEqual(OutEnum.Region, SourceList.SourceType);
            Assert.AreEqual(0, SourceList.MaxCount, "个数不限");
        }

        [TestMethod]
        public void Params_NullSourceList_Writes()
        {
            // 旧配置里 "RegionSources": null 反序列化后就是 null
            _strategy.inPara.RegionSources = null;

            Assert.IsTrue(_strategy.SetParam("输入区域", new[] { _a.Ref("区域"), _b.Ref("区域") }));

            CollectionAssert.AreEqual(new[] { _a.Ref("区域"), _b.Ref("区域") }, _strategy.inPara.RegionSources);
        }

        [TestMethod]
        public void SourceChanged_ClearsTmplPoint()
        {
            _strategy.inPara.TmplPoint = new Point2d(1, 2);

            _strategy.SetParam("输入区域", new[] { _b.Ref("区域") });

            Assert.IsNull(_strategy.inPara.TmplPoint);
            CollectionAssert.AreEqual(new[] { _b.Ref("区域") }, _strategy.inPara.RegionSources);
        }

        /// <summary>
        /// 旧方案：6 个槽位，没用的存成本地。JSON 都是数组，直接读得进来；读入后去掉补齐用的空槽，不需要参数迁移。
        /// </summary>
        [TestMethod]
        public void OldSchemeWithSixSlots_LoadsWithoutEmptySlots()
        {
            var old = new { RegionSources = new[] { _a.Ref("区域"), SourceRef.Local, _b.Ref("区域"), SourceRef.Local, SourceRef.Local, SourceRef.Local } };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(old);
            _strategy.inPara = Newtonsoft.Json.JsonConvert.DeserializeObject<RegionMerge>(json);

            CollectionAssert.AreEqual(new[] { _a.Ref("区域"), _b.Ref("区域") }, SourceList.Value.ToArray(), "参数页只看到真正的来源");
            Assert.IsTrue(_strategy.On(_display, _a, _b).IsOk);
            StringAssert.Contains(_display.LastText, "合并数量:2");
        }

        [TestMethod]
        public void CoordInChanged_ClearsTmplPoint()
        {
            _strategy.inPara.TmplPoint = new Point2d(1, 2);

            _strategy.SetParam("跟随坐标", _a.Ref("区域"));

            Assert.IsNull(_strategy.inPara.TmplPoint);
        }

        [TestMethod]
        public void SameValue_KeepsTmplPoint()
        {
            _strategy.inPara.TmplPoint = new Point2d(1, 2);

            Sources(_a.Ref("区域"));
            Assert.IsFalse(_strategy.SetParam("输入区域", new[] { _a.Ref("区域") }), "内容没变就不算改动（新列表实例也一样）");

            Assert.AreEqual(new Point2d(1, 2), _strategy.inPara.TmplPoint);
        }

        [TestMethod]
        public void DisplayOnlyChange_KeepsTmplPoint()
        {
            _strategy.inPara.TmplPoint = new Point2d(1, 2);

            _strategy.SetParam("字号", 30);
            _strategy.SetParam("查找区域", false);

            Assert.AreEqual(new Point2d(1, 2), _strategy.inPara.TmplPoint);
            Assert.AreEqual(30, _strategy.inPara.FontSize);
            Assert.IsFalse(_strategy.inPara.DispRegion);
        }

        #endregion

        [TestMethod]
        public void Close_ClearsResult_KeepsTmplPoint()
        {
            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);

            _strategy.Close(new FakeRoiHost());

            Assert.IsFalse(_strategy.Region.IsUsableRegion());
            Assert.AreEqual(new CvCoord(), _strategy.Coord);
            Assert.AreEqual(new Point2d(15, 15), _strategy.inPara.TmplPoint);
        }

        [TestMethod]
        public void Dispose_Idempotent_CloseAfterIsNoop()
        {
            Sources(_a.Ref("区域"));
            Assert.IsTrue(_strategy.On(_display, _a).IsOk);
            var result = _strategy.Region;

            _strategy.Dispose();
            _strategy.Dispose();
            _strategy.Close(new FakeRoiHost());

            Assert.IsFalse(result.IsInitialized());
        }

        [TestMethod]
        public void Serialization_KeepsSourcesAndTmplPoint()
        {
            Sources(_a.Ref("区域"));
            _strategy.inPara.TmplPoint = new Point2d(1, 2);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_strategy.inPara);
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<RegionMerge>(json);

            Assert.IsFalse(json.Contains("\"Region\""));
            Assert.AreEqual(new Point2d(1, 2), back.TmplPoint);
            CollectionAssert.AreEqual(new[] { _a.Ref("区域") }, back.RegionSources);
        }
    }
}
