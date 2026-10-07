using System;
using System.Drawing;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.VisionRuntime;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HDisplay"/> 保留运行结果叠加层：所有重画路径（缩放 / 平移 / 双击复位 / 尺寸变化 / ReDispImage）都重放它。
    /// </summary>
    /// <remarks>
    /// 叠加层是一块铺满整幅图的红色填充区域，底图是全黑的占位图：
    /// 窗口中心是红的说明叠加层画上去了，是黑的说明重画时丢了。
    /// </remarks>
    [TestClass]
    public class OverlayDisplayTests : HalconTestBase
    {
        private const string Red = "255,0,0", Black = "0,0,0";

        private static void Run(Action<WindowHost, HDisplay, HWindowMouse> body) =>
            WindowHost.Run(host =>
            {
                using (var display = new HDisplay(host.Control))
                using (var mouse = new HWindowMouse(host.Control, display, subscribe: false))
                {
                    body(host, display, mouse);
                }
            });

        private static OverlayList RedFill(HDisplay display)
        {
            var overlay = new OverlayList();
            HOperatorSet.GenRectangle1(out HObject all, 0, 0, display.HoHeight - 1, display.HoWidth - 1);
            using (all) overlay.Add(all, new DrawStyle { Color = HColor.Red, DrawMode = "fill" });
            return overlay;
        }

        /// <summary> 窗口中心像素的 "r,g,b"；窗口里只有灰度内容时 dump 出来是单通道 </summary>
        private static string CenterRgb(WindowHost host)
        {
            HOperatorSet.DumpWindowImage(out HObject dump, host.Window);
            using (dump)
            {
                HOperatorSet.GetImageSize(dump, out HTuple w, out HTuple h);
                HOperatorSet.CountChannels(dump, out HTuple channels);
                if (channels.I == 1)
                {
                    HOperatorSet.GetGrayval(dump, h.I / 2, w.I / 2, out HTuple gray);
                    return $"{gray.I},{gray.I},{gray.I}";
                }
                HOperatorSet.Decompose3(dump, out HObject r, out HObject g, out HObject b);
                using (r) using (g) using (b)
                {
                    HOperatorSet.GetGrayval(r, h.I / 2, w.I / 2, out HTuple vr);
                    HOperatorSet.GetGrayval(g, h.I / 2, w.I / 2, out HTuple vg);
                    HOperatorSet.GetGrayval(b, h.I / 2, w.I / 2, out HTuple vb);
                    return $"{vr.I},{vg.I},{vb.I}";
                }
            }
        }

        [TestMethod]
        public void SetOverlay_DrawsAndIsRetained()
        {
            Run((host, display, mouse) =>
            {
                Assert.AreEqual(Black, CenterRgb(host), "前提：占位图是黑的");
                var overlay = RedFill(display);

                display.SetOverlay(overlay);

                Assert.AreSame(overlay, display.Overlay);
                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void WheelZoom_ReplaysOverlay()
        {
            Run((host, display, mouse) =>
            {
                display.SetOverlay(RedFill(display));

                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, 120));
                Assert.AreEqual(Red, CenterRgb(host), "放大后");
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, -120));
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, -120));
                Assert.AreEqual(Red, CenterRgb(host), "缩小后");
            });
        }

        [TestMethod]
        public void MiddleDragPan_ReplaysOverlay()
        {
            Run((host, display, mouse) =>
            {
                display.SetOverlay(RedFill(display));

                mouse.OnHMouseDown(null, Mouse.Middle(400, 300));
                mouse.OnHMouseUp(null, Mouse.Middle(420, 310));

                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void DoubleClickReset_ReplaysOverlay()
        {
            Run((host, display, mouse) =>
            {
                display.SetOverlay(RedFill(display));
                mouse.OnHMouseWheel(null, Mouse.Wheel(100, 100, 120));

                mouse.OnHMouseDown(null, Mouse.Left(400, 300));
                mouse.OnHMouseDown(null, Mouse.Left(400, 300));

                host.Window.GetPart(out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(0, r1, "前提：双击复位了视野");
                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void ParentResize_ReplaysOverlay()
        {
            Run((host, display, mouse) =>
            {
                display.SetOverlay(RedFill(display));

                host.Panel.Size = new Size(300, 260);
                Application.DoEvents();

                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void ReDispImage_ReplaysOverlay()
        {
            Run((host, display, mouse) =>
            {
                display.SetOverlay(RedFill(display));
                host.Window.ClearWindow();

                display.ReDispImage();

                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void SetOverlay_ReplacesAndReleasesOld()
        {
            Run((host, display, mouse) =>
            {
                var first = RedFill(display);
                var second = new OverlayList();
                display.SetOverlay(first);

                display.SetOverlay(second);

                Assert.IsTrue(first.IsDisposed, "被替换的叠加层由显示控件释放");
                Assert.IsFalse(second.IsDisposed);
                Assert.AreEqual(Black, CenterRgb(host));
            });
        }

        [TestMethod]
        public void ClearOverlay_ReleasesAndRedrawsImageOnly()
        {
            Run((host, display, mouse) =>
            {
                var overlay = RedFill(display);
                display.SetOverlay(overlay);

                display.ClearOverlay();

                Assert.IsNull(display.Overlay);
                Assert.IsTrue(overlay.IsDisposed);
                Assert.AreEqual(Black, CenterRgb(host));
            });
        }

        /// <summary> 叠加层是对旧图算出来的：换新图时丢弃 </summary>
        [TestMethod]
        public void DispImage_NewImage_DropsOverlay()
        {
            Run((host, display, mouse) =>
            {
                var overlay = RedFill(display);
                display.SetOverlay(overlay);

                using (var image = new HImage("byte", 800, 600))
                {
                    display.DispImage(image);
                }

                Assert.IsNull(display.Overlay);
                Assert.IsTrue(overlay.IsDisposed);
                Assert.AreEqual(Black, CenterRgb(host));
            });
        }

        [TestMethod]
        public void ShowFrame_NewImageThenOverlay()
        {
            Run((host, display, mouse) =>
            {
                using (var image = new HImage("byte", 400, 300))
                {
                    var overlay = new OverlayList();
                    HOperatorSet.GenRectangle1(out HObject all, 0, 0, 299, 399);
                    using (all) overlay.Add(all, new DrawStyle { Color = HColor.Red, DrawMode = "fill" });

                    display.ShowFrame(image, overlay);

                    Assert.AreEqual(400, display.HoWidth, "底图换成了这一帧的图");
                    Assert.AreSame(overlay, display.Overlay, "叠加层在换图之后才放上去, 不会被换图丢弃");
                    Assert.AreEqual(Red, CenterRgb(host));
                }
            });
        }

        [TestMethod]
        public void ShowFrame_SameImage_KeepsImageHandle()
        {
            Run((host, display, mouse) =>
            {
                var current = display.HoImage;

                display.ShowFrame(current, RedFill(display));

                Assert.AreSame(current, display.HoImage, "底图就是当前图像时不重新复制");
                Assert.AreEqual(Red, CenterRgb(host));
            });
        }

        [TestMethod]
        public void Dispose_ReleasesOverlay_AndLaterOverlaysAreReleasedImmediately()
        {
            WindowHost.Run(host =>
            {
                var display = new HDisplay(host.Control);
                var overlay = RedFill(display);
                display.SetOverlay(overlay);

                display.Dispose();
                Assert.IsTrue(overlay.IsDisposed);

                var late = new OverlayList();
                display.SetOverlay(late);
                Assert.IsTrue(late.IsDisposed, "释放之后交进来的叠加层没人接手, 当场释放");
            });
        }
    }
}
