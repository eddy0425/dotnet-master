using System;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 交互绘图的纯几何计算与命中测试。
    /// </summary>
    /// <remarks>
    /// 全部为无副作用的静态函数：不碰窗口、不碰 Halcon，可以独立单元测试。
    /// 坐标一律是图像坐标系下的 (x = column, y = row)。
    /// </remarks>
    internal static class DrawGeometry
    {
        /// <summary>控制点命中半径 (屏幕像素)，实际比较时会乘以窗口缩放比例。</summary>
        internal const double NearThreshold = 10;

        internal static double Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>把任意两个对角点整理成 左/上/右/下。</summary>
        internal static void NormalizeRect1(double col1, double row1, double col2, double row2,
            out double left, out double top, out double right, out double bottom)
        {
            left = Math.Min(col1, col2);
            top = Math.Min(row1, row2);
            right = Math.Max(col1, col2);
            bottom = Math.Max(row1, row2);
        }

        /// <summary>
        /// 命中测试：判断 (x2,y2) 是否落在 (x1,y1) 的控制点热区内。
        /// </summary>
        /// <param name="pixelSize">窗口缩放比例 (图像像素 / 屏幕像素)，由 <see cref="DrawRenderer.PixelSize"/> 提供。</param>
        internal static bool IsNear(double pixelSize, double x1, double y1, double x2, double y2)
        {
            double threshold = NearThreshold * pixelSize;
            double dx = x1 - x2, dy = y1 - y2;
            return dx * dx + dy * dy < threshold * threshold;
        }

        /// <summary> 沿 phi 方向的端点: (cos(phi), -sin(phi)) </summary>
        internal static void AxisEnd(double cx, double cy, double phi, double length,
            out double ex, out double ey)
        {
            ex = cx + length * Math.Cos(phi);
            ey = cy - length * Math.Sin(phi);
        }

        /// <summary> 垂直于 phi 的端点: (-sin(phi), -cos(phi)) </summary>
        internal static void AxisEndPerp(double cx, double cy, double phi, double length,
            out double ex, out double ey)
        {
            ex = cx - length * Math.Sin(phi);
            ey = cy - length * Math.Cos(phi);
        }
    }
}
