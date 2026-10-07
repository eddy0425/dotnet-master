using System;
using System.Runtime.CompilerServices;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>
    /// 表示线段
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - sealed record class: 不可变引用类型，线程安全
    /// - 自动支持 with 表达式进行函数式更新
    /// </remarks>
    public sealed record CvLine
    {
        #region Properties

        /// <summary>
        /// 起点
        /// </summary>
        public Point2d Start { get; init; }

        /// <summary>
        /// 终点
        /// </summary>
        public Point2d End { get; init; }

        /// <summary>
        /// 线段长度
        /// </summary>
        public double Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Sqrt(LengthSquared);
        }

        /// <summary>
        /// 线段长度平方（避免开方，用于比较）
        /// </summary>
        public double LengthSquared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var dir = Direction;
                return dir.X * dir.X + dir.Y * dir.Y;
            }
        }

        /// <summary>
        /// 线段角度（弧度）
        /// </summary>
        public double Angle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Atan2(End.Y - Start.Y, End.X - Start.X);
        }

        /// <summary>
        /// 线段角度（度数）
        /// </summary>
        public double AngleDegrees
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Angle * 180.0 / Math.PI;
        }

        /// <summary>
        /// 中点
        /// </summary>
        public Point2d MidPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new((Start.X + End.X) / 2, (Start.Y + End.Y) / 2);
        }

        /// <summary>
        /// 方向向量（从起点到终点）
        /// </summary>
        public Point2d Direction
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => End - Start;
        }

        /// <summary>
        /// 是否为退化线段（长度为零）
        /// </summary>
        public bool IsDegenerate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // 必须在同一量纲上比较：LengthSquared 与 PixelTolerance 的平方比较，
            // 等价于"线段长度不足 0.01 像素"。原先拿长度平方与 1e-9 比，实际阈值是 3e-5 像素。
            get => LengthSquared < MathHelper.PixelTolerance * MathHelper.PixelTolerance;
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 从两点构造线段
        /// </summary>
        public CvLine(Point2d start, Point2d end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// 从坐标构造线段
        /// </summary>
        public CvLine(double startX, double startY, double endX, double endY)
        {
            Start = new Point2d(startX, startY);
            End = new Point2d(endX, endY);
        }

        /// <summary>
        /// 从起点、角度和长度构造线段
        /// </summary>
        public static CvLine FromAngle(Point2d start, double angle, double length)
        {
            var end = new Point2d(
                start.X + length * Math.Cos(angle),
                start.Y + length * Math.Sin(angle)
            );
            return new CvLine(start, end);
        }

        /// <summary>
        /// 从中点、角度和长度构造线段
        /// </summary>
        public static CvLine FromCenterAngle(Point2d center, double angle, double length)
        {
            double halfLen = length / 2;
            var start = new Point2d(
                center.X - halfLen * Math.Cos(angle),
                center.Y - halfLen * Math.Sin(angle)
            );
            var end = new Point2d(
                center.X + halfLen * Math.Cos(angle),
                center.Y + halfLen * Math.Sin(angle)
            );
            return new CvLine(start, end);
        }

        #endregion

        internal static double Distance(Point2d a, Point2d b) => MathHelper.Distance(a.X, a.Y, b.X, b.Y);

        #region Equality

        /// <summary>
        /// 使用容差的相等性比较
        /// </summary>
        public bool Equals(CvLine other)
        {
            if (other is null) return false;
            return Start.Equals(other.Start) && End.Equals(other.End);
        }

        public override int GetHashCode() => HashCode.Combine(Start, End);

        #endregion

        #region Formatting

        public override string ToString()
        {
            return $"Line[{Start} → {End}, Length={Length:G6}]";
        }

        #endregion
    }
}
