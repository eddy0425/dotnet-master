using System;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HDisplay"/>：图像接管与布局、画笔状态、参数校验、交互绘制写回。
    /// </summary>
    /// <remarks>
    /// 用真实 <see cref="HWindowControl"/>（<see cref="WindowHost"/>，父容器 400x300）。
    /// 窗体建出来后当前线程有 WinForms 的 SynchronizationContext，绘制确认后的续体要 <see cref="WindowHost.Pump(Task, int)"/> 才会跑。
    /// </remarks>
    [TestClass]
    public class HDisplayTests : HalconTestBase
    {
        [TestCleanup]
        public void Cleanup() => DrawHelper.CancelDraw();

        private static void Run(Action<WindowHost, HDisplay> body) =>
            WindowHost.Run(host =>
            {
                using (var display = new HDisplay(host.Control))
                {
                    body(host, display);
                }
            });

        private static HImage Image(int width, int height) => new HImage("byte", width, height);

        private static void Part(WindowHost host, out int r1, out int c1, out int r2, out int c2) =>
            host.Window.GetPart(out r1, out c1, out r2, out c2);

        /// <summary>HALCON 没有 get_color，按 get_rgb 比较：red/green/blue 分别对应纯色分量。</summary>
        private static class Rgb
        {
            public const string Red = "255,0,0", Green = "0,255,0", Blue = "0,0,255";

            public static string Of(WindowHost host)
            {
                HOperatorSet.GetRgb(host.Window, out HTuple r, out HTuple g, out HTuple b);
                return $"{r.I},{g.I},{b.I}";
            }
        }

        private static void Confirm(WindowHost host) => DrawHelper.ForwardMouseUp(host.Window, Mouse.Right(0, 0));

        #region 构造与图像

        [TestMethod]
        public void Ctor_NullControl_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new HDisplay(null));
        }

        [TestMethod]
        public void Ctor_ShowsPlaceholder_800x600_AndFitsPartToIt()
        {
            Run((host, display) =>
            {
                Assert.AreEqual(800, display.HoWidth);
                Assert.AreEqual(600, display.HoHeight);
                Assert.AreEqual(new Point2d(400, 300), display.HoCentre);
                Assert.AreEqual(new Size2d(800, 600), display.HoSize);
                Assert.IsTrue(display.HoImage.NotNull());

                Part(host, out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(0, r1); Assert.AreEqual(0, c1);
                Assert.AreEqual(599, r2); Assert.AreEqual(799, c2);
            });
        }

        [TestMethod]
        public void SetImage_TakesACopy_WithoutDrawing()
        {
            Run((host, display) =>
            {
                using (var image = Image(200, 100))
                {
                    display.SetImage(image);

                    Assert.AreEqual(200, display.HoWidth);
                    Assert.AreEqual(100, display.HoHeight);
                    Assert.AreNotSame(image, display.HoImage, "应复制一份再接管");
                }
                Assert.IsTrue(display.HoImage.IsInitialized(), "调用方释放原图不影响已接管的副本");

                Part(host, out _, out _, out int r2, out int c2);
                Assert.AreEqual(599, r2, "SetImage 只接管不绘制，Part 不变");
                Assert.AreEqual(799, c2);
            });
        }

        [TestMethod]
        public void SetImage_InvalidArguments_Throw()
        {
            Run((host, display) =>
            {
                Assert.ThrowsException<ArgumentException>(() => display.SetImage(null));
                using (var empty = new HObject())
                    Assert.ThrowsException<ArgumentException>(() => display.SetImage(empty));

                display.Dispose();
                using (var image = Image(10, 10))
                    Assert.ThrowsException<ObjectDisposedException>(() => display.SetImage(image));
            });
        }

        [TestMethod]
        public void DispImage_Adaptive_ResetsPartToWholeImage()
        {
            Run((host, display) =>
            {
                host.Window.SetPart(10, 10, 50, 50);
                using (var image = Image(300, 200))
                    display.DispImage(image);

                Part(host, out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(0, r1); Assert.AreEqual(0, c1);
                Assert.AreEqual(199, r2); Assert.AreEqual(299, c2);
            });
        }

        [TestMethod]
        public void DispImage_NotAdaptive_KeepsCurrentPart()
        {
            Run((host, display) =>
            {
                display.Adaptive = false;
                host.Window.SetPart(10, 20, 50, 60);
                using (var image = Image(300, 200))
                    display.DispImage(image);

                Part(host, out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(10, r1); Assert.AreEqual(20, c1);
                Assert.AreEqual(50, r2); Assert.AreEqual(60, c2);
                Assert.AreEqual(300, display.HoWidth, "不重设 Part 也要接管新图");
            });
        }

        [TestMethod]
        public void DispImage_ExplicitIsSetPart_OverridesAdaptive()
        {
            Run((host, display) =>
            {
                display.Adaptive = false;
                host.Window.SetPart(10, 20, 50, 60);
                using (var image = Image(300, 200))
                    display.DispImage(image, true);

                Part(host, out _, out _, out int r2, out int c2);
                Assert.AreEqual(199, r2); Assert.AreEqual(299, c2);
            });
        }

        [TestMethod]
        public void DispImage_NullOrEmpty_IsIgnored()
        {
            Run((host, display) =>
            {
                var before = display.HoImage;
                display.DispImage(null);
                using (var empty = new HObject())
                    display.DispImage(empty);

                Assert.AreSame(before, display.HoImage);
                Assert.AreEqual(800, display.HoWidth);
            });
        }

        [TestMethod]
        public void DispImage_WithCross_SwitchesPenToRed()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Blue);
                display.IsCross = true;
                using (var image = Image(300, 200))
                    display.DispImage(image);

                Assert.AreEqual(HColor.Red, display.GetColor());
                Assert.AreEqual(Rgb.Red, Rgb.Of(host));
            });
        }

        [TestMethod]
        public void DispImage_WithCross_ResetsPenEvenIfWindowColourChangedBehindCache()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Red);
                display.IsCross = true;
                host.Window.SetColor("green"); // 例如 DrawRenderer 直接改窗口颜色，本地缓存仍是 Red

                using (var image = Image(300, 200))
                    display.DispImage(image);

                Assert.AreEqual(Rgb.Red, Rgb.Of(host));
            });
        }

        [TestMethod]
        public void ReDispImage_AfterDispose_IsNoOp()
        {
            Run((host, display) =>
            {
                display.Dispose();
                display.ReDispImage();
                using (var image = Image(10, 10))
                    display.DispImage(image);
                Assert.IsNull(display.HoImage, "释放后图像引用应置空，而不是悬挂句柄");
            });
        }

        #endregion

        #region 画笔状态

        [TestMethod]
        public void SetColor_Empty_FallsBackToRed()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Green);
                Assert.AreEqual(Rgb.Green, Rgb.Of(host));

                display.SetColor(default(HColor));
                Assert.AreEqual(HColor.Red, display.GetColor());
                Assert.AreEqual(Rgb.Red, Rgb.Of(host));
            });
        }

        [TestMethod]
        public void SetDraw_UnknownMode_WarnsAndFallsBackToMargin()
        {
            Run((host, display) =>
            {
                display.SetDraw("fill");
                Assert.AreEqual("fill", host.Window.GetDraw());

                using (var log = new CapturingLogger())
                {
                    display.SetDraw("dotted");
                    Assert.AreEqual("margin", host.Window.GetDraw());
                    Assert.AreEqual(1, log.Messages(LogLevel.Warn).Count(m => m.Contains("dotted")));
                }

                display.SetDraw("fill");
                display.SetDraw(null);
                Assert.AreEqual("margin", host.Window.GetDraw(), "空模式按 margin 处理");
            });
        }

        [TestMethod]
        public void Style_AppliesOnlySpecifiedItems()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Green);
                display.SetDraw("fill");

                display.Disp(new Point2d(10, 10), new DrawStyle { LineWidth = 3 });

                Assert.AreEqual(3, host.Window.GetLineWidth());
                Assert.AreEqual(Rgb.Green, Rgb.Of(host), "未指定颜色时保持现状");
                Assert.AreEqual("fill", host.Window.GetDraw(), "未指定填充模式时保持现状");
            });
        }

        [TestMethod]
        public void Style_NonPositiveLineWidth_ThrowsBeforeAnySideEffect()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Green);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    display.Disp(new Point2d(10, 10), new DrawStyle { LineWidth = 0 }));
            });
        }

        #endregion

        #region 参数校验

        [TestMethod]
        public void Disp_NullGeometry_Throws()
        {
            Run((host, display) =>
            {
                Assert.ThrowsException<ArgumentNullException>(() => display.Disp((CvLine)null));
                Assert.ThrowsException<ArgumentNullException>(() => display.Disp((CvArrow)null));
                Assert.ThrowsException<ArgumentNullException>(() => display.Disp((CvCircle)null));
                Assert.ThrowsException<ArgumentNullException>(() => display.Disp((System.Collections.Generic.IReadOnlyList<Point2d>)null));
                Assert.ThrowsException<ArgumentNullException>(() => display.DispLineWithEndMarker(null, 3));
            });
        }

        [TestMethod]
        public void DispLineWithEndMarker_InvalidRadius_ThrowsWithoutTouchingPen()
        {
            Run((host, display) =>
            {
                display.SetColor(HColor.Green);
                var line = new CvLine(new Point2d(0, 0), new Point2d(10, 10));

                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    display.DispLineWithEndMarker(line, 0, DrawStyle.Of(HColor.Blue)));
                Assert.AreEqual(HColor.Green, display.GetColor(), "校验应先于 ApplyStyle");

                display.DispLineWithEndMarker(line, 3, DrawStyle.Of(HColor.Blue));
                Assert.AreEqual(HColor.Red, display.GetColor(), "末端圆标记固定为红色");
            });
        }

        [TestMethod]
        public void DispSegmentWithCrosses_InvalidArm_Throws()
        {
            Run((host, display) =>
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                    display.DispSegmentWithCrosses(new Point2d(0, 0), new Point2d(5, 5), -1));
                display.DispSegmentWithCrosses(new Point2d(0, 0), new Point2d(5, 5), 2);
            });
        }

        [TestMethod]
        public void AllDispOverloads_DrawWithoutLoggingErrors()
        {
            Run((host, display) =>
            {
                using (var log = new CapturingLogger())
                using (var rect = new HRegion(10.0, 10, 50, 50))
                {
                    var style = new DrawStyle { Color = HColor.Blue, LineWidth = 2, DrawMode = "margin", Size = 6 };
                    display.Disp(new Point2d(10, 20), style);
                    display.Disp(new[] { new Point2d(1, 2), new Point2d(3, 4) }, style);
                    display.Disp(new CvCoord(new Point2d(30, 40), Angle.FromRadians(0.3)), style);
                    display.Disp(new CvLine(new Point2d(0, 0), new Point2d(50, 60)), style);
                    display.Disp(new CvCircle(new Point2d(50, 60), 20), style);
                    display.Disp(rect, style);
                    display.DispRect2(new Point2d(100, 100), 0.5, 30, 10, style);
                    display.DispText("abc", new Point2d(10, 10), style);

                    Assert.AreEqual(0, log.Entries.Count(e => e.Level >= LogLevel.Warn),
                        string.Join("; ", log.Entries.Select(e => e.Message)));
                }
            });
        }

        [TestMethod]
        public void DispRegionOutline_EveryType_DrawsWithoutErrors()
        {
            Run((host, display) =>
            {
                using (var log = new CapturingLogger())
                {
                    foreach (RectEnum type in Enum.GetValues(typeof(RectEnum)))
                    {
                        var region = new CvRegion { Type = type };
                        region.SetRectByCorners(20, 30, 120, 150);
                        region.PolygonX = new HTuple(30.0, 150.0, 90.0);
                        region.PolygonY = new HTuple(20.0, 20.0, 120.0);
                        display.DispRegionOutline(region, DrawStyle.Of(HColor.Green));
                    }
                    display.DispRegionOutline(null);

                    Assert.AreEqual(0, log.Entries.Count(e => e.Level >= LogLevel.Warn),
                        string.Join("; ", log.Entries.Select(e => e.Message + " " + e.Exception?.Message)));
                }
            });
        }

        #endregion

        #region 交互绘制写回 CvRegion

        [TestMethod]
        public void DrawRegionAsync_Rectangle_Confirmed_WritesGeometryAndRegion()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Rectangle };
                var task = display.DrawRegionAsync(region);
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(50, 30));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(200, 150));
                Confirm(host);
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.AreEqual(30, region.Top); Assert.AreEqual(50, region.Left);
                Assert.AreEqual(150, region.Bottom); Assert.AreEqual(200, region.Right);
                Assert.IsTrue(Contains(region.HoRegion, 90, 120));
                Assert.IsFalse(Contains(region.HoRegion, 10, 10));
            });
        }

        [TestMethod]
        public void DrawRegionAsync_Circle_Confirmed_WritesBoundsFromRadius()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Circle };
                var task = display.DrawRegionAsync(region);
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(100, 100));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(130, 140));
                Confirm(host);
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.AreEqual(100, region.CenterX, 1e-9);
                Assert.AreEqual(100, region.CenterY, 1e-9);
                Assert.AreEqual(100, region.Width, 1e-9, "外接框边长为直径");
                Assert.AreEqual(Math.PI * 50 * 50, Area(region.HoRegion), 400);
            });
        }

        [TestMethod]
        public void DrawRegionAsync_Cancelled_LeavesRegionUntouched()
        {
            Run((host, display) =>
            {
                foreach (RectEnum type in Enum.GetValues(typeof(RectEnum)))
                {
                    var region = new CvRegion { Type = type };
                    region.SetRectByCorners(1, 2, 3, 4);
                    var before = region.HoRegion;

                    var task = display.DrawRegionAsync(region);
                    DrawHelper.CancelDraw(host.Window);
                    WindowHost.Pump(task);

                    Assert.IsFalse(task.Result, type.ToString());
                    Assert.AreEqual(1, region.Top, type.ToString());
                    Assert.AreEqual(4, region.Right, type.ToString());
                    Assert.AreSame(before, region.HoRegion, type + "：取消时不应替换区域");
                }
            });
        }

        [TestMethod]
        public void DrawRegionModAsync_ConfirmedUntouched_KeepsGeometry()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Rectangle };
                region.SetRectByCorners(20, 30, 120, 150);

                var task = display.DrawRegionModAsync(region);
                Confirm(host);
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.AreEqual(20, region.Top); Assert.AreEqual(30, region.Left);
                Assert.AreEqual(120, region.Bottom); Assert.AreEqual(150, region.Right);
                Assert.IsTrue(Contains(region.HoRegion, 70, 90));
            });
        }

        [TestMethod]
        public void DrawRegionModAsync_Circle_UsesHalfWidthAsRadius()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Circle };
                region.SetRectByCenter(new Point2d(100, 80), new Size2d(60, 60));

                var task = display.DrawRegionModAsync(region);
                Confirm(host);
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.AreEqual(60, region.Width, 1e-9);
                Assert.AreEqual(Math.PI * 30 * 30, Area(region.HoRegion), 200);
            });
        }

        [TestMethod]
        public void DrawRegionAsync_Ring_TwoSteps_WritesConcentricRadii()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Ring };
                var task = display.DrawRegionAsync(region);

                // 第一步：外圆，半径 50
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(100, 100));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(130, 140));
                Confirm(host);
                WindowHost.Pump();
                Assert.IsFalse(task.IsCompleted, "外圆确认后还要调整内圆");
                Assert.IsTrue(DrawHelper.IsDrawing(host.Window));

                // 第二步：内圆初值为外圆一半，原样确认
                Confirm(host);
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.AreEqual(50, region.MaxRadius, 1e-9);
                Assert.AreEqual(25, region.MinRadius, 1e-9);
                Assert.AreEqual(25, region.RingWidth, 1e-9);
                Assert.AreEqual(100, region.Width, 1e-9);
                Assert.IsFalse(Contains(region.HoRegion, 100, 100), "圆心在内圆里，不属于圆环");
                Assert.IsTrue(Contains(region.HoRegion, 100, 140));
            });
        }

        [TestMethod]
        public void DrawRegionAsync_Ring_CancelledAtSecondStep_LeavesRegionUntouched()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Ring };
                double maxBefore = region.MaxRadius, minBefore = region.MinRadius;
                var task = display.DrawRegionAsync(region);

                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(100, 100));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(130, 140));
                Confirm(host);
                WindowHost.Pump();

                DrawHelper.CancelDraw(host.Window);
                WindowHost.Pump(task);

                Assert.IsFalse(task.Result);
                Assert.AreEqual(maxBefore, region.MaxRadius);
                Assert.AreEqual(minBefore, region.MinRadius);
            });
        }

        [TestMethod]
        public void DrawRegionAsync_Polygon_WritesVerticesAndCenter()
        {
            Run((host, display) =>
            {
                var region = new CvRegion { Type = RectEnum.Polygon };
                var task = display.DrawRegionAsync(region);
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(20, 20));
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(120, 20));
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(120, 120));
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(20, 120));
                Confirm(host); // 闭合
                Confirm(host); // 确认
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result);
                Assert.IsTrue(region.PolygonX.Length >= 4);
                Assert.AreEqual(region.PolygonX.Length, region.PolygonY.Length);
                Assert.AreEqual(70, region.Center.X, 1);
                Assert.AreEqual(70, region.Center.Y, 1);
                Assert.IsTrue(Contains(region.HoRegion, 70, 70));
                // 与其它 ROI 类型一致写入外接框（原先 Width/Height 停在 0）
                Assert.AreEqual(20, region.Left, 1);
                Assert.AreEqual(20, region.Y, 1);
                Assert.AreEqual(100, region.Width, 1);
                Assert.AreEqual(100, region.Height, 1);
            });
        }

        [TestMethod]
        public void DrawRegionAsync_NullRegion_ReturnsFalse()
        {
            Run((host, display) =>
            {
                Assert.IsFalse(display.DrawRegionAsync((CvRegion)null).Result);
                Assert.IsFalse(display.DrawRegionModAsync(null).Result);
                Assert.IsFalse(DrawHelper.IsDrawing(host.Window));
            });
        }

        [TestMethod]
        public void DrawRegionAsync_AfterDispose_ReturnsFalseWithoutSession()
        {
            Run((host, display) =>
            {
                display.Dispose();
                Assert.IsFalse(display.DrawRegionAsync(new CvRegion()).Result);
                Assert.IsFalse(DrawHelper.IsDrawing(host.Window));
            });
        }

        #endregion

        #region 交互绘制返回 HObject

        [TestMethod]
        public void DrawRegionAsyncByType_Confirmed_ReturnsRegion()
        {
            Run((host, display) =>
            {
                var task = display.DrawRegionAsync(RectEnum.Rectangle);
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(50, 30));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(200, 150));
                Confirm(host);
                WindowHost.Pump(task);

                using (var region = task.Result)
                {
                    Assert.AreEqual(1, CountObj(region));
                    Assert.AreEqual(151 * 121, Area(region), 1);
                }
            });
        }

        [TestMethod]
        public void DrawRegionAsyncByType_Ring_ReturnsDifference()
        {
            Run((host, display) =>
            {
                var task = display.DrawRegionAsync(RectEnum.Ring);
                DrawHelper.ForwardMouseDown(host.Window, Mouse.Left(100, 100));
                DrawHelper.ForwardMouseUp(host.Window, Mouse.Left(130, 140));
                Confirm(host);
                WindowHost.Pump();
                Confirm(host);
                WindowHost.Pump(task);

                using (var region = task.Result)
                {
                    Assert.AreEqual(1, CountObj(region));
                    Assert.IsFalse(Contains(region, 100, 100));
                    Assert.IsTrue(Contains(region, 100, 140));
                }
            });
        }

        [TestMethod]
        public void DrawRegionAsyncByType_Cancelled_ReturnsEmptyObjectTuple()
        {
            Run((host, display) =>
            {
                foreach (RectEnum type in Enum.GetValues(typeof(RectEnum)))
                {
                    var task = display.DrawRegionAsync(type);
                    DrawHelper.CancelDraw(host.Window);
                    WindowHost.Pump(task);

                    using (var region = task.Result)
                    {
                        Assert.IsNotNull(region, type.ToString());
                        Assert.AreEqual(0, CountObj(region), type + "：取消应返回空对象元组");
                    }
                }
            });
        }

        #endregion

        [TestMethod]
        public void Dispose_IsIdempotent_AndReleasesImage()
        {
            Run((host, display) =>
            {
                var image = display.HoImage;
                display.Dispose();
                display.Dispose();

                Assert.IsFalse(image.IsInitialized());
                Assert.IsNull(display.HoImage);
                Assert.IsTrue(host.Window.IsInitialized(), "窗口归宿主控件所有，HDisplay 不应关闭它");
            });
        }
    }
}
