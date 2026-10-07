using System;
using System.Runtime.CompilerServices;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>
    /// 表示圆或圆弧
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - sealed record class: 不可变引用类型，线程安全
    /// - 自动支持 with 表达式进行函数式更新
    /// - 支持完整圆和圆弧
    /// </remarks>
    public sealed record CvCircle
    {
        #region Properties

        /// <summary>
        /// 圆心
        /// </summary>
        public Point2d Center { get; init; }

        private readonly double _radius;

        /// <summary>
        /// 半径
        /// </summary>
        /// <remarks>
        /// 校验放在 init 访问器里而不是只放在构造函数里：
        /// <c>circle with { Radius = -1 }</c> 不经过任何构造函数，只会走到这里。
        /// </remarks>
        public double Radius
        {
            get => _radius;
            init
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Radius), "Radius must be non-negative.");
                _radius = value;
            }
        }

        /// <summary>
        /// 开始角度（弧度）
        /// </summary>
        public double StartPhi { get; init; }

        /// <summary>
        /// 结束角度（弧度）
        /// </summary>
        public double EndPhi { get; init; }

        /// <summary>
        /// 是否为完整圆
        /// </summary>
        public bool IsFullCircle
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathHelper.AreEqual(StartPhi, EndPhi) ||
                   MathHelper.AreEqual(Math.Abs(EndPhi - StartPhi), 2 * Math.PI);
        }

        /// <summary>
        /// 是否为圆弧
        /// </summary>
        public bool IsArc
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsFullCircle;
        }

        /// <summary>
        /// 圆的周长
        /// </summary>
        public double Circumference
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2 * Math.PI * Radius;
        }

        /// <summary>
        /// 圆的面积
        /// </summary>
        public double Area
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.PI * Radius * Radius;
        }

        /// <summary>
        /// 圆弧的长度
        /// </summary>
        public double ArcLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Radius * Math.Abs(EndPhi - StartPhi);
        }

        /// <summary>
        /// 圆弧角度跨度（弧度）
        /// </summary>
        public double ArcSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Math.Abs(EndPhi - StartPhi);
        }

        /// <summary>
        /// 圆的直径
        /// </summary>
        public double Diameter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => 2 * Radius;
        }

        /// <summary>
        /// 圆弧起点
        /// </summary>
        public Point2d StartPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => PointAtAngle(StartPhi);
        }

        /// <summary>
        /// 圆弧终点
        /// </summary>
        public Point2d EndPoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => PointAtAngle(EndPhi);
        }

        /// <summary>
        /// 是否为退化圆（半径为零）
        /// </summary>
        public bool IsDegenerate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathHelper.IsZeroGeometric(Radius);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 构造完整圆
        /// </summary>
        public CvCircle(double x, double y, double radius)
        {
            Center = new Point2d(x, y);
            Radius = radius;
            StartPhi = 0;
            EndPhi = 2 * Math.PI;
        }

        /// <summary>
        /// 构造完整圆
        /// </summary>
        public CvCircle(Point2d center, double radius)
        {
            Center = center;
            Radius = radius;
            StartPhi = 0;
            EndPhi = 2 * Math.PI;
        }

        /// <summary>
        /// 构造圆弧
        /// </summary>
        public CvCircle(double x, double y, double radius, double startPhi, double endPhi)
        {
            Center = new Point2d(x, y);
            Radius = radius;
            StartPhi = startPhi;
            EndPhi = endPhi;
        }

        /// <summary>
        /// 构造圆弧
        /// </summary>
        public CvCircle(Point2d center, double radius, double startPhi, double endPhi)
        {
            Center = center;
            Radius = radius;
            StartPhi = startPhi;
            EndPhi = endPhi;
        }

        /// <summary>
        /// 从两点构造圆（第一个点为圆心，第二个点在圆周上）
        /// </summary>
        public CvCircle(Point2d centerPoint, Point2d edgePoint)
        {
            Center = centerPoint;
            Radius = Distance(centerPoint, edgePoint);
            StartPhi = 0;
            EndPhi = 2 * Math.PI;
        }

        /// <summary>
        /// 从三点构造圆（三点确定一个圆）
        /// </summary>
        public static CvCircle FromThreePoints(Point2d p1, Point2d p2, Point2d p3)
        {
            // 以 p1 为局部原点计算，避免判定阈值和中间平方项随整体坐标平移而变化。
            double ux = p2.X - p1.X;
            double uy = p2.Y - p1.Y;
            double vx = p3.X - p1.X;
            double vy = p3.Y - p1.Y;

            double cross = ux * vy - uy * vx;
            double scale = Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy);
            if (MathHelper.IsZeroRelative(cross, scale))
                return null; // 三点共线或近似共线

            double denominator = 2 * cross;
            double uLengthSquared = ux * ux + uy * uy;
            double vLengthSquared = vx * vx + vy * vy;
            double offsetX = (vy * uLengthSquared - uy * vLengthSquared) / denominator;
            double offsetY = (ux * vLengthSquared - vx * uLengthSquared) / denominator;

            var center = new Point2d(p1.X + offsetX, p1.Y + offsetY);
            double radius = Distance(center, p1);
            return new CvCircle(center, radius);
        }

        #endregion

        #region Point2d Methods

        /// <summary>
        /// 获取圆周上指定角度的点
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Point2d PointAtAngle(double phi)
        {
            return new Point2d(
                Center.X + Radius * Math.Cos(phi),
                Center.Y + Radius * Math.Sin(phi)
            );
        }

        #endregion

        private static double Distance(Point2d a, Point2d b) => MathHelper.Distance(a.X, a.Y, b.X, b.Y);

        #region Equality

        /// <summary>
        /// 使用容差的相等性比较
        /// </summary>
        public bool Equals(CvCircle other)
        {
            if (other is null) return false;
            return Center.Equals(other.Center) &&
                   MathHelper.AreEqualGeometric(Radius, other.Radius) &&
                   MathHelper.AreEqualQuantized(StartPhi, other.StartPhi) &&
                   MathHelper.AreEqualQuantized(EndPhi, other.EndPhi);
        }

        public override int GetHashCode() => HashCode.Combine(
            Center,
            MathHelper.QuantizeGeometric(Radius),
            MathHelper.QuantizeToTolerance(StartPhi),
            MathHelper.QuantizeToTolerance(EndPhi));

        #endregion

        #region Formatting

        public override string ToString()
        {
            if (IsFullCircle)
                return $"Circle[Center={Center}, Radius={Radius:G6}]";
            else
                return $"Arc[Center={Center}, Radius={Radius:G6}, φ=[{StartPhi:F3}, {EndPhi:F3}]]";
        }

        #endregion

        #region Static Members

        /// <summary>
        /// 单位圆
        /// </summary>
        public static readonly CvCircle Unit = new(default, 1);

        #endregion
    }
}
