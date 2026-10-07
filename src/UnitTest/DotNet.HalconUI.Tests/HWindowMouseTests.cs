using System;
using System.Collections.Generic;
using System.Linq;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HWindowMouse"/>：滚轮缩放、中键平移、双击复位、灰度回显。
    /// </summary>
    /// <remarks>
    /// 显示用真实 <see cref="HDisplay"/>：构造时载入 800x600 的全零占位图，Part 为 (0,0,599,799)。
    /// 事件处理方法是 public 的，直接调用，不经 HWindowControl 的事件转发。
    /// </remarks>
    [TestClass]
    public class HWindowMouseTests : HalconTestBase
    {
        private static void Run(Action<WindowHost, HWindowMouse> body) =>
            WindowHost.Run(host =>
            {
                using (var display = new HDisplay(host.Control))
                using (var mouse = new HWindowMouse(host.Control, display))
                {
                    body(host, mouse);
                }
            });

        private static double[] Part(WindowHost host)
        {
            HOperatorSet.GetPart(host.Window, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            return new[] { r1.D, c1.D, r2.D, c2.D };
        }

        private static void AssertPart(WindowHost host, double r1, double c1, double r2, double c2, string message = null)
        {
            var p = Part(host);
            string actual = string.Join(",", p);
            Assert.AreEqual(r1, p[0], 1, message + " 实际 " + actual);
            Assert.AreEqual(c1, p[1], 1, message + " 实际 " + actual);
            Assert.AreEqual(r2, p[2], 1, message + " 实际 " + actual);
            Assert.AreEqual(c2, p[3], 1, message + " 实际 " + actual);
        }

        [TestMethod]
        public void Ctor_NullArguments_Throw()
        {
            WindowHost.Run(host =>
            {
                Assert.ThrowsException<ArgumentNullException>(() => new HWindowMouse(null, new FakeDisplay()));
                Assert.ThrowsException<ArgumentNullException>(() => new HWindowMouse(host.Control, null));
            });
        }

        #region 滚轮缩放

        [TestMethod]
        public void WheelForward_ZoomsInAroundCursor()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, 120));

                // 以 (row 300, col 400) 为不动点，视野 600x800 像素缩到 1/1.5 即 400x533.3
                AssertPart(host, 100, 133.3, 499, 665.7);
            });
        }

        [TestMethod]
        public void WheelBackward_ZoomsOutAroundCursor()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, -120));

                // 缩小系数是放大的倒数：视野 600x800 → 900x1200（原先按 0.5 缩成 2 倍，且宽高少算 1）
                AssertPart(host, -150, -200, 749, 999);
            });
        }

        [TestMethod]
        public void WheelForwardThenBackward_RestoresOriginalPart()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, 120));
                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, -120));

                AssertPart(host, 0, 0, 599, 799, "放大再缩小一格应回到原视野");
            });
        }

        [TestMethod]
        public void Wheel_RepeatedRoundTrips_DoNotDrift()
        {
            Run((host, mouse) =>
            {
                for (int i = 0; i < 5; i++)
                {
                    mouse.OnHMouseWheel(null, Mouse.Wheel(123, 45, 120));
                    mouse.OnHMouseWheel(null, Mouse.Wheel(123, 45, -120));
                }

                AssertPart(host, 0, 0, 599, 799, "宽高少算 1 像素时每来回一次视野都会漂移");
            });
        }

        [TestMethod]
        public void WheelBackward_RefusesWhenResultWouldExceedHalconLimit()
        {
            Run((host, mouse) =>
            {
                // 25000x25000 本身在 32000² 以内，但再缩小一次就是 37500²，超出上限。
                // 原实现按缩小「前」的面积判断，会放行这一次。
                host.Window.SetPart(0, 0, 24999, 24999);

                mouse.OnHMouseWheel(null, Mouse.Wheel(0, 0, -120));

                AssertPart(host, 0, 0, 24999, 24999, "超出上限的缩小应被拒绝");
            });
        }

        [TestMethod]
        public void WheelForward_AlwaysAllowed_EvenForHugePart()
        {
            Run((host, mouse) =>
            {
                host.Window.SetPart(0, 0, 39999, 39999);
                mouse.OnHMouseWheel(null, Mouse.Wheel(0, 0, 120));

                var p = Part(host);
                Assert.IsTrue(p[2] < 39999, "放大不受上限限制");
            });
        }

        [TestMethod]
        public void Wheel_WithoutImage_IsIgnored()
        {
            WindowHost.Run(host =>
            {
                host.Window.SetPart(0, 0, 99, 99);
                using (var mouse = new HWindowMouse(host.Control, new FakeDisplay()))
                {
                    mouse.OnHMouseWheel(null, Mouse.Wheel(50, 50, 120));
                }
                AssertPart(host, 0, 0, 99, 99);
            });
        }

        #endregion

        #region 平移与双击

        [TestMethod]
        public void MiddleDrag_PansOppositeToMovement()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseDown(null, Mouse.Middle(100, 100));
                mouse.OnHMouseUp(null, Mouse.Middle(130, 120));

                // 向右下拖 (col +30, row +20)，视野向左上移
                AssertPart(host, -20, -30, 579, 769);
            });
        }

        [TestMethod]
        public void LeftDrag_DoesNotPan()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseDown(null, Mouse.Left(100, 100));
                mouse.OnHMouseUp(null, Mouse.Left(130, 120));

                AssertPart(host, 0, 0, 599, 799);
            });
        }

        [TestMethod]
        public void LeftDrag_AfterMiddleDownWithoutUp_DoesNotPan()
        {
            // 中键在控件外松开时收不到 Up；残留的平移状态不能让之后的左键拖拽（画 ROI）平移视图
            Run((host, mouse) =>
            {
                mouse.OnHMouseDown(null, Mouse.Middle(100, 100));
                ForgetLastClick(mouse); // 两次按下间隔超过双击时间，免得走双击分支顺带清掉状态

                mouse.OnHMouseDown(null, Mouse.Left(100, 100));
                mouse.OnHMouseUp(null, Mouse.Left(130, 120));

                AssertPart(host, 0, 0, 599, 799);
            });
        }

        private static void ForgetLastClick(HWindowMouse mouse)
        {
            var field = typeof(HWindowMouse).GetField("_lastClickMs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field == null) Assert.Fail("HWindowMouse._lastClickMs 已改名或移除，请同步更新 ForgetLastClick。");
            field.SetValue(mouse, null);
        }

        [TestMethod]
        public void DoubleClick_ResetsPartToWholeImage()
        {
            Run((host, mouse) =>
            {
                host.Window.SetPart(10, 10, 50, 50);

                mouse.OnHMouseDown(null, Mouse.Left(20, 20));
                mouse.OnHMouseDown(null, Mouse.Left(20, 20));

                AssertPart(host, 0, 0, 599, 799);
            });
        }

        [TestMethod]
        public void TripleClick_CountsAsOneDoubleClick()
        {
            Run((host, mouse) =>
            {
                mouse.OnHMouseDown(null, Mouse.Left(20, 20));
                mouse.OnHMouseDown(null, Mouse.Left(20, 20)); // 双击，消耗配对

                host.Window.SetPart(10, 10, 50, 50);
                mouse.OnHMouseDown(null, Mouse.Left(20, 20)); // 第三击只是新的单击起点

                AssertPart(host, 10, 10, 50, 50, "第三击不应再判成双击");
            });
        }

        #endregion

        #region 灰度回显

        [TestMethod]
        public void MouseUp_InsideImage_ReportsRowColumnAndGray()
        {
            Run((host, mouse) =>
            {
                var calls = new List<double[]>();
                mouse.RefreshUI += (r, c, g) => calls.Add(new[] { r.D, c.D, g.D });

                mouse.OnHMouseUp(null, Mouse.Left(10, 20));

                Assert.AreEqual(1, calls.Count);
                Assert.AreEqual(20, calls[0][0]);
                Assert.AreEqual(10, calls[0][1]);
                Assert.AreEqual(0, calls[0][2]);
            });
        }

        [TestMethod]
        public void MouseUp_SubscriberThrows_IsLoggedAsError()
        {
            Run((host, mouse) =>
            {
                mouse.RefreshUI += (r, c, g) => throw new InvalidOperationException("boom");

                using (var log = new CapturingLogger())
                {
                    mouse.OnHMouseUp(null, Mouse.Left(10, 20));

                    Assert.AreEqual(1, log.Messages(LogLevel.Error).Count(), "订阅方异常不能被当成“鼠标在图像外”吞成 Debug");
                    Assert.AreEqual(0, log.Messages(LogLevel.Debug).Count());
                }
            });
        }

        [TestMethod]
        public void MouseUp_OutsideImage_LogsDebugOnly()
        {
            Run((host, mouse) =>
            {
                int calls = 0;
                mouse.RefreshUI += (r, c, g) => calls++;

                using (var log = new CapturingLogger())
                {
                    mouse.OnHMouseUp(null, Mouse.Left(5000, 5000));

                    Assert.AreEqual(0, calls);
                    Assert.AreEqual(1, log.Messages(LogLevel.Debug).Count());
                    Assert.AreEqual(0, log.Entries.Count(e => e.Level >= LogLevel.Warn));
                }
            });
        }

        #endregion

        [TestMethod]
        public void Dispose_StopsHandlingAndDropsSubscribers()
        {
            Run((host, mouse) =>
            {
                int calls = 0;
                mouse.RefreshUI += (r, c, g) => calls++;

                mouse.Dispose();
                mouse.Dispose();

                mouse.OnHMouseWheel(null, Mouse.Wheel(400, 300, 120));
                mouse.OnHMouseUp(null, Mouse.Left(10, 20));

                AssertPart(host, 0, 0, 599, 799);
                Assert.AreEqual(0, calls);
            });
        }

        [TestMethod]
        public void NullEventArgs_AreIgnored()
        {
            Run((host, mouse) =>
            {
                using (var log = new CapturingLogger())
                {
                    mouse.OnHMouseDown(null, null);
                    mouse.OnHMouseUp(null, null);
                    mouse.OnHMouseWheel(null, null);
                    Assert.AreEqual(0, log.Entries.Count);
                }
            });
        }
    }
}
