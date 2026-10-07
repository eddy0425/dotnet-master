using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    /// <summary>
    /// 数值断言辅助。
    /// </summary>
    /// <remarks>
    /// 计算结果一律按分量带容差比较，不用 <c>==</c>：<see cref="Point2d"/> / <see cref="Rect2d"/> 的判等
    /// 是 0.01 像素网格上的量化比较，<see cref="Angle"/> 是 1e-9 网格，
    /// 计算值恰好落在网格边界两侧时 <c>==</c> 会给出与直觉相反的结果，测试会变得不稳定。
    /// 判等语义本身由专门的用例覆盖。
    /// </remarks>
    internal static class Geom
    {
        public const double Eps = 1e-9;

        public static void AreClose(double expected, double actual, string what = null)
            => Assert.AreEqual(expected, actual, Eps, what);

        public static void AreClose(Point2d expected, Point2d actual, double eps = Eps)
        {
            Assert.AreEqual(expected.X, actual.X, eps, $"X: expected {expected}, actual {actual}");
            Assert.AreEqual(expected.Y, actual.Y, eps, $"Y: expected {expected}, actual {actual}");
        }

        public static void AreClose(double x, double y, Point2d actual, double eps = Eps)
            => AreClose(new Point2d(x, y), actual, eps);

        public static void AreClose(double x, double y, double width, double height, Rect2d actual, double eps = Eps)
        {
            Assert.AreEqual(x, actual.X, eps, $"X of {actual}");
            Assert.AreEqual(y, actual.Y, eps, $"Y of {actual}");
            Assert.AreEqual(width, actual.Width, eps, $"Width of {actual}");
            Assert.AreEqual(height, actual.Height, eps, $"Height of {actual}");
        }
    }
}
