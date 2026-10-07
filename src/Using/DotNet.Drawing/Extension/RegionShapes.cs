using System;
using HalconDotNet;

namespace DotNet.Drawing
{
    /// <summary>
    /// 由几何参数生成 HALCON 区域。
    /// </summary>
    /// <remarks>
    /// 圆环原来有三份实现（<c>RebuildRegion</c>、交互绘制、按几何画轮廓），内外半径的归一化各写各的；收拢到这里。
    /// </remarks>
    public static class RegionShapes
    {
        /// <summary>
        /// 同心圆环（外圆减内圆）。内外半径顺序不敏感，自动取大者为外圆。返回的区域归调用方所有。
        /// </summary>
        public static HObject GenRing(double row, double column, double radiusA, double radiusB)
        {
            double outer = Math.Max(radiusA, radiusB);
            double inner = Math.Min(radiusA, radiusB);

            HObject outerCircle = null;
            HObject innerCircle = null;
            try
            {
                HOperatorSet.GenCircle(out outerCircle, row, column, outer);
                HOperatorSet.GenCircle(out innerCircle, row, column, inner);
                HOperatorSet.Difference(outerCircle, innerCircle, out HObject ring);
                return ring;
            }
            finally
            {
                outerCircle?.Dispose();
                innerCircle?.Dispose();
            }
        }
    }
}
