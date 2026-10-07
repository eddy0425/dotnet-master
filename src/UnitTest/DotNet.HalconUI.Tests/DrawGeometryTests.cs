using System;
using DotNet.HalconUI.Draw;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="DrawGeometry"/>：纯几何计算，不碰窗口与 HALCON。
    /// </summary>
    [TestClass]
    public class DrawGeometryTests
    {
        private const double Eps = 1e-9;

        [TestMethod]
        public void Dist_IsEuclidean_AndSymmetric()
        {
            Assert.AreEqual(5, DrawGeometry.Dist(0, 0, 3, 4), Eps);
            Assert.AreEqual(5, DrawGeometry.Dist(3, 4, 0, 0), Eps);
            Assert.AreEqual(0, DrawGeometry.Dist(7, -2, 7, -2), Eps);
        }

        [TestMethod]
        public void NormalizeRect1_AnyDragDirection_YieldsTopLeftBottomRight()
        {
            // 从右下往左上拖
            DrawGeometry.NormalizeRect1(100, 80, 10, 20, out double left, out double top, out double right, out double bottom);

            Assert.AreEqual(10, left);
            Assert.AreEqual(20, top);
            Assert.AreEqual(100, right);
            Assert.AreEqual(80, bottom);
        }

        [TestMethod]
        public void NormalizeRect1_MixedDirection_SortsEachAxisIndependently()
        {
            // 从右上往左下拖：列变小、行变大
            DrawGeometry.NormalizeRect1(100, 20, 10, 80, out double left, out double top, out double right, out double bottom);

            Assert.AreEqual(10, left);
            Assert.AreEqual(20, top);
            Assert.AreEqual(100, right);
            Assert.AreEqual(80, bottom);
        }

        [TestMethod]
        public void IsNear_UsesThresholdTimesPixelSize_Exclusive()
        {
            // 阈值 = 10 * pixelSize，边界上判为不命中（严格小于）
            Assert.IsTrue(DrawGeometry.IsNear(1, 0, 0, 9.99, 0));
            Assert.IsFalse(DrawGeometry.IsNear(1, 0, 0, 10, 0), "恰好落在阈值上不应命中");

            Assert.IsTrue(DrawGeometry.IsNear(2, 0, 0, 19.9, 0), "缩放 2 倍时热区应扩大到 20");
            Assert.IsFalse(DrawGeometry.IsNear(0.5, 0, 0, 6, 0), "缩放 0.5 倍时热区应缩小到 5");
        }

        [TestMethod]
        public void IsNear_MeasuresRadially_NotPerAxis()
        {
            // 两轴各 8 在单轴阈值内，但径向距离 ≈ 11.3 已超出
            Assert.IsFalse(DrawGeometry.IsNear(1, 0, 0, 8, 8));
            Assert.IsTrue(DrawGeometry.IsNear(1, 0, 0, 6, 6));
        }

        [TestMethod]
        public void AxisEnd_PhiZero_PointsAlongPositiveColumn()
        {
            DrawGeometry.AxisEnd(50, 40, 0, 10, out double ex, out double ey);

            Assert.AreEqual(60, ex, Eps);
            Assert.AreEqual(40, ey, Eps);
        }

        [TestMethod]
        public void AxisEnd_PhiHalfPi_PointsUp_BecauseRowGrowsDownward()
        {
            // 图像坐标系行向下增长，phi = +90° 的方向是「向上」，即行减小
            DrawGeometry.AxisEnd(50, 40, Math.PI / 2, 10, out double ex, out double ey);

            Assert.AreEqual(50, ex, Eps);
            Assert.AreEqual(30, ey, Eps);
        }

        [TestMethod]
        public void AxisEndPerp_IsPerpendicularToAxisEnd()
        {
            const double cx = 50, cy = 40, phi = 0.7;
            DrawGeometry.AxisEnd(cx, cy, phi, 10, out double ax, out double ay);
            DrawGeometry.AxisEndPerp(cx, cy, phi, 6, out double px, out double py);

            double dot = (ax - cx) * (px - cx) + (ay - cy) * (py - cy);
            Assert.AreEqual(0, dot, 1e-9, "副轴端点应与主轴方向正交");
            Assert.AreEqual(6, DrawGeometry.Dist(cx, cy, px, py), 1e-9);
        }

        [TestMethod]
        public void AxisEndPerp_PhiZero_PointsUp()
        {
            DrawGeometry.AxisEndPerp(50, 40, 0, 10, out double ex, out double ey);

            Assert.AreEqual(50, ex, Eps);
            Assert.AreEqual(30, ey, Eps);
        }

        [TestMethod]
        public void AxisEndPerp_NegativeLength_MirrorsAcrossCenter()
        {
            DrawGeometry.AxisEndPerp(50, 40, 0.3, 10, out double px, out double py);
            DrawGeometry.AxisEndPerp(50, 40, 0.3, -10, out double nx, out double ny);

            Assert.AreEqual(100, px + nx, Eps);
            Assert.AreEqual(80, py + ny, Eps);
        }
    }
}
