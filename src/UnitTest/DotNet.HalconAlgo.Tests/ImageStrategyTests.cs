using System;
using System.IO;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class FileImageStrategyTests : HalconTestBase
    {
        private string _dir;
        private FileImageStrategy _strategy;
        private FakeDisplay _display;

        [TestInitialize]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "DotNet.HalconAlgo.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _strategy = new FileImageStrategy();
            _strategy.inPara.ImageFolder = _dir;
            _display = new FakeDisplay();
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>写一张灰度恒为 <paramref name="gray"/> 的 bmp，文件名不含扩展名。</summary>
        private void WriteImage(string name, int gray, int width = 40, int height = 30)
        {
            using (var img = ConstImage(width, height, gray))
            {
                HOperatorSet.WriteImage(img, "bmp", 0, Path.Combine(_dir, name));
            }
        }

        private int CurrentGray() => GrayAt(_strategy.Image, 5, 5);

        [TestMethod]
        public void Identity()
        {
            Assert.AreEqual("image.file", AlgoInfo.Of(_strategy).Key);
            Assert.AreEqual("文件图像", _strategy.Name);
            Assert.AreEqual(string.Empty, new FileImage().ImageFolder, "未配置时是空串而不是 null");
            Assert.IsInstanceOfType(_strategy, typeof(IImageProducer), "流程里其后工具的本地图像取它的输出");
        }

        [TestMethod]
        public void Cycles_InNumericOrder_AndWraps()
        {
            WriteImage("10", 100);
            WriteImage("2", 20);
            WriteImage("1", 10);
            _strategy.Init(new FakeRoiHost());

            var grays = Enumerable.Range(0, 4).Select(_ =>
            {
                Assert.IsTrue(_strategy.On(_display).IsOk);
                return CurrentGray();
            }).ToArray();

            CollectionAssert.AreEqual(new[] { 10, 20, 100, 10 }, grays, "按数字排序轮播，越界回到第一张");
            Assert.AreEqual("文件图像 : W:40 H:30 索引:0/3", _display.LastText);
            Assert.AreEqual(4, _display.DispImageCount);
            Assert.AreSame(_strategy.Image, _display.HoImage);
        }

        [TestMethod]
        public void WithoutInit_ScansFolderLazily()
        {
            WriteImage("1", 10);
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(10, CurrentGray());
        }

        [TestMethod]
        public void PreviousImage_IsReleased()
        {
            WriteImage("1", 10);
            WriteImage("2", 20);
            _strategy.Init(new FakeRoiHost());

            Assert.IsTrue(_strategy.On(_display).IsOk);
            var first = _strategy.Image;
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.IsFalse(first.IsInitialized());
        }

        [TestMethod]
        public void DispTextOff_NoText()
        {
            WriteImage("1", 10);
            _strategy.inPara.DispText = false;
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(0, _display.Texts.Count);
        }

        /// <summary>40×30 暗图, 左上角 (0,0) 一个亮像素, 写成 1.bmp。</summary>
        private void WriteCornerDotImage()
        {
            using (var dark = ConstImage(40, 30, 0))
            using (var dot = Rectangle1(0, 0, 0, 0))
            using (var img = Paint(dark, dot, 255))
            {
                HOperatorSet.WriteImage(img, "bmp", 0, Path.Combine(_dir, "1"));
            }
        }

        [DataTestMethod]
        // rotate_image 按度数逆时针旋转; 90 / 270 宽高互换, 180 不变
        [DataRow(90, 30, 40, 39, 0)]
        [DataRow(180, 40, 30, 29, 39)]
        [DataRow(270, 30, 40, 0, 29)]
        public void Rotate_CounterClockwise_PixelMapping(int deg, int expW, int expH, int brightRow, int brightCol)
        {
            WriteCornerDotImage();
            _strategy.inPara.Rotate = deg;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            ImageSize(_strategy.Image, out int w, out int h);
            Assert.AreEqual(expW, w);
            Assert.AreEqual(expH, h);
            Assert.AreEqual(255, GrayAt(_strategy.Image, brightRow, brightCol));
        }

        [TestMethod]
        public void RotateThenMirror_AppliedInThatOrder()
        {
            // 先转 90°: 亮点 (0,0) → (39,0), 图变 30×40; 再行镜像 → (0,0)。
            // 若先镜像后旋转会落到 (39,29), 以此钉住处理顺序
            WriteCornerDotImage();
            _strategy.inPara.Rotate = 90;
            _strategy.inPara.Mirror = MirrorMode.Row;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(255, GrayAt(_strategy.Image, 0, 0));
            Assert.AreEqual(0, GrayAt(_strategy.Image, 39, 29));
        }

        [DataTestMethod]
        [DataRow(MirrorMode.Row, 29, 0)]
        [DataRow(MirrorMode.Column, 0, 39)]
        [DataRow(MirrorMode.Origin, 29, 39)]
        [DataRow(MirrorMode.None, 0, 0)]
        public void Mirror(MirrorMode mode, int brightRow, int brightCol)
        {
            // 左上角单个亮像素，镜像后落到对应角
            WriteCornerDotImage();
            _strategy.inPara.Mirror = mode;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreEqual(255, GrayAt(_strategy.Image, brightRow, brightCol));
        }

        [TestMethod]
        public void Mirror_Origin_KeepsSize_NotTransposed()
        {
            // 原点镜像是点对称 (旋转 180°), 宽高不变; mirror_image 的 "diagonal" 是转置, 会把 40×30 变成 30×40
            WriteImage("1", 10, 40, 30);
            _strategy.inPara.Mirror = MirrorMode.Origin;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            ImageSize(_strategy.Image, out int w, out int h);
            Assert.AreEqual(40, w);
            Assert.AreEqual(30, h);
        }

        [TestMethod]
        public void Init_BadFolder_LogsError_DoesNotThrow()
        {
            _strategy.inPara.ImageFolder = Path.Combine(_dir, "missing");

            using (var log = new CapturingLogger())
            {
                _strategy.Init(new FakeRoiHost());

                var entry = log.Entries.Single();
                Assert.AreEqual(LogLevel.Error, entry.Level);
                Assert.AreEqual("FileImageStrategy", entry.Category);
                StringAssert.Contains(entry.Message, "missing");
                Assert.IsInstanceOfType(entry.Exception, typeof(DirectoryNotFoundException));
            }

            Assert.IsNotNull(_strategy.Image, "失败时仍保留有效的空句柄");
        }

        [TestMethod]
        public void EmptyFolder_Fails()
        {
            using (new CapturingLogger())
            {
                _strategy.Init(new FakeRoiHost());
            }
            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "图片路径为空");
        }

        /// <summary> 目录一改就重扫、游标归零：不必等"保存参数"，执行时对比即可 </summary>
        [TestMethod]
        public void FolderChanged_RescansNewFolder_AndResetsCursor()
        {
            WriteImage("1", 10);
            WriteImage("2", 20);
            _strategy.Init(new FakeRoiHost());
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(10, CurrentGray());

            string other = Path.Combine(_dir, "other");
            Directory.CreateDirectory(other);
            using (var img = ConstImage(40, 30, 99))
            {
                HOperatorSet.WriteImage(img, "bmp", 0, Path.Combine(other, "1"));
            }

            Assert.IsTrue(_strategy.SetParam("图片路径", other));

            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(99, CurrentGray(), "改了目录后必须轮播新目录, 而不是继续旧目录的缓存列表");
            Assert.AreEqual("文件图像 : W:40 H:30 索引:0/1", _display.LastText, "游标从新目录第一张开始");
        }

        [TestMethod]
        public void SameFolder_KeepsCursor()
        {
            WriteImage("1", 10);
            WriteImage("2", 20);
            _strategy.Init(new FakeRoiHost());
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.IsFalse(_strategy.SetParam("图片路径", _dir), "目录没变不算改动");

            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(20, CurrentGray());
        }

        [TestMethod]
        public void ReadFailure_FailsCleanly_AndSkipsBadFile()
        {
            WriteImage("1", 10);
            File.WriteAllBytes(Path.Combine(_dir, "2.bmp"), new byte[] { 1, 2, 3, 4 });
            _strategy.Init(new FakeRoiHost());

            Assert.IsTrue(_strategy.On(_display).IsOk);
            var before = _strategy.Image;

            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);

            Assert.IsTrue(_strategy.Image.IsInitialized(), "读图失败不能留下已释放的句柄");
            Assert.AreEqual(0, _strategy.Image.CountObj(), "失败轮次输出复位为空对象, 下游明确报错而不是用旧图");
            Assert.IsFalse(before.IsInitialized(), "上一轮输出已释放");

            // 坏图不会卡住轮播: 下一轮越过它回到第一张
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(10, CurrentGray());
            Assert.AreEqual("文件图像 : W:40 H:30 索引:0/2", _display.LastText);
        }

        [TestMethod]
        public void Output_Image()
        {
            WriteImage("1", 10);
            Assert.IsTrue(_strategy.On(_display).IsOk);

            var ctx = new RunContext(null, new IParaStrategy[] { _strategy });
            Assert.AreSame(_strategy.Image, ctx.ResolveImage(_strategy.Ref("图像")));
        }

        [TestMethod]
        public void Params_FileImagePage()
        {
            CollectionAssert.AreEqual(new[] { "图片路径", "旋转", "镜像" }, _strategy.Labels(TabPageEnum.FileImage));
            Assert.IsTrue(_strategy.SetParam("旋转", 180));
            Assert.IsTrue(_strategy.SetParam("镜像", MirrorMode.Column));
            Assert.AreEqual(180, _strategy.inPara.Rotate);
            Assert.AreEqual(MirrorMode.Column, _strategy.inPara.Mirror);

            var mirror = (ChoiceParam)_strategy.Param("镜像");
            CollectionAssert.AreEqual(new[] { "无", "行镜像", "列镜像", "原点镜像" }, mirror.Options.Select(o => o.Text).ToArray());
        }
    }

    [TestClass]
    public class RotateImageStrategyTests : HalconTestBase
    {
        private HObject _source;
        private FakeDisplay _display;
        private RotateImageStrategy _strategy;

        [TestInitialize]
        public void SetUp()
        {
            _source = ConstImage(40, 30, 50);
            _display = new FakeDisplay();
            _display.SetImage(_source);
            _strategy = new RotateImageStrategy();
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            _source.Dispose();
        }

        private string RunWithCoord(RotateMode mode, double angleDeg)
        {
            _strategy.inPara.RotateType = mode;
            var locator = StubStrategy.Coord("定位", new Point2d(0, 0), CvCoord.FromDegrees(20, 15, angleDeg));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");
            Assert.IsTrue(_strategy.On(_display, locator).IsOk);
            return _display.LastText;
        }

        [TestMethod]
        public void ImageCenter_ZeroAngle_CopiesImage()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreNotSame(_source, _strategy.Image);
            Assert.AreEqual(50, GrayAt(_strategy.Image, 10, 10));
            Assert.AreSame(_strategy.Image, _display.HoImage);
            Assert.AreEqual("旋转图像 : 方式:图像中心 角度:0.00°", _display.LastText);
        }

        [TestMethod]
        public void ImageCenter_90_Rotates()
        {
            _strategy.inPara.RotateAngle = 90;

            Assert.IsTrue(_strategy.On(_display).IsOk);

            ImageSize(_strategy.Image, out int w, out int h);
            Assert.AreEqual(30, w);
            Assert.AreEqual(40, h);
            StringAssert.EndsWith(_display.LastText, "角度:90.00°");
        }

        [TestMethod]
        public void ImageOverload_UsesGivenImage()
        {
            Assert.IsTrue(_strategy.OnImage(_source, new FakeDisplay()).IsOk);
            Assert.AreEqual(50, GrayAt(_strategy.Image, 0, 0));
        }

        [TestMethod]
        public void NoImage_Fails_WithToolNameInStatus()
        {
            var display = new FakeDisplay();
            Assert.AreEqual(RunStatus.Error, _strategy.On(display).Status);
            StringAssert.StartsWith(display.LastText, "旋转图像 : ");
        }

        [TestMethod]
        public void ImageIn_FromUpstream()
        {
            using (var upstream = ConstImage(20, 10, 7))
            {
                var camera = new StubStrategy("取像").Image("图像", () => upstream);
                _strategy.inPara.ImageIn = camera.Ref("图像");
                Assert.IsTrue(_strategy.On(_display, camera).IsOk);
                Assert.AreEqual(7, GrayAt(_strategy.Image, 0, 0));
            }
        }

        [DataTestMethod]
        [DataRow(RotateMode.Coord, "坐标系", 30, -30)]
        [DataRow(RotateMode.CoordXAxis, "坐标系X轴", -45, 45)]
        [DataRow(RotateMode.CoordYAxis, "坐标系Y轴", 30, 60)]
        [DataRow(RotateMode.CoordYAxis, "坐标系Y轴", -30, -60)]
        [DataRow(RotateMode.CoordYAxis, "坐标系Y轴", 0, 90)]
        public void CoordModes_RotationAngle(RotateMode mode, string text, double coordDeg, double expectedDeg)
        {
            string shown = RunWithCoord(mode, coordDeg);

            Assert.AreEqual($"旋转图像 : 方式:{text} 坐标:(20.00,15.00) 角度:{expectedDeg:F2}°", shown);
            ImageSize(_strategy.Image, out int w, out int h);
            Assert.AreEqual(40, w, "affine_trans_image 不改变图像尺寸");
            Assert.AreEqual(30, h);
        }

        [TestMethod]
        public void CoordMode_AngleNormalizedBeforeMapping()
        {
            // 350° 归一化为 -10° → 坐标系模式旋转 +10°
            StringAssert.EndsWith(RunWithCoord(RotateMode.Coord, 350), "角度:10.00°");
        }

        [TestMethod]
        public void InvalidMode_Fails()
        {
            _strategy.inPara.RotateType = (RotateMode)99;
            var locator = StubStrategy.Coord("定位", new Point2d(0, 0), CvCoord.FromDegrees(20, 15, 30));
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            var result = _strategy.On(_display, locator);

            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "旋转方式无效");
        }

        [TestMethod]
        public void CoordMode_WithoutCoord_Fails()
        {
            _strategy.inPara.RotateType = RotateMode.Coord;
            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "必须选择坐标系");
        }

        [TestMethod]
        public void CoordMode_Unresolvable_Fails()
        {
            _strategy.inPara.RotateType = RotateMode.Coord;
            _strategy.inPara.CoordIn = StubStrategy.Coord("定位", new Point2d(), new CvCoord()).Ref("坐标系");
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);
        }

        [TestMethod]
        public void ImageOverload_UpstreamImageIn_Fails()
        {
            // 单图验证没有上游: 配了上游图像来源就解析不到, 明确失败
            _strategy.inPara.ImageIn = new StubStrategy("取像").Ref("图像");
            Assert.AreEqual(RunStatus.Error, _strategy.OnImage(_source, new FakeDisplay()).Status);
        }

        [TestMethod]
        public void Success_ReleasesPreviousOutput()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            var first = _strategy.Image;
            _display.SetImage(first);

            // 当前图像此时正是上一轮输出（FakeDisplay 不复制）：新结果必须先算完再释放旧图
            Assert.IsTrue(_strategy.On(_display).IsOk);

            Assert.AreNotSame(first, _strategy.Image);
            Assert.IsFalse(first.IsInitialized(), "上一轮输出应被释放");
            Assert.AreEqual(50, GrayAt(_strategy.Image, 10, 10));
        }

        [TestMethod]
        public void Failure_ResetsPreviousOutput()
        {
            Assert.IsTrue(_strategy.On(_display).IsOk);
            Assert.AreEqual(1, _strategy.Image.CountObj());

            _strategy.inPara.RotateType = RotateMode.Coord;
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);

            // 下游照跑时必须拿到空图像（下游会明确报错），而不是上一轮的旧图
            Assert.IsTrue(_strategy.Image.IsInitialized());
            Assert.AreEqual(0, _strategy.Image.CountObj());
        }

        [TestMethod]
        public void FailedOutput_IsRejectedDownstream()
        {
            _strategy.inPara.RotateType = RotateMode.Coord;
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);

            using (var downstream = new RotateImageStrategy { Name = "下游" })
            {
                downstream.inPara.ImageIn = _strategy.Ref("图像");
                var display = new FakeDisplay();
                display.SetImage(_source);

                var result = downstream.On(display, _strategy);

                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "旋转图像/图像", "报错带出来源");
                StringAssert.StartsWith(display.LastText, "下游 : ");
            }
        }

        [TestMethod]
        public void CoordMode_RotatesAroundCoordOrigin_Clockwise()
        {
            // 坐标系转 +90°（逆时针），"坐标系"方式反转 -90°：以坐标原点 (列20, 行15) 为轴顺时针转。
            // 原点右侧 10 像素的标记 → 原点下方 10 像素。行列互换 / 漏换弧度 / 方向取反都会落到别处。
            using (var mark = Rectangle1(14, 29, 16, 31))
            using (var marked = Paint(_source, mark, 255))
            {
                _display.SetImage(marked);
                RunWithCoord(RotateMode.Coord, 90);
            }

            var image = _strategy.Image;
            Assert.AreEqual(50, GrayAt(image, 15, 20), "旋转中心处不变");
            Assert.AreEqual(255, GrayAt(image, 25, 20), "标记转到原点正下方");
            Assert.AreEqual(50, GrayAt(image, 15, 30), "原位置不再有标记");
            Assert.AreEqual(50, GrayAt(image, 5, 20), "不是逆时针");
        }

        [TestMethod]
        public void UpstreamEmptyImage_Fails()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            using (empty)
            {
                var camera = new StubStrategy("取像").Image("图像", () => empty);
                _strategy.inPara.ImageIn = camera.Ref("图像");
                var result = _strategy.On(_display, camera);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "图像来源");
            }
        }

        /// <summary> "旋转角度"与"坐标系"是两项，各带显示条件，不再共用一个槽位 </summary>
        [TestMethod]
        public void Params_AngleAndCoordAreSeparateItems_WithVisibility()
        {
            var angle = _strategy.Param("旋转角度");
            var coord = _strategy.Param("坐标系");
            Assert.IsTrue(angle.IsVisible);
            Assert.IsFalse(coord.IsVisible);

            Assert.IsTrue(_strategy.SetParam("选择方式", RotateMode.CoordYAxis));
            Assert.IsFalse(angle.IsVisible);
            Assert.IsTrue(coord.IsVisible);
        }

        [TestMethod]
        public void Params_SwitchingModeKeepsBothValues()
        {
            var locator = StubStrategy.Coord("定位", new Point2d(), new CvCoord());
            _strategy.inPara.RotateAngle = 90;
            _strategy.inPara.CoordIn = locator.Ref("坐标系");

            _strategy.SetParam("选择方式", RotateMode.Coord);
            _strategy.SetParam("选择方式", RotateMode.ImageCenter);

            Assert.AreEqual(90, _strategy.inPara.RotateAngle);
            Assert.AreEqual(locator.Ref("坐标系"), _strategy.inPara.CoordIn);
        }

        [TestMethod]
        public void Params_InvalidAngleRejected()
        {
            var angle = (NumberParam)_strategy.Param("旋转角度");
            Assert.IsFalse(angle.TryParse("abc", out _, out _));
            Assert.IsTrue(angle.TryParse("12.5", out object value, out _));
            Assert.AreEqual(12.5, value);
        }
    }

    [TestClass]
    public class LineRotImageStrategyTests : HalconTestBase
    {
        private HObject _source;
        private FakeDisplay _display;
        private LineRotImageStrategy _strategy;

        [TestInitialize]
        public void SetUp()
        {
            _source = ConstImage(40, 30, 50);
            _display = new FakeDisplay();
            _display.SetImage(_source);
            _strategy = new LineRotImageStrategy();
        }

        [TestCleanup]
        public void TearDown()
        {
            _strategy.Dispose();
            _source.Dispose();
        }

        private StubStrategy LineSource(CvLine line)
        {
            var fit = new StubStrategy("拟合").Line("直线", () => line);
            _strategy.inPara.LineIn = fit.Ref("直线");
            return fit;
        }

        [TestMethod]
        public void LineUnresolvable_Fails()
        {
            _strategy.inPara.LineIn = new StubStrategy("拟合").Ref("直线");
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display).Status);
        }

        [TestMethod]
        public void LineNotSelected_Fails()
        {
            var result = _strategy.On(_display);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "直线来源");
        }

        [TestMethod]
        public void DegenerateLine_Fails_NamesSource()
        {
            var fit = LineSource(new CvLine(new Point2d(5, 5), new Point2d(5, 5)));
            // 退化直线是业务错误, 不再伪装成 NullReferenceException; 消息带直线来源便于定位
            var result = _strategy.On(_display, fit);
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "拟合/直线");
            StringAssert.StartsWith(_display.LastText, "直线图像 : ");
        }

        [TestMethod]
        public void NoImage_Fails()
        {
            var fit = LineSource(new CvLine(0, 0, 10, 0));
            Assert.AreEqual(RunStatus.Error, _strategy.On(new FakeDisplay(), fit).Status);
        }

        [DataTestMethod]
        [DataRow(AlignAxis.ParallelX, "平行X轴", 0, 0, 10, 0, 0.0)]
        [DataRow(AlignAxis.ParallelX, "平行X轴", 0, 0, 10, 10, 45.0)]
        [DataRow(AlignAxis.ParallelX, "平行X轴", 10, 10, 0, 0, 45.0)]       // 反向直线：-135° 归一化为 45°
        [DataRow(AlignAxis.ParallelX, "平行X轴", 0, 0, 10, -10, -45.0)]
        [DataRow(AlignAxis.ParallelX, "平行X轴", 0, 0, 0, 10, 90.0)]
        [DataRow(AlignAxis.ParallelY, "平行Y轴", 0, 0, 0, 10, 0.0)]
        [DataRow(AlignAxis.ParallelY, "平行Y轴", 0, 0, 0, -10, 0.0)]        // -90° - 90° = -180° 归一化为 0°
        [DataRow(AlignAxis.ParallelY, "平行Y轴", 0, 0, 10, 10, -45.0)]
        [DataRow(AlignAxis.ParallelY, "平行Y轴", 0, 0, 10, 0, -90.0)]
        public void RotationAngle_Normalized(AlignAxis axis, string text, double x1, double y1, double x2, double y2, double expectedDeg)
        {
            _strategy.inPara.AlignAxis = axis;
            var fit = LineSource(new CvLine(x1, y1, x2, y2));

            Assert.IsTrue(_strategy.On(_display, fit).IsOk);

            Assert.AreEqual($"直线图像 : 对齐:{text} 旋转:{expectedDeg:F2}°", _display.LastText);
            Assert.AreSame(_strategy.Image, _display.HoImage);
            ImageSize(_strategy.Image, out int w, out int h);
            Assert.AreEqual(40, w);
            Assert.AreEqual(30, h);
        }

        [TestMethod]
        public void ZeroRotation_PreservesPixels()
        {
            var fit = LineSource(new CvLine(0, 0, 10, 0));
            Assert.IsTrue(_strategy.On(_display, fit).IsOk);
            Assert.AreEqual(50, GrayAt(_strategy.Image, 15, 20));
        }

        [TestMethod]
        public void DiagonalLine_IsLeveled_AroundImageCenter()
        {
            // 45° 直线（向右下）→ 逆时针转 45° 摆平：中心 (行15, 列20) 右下方沿线 ~9.9 像素的标记
            // 应落到中心正右方同一行。方向取反或行列互换时标记会落到别处。
            var fit = LineSource(new CvLine(0, 0, 10, 10));
            using (var mark = Rectangle1(21, 26, 23, 28))
            using (var marked = Paint(_source, mark, 255))
            {
                _display.SetImage(marked);
                Assert.IsTrue(_strategy.On(_display, fit).IsOk);
            }

            var image = _strategy.Image;
            Assert.AreEqual(255, GrayAt(image, 15, 30), "标记转到中心正右方");
            Assert.AreEqual(50, GrayAt(image, 22, 27), "原位置不再有标记");
            Assert.AreEqual(50, GrayAt(image, 25, 20), "不是顺时针");
        }

        [TestMethod]
        public void Failure_ResetsPreviousOutput()
        {
            var good = LineSource(new CvLine(0, 0, 10, 0));
            Assert.IsTrue(_strategy.On(_display, good).IsOk);
            var first = _strategy.Image;
            Assert.AreEqual(1, first.CountObj());

            var bad = LineSource(new CvLine(new Point2d(5, 5), new Point2d(5, 5)));
            Assert.AreEqual(RunStatus.Error, _strategy.On(_display, bad).Status);

            Assert.AreEqual(0, _strategy.Image.CountObj(), "失败轮次不能留下上一轮的旋转图");
            Assert.IsFalse(first.IsInitialized(), "上一轮输出应被释放");
        }

        [TestMethod]
        public void UpstreamEmptyImage_Fails()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            using (empty)
            {
                var fit = LineSource(new CvLine(0, 0, 10, 0));
                var camera = new StubStrategy("取像").Image("图像", () => empty);
                _strategy.inPara.ImageIn = camera.Ref("图像");
                var result = _strategy.On(_display, fit, camera);
                Assert.AreEqual(RunStatus.Error, result.Status);
                StringAssert.Contains(result.Message, "图像来源");
            }
        }

        [TestMethod]
        public void ImageOverload_LineFromNoUpstream_Fails()
        {
            // 单图验证没有上游：直线来源必然解析不到
            LineSource(new CvLine(0, 0, 10, 0));
            var result = _strategy.OnImage(_source, new FakeDisplay());
            Assert.AreEqual(RunStatus.Error, result.Status);
            StringAssert.Contains(result.Message, "未能解析");
        }

        [TestMethod]
        public void Params_Declared()
        {
            CollectionAssert.AreEqual(new[] { "图像来源", "直线来源", "对齐方式" }, _strategy.Labels(TabPageEnum.Parameter));
            Assert.AreEqual(OutEnum.Line, ((SourceParam)_strategy.Param("直线来源")).SourceType);
            Assert.IsTrue(_strategy.SetParam("对齐方式", AlignAxis.ParallelY));
            Assert.AreEqual(AlignAxis.ParallelY, _strategy.inPara.AlignAxis);
        }
    }
}
