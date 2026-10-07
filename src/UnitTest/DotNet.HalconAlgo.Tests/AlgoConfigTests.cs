using System;
using DotNet.Drawing;
using DotNet.HalconCore;
using DotNet.HalconKit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class DisplayOptionsTests
    {
        [TestMethod]
        public void Defaults()
        {
            var font = new DisplayOptions();
            Assert.IsTrue(font.DispText);
            Assert.AreEqual(50, font.FontX);
            Assert.AreEqual(50, font.FontY);
            Assert.AreEqual(15, font.FontSize);
        }
    }

    [TestClass]
    public class EdgeMeasureSetupTests
    {
        private static EdgeMeasureSetup Make(int stepPace = 10, int stepWidth = 5, string contourType = "first")
            => new EdgeMeasureSetup(new Point2d(3, 4), Angle.FromDegrees(30), 20, 40,
                stepPace, stepWidth, 1, 80, "positive", contourType, 640, 480);

        [TestMethod]
        public void Constructor_CopiesGeometry()
        {
            var s = Make();
            Assert.AreEqual(new Point2d(3, 4), s.Center);
            Assert.AreEqual(30, s.Phi.Degrees, 1e-9);
            Assert.AreEqual(20, s.HalfLength);
            Assert.AreEqual(40, s.HalfHeight);
            Assert.AreEqual(1, s.Sigma);
            Assert.AreEqual(80, s.Threshold);
            Assert.AreEqual("positive", s.Transition);
            Assert.AreEqual(640, s.ImageWidth);
            Assert.AreEqual(480, s.ImageHeight);
        }

        [TestMethod]
        public void HalfWidth_IsHalfStepWidth_ClampedToOne()
        {
            Assert.AreEqual(2.5, Make(stepWidth: 5).HalfWidth);
            Assert.AreEqual(1.0, Make(stepWidth: 1).HalfWidth, "半宽不足 1 像素时 gen_measure_rectangle2 会报错，应夹到 1");
            Assert.AreEqual(1.0, Make(stepWidth: 0).HalfWidth);
            Assert.AreEqual(1.0, Make(stepWidth: -4).HalfWidth);
        }

        [TestMethod]
        public void StepPace_ClampedToOne()
        {
            Assert.AreEqual(10, Make(stepPace: 10).StepPace);
            Assert.AreEqual(1, Make(stepPace: 0).StepPace, "步距为 0 会让步数除零");
            Assert.AreEqual(1, Make(stepPace: -3).StepPace);
        }

        [TestMethod]
        public void Second_MapsToAllWithPickIndexOne()
        {
            var s = Make(contourType: "second");
            Assert.AreEqual("all", s.MeasureSelect, "measure_pos 没有 second 选项，需取 all 再挑第 2 条");
            Assert.AreEqual(1, s.PickIndex);
        }

        [DataTestMethod]
        [DataRow("first", 0)]
        [DataRow("last", -1)]
        [DataRow("all", 0)]
        public void OtherSelections_MapToAllWithPickIndex(string contourType, int pickIndex)
        {
            // measure_pos 忽略定义域: 一律取 all, 滤掉域外点之后再按下标挑 (-1 = 最后一个)
            var s = Make(contourType: contourType);
            Assert.AreEqual("all", s.MeasureSelect);
            Assert.AreEqual(pickIndex, s.PickIndex);
        }
    }

    [TestClass]
    public class FitParaMappingTests : HalconTestBase
    {
        [DataTestMethod]
        [DataRow(Transition.Positive, "positive")]
        [DataRow(Transition.Negative, "negative")]
        [DataRow(Transition.All, "all")]
        public void Transition_Mapping(Transition value, string expected)
        {
            Assert.AreEqual(expected, value.ToHalcon());
        }

        [TestMethod]
        public void Transition_Invalid_Throws()
        {
            // 旧配置手改出来的非法枚举值: 明确报配置错误, 而不是把空串送进 measure_pos 换来 #1302
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ((Transition)99).ToHalcon());
        }

        [DataTestMethod]
        [DataRow(EdgeSelect.First, "first")]
        [DataRow(EdgeSelect.Second, "second")]
        [DataRow(EdgeSelect.Last, "last")]
        [DataRow(EdgeSelect.All, "all")]
        public void ContourType_Mapping(EdgeSelect value, string expected)
        {
            Assert.AreEqual(expected, value.ToHalcon());
        }

        [TestMethod]
        public void FitLine_Defaults()
        {
            var p = new FitLine();
            Assert.AreEqual(RectEnum.AffRect, p.HoRect.Type, "拟合 ROI 默认是带角度的矩形");
            Assert.AreEqual(SourceRef.Local, p.ImageIn);
            Assert.AreEqual(SourceRef.Local, p.RegionIn);
            Assert.AreEqual(SourceRef.Local, p.CoordIn);
            Assert.AreEqual(Transition.Positive, p.Transition);
            Assert.AreEqual(EdgeSelect.First, p.ContourType);
            Assert.AreEqual(80, p.Threshold);
            Assert.IsTrue(p.TrimEnds);
            Assert.IsTrue(new FitLineStrategy().Line.IsDegenerate, "未运行时输出是退化线段而不是 null");
        }

        [TestMethod]
        public void FitArcMidpoint_Defaults()
        {
            var p = new FitArcMidpoint();
            Assert.AreEqual(RectEnum.AffRect, p.HoRect.Type);
            Assert.AreEqual(60, p.Threshold);
            Assert.AreEqual(15.0, p.CoarseGate);
            Assert.IsTrue(p.TrimEnds);
        }
    }
}
