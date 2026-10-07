using System;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class FitArcMidpointStrategyTests : HalconTestBase
    {
        private const int Size = 200;
        private static readonly Point2d DiskCenter = new Point2d(100, 100);
        private const double Radius = 50;

        private HObject _image;
        private FakeDisplay _display;
        private FitArcMidpointStrategy _strategy;

        [TestInitialize]
        public void SetUp()
        {
            _image = DiskImage(Size, Size, DiskCenter, Radius);
            _display = new FakeDisplay();
            _display.SetImage(_image);

            _strategy = new FitArcMidpointStrategy();
            // 圆盘右侧弧段：沿列从内（亮）向外（暗）测量，行 70..130 共 7 个测量矩形
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = NewAffRect(new Point2d(150, 100), 40, 60);
            _strategy.inPara.Transition = Transition.Negative;
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            _image.Dispose();
        }

        [TestMethod]
        public void Identity()
        {
            var info = AlgoInfo.Of(_strategy);
            Assert.AreEqual("fit.arc-midpoint", info.Key);
            Assert.AreEqual(RectEnum.AffRect, info.DefaultRoi, "新建 ROI 默认带角度的矩形");
            Assert.AreEqual("圆弧中点", _strategy.Name);
        }

        [TestMethod]
        public void FindsMidpointOfRightArc()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            var mid = _strategy.ArcMidpoint;
            Assert.AreEqual(150, mid.X, 1.0);
            Assert.AreEqual(100, mid.Y, 1.0);
        }

        [TestMethod]
        public void Overlay_IsDrawnAndReleased()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.IsNull(_strategy.TakeRenderData(), "画完叠加层后应立即释放，不留待取数据");
            Assert.AreEqual(1, _display.Points.Count(p => p.ColorName == HColor.OrangeRed.Name), "中点");
            Assert.AreEqual(5, _display.Points.Count(p => p.ColorName == HColor.Green.Name), "7 点裁首尾后剩 5 点");
            Assert.AreEqual(2, _display.Points.Count(p => p.ColorName == HColor.Red.Name));
            StringAssert.Contains(_display.LastText, "圆弧中点 : 中点:(");
            StringAssert.Contains(_display.LastText, "用点:5");
            Assert.AreEqual(1, _display.Texts.Count, "结果文本只由基类画一次");
            Assert.IsTrue(_display.Texts.All(t => t.ColorName == HColor.Green.Name));
        }

        /// <summary> 无界面运行：显示数据留在槽里，机台可在任意线程取走绘制 </summary>
        [TestMethod]
        public void Headless_LeavesRenderDataForTaking()
        {
            Assert.IsTrue(_strategy.Run(RunContext.ForImage(_image), null).IsOk);

            using (var data = _strategy.TakeRenderData())
            {
                Assert.IsNotNull(data);
                Assert.IsTrue(data.HasMidpoint);
                Assert.AreEqual(_strategy.ArcMidpoint, data.Midpoint);
            }
            Assert.IsNull(_strategy.TakeRenderData(), "取走后槽为空");
        }

        [TestMethod]
        public void ImageOverload_UsesGivenImage()
        {
            var display = new FakeDisplay();
            Assert.IsTrue(_strategy.OnImage(_image, display).IsOk);
            Assert.AreEqual(150, _strategy.ArcMidpoint.X, 1.0);
            Assert.IsNull(_strategy.TakeRenderData());
        }

        [TestMethod]
        public void NoEdge_Fails_ButStillDrawsPartialOverlay()
        {
            _strategy.inPara.Transition = Transition.Positive;
            _strategy.inPara.DispFixRegion = true;

            var result = _strategy.On(_display);

            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "未找到足够的轮廓点");
            Assert.AreEqual(1, _display.Objects.Count(o => o.ColorName == HColor.Blue.Name), "失败时仍显示查找区域便于排查");
            Assert.AreEqual(7, _display.Rect2Centers.Count, "失败时仍显示测量矩形");
            Assert.AreEqual(1, _display.Texts.Count, "失败原因由基类统一画成红字");
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.IsNull(_strategy.TakeRenderData());
        }

        [TestMethod]
        public void TooFewPoints_Fails_ButStillDrawsFoundPoints()
        {
            // 行 60..114 右半圆涂黑: 测量矩形在行 70..130 每 10 行一个, 只有行 120 / 130 仍有边缘, 共 2 点 < 3
            using (var mask = Rectangle1(60, 100, 114, Size - 1))
            using (var masked = Paint(_image, mask, 0))
            {
                _display.SetImage(masked);

                var result = _strategy.On(_display);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "未找到足够的轮廓点");
            }

            // 点不足恰恰最需要看点落在哪: 已找到的点在失败时也要画出来
            var green = _display.Points.Where(p => p.ColorName == HColor.Green.Name).ToList();
            Assert.AreEqual(2, green.Count);
            Assert.IsTrue(green.All(p => p.Item.Y > 115), "只有未涂黑的行 120 / 130 有点");
            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.OrangeRed.Name), "失败时没有中点");
            Assert.IsNull(_strategy.TakeRenderData());
        }

        [DataTestMethod]
        [DataRow(150.0, 100.0, 0.0, DisplayName = "右侧弧")]
        [DataRow(50.0, 100.0, 180.0, DisplayName = "左侧弧(起止角跨 ±π)")]
        [DataRow(100.0, 50.0, 90.0, DisplayName = "顶部弧")]
        [DataRow(100.0, 150.0, -90.0, DisplayName = "底部弧")]
        public void FindsMidpoint_InEveryDirection(double x, double y, double phiDeg)
        {
            // 测量方向沿 phi 由圆内(亮)指向圆外(暗); HALCON 角度逆时针为正、行向下, 90° 指向图像上方
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = NewAffRect(new Point2d(x, y), 40, 60, phiDeg * Math.PI / 180);

            Assert.IsTrue(_strategy.On(_display).IsOk);

            var mid = _strategy.ArcMidpoint;
            Assert.AreEqual(x, mid.X, 1.0);
            Assert.AreEqual(y, mid.Y, 1.0);
        }

        [TestMethod]
        public void CoarseGate_CullsOutlier_MidpointUnaffected()
        {
            // 行 77..83 处圆盘向右凸出到列 174: 行 80 的测量点落在 ~175, 径向偏差 ~25 > 粗滤门限 15。
            // 凸起故意避开弧顶(行 100): 7 点短弧上弧顶的单个离群点会劫持第一阶段 atukey 粗拟合,
            // 反把好点剔掉 —— 这是已知局限, 此处只验证常规离群点的剔除路径。
            using (var bump = Rectangle1(77, 140, 83, 174))
            using (var bumped = Paint(_image, bump, 255))
            {
                _display.SetImage(bumped);
                _strategy.inPara.HoRect.Dispose();
                _strategy.inPara.HoRect = NewAffRect(new Point2d(150, 100), 60, 60);
                _strategy.inPara.TrimEnds = false;

                Assert.IsTrue(_strategy.On(_display).IsOk);
            }

            var mid = _strategy.ArcMidpoint;
            Assert.AreEqual(150, mid.X, 1.0);
            Assert.AreEqual(100, mid.Y, 1.0);

            var red = _display.Points.Where(p => p.ColorName == HColor.Red.Name).ToList();
            Assert.AreEqual(1, red.Count, "离群点被剔除并以红色显示");
            Assert.AreEqual(175, red[0].Item.X, 1.0);
            Assert.AreEqual(80, red[0].Item.Y, 1.0);
            StringAssert.Contains(_display.LastText, "用点:6");
        }

        [TestMethod]
        public void NotEnoughPoints_Fails_StillDrawsFoundPoints()
        {
            // 3 个测量矩形(行 90/100/110), 把行 110 附近涂暗后只剩 2 个边缘点 < 最少 3 点
            using (var mask = Rectangle1(105, 100, 115, Size - 1))
            using (var masked = Paint(_image, mask, 0))
            {
                _display.SetImage(masked);
                _strategy.inPara.HoRect.Dispose();
                _strategy.inPara.HoRect = NewAffRect(new Point2d(150, 100), 40, 20);

                var result = _strategy.On(_display);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "未找到足够的轮廓点");
            }

            Assert.AreEqual(2, _display.Points.Count(p => p.ColorName == HColor.Green.Name), "失败时仍显示已找到的点");
            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.OrangeRed.Name), "失败时没有中点");
        }

        [TestMethod]
        public void RoiNotDrawn_Fails()
        {
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = new CvRegion { Type = RectEnum.AffRect };

            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "尚未绘制 ROI");
        }

        [TestMethod]
        public void NoImage_Fails()
        {
            var result = _strategy.On(new FakeDisplay());
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "图像来源为空");
        }

        [TestMethod]
        public void Failure_ResetsPreviousMidpoint()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreNotEqual(default(Point2d), _strategy.ArcMidpoint);

            _strategy.inPara.Transition = Transition.Positive;
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);

            // 宿主不看结果时下游照跑: 不能继续拿到上一轮的中点
            Assert.AreEqual(default(Point2d), _strategy.ArcMidpoint);
        }

        [TestMethod]
        public void InvalidTransition_ClearError_ResetsPreviousMidpoint()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            _strategy.inPara.Transition = (Transition)99;   // 旧配置手改出来的非法值
            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "过渡方向");
            StringAssert.Contains(result.Message, "99", "报错要带出写错的原值");
            StringAssert.StartsWith(_display.LastText, _strategy.Name + " : ", "状态文本带出工具名");

            Assert.AreEqual(default(Point2d), _strategy.ArcMidpoint);
        }

        [TestMethod]
        public void ImageIn_ResolvesUpstreamImage()
        {
            var camera = new StubStrategy("取像").Image("图像", () => _image);
            _strategy.inPara.ImageIn = camera.Ref("图像");
            // 显示窗口里是全黑图, 只有上游那张有圆盘
            using (var black = ConstImage(Size, Size, 0))
            {
                _display.SetImage(black);
                Assert.IsTrue(_strategy.On(_display, camera).IsOk);
            }

            Assert.AreEqual(150, _strategy.ArcMidpoint.X, 1.0);
            Assert.AreEqual(100, _strategy.ArcMidpoint.Y, 1.0);
        }

        [TestMethod]
        public void RegionIn_UpstreamRegionExcludingArc_Fails()
        {
            // 圆盘右缘在列 ~150；上游区域只到列 140。measure_pos 忽略定义域，必须先滤掉域外点
            using (var upstreamRegion = Rectangle1(0, 0, Size - 1, 140))
            {
                var upstream = new StubStrategy("上游").Region("区域", () => upstreamRegion);
                _strategy.inPara.RegionIn = upstream.Ref("区域");

                var result = _strategy.On(_display, upstream);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "未找到足够的轮廓点");
                Assert.AreEqual(default(Point2d), _strategy.ArcMidpoint);
            }
        }

        [TestMethod]
        public void RegionIn_UpstreamRegionCoveringArc_Fits()
        {
            using (var upstreamRegion = Rectangle1(0, 0, Size - 1, Size - 1))
            {
                var upstream = new StubStrategy("上游").Region("区域", () => upstreamRegion);
                _strategy.inPara.RegionIn = upstream.Ref("区域");

                Assert.IsTrue(_strategy.On(_display, upstream).IsOk);
                Assert.AreEqual(150, _strategy.ArcMidpoint.X, 1.0);
                Assert.AreEqual(100, _strategy.ArcMidpoint.Y, 1.0);
                Assert.IsTrue(upstreamRegion.IsInitialized(), "上游区域是借用的, 不能被释放");
            }
        }

        [TestMethod]
        public void MaxErrAndCoarseGateZero_DisableFiltering()
        {
            // 两个门限都为 0 时粗滤门限也是 0：RemoveOutliers 的 <= 0 守卫让它不剔点
            _strategy.inPara.MaxErr = 0;
            _strategy.inPara.CoarseGate = 0;
            _strategy.inPara.TrimEnds = false;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(150, _strategy.ArcMidpoint.X, 1.0);
            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.Red.Name));
            StringAssert.Contains(_display.LastText, "用点:7");
        }

        [TestMethod]
        public void SigmaZero_StillFits()
        {
            _strategy.inPara.Sigma = 0;

            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(150, _strategy.ArcMidpoint.X, 1.0);
        }

        [TestMethod]
        public void RegionIn_Unresolvable_Fails()
        {
            _strategy.inPara.RegionIn = new StubStrategy("上游").Ref("区域");
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);
        }

        [TestMethod]
        public void CoordIn_TranslatesRoi()
        {
            using (var image = DiskImage(Size + 40, Size, new Point2d(130, 100), Radius))
            {
                _display.SetImage(image);
                var locator = StubStrategy.Coord("定位", new Point2d(100, 100), new CvCoord(130, 100));
                _strategy.inPara.CoordIn = locator.Ref("坐标系");

                Assert.IsTrue(_strategy.On(_display, locator).IsOk);

                Assert.AreEqual(180, _strategy.ArcMidpoint.X, 1.0);
                Assert.AreEqual(100, _strategy.ArcMidpoint.Y, 1.0);
            }
        }

        [TestMethod]
        public void CoordIn_RotatesAroundTmplPoint()
        {
            // 坐标系绕圆心转 90°（HALCON 角度逆时针为正，行向下）：右侧弧段转到上方。
            var locator = StubStrategy.Coord("定位", DiskCenter, CvCoord.FromDegrees(100, 100, 90));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            Assert.IsTrue(_strategy.On(_display, locator).IsOk);

            var mid = _strategy.ArcMidpoint;
            Assert.AreEqual(100, mid.X, 1.0);
            Assert.AreEqual(50, mid.Y, 1.0, "逆时针 90° 后右侧 (150,100) 转到顶部 (100,50)");
        }

        [TestMethod]
        public void Outputs_TreeAndResolution()
        {
            var tree = new FakeTree();
            _strategy.GenTreeNode(tree);
            Assert.IsTrue(_strategy.On(_display).IsOk);

            CollectionAssert.IsSubsetOf(new[] { "圆弧中点/中点", "圆弧中点/中点/行", "圆弧中点/中点/列" }, tree.Paths);
            Assert.AreEqual(OutEnum.Point, tree.Types["圆弧中点/中点"]);

            var mid = _strategy.ArcMidpoint;
            var ctx = new RunContext(null, new IParaStrategy[] { _strategy });
            Assert.AreEqual(mid, ctx.Resolve<Point2d>(_strategy.Ref("中点")));
            Assert.AreEqual(mid.Y, ctx.Resolve<double>(_strategy.Ref("中点/行")));
            Assert.AreEqual(mid.X, ctx.Resolve<double>(_strategy.Ref("中点/列")));
        }

        [TestMethod]
        public void Params_Declared()
        {
            var labels = _strategy.Labels(TabPageEnum.Parameter);
            CollectionAssert.AreEqual(new[]
            {
                "图像来源", "区域来源", "过渡方向", "选择", "滤波", "阈值", "步距", "步宽", "最大偏差", "裁剪首尾", "粗滤阈值",
            }, labels);
            CollectionAssert.AreEqual(new[] { "跟随坐标" }, _strategy.Labels(TabPageEnum.Region));
            CollectionAssert.IsSubsetOf(new[] { "查找区域", "拟合区域", "拟合点", "显示结果", "点大小", "显示文本" },
                _strategy.Labels(TabPageEnum.Display));
        }

        [TestMethod]
        public void Params_WriteBackValuesNotTexts()
        {
            Assert.IsTrue(_strategy.SetParam("粗滤阈值", 30.0));
            Assert.IsTrue(_strategy.SetParam("最大偏差", 3));
            Assert.IsTrue(_strategy.SetParam("选择", EdgeSelect.Second));
            Assert.IsTrue(_strategy.SetParam("拟合区域", true));

            Assert.AreEqual(30.0, _strategy.inPara.CoarseGate);
            Assert.AreEqual(3, _strategy.inPara.MaxErr);
            Assert.AreEqual(EdgeSelect.Second, _strategy.inPara.ContourType);
            Assert.IsTrue(_strategy.inPara.DispFixRegion);

            var choice = (ChoiceParam)_strategy.Param("过渡方向");
            Assert.AreEqual("由白到黑", choice.Options[choice.SelectedIndex].Text, "界面文字只出现在选项声明里");
        }

        [TestMethod]
        public void Params_NumberValidation()
        {
            var threshold = (NumberParam)_strategy.Param("阈值");
            Assert.IsFalse(threshold.TryParse("-1", out _, out string error));
            StringAssert.Contains(error, "不能小于");
            Assert.IsFalse(threshold.TryParse("abc", out _, out _));
            Assert.IsTrue(threshold.TryParse("30", out object value, out _));
            Assert.AreEqual(30, value);
        }

        [TestMethod]
        public void Close_And_Dispose_AreSafeWithoutRenderData()
        {
            _strategy.Close(new FakeRoiHost());
            var roi = _strategy.inPara.HoRect;
            _strategy.Dispose();
            _strategy.Dispose();
            Assert.IsNull(roi.HoRegion);
        }
    }
}
