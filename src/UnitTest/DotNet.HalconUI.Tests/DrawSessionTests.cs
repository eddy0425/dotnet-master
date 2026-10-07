using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconUI.Draw;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="DrawSession"/>：按窗口注册、鼠标事件分发、确认 / 取消 / 超时三条完成路径。
    /// </summary>
    [TestClass]
    public class DrawSessionTests : HalconTestBase
    {
        private HWindow _window;

        [TestInitialize]
        public void OpenWindow() => _window = BufferWindow();

        [TestCleanup]
        public void Cleanup()
        {
            DrawSession.CancelAll(null);
            _window?.Dispose();
        }

        #region 注册表

        [TestMethod]
        public void Begin_NullArguments_Throw()
        {
            Assert.ThrowsException<ArgumentNullException>(() => DrawSession.Begin(null, new PointShape()));
            Assert.ThrowsException<ArgumentNullException>(() => DrawSession.Begin(_window, null));
            Assert.IsNull(DrawSession.ActiveFor(_window), "参数校验失败不应留下注册项");
        }

        [TestMethod]
        public void ActiveFor_Null_ReturnsNull()
        {
            using (DrawSession.Begin(_window, new PointShape()))
            {
                Assert.IsNull(DrawSession.ActiveFor(null));
            }
        }

        [TestMethod]
        public void ActiveFor_ReturnsLatestSessionOnSameWindow()
        {
            using (var first = DrawSession.Begin(_window, new PointShape()))
            using (var second = DrawSession.Begin(_window, new LineShape()))
            {
                Assert.AreSame(second, DrawSession.ActiveFor(_window));

                second.Dispose();
                Assert.AreSame(first, DrawSession.ActiveFor(_window), "摘掉新会话后应露出旧会话");
            }
            Assert.IsNull(DrawSession.ActiveFor(_window));
        }

        [TestMethod]
        public void Sessions_OnDifferentWindows_DoNotInterfere()
        {
            using (var other = BufferWindow())
            using (var a = DrawSession.Begin(_window, new PointShape()))
            using (var b = DrawSession.Begin(other, new PointShape()))
            {
                Assert.AreSame(a, DrawSession.ActiveFor(_window));
                Assert.AreSame(b, DrawSession.ActiveFor(other));

                DrawSession.CancelAll(other);

                Assert.AreSame(a, DrawSession.ActiveFor(_window), "取消另一窗口不应影响本窗口");
                Assert.IsNull(DrawSession.ActiveFor(other));
            }
        }

        [TestMethod]
        public void CancelAll_Null_CancelsEveryWindow()
        {
            using (var other = BufferWindow())
            {
                var a = DrawSession.Begin(_window, new PointShape());
                var b = DrawSession.Begin(other, new PointShape());
                var ta = a.WaitForCompletionAsync(TimeSpan.Zero, CancellationToken.None);
                var tb = b.WaitForCompletionAsync(TimeSpan.Zero, CancellationToken.None);

                DrawSession.CancelAll(null);

                Assert.IsFalse(ta.Result);
                Assert.IsFalse(tb.Result);
                Assert.IsNull(DrawSession.ActiveFor(_window));
                Assert.IsNull(DrawSession.ActiveFor(other));
            }
        }

        [TestMethod]
        public void Dispose_IsIdempotent_AndCompletesPendingWaitWithFalse()
        {
            var session = DrawSession.Begin(_window, new PointShape());
            var wait = session.WaitForCompletionAsync(TimeSpan.Zero, CancellationToken.None);
            Assert.IsFalse(wait.IsCompleted);

            session.Dispose();
            session.Dispose();

            Assert.IsTrue(wait.IsCompleted);
            Assert.IsFalse(wait.Result);
            Assert.IsNull(DrawSession.ActiveFor(_window));
        }

        #endregion

        #region 完成路径

        [TestMethod]
        public async Task RightClickConfirm_CompletesTrue_AndUnregisters()
        {
            var shape = new LineShape();
            using (var session = DrawSession.Begin(_window, shape))
            {
                var wait = session.WaitForCompletionAsync(TimeSpan.Zero, CancellationToken.None);

                session.OnMouseDown(Mouse.Left(10, 20));
                session.OnMouseMove(Mouse.Move(60, 20));
                session.OnMouseUp(Mouse.Left(110, 20));
                Assert.IsFalse(wait.IsCompleted, "进入编辑阶段还不算完成");

                session.OnMouseUp(Mouse.Right(110, 20));

                Assert.IsTrue(await wait);
                Assert.IsTrue(session.Completed);
                Assert.IsNull(DrawSession.ActiveFor(_window), "定稿后应立即注销");
            }

            Assert.AreEqual(10, shape.X1); Assert.AreEqual(20, shape.Y1);
            Assert.AreEqual(110, shape.X2); Assert.AreEqual(20, shape.Y2);
        }

        [TestMethod]
        public async Task Timeout_CompletesFalse_AndLogsWarn()
        {
            using (var log = new CapturingLogger())
            using (var session = DrawSession.Begin(_window, new PointShape()))
            {
                bool ok = await session.WaitForCompletionAsync(TimeSpan.FromMilliseconds(30), CancellationToken.None);

                Assert.IsFalse(ok);
                Assert.IsFalse(session.Completed);
                Assert.IsNull(DrawSession.ActiveFor(_window));
                Assert.IsTrue(log.Entries.Any(e => e.Level == LogLevel.Warn && e.Category == DrawSafe.Category && e.Message.Contains("自动结束")),
                    "超时应以 Warn 记录，提示宿主可能未转发鼠标事件");
            }
        }

        [TestMethod]
        public void NonPositiveTimeout_MeansNoTimeout()
        {
            using (var session = DrawSession.Begin(_window, new PointShape()))
            {
                var wait = session.WaitForCompletionAsync(TimeSpan.FromMilliseconds(-1), CancellationToken.None);
                Thread.Sleep(50);
                Assert.IsFalse(wait.IsCompleted);
            }
        }

        [TestMethod]
        public async Task CallerCancel_CompletesFalse_AndLogsInfo()
        {
            using (var log = new CapturingLogger())
            using (var cts = new CancellationTokenSource())
            using (var session = DrawSession.Begin(_window, new PointShape()))
            {
                var wait = session.WaitForCompletionAsync(TimeSpan.Zero, cts.Token);
                cts.Cancel();

                Assert.IsFalse(await wait);
                CollectionAssert.Contains(log.Messages(LogLevel.Info).ToList(), "交互绘制被调用方取消.");
            }
        }

        [TestMethod]
        public void AlreadyCancelledToken_CompletesSynchronously()
        {
            using (var session = DrawSession.Begin(_window, new PointShape()))
            {
                var wait = session.WaitForCompletionAsync(TimeSpan.FromMinutes(1), new CancellationToken(true));

                Assert.IsTrue(wait.IsCompleted);
                Assert.IsFalse(wait.Result);
            }
        }

        [TestMethod]
        public void WaitAfterFinish_ReturnsSameCompletedResult()
        {
            var session = DrawSession.Begin(_window, new PointShape());
            DrawSession.CancelAll(_window);

            var wait = session.WaitForCompletionAsync(TimeSpan.FromMinutes(1), CancellationToken.None);

            Assert.IsTrue(wait.IsCompleted);
            Assert.IsFalse(wait.Result);
        }

        [TestMethod]
        public void ConfirmWins_LaterCancelIsIgnored()
        {
            var shape = new PointShape();
            using (var session = DrawSession.Begin(_window, shape))
            {
                var wait = session.WaitForCompletionAsync(TimeSpan.Zero, CancellationToken.None);
                session.OnMouseDown(Mouse.Left(5, 5));
                session.OnMouseUp(Mouse.Right(5, 5));

                DrawSession.CancelAll(null);

                Assert.IsTrue(wait.Result, "结果先到者胜");
            }
        }

        #endregion

        #region 鼠标分发与渲染

        [TestMethod]
        public void MouseMove_NearHandle_SetsHover()
        {
            var shape = new LineShape { X1 = 50, Y1 = 50, X2 = 150, Y2 = 50 };
            shape.BeginEdit();
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.OnMouseMove(Mouse.Move(52, 51));
                Assert.AreEqual(DrawHandle.P1, shape.Hover);

                session.OnMouseMove(Mouse.Move(100, 48));
                Assert.AreEqual(DrawHandle.Center, shape.Hover);

                session.OnMouseMove(Mouse.Move(100, 120));
                Assert.AreEqual(DrawHandle.None, shape.Hover);
            }
        }

        [TestMethod]
        public void MouseDown_OnHandle_DoesNotSnapGeometry_UntilNextMove()
        {
            // 回归：按下瞬间若走拖拽分支，控制点会被吸附到鼠标位置（Rect2 / Ellipse 会瞬间旋转）
            var shape = new CircleShape { CX = 100, CY = 100, Radius = 30 };
            shape.BeginEdit();
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.OnMouseMove(Mouse.Move(106, 100));
                Assert.AreEqual(DrawHandle.Center, shape.Hover);

                session.OnMouseDown(Mouse.Left(106, 100));

                Assert.IsTrue(shape.Dragging, "按下后应处于拖拽状态");
                Assert.AreEqual(100, shape.CX, "按下这一帧不应移动圆心");

                session.OnMouseMove(Mouse.Move(120, 110));
                Assert.AreEqual(120, shape.CX);
                Assert.AreEqual(110, shape.CY);

                session.OnMouseUp(Mouse.Left(120, 110));
                Assert.IsFalse(shape.Dragging);
            }
        }

        [TestMethod]
        public void Rect2_DragAxisEnd_RotatesAndResizes()
        {
            var shape = new Rect2Shape { CX = 100, CY = 100, Phi = 0, HalfLen1 = 40, HalfLen2 = 10 };
            shape.BeginEdit();
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.OnMouseMove(Mouse.Move(140, 100));
                Assert.AreEqual(DrawHandle.AxisEnd1, shape.Hover);

                session.OnMouseDown(Mouse.Left(140, 100));
                session.OnMouseMove(Mouse.Move(100, 50));

                Assert.AreEqual(50, shape.HalfLen1, 1e-9);
                Assert.AreEqual(Math.PI / 2, shape.Phi, 1e-9);
                Assert.AreEqual(10, shape.HalfLen2, 1e-9, "拖主轴不应改变短轴");
            }
        }

        [TestMethod]
        public void Region_DragVertex_MovesOnlyThatVertex()
        {
            var shape = new RegionShape();
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.OnMouseDown(Mouse.Left(20, 20));
                session.OnMouseDown(Mouse.Left(120, 20));
                session.OnMouseDown(Mouse.Left(20, 120));
                session.OnMouseUp(Mouse.Right(0, 0));
                Assert.AreEqual(DrawPhase.Editing, shape.Phase);

                session.OnMouseMove(Mouse.Move(121, 21));
                Assert.AreEqual(DrawHandle.P1, shape.Hover);

                session.OnMouseDown(Mouse.Left(121, 21));
                session.OnMouseMove(Mouse.Move(150, 40));

                Assert.AreEqual(150, shape.Cols[1]);
                Assert.AreEqual(40, shape.Rows[1]);
                Assert.AreEqual(20, shape.Cols[0], "其它顶点不动");
            }
        }

        [TestMethod]
        public void RenderInitial_ResetsHoverAndDragging()
        {
            var shape = new CircleShape { CX = 50, CY = 50, Radius = 10 };
            shape.BeginEdit();
            shape.Hover = DrawHandle.Center;
            shape.Dragging = true;
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.RenderInitial(withShape: true);

                Assert.AreEqual(DrawHandle.None, shape.Hover);
                Assert.IsFalse(shape.Dragging);
                Assert.AreEqual(50, shape.CX, "初始渲染不改几何");
            }
        }

        [TestMethod]
        public void RenderInitial_WithoutShape_LeavesShapeUntouched()
        {
            // 新建会话只画背景：图元仍停在 Idle，Hover / Dragging 不被改写
            var shape = new CircleShape();
            shape.Hover = DrawHandle.Center;
            using (var session = DrawSession.Begin(_window, shape))
            {
                session.RenderInitial(withShape: false);

                Assert.AreEqual(DrawPhase.Idle, shape.Phase);
                Assert.AreEqual(DrawHandle.Center, shape.Hover);
                Assert.IsFalse(shape.Dragging);
                Assert.IsFalse(shape.Completed);
            }
        }

        [TestMethod]
        public void AllShapes_RenderEveryPhase_WithoutDrawFailures()
        {
            using (var log = new CapturingLogger())
            {
                foreach (var shape in new DrawShape[] { new PointShape(), new LineShape(), new Rect1Shape(), new Rect2Shape(), new CircleShape(), new EllipseShape(), new RegionShape() })
                {
                    using (var session = DrawSession.Begin(_window, shape))
                    {
                        session.OnMouseMove(Mouse.Move(30, 30));
                        session.OnMouseDown(Mouse.Left(30, 30));
                        session.OnMouseMove(Mouse.Move(80, 60));
                        session.OnMouseUp(Mouse.Left(80, 60));
                        session.OnMouseDown(Mouse.Left(30, 100));
                        session.OnMouseUp(Mouse.Right(30, 100));
                        session.OnMouseMove(Mouse.Move(31, 31));
                        session.RenderInitial(withShape: true);
                        session.RenderInitial(withShape: false);
                    }
                }

                var warns = log.Entries.Where(e => e.Level == LogLevel.Warn).Select(e => e.Message).ToList();
                Assert.AreEqual(0, warns.Count, "绘制不应失败：" + string.Join(" | ", warns));
            }
        }

        #endregion
    }

    /// <summary>
    /// <see cref="DrawRenderer"/>：缩放比例、窗口状态的切换与还原、绘制失败的日志节流。
    /// </summary>
    [TestClass]
    public class DrawRendererTests : HalconTestBase
    {
        [TestMethod]
        public void Ctor_NullWindow_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DrawRenderer(null));
        }

        [TestMethod]
        public void PixelSize_IsImagePixelsPerWindowPixel()
        {
            using (var window = BufferWindow(320, 240))
            using (var renderer = new DrawRenderer(window))
            {
                Assert.AreEqual(1, renderer.PixelSize, "刷新前为默认值 1");

                renderer.RefreshPixelSize();
                Assert.AreEqual(1, renderer.PixelSize, 1e-9);

                // 显示区域是窗口的 2 倍（缩小显示），一个屏幕像素对应 2 个图像像素
                window.SetPart(0, 0, 479, 639);
                renderer.RefreshPixelSize();
                Assert.AreEqual(2, renderer.PixelSize, 1e-9);
            }
        }

        [TestMethod]
        public void Session_OnBufferWindow_DegradesGracefully()
        {
            // HALCON 22.11 不支持 autodraw 系统参数，缓冲窗口也不支持 flush 窗口参数；
            // 渲染器应只记 Debug 并继续工作，而不是抛出或记 Warn。
            using (var log = new CapturingLogger())
            using (var window = BufferWindow())
            {
                var renderer = new DrawRenderer(window);
                renderer.Cross(10, 10, "red");

                renderer.Dispose();
                renderer.Dispose();

                Assert.IsFalse(log.Entries.Any(e => e.Level >= LogLevel.Warn),
                    string.Join("; ", log.Entries.Select(e => e.Level + ": " + e.Message)));
            }
        }

        [TestMethod]
        public void RestoreBackground_ErasesOverlay()
        {
            using (var window = BufferWindow(100, 100))
            {
                HOperatorSet.GenImageConst(out HObject blank, "byte", 100, 100);
                using (blank)
                using (var full = Rectangle1(0, 0, 99, 99))
                {
                    HOperatorSet.PaintRegion(full, blank, out HObject gray, 100, "fill");
                    using (gray) window.DispObj(gray);
                }

                using (var renderer = new DrawRenderer(window))
                {
                    renderer.Rect1(10, 10, 90, 90, "white");
                    renderer.RestoreBackground();

                    HOperatorSet.DumpWindowImage(out HObject dump, window);
                    using (dump)
                    {
                        HOperatorSet.GetGrayval(dump, 10, 50, out HTuple value);
                        Assert.AreEqual(100, value[0].I, "叠加层应被背景快照覆盖");
                    }
                }
            }
        }

        [TestMethod]
        public void DrawFailure_IsLoggedOncePerSession()
        {
            using (var log = new CapturingLogger())
            {
                var window = BufferWindow();
                var renderer = new DrawRenderer(window);
                HOperatorSet.CloseWindow(window); // 模拟拖拽中窗口被关闭（HWindow.Dispose 在句柄仍被引用时不会真正关闭）

                try
                {
                    renderer.Cross(10, 10, "red");
                    renderer.Line(0, 0, 10, 10, "red");
                    renderer.Circle(10, 10, 5, "red");
                }
                finally
                {
                    renderer.Dispose();
                    window.Dispose();
                }

                var warns = log.Entries.Where(e => e.Level == LogLevel.Warn && e.Category == DrawSafe.Category).ToList();
                Assert.AreEqual(1, warns.Count, "同一会话内的绘制失败只记一条");
                StringAssert.StartsWith(warns[0].Message, "Cross 绘制失败");
                Assert.IsNotNull(warns[0].Exception);
            }
        }
    }
}
