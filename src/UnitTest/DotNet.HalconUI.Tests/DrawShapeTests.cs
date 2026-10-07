using System;
using DotNet.HalconUI.Draw;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 各 <see cref="DrawShape"/> 子类的状态机：Idle → Drawing → Editing → 右键确认。
    /// </summary>
    /// <remarks>
    /// OnDown / OnUp 只改几何与阶段，不访问 <see cref="DrawRenderer"/>，因此不需要窗口。
    /// 控制点命中 (Hover) 由 Render 计算，这里直接赋值模拟「鼠标已悬停在某控制点上」。
    /// </remarks>
    [TestClass]
    public class DrawShapeTests
    {
        private const double Eps = 1e-9;

        #region 公共行为

        [TestMethod]
        public void NewShape_StartsIdle_NotCompleted()
        {
            foreach (var shape in AllShapes())
            {
                Assert.AreEqual(DrawPhase.Idle, shape.Phase, shape.GetType().Name);
                Assert.AreEqual(DrawHandle.None, shape.Hover, shape.GetType().Name);
                Assert.IsFalse(shape.Dragging, shape.GetType().Name);
                Assert.IsFalse(shape.Completed, shape.GetType().Name);
            }
        }

        [TestMethod]
        public void NonLeftDown_IsIgnored_InIdle()
        {
            foreach (var shape in AllShapes())
            {
                shape.OnDown(Mouse.Right(10, 10));
                shape.OnDown(Mouse.Middle(10, 10));
                Assert.AreEqual(DrawPhase.Idle, shape.Phase, shape.GetType().Name + " 只响应左键");
            }
        }

        [TestMethod]
        public void BeginEdit_JumpsToEditing()
        {
            foreach (var shape in AllShapes())
            {
                shape.BeginEdit();
                Assert.AreEqual(DrawPhase.Editing, shape.Phase, shape.GetType().Name);
            }
        }

        [TestMethod]
        public void Render_WithoutRenderer_ThrowsInvalidOperation()
        {
            var shape = new PointShape();
            Assert.ThrowsException<InvalidOperationException>(() => shape.Render(Mouse.Move(0, 0)));
        }

        [TestMethod]
        public void Editing_RightUp_Confirms_ExceptRegionWhichNeedsTwoClicks()
        {
            foreach (var shape in new DrawShape[] { new PointShape(), new LineShape(), new Rect1Shape(), new Rect2Shape(), new CircleShape(), new EllipseShape() })
            {
                shape.BeginEdit();
                shape.OnUp(Mouse.Right(0, 0));
                Assert.IsTrue(shape.Completed, shape.GetType().Name + " 编辑阶段右键应确认");
            }
        }

        [TestMethod]
        public void Editing_DownOnHandle_StartsDrag_LeftUpEndsDrag()
        {
            foreach (var shape in new DrawShape[] { new PointShape(), new LineShape(), new Rect1Shape(), new Rect2Shape(), new CircleShape(), new EllipseShape() })
            {
                string name = shape.GetType().Name;
                shape.BeginEdit();
                shape.Hover = DrawHandle.Center;

                shape.OnDown(Mouse.Left(0, 0));
                Assert.IsTrue(shape.Dragging, name + " 按在控制点上应开始拖拽");

                shape.OnUp(Mouse.Left(0, 0));
                Assert.IsFalse(shape.Dragging, name + " 左键释放应结束拖拽");
                Assert.AreEqual(DrawHandle.None, shape.Hover, name + " 结束拖拽应清除悬停");
                Assert.IsFalse(shape.Completed, name + " 左键释放不应确认");
            }
        }

        [TestMethod]
        public void Editing_DownAwayFromHandle_DoesNotDrag()
        {
            foreach (var shape in new DrawShape[] { new LineShape(), new Rect1Shape(), new Rect2Shape(), new CircleShape(), new EllipseShape() })
            {
                shape.BeginEdit();
                shape.OnDown(Mouse.Left(0, 0));
                Assert.IsFalse(shape.Dragging, shape.GetType().Name);
            }
        }

        [TestMethod]
        public void Idle_UpEvents_DoNothing()
        {
            foreach (var shape in AllShapes())
            {
                shape.OnUp(Mouse.Left(10, 10));
                shape.OnUp(Mouse.Right(10, 10));
                Assert.AreEqual(DrawPhase.Idle, shape.Phase, shape.GetType().Name);
                Assert.IsFalse(shape.Completed, shape.GetType().Name + " 空闲阶段右键不应确认");
            }
        }

        #endregion

        #region Point

        [TestMethod]
        public void Point_LeftDown_PlacesAndEntersEditingDirectly()
        {
            var s = new PointShape();
            s.OnDown(Mouse.Left(12, 34));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(12, s.X);
            Assert.AreEqual(34, s.Y);
        }

        [TestMethod]
        public void Point_Editing_OnlyCenterHandleStartsDrag()
        {
            var s = new PointShape();
            s.BeginEdit();
            s.Hover = DrawHandle.P1;
            s.OnDown(Mouse.Left(0, 0));

            Assert.IsFalse(s.Dragging, "点只有中心一个控制点");
        }

        #endregion

        #region Line

        [TestMethod]
        public void Line_DragAndRelease_SetsBothEnds()
        {
            var s = new LineShape();
            s.OnDown(Mouse.Left(10, 20));
            Assert.AreEqual(DrawPhase.Drawing, s.Phase);

            s.OnUp(Mouse.Left(110, 70));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(10, s.X1); Assert.AreEqual(20, s.Y1);
            Assert.AreEqual(110, s.X2); Assert.AreEqual(70, s.Y2);
        }

        [TestMethod]
        public void Line_ReleaseWithinTwoPixels_StaysDrawing_SecondClickFinishes()
        {
            var s = new LineShape();
            s.OnDown(Mouse.Left(10, 20));
            s.OnUp(Mouse.Left(11, 21));
            Assert.AreEqual(DrawPhase.Drawing, s.Phase, "位移 ≤ 2 视为单击，继续等第二个端点");

            s.OnDown(Mouse.Left(50, 60));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(50, s.X2); Assert.AreEqual(60, s.Y2);
        }

        [TestMethod]
        public void Line_Drawing_RightUp_DoesNotConfirm()
        {
            var s = new LineShape();
            s.OnDown(Mouse.Left(10, 20));
            s.OnUp(Mouse.Right(80, 20));

            Assert.IsFalse(s.Completed);
            Assert.AreEqual(DrawPhase.Drawing, s.Phase);
        }

        #endregion

        #region Rect1

        [TestMethod]
        public void Rect1_DragAndRelease_SetsCornersAndCenter()
        {
            var s = new Rect1Shape();
            s.OnDown(Mouse.Left(10, 20));
            s.OnUp(Mouse.Left(50, 80));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(50, s.X2); Assert.AreEqual(80, s.Y2);
            Assert.AreEqual(30, s.CX); Assert.AreEqual(50, s.CY);
        }

        [TestMethod]
        public void Rect1_SecondClick_AlsoSyncsCenter()
        {
            var s = new Rect1Shape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(1, 1));
            s.OnDown(Mouse.Left(40, 20));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(20, s.CX); Assert.AreEqual(10, s.CY);
        }

        [TestMethod]
        public void Rect1_SyncCenter_IsMidpointOfCorners()
        {
            var s = new Rect1Shape { X1 = 100, Y1 = 10, X2 = 20, Y2 = 50 };
            s.SyncCenter();

            Assert.AreEqual(60, s.CX);
            Assert.AreEqual(30, s.CY);
        }

        #endregion

        #region Rect2

        [TestMethod]
        public void Rect2_DragAndRelease_SetsAxisFromDragVector()
        {
            var s = new Rect2Shape();
            s.OnDown(Mouse.Left(100, 100));
            // 向上拖 50：phi = +90°
            s.OnUp(Mouse.Left(100, 50));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(100, s.CX); Assert.AreEqual(100, s.CY);
            Assert.AreEqual(50, s.HalfLen1, Eps);
            Assert.AreEqual(Math.PI / 2, s.Phi, Eps);
            Assert.AreEqual(10, s.HalfLen2, Eps, "短轴默认取主轴的 1/5，与拖拽预览一致");
        }

        [TestMethod]
        public void Rect2_ShortDrag_HalfLen2ClampedToOne()
        {
            var s = new Rect2Shape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(3, 0));

            Assert.AreEqual(3, s.HalfLen1, Eps);
            Assert.AreEqual(1, s.HalfLen2, Eps, "3/5 < 1 时应钳到 1");
        }

        [TestMethod]
        public void Rect2_SecondClickOnCenter_FallsBackToUnitSizeAndZeroPhi()
        {
            var s = new Rect2Shape();
            s.OnDown(Mouse.Left(50, 50));
            s.OnUp(Mouse.Left(50, 50));
            Assert.AreEqual(DrawPhase.Drawing, s.Phase);

            s.OnDown(Mouse.Left(50.5, 50));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(1, s.HalfLen1, Eps);
            Assert.AreEqual(1, s.HalfLen2, Eps);
            Assert.AreEqual(0, s.Phi, Eps, "长度 ≤ 1 时方向无意义，应置 0");
        }

        [TestMethod]
        public void Rect2_SecondClickFarAway_UsesItAsAxisEnd()
        {
            var s = new Rect2Shape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(0, 0));
            s.OnDown(Mouse.Left(-40, 0));

            Assert.AreEqual(40, s.HalfLen1, Eps);
            Assert.AreEqual(Math.PI, Math.Abs(s.Phi), Eps);
            Assert.AreEqual(8, s.HalfLen2, Eps);
        }

        #endregion

        #region Circle

        [TestMethod]
        public void Circle_DragAndRelease_SetsRadius()
        {
            var s = new CircleShape();
            s.OnDown(Mouse.Left(100, 100));
            Assert.AreEqual(0, s.Radius);

            s.OnUp(Mouse.Left(130, 140));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(50, s.Radius, Eps);
        }

        [TestMethod]
        public void Circle_SecondClickOnCenter_RadiusClampedToOne()
        {
            var s = new CircleShape();
            s.OnDown(Mouse.Left(100, 100));
            s.OnUp(Mouse.Left(100, 100));
            s.OnDown(Mouse.Left(100, 100));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(1, s.Radius, Eps);
        }

        #endregion

        #region Ellipse

        [TestMethod]
        public void Ellipse_DragAndRelease_MinorIsHalfOfMajor()
        {
            var s = new EllipseShape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(40, 0));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(40, s.R1, Eps);
            Assert.AreEqual(20, s.R2, Eps);
            Assert.AreEqual(0, s.Phi, Eps);
        }

        [TestMethod]
        public void Ellipse_ShortRelease_StaysDrawing()
        {
            var s = new EllipseShape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(1, 1));

            Assert.AreEqual(DrawPhase.Drawing, s.Phase);
            Assert.AreEqual(0, s.R1);
        }

        [TestMethod]
        public void Ellipse_SecondClick_FinishesWithClampedRadii()
        {
            var s = new EllipseShape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnUp(Mouse.Left(0, 0));
            s.OnDown(Mouse.Left(0, 0));

            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.AreEqual(1, s.R1, Eps);
            Assert.AreEqual(0.5, s.R2, Eps);
            Assert.AreEqual(0, s.Phi, Eps);
        }

        #endregion

        #region Region

        [TestMethod]
        public void Region_EachLeftDown_AddsVertex()
        {
            var s = new RegionShape();
            s.OnDown(Mouse.Left(10, 20));
            Assert.AreEqual(DrawPhase.Drawing, s.Phase);
            s.OnDown(Mouse.Left(30, 40));
            s.OnDown(Mouse.Left(50, 60));

            CollectionAssert.AreEqual(new[] { 10.0, 30.0, 50.0 }, new System.Collections.Generic.List<double>(s.Cols));
            CollectionAssert.AreEqual(new[] { 20.0, 40.0, 60.0 }, new System.Collections.Generic.List<double>(s.Rows));
        }

        [TestMethod]
        public void Region_RightUpWithFewerThanThreePoints_DoesNothing()
        {
            var s = new RegionShape();
            s.OnDown(Mouse.Left(10, 20));
            s.OnDown(Mouse.Left(30, 40));
            s.OnUp(Mouse.Right(0, 0));

            Assert.AreEqual(DrawPhase.Drawing, s.Phase, "两个点围不成多边形");
            Assert.IsFalse(s.Completed);
        }

        [TestMethod]
        public void Region_FirstRightUpCloses_SecondRightUpConfirms()
        {
            var s = Triangle();

            s.OnUp(Mouse.Right(0, 0));
            Assert.AreEqual(DrawPhase.Editing, s.Phase);
            Assert.IsFalse(s.Completed, "第一次右键只是闭合");

            s.OnUp(Mouse.Right(0, 0));
            Assert.IsTrue(s.Completed);
        }

        [TestMethod]
        public void Region_Editing_LeftDownDoesNotAddVertex_OnlyP1HoverDrags()
        {
            var s = Triangle();
            s.OnUp(Mouse.Right(0, 0));

            s.OnDown(Mouse.Left(99, 99));
            Assert.AreEqual(3, s.Cols.Count, "编辑阶段不再加点");
            Assert.IsFalse(s.Dragging);

            s.Hover = DrawHandle.P1;
            s.OnDown(Mouse.Left(99, 99));
            Assert.IsTrue(s.Dragging);

            s.OnUp(Mouse.Left(99, 99));
            Assert.IsFalse(s.Dragging);
        }

        private static RegionShape Triangle()
        {
            var s = new RegionShape();
            s.OnDown(Mouse.Left(0, 0));
            s.OnDown(Mouse.Left(100, 0));
            s.OnDown(Mouse.Left(0, 100));
            return s;
        }

        #endregion

        private static DrawShape[] AllShapes() => new DrawShape[]
        {
            new PointShape(), new LineShape(), new Rect1Shape(), new Rect2Shape(),
            new CircleShape(), new EllipseShape(), new RegionShape(),
        };
    }
}
