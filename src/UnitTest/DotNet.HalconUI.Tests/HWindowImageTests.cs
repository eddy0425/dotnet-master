using System;
using System.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HWindowImage"/>：按图像宽高比把控件缩放居中到父容器、跟随父容器缩放、图像所有权。
    /// </summary>
    /// <remarks>父容器固定 400x300（见 <see cref="WindowHost"/>）。</remarks>
    [TestClass]
    public class HWindowImageTests : HalconTestBase
    {
        private static void Run(Action<WindowHost, HWindowImage> body) =>
            WindowHost.Run(host =>
            {
                using (var image = new HWindowImage(host.Control))
                {
                    body(host, image);
                }
            });

        private static void Show(HWindowImage target, int width, int height, bool isSetPart = true)
        {
            using (var image = new HImage("byte", width, height))
                target.Fun_DispImage(image, isSetPart);
        }

        [TestMethod]
        public void Ctor_NullControl_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new HWindowImage(null));
        }

        [TestMethod]
        public void TallImage_FitsHeight_AndCentersHorizontally()
        {
            Run((host, target) =>
            {
                Show(target, 100, 400);

                Assert.AreEqual(new Size(75, 300), host.Control.Size);
                Assert.AreEqual(new Point(162, 0), host.Control.Location);
            });
        }

        [TestMethod]
        public void WideImage_FitsWidth_AndCentersVertically()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);

                Assert.AreEqual(new Size(400, 100), host.Control.Size);
                Assert.AreEqual(new Point(0, 100), host.Control.Location);
            });
        }

        [TestMethod]
        public void DispImage_SetsPartToWholeImage_OnlyWhenAsked()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);
                host.Window.GetPart(out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(0, r1); Assert.AreEqual(0, c1);
                Assert.AreEqual(199, r2); Assert.AreEqual(799, c2);

                host.Window.SetPart(5, 6, 50, 60);
                Show(target, 800, 200, isSetPart: false);
                host.Window.GetPart(out r1, out c1, out r2, out c2);
                Assert.AreEqual(5, r1); Assert.AreEqual(6, c1);
                Assert.AreEqual(50, r2); Assert.AreEqual(60, c2);
            });
        }

        [TestMethod]
        public void ParentResize_RelaysOutControl()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);

                host.Panel.Size = new Size(200, 300);
                System.Windows.Forms.Application.DoEvents();

                Assert.AreEqual(new Size(200, 50), host.Control.Size);
                Assert.AreEqual(new Point(0, 125), host.Control.Location);
            });
        }

        [TestMethod]
        public void HiddenControl_StillAdoptsImage_ButDoesNotLayout()
        {
            Run((host, target) =>
            {
                host.Control.Visible = false;
                Show(target, 100, 400);

                Assert.AreEqual(100, target.HoWidth, "画不了也要接管这一帧，否则 HoImage 停在旧图上");
                Assert.AreEqual(400, target.HoHeight);
                Assert.IsTrue(target.HoImage.IsInitialized());
                Assert.AreEqual(new Size(400, 300), host.Control.Size, "不可见时不调整布局");
            });
        }

        [TestMethod]
        public void HiddenControl_AppliesLayoutAndPart_OnceShownAgain()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);
                host.Control.Visible = false;
                Show(target, 100, 400);

                host.Control.Visible = true;
                System.Windows.Forms.Application.DoEvents();

                Assert.AreEqual(new Size(75, 300), host.Control.Size, "重新可见后应按新图布局");
                host.Window.GetPart(out int r1, out int c1, out int r2, out int c2);
                Assert.AreEqual(399, r2, "重新可见后应补上被挂起的 SetPart");
                Assert.AreEqual(99, c2);
            });
        }

        [TestMethod]
        public void FirstImage_WithDefaultSizeCacheValue_IsStillLaidOut()
        {
            // 1248x2200 是 ZoomImage 的默认值，也是本项目相机的实际分辨率
            Run((host, target) =>
            {
                Show(target, 1248, 2200);

                Assert.AreEqual(new Size(170, 300), host.Control.Size);
                Assert.AreEqual(new Point(115, 0), host.Control.Location);
            });
        }

        [TestMethod]
        public void SizeChange_KeepsCallerDrawMode()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);
                Assert.AreEqual("margin", host.Window.GetDraw(), "首次布局给默认 margin");

                host.Window.SetDraw("fill");
                Show(target, 100, 400);
                Assert.AreEqual("fill", host.Window.GetDraw(), "之后换尺寸不应覆盖调用方的画笔模式");
            });
        }

        [TestMethod]
        public void DispImage_Replaces_AndReleasesPreviousCopy()
        {
            Run((host, target) =>
            {
                Show(target, 100, 100);
                var first = target.HoImage;
                Show(target, 200, 100);

                Assert.IsFalse(first.IsInitialized(), "换图时应释放旧副本");
                Assert.AreEqual(200, target.HoWidth);
            });
        }

        [TestMethod]
        public void EmptyImage_ClearsWindow_WithoutReplacingCurrent()
        {
            Run((host, target) =>
            {
                Show(target, 100, 100);
                var current = target.HoImage;

                target.Fun_DispImage(null, true);
                using (var empty = new HObject())
                    target.Fun_DispImage(empty, true);

                Assert.AreSame(current, target.HoImage);
                Assert.IsTrue(current.IsInitialized());
            });
        }

        [TestMethod]
        public void Dispose_ReleasesImage_AndStopsFollowingParent()
        {
            Run((host, target) =>
            {
                Show(target, 800, 200);
                var image = target.HoImage;

                target.Dispose();
                target.Dispose();

                Assert.IsNull(target.HoImage);
                Assert.IsFalse(image.IsInitialized());

                host.Panel.Size = new Size(200, 300);
                System.Windows.Forms.Application.DoEvents();
                Assert.AreEqual(new Size(200, 100), host.Control.Size, "释放后只剩锚定的缩放，不再按图像比例布局");

                Show(target, 50, 50);
                Assert.IsNull(target.HoImage, "释放后不再接管图像");
            });
        }
    }
}
