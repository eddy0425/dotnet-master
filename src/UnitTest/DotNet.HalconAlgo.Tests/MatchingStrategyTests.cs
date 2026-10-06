using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 形状 / 灰度 / 缩放 / 通用四种匹配的公共行为：流程完全相同，只有 HALCON 算子不同。
    /// 子类只提供参数访问器，测试方法由 MSTest 从基类继承执行。
    /// </summary>
    public abstract class MatchingStrategyTestBase<TStrategy> : HalconTestBase
        where TStrategy : IParaStrategy, IParaBinding, ITreeNodeProvider, IRoiEditable, ITemplateEditable, new()
    {
        private const int W = 200, H = 160;

        /// <summary>模板框：左上 (35,25)，80×80 → 中心 (75,65)，距图像边界留足 SaveSmallestRectImage 的 20px 外扩。</summary>
        private static readonly Rect2d TemplateBounds = new Rect2d(35.0, 25.0, 80.0, 80.0);
        private static readonly Point2d TemplateCenter = new Point2d(75, 65);

        private string _savedProjectDir;
        private string _projectDir;
        private HObject _image;
        protected TStrategy Strategy;
        private FakeDisplay _display;

        protected abstract string ExpectedName { get; }
        protected abstract CvRegion ModeRect(TStrategy s);
        protected abstract CvRegion HoRect(TStrategy s);
        protected abstract HTuple ModelID(TStrategy s);
        protected abstract Point2d TmplPoint(TStrategy s);
        protected abstract CvCoord Coord(TStrategy s);
        protected abstract List<ModelResult> Results(TStrategy s);
        protected abstract string ModelPath(TStrategy s);
        protected abstract void ClearModel(HTuple modelId);

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

        private protected FakeDisplay Display => _display;
        protected abstract HObject HoContour(TStrategy s);

        [TestInitialize]
        public void SetUpMatching()
        {
            _savedProjectDir = AlgoPaths.ProjectDir;
            _projectDir = Path.Combine(Path.GetTempPath(), "DotNet.HalconAlgo.Tests", Guid.NewGuid().ToString("N"));
            AlgoPaths.ProjectDir = _projectDir;

            _image = LImage();
            _display = new FakeDisplay();
            Strategy = new TStrategy { RunIndex = 3 };
        }

        [TestCleanup]
        public void TearDownMatching()
        {
            var id = ModelID(Strategy);
            if (id != null && id.Length > 0) ClearModel(id);
            ModeRect(Strategy).Dispose();
            HoRect(Strategy).Dispose();
            _image.Dispose();

            AlgoPaths.ProjectDir = _savedProjectDir;
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
            HoRect(Strategy).Dispose();
            SetHoRect(region);
        }

        /// <summary>整图查找 ROI。</summary>
        private protected void UseFullImageRoi() => ReplaceHoRect(NewRegion(RectEnum.Rectangle, 0, 0, W - 1, H - 1));

        [TestMethod]
        public void Name_IsDefault() => Assert.AreEqual(ExpectedName, Strategy.Name);

        [TestMethod]
        public void SetTemplate_CreatesModel_TeachesTmplPoint_SavesImage()
        {
            var host = CreateTemplate();

            Assert.IsNotNull(ModelID(Strategy));
            Assert.IsTrue(ModelID(Strategy).Length > 0);
            Assert.AreEqual(RectEnum.Rectangle, ModeRect(Strategy).Type);

            var tmpl = TmplPoint(Strategy);
            Assert.AreEqual(TemplateCenter.X, tmpl.X, 1.0, "模板原点取模板区域重心");
            Assert.AreEqual(TemplateCenter.Y, tmpl.Y, 1.0);
            Assert.AreEqual(tmpl, Coord(Strategy).Center);
            Assert.AreEqual(1, Results(Strategy).Count);

            string expectedPath = Path.Combine(AlgoPaths.JobDir, "3", "matching.bmp");
            Assert.AreEqual(expectedPath, ModelPath(Strategy));
            Assert.AreEqual(expectedPath, host.DonePath);
            Assert.IsTrue(File.Exists(expectedPath), "模板图应落盘到 JobDir/RunIndex/matching.bmp");
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
            Assert.AreEqual(RectEnum.Rectangle, ModeRect(Strategy).Type, "取消后类型还原");
            Assert.IsNull(ModelID(Strategy));
            Assert.AreEqual(string.Empty, ModelPath(Strategy));
            Assert.AreEqual(new Point2d(), TmplPoint(Strategy));
            Assert.IsNull(host.DonePath);
            Assert.AreEqual(0, host.SetModelParaCount);
            Assert.AreEqual(1, host.FakeDisplay.Regions.Count, "原模板区域重画回去");
            Assert.AreEqual(HColor.Orange.Name, host.FakeDisplay.Regions[0].ColorName);
            Assert.IsFalse(Directory.Exists(_projectDir));
        }

        [TestMethod]
        public void SetTemplate_Cancel_KeepsExistingModel()
        {
            CreateTemplate();
            var id = ModelID(Strategy);
            var tmpl = TmplPoint(Strategy);

            Strategy.SetTemplateAsync(TemplateHost(confirm: false), RectEnum.Circle, false).GetAwaiter().GetResult();

            Assert.AreSame(id, ModelID(Strategy));
            Assert.AreEqual(tmpl, TmplPoint(Strategy));
        }

        [TestMethod]
        public void SetTemplate_Modify_UsesModDraw()
        {
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            Assert.AreEqual(0, host.DrawCount);
            Assert.AreEqual(1, host.DrawModCount);
            Assert.IsNotNull(ModelID(Strategy));
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
            var tmpl = TmplPoint(Strategy);
            UseFullImageRoi();

            using (var shifted = LImage(dx: 30, dy: 20))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            }

            Assert.AreEqual(1, Results(Strategy).Count);
            var coord = Coord(Strategy);
            Assert.AreEqual(tmpl.X + 30, coord.X, 1.0);
            Assert.AreEqual(tmpl.Y + 20, coord.Y, 1.0);
            Assert.AreEqual(0, coord.AngleDegrees, 1.0);
            Assert.IsTrue(Results(Strategy)[0].Score > 0.9);

            StringAssert.StartsWith(_display.LastText, ExpectedName + " : 数量:1 最佳得分:");
            Assert.AreEqual(HColor.Green.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(1, _display.Objects.FindAll(o => o.ColorName == HColor.Blue.Name).Count, "查找区域");
            Assert.AreEqual(1, _display.Objects.FindAll(o => o.ColorName == HColor.Green.Name).Count, "模板轮廓");
            Assert.AreEqual(1, _display.Coords.Count, "匹配点");
        }

        [TestMethod]
        public void Run_Outputs_ResolveFromTree()
        {
            CreateTemplate();
            UseFullImageRoi();
            Strategy.GenTreeNode(new FakeTree());
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));

            var all = Strategies.Of(Strategy);
            string coordPath = ExpectedName + "/坐标系";
            Assert.AreEqual(Coord(Strategy), all.ResolveFrom<CvCoord>(coordPath));
            Assert.AreEqual(TmplPoint(Strategy), all.ResolveFrom<Point2d>(coordPath.ToTmplPoint()));
            Assert.AreEqual(Coord(Strategy).Y, all.ResolveFrom<double>(coordPath + "/原点/行"));
            Assert.AreEqual(Coord(Strategy).Angle.Radians, all.ResolveFrom<double>(coordPath + "/角度"));
        }

        [TestMethod]
        public void Run_NoMatch_RedText_ResetsPreviousResult()
        {
            CreateTemplate();
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            Assert.AreEqual(1, Results(Strategy).Count);

            using (var blank = ConstImage(W, H, 0))
            {
                _display.SetImage(blank);
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            }

            Assert.AreEqual(0, Results(Strategy).Count);
            Assert.AreEqual(new CvCoord(), Coord(Strategy), "没有匹配时不能留着上一轮的坐标系");
            StringAssert.Contains(_display.LastText, "数量:0");
            Assert.AreEqual(HColor.Red.Name, _display.Texts[_display.Texts.Count - 1].ColorName);
        }

        [TestMethod]
        public void Run_RoiNotDrawn_ReturnsFalse_ResetsResult()
        {
            CreateTemplate();
            Assert.AreNotEqual(new CvCoord(), Coord(Strategy));
            _display.SetImage(_image);

            Assert.IsFalse(Strategy.Fun_action(_display, Strategies.Of()));

            Assert.AreEqual($"{ExpectedName} : 尚未绘制 ROI，无法执行匹配！", _display.LastText);
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(0, Results(Strategy).Count);
            Assert.AreEqual(new CvCoord(), Coord(Strategy));
        }

        [TestMethod]
        public void Run_NoImage_Throws()
        {
            // 未建模板的校验先于取图, 要走到取图这一步必须先有模板
            CreateTemplate();
            var ex = Assert.ThrowsException<InvalidOperationException>(() => Strategy.Fun_action(new FakeDisplay(), Strategies.Of()));
            StringAssert.Contains(ex.Message, ExpectedName);
        }

        [TestMethod]
        public void Run_RegionIn_Unresolvable_Throws()
        {
            CreateTemplate();
            SetRegionIn("上游/区域");
            _display.SetImage(_image);
            Assert.ThrowsException<AlgoOutputNotFoundException>(() => Strategy.Fun_action(_display, Strategies.Of()));
        }

        /// <summary>
        /// 跟随坐标：本地查找 ROI 只框住示教位置，工件平移 (30,20) 后必须随上游坐标系一起搬过去才找得到。
        /// </summary>
        [TestMethod]
        public void Run_CoordIn_MovesLocalRegionWithUpstreamCoord()
        {
            CreateTemplate();
            var tmpl = TmplPoint(Strategy);
            // 只框住示教位置的 L (行 40..90 × 列 50..100), 平移后的 L 落在框外
            ReplaceHoRect(NewRegion(RectEnum.Rectangle, 45, 35, 60, 60));
            var upstream = StubStrategy.Coord("定位", tmpl, new CvCoord(new Point2d(tmpl.X + 30, tmpl.Y + 20), Angle.FromRadians(0)));
            SetPara(Strategy, "CoordIn", "定位/坐标系");

            using (var shifted = LImage(dx: 30, dy: 20))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of(upstream)));
            }

            Assert.AreEqual(1, Results(Strategy).Count, "查找区域没跟着搬过去就找不到平移后的目标");
            Assert.AreEqual(tmpl.X + 30, Coord(Strategy).X, 1.0);
            Assert.AreEqual(tmpl.Y + 20, Coord(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void Run_CoordIn_Unresolvable_Throws()
        {
            CreateTemplate();
            UseFullImageRoi();
            SetPara(Strategy, "CoordIn", "上游/坐标系");
            _display.SetImage(_image);
            Assert.ThrowsException<AlgoOutputNotFoundException>(() => Strategy.Fun_action(_display, Strategies.Of()));
        }

        [TestMethod]
        public void Run_WithoutModel_ReturnsFalse_RedText_ResetsResult()
        {
            CreateTemplate();
            Assert.AreEqual(1, Results(Strategy).Count);
            ClearModel(ModelID(Strategy));
            SetModelID(null);
            UseFullImageRoi();
            _display.SetImage(_image);

            // 原先 null 的 ModelID 会直接传进查找算子, 报出与真实原因无关的 HALCON 参数错误
            Assert.IsFalse(Strategy.Fun_action(_display, Strategies.Of()));

            StringAssert.StartsWith(_display.LastText, ExpectedName + " : 未建立模板");
            Assert.AreEqual(HColor.Red.Name, _display.Texts[0].ColorName);
            Assert.AreEqual(0, Results(Strategy).Count, "不能留着上一轮的匹配结果");
            Assert.AreEqual(new CvCoord(), Coord(Strategy));
        }

        /// <summary>两个查找 ROI (左右半幅, 经上游区域输出给入), 返回本轮的示教原点。</summary>
        private Point2d RunOnTwoRois(bool perfectOnLeft)
        {
            // 残缺 L 的得分约 0.7, 放低门槛保证两个 ROI 都有结果, 这样"取哪一个"才有意义
            SetSearchRange(-90, 180, 0.5);
            CreateTemplate();
            var tmpl = TmplPoint(Strategy);

            using (var image = TwoLImage(perfectOnLeft))
            using (var left = Rectangle1(0, 0, H - 1, W - 1))
            using (var right = Rectangle1(0, W, H - 1, 2 * W - 1))
            {
                HOperatorSet.ConcatObj(left, right, out HObject rois);
                try
                {
                    SetRegionIn("上游/区域");
                    _display.SetImage(image);
                    Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of(new StubStrategy("上游").Output("区域", rois))));
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
            var results = Results(Strategy);
            Assert.AreEqual(2, results.Count, "两个 ROI 各一个结果");
            Assert.IsTrue(results[0].Score > results[1].Score, "Results[0] 必须是全局最佳");

            var coord = Coord(Strategy);
            Assert.AreEqual(tmpl.X + perfectDx, coord.X, 1.0, "坐标系取全局最佳, 而不是第一个 ROI 的最佳");
            Assert.AreEqual(tmpl.Y, coord.Y, 1.0);
            StringAssert.StartsWith(_display.LastText, $"{ExpectedName} : 数量:2 最佳得分:{results[0].Score:F3}");

            // 轮廓与坐标系是一对(编辑模板窗口配对显示), 必须同是最佳实例的
            ColumnRange(HoContour(Strategy), out double minCol, out double maxCol);
            Assert.AreEqual(TemplateCenter.X + perfectDx, (minCol + maxCol) / 2, 10.0, "HoContour 必须是最佳实例的轮廓");
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
            Assert.IsTrue(HoContour(Strategy).CountObj() > 0);
            UseFullImageRoi();

            using (var blank = ConstImage(W, H, 0))
            {
                _display.SetImage(blank);
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            }

            Assert.AreEqual(0, HoContour(Strategy).CountObj(), "没有匹配时不能留着上一轮的轮廓配零坐标系");
        }

        [TestMethod]
        public void Run_RoiNotDrawn_ClearsPreviousContour()
        {
            CreateTemplate();
            _display.SetImage(_image);

            Assert.IsFalse(Strategy.Fun_action(_display, Strategies.Of()));

            Assert.AreEqual(0, HoContour(Strategy).CountObj());
        }

        [TestMethod]
        public void Run_ImageOverload_UsesGivenImage()
        {
            CreateTemplate();
            var tmpl = TmplPoint(Strategy);
            UseFullImageRoi();
            var display = new FakeDisplay();

            using (var shifted = LImage(dx: 20, dy: 10))
            {
                Assert.IsTrue(Strategy.Fun_action(shifted, display));
                Assert.AreSame(shifted, display.HoImage);
            }

            Assert.AreEqual(tmpl.X + 20, Coord(Strategy).X, 1.0);
            Assert.AreEqual(tmpl.Y + 10, Coord(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void Run_ImageIn_ResolvesUpstreamImage()
        {
            CreateTemplate();
            var tmpl = TmplPoint(Strategy);
            UseFullImageRoi();
            SetImageIn("取像/图像");
            _display.SetImage(_image);   // 显示窗口里是未平移的图: 必须用上游那张

            using (var shifted = LImage(dx: 25, dy: 15))
            {
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of(new StubStrategy("取像").Output("图像", shifted))));
            }

            Assert.AreEqual(tmpl.X + 25, Coord(Strategy).X, 1.0);
            Assert.AreEqual(tmpl.Y + 15, Coord(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void Run_DisplayFlagsOff_DrawsNothing()
        {
            CreateTemplate();
            UseFullImageRoi();
            SetDisplayFlags(false);
            _display.SetImage(_image);

            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));

            Assert.AreEqual(1, Results(Strategy).Count, "关显示不影响匹配本身");
            Assert.AreEqual(0, _display.Texts.Count);
            Assert.AreEqual(0, _display.Objects.Count);
            Assert.AreEqual(0, _display.Coords.Count);
        }

        [TestMethod]
        public void SetTemplate_Repeated_ReplacesModel_StillMatches()
        {
            CreateTemplate();
            var first = ModelID(Strategy);

            CreateTemplate();

            Assert.AreNotSame(first, ModelID(Strategy), "重建模板必须换成新句柄(旧句柄已释放)");
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            Assert.AreEqual(1, Results(Strategy).Count);
            Assert.AreEqual(TmplPoint(Strategy).X, Coord(Strategy).X, 1.0);
            Assert.AreEqual(TmplPoint(Strategy).Y, Coord(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void SetTemplate_TrialNoMatch_RedText_KeepsPreviousModel()
        {
            CreateTemplate();
            var id = ModelID(Strategy);
            var tmpl = TmplPoint(Strategy);

            // 新模板只搜 85°..95° 且要求 0.9 分: 未旋转的 L 形转 90° 后最多与自身一条边重合, 试匹配必然 0 个结果
            SetSearchRange(85, 10, 0.9);
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            Assert.AreEqual("新建模板失败！", host.FakeDisplay.LastText);
            Assert.AreEqual(HColor.Red.Name, host.FakeDisplay.Texts[0].ColorName);
            Assert.AreSame(id, ModelID(Strategy), "试匹配失败不能丢掉旧模板");
            Assert.AreEqual(tmpl, TmplPoint(Strategy), "旧模板与旧示教原点必须仍是一对");
            Assert.IsNull(host.DonePath);
            Assert.AreEqual(0, host.SetModelParaCount);

            // 旧模板句柄仍然可用: 恢复查找参数后照常匹配到示教位置
            SetSearchRange(-90, 180, 0.6);
            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            Assert.AreEqual(1, Results(Strategy).Count);
            Assert.AreEqual(tmpl.X, Coord(Strategy).X, 1.0);
            Assert.AreEqual(tmpl.Y, Coord(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void SetTemplate_TrialNoMatch_FirstTime_LeavesNoTrace()
        {
            // 失败轮次不能留下半截状态: ModelPath 指向一张根本没写出的图, ModeRect 已是新画的几何却没有配套模板
            var bounds = ModeRect(Strategy).Bounds;
            bool hadRegion = ModeRect(Strategy).HoRegion.IsUsableRegion();

            SetSearchRange(85, 10, 0.9);
            var host = TemplateHost();
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();

            Assert.AreEqual("新建模板失败！", host.FakeDisplay.LastText);
            Assert.IsNull(ModelID(Strategy));
            Assert.AreEqual(string.Empty, ModelPath(Strategy));
            Assert.AreEqual(bounds, ModeRect(Strategy).Bounds, "模板区域几何回滚");
            Assert.AreEqual(hadRegion, ModeRect(Strategy).HoRegion.IsUsableRegion());
        }

        [TestMethod]
        public void SetTemplate_Exception_RestoresModeRect()
        {
            var bounds = ModeRect(Strategy).Bounds;
            var host = TemplateHost();
            host.FakeDisplay.HoImage = null;

            Assert.ThrowsException<InvalidOperationException>(
                () => Strategy.SetTemplateAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult());

            Assert.AreEqual(RectEnum.Rectangle, ModeRect(Strategy).Type, "类型回滚");
            Assert.AreEqual(bounds, ModeRect(Strategy).Bounds, "几何回滚");
            Assert.AreEqual(string.Empty, ModelPath(Strategy));
        }

        [TestMethod]
        public void SetTemplate_SaveImageFails_KeepsPreviousModelAndTmplPoint()
        {
            CreateTemplate();
            var id = ModelID(Strategy);
            var tmpl = TmplPoint(Strategy);
            var path = ModelPath(Strategy);

            // 同名文件占住 JobDir/4: 新模板图的目录建不出来, 保存必然失败
            Strategy.RunIndex = 4;
            File.WriteAllText(Path.Combine(AlgoPaths.JobDir, "4"), string.Empty);
            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X + 5, TemplateBounds.Y + 5, TemplateBounds.Width, TemplateBounds.Height);
                r.RebuildRegion();
            };

            Assert.ThrowsException<Exception>(() => Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult());

            // 原先先换模板再存图: 存图一失败, 新模板配着旧 TmplPoint, 下游跟随整体偏 5 像素
            Assert.AreSame(id, ModelID(Strategy), "保存失败不能换掉旧模板");
            Assert.AreEqual(tmpl, TmplPoint(Strategy));
            Assert.AreEqual(path, ModelPath(Strategy));
            Assert.AreEqual(TemplateBounds, ModeRect(Strategy).Bounds, "模板区域与仍在用的旧模板一致");
            Assert.IsNull(host.DonePath);

            UseFullImageRoi();
            _display.SetImage(_image);
            Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            Assert.AreEqual(tmpl.X, Coord(Strategy).X, 1.0, "旧模板句柄仍然可用");
        }

        [TestMethod]
        public void SetTemplate_SaveImageFails_OldModelImageUntouched()
        {
            CreateTemplate();
            string path = ModelPath(Strategy);
            byte[] before = File.ReadAllBytes(path);

            // 临时文件的位置被同名目录占住: 新模板图写不出来。
            // 原先直接覆盖 matching.bmp, 写到一半失败时旧模板还在用、它的模板图却已损坏
            Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path), "matching.tmp.bmp"));
            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X + 5, TemplateBounds.Y + 5, TemplateBounds.Width, TemplateBounds.Height);
                r.RebuildRegion();
            };

            Assert.ThrowsException<Exception>(() => Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult());

            Assert.AreEqual(path, ModelPath(Strategy));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "旧模板图不能被改动");
        }

        [TestMethod]
        public void SetTemplate_Reteach_ReplacesModelImage_LeavesNoTempFile()
        {
            CreateTemplate();
            string path = ModelPath(Strategy);
            byte[] before = File.ReadAllBytes(path);

            var host = TemplateHost();
            host.OnDraw = r =>
            {
                r.Bounds = new Rect2d(TemplateBounds.X, TemplateBounds.Y, TemplateBounds.Width + 10, TemplateBounds.Height);
                r.RebuildRegion();
            };
            Strategy.SetTemplateAsync(host, RectEnum.Rectangle, false).GetAwaiter().GetResult();

            Assert.AreEqual(path, ModelPath(Strategy));
            CollectionAssert.AreNotEqual(before, File.ReadAllBytes(path), "重新示教应换成新模板图");
            CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)), "不留临时文件");
        }

        [TestMethod]
        public void SetTemplate_TrialSearchesTemplateRegionOnly()
        {
            // 图里左右各一个一模一样的 L, 模板框在右边那个上。试匹配若搜整图, 同分的左边那个可能排在前面,
            // 示教原点 (TmplPoint) 就落到了另一个工件上
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

            Assert.AreEqual(TemplateCenter.X + W, TmplPoint(Strategy).X, 1.0);
            Assert.AreEqual(TemplateCenter.Y, TmplPoint(Strategy).Y, 1.0);
        }

        [TestMethod]
        public void Run_ExceptionMidLoop_ResetsPartialResults()
        {
            CreateTemplate();
            // 上游"区域"第 1 个是整图矩形 (能匹配到), 第 2 个是 XLD: reduce_domain 在循环中途抛异常。
            // 此时第 1 个 ROI 的结果已写进 Results / HoContour, 不清就把半截结果留给"编辑模板"和下游
            HOperatorSet.GenContourPolygonXld(out HObject xld, new HTuple(0.0, 10.0), new HTuple(0.0, 10.0));
            using (xld)
            using (var roi = Rectangle1(0, 0, H - 1, W - 1))
            {
                HOperatorSet.ConcatObj(roi, xld, out HObject mixed);
                using (mixed)
                {
                    SetRegionIn("上游/区域");
                    _display.SetImage(_image);
                    try
                    {
                        Strategy.Fun_action(_display, Strategies.Of(new StubStrategy("上游").Output("区域", mixed)));
                        Assert.Fail("第 2 个 ROI 是 XLD, 应抛 HALCON 异常");
                    }
                    catch (HalconException) { }
                }
            }

            Assert.AreEqual(0, Results(Strategy).Count);
            Assert.AreEqual(new CvCoord(), Coord(Strategy));
            Assert.AreEqual(0, HoContour(Strategy).CountObj());
        }

        [TestMethod]
        public void DrawROIAsync_Cancel_RestoresType_StillRedraws()
        {
            var host = new FakeRoiHost { Confirm = false };

            Strategy.DrawROIAsync(host, RectEnum.Circle, true).GetAwaiter().GetResult();

            Assert.AreEqual(RectEnum.Rectangle, HoRect(Strategy).Type);
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
        public void ParaRoundTrip_NumMatchesMany()
        {
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);
            ui.Set("cmb_110", "多个").Set("cmb_111", "0.75").Set("cmb_102", "-45");

            var other = new TStrategy();
            other.SavePara(ui);
            try
            {
                Assert.AreEqual(0, NumMatches(other), "\"多个\" 映射为 0（找全部）");
                Assert.AreEqual(0.75, MinScore(other), 1e-12);
                Assert.AreEqual(-45, AngleStart(other), 1e-12);

                var back = new FakeUiHost();
                other.DispPara(back);
                Assert.AreEqual("多个", back.Values["cmb_110"]);
            }
            finally
            {
                ModeRect(other).Dispose();
                HoRect(other).Dispose();
                HoContour(other).Dispose();
            }
        }

        /// <summary>四种参数类型各自独立 (无公共基类), 这里按属性名反射读取, 让公共参数测试只写一份。</summary>
        private static PropertyInfo ParaProperty(TStrategy s, out object para, string name)
        {
            para = typeof(TStrategy).GetProperty("inPara").GetValue(s);
            var prop = para.GetType().GetProperty(name);
            Assert.IsNotNull(prop, $"{para.GetType().Name} 缺少属性 {name}");
            return prop;
        }

        private static object Para(TStrategy s, string name)
            => ParaProperty(s, out object para, name).GetValue(para);

        private static void SetPara(TStrategy s, string name, object value)
            => ParaProperty(s, out object para, name).SetValue(para, value);

        [TestMethod]
        public void Defaults()
        {
            Assert.AreEqual("默认", Para(Strategy, "ImageIn"));
            Assert.AreEqual("默认", Para(Strategy, "RegionIn"));
            Assert.AreEqual("默认", Para(Strategy, "CoordIn"));
            Assert.AreEqual(-90, ((HTuple)Para(Strategy, "AngleStart")).D, 1e-12);
            Assert.AreEqual(180, ((HTuple)Para(Strategy, "AngleExtent")).D, 1e-12);
            Assert.AreEqual(0.6, ((HTuple)Para(Strategy, "MinScore")).D, 1e-12);
            Assert.AreEqual(1, ((HTuple)Para(Strategy, "NumMatches")).I);
            Assert.AreEqual(0.5, ((HTuple)Para(Strategy, "MaxOverlap")).D, 1e-12);
            Assert.AreEqual(0, ((HTuple)Para(Strategy, "NumLevels")).I, "0 = 金字塔层数由 HALCON 自动确定");
            Assert.AreEqual(string.Empty, ModelPath(Strategy), "未建模板时是空串而不是 null");
            Assert.IsNull(ModelID(Strategy));
            Assert.AreEqual(0, Results(Strategy).Count);
            Assert.AreEqual(0, HoContour(Strategy).CountObj(), "轮廓句柄已初始化为空对象");
        }

        /// <remarks>CoordIn 的跟随行为见 <see cref="Run_CoordIn_MovesLocalRegionWithUpstreamCoord"/>。</remarks>
        [TestMethod]
        public void ParaRoundTrip_SearchParams()
        {
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);
            ui.Set("cmb_CoordIn", "上游/坐标系").Set("cmb_100", "图像源/图像").Set("cmb_101", "区域源/区域")
              .Set("cmb_103", "90").Set("cmb_104", "0.3").Set("cmb_110", "2").Set("cmb_112", "2");

            var other = new TStrategy();
            other.SavePara(ui);
            try
            {
                Assert.AreEqual("上游/坐标系", Para(other, "CoordIn"));
                Assert.AreEqual("图像源/图像", Para(other, "ImageIn"));
                Assert.AreEqual("区域源/区域", Para(other, "RegionIn"));
                Assert.AreEqual(90, ((HTuple)Para(other, "AngleExtent")).D, 1e-12);
                Assert.AreEqual(0.3, ((HTuple)Para(other, "MaxOverlap")).D, 1e-12);
                Assert.AreEqual(2, NumMatches(other));
                Assert.AreEqual(2, ((HTuple)Para(other, "NumLevels")).I);

                var back = new FakeUiHost();
                other.DispPara(back);
                foreach (var key in new[] { "cmb_CoordIn", "cmb_100", "cmb_101", "cmb_103", "cmb_104", "cmb_110", "cmb_112" })
                {
                    Assert.AreEqual(ui.Values[key], back.Values[key], key);
                }
            }
            finally
            {
                ModeRect(other).Dispose();
                HoRect(other).Dispose();
                HoContour(other).Dispose();
            }
        }

        [TestMethod]
        public void SetTemplate_ExplicitNumLevels_TrainsAndFinds()
        {
            // 金字塔层数除默认 0 (自动) 外, 界面另一个选项是 2: 四种模型都必须接受并能据此查找
            SetPara(Strategy, "NumLevels", new HTuple(2));
            CreateTemplate();
            var tmpl = TmplPoint(Strategy);
            UseFullImageRoi();

            using (var shifted = LImage(dx: 25, dy: 15))
            {
                _display.SetImage(shifted);
                Assert.IsTrue(Strategy.Fun_action(_display, Strategies.Of()));
            }

            Assert.AreEqual(1, Results(Strategy).Count);
            Assert.AreEqual(tmpl.X + 25, Coord(Strategy).X, 1.0);
            Assert.AreEqual(tmpl.Y + 15, Coord(Strategy).Y, 1.0);
        }

        protected abstract void SetHoRect(CvRegion region);
        protected abstract void SetModelID(HTuple modelId);
        protected abstract void SetRegionIn(string path);
        protected abstract void SetImageIn(string path);
        protected abstract void SetDisplayFlags(bool on);
        protected abstract void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore);
        protected abstract int NumMatches(TStrategy s);
        protected abstract double MinScore(TStrategy s);
        protected abstract double AngleStart(TStrategy s);
    }

    [TestClass]
    public class ShapeModelStrategyTests : MatchingStrategyTestBase<ShapeModelStrategy>
    {
        protected override string ExpectedName => "形状匹配";
        protected override CvRegion ModeRect(ShapeModelStrategy s) => s.inPara.ModeRect;
        protected override CvRegion HoRect(ShapeModelStrategy s) => s.inPara.HoRect;
        protected override HTuple ModelID(ShapeModelStrategy s) => s.inPara.ModelID;
        protected override Point2d TmplPoint(ShapeModelStrategy s) => s.inPara.TmplPoint;
        protected override CvCoord Coord(ShapeModelStrategy s) => s.inPara.Coord;
        protected override List<ModelResult> Results(ShapeModelStrategy s) => s.inPara.Results;
        protected override string ModelPath(ShapeModelStrategy s) => s.inPara.ModelPath;
        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearShapeModel(modelId);
        protected override void SetHoRect(CvRegion region) => Strategy.inPara.HoRect = region;
        protected override void SetRegionIn(string path) => Strategy.inPara.RegionIn = path;
        protected override void SetImageIn(string path) => Strategy.inPara.ImageIn = path;
        protected override HObject HoContour(ShapeModelStrategy s) => s.inPara.HoContour;
        protected override void SetDisplayFlags(bool on)
        {
            Strategy.inPara.DispText = on;
            Strategy.inPara.DispRegion = on;
            Strategy.inPara.DispContour = on;
            Strategy.inPara.DispPoint = on;
        }
        protected override void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore)
        {
            Strategy.inPara.AngleStart = angleStartDeg;
            Strategy.inPara.AngleExtent = angleExtentDeg;
            Strategy.inPara.MinScore = minScore;
        }
        protected override void SetModelID(HTuple modelId) => Strategy.inPara.ModelID = modelId;
        protected override int NumMatches(ShapeModelStrategy s) => s.inPara.NumMatches.I;
        protected override double MinScore(ShapeModelStrategy s) => s.inPara.MinScore.D;
        protected override double AngleStart(ShapeModelStrategy s) => s.inPara.AngleStart.D;
    }

    [TestClass]
    public class NccModelStrategyTests : MatchingStrategyTestBase<NccModelStrategy>
    {
        protected override string ExpectedName => "灰度匹配";
        protected override CvRegion ModeRect(NccModelStrategy s) => s.inPara.ModeRect;
        protected override CvRegion HoRect(NccModelStrategy s) => s.inPara.HoRect;
        protected override HTuple ModelID(NccModelStrategy s) => s.inPara.ModelID;
        protected override Point2d TmplPoint(NccModelStrategy s) => s.inPara.TmplPoint;
        protected override CvCoord Coord(NccModelStrategy s) => s.inPara.Coord;
        protected override List<ModelResult> Results(NccModelStrategy s) => s.inPara.Results;
        protected override string ModelPath(NccModelStrategy s) => s.inPara.ModelPath;
        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearNccModel(modelId);
        protected override void SetHoRect(CvRegion region) => Strategy.inPara.HoRect = region;
        protected override void SetRegionIn(string path) => Strategy.inPara.RegionIn = path;
        protected override void SetImageIn(string path) => Strategy.inPara.ImageIn = path;
        protected override HObject HoContour(NccModelStrategy s) => s.inPara.HoContour;
        protected override void SetDisplayFlags(bool on)
        {
            Strategy.inPara.DispText = on;
            Strategy.inPara.DispRegion = on;
            Strategy.inPara.DispContour = on;
            Strategy.inPara.DispPoint = on;
        }
        protected override void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore)
        {
            Strategy.inPara.AngleStart = angleStartDeg;
            Strategy.inPara.AngleExtent = angleExtentDeg;
            Strategy.inPara.MinScore = minScore;
        }
        protected override void SetModelID(HTuple modelId) => Strategy.inPara.ModelID = modelId;
        protected override int NumMatches(NccModelStrategy s) => s.inPara.NumMatches.I;
        protected override double MinScore(NccModelStrategy s) => s.inPara.MinScore.D;
        protected override double AngleStart(NccModelStrategy s) => s.inPara.AngleStart.D;
    }

    [TestClass]
    public class ScaledModelStrategyTests : MatchingStrategyTestBase<ScaledModelStrategy>
    {
        protected override string ExpectedName => "缩放匹配";
        protected override CvRegion ModeRect(ScaledModelStrategy s) => s.inPara.ModeRect;
        protected override CvRegion HoRect(ScaledModelStrategy s) => s.inPara.HoRect;
        protected override HTuple ModelID(ScaledModelStrategy s) => s.inPara.ModelID;
        protected override Point2d TmplPoint(ScaledModelStrategy s) => s.inPara.TmplPoint;
        protected override CvCoord Coord(ScaledModelStrategy s) => s.inPara.Coord;
        protected override List<ModelResult> Results(ScaledModelStrategy s) => s.inPara.Results;
        protected override string ModelPath(ScaledModelStrategy s) => s.inPara.ModelPath;
        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearShapeModel(modelId);
        protected override void SetHoRect(CvRegion region) => Strategy.inPara.HoRect = region;
        protected override void SetRegionIn(string path) => Strategy.inPara.RegionIn = path;
        protected override void SetImageIn(string path) => Strategy.inPara.ImageIn = path;
        protected override HObject HoContour(ScaledModelStrategy s) => s.inPara.HoContour;
        protected override void SetDisplayFlags(bool on)
        {
            Strategy.inPara.DispText = on;
            Strategy.inPara.DispRegion = on;
            Strategy.inPara.DispContour = on;
            Strategy.inPara.DispPoint = on;
        }
        protected override void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore)
        {
            Strategy.inPara.AngleStart = angleStartDeg;
            Strategy.inPara.AngleExtent = angleExtentDeg;
            Strategy.inPara.MinScore = minScore;
        }
        protected override void SetModelID(HTuple modelId) => Strategy.inPara.ModelID = modelId;
        protected override int NumMatches(ScaledModelStrategy s) => s.inPara.NumMatches.I;
        protected override double MinScore(ScaledModelStrategy s) => s.inPara.MinScore.D;
        protected override double AngleStart(ScaledModelStrategy s) => s.inPara.AngleStart.D;

        [TestMethod]
        public void ParaRoundTrip_ScaleRange()
        {
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);
            Strategy.SavePara(ui.Set("cmb_113", "0.7").Set("cmb_114", "1.5"));

            Assert.AreEqual(0.7, Strategy.inPara.ScaleMin.D, 1e-12);
            Assert.AreEqual(1.5, Strategy.inPara.ScaleMax.D, 1e-12);
        }

        [TestMethod]
        public void Run_ScaledTarget_ContourFollowsFoundScale()
        {
            Strategy.inPara.ScaleMax = 1.3;
            CreateTemplate();
            var tmpl = Strategy.inPara.TmplPoint;
            ColumnRange(Strategy.inPara.HoContour, out double tmplMin, out double tmplMax);
            UseFullImageRoi();

            // 绕模板原点放大 1.2 倍: 原点位置不变, 轮廓应随之变宽
            using (var bigger = LImage(scale: 1.2))
            {
                Display.SetImage(bigger);
                Assert.IsTrue(Strategy.Fun_action(Display, Strategies.Of()));
            }

            Assert.AreEqual(1, Strategy.inPara.Results.Count);
            Assert.AreEqual(tmpl.X, Strategy.inPara.Coord.X, 1.0);
            Assert.AreEqual(tmpl.Y, Strategy.inPara.Coord.Y, 1.0);
            ColumnRange(Strategy.inPara.HoContour, out double minCol, out double maxCol);
            Assert.AreEqual((tmplMax - tmplMin) * 1.2, maxCol - minCol, 2.0, "轮廓必须按找到的缩放系数缩放, 而不是停在模板原尺寸");
        }
    }

    [TestClass]
    public class GenericModelStrategyTests : MatchingStrategyTestBase<GenericModelStrategy>
    {
        protected override string ExpectedName => "通用匹配";
        protected override CvRegion ModeRect(GenericModelStrategy s) => s.inPara.ModeRect;
        protected override CvRegion HoRect(GenericModelStrategy s) => s.inPara.HoRect;
        protected override HTuple ModelID(GenericModelStrategy s) => s.inPara.ModelID;
        protected override Point2d TmplPoint(GenericModelStrategy s) => s.inPara.TmplPoint;
        protected override CvCoord Coord(GenericModelStrategy s) => s.inPara.Coord;
        protected override List<ModelResult> Results(GenericModelStrategy s) => s.inPara.Results;
        protected override string ModelPath(GenericModelStrategy s) => s.inPara.ModelPath;
        // 22.11 没有 ClearGenericShapeModel, 通用句柄统一由 clear_handle 释放
        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearHandle(modelId);
        protected override void SetHoRect(CvRegion region) => Strategy.inPara.HoRect = region;
        protected override void SetModelID(HTuple modelId) => Strategy.inPara.ModelID = modelId;
        protected override void SetRegionIn(string path) => Strategy.inPara.RegionIn = path;
        protected override void SetImageIn(string path) => Strategy.inPara.ImageIn = path;
        protected override HObject HoContour(GenericModelStrategy s) => s.inPara.HoContour;
        protected override void SetDisplayFlags(bool on)
        {
            Strategy.inPara.DispText = on;
            Strategy.inPara.DispRegion = on;
            Strategy.inPara.DispContour = on;
            Strategy.inPara.DispPoint = on;
        }
        protected override void SetSearchRange(double angleStartDeg, double angleExtentDeg, double minScore)
        {
            Strategy.inPara.AngleStart = angleStartDeg;
            Strategy.inPara.AngleExtent = angleExtentDeg;
            Strategy.inPara.MinScore = minScore;
        }
        protected override int NumMatches(GenericModelStrategy s) => s.inPara.NumMatches.I;
        protected override double MinScore(GenericModelStrategy s) => s.inPara.MinScore.D;
        protected override double AngleStart(GenericModelStrategy s) => s.inPara.AngleStart.D;

        [TestMethod]
        public void SavePara_WithModel_PushesSearchParamsIntoModel()
        {
            CreateTemplate();
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);

            Strategy.SavePara(ui.Set("cmb_111", "0.8").Set("cmb_110", "多个").Set("cmb_104", "0.3"));

            // 通用匹配的查找参数存在模板句柄里, 只改 inPara 不会影响下一次 FindGenericShapeModel
            var id = Strategy.inPara.ModelID;
            HOperatorSet.GetGenericShapeModelParam(id, "min_score", out HTuple minScore);
            HOperatorSet.GetGenericShapeModelParam(id, "num_matches", out HTuple numMatches);
            HOperatorSet.GetGenericShapeModelParam(id, "max_overlap", out HTuple maxOverlap);
            Assert.AreEqual(0.8, minScore.D, 1e-9);
            Assert.AreEqual("all", numMatches.S, "\"多个\" 映射为 0, 模型内读回为 all（找全部）");
            Assert.AreEqual(0.3, maxOverlap.D, 1e-9);
        }

        [TestMethod]
        public void SavePara_WithModel_PushesAngleRangeInRadians()
        {
            CreateTemplate();
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);

            // 界面是"起始角度 + 增量角度"(度), 模型参数是 angle_start / angle_end (弧度)
            Strategy.SavePara(ui.Set("cmb_102", "-45").Set("cmb_103", "90"));

            var id = Strategy.inPara.ModelID;
            HOperatorSet.GetGenericShapeModelParam(id, "angle_start", out HTuple angleStart);
            HOperatorSet.GetGenericShapeModelParam(id, "angle_end", out HTuple angleEnd);
            Assert.AreEqual(-Math.PI / 4, angleStart.D, 1e-9);
            Assert.AreEqual(Math.PI / 4, angleEnd.D, 1e-9);
        }

        [TestMethod]
        public void SavePara_ModelRejectsValue_DisplaySettingsStillSaved()
        {
            CreateTemplate();
            var ui = new FakeUiHost();
            Strategy.DispPara(ui);

            // HALCON 拒收 min_score 1.5: 原先写模型参数夹在中间, 一抛异常"显示"页的改动全部丢失
            ui.Set("cmb_111", "1.5").Set("CB_FontSize", "30").Check("ckb_disp0", false);
            Assert.ThrowsException<HOperatorException>(() => Strategy.SavePara(ui));

            Assert.IsFalse(Strategy.inPara.DispText);
            Assert.AreEqual(30, Strategy.inPara.FontSize);
        }

        [TestMethod]
        public void Run_WithoutModel_NoImageNeeded_GenericMessage()
        {
            var strategy = Strategy;   // 用夹具实例: TearDown 统一释放 ROI 句柄
            strategy.inPara.Results = new List<ModelResult> { new ModelResult(1, 2, 0, 1) };
            strategy.inPara.Coord = new CvCoord(2, 1);
            var display = new FakeDisplay();

            // 未建模板时连图像都不需要：前置校验先于取图
            Assert.IsFalse(strategy.Fun_action(display, Strategies.Of()));

            Assert.AreEqual("通用匹配", strategy.Name);
            Assert.AreEqual("通用匹配 : 未建立模板，无法执行通用匹配！", display.LastText);
            Assert.AreEqual(HColor.Red.Name, display.Texts[0].ColorName);
            Assert.AreEqual(0, strategy.inPara.Results.Count);
            Assert.AreEqual(new CvCoord(), strategy.inPara.Coord);
        }

        [TestMethod]
        public void Run_EmptyModelId_ReturnsFalse()
        {
            var strategy = Strategy;   // 用夹具实例: TearDown 统一释放 ROI 句柄
            strategy.inPara.ModelID = new HTuple();
            Assert.IsFalse(strategy.Fun_action(new FakeDisplay(), Strategies.Of()));
        }
    }
}
