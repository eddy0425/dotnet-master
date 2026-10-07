using HalconDotNet;
using Newtonsoft.Json;
using System;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>表示不可变的双精度矩形，支持 JSON 序列化。</summary>
    /// <remarks>四个几何分量只在构造时赋值；宽、高必须为非负值，相等性按几何容差比较。</remarks>
    [Serializable]
    public class Rect2d : IEquatable<Rect2d>
    {
        /// <summary>左上角 X 坐标</summary>
        public double X { get; }

        /// <summary>左上角 Y 坐标</summary>
        public double Y { get; }

        /// <summary>非负宽度</summary>
        public double Width { get; }

        /// <summary>非负高度</summary>
        public double Height { get; }

        /// <summary>构造位于原点的空矩形。</summary>
        public Rect2d() : this(0.0, 0.0, 0.0, 0.0) { }

        /// <summary>从左上角位置和尺寸构造矩形。</summary>
        public Rect2d(Point2d location, Size2d size) : this(location.X, location.Y, size.Width, size.Height) { }

        /// <summary>用坐标和尺寸构造矩形，并校验宽、高非负。</summary>
        [JsonConstructor]
        public Rect2d(double x, double y, double width, double height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>从 Halcon 的上下左右边界构造矩形。</summary>
        /// <param name="row1">上边界行坐标</param>
        /// <param name="column1">左边界列坐标</param>
        /// <param name="row2">下边界行坐标</param>
        /// <param name="column2">右边界列坐标</param>
        public Rect2d(HTuple row1, HTuple column1, HTuple row2, HTuple column2)
        {
            if (row1 == null) throw new ArgumentNullException(nameof(row1));
            if (column1 == null) throw new ArgumentNullException(nameof(column1));
            if (row2 == null) throw new ArgumentNullException(nameof(row2));
            if (column2 == null) throw new ArgumentNullException(nameof(column2));
            X = column1.D;
            Y = row1.D;
            Width = column2.D - column1.D;
            Height = row2.D - row1.D;
            if (Width < 0 || Height < 0)
                throw new ArgumentOutOfRangeException(Width < 0 ? nameof(column2) : nameof(row2),
                    "Width and Height must be non-negative.");
        }

        /// <summary>按点的分量平移矩形。</summary>
        public static Rect2d operator +(Rect2d rect, Point2d point) => new(rect.X + point.X, rect.Y + point.Y, rect.Width, rect.Height);
        /// <summary>按点的分量反向平移矩形。</summary>
        public static Rect2d operator -(Rect2d rect, Point2d point) => new(rect.X - point.X, rect.Y - point.Y, rect.Width, rect.Height);
        /// <summary>增加矩形的宽、高，左上角不变。</summary>
        public static Rect2d operator +(Rect2d rect, Size2d size) => new(rect.X, rect.Y, rect.Width + size.Width, rect.Height + size.Height);
        /// <summary>减少矩形的宽、高，负的分量截断为零（与 <see cref="Size2d"/> 相减一致）。</summary>
        public static Rect2d operator -(Rect2d rect, Size2d size) =>
            new(rect.X, rect.Y, Math.Max(0, rect.Width - size.Width), Math.Max(0, rect.Height - size.Height));

        /// <summary>使用几何容差比较四个矩形分量。</summary>
        public bool Equals(Rect2d other) =>
            other is not null && (ReferenceEquals(this, other) ||
            (MathHelper.AreEqualGeometric(X, other.X) && MathHelper.AreEqualGeometric(Y, other.Y) &&
             MathHelper.AreEqualGeometric(Width, other.Width) && MathHelper.AreEqualGeometric(Height, other.Height)));
        public override bool Equals(object obj) => obj is Rect2d other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            MathHelper.QuantizeGeometric(X), MathHelper.QuantizeGeometric(Y),
            MathHelper.QuantizeGeometric(Width), MathHelper.QuantizeGeometric(Height));
        public static bool operator ==(Rect2d left, Rect2d right) =>
            ReferenceEquals(left, right) || (left is not null && left.Equals(right));
        public static bool operator !=(Rect2d left, Rect2d right) => !(left == right);
    }
}
