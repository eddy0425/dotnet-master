using System;
using System.Collections.Generic;
using System.Linq;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    [TestClass]
    public class RobustFitPipelineTests
    {
        /// <summary>残差 = |Y|：点到 X 轴的距离。</summary>
        private static double DistToXAxis(Point2d p) => Math.Abs(p.Y);

        private static List<Point2d> Pts(params double[] ys)
            => ys.Select((y, i) => new Point2d(i, y)).ToList();

        #region Residuals

        [TestMethod]
        public void LineResidual_IsAbsoluteSignedDistance()
        {
            // 直线 Y = 5：法向 (nr=1, nc=0)，dist = 5
            Assert.AreEqual(0, RobustFitPipeline.LineResidual(new Point2d(123, 5), 1, 0, 5), 1e-12);
            Assert.AreEqual(3, RobustFitPipeline.LineResidual(new Point2d(0, 8), 1, 0, 5), 1e-12);
            Assert.AreEqual(3, RobustFitPipeline.LineResidual(new Point2d(0, 2), 1, 0, 5), 1e-12);

            // 直线 X = 10：法向 (nr=0, nc=1)。行列不能搞反：nr 乘 Y(行)，nc 乘 X(列)。
            Assert.AreEqual(4, RobustFitPipeline.LineResidual(new Point2d(14, 99), 0, 1, 10), 1e-12);
        }

        [TestMethod]
        public void CircleResidual_IsRadialError()
        {
            // 圆心 (row=10, col=20), 半径 5
            Assert.AreEqual(0, RobustFitPipeline.CircleResidual(new Point2d(25, 10), 10, 20, 5), 1e-12);
            Assert.AreEqual(0, RobustFitPipeline.CircleResidual(new Point2d(20, 5), 10, 20, 5), 1e-12);
            Assert.AreEqual(2, RobustFitPipeline.CircleResidual(new Point2d(27, 10), 10, 20, 5), 1e-12);
            Assert.AreEqual(5, RobustFitPipeline.CircleResidual(new Point2d(20, 10), 10, 20, 5), 1e-12, "圆心处残差等于半径");
        }

        #endregion

        #region RemoveOutliers

        [TestMethod]
        public void RemoveOutliers_RemovesStrictlyAboveGate_PreservingOrder()
        {
            var points = Pts(0, 5, 1, -7, 2, 3);
            var removed = new List<Point2d>();

            int n = RobustFitPipeline.RemoveOutliers(points, removed, 3, DistToXAxis);

            Assert.AreEqual(2, n);
            CollectionAssert.AreEqual(new double[] { 0, 1, 2, 3 }, points.Select(p => p.Y).ToArray(), "等于门限的点保留，剩余点保持原顺序");
            CollectionAssert.AreEquivalent(new double[] { 5, -7 }, removed.Select(p => p.Y).ToArray());
        }

        [TestMethod]
        public void RemoveOutliers_NothingAboveGate_ReturnsZero()
        {
            var points = Pts(0, 1, 2);
            var removed = new List<Point2d>();
            Assert.AreEqual(0, RobustFitPipeline.RemoveOutliers(points, removed, 10, DistToXAxis));
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(0, removed.Count);
        }

        [TestMethod]
        public void RemoveOutliers_NonPositiveGate_IsDisabled()
        {
            // 与 Refine 的 maxErr <= 0 同口径：门限为 0 时不做粗滤，否则所有残差非零的点都会被剔光
            var points = Pts(0, 5, 1, -7);
            var removed = new List<Point2d>();

            Assert.AreEqual(0, RobustFitPipeline.RemoveOutliers(points, removed, 0, DistToXAxis));
            Assert.AreEqual(0, RobustFitPipeline.RemoveOutliers(points, removed, -1, DistToXAxis));
            Assert.AreEqual(4, points.Count);
            Assert.AreEqual(0, removed.Count);
        }

        [TestMethod]
        public void RemoveOutliers_NullArguments_Throw()
        {
            var list = new List<Point2d>();
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.RemoveOutliers(null, list, 1, DistToXAxis));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.RemoveOutliers(list, null, 1, DistToXAxis));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.RemoveOutliers(list, list, 1, null));
        }

        #endregion

        #region Refine

        [TestMethod]
        public void Refine_NonPositiveMaxErr_IsDisabled()
        {
            var points = Pts(0, 100, 200);
            var removed = new List<Point2d>();
            int refits = 0;

            Assert.AreEqual(0, RobustFitPipeline.Refine(points, removed, 0, 2, DistToXAxis, () => refits++));
            Assert.AreEqual(0, RobustFitPipeline.Refine(points, removed, -1, 2, DistToXAxis, () => refits++));
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(0, refits);
        }

        [TestMethod]
        public void Refine_RemovesHalfOfWorstOffendersPerRound()
        {
            // 4 个超差点：第一轮只剔最差的 ceil(4/2)=2 个，第二轮再剔 1 个，第三轮剔最后 1 个。
            var points = Pts(0, 0, 0, 0, 10, 40, 20, 30);
            var removed = new List<Point2d>();
            int refits = 0;

            int rounds = RobustFitPipeline.Refine(points, removed, 5, 2, DistToXAxis, () => refits++);

            Assert.AreEqual(3, rounds);
            Assert.AreEqual(rounds, refits, "每轮剔点后都要重拟合一次");
            var removedY = removed.Select(p => p.Y).ToArray();
            CollectionAssert.AreEquivalent(new double[] { 40, 30 }, removedY.Take(2).ToArray(), "第一轮剔最差的两个");
            CollectionAssert.AreEqual(new double[] { 20, 10 }, removedY.Skip(2).ToArray());
            Assert.IsTrue(points.All(p => p.Y == 0));
        }

        [TestMethod]
        public void Refine_UsesRefittedResidualEachRound()
        {
            // 模拟重拟合：残差基准随 refit 改变。第一轮后基准移到 10，原先离群的点变成合格。
            var points = Pts(0, 0, 0, 10, 10, 10, 50);
            var removed = new List<Point2d>();
            double center = 25;

            int rounds = RobustFitPipeline.Refine(points, removed, 5, 2,
                p => Math.Abs(p.Y - center),
                () => center = points.Average(p => p.Y));

            Assert.IsTrue(rounds >= 1);
            Assert.IsTrue(removed.Any(p => p.Y == 50), "最远的点必然被剔除");
            Assert.IsTrue(points.All(p => Math.Abs(p.Y - center) <= 5), "结束时剩余点都在门限内");
        }

        [TestMethod]
        public void Refine_NeverGoesBelowMinPoints()
        {
            var points = Pts(10, 20, 30, 40, 50);
            var removed = new List<Point2d>();

            RobustFitPipeline.Refine(points, removed, 1, 3, DistToXAxis, () => { });

            Assert.AreEqual(3, points.Count, "全部超差时也要保留 minPoints 个点");
            Assert.AreEqual(2, removed.Count);
            CollectionAssert.AreEqual(new double[] { 10, 20, 30 }, points.Select(p => p.Y).ToArray(), "保留残差最小的那些");
        }

        [TestMethod]
        public void Refine_AlreadyAtMinPoints_DoesNothing()
        {
            var points = Pts(100, 200);
            var removed = new List<Point2d>();
            int refits = 0;

            Assert.AreEqual(0, RobustFitPipeline.Refine(points, removed, 1, 2, DistToXAxis, () => refits++));
            Assert.AreEqual(2, points.Count);
            Assert.AreEqual(0, refits);
        }

        [TestMethod]
        public void Refine_NullArguments_Throw()
        {
            var list = new List<Point2d>();
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.Refine(null, list, 1, 2, DistToXAxis, () => { }));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.Refine(list, null, 1, 2, DistToXAxis, () => { }));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.Refine(list, list, 1, 2, null, () => { }));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.Refine(list, list, 1, 2, DistToXAxis, null));
        }

        #endregion

        #region TrimEnds

        [TestMethod]
        public void TrimEnds_RemovesFirstAndLast()
        {
            var points = Pts(1, 2, 3, 4);
            var removed = new List<Point2d>();

            Assert.IsTrue(RobustFitPipeline.TrimEnds(points, removed, 4));
            CollectionAssert.AreEqual(new double[] { 2, 3 }, points.Select(p => p.Y).ToArray());
            CollectionAssert.AreEqual(new double[] { 1, 4 }, removed.Select(p => p.Y).ToArray());
        }

        [TestMethod]
        public void TrimEnds_BelowMinCount_LeavesPointsUntouched()
        {
            var points = Pts(1, 2, 3);
            var removed = new List<Point2d>();

            Assert.IsFalse(RobustFitPipeline.TrimEnds(points, removed, 4));
            Assert.AreEqual(3, points.Count);
            Assert.AreEqual(0, removed.Count);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void TrimEnds_FewerThanTwoPoints_NeverTrims(int count)
        {
            // minCountToTrim 给得过小时也不能越界：少于 2 个点没有"首尾"可言
            var points = Pts(new double[count]);
            var removed = new List<Point2d>();

            Assert.IsFalse(RobustFitPipeline.TrimEnds(points, removed, 0));
            Assert.AreEqual(count, points.Count);
            Assert.AreEqual(0, removed.Count);
        }

        [TestMethod]
        public void TrimEnds_TwoPoints_MinCountZero_RemovesBoth()
        {
            var points = Pts(1, 2);
            var removed = new List<Point2d>();

            Assert.IsTrue(RobustFitPipeline.TrimEnds(points, removed, 0));
            Assert.AreEqual(0, points.Count);
            CollectionAssert.AreEqual(new double[] { 1, 2 }, removed.Select(p => p.Y).ToArray());
        }

        [TestMethod]
        public void TrimEnds_NullArguments_Throw()
        {
            var list = new List<Point2d>();
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.TrimEnds(null, list, 1));
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.TrimEnds(list, null, 1));
        }

        #endregion
    }

    [TestClass]
    public class RobustFitPipelineContourTests : HalconTestBase
    {
        [TestMethod]
        public void GenContour_UsesYAsRowAndXAsColumn()
        {
            HObject contour = null;
            try
            {
                RobustFitPipeline.GenContour(ref contour, new[] { new Point2d(10, 1), new Point2d(20, 2), new Point2d(30, 3) });

                HOperatorSet.GetContourXld(contour, out HTuple rows, out HTuple cols);
                CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, rows.DArr);
                CollectionAssert.AreEqual(new[] { 10.0, 20.0, 30.0 }, cols.DArr);
            }
            finally
            {
                contour?.Dispose();
            }
        }

        [TestMethod]
        public void GenContour_ReplacesExistingHandle()
        {
            HObject contour = null;
            try
            {
                RobustFitPipeline.GenContour(ref contour, new[] { new Point2d(0, 0), new Point2d(1, 1) });
                var first = contour;

                RobustFitPipeline.GenContour(ref contour, new[] { new Point2d(5, 5), new Point2d(6, 6), new Point2d(7, 7) });

                Assert.AreNotSame(first, contour);
                Assert.IsFalse(first.IsInitialized(), "旧轮廓句柄应被释放");
                HOperatorSet.GetContourXld(contour, out HTuple rows, out HTuple _);
                Assert.AreEqual(3, rows.Length);
            }
            finally
            {
                contour?.Dispose();
            }
        }

        [TestMethod]
        public void GenContour_NullPoints_Throws()
        {
            HObject contour = null;
            Assert.ThrowsException<ArgumentNullException>(() => RobustFitPipeline.GenContour(ref contour, null));
        }
    }
}
