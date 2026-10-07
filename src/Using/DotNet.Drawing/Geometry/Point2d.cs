using System;
using System.Runtime.CompilerServices;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>
    /// 表示二维空间中的点
    /// </summary>
    /// <remarks>不可变值类型；X、Y 使用 init 访问器，相等性按几何容差比较。</remarks>
    public readonly struct Point2d : IEquatable<Point2d>
    {
        /// <summary>X坐标</summary>
        public double X { get; init; }

        /// <summary>Y坐标</summary>
        public double Y { get; init; }

        /// <summary>用 X、Y 坐标构造点。</summary>
        /// <param name="x">X 坐标</param>
        /// <param name="y">Y 坐标</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Point2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        #region Operators

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator +(Point2d p1, Point2d p2) => new(p1.X + p2.X, p1.Y + p2.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator -(Point2d p1, Point2d p2) => new(p1.X - p2.X, p1.Y - p2.Y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator *(Point2d p, double scalar) => new(p.X * scalar, p.Y * scalar);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator *(double scalar, Point2d p) => p * scalar;

        /// <summary>点除以标量；仅当标量恰为零时抛出异常。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator /(Point2d p, double scalar)
        {
            if (scalar == 0)
                throw new DivideByZeroException("Cannot divide by zero.");
            return new Point2d(p.X / scalar, p.Y / scalar);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point2d operator -(Point2d p) => new(-p.X, -p.Y);

        #endregion

        #region Equality

        /// <summary>使用几何容差的相等性比较</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Point2d other) =>
            MathHelper.AreEqualGeometric(X, other.X) && MathHelper.AreEqualGeometric(Y, other.Y);

        public override bool Equals(object obj) => obj is Point2d other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(
            MathHelper.QuantizeGeometric(X),
            MathHelper.QuantizeGeometric(Y));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Point2d left, Point2d right) => left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Point2d left, Point2d right) => !left.Equals(right);

        #endregion

        /// <summary>格式化为 "(X, Y)"；CvLine / CvArrow / CvCircle 的 ToString 依赖此输出。</summary>
        public override string ToString() => $"({X:G6}, {Y:G6})";
    }
}
