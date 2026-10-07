using System;
using System.IO;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 形状 / 灰度 / 缩放 / 通用四种匹配的公共行为：流程都在 <see cref="MatchStrategyBase{TPara}"/> 里，
    /// 只有 HALCON 算子不同。测试方法由 MSTest 从基类继承执行。
    /// </summary>
    public abstract class MatchingStrategyTestBase<TStrategy, TPara> : HalconTestBase
        where TStrategy : MatchStrategyBase<TPara>, new()
        where TPara : MatchParaBase, new()
    {
        private const int W = 200, H = 160;

        /// <summary>模板框：左上 (35,25)，80×80 → 中心 (75,65)，距图像边界留足 SaveSmallestRectImage 的 20px 外扩。</summary>
        private static readonly Rect2d TemplateBounds = new Rect2d(35.0, 25.0, 80.0, 80.0);
        private static readonly Point2d TemplateCenter = new Point2d(75, 65);

        private string _projectDir;
        private HObject _image;
        protected TStrategy Strategy;
        private FakeDisplay _display;

        protected abstract string ExpectedName { get; }
        protected abstract string ExpectedKey { get; }

        private protected FakeDisplay Display => _display;

        /// <summary>
        /// 暗背景上的亮 L 形：竖条行 40..90 × 列 50..60，横条行 80..90 × 列 50..100，整体平移 (dx, dy)。
        /// <paramref name="scale"/> 绕模板中心 (75,65) 缩放, 1 时与原尺寸逐像素一致。
        /// </summary>
        private protected static HObject LImage(int dx = 0, int dy = 0, double scale = 1)
        {
            int R(double r) => (int)Math.Round(TemplateCenter.Y + (r - TemplateCenter.Y) * scale) + dy;
            int C(double c) => (int)Math.Round(TemplateCenter.X + (c - TemplateCenter.X) * scale) + dx;

            using (var dark = ConstImage(W, H, 0))
            using (var bar1 = Rectangle1(R(40), C(50), R(90), C(60)))
            using (var bar2 = Rectangle1(R(80), C(50), R(90), C(100)))
            using (var step = Paint(dark, bar1, 255))
            {
                return Paint(step, bar2, 255);
            }
        }

        /// <summary>
        /// 宽 2W 的图上左右各一个 L: 一个完整, 另一个横条只剩一半 (得分明显更低)。
        /// 左半 (列 0..W-1) 与右半 (列 W..2W-1) 各做一个查找 ROI, 用来验证跨 ROI 取全局最佳。
        /// </summary>
        private static HObject TwoLImage(bool perfectOnLeft)
        {
            int perfectDx = perfectOnLeft ? 0 : W, degradedDx = perfectOnLeft ? W : 0;
            using (var dark = ConstImage(2 * W, H, 0))
            using (var a1 = Rectangle1(40, 50 + perfectDx, 90, 60 + perfectDx))
            using (var a2 = Rectangle1(80, 50 + perfectDx, 90, 100 + perfectDx))
            using (var b1 = Rectangle1(40, 50 + degradedDx, 90, 60 + degradedDx))
            using (var b2 = Rectangle1(80, 50 + degradedDx, 90, 75 + degradedDx))
            using (var s1 = Paint(dark, a1, 255))
            using (var s2 = Paint(s1, a2, 255))
            using (var s3 = Paint(s2, b1, 255))
            {
                return Paint(s3, b2, 255);
            }
        }

        /// <summary>宽 2W 的图上左右各一个完全相同的 L。</summary>
        private static HObject TwinLImage()
        {
            using (var dark = ConstImage(2 * W, H, 0))
            using (var a1 = Rectangle1(40, 50, 90, 60))
            using (var a2 = Rectangle1(80, 50, 90, 100))
            using (var b1 = Rectangle1(40, 50 + W, 90, 60 + W))
            using (var b2 = Rectangle1(80, 50 + W, 90, 100 + W))
            using (var s1 = Paint(dark, a1, 255))
            using (var s2 = Paint(s1, a2, 255))
            using (var s3 = Paint(s2, b1, 255))
            {
                return Paint(s3, b2, 255);
            }
        }

        /// <summary>轮廓 / 区域的外接列范围 (XLD 与 region 两种都收: NCC 的"轮廓"是区域)。</summary>
        private protected static void ColumnRange(HObject obj, out double minCol, out double maxCol)
        {
            HOperatorSet.GetObjClass(obj, out HTuple cls);
            HTuple c1, c2;
            if (cls[0].S.StartsWith("xld"))
                HOperatorSet.SmallestRectangle1Xld(obj, out _, out c1, out _, out c2);
            else
                HOperatorSet.SmallestRectangle1(obj, out _, out c1, out _, out c2);
            minCol = c1.TupleMin().D;
            maxCol = c2.TupleMax().D;
        }

        [TestInitialize]
        public void SetUpMatching()
        {
            _projectDir = Path.Combine(Path.GetTempPath(), "DotNet.HalconAlgo.Tests", Guid.NewGuid().ToString("N"));
            _image = LImage();
            _display = new FakeDisplay();
            Strategy = new TStrategy { DataDir = Path.Combine(_projectDir, "tool") };
        }

        [TestCleanup]
        public void TearDownMatching()
        {
            Strategy.Dispose();
            _image.Dispose();
            if (Directory.Exists(_projectDir)) Directory.Delete(_projectDir, true);
        }

        private FakeRoiHost TemplateHost(bool confirm = true)
        {
            var host = new FakeRoiHost
            {
                Confirm = confirm,
                OnDraw = r =>
                {
                    r.Bounds = TemplateBounds;
                    r.RebuildRegion();
                },
            };
            host.FakeDisplay.SetImage(_image);
            return host;
        }

        private protected FakeRoiHost CreateTemplate()
        {
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();
            return host;
        }

        /// <summary>替换查找 ROI，并释放原 ROI 的区域句柄。</summary>
        private protected void ReplaceHoRect(CvRegion region)
        {
            Strategy.inPara.HoRect.Dispose();
            Strategy.inPara.HoRect = region;
        }

        /// <summary>整图查找 ROI。</summary>
        private protected void UseFullImageRoi() => ReplaceHoRect(NewRegion(RectEnum.Rectangle, 0, 0, W - 1, H - 1));

        private Point2d Tmpl => Strategy.inPara.TmplPoint.Value;

        private void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore)
        {
            Strategy.inPara.AngleStart = angleStartDeg;
            Strategy.inPara.AngleExtent = angleExtentDeg;
            Strategy.inPara.MinScore = minScore;
        }

        [TestMethod]
        public void Identity()
        {
            Assert.AreEqual(ExpectedName, Strategy.Name);
            Assert.AreEqual(ExpectedKey, AlgoInfo.Of(Strategy).Key);
            Assert.IsInstanceOfType(Strategy, typeof(ITemplateEditable));
            Assert.IsInstanceOfType(Strategy, typeof(IRoiEditable));
        }

        [TestMethod]
        public void SetTemplate_CreatesModel_TeachesTmplPoint_SavesFiles()
        {
            var host = CreateTemplate();

            Assert.IsTrue(Strategy.HasModel);
            Assert.AreEqual(RectEnum.Rectangle, Strategy.inPara.ModeRect.Type);

            Assert.AreEqual(TemplateCenter.X, Tmpl.X, 1.0, "模板原点取模板区域重心");
            Assert.AreEqual(TemplateCenter.Y, Tmpl.Y, 1.0);
            Assert.AreEqual(Tmpl, Strategy.Coord.Center);
            Assert.AreEqual(1, Strategy.Results.Count);

            string expectedPath = Path.Combine(Strategy.DataDir, "matching.bmp");
            Assert.AreEqual(expectedPath, Strategy.GetTemplateView().ModelPath);
            Assert.AreEqual(expectedPath, host.DonePath);
            Assert.IsTrue(File.Exists(expectedPath), "模板图应落盘到工具的数据目录");
            Assert.AreEqual(2, Directory.GetFiles(Strategy.DataDir).Length, "模板图 + 模型文件, 不留临时文件");
            Assert.IsTrue(host.DoneResult.HasValue);
            Assert.AreEqual(1, host.SetModelParaCount);

            Assert.AreEqual("新建模板成功！", host.FakeDisplay.LastText);
            Assert.AreEqual(HColor.Green.Name, host.FakeDisplay.Texts[0].ColorName);
            Assert.AreEqual(HColor.Orange.Name, host.FakeDisplay.Regions[0].ColorName);
        }

        [TestMethod]
        public void SetTemplate_Cancel_HasNoSideEffects()
        {
            var host = TemplateHost(confirm: false);

            Strategy.SetTemplateAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult();

            Assert.AreEqual(RectEnum.Circle, host.TypeDuringDraw);
            Assert.AreEqual(RectEnum.Rectangle, Strategy.inPara.ModeRect.Type, "取消后类型还原");
            Assert.IsFalse(Strategy.HasModel);
            Assert.AreEqual(string.Empty, Strategy.GetTemplateView().ModelPath);
            Assert.IsNull(Strategy.inPara.TmplPoint);
            Assert.IsNull(host.DonePath);
            Assert.AreEqual(0, host.SetModelParaCount);
            Assert.AreEqual(1, host.FakeDisplay.Regions.Count, "原模板区域重画回去");
            Assert.AreEqual(HColor.Orange.Name, host.FakeDisplay.Regions[0].ColorName);
            Assert.IsFalse(Directory.Exists(Strategy.DataDir));
        }

        [TestMethod]
        public void SetTemplate_Cancel_KeepsExistingModel()
        {
            CreateTemplate();
            var id = Strategy.ModelID;
            var tmpl = Tmpl;

            Strategy.SetTemplateAsync(TemplateHost(confirm: false), RectEnum.Circle, false).GetAwaiter().GetResult();

            Assert.AreSame(id, Strategy.ModelID);
            Assert.AreEqual(tmpl, Tmpl);
        }

        [TestMethod]
        public void SetTemplate_Modify_UsesModDraw()
        {
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            Assert.AreEqual(0, host.DrawCount);
            Assert.AreEqual(1, host.DrawModCount);
            Assert.IsTrue(Strategy.HasModel);
        }

        [TestMethod]
        public void SetTemplate_NoImage_Throws()
        {
            var host = TemplateHost();
            host.FakeDisplay.HoImage = null;

            var ex = Assert.ThrowsException<InvalidOperationException>(
                () => Strategy.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult());
            StringAssert.Contains(ex.Message, ExpectedName);
        }

        [TestMethod]
        public void Run_FindsShiftedTarget()
        {
            CreateTemplate();
            var tmpl = Tmpl;
            UseFullImageRoi();

            using (var shifted = LImage(dx: 30, dy: 20))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.On(_display).IsOk);
            }

            Assert.AreEqual(1, Strategy.Results.Count);
            var coord = Strategy.Coord;
            Assert.AreEqual(tmpl.X + 30, coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 20, coord.Y, 1.0);
            Assert.AreEqual(0, coord.AngleDegrees, 1.0);
            Assert.IsTrue(Strategy.Results[0].Score > 0.9);

            StringAssert.StartsWith(_display.LastText, ExpectedName + " : 数量:1 最佳得分:");
            Assert.AreEqual(HColor.Green.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(1, _display.Objects.FindAll(o => o.ColorName == HColor.Blue.Name).Count, "查找区域");
            Assert.AreEqual(1, _display.Objects.FindAll(o => o.ColorName == HColor.Green.Name).Count, "模板轮廓");
            Assert.AreEqual(1, _display.Coords.Count, "匹配点");
        }

        [TestMethod]
        public void Run_Outputs_CoordWithTemplate()
        {
            CreateTemplate();
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);

            var tree = FakeTree.Of(Strategy);
            CollectionAssert.IsSubsetOf(new[] { ExpectedName + "/坐标系", ExpectedName + "/坐标系/原点/行", ExpectedName + "/坐标系/角度" }, tree.Paths);

            var ctx = new RunContext(null, new IParaStrategy[] { Strategy });
            var follow = ctx.ResolveCoord(Strategy.Ref("坐标系"));
            Assert.AreEqual(Strategy.Coord, follow.Current);
            Assert.AreEqual(Tmpl, follow.Template);
            Assert.AreEqual(Strategy.Coord.Y, ctx.Resolve<double>(Strategy.Ref("坐标系/原点/行")));
            Assert.AreEqual(Strategy.Coord.Angle.Radians, ctx.Resolve<double>(Strategy.Ref("坐标系/角度")));
        }

        [TestMethod]
        public void Run_NoMatch_Fails_RedText_ResetsPreviousResult()
        {
            CreateTemplate();
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);
            Assert.AreEqual(1, Strategy.Results.Count);

            using (var blank = ConstImage(W, H, 0))
            {
                _display.SetImage(blank);
                Assert.AreEqual(RunStatus.Error, Strategy.On(_display).Status, "一个都没找到不该长得跟成功一样");
            }

            Assert.AreEqual(0, Strategy.Results.Count);
            Assert.AreEqual(new CvCoord(), Strategy.Coord, "没有匹配时不能留着上一轮的坐标系");
            StringAssert.Contains(_display.LastText, "数量:0");
            Assert.AreEqual(HColor.Red.Name, _display.Texts[_display.Texts.Count - 1].ColorName);
        }

        [TestMethod]
        public void Run_RoiNotDrawn_Fails_ResetsResult()
        {
            CreateTemplate();
            Assert.AreNotEqual(new CvCoord(), Strategy.Coord);
            _display.SetImage(_image);

            Assert.AreEqual(RunStatus.Error, Strategy.On(_display).Status);

            Assert.AreEqual($"{ExpectedName} : 尚未绘制 ROI", _display.LastText);
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(0, Strategy.Results.Count);
            Assert.AreEqual(new CvCoord(), Strategy.Coord);
            Assert.AreEqual(0, Strategy.Contour.CountObj());
        }

        [TestMethod]
        public void Run_NoImage_Fails()
        {
            // 未建模板的校验先于取图, 要走到取图这一步必须先有模板
            CreateTemplate();
            var display = new FakeDisplay();
            var result = Strategy.On(display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "图像来源为空");
            StringAssert.StartsWith(display.LastText, ExpectedName + " : ");
        }

        [TestMethod]
        public void Run_RegionIn_Unresolvable_Fails()
        {
            CreateTemplate();
            Strategy.inPara.RegionIn = new StubStrategy("上游").Ref("区域");
            _display.SetImage(_image);
            Assert.AreEqual(RunStatus.Error, Strategy.On(_display).Status);
        }

        /// <summary>
        /// 跟随坐标：本地查找 ROI 只框住示教位置，工件平移 (30,20) 后必须随上游坐标系一起搬过去才找得到。
        /// </summary>
        [TestMethod]
        public void Run_CoordIn_MovesLocalRegionWithUpstreamCoord()
        {
            CreateTemplate();
            var tmpl = Tmpl;
            // 只框住示教位置的 L (行 40..90 × 列 50..100), 平移后的 L 落在框外
            ReplaceHoRect(NewRegion(RectEnum.Rectangle, 45, 35, 60, 60));
            var upstream = StubStrategy.Coord("定位", tmpl, new CvCoord(new Point2d(tmpl.X + 30, tmpl.Y + 20), Angle.FromRadians(0)));
            Strategy.inPara.CoordIn = upstream.Ref("坐标系");

            using (var shifted = LImage(dx: 30, dy: 20))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.On(_display, upstream).IsOk);
            }

            Assert.AreEqual(1, Strategy.Results.Count, "查找区域没跟着搬过去就找不到平移后的目标");
            Assert.AreEqual(tmpl.X + 30, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 20, Strategy.Coord.Y, 1.0);
        }

        [TestMethod]
        public void Run_CoordIn_Unresolvable_Fails()
        {
            CreateTemplate();
            UseFullImageRoi();
            Strategy.inPara.CoordIn = StubStrategy.Coord("上游", new Point2d(), new CvCoord()).Ref("坐标系");
            _display.SetImage(_image);
            Assert.AreEqual(RunStatus.Error, Strategy.On(_display).Status);
        }

        [TestMethod]
        public void Run_WithoutModel_Fails_NoImageNeeded_ResetsResult()
        {
            CreateTemplate();
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);
            Assert.AreEqual(1, Strategy.Results.Count);

            // 模型被清掉: 空的 ModelID 若传进查找算子, 报出的是与真实原因无关的 HALCON 参数错误
            var id = Strategy.ModelID;
            Strategy.ModelID = null;
            try
            {
                var display = new FakeDisplay();   // 连图像都不需要: 前置校验先于取图
                Assert.AreEqual(RunStatus.Error, Strategy.On(display).Status);

                Assert.AreEqual(ExpectedName + " : 未建立模板，无法执行匹配！", display.LastText);
                Assert.AreEqual(HColor.Red.Name, display.Texts[0].ColorName);
                Assert.AreEqual(0, Strategy.Results.Count, "不能留着上一轮的匹配结果");
                Assert.AreEqual(new CvCoord(), Strategy.Coord);
            }
            finally
            {
                Strategy.ModelID = id;
            }
        }

        /// <summary>两个查找 ROI (左右半幅, 经上游区域输出给入), 返回本轮的示教原点。</summary>
        private Point2d RunOnTwoRois(bool perfectOnLeft)
        {
            // 残缺 L 的得分约 0.7, 放低门槛保证两个 ROI 都有结果, 这样"取哪一个"才有意义
            SetSearchRange(-90, 180, 0.5);
            CreateTemplate();
            var tmpl = Tmpl;

            using (var image = TwoLImage(perfectOnLeft))
            using (var left = Rectangle1(0, 0, H - 1, W - 1))
            using (var right = Rectangle1(0, W, H - 1, 2 * W - 1))
            {
                HOperatorSet.ConcatObj(left, right, out HObject rois);
                try
                {
                    var upstream = new StubStrategy("上游").Region("区域", () => rois);
                    Strategy.inPara.RegionIn = upstream.Ref("区域");
                    _display.SetImage(image);
                    Assert.IsTrue(Strategy.On(_display, upstream).IsOk);
                }
                finally
                {
                    rois.Dispose();
                }
            }
            return tmpl;
        }

        private void AssertBestIsPerfectL(Point2d tmpl, int perfectDx)
        {
            var results = Strategy.Results;
            Assert.AreEqual(2, results.Count, "两个 ROI 各一个结果");
            Assert.IsTrue(results[0].Score > results[1].Score, "Results[0] 必须是全局最佳");

            var coord = Strategy.Coord;
            Assert.AreEqual(tmpl.X + perfectDx, coord.X, 1.0, "坐标系取全局最佳, 而不是第一个 ROI 的最佳");
            Assert.AreEqual(tmpl.Y, coord.Y, 1.0);
            StringAssert.StartsWith(_display.LastText, $"{ExpectedName} : 数量:2 最佳得分:{results[0].Score:F3}");

            // 轮廓与坐标系是一对(编辑模板窗口配对显示), 必须同是最佳实例的
            ColumnRange(Strategy.Contour, out double minCol, out double maxCol);
            Assert.AreEqual(TemplateCenter.X + perfectDx, (minCol + maxCol) / 2, 10.0, "Contour 必须是最佳实例的轮廓");
        }

        [TestMethod]
        public void Run_MultiRoi_BestInSecondRoi_CoordFollowsGlobalBest()
        {
            var tmpl = RunOnTwoRois(perfectOnLeft: false);
            AssertBestIsPerfectL(tmpl, W);
        }

        [TestMethod]
        public void Run_MultiRoi_BestInFirstRoi_ContourIsBestNotLast()
        {
            var tmpl = RunOnTwoRois(perfectOnLeft: true);
            AssertBestIsPerfectL(tmpl, 0);
        }

        [TestMethod]
        public void Run_NoMatch_ClearsPreviousContour()
        {
            CreateTemplate();
            Assert.IsTrue(Strategy.Contour.CountObj() > 0);
            UseFullImageRoi();

            using (var blank = ConstImage(W, H, 0))
            {
                _display.SetImage(blank);
                Strategy.On(_display);
            }

            Assert.AreEqual(0, Strategy.Contour.CountObj(), "没有匹配时不能留着上一轮的轮廓配零坐标系");
        }

        [TestMethod]
        public void Run_ImageOverload_UsesGivenImage()
        {
            CreateTemplate();
            var tmpl = Tmpl;
            UseFullImageRoi();

            using (var shifted = LImage(dx: 20, dy: 10))
            {
                Assert.IsTrue(Strategy.OnImage(shifted, new FakeDisplay()).IsOk);
            }

            Assert.AreEqual(tmpl.X + 20, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 10, Strategy.Coord.Y, 1.0);
        }

        [TestMethod]
        public void Run_ImageIn_ResolvesUpstreamImage()
        {
            CreateTemplate();
            var tmpl = Tmpl;
            UseFullImageRoi();
            _display.SetImage(_image);   // 显示窗口里是未平移的图: 必须用上游那张

            using (var shifted = LImage(dx: 25, dy: 15))
            {
                var camera = new StubStrategy("取像").Image("图像", () => shifted);
                Strategy.inPara.ImageIn = camera.Ref("图像");
                Assert.IsTrue(Strategy.On(_display, camera).IsOk);
            }

            Assert.AreEqual(tmpl.X + 25, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 15, Strategy.Coord.Y, 1.0);
        }

        [TestMethod]
        public void Run_DisplayFlagsOff_DrawsNothing()
        {
            CreateTemplate();
            UseFullImageRoi();
            Strategy.inPara.DispText = false;
            Strategy.inPara.DispRegion = false;
            Strategy.inPara.DispContour = false;
            Strategy.inPara.DispPoint = false;
            _display.SetImage(_image);

            Assert.IsTrue(Strategy.On(_display).IsOk);

            Assert.AreEqual(1, Strategy.Results.Count, "关显示不影响匹配本身");
            Assert.AreEqual(0, _display.Texts.Count);
            Assert.AreEqual(0, _display.Objects.Count);
            Assert.AreEqual(0, _display.Coords.Count);
        }

        [TestMethod]
        public void Run_ExceptionMidLoop_ResetsPartialResults()
        {
            CreateTemplate();
            // 上游"区域"第 1 个是整图矩形 (能匹配到), 第 2 个是 XLD: reduce_domain 在循环中途抛异常。
            // 此时第 1 个 ROI 的结果已经产出, 不清就把半截结果留给"编辑模板"和下游
            HOperatorSet.GenContourPolygonXld(out HObject xld, new HTuple(0.0, 10.0), new HTuple(0.0, 10.0));
            using (xld)
            using (var roi = Rectangle1(0, 0, H - 1, W - 1))
            {
                HOperatorSet.ConcatObj(roi, xld, out HObject mixed);
                using (mixed)
                {
                    var upstream = new StubStrategy("上游").Region("区域", () => mixed);
                    Strategy.inPara.RegionIn = upstream.Ref("区域");
                    _display.SetImage(_image);

                    Assert.AreEqual(RunStatus.Error, Strategy.On(_display, upstream).Status, "第 2 个 ROI 是 XLD, HALCON 报错转成失败");
                }
            }

            Assert.AreEqual(0, Strategy.Results.Count);
            Assert.AreEqual(new CvCoord(), Strategy.Coord);
            Assert.AreEqual(0, Strategy.Contour.CountObj());
        }

        [TestMethod]
        public void SetTemplate_Repeated_ReplacesModel_StillMatches()
        {
            CreateTemplate();
            var first = Strategy.ModelID;

            CreateTemplate();

            Assert.AreNotSame(first, Strategy.ModelID, "重建模板必须换成新句柄(旧句柄已释放)");
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);
            Assert.AreEqual(1, Strategy.Results.Count);
            Assert.AreEqual(Tmpl.X, Strategy.Coord.X, 1.0);
            Assert.AreEqual(Tmpl.Y, Strategy.Coord.Y, 1.0);
        }

        [TestMethod]
        public void SetTemplate_TrialNoMatch_RedText_KeepsPreviousModel()
        {
            CreateTemplate();
            var id = Strategy.ModelID;
            var tmpl = Tmpl;

            // 新模板只搜 85°..95° 且要求 0.9 分: 未旋转的 L 形转 90° 后最多与自身一条边重合, 试匹配必然 0 个结果
            SetSearchRange(85, 10, 0.9);
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            Assert.AreEqual("新建模板失败！", host.FakeDisplay.LastText);
            Assert.AreEqual(HColor.Red.Name, host.FakeDisplay.Texts[0].ColorName);
            Assert.AreSame(id, Strategy.ModelID, "试匹配失败不能丢掉旧模板");
            Assert.AreEqual(tmpl, Tmpl, "旧模板与旧示教原点必须仍是一对");
            Assert.IsNull(host.DonePath);
            Assert.AreEqual(0, host.SetModelParaCount);

            // 旧模板句柄仍然可用: 恢复查找参数后照常匹配到示教位置
            SetSearchRange(-90, 180, 0.6);
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);
            Assert.AreEqual(1, Strategy.Results.Count);
            Assert.AreEqual(tmpl.X, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y, Strategy.Coord.Y, 1.0);
        }

        [TestMethod]
        public void SetTemplate_TrialNoMatch_FirstTime_LeavesNoTrace()
        {
            // 失败轮次不能留下半截状态: 一张根本没写出的模板图, 或者已是新画的几何却没有配套模板
            var bounds = Strategy.inPara.ModeRect.Bounds;
            bool hadRegion = Strategy.inPara.ModeRect.HoRegion.IsUsableRegion();

            SetSearchRange(85, 10, 0.9);
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();

            Assert.AreEqual("新建模板失败！", host.FakeDisplay.LastText);
            Assert.IsFalse(Strategy.HasModel);
            Assert.AreEqual(string.Empty, Strategy.GetTemplateView().ModelPath);
            Assert.AreEqual(bounds, Strategy.inPara.ModeRect.Bounds, "模板区域几何回滚");
            Assert.AreEqual(hadRegion, Strategy.inPara.ModeRect.HoRegion.IsUsableRegion());
        }

        [TestMethod]
        public void SetTemplate_Exception_RestoresModeRect()
        {
            var bounds = Strategy.inPara.ModeRect.Bounds;
            var host = TemplateHost();
            host.FakeDisplay.HoImage = null;

            Assert.ThrowsException<InvalidOperationException>(
                () => Strategy.SetTemplateAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult());

            Assert.AreEqual(RectEnum.Rectangle, Strategy.inPara.ModeRect.Type, "类型回滚");
            Assert.AreEqual(bounds, Strategy.inPara.ModeRect.Bounds, "几何回滚");
            Assert.IsFalse(Strategy.HasModel);
        }

        [TestMethod]
        public void SetTemplate_SaveFails_KeepsPreviousModelAndTmplPoint()
        {
            CreateTemplate();
            var id = Strategy.ModelID;
            var tmpl = Tmpl;

            // 同名文件占住新的数据目录: 目录建不出来, 落盘必然失败
            string blocked = Path.Combine(_projectDir, "blocked");
            File.WriteAllText(blocked, string.Empty);
            Strategy.DataDir = blocked;
            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X + 5, TemplateBounds.Y + 5, TemplateBounds.Width, TemplateBounds.Height);
                r.RebuildRegion();
            };

            Assert.ThrowsException<IOException>(() => Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult());

            // 先换模板再存图的话: 存图一失败, 新模板配着旧 TmplPoint, 下游跟随整体偏 5 像素
            Assert.AreSame(id, Strategy.ModelID, "保存失败不能换掉旧模板");
            Assert.AreEqual(tmpl, Tmpl);
            Assert.AreEqual(TemplateBounds, Strategy.inPara.ModeRect.Bounds, "模板区域与仍在用的旧模板一致");
            Assert.IsNull(host.DonePath);

            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.On(_display).IsOk);
            Assert.AreEqual(tmpl.X, Strategy.Coord.X, 1.0, "旧模板句柄仍然可用");
        }

        [TestMethod]
        public void SetTemplate_SaveImageFails_OldModelImageUntouched()
        {
            CreateTemplate();
            string path = Strategy.ModelImagePath;
            byte[] before = File.ReadAllBytes(path);

            // 临时文件的位置被同名目录占住: 新模板图写不出来。
            // 直接覆盖 matching.bmp 的话, 写到一半失败时旧模板还在用、它的模板图却已损坏
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path), "matching.tmp.bmp"));
            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X + 5, TemplateBounds.Y + 5, TemplateBounds.Width, TemplateBounds.Height);
                r.RebuildRegion();
            };

            Assert.ThrowsException<Exception>(() => Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult());

            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "旧模板图不能被改动");
        }

        [TestMethod]
        public void SetTemplate_Reteach_ReplacesModelImage_LeavesNoTempFile()
        {
            CreateTemplate();
            string path = Strategy.ModelImagePath;
            byte[] before = File.ReadAllBytes(path);

            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X, TemplateBounds.Y, TemplateBounds.Width + 10, TemplateBounds.Height);
                r.RebuildRegion();
            };
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            CollectionAssert.AreNotEqual(before, File.ReadAllBytes(path), "重新示教应换成新模板图");
            Assert.IsFalse(Directory.GetFiles(Strategy.DataDir).Any(f => f.Contains(".tmp")), "不留临时文件");
        }

        [TestMethod]
        public void SetTemplate_TrialSearchesTemplateRegionOnly()
        {
            // 图里左右各一个一模一样的 L, 模板框在右边那个上。试匹配若搜整图, 同分的左边那个可能排在前面,
            // 示教原点就落到了另一个工件上
            using (var twin = TwinLImage())
            {
                var host = new FakeRoiHost
                {
                    OnDraw = r =>
                    {
                        r.Bounds = new Rect2d(TemplateBounds.X + W, TemplateBounds.Y, TemplateBounds.Width, TemplateBounds.Height);
                        r.RebuildRegion();
                    },
                };
                host.FakeDisplay.SetImage(twin);

                Strategy.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();
            }

            Assert.AreEqual(TemplateCenter.X + W, Tmpl.X, 1.0);
            Assert.AreEqual(TemplateCenter.Y, Tmpl.Y, 1.0);
        }

        [TestMethod]
        public void SetTemplate_ExplicitNumLevels_TrainsAndFinds()
        {
            // 金字塔层数除默认 0 (自动) 外, 常用值里另一个是 2: 四种模型都必须接受并能据此查找
            Strategy.inPara.NumLevels = 2;
            CreateTemplate();
            var tmpl = Tmpl;
            UseFullImageRoi();

            using (var shifted = LImage(dx: 25, dy: 15))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.On(_display).IsOk);
            }

            Assert.AreEqual(1, Strategy.Results.Count);
            Assert.AreEqual(tmpl.X + 25, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 15, Strategy.Coord.Y, 1.0);
        }

        /// <summary>
        /// 模型句柄不随参数落盘：新实例（例如重新打开方案）从数据目录里的模型文件重建，照常匹配。
        /// </summary>
        [TestMethod]
        public void ModelFile_ReloadedByNewInstance()
        {
            CreateTemplate();
            var tmpl = Tmpl;

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Strategy.inPara);
            using (var reopened = new TStrategy { DataDir = Strategy.DataDir })
            {
                reopened.inPara.HoRect.Dispose();
                reopened.inPara.ModeRect.Dispose();
                reopened.inPara = Newtonsoft.Json.JsonConvert.DeserializeObject<TPara>(json);
                Assert.IsFalse(reopened.HasModel);

                reopened.Init(new FakeRoiHost());
                Assert.IsTrue(reopened.HasModel, "Init 从模型文件重建句柄");

                reopened.inPara.HoRect.Dispose();
                reopened.inPara.HoRect = NewRegion(RectEnum.Rectangle, 0, 0, W - 1, H - 1);
                using (var shifted = LImage(dx: 10, dy: 5))
                {
                    _display.SetImage(shifted);
                    Assert.IsTrue(reopened.On(_display).IsOk);
                }
                Assert.AreEqual(tmpl.X + 10, reopened.Coord.X, 1.0);
                Assert.AreEqual(tmpl, reopened.inPara.TmplPoint, "示教原点随参数落盘");
            }
        }

        [TestMethod]
        public void GetTemplateView_PairsImageRegionContourAndBest()
        {
            Assert.IsFalse(Strategy.GetTemplateView().Best.HasValue);

            CreateTemplate();
            var view = Strategy.GetTemplateView();

            Assert.AreEqual(Strategy.ModelImagePath, view.ModelPath);
            Assert.AreSame(Strategy.inPara.ModeRect.HoRegion, view.ModelRegion);
            Assert.AreSame(Strategy.Contour, view.Contour);
            Assert.AreEqual(Strategy.Results[0], view.Best.Value);
        }

        [TestMethod]
        public void DrawROIAsync_Cancel_RestoresType_StillRedraws()
        {
            var host = new FakeRoiHost { Confirm = false };

            Strategy.DrawROIAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult();

            Assert.AreEqual(RectEnum.Rectangle, Strategy.inPara.HoRect.Type);
            Assert.AreEqual(1, host.FakeDisplay.Regions.Count);
            Assert.AreEqual(1, host.SetRectParaCount);
        }

        [TestMethod]
        public void DispROI_PushesModelPara()
        {
            var host = new FakeRoiHost();
            Strategy.DispROI(host);
            Assert.AreEqual(1, host.SetModelParaCount);
            Assert.AreEqual(0, host.SetRectParaCount);
        }

        [TestMethod]
        public void Defaults()
        {
            var p = Strategy.inPara;
            Assert.AreEqual(SourceRef.Local, p.ImageIn);
            Assert.AreEqual(SourceRef.Local, p.RegionIn);
            Assert.AreEqual(SourceRef.Local, p.CoordIn);
            Assert.AreEqual(-90, p.AngleStart);
            Assert.AreEqual(180, p.AngleExtent);
            Assert.AreEqual(0.6, p.MinScore);
            Assert.AreEqual(1, p.NumMatches);
            Assert.AreEqual(0.5, p.MaxOverlap);
            Assert.AreEqual(0, p.NumLevels, "0 = 金字塔层数由 HALCON 自动确定");
            Assert.IsNull(p.TmplPoint);
            Assert.IsFalse(Strategy.HasModel);
            Assert.AreEqual(0, Strategy.Results.Count);
            Assert.AreEqual(0, Strategy.Contour.CountObj(), "轮廓句柄已初始化为空对象");
        }

        [TestMethod]
        public void Params_Declared()
        {
            CollectionAssert.IsSubsetOf(
                new[] { "图像来源", "区域来源", "起始角度", "增量角度", "最大重叠率", "匹配数量", "得分", "金字塔" },
                Strategy.Labels(TabPageEnum.Parameter));
            CollectionAssert.AreEqual(new[] { "跟随坐标" }, Strategy.Labels(TabPageEnum.Region));
            CollectionAssert.IsSubsetOf(new[] { "查找区域", "显示轮廓", "显示点" }, Strategy.Labels(TabPageEnum.Display));
        }

        [TestMethod]
        public void Params_NumMatchesMany_IsZero()
        {
            Assert.IsTrue(Strategy.SetParam("匹配数量", 0));
            Assert.AreEqual(0, Strategy.inPara.NumMatches, "\"多个\" 存成 0（找全部）");

            var choice = (ChoiceParam)Strategy.Param("匹配数量");
            Assert.AreEqual("多个", choice.Options[choice.SelectedIndex].Text);
        }

        [TestMethod]
        public void Params_ScoreAndOverlapValidated()
        {
            var score = (NumberParam)Strategy.Param("得分");
            Assert.IsFalse(score.TryParse("1.5", out _, out string error), "HALCON 不收 > 1 的得分, 在面板上就拦下");
            StringAssert.Contains(error, "不能大于");
            Assert.IsTrue(score.TryParse("0.75", out object value, out _));
            Assert.AreEqual(0.75, value);

            Assert.IsFalse(((NumberParam)Strategy.Param("最大重叠率")).TryParse("-0.1", out _, out _));
        }

        [TestMethod]
        public void Para_ContainsOnlyConfig()
        {
            CreateTemplate();
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(Strategy.inPara);
            Assert.IsFalse(json.Contains("\"ModelID\""), "句柄不能落盘");
            Assert.IsFalse(json.Contains("\"Results\""));
            Assert.IsFalse(json.Contains("\"Coord\""));
            Assert.IsTrue(json.Contains("\"TmplPoint\""));
            Assert.IsTrue(json.Contains("\"ModeRect\""));
        }
    }

    [TestClass]
    public class ShapeModelStrategyTests : MatchingStrategyTestBase<ShapeModelStrategy, ShapeModel>
    {
        protected override string ExpectedName => "形状匹配";
        protected override string ExpectedKey => "match.shape";
    }

    [TestClass]
    public class NccModelStrategyTests : MatchingStrategyTestBase<NccModelStrategy, NccModel>
    {
        protected override string ExpectedName => "灰度匹配";
        protected override string ExpectedKey => "match.ncc";
    }

    [TestClass]
    public class ScaledModelStrategyTests : MatchingStrategyTestBase<ScaledModelStrategy, ScaledModel>
    {
        protected override string ExpectedName => "缩放匹配";
        protected override string ExpectedKey => "match.scaled";

        [TestMethod]
        public void Params_ScaleRange()
        {
            Assert.IsTrue(Strategy.SetParam("最小缩放", 0.7));
            Assert.IsTrue(Strategy.SetParam("最大缩放", 1.5));

            Assert.AreEqual(0.7, Strategy.inPara.ScaleMin, 1e-12);
            Assert.AreEqual(1.5, Strategy.inPara.ScaleMax, 1e-12);
        }

        [TestMethod]
        public void Run_ScaledTarget_ContourFollowsFoundScale()
        {
            Strategy.inPara.ScaleMax = 1.3;
            CreateTemplate();
            var tmpl = Strategy.inPara.TmplPoint.Value;
            ColumnRange(Strategy.Contour, out double tmplMin, out double tmplMax);
            UseFullImageRoi();

            // 绕模板原点放大 1.2 倍: 原点位置不变, 轮廓应随之变宽
            using (var bigger = LImage(scale: 1.2))
            {
                Display.SetImage(bigger);
                Assert.IsTrue(Strategy.On(Display).IsOk);
            }

            Assert.AreEqual(1, Strategy.Results.Count);
            Assert.AreEqual(tmpl.X, Strategy.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y, Strategy.Coord.Y, 1.0);
            ColumnRange(Strategy.Contour, out double minCol, out double maxCol);
            Assert.AreEqual((tmplMax - tmplMin) * 1.2, maxCol - minCol, 2.0, "轮廓必须按找到的缩放系数缩放, 而不是停在模板原尺寸");
        }
    }

    [TestClass]
    public class GenericModelStrategyTests : MatchingStrategyTestBase<GenericModelStrategy, GenericModel>
    {
        protected override string ExpectedName => "通用匹配";
        protected override string ExpectedKey => "match.generic";

        /// <summary>
        /// 通用匹配的查找参数存在模型句柄里：每次查找前写入，改了参数下一轮即生效（角度换成弧度）。
        /// </summary>
        [TestMethod]
        public void Run_PushesSearchParamsIntoModel()
        {
            CreateTemplate();
            Assert.IsTrue(Strategy.SetParam("得分", 0.8));
            Assert.IsTrue(Strategy.SetParam("匹配数量", 0));
            Assert.IsTrue(Strategy.SetParam("最大重叠率", 0.3));
            Assert.IsTrue(Strategy.SetParam("起始角度", -45.0));
            Assert.IsTrue(Strategy.SetParam("增量角度", 90.0));
            UseFullImageRoi();
            Display.SetImage(LImage());

            Strategy.On(Display);

            var id = Strategy.ModelID;
            HOperatorSet.GetGenericShapeModelParam(id, "min_score", out HTuple minScore);
            HOperatorSet.GetGenericShapeModelParam(id, "num_matches", out HTuple numMatches);
            HOperatorSet.GetGenericShapeModelParam(id, "max_overlap", out HTuple maxOverlap);
            HOperatorSet.GetGenericShapeModelParam(id, "angle_start", out HTuple angleStart);
            HOperatorSet.GetGenericShapeModelParam(id, "angle_end", out HTuple angleEnd);
            Assert.AreEqual(0.8, minScore.D, 1e-9);
            Assert.AreEqual("all", numMatches.S, "0 在模型内读回为 all（找全部）");
            Assert.AreEqual(0.3, maxOverlap.D, 1e-9);
            Assert.AreEqual(-Math.PI / 4, angleStart.D, 1e-9);
            Assert.AreEqual(Math.PI / 4, angleEnd.D, 1e-9);
        }
    }
}
