using System;
using System.Runtime.CompilerServices;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>
    /// 表示箭头（组合模式：包含线段 + 箭头头部样式）
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - sealed record class: 不可变引用类型，线程安全
    /// - 使用组合模式，将箭头视为线段的特化
    /// - 自动支持 with 表达式进行函数式更新
    /// </remarks>
    public sealed record CvArrow
    {
        #region Properties

        private readonly CvLine _line;

        /// <summary>
        /// 箭头的线段部分
        /// </summary>
        /// <remarks>
        /// 判空放在 init 访问器里：<c>arrow with { Line = null }</c> 不经过构造函数，
        /// 否则之后访问 Start/End/Length 或判等都会 NRE。
        /// </remarks>
        public CvLine Line
        {
            get => _line;
            init => _line = value ?? throw new ArgumentNullException(nameof(Line));
        }

        /// <summary>
        /// 箭头头部大小
        /// </summary>
        public double HeadSize { get; init; } = 10.0;

        /// <summary>
        /// 箭头头部角度（度数）
        /// </summary>
        public double HeadAngle { get; init; } = 30.0;
        /// <summary>
        /// 起点
        /// </summary>
        public Point2d Start => Line.Start;

        /// <summary>
        /// 终点
        /// </summary>
        public Point2d End => Line.End;

        /// <summary>
        /// 线段长度
        /// </summary>
        public double Length => Line.Length;

        /// <summary>
        /// 线段角度（弧度）
        /// </summary>
        public double Angle => Line.Angle;

        /// <summary>
        /// 线段角度（度数）
        /// </summary>
        public double AngleDegrees => Line.AngleDegrees;
        #endregion

        #region Constructors

        /// <summary>
        /// 从线段构造箭头
        /// </summary>
        public CvArrow(CvLine line, double headSize = 10.0, double headAngle = 30.0)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));
            Line = line;
            HeadSize = headSize;
            HeadAngle = headAngle;
        }

        /// <summary>
        /// 从两点构造箭头
        /// </summary>
        public CvArrow(Point2d start, Point2d end, double headSize = 10.0, double headAngle = 30.0)
        {
            Line = new CvLine(start, end);
            HeadSize = headSize;
            HeadAngle = headAngle;
        }

        /// <summary>
        /// 从坐标构造箭头
        /// </summary>
        public CvArrow(double startX, double startY, double endX, double endY, double headSize = 10.0, double headAngle = 30.0)
        {
            Line = new CvLine(startX, startY, endX, endY);
            HeadSize = headSize;
            HeadAngle = headAngle;
        }

        /// <summary>
        /// 从起点、角度和长度构造箭头
        /// </summary>
        public static CvArrow FromAngle(Point2d start, double angle, double length, double headSize = 10.0, double headAngle = 30.0)
        {
            return new CvArrow(CvLine.FromAngle(start, angle, length), headSize, headAngle);
        }

        /// <summary>
        /// 从中点、角度和长度构造箭头
        /// </summary>
        public static CvArrow FromCenterAngle(Point2d center, double angle, double length, double headSize = 10.0, double headAngle = 30.0)
        {
            return new CvArrow(CvLine.FromCenterAngle(center, angle, length), headSize, headAngle);
        }

        #endregion

        #region Equality

        /// <summary>
        /// 使用容差的相等性比较
        /// </summary>
        public bool Equals(CvArrow other)
        {
            if (other is null) return false;
            return Line.Equals(other.Line) &&
                   MathHelper.AreEqualGeometric(HeadSize, other.HeadSize) &&
                   MathHelper.AreEqualQuantized(HeadAngle, other.HeadAngle);
        }

        public override int GetHashCode() => HashCode.Combine(
            Line,
            MathHelper.QuantizeGeometric(HeadSize),
            MathHelper.QuantizeToTolerance(HeadAngle));

        #endregion

        #region Formatting

        public override string ToString()
        {
            return $"Arrow[{Start} → {End}, Length={Length:G6}, Head={HeadSize:G4}@{HeadAngle}°]";
        }

        #endregion
    }
}
