using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;
using System.Collections.Generic;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// Map 相机的一帧显示内容：图像副本 + 可选的拟合叠加数据（教导/取像流程为 null）。
    /// 创建后不再修改，整帧随 Dispose 一次性释放。
    /// </summary>
    public sealed class FitArcMidpointRenderFrame : IDisposable
    {
        public HObject Image { get; }

        /// <summary> 拟合叠加数据；教导 / 取像流程没有拟合结果，此处为 null </summary>
        public FitArcMidpointRenderData Overlay { get; }

        private bool _disposed;

        private FitArcMidpointRenderFrame(HObject image, FitArcMidpointRenderData overlay)
        {
            Image = image;
            Overlay = overlay;
        }

        /// <summary>
        /// 从相机原图复制出显示帧，并接管 overlay 的所有权；复制失败时 overlay 一并释放。
        /// </summary>
        public static FitArcMidpointRenderFrame Create(HObject sourceImage, FitArcMidpointRenderData overlay)
        {
            HObject image = null;
            try
            {
                image = sourceImage.CopyObj(1, -1);
                return new FitArcMidpointRenderFrame(image, overlay);
            }
            catch
            {
                image?.Dispose();
                overlay?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            // 帧会被多个显示路径共享, 谁先用完谁 Dispose; 没有这个闸门就会对同一个句柄 Dispose 两次。
            if (_disposed) return;
            _disposed = true;

            Overlay?.Dispose();
            Image?.Dispose();
        }
    }

    /// <summary>
    /// 圆弧中点拟合的一帧显示数据：拟合线程构建并发布，发布后只读。
    /// 样式与可见性开关在拟合时从 inPara 捕获，因此可在任意线程独立于策略实例绘制。
    /// 其中的 HObject 归本对象所有，随 Dispose 释放（可重复调用）。
    /// </summary>
    /// <remarks>
    /// 拟合过程按阶段逐项填充（中途失败时保留已生成的部分便于排查），无法一次性构造，
    /// 因此属性的 setter 限定为 <c>internal</c>：只有本程序集内的拟合策略能写，
    /// 发布给外部（显示线程）后对方只能读。点集以 <see cref="IReadOnlyList{T}"/> 暴露。
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
        /// 唯一的叠加层绘制入口：编辑器同步显示与机台异步刷新共用。
        /// 只绘制已生成的元素，拟合中途失败时呈现部分数据便于排查。
        /// </summary>
        public void DrawTo(IHDisplay display)
        {
            if (ShowRegion && SearchRegion != null)
            {
                display.Disp(SearchRegion, DrawStyle.Of(HColor.Blue));
            }

            if (ShowFixRegion)
            {
                foreach (Point2d center in MeasurePoints)
                {
                    display.DispRect2(center, MeasurePhi.Radians, MeasureLen1, MeasureLen2, DrawStyle.Of(HColor.Blue));
                }
            }

            if (ShowPoints)
            {
                foreach (Point2d pt in RemovedPoints)
                {
                    display.Disp(pt, DrawStyle.Of(HColor.Red, PointSize));
                }
                foreach (Point2d pt in UsedPoints)
                {
                    display.Disp(pt, DrawStyle.Of(HColor.Green, PointSize));
                }
            }

            if (ShowResult)
            {
                if (ArcContour != null) display.Disp(ArcContour, DrawStyle.Of(HColor.Red));
                if (HasMidpoint) display.Disp(Midpoint, DrawStyle.Of(HColor.OrangeRed, PointSize + 50));
            }

            if (ShowText && !string.IsNullOrEmpty(Message))
            {
                display.DispText(Message, new Point2d(FontX, FontY), DrawStyle.Of(HColor.Green, FontSize));
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
