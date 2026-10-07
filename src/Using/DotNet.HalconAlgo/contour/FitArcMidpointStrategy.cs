using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 圆弧中点：沿测量区域找边缘点，atukey 稳健圆拟合 + 径向残差粗滤 / 精滤，输出弧段中点。
    /// </summary>
    /// <remarks>
    /// 显示数据单独做成 <see cref="FitArcMidpointRenderData"/>：<see cref="Execute"/> 填充，
    /// <see cref="Render"/> 写进叠加层后立即释放。无界面运行（不绘制）时留到下一轮 / 关闭页面时释放。
    /// </remarks>
    [Algo("fit.arc-midpoint", "圆弧中点", Group = "测量", Order = 320, DefaultRoi = RectEnum.AffRect)]
    public class FitArcMidpointStrategy : EdgeFitStrategyBase<FitArcMidpoint>
    {
        /// <summary> 拟合一段圆弧所需的最少点数 </summary>
        private const int MinFitPoints = 3;

        /// <summary> 粗滤阈值相对 MaxErr 的放大倍数 </summary>
        /// <remarks>
        /// 粗滤只负责剔除「明显落在别的边上」的点，判据要比 Stage 2 的 MaxErr 宽松，
        /// 否则等于把精滤提前做了一遍，还少了迭代重拟合的纠偏机会。
        /// </remarks>
        private const double CoarseGateErrScale = 3.0;

        // 本轮的显示数据, 只供 Render 使用
        private FitArcMidpointRenderData _render;

        /// <summary> 弧段中点；失败时为默认值 </summary>
        public Point2d ArcMidpoint { get; private set; }

        protected override void DeclareParams(ParamBuilder p)
        {
            base.DeclareParams(p);
            p.Double("粗滤阈值", () => inPara.CoarseGate, v => inPara.CoarseGate = v, presets: new double[] { 5, 10, 15, 30 }, min: 0);
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            o.Point("中点", () => ArcMidpoint);
        }

        protected override void ResetOutputs() => ArcMidpoint = default(Point2d);

        /// <summary>
        /// 纯计算：不触碰任何显示对象。显示数据无论成败都会留给 <see cref="Render"/>（失败时为部分数据）。
        /// </summary>
        protected override RunResult Execute(RunContext context)
        {
            ClearRenderData();
            var render = _render = new FitArcMidpointRenderData
            {
                PointSize = inPara.PointSize,
                ShowRegion = inPara.DispRegion,
                ShowFixRegion = inPara.DispFixRegion,
                ShowPoints = inPara.DispFixPoint,
                ShowResult = inPara.DispResult,
            };

            HObject contour = null;
            try
            {
                HObject search = null;
                EdgeMeasureResult measured;
                try
                {
                    measured = Measure(context, out search);
                    render.SearchRegion = search;
                    search = null;
                }
                finally
                {
                    search?.Dispose();
                }

                List<Point2d> points = measured.Points;
                var removed = new List<Point2d>();
                render.MeasurePoints = measured.RectCenters;
                render.MeasurePhi = measured.Phi;
                render.MeasureLen1 = measured.HalfLength;
                render.MeasureLen2 = measured.HalfWidth;
                // 点集立即挂上显示数据 (同一列表实例, 后续筛选原地增删): "点不足"这类失败最需要看点落在哪
                render.UsedPoints = points;
                render.RemovedPoints = removed;

                if (points.Count < MinFitPoints)
                    throw new InvalidOperationException("未找到足够的轮廓点！");

                double maxErr = Math.Max(inPara.MaxErr, 0);
                double coarseGate = Math.Max(maxErr * CoarseGateErrScale, Math.Max(inPara.CoarseGate, 0));

                // 拟合结果：由 refit 闭包更新，供残差函数与最终取值共用
                HOperatorSet.GenEmptyObj(out contour);
                HTuple circRow = 0, circCol = 0, circRadius = 0;
                HTuple circStartPhi = 0, circEndPhi = 0, circPointOrder = "positive";
                Action refit = () =>
                {
                    RobustFitPipeline.GenContour(ref contour, points);
                    HOperatorSet.FitCircleContourXld(contour, "atukey", -1, 0, 0, 5, 2,
                        out circRow, out circCol, out circRadius, out circStartPhi, out circEndPhi, out circPointOrder);
                };

                // 残差一律取「到拟合圆的径向偏差」，Stage 1 / Stage 2 同一把尺子
                Func<Point2d, double> radialErr = pt => RobustFitPipeline.CircleResidual(pt, circRow.D, circCol.D, circRadius.D);

                // Stage 1：atukey 稳健圆拟合 + 径向残差一次性粗滤。
                // 模型与待测几何同形，弧相对弦的凸量不再计入残差；atukey 是重降权估计，离群点首次拟合时权重就已趋零。
                refit();
                int coarseCulled = RobustFitPipeline.RemoveOutliers(points, removed, coarseGate, radialErr);
                if (points.Count < MinFitPoints)
                    throw new InvalidOperationException("粗滤后有效点不足，无法拟合圆弧！");
                // 点集变了，模型必须跟上：Stage 2 的收敛判据建立在「模型对应当前点集」之上
                if (coarseCulled > 0) refit();

                // Stage 2：径向距离迭代精滤
                RobustFitPipeline.Refine(points, removed, maxErr, MinFitPoints, radialErr, refit);
                if (points.Count < MinFitPoints)
                    throw new InvalidOperationException("最大偏差筛选后有效点不足，无法拟合圆弧！");

                // Stage 3：可选裁剪筛选后首尾点并重新拟合
                if (inPara.TrimEnds && RobustFitPipeline.TrimEnds(points, removed, MinFitPoints + 2))
                    refit();

                Angle midPhi = ComputeArcMidPhi(Angle.FromRadians(circStartPhi.D), Angle.FromRadians(circEndPhi.D), circPointOrder.S);
                double midRow = circRow.D - circRadius.D * Math.Sin(midPhi.Radians);
                double midCol = circCol.D + circRadius.D * Math.Cos(midPhi.Radians);
                ArcMidpoint = new Point2d(midCol, midRow);

                HOperatorSet.GenCircleContourXld(out HObject arc, circRow, circCol, circRadius, circStartPhi, circEndPhi, circPointOrder, 1);
                render.ArcContour = arc;
                render.Midpoint = ArcMidpoint;
                render.HasMidpoint = true;

                return RunResult.Ok($"中点:({ArcMidpoint.X:F2},{ArcMidpoint.Y:F2}) 半径:{circRadius.D:F2} 用点:{points.Count}");
            }
            finally
            {
                contour?.Dispose();
            }
        }

        /// <summary> 写完叠加层（它复制句柄）立即释放本轮显示数据 </summary>
        protected override void Render(IOverlay overlay, RunResult result)
        {
            _render?.DrawTo(overlay);
            ClearRenderData();
        }

        private void ClearRenderData()
        {
            _render?.Dispose();
            _render = null;
        }

        /// <summary>
        /// 根据拟合得到的起止角与点序，计算弧段中点所在角度（Halcon 图像坐标系）。
        /// </summary>
        private static Angle ComputeArcMidPhi(Angle startPhi, Angle endPhi, string pointOrder)
        {
            const double twoPi = 2 * Math.PI;
            double start = startPhi.Radians;
            double end = endPhi.Radians;

            if (pointOrder == "positive")
            {
                double span = end - start;
                if (span < 0) span += twoPi;
                return Angle.FromRadians(start + span * 0.5);
            }
            else
            {
                double span = start - end;
                if (span < 0) span += twoPi;
                return Angle.FromRadians(start - span * 0.5);
            }
        }

        /// <summary> 工具页关闭：丢弃未绘制的显示数据，配置 ROI 保留 </summary>
        public override void Close(IInteractionHost host) => ClearRenderData();

        protected override void Dispose(bool disposing)
        {
            ClearRenderData();
            base.Dispose(disposing);
        }
    }

    public class FitArcMidpoint : EdgeFitPara
    {
        /// <summary> 粗滤阈值下限（像素） </summary>
        /// <remarks>
        /// Stage 1 的实际门限取 <c>Max(MaxErr * 3, CoarseGate)</c>：径向偏差超过它的点
        /// 在首次稳健圆拟合后就整批剔除，不参与后续迭代。需要更早拦住跳到邻边的点时可以调小。
        /// </remarks>
        public double CoarseGate { get; set; } = 15.0;
    }
}
