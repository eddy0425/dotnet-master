using System;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconKit;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class EdgeMeasurePipelineTests : HalconTestBase
    {
        private const int W = 200, H = 100;

        /// <summary>中心 (100,50)，phi=0：沿列方向测量，沿行方向步进。</summary>
        private static EdgeMeasureSetup Setup(string transition, string select,
            double halfHeight = 30, int stepPace = 10, int threshold = 30, double phiDeg = 0, Point2d? center = null,
            int sigma = 1)
            => new EdgeMeasureSetup(center ?? new Point2d(100, 50), Angle.FromDegrees(phiDeg), 30, halfHeight,
                stepPace, 5, sigma, threshold, transition, select, W, H);

        /// <summary>列 [100, 120) 为亮条：列 99.5 处上升沿，119.5 处下降沿（都在测量矩形 70..130 内）。</summary>
        private static HObject StripeImage()
        {
            using (var dark = ConstImage(W, H, 0))
            using (var stripe = Rectangle1(0, 100, H - 1, 119))
            {
                return Paint(dark, stripe, 255);
            }
        }

        [TestMethod]
        public void Run_NullImage_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => EdgeMeasurePipeline.Run(null, Setup("positive", "first")));
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("rising")]
        public void Setup_UnknownTransition_Throws(string transition)
        {
            // 过渡方向只有 positive / negative / all 三个合法值; 非法值原先原样传给 measure_pos,
            // 报出 HALCON #1302 这种与配置错误无关的原生异常, 现场无从定位
            var ex = Assert.ThrowsException<ArgumentException>(() => Setup(transition, "first"));
            StringAssert.Contains(ex.Message, "过渡方向");
        }

        [DataTestMethod]
        [DataRow("positive")]
        [DataRow("negative")]
        [DataRow("all")]
        public void Setup_ValidTransition_IsKept(string transition)
        {
            Assert.AreEqual(transition, Setup(transition, "first").Transition);
        }

        [TestMethod]
        public void Run_StepCountAndRectCenters()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("positive", "first", halfHeight: 30, stepPace: 10));

                // stepCount = round(30 / 10) = 3 → 2*3+1 = 7 个测量矩形，行 20..80
                Assert.AreEqual(7, result.RectCenters.Count);
                CollectionAssert.AreEqual(new[] { 20.0, 30, 40, 50, 60, 70, 80 },
                    result.RectCenters.Select(c => Math.Round(c.Y, 6)).ToArray());
                Assert.IsTrue(result.RectCenters.All(c => Math.Abs(c.X - 100) < 1e-9), "phi=0 时步进方向是行，列不变");

                Assert.AreEqual(30, result.HalfLength);
                Assert.AreEqual(2.5, result.HalfWidth);
                Assert.AreEqual(0, result.Phi.Radians);
            }
        }

        [TestMethod]
        public void Run_TinyHalfHeight_StillMeasuresAtLeastThreeRects()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("positive", "first", halfHeight: 2, stepPace: 10));
                Assert.AreEqual(3, result.RectCenters.Count, "步数至少为 1");
            }
        }

        [TestMethod]
        public void Run_FindsStepEdge_OnePointPerRect()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("positive", "first"));

                Assert.AreEqual(7, result.Points.Count);
                foreach (var p in result.Points)
                    Assert.AreEqual(99.5, p.X, 0.5, "边缘点 X 应为列坐标（不是行）");
                CollectionAssert.AreEqual(result.RectCenters.Select(c => Math.Round(c.Y, 3)).ToArray(),
                    result.Points.Select(p => Math.Round(p.Y, 3)).ToArray(), "边缘点落在各测量矩形的中轴上");
            }
        }

        [TestMethod]
        public void Run_WrongTransition_FindsNothing()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("negative", "first"));
                Assert.AreEqual(0, result.Points.Count);
                Assert.AreEqual(7, result.RectCenters.Count, "找不到点时测量矩形仍全部返回，供显示排查");
            }
        }

        [DataTestMethod]
        [DataRow("first", 99.5)]
        [DataRow("last", 119.5)]
        [DataRow("second", 119.5)]
        public void Run_Selection(string select, double expectedX)
        {
            using (var image = StripeImage())
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("all", select));
                Assert.AreEqual(7, result.Points.Count);
                foreach (var p in result.Points)
                    Assert.AreEqual(expectedX, p.X, 0.5);
            }
        }

        [TestMethod]
        public void Run_Second_WithSingleEdge_SkipsRect()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("all", "second"));
                Assert.AreEqual(0, result.Points.Count, "只有一条边时取不到第 2 条，不能越界");
            }
        }

        [TestMethod]
        public void Run_ThresholdAboveContrast_FindsNothing()
        {
            using (var dark = ConstImage(W, H, 100))
            using (var bright = Rectangle1(0, 100, H - 1, W - 1))
            using (var image = Paint(dark, bright, 110))
            {
                Assert.AreEqual(0, EdgeMeasurePipeline.Run(image, Setup("all", "first", threshold: 30)).Points.Count);
                Assert.AreEqual(7, EdgeMeasurePipeline.Run(image, Setup("all", "first", threshold: 2)).Points.Count);
            }
        }

        [TestMethod]
        public void Run_Phi90_StepsAlongColumns()
        {
            // phi=90°：测量方向沿行，步进方向沿列。水平阶跃边在行 50。
            using (var dark = ConstImage(W, H, 0))
            using (var bottom = Rectangle1(50, 0, H - 1, W - 1))
            using (var image = Paint(dark, bottom, 255))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("all", "first", phiDeg: 90));

                Assert.AreEqual(7, result.RectCenters.Count);
                Assert.IsTrue(result.RectCenters.All(c => Math.Abs(c.Y - 50) < 1e-9));
                CollectionAssert.AreEquivalent(new[] { 70.0, 80, 90, 100, 110, 120, 130 },
                    result.RectCenters.Select(c => Math.Round(c.X, 6)).ToArray());

                Assert.AreEqual(7, result.Points.Count);
                foreach (var p in result.Points)
                    Assert.AreEqual(49.5, p.Y, 0.5);
            }
        }

        [TestMethod]
        public void Setup_SigmaBelowHalconMinimum_IsClamped()
        {
            // measure_pos 要求 Sigma >= 0.4；界面的"滤波"下拉提供 0，不钳位会让每一轮都抛 HALCON 参数异常
            Assert.AreEqual(0.4, Setup("positive", "first", sigma: 0).Sigma, 1e-12);
            Assert.AreEqual(0.4, Setup("positive", "first", sigma: -3).Sigma, 1e-12);
            Assert.AreEqual(2, Setup("positive", "first", sigma: 2).Sigma, 1e-12);
        }

        [TestMethod]
        public void Run_SigmaZero_StillFindsEdge()
        {
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("positive", "first", sigma: 0));
                Assert.AreEqual(7, result.Points.Count);
                foreach (var p in result.Points)
                    Assert.AreEqual(99.5, p.X, 0.5);
            }
        }

        [TestMethod]
        public void Run_DropsEdgesOutsideImageDomain()
        {
            // measure_pos 为了效率忽略图像定义域；调用方 reduce_domain 到搜索区域是想限制找边范围，
            // 因此域外的边缘点必须丢弃，否则"区域来源"只影响显示、不影响结果。
            using (var image = VerticalStepImage(W, H, 100))
            using (var left = Rectangle1(0, 0, H - 1, 90))
            {
                HOperatorSet.ReduceDomain(image, left, out HObject reduced);
                using (reduced)
                {
                    var result = EdgeMeasurePipeline.Run(reduced, Setup("positive", "first"));
                    Assert.AreEqual(0, result.Points.Count, "边在列 99.5，搜索区域只到列 90");
                    Assert.AreEqual(7, result.RectCenters.Count);
                }
            }
        }

        [TestMethod]
        public void Run_KeepsEdgesInsidePartialDomain()
        {
            // 只保留上半部分：行 0..45 的测量矩形 (行 20,30,40) 有点，其余丢弃
            using (var image = VerticalStepImage(W, H, 100))
            using (var top = Rectangle1(0, 0, 45, W - 1))
            {
                HOperatorSet.ReduceDomain(image, top, out HObject reduced);
                using (reduced)
                {
                    var result = EdgeMeasurePipeline.Run(reduced, Setup("positive", "first"));
                    CollectionAssert.AreEqual(new[] { 20.0, 30, 40 }, result.Points.Select(p => Math.Round(p.Y, 3)).ToArray());
                }
            }
        }

        [DataTestMethod]
        [DataRow("first", 105, W - 1, 119.5)]
        [DataRow("last", 0, 110, 99.5)]
        public void Run_SelectionAppliesWithinDomain(string select, int col1, int col2, double expectedX)
        {
            // 亮条两条边 99.5 / 119.5 都在测量矩形内, 但只有一条在搜索区域里。
            // 先挑"第一条 / 最后一条"再判定域 —— 挑中的恰是域外那条, 整个采样被丢掉, 区域内的有效边拿不到
            using (var image = StripeImage())
            using (var roi = Rectangle1(0, col1, H - 1, col2))
            {
                HOperatorSet.ReduceDomain(image, roi, out HObject reduced);
                using (reduced)
                {
                    var result = EdgeMeasurePipeline.Run(reduced, Setup("all", select));
                    Assert.AreEqual(7, result.Points.Count);
                    foreach (var p in result.Points)
                        Assert.AreEqual(expectedX, p.X, 0.5);
                }
            }
        }

        [TestMethod]
        public void Run_SecondCountsOnlyEdgesInsideDomain()
        {
            // 域内只有一条边: "第二条"应当找不到, 而不是把域外的那条算作第一条
            using (var image = StripeImage())
            using (var roi = Rectangle1(0, 105, H - 1, W - 1))
            {
                HOperatorSet.ReduceDomain(image, roi, out HObject reduced);
                using (reduced)
                {
                    Assert.AreEqual(0, EdgeMeasurePipeline.Run(reduced, Setup("all", "second")).Points.Count);
                }
            }
        }

        [TestMethod]
        public void Run_ObliquePhi_StepsPerpendicularToMeasureAxis()
        {
            // phi=30°：步进方向 (行 += cos, 列 += sin)，与测量方向 (行 -= sin, 列 += cos) 正交
            using (var image = VerticalStepImage(W, H, 100))
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("all", "first", halfHeight: 20, stepPace: 10, phiDeg: 30));

                Assert.AreEqual(5, result.RectCenters.Count);
                double c = Math.Cos(Math.PI / 6), s = Math.Sin(Math.PI / 6);
                for (int i = 0; i < 5; i++)
                {
                    int k = i - 2;
                    Assert.AreEqual(50 + k * 10 * c, result.RectCenters[i].Y, 1e-9);
                    Assert.AreEqual(100 + k * 10 * s, result.RectCenters[i].X, 1e-9);
                }
            }
        }

        [TestMethod]
        public void Run_StripeNegativeFirst_PicksFallingEdge()
        {
            using (var image = StripeImage())
            {
                var result = EdgeMeasurePipeline.Run(image, Setup("negative", "first"));
                Assert.AreEqual(7, result.Points.Count);
                foreach (var p in result.Points)
                    Assert.AreEqual(119.5, p.X, 0.5);
            }
        }
    }
}
