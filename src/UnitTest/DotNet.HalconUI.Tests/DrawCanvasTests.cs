using System.Collections.Generic;
using System.Windows.Forms;
using DotNet.HalconUI.Draw;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 图元状态机脱离 HALCON 窗口可测（<see cref="IDrawCanvas"/> + <see cref="MouseInput"/>）。
    /// </summary>
    [TestClass]
    public class DrawCanvasTests
    {
        /// <summary> 只记录调用的画布 </summary>
        private sealed class RecordingCanvas : IDrawCanvas
        {
            public readonly List<string> Calls = new List<string>();
            public double PixelSize { get; set; } = 1;
            public void RestoreBackground() => Calls.Add("bg");
            public void Cross(double col, double row, string color, double screenSize = 20) => Calls.Add($"cross {col},{row} {color}");
            public void Rect1(double col1, double row1, double col2, double row2, string color) => Calls.Add($"rect1 {col1},{row1},{col2},{row2}");
            public void Rect2(double cx, double cy, double phi, double len1, double len2, string color) => Calls.Add("rect2");
            public void Rect2Arrow(double cx, double cy, double phi, double len1, double len2, string color) => Calls.Add("rect2arrow");
            public void Arrow(double col1, double row1, double col2, double row2, string color) => Calls.Add("arrow");
            public void Circle(double col, double row, double radius, string color) => Calls.Add("circle");
            public void Ellipse(double cx, double cy, double phi, double r1, double r2, string color) => Calls.Add("ellipse");
            public void Line(double col1, double row1, double col2, double row2, string color) => Calls.Add("line");
        }

        private static MouseInput Left(double x, double y) => new MouseInput(x, y, MouseButtons.Left);
        private static MouseInput Right(double x, double y) => new MouseInput(x, y, MouseButtons.Right);
        private static MouseInput Move(double x, double y) => new MouseInput(x, y);

        [TestMethod]
        public void Rect1Shape_DrawEditConfirm_WithoutHalconWindow()
        {
            var canvas = new RecordingCanvas();
            var shape = new Rect1Shape();
            shape.Attach(canvas);

            shape.OnDown(Left(10, 20));
            Assert.AreEqual(DrawPhase.Drawing, shape.Phase);

            shape.Render(Move(50, 60));
            CollectionAssert.Contains(canvas.Calls, "rect1 10,20,50,60", "拖拽中按鼠标位置画橡皮筋");

            shape.OnUp(Left(50, 60));
            Assert.AreEqual(DrawPhase.Editing, shape.Phase);
            Assert.AreEqual(30, shape.CX);
            Assert.AreEqual(40, shape.CY);

            // 编辑态: 悬停到中心再拖动, 整个矩形平移
            shape.Render(Move(30, 40));
            Assert.AreEqual(DrawHandle.Center, shape.Hover);
            shape.OnDown(Left(30, 40));
            shape.Render(Move(35, 45));
            Assert.AreEqual(15, shape.X1);
            Assert.AreEqual(25, shape.Y1);

            shape.OnUp(Left(35, 45));
            Assert.IsFalse(shape.Completed);
            shape.OnUp(Right(0, 0));
            Assert.IsTrue(shape.Completed, "右键确认");
        }

        [TestMethod]
        public void MouseInput_ConvertsFromHalconEvent()
        {
            MouseInput input = Mouse.Left(3, 4);
            Assert.AreEqual(3, input.X);
            Assert.AreEqual(4, input.Y);
            Assert.AreEqual(MouseButtons.Left, input.Button);
        }
    }
}
