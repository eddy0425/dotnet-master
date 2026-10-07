using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class CreateROIStrategyTests : HalconTestBase
    {
        private FakeDisplay _display;
        private CreateROIStrategy _strategy;

        [TestInitialize]
        public void SetUp()
        {
            _display = new FakeDisplay();
            _strategy = new CreateROIStrategy();
            // 左上 (40,20)，宽 60 高 40 → 中心 (70,40)，面积 61*41（HALCON 矩形含端点）
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = NewRegion(RectEnum.Rectangle, 40, 20, 60, 40);
        }

        [TestCleanup]
        public void TearDown() => _strategy.Dispose();

        [TestMethod]
        public void Identity()
        {
            var info = AlgoInfo.Of(_strategy);
            Assert.AreEqual("region.create-roi", info.Key);
            Assert.AreEqual("创建ROI", _strategy.Name);
        }

        [TestMethod]
        public void RoiNotDrawn_Fails_RedText_ClearsResult()
        {
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = new CvRegion();
            _strategy.inPara.DispText = false;

            var result = _strategy.On(_display);

            Assert.AreEqual(RunStatus.Error, result.Status);
            Assert.AreEqual(1, _display.Texts.Count, "报错不受 DispText 门控");
            Assert.AreEqual("创建ROI : 尚未绘制 ROI", _display.LastText);
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(new CvCoord(), _strategy.Coord);
            Assert.IsFalse(_strategy.Region.IsUsableRegion());
        }

        [TestMethod]
        public void Local_CopiesRegion_CoordIsRoiCenter()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            var result = _strategy.Region;
            Assert.AreNotSame(_strategy.inPara.HoRect.HoRegion, result, "结果是独立句柄，不与配置 ROI 共享");
            double area = AreaCenter(result, out Point2d center);
            Assert.AreEqual(AreaCenter(_strategy.inPara.HoRect.HoRegion, out _), area);
            Assert.AreEqual(70, center.X, 0.6);
            Assert.AreEqual(40, center.Y, 0.6);

            Assert.AreEqual(new CvCoord(new Point2d(70, 40)), _strategy.Coord);

            Assert.AreEqual(1, _display.Objects.Count);
            Assert.AreSame(result, _display.Objects[0].Item);
            Assert.AreEqual(HColor.Blue.Name, _display.Objects[0].ColorName);
            Assert.AreEqual("创建ROI : 中心:(70.00,40.00) 宽:60 高:40", _display.LastText);
            Assert.AreEqual(HColor.Green.Name, _display.Texts[0].ColorName);
        }

        [TestMethod]
        public void DisplayFlagsOff_DrawsNothing()
        {
            _strategy.inPara.DispRegion = false;
            _strategy.inPara.DispText = false;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(0, _display.Objects.Count);
            Assert.AreEqual(0, _display.Texts.Count);
        }

        /// <summary> 无界面运行：display 为 null 时只计算 </summary>
        [TestMethod]
        public void Headless_ComputesWithoutDisplay()
        {
            var result = _strategy.Run(RunContext.ForImage(null), null);

            Assert.IsTrue(result.IsOk);
            Assert.IsTrue(_strategy.Region.IsUsableRegion());
            Assert.AreSame(result, _strategy.LastResult);
        }

        [TestMethod]
        public void RepeatedRun_ReleasesPreviousResult()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            var first = _strategy.Region;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreNotSame(first, _strategy.Region);
            Assert.IsFalse(first.IsInitialized());
            Assert.IsTrue(_strategy.inPara.HoRect.HoRegion.IsInitialized(), "配置 ROI 不受影响");
        }

        [TestMethod]
        public void CoordIn_Translation_MovesRegionAndCoord()
        {
            var locator = StubStrategy.Coord("定位", new Point2d(0, 0), new CvCoord(10, 5));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.IsTrue(_strategy.On(_display, locator).IsOk);

            AreaCenter(_strategy.Region, out Point2d center);
            Assert.AreEqual(80, center.X, 0.6);
            Assert.AreEqual(45, center.Y, 0.6);
            Assert.AreEqual(80, _strategy.Coord.X, 1e-6);
            Assert.AreEqual(45, _strategy.Coord.Y, 1e-6);
            Assert.AreEqual(0, _strategy.Coord.Angle.Radians, 1e-12);
        }

        [TestMethod]
        public void CoordIn_Rotation_CoordUsesInputAngle()
        {
            // 绕 ROI 中心转 90°：中心不动，区域外接框宽高互换，坐标系角度取输入角度。
            var locator = StubStrategy.Coord("定位", new Point2d(70, 40), CvCoord.FromDegrees(70, 40, 90));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.IsTrue(_strategy.On(_display, locator).IsOk);

            // TransPoint 走 affine_trans_pixel（像素中心约定），旋转时原点会偏出最多 1px
            Assert.AreEqual(70, _strategy.Coord.X, 1.0);
            Assert.AreEqual(40, _strategy.Coord.Y, 1.0);
            Assert.AreEqual(90, _strategy.Coord.AngleDegrees, 1e-9);

            HOperatorSet.SmallestRectangle1(_strategy.Region, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            Assert.AreEqual(60, r2.D - r1.D, 1.5, "旋转后高度 ≈ 原宽");
            Assert.AreEqual(40, c2.D - c1.D, 1.5, "旋转后宽度 ≈ 原高");
        }

        [TestMethod]
        public void CoordIn_Unresolvable_FailsAndClearsResult()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            var gone = StubStrategy.Coord("不存在", new Point2d(), new CvCoord());
            _strategy.inPara.CoordIn = gone.Ref("坐标系");

            var result = _strategy.On(_display);   // 上游不在列表里

            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "未能解析");
            Assert.IsFalse(_strategy.Region.IsUsableRegion(), "失败时清掉上一轮结果，避免下游读到过期区域");
            Assert.AreEqual(new CvCoord(), _strategy.Coord);
        }

        [TestMethod]
        public void CoordIn_NotTaught_Fails()
        {
            var locator = StubStrategy.Coord("定位", null, new CvCoord(10, 5));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            var result = _strategy.On(_display, locator);

            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "示教");
        }

        [TestMethod]
        public void Outputs_TreeAndResolution()
        {
            var tree = FakeTree.Of(_strategy);
            CollectionAssert.IsSubsetOf(new[]
            {
                "创建ROI/坐标系", "创建ROI/坐标系/原点", "创建ROI/坐标系/原点/行", "创建ROI/坐标系/原点/列",
                "创建ROI/坐标系/角度", "创建ROI/区域", "创建ROI/结果", "创建ROI/文本显示",
            }, tree.Paths);
            Assert.AreEqual(OutEnum.Coord, tree.Types["创建ROI/坐标系"]);
            Assert.AreEqual(OutEnum.Region, tree.Types["创建ROI/区域"]);

            var locator = StubStrategy.Coord("定位", new Point2d(0, 0), new CvCoord(10, 5));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");
            Assert.IsTrue(_strategy.On(_display, locator).IsOk);

            var ctx = new RunContext(null, new IParaStrategy[] { locator, _strategy });
            var follow = ctx.ResolveCoord(_strategy.Ref("坐标系"));
            Assert.AreEqual(new Point2d(70, 40), follow.Template, "模板点是配置态中心，不随跟随变化");
            Assert.AreEqual(_strategy.Coord, follow.Current);
            Assert.AreEqual(_strategy.Coord.Center, ctx.Resolve<Point2d>(_strategy.Ref("坐标系/原点")));
            Assert.AreEqual(45, ctx.Resolve<double>(_strategy.Ref("坐标系/原点/行")), 1e-6);
            Assert.AreEqual(80, ctx.Resolve<double>(_strategy.Ref("坐标系/原点/列")), 1e-6);
            Assert.AreEqual(0, ctx.Resolve<double>(_strategy.Ref("坐标系/角度")), 1e-12);
            Assert.AreSame(_strategy.Region, ctx.ResolveRegion(_strategy.Ref("区域"), null));
            Assert.AreEqual(true, ctx.Resolve<bool>(_strategy.Ref("结果")));
        }

        [TestMethod]
        public void Params_Declared()
        {
            CollectionAssert.AreEqual(new[] { "跟随坐标" }, _strategy.Labels(Pages.Region));
            CollectionAssert.AreEqual(new[] { "查找区域", "显示文本", "文本X", "文本Y", "字号" }, _strategy.Labels(Pages.Display));
            Assert.AreEqual(0, _strategy.Labels(Pages.Parameter).Length);

            var coord = (SourceParam)_strategy.Param("跟随坐标");
            Assert.AreEqual(OutEnum.Coord, coord.SourceType);

            Assert.IsTrue(_strategy.SetParam("查找区域", false));
            Assert.IsFalse(_strategy.inPara.DispRegion);
            Assert.IsTrue(_strategy.SetParam("字号", 30));
            Assert.AreEqual(30, _strategy.inPara.FontSize);
            Assert.IsFalse(_strategy.SetParam("字号", 30), "值没变不写回");
        }

        [TestMethod]
        public void Close_ClearsResult_KeepsRoi()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            _strategy.Close(new FakeRoiHost());

            Assert.IsFalse(_strategy.Region.IsUsableRegion());
            Assert.AreEqual(new CvCoord(), _strategy.Coord);
            Assert.IsTrue(_strategy.inPara.HoRect.HoRegion.IsUsableRegion(), "重新打开工具页仍可复用配置 ROI");
        }

        [TestMethod]
        public void Dispose_ReleasesHandles_Idempotent()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            var result = _strategy.Region;
            var roi = _strategy.inPara.HoRect.HoRegion;

            _strategy.Dispose();
            _strategy.Dispose();
            _strategy.Close(new FakeRoiHost()); // Dispose 后 Close 为空操作

            Assert.IsFalse(result.IsInitialized());
            Assert.IsFalse(roi.IsInitialized(), "工具生命周期结束时连配置 ROI 一起释放（宿主先保存再释放）");
            Assert.ThrowsException<System.ObjectDisposedException>(() => _strategy.On(_display));
        }

        [TestMethod]
        public void DrawROIAsync_Cancel_RestoresType()
        {
            var host = new FakeRoiHost { Confirm = false };

            _strategy.DrawROIAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult();

            Assert.AreEqual(RectEnum.Circle, host.TypeDuringDraw);
            Assert.AreEqual(RectEnum.Rectangle, _strategy.inPara.HoRect.Type);
            Assert.AreEqual(1, host.FakeDisplay.Regions.Count, "取消后仍把原 ROI 重画回去");
            Assert.AreEqual(1, host.SetRectParaCount);
        }

        [TestMethod]
        public void DrawROIAsync_Confirm_KeepsType_ModifyUsesModDraw()
        {
            var host = new FakeRoiHost();

            _strategy.DrawROIAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult();
            Assert.AreEqual(RectEnum.Circle, _strategy.inPara.HoRect.Type);

            _strategy.DrawROIAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();
            Assert.AreEqual(1, host.DrawCount);
            Assert.AreEqual(1, host.DrawModCount);
            Assert.AreEqual(RectEnum.Circle, _strategy.inPara.HoRect.Type, "修改模式不改类型");
            Assert.AreEqual(2, host.SetRectParaCount);
        }

        [TestMethod]
        public void DispROI_KeepsType()
        {
            var host = new FakeRoiHost();
            _strategy.DispROI(host);
            Assert.AreEqual(RectEnum.Rectangle, _strategy.inPara.HoRect.Type);
            Assert.AreEqual(1, host.SetRectParaCount);
            Assert.AreEqual(0, host.FakeDisplay.Regions.Count + host.FakeDisplay.Objects.Count);
        }

        /// <summary> 参数类只放配置：运行结果在策略上，不进序列化 </summary>
        [TestMethod]
        public void Para_ContainsOnlyConfig()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_strategy.inPara);
            Assert.IsFalse(json.Contains("\"Result\""));
            Assert.IsFalse(json.Contains("\"Coord\""));
            Assert.IsTrue(json.Contains("\"HoRect\""));
            Assert.IsTrue(json.Contains("\"CoordIn\""));
        }
    }
}
