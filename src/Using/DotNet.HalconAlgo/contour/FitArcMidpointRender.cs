using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;
using System.Collections.Generic;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 圆弧中点拟合的一帧显示数据：<c>Execute</c> 逐项填充，<c>Render</c> 写进叠加层后即释放。
    /// 样式与可见性开关在拟合时从 inPara 捕获。其中的 HObject 归本对象所有，随 Dispose 释放（可重复调用）。
    /// </summary>
    /// <remarks>
    /// 拟合过程按阶段逐项填充（中途失败时保留已生成的部分便于排查），无法一次性构造，
    /// 因此属性的 setter 限定为 <c>internal</c>：只有本程序集内的拟合策略能写。点集以 <see cref="IReadOnlyList{T}"/> 暴露。
    /// 在哪个线程画、缩放后怎么重画由宿主负责（叠加层复制句柄），本类不再处理跨线程与所有权转移。
    /// </remarks>
    public sealed class FitArcMidpointRenderData : IDisposable
    {
        /// <summary> 查找区域（蓝）；尚未生成或已 Dispose 时为 null </summary>
        public HObject SearchRegion { get; internal set; }

        /// <summary> 拟合出的圆弧轮廓（红）；拟合失败或已 Dispose 时为 null </summary>
        public HObject ArcContour { get; internal set; }

        /// <summary> 逐步测量矩形中心（拟合区域，蓝），姿态与尺寸各步相同 </summary>
        /// <remarks>
        /// 这里以及下面几组点集都用 <see cref="Point2d"/>(X=列, Y=行)，
        /// 不再是并行的 rows / cols 两条 <c>List&lt;double&gt;</c>（审查项 C1）：
        /// 两条并行列表既无法保证等长，也让「哪个是行哪个是列」只能靠变量名约定。
        /// </remarks>
        public IReadOnlyList<Point2d> MeasurePoints { get; internal set; } = new List<Point2d>();

        /// <summary> 测量矩形姿态 </summary>
        public Angle MeasurePhi { get; internal set; }

        /// <summary> 测量矩形半长（Halcon Length1） </summary>
        public double MeasureLen1 { get; internal set; }

        /// <summary> 测量矩形半宽（Halcon Length2） </summary>
        public double MeasureLen2 { get; internal set; }

        /// <summary> 参与拟合的点（绿） </summary>
        public IReadOnlyList<Point2d> UsedPoints { get; internal set; } = new List<Point2d>();

        /// <summary> 被剔除的点（红） </summary>
        public IReadOnlyList<Point2d> RemovedPoints { get; internal set; } = new List<Point2d>();

        /// <summary> 圆弧中点（橙红），HasMidpoint 为 true 时有效 </summary>
        public Point2d Midpoint { get; internal set; }
        public bool HasMidpoint { get; internal set; }

        /// <summary> 结果文本（绿），拟合失败时为 null </summary>
        public string Message { get; internal set; }

        public int PointSize { get; internal set; }
        public int FontX { get; internal set; }
        public int FontY { get; internal set; }
        public int FontSize { get; internal set; }

        public bool ShowRegion { get; internal set; }
        public bool ShowFixRegion { get; internal set; }
        public bool ShowPoints { get; internal set; }
        public bool ShowResult { get; internal set; }
        public bool ShowText { get; internal set; }

        /// <summary>
        /// 写进叠加层（叠加层复制句柄，本对象随后即可释放）。
        /// 只写已生成的元素，拟合中途失败时呈现部分数据便于排查。
        /// </summary>
        public void DrawTo(IOverlay overlay)
        {
            if (ShowRegion && SearchRegion != null)
            {
                overlay.Add(SearchRegion, DrawStyle.Of(HColor.Blue));
            }

            if (ShowFixRegion)
            {
                foreach (Point2d center in MeasurePoints)
                {
                    overlay.AddRect2(center, MeasurePhi.Radians, MeasureLen1, MeasureLen2, DrawStyle.Of(HColor.Blue));
                }
            }

            if (ShowPoints)
            {
                foreach (Point2d pt in RemovedPoints)
                {
                    overlay.Add(pt, DrawStyle.Of(HColor.Red, PointSize));
                }
                foreach (Point2d pt in UsedPoints)
                {
                    overlay.Add(pt, DrawStyle.Of(HColor.Green, PointSize));
                }
            }

            if (ShowResult)
            {
                if (ArcContour != null) overlay.Add(ArcContour, DrawStyle.Of(HColor.Red));
                if (HasMidpoint) overlay.Add(Midpoint, DrawStyle.Of(HColor.OrangeRed, PointSize + 50));
            }

            if (ShowText && !string.IsNullOrEmpty(Message))
            {
                overlay.Text(Message, new Point2d(FontX, FontY), DrawStyle.Of(HColor.Green, FontSize));
            }
        }

        public void Dispose()
        {
            SearchRegion?.Dispose();
            ArcContour?.Dispose();
            SearchRegion = null;
            ArcContour = null;
        }
    }

}
