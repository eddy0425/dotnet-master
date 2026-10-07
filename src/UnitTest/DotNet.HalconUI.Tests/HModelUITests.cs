using System;
using System.IO;
using System.Reflection;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HModelUI"/>：把匹配位姿处的模板区域 / 轮廓平移到模板图中心显示，并在释放时归还句柄。
    /// </summary>
    /// <remarks>模板字段是 private 的，经反射读取。</remarks>
    [TestClass]
    public class HModelUITests : HalconTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // 模板图 200 × 160 → 中心 (row 80, column 100)；匹配位姿 (row 40, column 50)
        private const int ImageWidth = 200, ImageHeight = 160;
        private const double ResultRow = 40, ResultCol = 50;
        private static readonly ModelResult Result = new ModelResult(ResultRow, ResultCol, 0.3, 1);

        private string _imagePath;

        [TestInitialize]
        public void Setup() => _imagePath = WriteTempImage(ImageWidth, ImageHeight);

        [TestCleanup]
        public void Cleanup()
        {
            if (_imagePath != null && File.Exists(_imagePath)) File.Delete(_imagePath);
        }

        private static void Run(Action<HModelUI> body) =>
            Sta.Run(() =>
            {
                var ui = new HModelUI();
                using (WindowHost.ShowOffscreen(ui))
                    body(ui);
            });

        private static T Field<T>(HModelUI ui, string name) =>
            (T)typeof(HModelUI).GetField(name, Private).GetValue(ui);

        private static HObject TemplateRect() => Rectangle1(ResultRow - 10, ResultCol - 10, ResultRow + 10, ResultCol + 10);

        private static HObject TemplateContour()
        {
            HOperatorSet.GenContourPolygonXld(out HObject contour,
                new HTuple(ResultRow - 10, ResultRow - 10, ResultRow + 10, ResultRow + 10, ResultRow - 10),
                new HTuple(ResultCol - 10, ResultCol + 10, ResultCol + 10, ResultCol - 10, ResultCol - 10));
            return contour;
        }

        [TestMethod]
        public void DisplayModel_MovesRegionAndContourToImageCentre()
        {
            Run(ui =>
            {
                using (var rect = TemplateRect())
                using (var contour = TemplateContour())
                {
                    ui.DisplayModel(_imagePath, rect, contour, Result);

                    var modeRect = Field<HObject>(ui, "_modeRect");
                    Centre(modeRect, out double row, out double col);
                    Assert.AreEqual(ImageHeight / 2.0, row, 1);
                    Assert.AreEqual(ImageWidth / 2.0, col, 1);

                    var trans = Field<HObject>(ui, "_contour");
                    HOperatorSet.GetObjClass(trans, out HTuple cls);
                    Assert.AreEqual("xld_cont", cls.S, "XLD 轮廓应走 affine_trans_contour_xld，仍是 XLD");
                    Centre(trans, out row, out col);
                    Assert.AreEqual(ImageHeight / 2.0, row, 1);
                    Assert.AreEqual(ImageWidth / 2.0, col, 1);

                    var coord = Field<CvCoord>(ui, "_coord");
                    Assert.AreEqual(ImageWidth / 2.0, coord.X, 1e-6);
                    Assert.AreEqual(ImageHeight / 2.0, coord.Y, 1e-6);
                    Assert.AreEqual(0.3, coord.Angle.Radians, 1e-9, "只平移，朝向沿用匹配结果");

                    Assert.IsTrue(rect.IsInitialized() && contour.IsInitialized(), "入参是借用的，不得释放");
                }
            });
        }

        [TestMethod]
        public void DisplayModel_NullOrEmptyObjects_YieldEmptyObjects()
        {
            Run(ui =>
            {
                using (var empty = EmptyObj())
                {
                    ui.DisplayModel(_imagePath, null, empty, Result);

                    Assert.AreEqual(0, CountObj(Field<HObject>(ui, "_modeRect")));
                    Assert.AreEqual(0, CountObj(Field<HObject>(ui, "_contour")));
                    ui.OnMouseMove(null, Mouse.Move(0, 0));
                }
            });
        }

        [TestMethod]
        public void DisplayModel_Again_ReleasesPreviousObjects()
        {
            Run(ui =>
            {
                using (var rect = TemplateRect())
                using (var contour = TemplateContour())
                {
                    ui.DisplayModel(_imagePath, rect, contour, Result);
                    var names = new[] { "_srcImage", "_modeRect", "_contour" };
                    var first = Array.ConvertAll(names, n => Field<HObject>(ui, n));

                    ui.DisplayModel(_imagePath, rect, contour, Result);

                    for (int i = 0; i < names.Length; i++)
                    {
                        Assert.IsFalse(first[i].IsInitialized(), names[i] + " 的旧对象应被释放");
                        Assert.IsTrue(Field<HObject>(ui, names[i]).IsInitialized());
                    }
                }
            });
        }

        [TestMethod]
        public void DisplayModel_MissingImage_KeepsPreviousModel()
        {
            // 读图失败时，已显示的模板不能被拆成半释放状态：之后鼠标移动还要拿这些字段重绘
            Run(ui =>
            {
                using (var rect = TemplateRect())
                using (var contour = TemplateContour())
                {
                    ui.DisplayModel(_imagePath, rect, contour, Result);
                    var names = new[] { "_srcImage", "_modeRect", "_contour" };
                    var before = Array.ConvertAll(names, n => Field<HObject>(ui, n));

                    Assert.ThrowsException<HOperatorException>(() =>
                        ui.DisplayModel(_imagePath + ".missing.png", rect, contour, Result));

                    for (int i = 0; i < names.Length; i++)
                    {
                        Assert.AreSame(before[i], Field<HObject>(ui, names[i]), names[i]);
                        Assert.IsTrue(before[i].IsInitialized(), names[i] + " 不应被释放");
                    }
                }
            });
        }

        [TestMethod]
        public void MouseMove_BeforeDisplayModel_DoesNotThrow()
        {
            Run(ui => ui.OnMouseMove(null, Mouse.Move(10, 10)));
        }

        [TestMethod]
        public void Dispose_ReleasesHandles()
        {
            Sta.Run(() =>
            {
                var ui = new HModelUI();
                var form = WindowHost.ShowOffscreen(ui);
                using (var rect = TemplateRect())
                using (var contour = TemplateContour())
                    ui.DisplayModel(_imagePath, rect, contour, Result);

                var names = new[] { "_srcImage", "_modeRect", "_contour" };
                var objects = Array.ConvertAll(names, n => Field<HObject>(ui, n));

                using (var log = new CapturingLogger())
                {
                    form.Dispose();
                    Assert.AreEqual(0, log.Entries.FindAll(e => e.Level >= LogLevel.Warn).Count);
                }

                Assert.IsTrue(ui.IsDisposed);
                for (int i = 0; i < names.Length; i++)
                    Assert.IsFalse(objects[i].IsInitialized(), names[i] + " 应随控件释放");
            });
        }
    }
}
