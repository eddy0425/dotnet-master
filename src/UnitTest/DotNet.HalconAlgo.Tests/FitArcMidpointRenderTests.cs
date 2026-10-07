using System.Collections.Generic;
using System.Linq;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class FitArcMidpointRenderDataTests : HalconTestBase
    {
        private static FitArcMidpointRenderData Full(bool show = true)
        {
            HOperatorSet.GenCircleContourXld(out HObject arc, 100, 100, 50, -0.5, 0.5, "positive", 1);
            return new FitArcMidpointRenderData
            {
                SearchRegion = Rectangle1(0, 0, 10, 10),
                ArcContour = arc,
                MeasurePoints = new List<Point2d> { new Point2d(1, 2), new Point2d(3, 4), new Point2d(5, 6) },
                MeasurePhi = Angle.FromDegrees(0),
                MeasureLen1 = 20,
                MeasureLen2 = 2.5,
                UsedPoints = new List<Point2d> { new Point2d(10, 10), new Point2d(11, 11) },
                RemovedPoints = new List<Point2d> { new Point2d(99, 99) },
                Midpoint = new Point2d(150, 100),
                HasMidpoint = true,
                Message = "圆弧中点 : ok",
                PointSize = 7,
                FontX = 20,
                FontY = 30,
                FontSize = 15,
                ShowRegion = show,
                ShowFixRegion = show,
                ShowPoints = show,
                ShowResult = show,
                ShowText = show,
            };
        }

        [TestMethod]
        public void DrawTo_AllFlagsOn_DrawsEveryLayer()
        {
            var display = new FakeDisplay();
            using (var data = Full())
            {
                data.DrawTo(display);

                Assert.AreEqual(2, display.Objects.Count, "查找区域 + 圆弧轮廓");
                Assert.AreSame(data.SearchRegion, display.Objects[0].Item);
                Assert.AreEqual(HColor.Blue.Name, display.Objects[0].ColorName);
                Assert.AreSame(data.ArcContour, display.Objects[1].Item);
                Assert.AreEqual(HColor.Red.Name, display.Objects[1].ColorName);

                CollectionAssert.AreEqual(data.MeasurePoints.ToList(), display.Rect2Centers);

                var red = display.Points.Where(p => p.ColorName == HColor.Red.Name).ToList();
                var green = display.Points.Where(p => p.ColorName == HColor.Green.Name).ToList();
                var mid = display.Points.Where(p => p.ColorName == HColor.OrangeRed.Name).ToList();
                Assert.AreEqual(1, red.Count);
                Assert.AreEqual(2, green.Count);
                Assert.AreEqual(1, mid.Count);
                Assert.AreEqual(new Point2d(150, 100), mid[0].Item);
                Assert.AreEqual(7, green[0].Style.Size);
                Assert.AreEqual(57, mid[0].Style.Size, "中点比普通点放大 50");

                Assert.AreEqual(1, display.Texts.Count);
                Assert.AreEqual("圆弧中点 : ok", display.Texts[0].Item);
                Assert.AreEqual(new Point2d(20, 30), display.Texts[0].Position);
                Assert.AreEqual(HColor.Green.Name, display.Texts[0].ColorName);
            }
        }

        [TestMethod]
        public void DrawTo_AllFlagsOff_DrawsNothing()
        {
            var display = new FakeDisplay();
            using (var data = Full(show: false))
            {
                data.DrawTo(display);
            }

            Assert.AreEqual(0, display.Objects.Count);
            Assert.AreEqual(0, display.Rect2Centers.Count);
            Assert.AreEqual(0, display.Points.Count);
            Assert.AreEqual(0, display.Texts.Count);
        }

        [TestMethod]
        public void DrawTo_PartialData_SkipsMissingElements()
        {
            // 拟合中途失败：只有查找区域与测量矩形，没有轮廓、中点和文本。
            var display = new FakeDisplay();
            using (var data = Full())
            {
                data.ArcContour.Dispose();
                data.ArcContour = null;
                data.HasMidpoint = false;
                data.Message = null;

                data.DrawTo(display);
            }

            Assert.AreEqual(1, display.Objects.Count);
            Assert.AreEqual(HColor.Blue.Name, display.Objects[0].ColorName);
            Assert.AreEqual(0, display.Points.Count(p => p.ColorName == HColor.OrangeRed.Name));
            Assert.AreEqual(0, display.Texts.Count);
        }

        [TestMethod]
        public void DrawTo_EmptyMessage_NoText()
        {
            var display = new FakeDisplay();
            using (var data = Full())
            {
                data.Message = "";
                data.DrawTo(display);
            }
            Assert.AreEqual(0, display.Texts.Count);
        }

        [TestMethod]
        public void Defaults_AreEmptyAndSafeToDraw()
        {
            var display = new FakeDisplay();
            using (var data = new FitArcMidpointRenderData { ShowRegion = true, ShowFixRegion = true, ShowPoints = true, ShowResult = true, ShowText = true })
            {
                data.DrawTo(display);
            }

            Assert.AreEqual(0, display.Objects.Count + display.Points.Count + display.Rect2Centers.Count + display.Texts.Count);
        }

        [TestMethod]
        public void Dispose_ReleasesAndNullsHandles_Idempotent()
        {
            var data = Full();
            var region = data.SearchRegion;
            var arc = data.ArcContour;

            data.Dispose();
            data.Dispose();

            Assert.IsNull(data.SearchRegion);
            Assert.IsNull(data.ArcContour);
            Assert.IsFalse(region.IsInitialized());
            Assert.IsFalse(arc.IsInitialized());
        }
    }
}
