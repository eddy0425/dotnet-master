using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HDisplayUI"/>：按 <see cref="HDisplayUI.DrawType"/> 分派鼠标事件、绘制入口复位交互模式、释放时终止绘制会话。
    /// </summary>
    /// <remarks>
    /// 鼠标事件处理方法是 private 的（由 HWindowControl 事件驱动），这里用反射直接调用，
    /// 绕开窗口消息，结果不依赖真实鼠标位置。
    /// </remarks>
    [TestClass]
    public class HDisplayUITests : HalconTestBase
    {
        [TestCleanup]
        public void Cleanup() => DrawHelper.CancelDraw();

        private static void Run(Action<HDisplayUI> body) =>
            Sta.Run(() =>
            {
                var ui = new HDisplayUI();
                using (var form = WindowHost.ShowOffscreen(ui))
                {
                    body(ui);
                }
            });

        private static void Raise(HDisplayUI ui, string handler, HMouseEventArgs e) =>
            typeof(HDisplayUI).GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(object), typeof(HMouseEventArgs) }, null)
                .Invoke(ui, new object[] { ui.hWindowControl, e });

        /// <summary>直接在 Display 上开一个绘制会话，不经 HDisplayUI 的入口（入口会把 DrawType 复位成 None）。</summary>
        private static Task<bool> StartDraw(HDisplayUI ui, out CvRegion region)
        {
            region = new CvRegion { Type = RectEnum.Rectangle };
            return ((HDisplay)((UiThreadDisplay)ui.Display).Inner).DrawRegionAsync(region);
        }

        [TestMethod]
        public void NoneMode_ForwardsMouseToDrawSession()
        {
            Run(ui =>
            {
                ui.DrawType = DrawEnum.None;
                var task = StartDraw(ui, out var region);

                Raise(ui, "OnMouseDown", Mouse.Left(50, 30));
                Raise(ui, "OnMouseUp", Mouse.Left(200, 150));
                Raise(ui, "OnMouseUp", Mouse.Right(0, 0));
                WindowHost.Pump(task);

                Assert.IsTrue(task.Result, "None 模式下鼠标事件应转发给本窗口的绘制会话");
                Assert.AreEqual(30, region.Top); Assert.AreEqual(50, region.Left);
                Assert.AreEqual(150, region.Bottom); Assert.AreEqual(200, region.Right);
            });
        }

        [DataTestMethod]
        [DataRow(DrawEnum.DispRect)]
        [DataRow(DrawEnum.DispModel)]
        [DataRow(DrawEnum.Erase)]
        public void OtherModes_DoNotForwardToDrawSession(DrawEnum mode)
        {
            Run(ui =>
            {
                using (var log = new CapturingLogger())
                {
                    var task = StartDraw(ui, out _);
                    ui.DrawType = mode;

                    Raise(ui, "OnMouseDown", Mouse.Left(50, 30));
                    Raise(ui, "OnMouseUp", Mouse.Left(200, 150));
                    Raise(ui, "OnMouseUp", Mouse.Right(0, 0));
                    Raise(ui, "OnMouseMove", Mouse.Move(5, 5));
                    Raise(ui, "OnMouseWheel", Mouse.Wheel(5, 5, 120));
                    WindowHost.Pump();

                    Assert.IsFalse(task.IsCompleted, $"{mode} 模式下鼠标事件不应落到绘制会话上");
                    Assert.AreEqual(0, log.Entries.FindAll(e => e.Level >= LogLevel.Error).Count);
                }
            });
        }

        [DataTestMethod]
        [DataRow(DrawEnum.DispRect)]
        [DataRow(DrawEnum.DispModel)]
        public void DisplayModes_WithoutSetUp_AreSilentNoOps(DrawEnum mode)
        {
            // DrawType 是公开可写的，直接切模式而不走 SetRectPara / SetModelPara 时处理器尚未 SetUp；
            // 原先 DispModelMouse 在鼠标移动回调里直接 NullReferenceException 冒到 HWindowControl 事件上。
            Run(ui =>
            {
                using (var log = new CapturingLogger())
                {
                    ui.DrawType = mode;
                    Raise(ui, "OnMouseMove", Mouse.Move(5, 5));
                    Assert.AreEqual(0, log.Entries.Count);
                }
            });
        }

        [TestMethod]
        public void DispModelMode_DrawsHandlesPassedToSetUp()
        {
            Run(ui =>
            {
                using (var log = new CapturingLogger())
                using (var findMode = new HRegion(10.0, 10, 50, 50))
                using (var contour = new HXLDCont(new HTuple(10.0, 50.0), new HTuple(10.0, 50.0)))
                {
                    ui.SetModelPara(findMode, contour, new CvCoord(30, 30));
                    Raise(ui, "OnMouseMove", Mouse.Move(5, 5));
                    Assert.AreEqual(0, log.Entries.FindAll(e => e.Level >= LogLevel.Warn).Count);
                }
            });
        }

        /// <summary> 模式切换有校验：未知模式在设置时就被拒绝，而不是等到每个鼠标事件里才告警 </summary>
        [TestMethod]
        public void UnknownMode_RejectedOnSet()
        {
            Run(ui =>
            {
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => ui.DrawType = (DrawEnum)99);
                Assert.AreEqual(DrawEnum.None, ui.DrawType);
            });
        }

        /// <summary>
        /// 视图导航先于模式处理器执行：中键拖动平移视图后，同一次抬起事件里模式处理器拿到的已是新视图。
        /// 顺序由 HDisplayUI 显式决定，不再依赖两个订阅方谁先订阅。
        /// </summary>
        [TestMethod]
        public void Dispatch_NavigationIsForwardedNotSelfSubscribed()
        {
            Run(ui =>
            {
                const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var mouse = typeof(HDisplayUI).GetField("mouse", Private).GetValue(ui);
                bool subscribed = (bool)typeof(HWindowMouse).GetField("_subscribed", Private).GetValue(mouse);

                Assert.IsFalse(subscribed, "视图导航由 HDisplayUI.Dispatch 按固定顺序转发, 不再自己订阅控件事件");
            });
        }

        [TestMethod]
        public void DrawEntries_ResetModeToNone()
        {
            Run(ui =>
            {
                ui.DrawType = DrawEnum.DispRect;
                var t1 = ui.DrawRegionAsync(new CvRegion { Type = RectEnum.Rectangle });
                Assert.AreEqual(DrawEnum.None, ui.DrawType);
                DrawHelper.CancelDraw(ui.HoWindow);
                WindowHost.Pump(t1);

                ui.DrawType = DrawEnum.Erase;
                var t2 = ui.DrawRegionModAsync(new CvRegion { Type = RectEnum.Rectangle });
                Assert.AreEqual(DrawEnum.None, ui.DrawType);
                DrawHelper.CancelDraw(ui.HoWindow);
                WindowHost.Pump(t2);

                ui.DrawType = DrawEnum.DispModel;
                var t3 = ui.DrawRegionAsync(RectEnum.Circle);
                Assert.AreEqual(DrawEnum.None, ui.DrawType);
                DrawHelper.CancelDraw(ui.HoWindow);
                WindowHost.Pump(t3);
                using (var result = t3.Result)
                    Assert.AreEqual(0, result.CountObj());
            });
        }

        [TestMethod]
        public void SetParaMethods_SwitchMode()
        {
            Run(ui =>
            {
                ui.SetRectPara(new CvRegion { Type = RectEnum.Rectangle });
                Assert.AreEqual(DrawEnum.DispRect, ui.DrawType);

                ui.SetNonePara();
                Assert.AreEqual(DrawEnum.None, ui.DrawType);
            });
        }

        [TestMethod]
        public void DispImage_RaisesOnShow_AfterDisplaying()
        {
            Run(ui =>
            {
                int shown = 0;
                ui.OnShow += () =>
                {
                    shown++;
                    Assert.AreEqual(64, ui.Display.HoWidth, "OnShow 触发时新图像应已显示");
                };

                using (var image = new HImage("byte", 64, 48))
                {
                    ui.DispImage(image);
                    ui.DispImage(image, false);
                }

                Assert.AreEqual(2, shown);
            });
        }

        [TestMethod]
        public void Dispose_CancelsPendingDrawOnOwnWindowOnly()
        {
            Sta.Run(() =>
            {
                var other = new HDisplayUI();
                using (var otherForm = WindowHost.ShowOffscreen(other))
                {
                    var ui = new HDisplayUI();
                    var form = WindowHost.ShowOffscreen(ui);

                    var mine = StartDraw(ui, out var region);
                    var theirs = StartDraw(other, out _);

                    form.Dispose();
                    WindowHost.Pump(mine);

                    Assert.IsTrue(ui.IsReleasing);
                    Assert.IsFalse(mine.Result, "释放时应取消自己窗口上的绘制会话");
                    Assert.AreEqual(0, region.Width, "取消不回写几何");
                    Assert.IsFalse(theirs.IsCompleted, "不应误伤其他 HDisplayUI 上的绘制");

                    var image = ui.Display.HoImage;
                    Assert.IsTrue(image == null || !image.IsInitialized(), "显示资源应已释放");
                    ui.ReDispImage(); // 释放后调用不抛
                }
            });
        }
    }
}
