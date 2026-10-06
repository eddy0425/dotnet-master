using System;
using System.Threading;
using System.Threading.Tasks;
using DotNet.HalconUI.Draw;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="DrawHelper"/> 公开入口：Draw*Async / Draw*ModAsync 的结果换算、取消、超时与事件转发。
    /// </summary>
    /// <remarks>
    /// 鼠标事件通过 <c>Forward*</c> 注入，与宿主控件的转发路径一致。
    /// 测试没有 SynchronizationContext，确认时续体内联在 Forward 调用栈上跑完，返回后 Task 即已完成。
    /// </remarks>
    [TestClass]
    public class DrawHelperTests : HalconTestBase
    {
        private HWindow _window;

        [TestInitialize]
        public void OpenWindow() => _window = BufferWindow();

        [TestCleanup]
        public void Cleanup()
        {
            DrawHelper.CancelDraw();
            _window?.Dispose();
        }

        private void Confirm() => DrawHelper.ForwardMouseUp(_window, Mouse.Right(0, 0));

        #region 参数与会话管理

        [TestMethod]
        public async Task NullWindow_FaultsWithArgumentNull()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => DrawHelper.DrawPointAsync(null));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => DrawHelper.DrawCircleModAsync(null, 1, 1, 1));
        }

        [TestMethod]
        public void DefaultTimeout_IsFiveMinutes()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(5), DrawSession.DefaultTimeout);
            Assert.AreEqual(DrawSession.DefaultTimeout, DrawHelper.DefaultTimeout);
        }

        [TestMethod]
        public void IsDrawing_TracksSessionLifetime()
        {
            Assert.IsFalse(DrawHelper.IsDrawing(_window));
            Assert.IsFalse(DrawHelper.IsDrawing(null));

            var task = DrawHelper.DrawPointAsync(_window);
            Assert.IsTrue(DrawHelper.IsDrawing(_window));

            DrawHelper.CancelDraw(_window);

            Assert.IsFalse(DrawHelper.IsDrawing(_window));
            Assert.IsFalse(task.Result.Completed);
        }

        [TestMethod]
        public void NewDraw_SupersedesPendingDrawOnSameWindow()
        {
            var first = DrawHelper.DrawLineAsync(_window);
            var second = DrawHelper.DrawCircleAsync(_window);

            Assert.IsTrue(first.IsCompleted, "新会话开始前应顶掉旧会话");
            Assert.IsFalse(first.Result.Completed);
            Assert.IsFalse(second.IsCompleted);

            DrawHelper.CancelDraw();
            Assert.IsFalse(second.Result.Completed);
        }

        [TestMethod]
        public void NewDraw_StartedFromSupersededContinuation_IsNotOrphaned()
        {
            // 顶掉旧会话时其续体内联执行；续体里立即再发起一次绘制（例如“取消后自动重画”）。
            // 原实现里这次绘制注册在外层新会话之前，被遮住后收不到任何鼠标事件，IsDrawing 一直为 true。
            Task<DrawLineResult> inner = null;
            var first = DrawHelper.DrawPointAsync(_window);
            first.ContinueWith(_ => inner = DrawHelper.DrawLineAsync(_window), TaskContinuationOptions.ExecuteSynchronously);

            var outer = DrawHelper.DrawPointAsync(_window);
            Assert.IsNotNull(inner, "续体应已内联执行");
            Assert.IsTrue(inner.IsCompleted, "续体发起的会话应被外层新会话顶掉，而不是悬着");
            Assert.IsFalse(inner.Result.Completed);

            DrawHelper.ForwardMouseDown(_window, Mouse.Left(30, 40));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(30, 40));
            Confirm();

            Assert.IsTrue(outer.Result.Completed, "鼠标事件应派给外层会话");
            Assert.IsFalse(DrawHelper.IsDrawing(_window), "不应残留孤儿会话");
        }

        [TestMethod]
        public void CancelDraw_OnOtherWindow_LeavesThisSessionRunning()
        {
            using (var other = BufferWindow())
            {
                var task = DrawHelper.DrawPointAsync(_window);
                DrawHelper.CancelDraw(other);

                Assert.IsFalse(task.IsCompleted);
                Assert.IsTrue(DrawHelper.IsDrawing(_window));
            }
        }

        [TestMethod]
        public void Forward_WithoutSession_IsNoOp()
        {
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(1, 1));
            DrawHelper.ForwardMouseUp(_window, Mouse.Right(1, 1));
            DrawHelper.ForwardMouseMove(_window, Mouse.Move(1, 1));
            DrawHelper.ForwardMouseWheel(_window, Mouse.Move(1, 1));
            DrawHelper.ForwardMouseDown(null, Mouse.Left(1, 1));
        }

        [TestMethod]
        public async Task Timeout_CompletesWithFalse()
        {
            // 超时按次传入: 原来改的是全进程共用的静态属性, 测试之间、窗口之间互相影响
            var result = await DrawHelper.DrawPointAsync(_window, timeout: TimeSpan.FromMilliseconds(30));

            Assert.IsFalse(result.Completed);
            Assert.IsFalse(DrawHelper.IsDrawing(_window));
        }

        [TestMethod]
        public async Task CallerToken_CancelsDraw()
        {
            using (var cts = new CancellationTokenSource())
            {
                var task = DrawHelper.DrawLineAsync(_window, cts.Token);
                cts.Cancel();

                Assert.IsFalse((await task).Completed);
            }
        }

        #endregion

        #region 新建 ROI

        [TestMethod]
        public async Task DrawPointAsync_ReturnsRowColumn()
        {
            var task = DrawHelper.DrawPointAsync(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(30, 40));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(30, 40));
            Confirm();

            var r = await task;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(40, r.Row);
            Assert.AreEqual(30, r.Column);
        }

        [TestMethod]
        public async Task DrawLineAsync_ReturnsEndPointsAsRowColumn()
        {
            var task = DrawHelper.DrawLineAsync(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(10, 20));
            DrawHelper.ForwardMouseMove(_window, Mouse.Move(50, 60));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(100, 120));
            Confirm();

            var r = await task;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(20, r.Row1); Assert.AreEqual(10, r.Column1);
            Assert.AreEqual(120, r.Row2); Assert.AreEqual(100, r.Column2);
        }

        [TestMethod]
        public async Task DrawRectangle1Async_NormalizesDragFromBottomRight()
        {
            var task = DrawHelper.DrawRectangle1Async(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(200, 150));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(50, 30));
            Confirm();

            var r = await task;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(30, r.Row1); Assert.AreEqual(50, r.Column1);
            Assert.AreEqual(150, r.Row2); Assert.AreEqual(200, r.Column2);
        }

        [TestMethod]
        public async Task DrawCircleAsync_RadiusIsDragDistance()
        {
            var task = DrawHelper.DrawCircleAsync(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(100, 100));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(130, 140));
            Confirm();

            var r = await task;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(100, r.Row); Assert.AreEqual(100, r.Column);
            Assert.AreEqual(50, r.Radius, 1e-9);
        }

        [TestMethod]
        public async Task DrawRegionAsync_Confirmed_ReturnsFilledPolygon()
        {
            var task = DrawHelper.DrawRegionAsync(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(20, 20));
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(120, 20));
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(120, 120));
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(20, 120));
            Confirm(); // 闭合
            Assert.IsFalse(task.IsCompleted, "区域第一次右键只是闭合");
            Confirm(); // 确认

            var r = await task;
            using (r.Region)
            {
                Assert.IsTrue(r.Completed);
                Assert.AreEqual(1, CountObj(r.Region));
                Assert.IsTrue(Contains(r.Region, 70, 70), "多边形内部点应在区域内");
                Assert.IsFalse(Contains(r.Region, 200, 200));
                Assert.AreEqual(101 * 101, Area(r.Region), 101 * 4, "面积应接近 100x100 的正方形");
            }
        }

        [TestMethod]
        public async Task DrawRegionAsync_NullWindow_FaultsWithArgumentNull()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => DrawHelper.DrawRegionAsync(null));
        }

        [TestMethod]
        public void DrawRegionAsync_DraggingVertex_DrawsPolygonAtCurrentMousePosition()
        {
            // 原实现先画多边形再更新被拖的顶点，画面总落后鼠标一帧
            using (var window = BufferWindow())
            {
                var task = DrawHelper.DrawRegionAsync(window);
                foreach (var p in new[] { new[] { 20, 20 }, new[] { 120, 20 }, new[] { 120, 120 }, new[] { 20, 120 } })
                    DrawHelper.ForwardMouseDown(window, Mouse.Left(p[0], p[1]));
                DrawHelper.ForwardMouseUp(window, Mouse.Right(0, 0)); // 闭合

                DrawHelper.ForwardMouseMove(window, Mouse.Move(120, 120)); // 悬停到第 3 个顶点
                DrawHelper.ForwardMouseDown(window, Mouse.Left(120, 120));
                DrawHelper.ForwardMouseMove(window, Mouse.Move(200, 200));

                // 新边 (120,20)→(200,200) 的中点 (x=160, y=110)；旧顶点位置的边在 x=120，不会经过这里
                HOperatorSet.DumpWindowImage(out HObject dump, window);
                using (dump)
                using (var probe = Rectangle1(109, 159, 111, 161))
                {
                    HOperatorSet.Rgb1ToGray(dump, out HObject gray);
                    using (gray)
                    using (var empty = Rectangle1(220, 290, 230, 310))
                    {
                        HOperatorSet.MinMaxGray(empty, gray, 0, out HTuple _, out HTuple bg, out HTuple _);
                        Assert.AreEqual(0.0, bg.D, "探测前提：空白处应为黑色背景");
                        HOperatorSet.MinMaxGray(probe, gray, 0, out HTuple _, out HTuple max, out HTuple _);
                        Assert.IsTrue(max.D > 0, "拖拽中的多边形应画在当前鼠标位置");
                    }
                }

                DrawHelper.CancelDraw(window);
                task.Result.Region.Dispose();
            }
        }

        [TestMethod]
        public void DrawRegionAsync_Cancelled_ReturnsEmptyDisposableRegion()
        {
            var task = DrawHelper.DrawRegionAsync(_window);
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(20, 20));
            DrawHelper.CancelDraw(_window);

            var r = task.Result;
            using (r.Region)
            {
                Assert.IsFalse(r.Completed);
                Assert.IsNotNull(r.Region, "失败路径也应返回可释放的占位对象");
                Assert.AreEqual(0, Area(r.Region));
            }
        }

        #endregion

        #region 修改已有 ROI

        [TestMethod]
        public void ModAsync_RegistersSessionInEditingPhase()
        {
            var task = DrawHelper.DrawCircleModAsync(_window, 100, 100, 30);

            Assert.IsTrue(DrawHelper.IsDrawing(_window));
            Assert.IsFalse(task.IsCompleted, "Mod 入口也要等用户确认");

            // 编辑阶段直接右键即确认，几何保持传入值
            Confirm();
            var r = task.Result;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(100, r.Row); Assert.AreEqual(100, r.Column); Assert.AreEqual(30, r.Radius);
        }

        [TestMethod]
        public void DrawCircleModAsync_ClampsRadiusToOne()
        {
            var task = DrawHelper.DrawCircleModAsync(_window, 50, 60, -5);
            Confirm();

            Assert.AreEqual(1, task.Result.Radius);
        }

        [TestMethod]
        public void DrawCircleModAsync_DragCenter_MovesCircle()
        {
            var task = DrawHelper.DrawCircleModAsync(_window, 100, 100, 30);
            DrawHelper.ForwardMouseMove(_window, Mouse.Move(102, 101));
            DrawHelper.ForwardMouseDown(_window, Mouse.Left(102, 101));
            DrawHelper.ForwardMouseMove(_window, Mouse.Move(150, 80));
            DrawHelper.ForwardMouseUp(_window, Mouse.Left(150, 80));
            Confirm();

            var r = task.Result;
            Assert.AreEqual(80, r.Row); Assert.AreEqual(150, r.Column); Assert.AreEqual(30, r.Radius);
        }

        [TestMethod]
        public void DrawRectangle1ModAsync_NormalizesReversedInput()
        {
            var task = DrawHelper.DrawRectangle1ModAsync(_window, 150, 200, 30, 50);
            Confirm();

            var r = task.Result;
            Assert.IsTrue(r.Completed);
            Assert.AreEqual(30, r.Row1); Assert.AreEqual(50, r.Column1);
            Assert.AreEqual(150, r.Row2); Assert.AreEqual(200, r.Column2);
        }

        [TestMethod]
        public void DrawRectangle2ModAsync_ClampsLengths()
        {
            var task = DrawHelper.DrawRectangle2ModAsync(_window, 100, 100, 0.5, 0, -3);
            Confirm();

            var r = task.Result;
            Assert.AreEqual(0.5, r.Phi);
            Assert.AreEqual(1, r.Length1);
            Assert.AreEqual(1, r.Length2);
        }

        [TestMethod]
        public void DrawEllipseModAsync_SwapsAxesWhenR1IsMinor()
        {
            var task = DrawHelper.DrawEllipseModAsync(_window, 100, 120, 0.2, 10, 40);
            Confirm();

            var r = task.Result;
            Assert.AreEqual(100, r.Row); Assert.AreEqual(120, r.Column);
            Assert.AreEqual(40, r.Radius1, "Radius1 应为长半轴");
            Assert.AreEqual(10, r.Radius2);
            Assert.AreEqual(0.2 + Math.PI / 2, r.Phi, 1e-9, "交换长短轴时 phi 旋转 90°");
        }

        [TestMethod]
        public void DrawEllipseModAsync_SwappedPhi_IsWrappedIntoMinusPiToPi()
        {
            // 交换长短轴后 phi + 90° 可能越过 π；绘制得到的 phi 来自 Atan2，输出应落在同一区间 (-π, π]
            var task = DrawHelper.DrawEllipseModAsync(_window, 100, 120, 3.0, 10, 40);
            Confirm();

            var r = task.Result;
            Assert.AreEqual(40, r.Radius1);
            Assert.AreEqual(3.0 + Math.PI / 2 - 2 * Math.PI, r.Phi, 1e-9);
            Assert.IsTrue(r.Phi > -Math.PI && r.Phi <= Math.PI);
        }

        [TestMethod]
        public void DrawEllipseModAsync_OutOfRangePhiIn_IsWrapped()
        {
            // 不交换长短轴、也不拖动主轴时 phi 直接取自调用方的 phiIn，同样要折回 (-π, π]
            var task = DrawHelper.DrawEllipseModAsync(_window, 100, 120, 4.0, 40, 10);
            Confirm();

            Assert.AreEqual(4.0 - 2 * Math.PI, task.Result.Phi, 1e-9);
        }

        [TestMethod]
        public void DrawEllipseModAsync_KeepsPhiWhenR1IsMajor()
        {
            var task = DrawHelper.DrawEllipseModAsync(_window, 100, 120, 0.2, 40, 10);
            Confirm();

            var r = task.Result;
            Assert.AreEqual(40, r.Radius1);
            Assert.AreEqual(10, r.Radius2);
            Assert.AreEqual(0.2, r.Phi, 1e-9);
        }

        [TestMethod]
        public void DrawLineModAsync_And_DrawPointModAsync_ReturnInputWhenConfirmedUntouched()
        {
            var line = DrawHelper.DrawLineModAsync(_window, 10, 20, 30, 40);
            Confirm();
            var l = line.Result;
            Assert.IsTrue(l.Completed);
            Assert.AreEqual(10, l.Row1); Assert.AreEqual(20, l.Column1);
            Assert.AreEqual(30, l.Row2); Assert.AreEqual(40, l.Column2);

            var point = DrawHelper.DrawPointModAsync(_window, 5, 6);
            Confirm();
            var p = point.Result;
            Assert.IsTrue(p.Completed);
            Assert.AreEqual(5, p.Row); Assert.AreEqual(6, p.Column);
        }

        [TestMethod]
        public void ModAsync_Cancelled_ReturnsInitialGeometryWithCompletedFalse()
        {
            var task = DrawHelper.DrawCircleModAsync(_window, 70, 80, 20);
            DrawHelper.CancelDraw();

            var r = task.Result;
            Assert.IsFalse(r.Completed);
            Assert.AreEqual(70, r.Row); Assert.AreEqual(80, r.Column); Assert.AreEqual(20, r.Radius);
        }

        #endregion
    }

    /// <summary><see cref="NoneMouse"/>：唯一职责是把鼠标事件转发给当前绘图会话。</summary>
    [TestClass]
    public class NoneMouseTests : HalconTestBase
    {
        [TestCleanup]
        public void Cleanup() => DrawHelper.CancelDraw();

        [TestMethod]
        public void Ctor_NullWindow_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new NoneMouse(null));
        }

        [TestMethod]
        public void Events_AreForwardedToActiveSession()
        {
            using (var window = BufferWindow())
            {
                var mouse = new NoneMouse(window);
                var task = DrawHelper.DrawLineAsync(window);

                mouse.OnMouseDown(Mouse.Left(10, 10));
                mouse.OnMouseMove(Mouse.Move(40, 40));
                mouse.OnMouseUp(Mouse.Left(60, 80));
                mouse.OnMouseWheel(Mouse.Move(60, 80));
                mouse.OnMouseUp(Mouse.Right(60, 80));

                var r = task.Result;
                Assert.IsTrue(r.Completed);
                Assert.AreEqual(80, r.Row2);
                Assert.AreEqual(60, r.Column2);
            }
        }

        [TestMethod]
        public void Events_WithoutSession_AreIgnored()
        {
            using (var window = BufferWindow())
            {
                var mouse = new NoneMouse(window);
                mouse.OnMouseDown(Mouse.Left(10, 10));
                mouse.OnMouseUp(Mouse.Right(10, 10));
                mouse.OnMouseMove(Mouse.Move(10, 10));
                mouse.OnMouseWheel(Mouse.Move(10, 10));
            }
        }
    }
}
