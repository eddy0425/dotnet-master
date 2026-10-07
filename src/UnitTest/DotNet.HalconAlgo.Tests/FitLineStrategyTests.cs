using System;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class FitLineStrategyTests : HalconTestBase
    {
        private const int Size = 200;

        private HObject _image;
        private FakeDisplay _display;
        private FitLineStrategy _strategy;

        [TestInitialize]
        public void SetUp()
        {
            _image = VerticalStepImage(Size, Size, 100);
            _display = new FakeDisplay();
            _display.SetImage(_image);

            _strategy = new FitLineStrategy();
            // 沿列测量 60 宽，沿行步进 120 高：步数 6 → 13 个测量矩形，行 40..160
            _strategy.inPara.HoRect.Dispose();
            _strategy.inPara.HoRect = NewAffRect(new Point2d(100, 100), 60, 120);
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            _display.Dispose();
            _image.Dispose();
        }

        [TestMethod]
        public void Identity()
        {
            Assert.AreEqual("fit.line", AlgoInfo.Of(_strategy).Key);
            Assert.AreEqual(RectEnum.AffRect, AlgoInfo.Of(_strategy).DefaultRoi);
            Assert.AreEqual("拟合直线", _strategy.Name);
        }

        [TestMethod]
        public void FitsVerticalEdge_WithTrimEnds()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            var line = _strategy.Line;
            Assert.AreEqual(99.5, line.Start.X, 0.5);
            Assert.AreEqual(99.5, line.End.X, 0.5);
            // 首尾各裁掉一个点：剩余 11 点，行 50..150
            Assert.AreEqual(50, Math.Min(line.Start.Y, line.End.Y), 0.5);
            Assert.AreEqual(150, Math.Max(line.Start.Y, line.End.Y), 0.5);

            Assert.AreEqual(11, _display.Points.Count(p => p.ColorName == HColor.Green.Name), "参与拟合的点为绿色");
            Assert.AreEqual(2, _display.Points.Count(p => p.ColorName == HColor.Red.Name), "被裁剪的首尾点为红色");
            Assert.AreEqual(1, _display.Arrows.Count);
            Assert.AreEqual(1, _display.Objects.Count(o => o.ColorName == HColor.Blue.Name), "显示查找区域");
            StringAssert.Contains(_display.LastText, "用点:11");
        }

        [TestMethod]
        public void TrimEndsOff_UsesAllPoints()
        {
            _strategy.inPara.TrimEnds = false;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            var line = _strategy.Line;
            Assert.AreEqual(40, Math.Min(line.Start.Y, line.End.Y), 0.5);
            Assert.AreEqual(160, Math.Max(line.Start.Y, line.End.Y), 0.5);
            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.Red.Name));
            StringAssert.Contains(_display.LastText, "用点:13");
        }

        [TestMethod]
        public void DisplayFlags_SuppressOverlay()
        {
            _strategy.inPara.DispRegion = false;
            _strategy.inPara.DispFixPoint = false;
            _strategy.inPara.DispResult = false;
            _strategy.inPara.DispText = false;
            _strategy.inPara.DispFixRegion = true;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(0, _display.Objects.Count);
            Assert.AreEqual(0, _display.Points.Count);
            Assert.AreEqual(0, _display.Arrows.Count);
            Assert.AreEqual(0, _display.Texts.Count);
            Assert.AreEqual(13, _display.Rect2Centers.Count, "拟合区域：每个测量矩形各画一次");
        }

        /// <summary> 单图验证没有上游：本地图像来源就是给的那张图 </summary>
        [TestMethod]
        public void ImageOverload_UsesGivenImage()
        {
            Assert.IsTrue(_strategy.OnImage(_image, new FakeDisplay()).IsOk);
            Assert.AreEqual(99.5, _strategy.Line.Start.X, 0.5);
        }

        [TestMethod]
        public void Headless_Computes()
        {
            Assert.IsTrue(_strategy.Run(RunContext.ForImage(_image), null).IsOk);
            Assert.AreEqual(99.5, _strategy.Line.Start.X, 0.5);
        }

        [TestMethod]
        public void MaxErr_RejectsOutlierPoint()
        {
            // 行 97..103 处边缘被挖成列 115: 只有行 100 那个测量矩形(半宽 2.5)落在缺口里, 得到一个 ~15px 的离群点
            using (var notch = Rectangle1(97, 100, 103, 114))
            using (var notched = Paint(_image, notch, 0))
            {
                _display.SetImage(notched);
                _strategy.inPara.TrimEnds = false;

                Assert.IsTrue(_strategy.On(_display).IsOk);
            }

            var line = _strategy.Line;
            Assert.AreEqual(99.5, line.Start.X, 0.5, "离群点被剔除后直线回到真实边缘");
            Assert.AreEqual(99.5, line.End.X, 0.5);

            var red = _display.Points.Where(p => p.ColorName == HColor.Red.Name).ToList();
            Assert.AreEqual(1, red.Count, "离群点以红色显示");
            Assert.AreEqual(114.5, red[0].Item.X, 0.5);
            Assert.AreEqual(100, red[0].Item.Y, 0.5);
            StringAssert.Contains(_display.LastText, "用点:12");
        }

        [TestMethod]
        public void MaxErr_Large_KeepsOutlierPoint()
        {
            using (var notch = Rectangle1(97, 100, 103, 114))
            using (var notched = Paint(_image, notch, 0))
            {
                _display.SetImage(notched);
                _strategy.inPara.TrimEnds = false;
                _strategy.inPara.MaxErr = 100;

                Assert.IsTrue(_strategy.On(_display).IsOk);
            }

            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.Red.Name));
            StringAssert.Contains(_display.LastText, "用点:13");
        }

        [DataTestMethod]
        [DataRow(EdgeSelect.First, 99.5)]
        [DataRow(EdgeSelect.Second, 129.5)]
        public void ContourType_SelectsEdge(EdgeSelect contourType, double expectedX)
        {
            // 亮带 100..119、暗带 120..129、其后再亮: 由黑到白的边缘在 99.5 与 129.5 各一条
            using (var dark = Rectangle1(0, 120, Size - 1, 129))
            using (var striped = Paint(_image, dark, 0))
            {
                _display.SetImage(striped);
                _strategy.inPara.HoRect.Dispose();
                _strategy.inPara.HoRect = NewAffRect(new Point2d(110, 100), 80, 120);
                _strategy.inPara.ContourType = contourType;

                Assert.IsTrue(_strategy.On(_display).IsOk);
            }

            Assert.AreEqual(expectedX, _strategy.Line.Start.X, 0.5);
            Assert.AreEqual(expectedX, _strategy.Line.End.X, 0.5);
        }

        [TestMethod]
        public void NoImage_Fails_WithToolNameInStatus()
        {
            var display = new FakeDisplay();
            var result = _strategy.On(display);

            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "图像来源为空");
            StringAssert.StartsWith(display.LastText, "拟合直线 : ");
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
        public void NoEdge_Fails()
        {
            _strategy.inPara.Transition = Transition.Negative;

            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "未找到足够的轮廓点");
        }

        [TestMethod]
        public void Failure_ResetsPreviousLine()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.IsFalse(_strategy.Line.IsDegenerate);

            _strategy.inPara.Transition = Transition.Negative;
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);

            // 下游照跑时必须拿到退化直线(下游会明确报错), 而不是上一轮的旧直线
            Assert.IsTrue(_strategy.Line.IsDegenerate);
        }

        [TestMethod]
        public void InvalidTransition_ClearError_ResetsPreviousLine()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            // 配置文件里手改出来的非法值: 必须报出配置错误, 而不是 HALCON 原生异常
            _strategy.inPara.Transition = (Transition)99;
            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "过渡方向");
            StringAssert.Contains(result.Message, "99", "报错要带出写错的原值");

            Assert.IsTrue(_strategy.Line.IsDegenerate);
        }

        [TestMethod]
        public void NoImage_ResetsPreviousLine()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(RunStatus.Error, _strategy.On(new FakeDisplay()).Status);

            Assert.IsTrue(_strategy.Line.IsDegenerate);
        }

        [TestMethod]
        public void ImageIn_ResolvesUpstreamImage()
        {
            using (var shifted = VerticalStepImage(Size, Size, 110))
            {
                var upstream = new StubStrategy("取像").Image("图像", () => shifted);
                _strategy.inPara.ImageIn = upstream.Ref("图像");

                Assert.IsTrue(_strategy.On(_display, upstream).IsOk);
                Assert.AreEqual(109.5, _strategy.Line.Start.X, 0.5, "应使用上游图像而不是窗口图像");
            }
        }

        [TestMethod]
        public void RegionIn_UsesUpstreamRegionButLocalGeometry()
        {
            using (var upstreamRegion = Rectangle1(0, 0, Size - 1, Size - 1))
            {
                var upstream = new StubStrategy("区域源").Region("区域", () => upstreamRegion);
                _strategy.inPara.RegionIn = upstream.Ref("区域");
                _display.CopyObjects = true;

                Assert.IsTrue(_strategy.On(_display, upstream).IsOk);
                Assert.AreEqual(99.5, _strategy.Line.Start.X, 0.5);
                var shown = _display.Objects.Single().Item;
                Assert.AreEqual(AreaCenter(upstreamRegion, out _), AreaCenter(shown, out _), "显示的查找区域就是上游区域");
                Assert.IsTrue(upstreamRegion.IsInitialized(), "上游区域是借用的");
            }
        }

        [TestMethod]
        public void RegionIn_UpstreamRegionExcludingEdge_Fails()
        {
            // measure_pos 忽略定义域：必须先滤掉域外点，否则区域外的边照样被找到并拟合成功
            using (var upstreamRegion = Rectangle1(0, 0, Size - 1, 90))
            {
                var upstream = new StubStrategy("区域源").Region("区域", () => upstreamRegion);
                _strategy.inPara.RegionIn = upstream.Ref("区域");

                var result = _strategy.On(_display, upstream);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "未找到足够的轮廓点");
                Assert.IsTrue(_strategy.Line.IsDegenerate);
            }
        }

        [TestMethod]
        public void RegionIn_UpstreamRegion_LimitsPointsToRegion()
        {
            // 上游区域只覆盖行 0..100：测量矩形行 40..100 共 7 个有点，裁剪首尾后行 50..90
            using (var upstreamRegion = Rectangle1(0, 0, 100, Size - 1))
            {
                var upstream = new StubStrategy("区域源").Region("区域", () => upstreamRegion);
                _strategy.inPara.RegionIn = upstream.Ref("区域");

                Assert.IsTrue(_strategy.On(_display, upstream).IsOk);

                var line = _strategy.Line;
                Assert.AreEqual(50, Math.Min(line.Start.Y, line.End.Y), 0.5);
                Assert.AreEqual(90, Math.Max(line.Start.Y, line.End.Y), 0.5);
                StringAssert.Contains(_display.LastText, "用点:5");
            }
        }

        [TestMethod]
        public void SigmaZero_StillFits()
        {
            // "滤波"常用值里有 0：直接传给 measure_pos 会抛 HALCON #1302，管线里钳到 0.4
            _strategy.inPara.Sigma = 0;

            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(99.5, _strategy.Line.Start.X, 0.5);
        }

        [TestMethod]
        public void MaxErrZero_DisablesRefinement()
        {
            _strategy.inPara.MaxErr = 0;
            _strategy.inPara.TrimEnds = false;

            Assert.IsTrue(_strategy.On(_display).IsOk);
            StringAssert.Contains(_display.LastText, "用点:13");
            Assert.AreEqual(0, _display.Points.Count(p => p.ColorName == HColor.Red.Name));
        }

        [TestMethod]
        public void RegionIn_Unresolvable_Fails()
        {
            _strategy.inPara.RegionIn = new StubStrategy("区域源").Ref("区域");
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);
        }

        [TestMethod]
        public void CoordIn_TranslatesRoi()
        {
            using (var image = VerticalStepImage(Size, Size, 130))
            {
                _display.SetImage(image);
                var locator = StubStrategy.Coord("定位", new Point2d(100, 100), new CvCoord(130, 100));
                _strategy.inPara.CoordIn = locator.Ref("坐标系");

                Assert.IsTrue(_strategy.On(_display, locator).IsOk);

                Assert.AreEqual(129.5, _strategy.Line.Start.X, 0.5, "ROI 应随坐标系平移 +30 列");
                Assert.AreEqual(129.5, _strategy.Line.End.X, 0.5);
            }
        }

        [TestMethod]
        public void CoordIn_RotatesMeasureDirection()
        {
            // 水平阶跃边在行 100；配置态 ROI 沿列测量，坐标系转 90° 后改为沿行测量。
            using (var dark = ConstImage(Size, Size, 0))
            using (var bottom = Rectangle1(100, 0, Size - 1, Size - 1))
            using (var image = Paint(dark, bottom, 255))
            {
                _display.SetImage(image);
                _strategy.inPara.Transition = Transition.All;
                var locator = StubStrategy.Coord("定位", new Point2d(100, 100), CvCoord.FromDegrees(100, 100, 90));
                _strategy.inPara.CoordIn = locator.Ref("坐标系");

                Assert.IsTrue(_strategy.On(_display, locator).IsOk);

                var line = _strategy.Line;
                Assert.AreEqual(99.5, line.Start.Y, 0.5);
                Assert.AreEqual(99.5, line.End.Y, 0.5);
                Assert.AreEqual(100, Math.Abs(line.End.X - line.Start.X), 1, "11 个点沿列展开 100 像素");
            }
        }

        [TestMethod]
        public void CoordIn_RotationWithTranslation_RotatesAroundTmplPointThenMoves()
        {
            // 示教原点 (100,100) → 当前 (130,120) 且转 90°：ROI 中心本就在示教原点，跟随后落在 (130,120)，
            // 改为沿行测量；水平阶跃边在行 120。旋转中心或平移顺序写错时 ROI 会落在别处而找不到边。
            using (var dark = ConstImage(Size, Size, 0))
            using (var bottom = Rectangle1(120, 0, Size - 1, Size - 1))
            using (var image = Paint(dark, bottom, 255))
            {
                _display.SetImage(image);
                _strategy.inPara.Transition = Transition.All;
                var locator = StubStrategy.Coord("定位", new Point2d(100, 100), CvCoord.FromDegrees(130, 120, 90));
                _strategy.inPara.CoordIn = locator.Ref("坐标系");

                Assert.IsTrue(_strategy.On(_display, locator).IsOk);

                var line = _strategy.Line;
                Assert.AreEqual(119.5, line.Start.Y, 0.5);
                Assert.AreEqual(119.5, line.End.Y, 0.5);
                Assert.AreEqual(130, (line.Start.X + line.End.X) / 2, 1, "点沿列以跟随后的中心 130 对称展开");
                Assert.AreEqual(100, Math.Abs(line.End.X - line.Start.X), 1);
            }
        }

        [TestMethod]
        public void CoordIn_NotTaught_Fails()
        {
            var locator = StubStrategy.Coord("定位", null, new CvCoord(130, 100));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            var result = _strategy.On(_display, locator);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "示教");
        }

        [TestMethod]
        public void Outputs_TreeAndResolution()
        {
            var tree = FakeTree.Of(_strategy);
            Assert.IsTrue(_strategy.On(_display).IsOk);

            CollectionAssert.IsSubsetOf(new[] { "拟合直线", "拟合直线/直线", "拟合直线/直线/起点/行", "拟合直线/直线/终点/列", "拟合直线/结果" }, tree.Paths);
            Assert.AreEqual(OutEnum.Line, tree.Types["拟合直线/直线"]);
            Assert.AreEqual(OutEnum.Point, tree.Types["拟合直线/直线/起点"]);

            var ctx = new RunContext(null, new IParaStrategy[] { _strategy });
            var line = ctx.Resolve<CvLine>(_strategy.Ref("直线"));
            Assert.AreEqual(_strategy.Line, line);
            Assert.AreEqual(line.Start, ctx.Resolve<Point2d>(_strategy.Ref("直线/起点")));
            Assert.AreEqual(line.End, ctx.Resolve<Point2d>(_strategy.Ref("直线/终点")));
            Assert.AreEqual(line.Start.Y, ctx.Resolve<double>(_strategy.Ref("直线/起点/行")));
            Assert.AreEqual(line.Start.X, ctx.Resolve<double>(_strategy.Ref("直线/起点/列")));
            Assert.AreEqual(line.End.Y, ctx.Resolve<double>(_strategy.Ref("直线/终点/行")));
            Assert.AreEqual(line.End.X, ctx.Resolve<double>(_strategy.Ref("直线/终点/列")));
        }

        /// <summary> 输出是声明出来的，不再依赖"先生成过变量树才登记解析器" </summary>
        [TestMethod]
        public void Outputs_AvailableWithoutTree()
        {
            Assert.IsNotNull(_strategy.FindOutput("直线"));
            Assert.IsTrue(((CvLine)_strategy.FindOutput("直线").GetValue()).IsDegenerate, "未运行时是退化线段");
            Assert.IsNull(_strategy.FindOutput("不存在"));
        }

        [TestMethod]
        public void Params_RoundTripThroughDeclaration()
        {
            var upstream = StubStrategy.Coord("定位", new Point2d(), new CvCoord());
            Assert.IsTrue(_strategy.SetParam("跟随坐标", upstream.Ref("坐标系")));
            Assert.IsTrue(_strategy.SetParam("过渡方向", Transition.All));
            Assert.IsTrue(_strategy.SetParam("选择", EdgeSelect.Last));
            Assert.IsTrue(_strategy.SetParam("滤波", 2));
            Assert.IsTrue(_strategy.SetParam("阈值", 33));
            Assert.IsTrue(_strategy.SetParam("步距", 7));
            Assert.IsTrue(_strategy.SetParam("步宽", 3));
            Assert.IsTrue(_strategy.SetParam("最大偏差", 9));
            Assert.IsTrue(_strategy.SetParam("裁剪首尾", false));
            Assert.IsTrue(_strategy.SetParam("显示文本", false));
            Assert.IsTrue(_strategy.SetParam("拟合区域", true));
            Assert.IsTrue(_strategy.SetParam("文本X", 20));

            var p = _strategy.inPara;
            Assert.AreEqual(upstream.Ref("坐标系"), p.CoordIn);
            Assert.AreEqual(Transition.All, p.Transition);
            Assert.AreEqual(EdgeSelect.Last, p.ContourType);
            Assert.AreEqual(2, p.Sigma);
            Assert.AreEqual(33, p.Threshold);
            Assert.AreEqual(7, p.StepPace);
            Assert.AreEqual(3, p.StepWidth);
            Assert.AreEqual(9, p.MaxErr);
            Assert.IsFalse(p.TrimEnds);
            Assert.IsFalse(p.DispText);
            Assert.IsTrue(p.DispFixRegion);
            Assert.AreEqual(20, p.FontX);

            // 参数类序列化后读回, 来源与枚举都还在
            var back = Newtonsoft.Json.JsonConvert.DeserializeObject<FitLine>(Newtonsoft.Json.JsonConvert.SerializeObject(p));
            Assert.AreEqual(p.CoordIn, back.CoordIn);
            Assert.AreEqual(Transition.All, back.Transition);
            Assert.AreEqual(EdgeSelect.Last, back.ContourType);
            back.HoRect.Dispose();
        }

        [TestMethod]
        public async Task DrawROIAsync_Cancelled_RestoresType()
        {
            var host = new FakeInteractionHost { Confirm = false };

            await _strategy.DrawROIAsync(host, RectEnum.Circle, true);

            Assert.AreEqual(RectEnum.Circle, host.TypeDuringDraw, "绘制前要先写入 Type");
            Assert.AreEqual(RectEnum.AffRect, _strategy.inPara.HoRect.Type, "取消后 Type 还原");
            Assert.AreEqual(1, host.FakeDisplay.Regions.Count, "取消后仍把原 ROI 画回去");
            Assert.AreEqual(1, host.ShowRoiCount);
        }

        [TestMethod]
        public async Task DrawROIAsync_Confirmed_KeepsNewType()
        {
            var host = new FakeInteractionHost();

            await _strategy.DrawROIAsync(host, RectEnum.Rectangle, true);

            Assert.AreEqual(RectEnum.Rectangle, _strategy.inPara.HoRect.Type);
            Assert.AreEqual(1, host.DrawCount);
        }

        [TestMethod]
        public async Task DrawROIAsync_Modify_UsesModApi()
        {
            var host = new FakeInteractionHost();

            await _strategy.DrawROIAsync(host, RectEnum.Rectangle, false);

            Assert.AreEqual(0, host.DrawCount);
            Assert.AreEqual(1, host.DrawModCount);
            Assert.AreEqual(RectEnum.AffRect, _strategy.inPara.HoRect.Type, "修改模式不改 Type");
        }

        /// <summary> 显示 ROI 不能顺手改配置（原来每次选中工具都把类型强改成仿射矩形） </summary>
        [TestMethod]
        public void DispROI_DoesNotMutateConfig()
        {
            _strategy.inPara.HoRect.Type = RectEnum.Circle;
            var host = new FakeInteractionHost();

            _strategy.DispROI(host);

            Assert.AreEqual(RectEnum.Circle, _strategy.inPara.HoRect.Type);
            Assert.AreEqual(1, host.ShowRoiCount);
        }

        [TestMethod]
        public void Dispose_IsIdempotent_AndReleasesRoi()
        {
            var roi = _strategy.inPara.HoRect;
            _strategy.Dispose();
            _strategy.Dispose();
            Assert.IsNull(roi.HoRegion);
        }
    }
}
