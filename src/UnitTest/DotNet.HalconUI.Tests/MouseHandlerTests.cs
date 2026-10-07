using System;
using System.Linq;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="EraseRectMouse"/>：左键按住涂抹，擦除区域并入 _erase 并从模板区域中扣除。
    /// </summary>
    [TestClass]
    public class EraseRectMouseTests : HalconTestBase
    {
        private const int Brush = 5;

        private FakeDisplay _display;
        private EraseRectMouse _mouse;

        [TestInitialize]
        public void Setup()
        {
            _display = new FakeDisplay();
            _mouse = new EraseRectMouse();
            _mouse.SetUp(_display, EmptyObj(), Rectangle1(0, 0, 99, 99), HColor.Red, Brush);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Setup 在 HALCON 不可用时会先被 Inconclusive 打断，_mouse 可能根本没建出来
            _mouse?.Erase?.Dispose();
            _mouse?.FindMode?.Dispose();
        }

        private HObject Erase => _mouse.Erase;
        private HObject FindMode => _mouse.FindMode;

        [TestMethod]
        public void LeftDown_EraseCircleAtRowColumn()
        {
            // e.X = 列, e.Y = 行
            _mouse.OnMouseDown(Mouse.Left(30, 60));

            Assert.AreEqual(1, CountObj(Erase));
            Assert.IsTrue(Contains(Erase, 60, 30));
            Assert.IsFalse(Contains(Erase, 30, 60), "行列不应颠倒");
            Assert.IsFalse(Contains(FindMode, 60, 30), "擦除区域应从模板区域中扣除");
            Assert.IsTrue(Contains(FindMode, 10, 10));
        }

        [TestMethod]
        public void LeftDown_DisplaysEraseRegion_FilledThenMargin()
        {
            _mouse.OnMouseDown(Mouse.Left(30, 60));

            Assert.AreEqual(1, _display.Objects.Count);
            Assert.AreEqual("red", _display.Objects[0].ColorName);
            Assert.AreEqual("fill", _display.Objects[0].Style.DrawMode);
            CollectionAssert.AreEqual(new[] { "fill", "margin", "margin" }, _display.DrawModes, "填充绘制后应恢复为 margin");
        }

        [TestMethod]
        public void RightDown_DoesNothing()
        {
            _mouse.OnMouseDown(Mouse.Right(30, 60));
            _mouse.OnMouseMove(Mouse.Move(40, 60));

            Assert.AreEqual(0, CountObj(Erase));
            Assert.AreEqual(0, _display.Objects.Count);
        }

        [TestMethod]
        public void Move_ErasesOnlyWhileLeftHeld()
        {
            _mouse.OnMouseMove(Mouse.Move(80, 80));
            Assert.AreEqual(0, CountObj(Erase), "未按下时移动不擦除");

            _mouse.OnMouseDown(Mouse.Left(20, 20));
            _mouse.OnMouseMove(Mouse.Move(50, 20));
            Assert.IsTrue(Contains(Erase, 20, 20));
            Assert.IsTrue(Contains(Erase, 20, 50), "拖动轨迹应并入擦除区域");
            Assert.AreEqual(1, CountObj(Erase), "并集后仍是单个对象");

            _mouse.OnMouseUp(Mouse.Left(50, 20));
            _mouse.OnMouseMove(Mouse.Move(80, 80));
            Assert.IsFalse(Contains(Erase, 80, 80), "左键释放后不再擦除");
        }

        [TestMethod]
        public void RightUp_DoesNotStopEditing()
        {
            _mouse.OnMouseDown(Mouse.Left(20, 20));
            _mouse.OnMouseUp(Mouse.Right(20, 20));
            _mouse.OnMouseMove(Mouse.Move(70, 70));

            Assert.IsTrue(Contains(Erase, 70, 70));
        }

        [TestMethod]
        public void BrushRadius_FollowsLineWidth_AndSetParaUpdatesIt()
        {
            _mouse.OnMouseDown(Mouse.Left(50, 50));
            double small = Area(Erase);
            _mouse.OnMouseUp(Mouse.Left(50, 50));

            _mouse.SetPara(HColor.Blue, 20);
            _mouse.OnMouseDown(Mouse.Left(50, 50));

            Assert.IsTrue(Area(Erase) > small * 4, "半径 5 → 20，面积应大幅增加");
            Assert.AreEqual("blue", _display.Objects.Last().ColorName);
        }

        [TestMethod]
        public void EmptyFindMode_IsLeftUntouched()
        {
            Erase.Dispose();
            FindMode.Dispose();
            _mouse.SetUp(_display, EmptyObj(), EmptyObj(), HColor.Red, Brush);

            _mouse.OnMouseDown(Mouse.Left(30, 30));

            Assert.AreEqual(0, CountObj(FindMode));
            Assert.AreEqual(1, CountObj(Erase));
        }

        [TestMethod]
        public void Wheel_RedisplaysEraseRegion()
        {
            _mouse.OnMouseWheel(Mouse.Move(0, 0));
            Assert.AreEqual(1, _display.Objects.Count, "空对象也会显示一次（由显示层决定是否画）");

            _mouse.OnMouseDown(Mouse.Left(30, 30));
            _mouse.OnMouseWheel(Mouse.Move(0, 0));
            Assert.AreEqual(3, _display.Objects.Count);
        }

        [TestMethod]
        public void Stroke_TakesOwnershipOfSetUpHandles_AndPropertiesExposeReplacements()
        {
            HObject originalErase = Erase, originalFindMode = FindMode;

            _mouse.OnMouseDown(Mouse.Left(30, 30));

            Assert.IsFalse(originalErase.IsInitialized(), "旧擦除句柄应在涂抹后释放");
            Assert.IsFalse(originalFindMode.IsInitialized(), "旧模板句柄应在涂抹后释放");
            Assert.IsTrue(Erase.IsInitialized());
            Assert.IsTrue(FindMode.IsInitialized());
            Assert.AreNotSame(originalErase, Erase);
        }

        [TestMethod]
        public void ReAddedArea_IsNotSubtractedAgain_ByLaterStrokes()
        {
            _mouse.OnMouseDown(Mouse.Left(30, 30));
            _mouse.OnMouseUp(Mouse.Left(30, 30));

            // 模拟 HEditModelUI：用户把整块模板重新添加回来，累计的擦除区域照旧传回
            FindMode.Dispose();
            _mouse.SetUp(_display, Erase, Rectangle1(0, 0, 99, 99), HColor.Red, Brush);

            _mouse.OnMouseDown(Mouse.Left(80, 80));

            Assert.IsTrue(Contains(Erase, 30, 30), "擦除区域仍是累计的");
            Assert.IsTrue(Contains(FindMode, 30, 30), "重新添加回来的部分不应被历史擦除再扣一次");
            Assert.IsFalse(Contains(FindMode, 80, 80), "本次笔刷照常扣除");
        }

        [TestMethod]
        public void WithoutSetUp_MouseEvents_AreSilentNoOps()
        {
            // DrawType 是公开可写的：外部可能不经 but_ApplyRegion_Click 直接切到 Erase
            var mouse = new EraseRectMouse();
            using (var log = new CapturingLogger())
            {
                mouse.OnMouseDown(Mouse.Left(10, 10));
                mouse.OnMouseMove(Mouse.Move(20, 20));
                mouse.OnMouseWheel(Mouse.Move(20, 20));
                mouse.OnMouseUp(Mouse.Left(20, 20));

                Assert.AreEqual(0, log.Entries.Count, "未 SetUp 时不应每次移动都告警");
            }
            Assert.IsNull(mouse.Erase);
        }

        [TestMethod]
        public void DisplayFailure_IsLoggedNotThrown_AndStrokeIsKept()
        {
            _display.ThrowOnDispObject = new InvalidOperationException("窗口已销毁");
            using (var log = new CapturingLogger())
            {
                _mouse.OnMouseDown(Mouse.Left(30, 60));
                _mouse.OnMouseMove(Mouse.Move(31, 61));
                _mouse.OnMouseMove(Mouse.Move(32, 62));

                var warn = log.Entries.Single(e => e.Level == LogLevel.Warn); // 失败即结束本次涂抹，拖动不再重复告警
                Assert.AreEqual(nameof(EraseRectMouse), warn.Category);
                Assert.IsInstanceOfType(warn.Exception, typeof(InvalidOperationException));
            }
            Assert.IsTrue(Contains(Erase, 60, 30), "显示失败不影响已完成的擦除");
            Assert.AreEqual("margin", _display.DrawModes.Last(), "失败后画笔仍应恢复为 margin");
        }

        [TestMethod]
        public void NullHandles_AreLoggedNotThrown()
        {
            using (var log = new CapturingLogger())
            {
                var mouse = new EraseRectMouse();
                mouse.SetUp(_display, null, null, HColor.Red, Brush);

                mouse.OnMouseDown(Mouse.Left(30, 60));
                mouse.OnMouseWheel(Mouse.Move(0, 0));

                Assert.IsTrue(log.Messages(LogLevel.Warn).Any(), "调用方错误应留下日志");
            }
            CollectionAssert.AreEqual(new[] { "fill", "margin" }, _display.DrawModes.Take(2).ToArray());
        }
    }

    /// <summary><see cref="DispRectMouse"/> 与 <see cref="DispModelMouse"/>：只在移动时重画叠加层。</summary>
    [TestClass]
    public class DispMouseTests : HalconTestBase
    {
        [TestMethod]
        public void DispRect_Move_DrawsCenterAndRegion()
        {
            var display = new FakeDisplay();
            using (var region = new CvRegion { Bounds = new Rect2d(10d, 20d, 100d, 40d) })
            {
                var mouse = new DispRectMouse();
                mouse.SetUp(display, region);

                mouse.OnMouseDown(Mouse.Left(0, 0));
                mouse.OnMouseUp(Mouse.Left(0, 0));
                mouse.OnMouseWheel(Mouse.Move(0, 0));
                Assert.AreEqual(0, display.Points.Count + display.Regions.Count, "只有移动才重画");

                mouse.OnMouseMove(Mouse.Move(5, 5));

                Assert.AreEqual(1, display.Points.Count);
                Assert.AreEqual(60, display.Points[0].Item.X);
                Assert.AreEqual(40, display.Points[0].Item.Y);
                Assert.AreEqual("orange", display.Points[0].ColorName);
                Assert.AreEqual(50, display.Points[0].Style.Size);

                Assert.AreEqual(1, display.Regions.Count);
                Assert.AreSame(region, display.Regions[0].Item);
                Assert.AreEqual("blue", display.Regions[0].ColorName);
            }
        }

        [TestMethod]
        public void DispRect_DisplayFailure_IsLoggedNotThrown()
        {
            var display = new FakeDisplay { ThrowOnDispPoint = new InvalidOperationException("窗口已销毁") };
            using (var log = new CapturingLogger())
            using (var region = new CvRegion())
            {
                var mouse = new DispRectMouse();
                mouse.SetUp(display, region);

                mouse.OnMouseMove(Mouse.Move(5, 5));

                var warn = log.Entries.Single(e => e.Level == LogLevel.Warn);
                Assert.AreEqual(nameof(DispRectMouse), warn.Category);
                Assert.AreEqual("显示区域失败.", warn.Message);
                Assert.IsInstanceOfType(warn.Exception, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        public void DispRect_NullRegion_IsLoggedNotThrown()
        {
            using (var log = new CapturingLogger())
            {
                var mouse = new DispRectMouse();
                mouse.SetUp(new FakeDisplay(), null);

                mouse.OnMouseMove(Mouse.Move(5, 5));

                Assert.AreEqual(1, log.Messages(LogLevel.Warn).Count());
            }
        }

        [TestMethod]
        public void DispModel_Move_DrawsFindModeContourAndCoord()
        {
            var display = new FakeDisplay();
            using (var findMode = Rectangle1(0, 0, 10, 10))
            using (var contour = EmptyObj())
            {
                var coord = new CvCoord(3, 4);
                var mouse = new DispModelMouse();
                mouse.SetUp(display, findMode, contour, coord);

                mouse.OnMouseDown(Mouse.Left(0, 0));
                mouse.OnMouseUp(Mouse.Left(0, 0));
                mouse.OnMouseWheel(Mouse.Move(0, 0));
                Assert.AreEqual(0, display.Objects.Count);

                mouse.OnMouseMove(Mouse.Move(1, 1));

                Assert.AreEqual(2, display.Objects.Count);
                Assert.AreSame(findMode, display.Objects[0].Item);
                Assert.AreEqual("blue", display.Objects[0].ColorName);
                Assert.AreSame(contour, display.Objects[1].Item);
                Assert.AreEqual("green", display.Objects[1].ColorName);

                Assert.AreEqual(1, display.Coords.Count);
                Assert.AreEqual(coord, display.Coords[0].Item);
                Assert.AreEqual(HColor.OrangeRed.Name, display.Coords[0].ColorName);
            }
        }

        [TestMethod]
        public void DispRect_WithoutSetUp_IsSilentNoOp()
        {
            using (var log = new CapturingLogger())
            {
                new DispRectMouse().OnMouseMove(Mouse.Move(5, 5));
                Assert.AreEqual(0, log.Entries.Count);
            }
        }

        [TestMethod]
        public void DispModel_WithoutSetUp_IsSilentNoOp()
        {
            using (var log = new CapturingLogger())
            {
                var mouse = new DispModelMouse();
                mouse.OnMouseDown(Mouse.Left(0, 0));
                mouse.OnMouseMove(Mouse.Move(5, 5));
                Assert.AreEqual(0, log.Entries.Count);
            }
        }

        [TestMethod]
        public void DispModel_DisplayFailure_IsLoggedNotThrown()
        {
            var display = new FakeDisplay { ThrowOnDispObject = new InvalidOperationException("窗口已销毁") };
            using (var log = new CapturingLogger())
            using (var findMode = Rectangle1(0, 0, 10, 10))
            using (var contour = EmptyObj())
            {
                var mouse = new DispModelMouse();
                mouse.SetUp(display, findMode, contour, new CvCoord(3, 4));

                mouse.OnMouseMove(Mouse.Move(1, 1));

                var warn = log.Entries.Single(e => e.Level == LogLevel.Warn);
                Assert.AreEqual(nameof(DispModelMouse), warn.Category);
                Assert.IsInstanceOfType(warn.Exception, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        public void DispModel_UpdateFindMode_ReplacesOnlyTemplateHandle()
        {
            var display = new FakeDisplay();
            using (var first = Rectangle1(0, 0, 10, 10))
            using (var second = Rectangle1(0, 0, 20, 20))
            using (var contour = EmptyObj())
            {
                var mouse = new DispModelMouse();
                mouse.SetUp(display, first, contour, new CvCoord(3, 4));
                mouse.UpdateFindMode(second);

                mouse.OnMouseMove(Mouse.Move(1, 1));

                Assert.AreSame(second, display.Objects[0].Item);
                Assert.AreSame(contour, display.Objects[1].Item);
            }
        }
    }
}
