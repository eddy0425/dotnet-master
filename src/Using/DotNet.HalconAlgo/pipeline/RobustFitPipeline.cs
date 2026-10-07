using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 稳健拟合的公共骨架：轮廓重建、一次性粗滤、按残差迭代精滤、裁剪首尾。
    /// </summary>
    /// <remarks>
    /// 提取动机（审查项 D5）：拟合直线与圆弧中点两个策略的 Stage 2 / Stage 3 是同一套逻辑，
    /// 只有「拟合什么」和「残差怎么算」不同。这里把不变的部分收成一处，
    /// 变化的部分由调用方以 <c>refit</c> / <c>residual</c> 两个委托注入。
    /// <para>
    /// 坐标一律 <see cref="Point2d"/>(X=列, Y=行)，(Row, Col) 序只在
    /// <see cref="GenContour"/> 内部出现一次（审查项 C1）。
    /// </para>
    /// </remarks>
    public static class RobustFitPipeline
    {
        /// <summary>
        /// 用点集重建多边形轮廓，供后续 <c>fit_*_contour_xld</c> 使用。旧句柄先释放。
        /// </summary>
        public static void GenContour(ref HObject contour, IReadOnlyList<Point2d> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));

            var rows = new double[points.Count];
            var cols = new double[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                rows[i] = points[i].Y;
                cols[i] = points[i].X;
            }

            contour?.Dispose();
            HOperatorSet.GenContourPolygonXld(out contour, rows, cols);
        }

        /// <summary>
        /// 一次性粗滤：把残差超过 <paramref name="gate"/> 的点整批移到 <paramref name="removed"/>，不重拟合。
        /// </summary>
        /// <returns>被剔除的点数</returns>
        public static int RemoveOutliers(List<Point2d> points, List<Point2d> removed,
            double gate, Func<Point2d, double> residual)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (removed == null) throw new ArgumentNullException(nameof(removed));
            if (residual == null) throw new ArgumentNullException(nameof(residual));

            // 与 Refine 的 maxErr <= 0 同口径：门限 <= 0 表示不做粗滤，否则残差非零的点会被全部剔光
            if (gate <= 0) return 0;

            int count = 0;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                if (residual(points[i]) <= gate) continue;

                removed.Add(points[i]);
                points.RemoveAt(i);
                count++;
            }
            return count;
        }

        /// <summary>
        /// 按残差迭代精滤，直到全部残差不超过 <paramref name="maxErr"/> 或点数触及下限。
        /// </summary>
        /// <param name="points">当前参与拟合的点，就地修改</param>
        /// <param name="removed">被剔除的点，追加写入</param>
        /// <param name="maxErr">允许的最大残差，小于等于 0 表示不做精滤</param>
        /// <param name="minPoints">拟合所需的最少点数，不会剔除到该数量以下</param>
        /// <param name="residual">依据「最近一次拟合」的模型计算单点残差</param>
        /// <param name="refit">用当前 <paramref name="points"/> 重新拟合，并更新调用方持有的模型参数</param>
        /// <returns>实际重拟合的轮数</returns>
        /// <remarks>
        /// 调用约定：进入本方法前调用方必须已经完成一次拟合，<paramref name="residual"/> 才有意义；
        /// 返回时模型对应的是最终点集。
        /// <para>
        /// 与改造前的差异：原实现每轮只剔除「最差的那一个点」再全量重拟合，
        /// 重拟合次数与被剔点数同阶，最坏 O(n) 次 Halcon 拟合、整体 O(n²)。
        /// 现在每轮先算出全部超差点，按残差降序一次剔除其中的一半（至少 1 个），
        /// 重拟合次数降到约 log2(n)。收敛判据没有变——退出时同样满足「所有残差 ≤ maxErr」，
        /// 但中间模型不同，因此临界点的取舍可能与旧实现不完全一致。
        /// </para>
        /// </remarks>
        public static int Refine(List<Point2d> points, List<Point2d> removed,
            double maxErr, int minPoints,
            Func<Point2d, double> residual, Action refit)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (removed == null) throw new ArgumentNullException(nameof(removed));
            if (residual == null) throw new ArgumentNullException(nameof(residual));
            if (refit == null) throw new ArgumentNullException(nameof(refit));

            if (maxErr <= 0) return 0;

            // 每轮至少剔除 1 个点，故轮数上界就是当前点数
            int safety = points.Count;
            var offenders = new List<KeyValuePair<int, double>>();
            var victims = new List<int>();
            int rounds = 0;

            while (rounds < safety)
            {
                offenders.Clear();
                for (int i = 0; i < points.Count; i++)
                {
                    double err = residual(points[i]);
                    if (err > maxErr) offenders.Add(new KeyValuePair<int, double>(i, err));
                }
                if (offenders.Count == 0) break;

                int allowed = points.Count - minPoints;
                if (allowed <= 0) break;

                // 一次剔一半：既保留「先剔最差」的稳健性，又让轮数从 O(n) 降到 O(log n)
                int budget = (offenders.Count + 1) / 2;
                if (budget > allowed) budget = allowed;

                offenders.Sort((a, b) => b.Value.CompareTo(a.Value));

                victims.Clear();
                for (int k = 0; k < budget; k++) victims.Add(offenders[k].Key);
                victims.Sort();

                // 下标降序删除，避免前面的删除让后面的下标失效
                for (int k = victims.Count - 1; k >= 0; k--)
                {
                    removed.Add(points[victims[k]]);
                    points.RemoveAt(victims[k]);
                }

                refit();
                rounds++;
            }

            return rounds;
        }

        /// <summary>
        /// 裁剪首尾各一个点（端点常受 ROI 边界影响而偏移）。
        /// </summary>
        /// <param name="minCountToTrim">点数不足此值时不裁剪，避免把点集削到无法拟合</param>
        /// <returns>是否真的裁剪了；为 true 时调用方需要重新拟合</returns>
        public static bool TrimEnds(List<Point2d> points, List<Point2d> removed, int minCountToTrim)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (removed == null) throw new ArgumentNullException(nameof(removed));

            // 少于 2 个点没有"首尾"可言：1 个点会被加进 removed 两次再越界删除
            if (points.Count < Math.Max(minCountToTrim, 2)) return false;

            int last = points.Count - 1;
            removed.Add(points[0]);
            removed.Add(points[last]);

            points.RemoveAt(last);
            points.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// 点到直线的距离，直线用 Halcon <c>fit_line_contour_xld</c> 输出的 Hesse 法线形式给出。
        /// </summary>
        /// <remarks>法线参数是 (Nr, Nc) 即 (行, 列) 序，故与点的 Y / X 分别相乘。</remarks>
        public static double LineResidual(Point2d point, double nr, double nc, double dist)
        {
            return Math.Abs(nr * point.Y + nc * point.X - dist);
        }

        /// <summary>
        /// 点到圆的径向偏差，圆心用 Halcon <c>fit_circle_contour_xld</c> 输出的 (Row, Col) 给出。
        /// </summary>
        public static double CircleResidual(Point2d point, double centerRow, double centerCol, double radius)
        {
            double dRow = point.Y - centerRow;
            double dCol = point.X - centerCol;
            return Math.Abs(Math.Sqrt(dRow * dRow + dCol * dCol) - radius);
        }
    }
}
