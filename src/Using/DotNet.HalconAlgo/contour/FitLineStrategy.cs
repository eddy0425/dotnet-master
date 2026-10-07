using HalconDotNet;
using System;
using DotNet.Drawing;
using DotNet.HalconCore;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 拟合直线：沿测量区域逐步找边缘点，gauss 鲁棒直线拟合 + 最大偏差迭代精滤 + 可选裁剪首尾点。
    /// </summary>
    [Algo("fit.line", "拟合直线", Group = "测量", Order = 310, DefaultRoi = RectEnum.AffRect)]
    public class FitLineStrategy : EdgeFitStrategyBase<FitLine>
    {
        /// <summary> 拟合一条直线所需的最少点数 </summary>
        private const int MinFitPoints = 2;

        // ---------- 显示数据：每轮 Execute 开头清空，失败时保留已生成的部分便于排查 ----------
        private HObject _searchRegion;
        private EdgeMeasureResult _measured;
        private List<Point2d> _used = new List<Point2d>();
        private List<Point2d> _removed = new List<Point2d>();

        /// <summary> 拟合出的直线；失败时为零长线段（<see cref="CvLine.IsDegenerate"/>） </summary>
        public CvLine Line { get; private set; } = new CvLine(0, 0, 0, 0);

        protected override void DeclareOutputs(OutputBuilder o)
        {
            o.Line("直线", () => Line);
        }

        protected override void ResetOutputs() => Line = new CvLine(0, 0, 0, 0);

        protected override RunResult Execute(RunContext context)
        {
            ClearRenderData();
            _measured = Measure(context, out _searchRegion);
            var points = _measured.Points;
            _used = points;

            if (points.Count < MinFitPoints)
                throw new InvalidOperationException("未找到足够的轮廓点！");

            HOperatorSet.GenEmptyObj(out HObject contour);
            try
            {
                // 拟合结果：由 refit 闭包更新，供残差函数与最终取值共用
                HTuple rowBegin = 0, colBegin = 0, rowEnd = 0, colEnd = 0;
                HTuple nr = 0, nc = 0, dist = 0;
                Action refit = () =>
                {
                    RobustFitPipeline.GenContour(ref contour, points);
                    HOperatorSet.FitLineContourXld(contour, "gauss", -1, 0, 5, 1.345,
                        out rowBegin, out colBegin, out rowEnd, out colEnd, out nr, out nc, out dist);
                };

                // Stage 1：gauss 鲁棒直线拟合
                refit();

                // Stage 2：依据最大偏差迭代精滤
                RobustFitPipeline.Refine(points, _removed, Math.Max(inPara.MaxErr, 0), MinFitPoints,
                    pt => RobustFitPipeline.LineResidual(pt, nr.D, nc.D, dist.D), refit);
                if (points.Count < MinFitPoints)
                    throw new InvalidOperationException("最大偏差筛选后有效点不足，无法拟合直线！");

                // Stage 3：可选裁剪筛选后首尾点并重新拟合
                if (inPara.TrimEnds && RobustFitPipeline.TrimEnds(points, _removed, MinFitPoints + 2))
                    refit();

                Line = new CvLine(colBegin.D, rowBegin.D, colEnd.D, rowEnd.D);
            }
            finally
            {
                contour.Dispose();
            }

            return RunResult.Ok($"起点:({Line.Start.X:F2},{Line.Start.Y:F2}) 终点:({Line.End.X:F2},{Line.End.Y:F2}) " +
                                $"角度:{Line.AngleDegrees:F2}° 用点:{points.Count}");
        }

        /// <summary> 写完叠加层（它复制句柄）立即释放只供绘制的数据，不留到下一轮 </summary>
        protected override void Render(IOverlay overlay, RunResult result)
        {
            if (inPara.DispRegion && _searchRegion.IsUsableRegion())
                overlay.Add(_searchRegion, DrawStyle.Of(HColor.Blue));

            if (inPara.DispFixRegion && _measured != null)
            {
                foreach (Point2d center in _measured.RectCenters)
                    overlay.AddRect2(center, _measured.Phi.Radians, _measured.HalfLength, _measured.HalfWidth, DrawStyle.Of(HColor.Blue));
            }

            if (inPara.DispFixPoint)
            {
                foreach (Point2d pt in _removed) overlay.Add(pt, DrawStyle.Of(HColor.Red, inPara.PointSize));
                foreach (Point2d pt in _used) overlay.Add(pt, DrawStyle.Of(HColor.Green, inPara.PointSize));
            }

            if (inPara.DispResult && !Line.IsDegenerate)
                overlay.Add(new CvArrow(Line, 2), DrawStyle.Of(HColor.Red));

            ClearRenderData();
        }

        private void ClearRenderData()
        {
            _searchRegion?.Dispose();
            _searchRegion = null;
            _measured = null;
            _used = new List<Point2d>();
            _removed = new List<Point2d>();
        }

        /// <summary> 工具页关闭：丢弃显示数据，配置 ROI 保留 </summary>
        public override void Close(IInteractionHost host) => ClearRenderData();

        protected override void Dispose(bool disposing)
        {
            ClearRenderData();
            base.Dispose(disposing);
        }
    }

    public class FitLine : EdgeFitPara
    {
        public FitLine()
        {
            Threshold = 80;
        }
    }
}
