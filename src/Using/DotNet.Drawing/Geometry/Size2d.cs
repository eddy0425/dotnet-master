using Newtonsoft.Json;
using System;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>表示二维尺寸。</summary>
    /// <remarks>不可变值类型；宽、高在构造时必须为非负值，相等性按几何容差比较。</remarks>
    public readonly struct Size2d : IEquatable<Size2d>
    {
        /// <summary>宽度</summary>
        public double Width { get; }

        /// <summary>高度</summary>
        public double Height { get; }

        /// <summary>用宽、高构造尺寸。</summary>
        /// <param name="width">非负宽度</param>
        /// <param name="height">非负高度</param>
        [JsonConstructor]
        public Size2d(double width, double height)
        {
            if (width < 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be non-negative.");
            if (height < 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be non-negative.");
            Width = width;
            Height = height;
        }

        public static Size2d operator +(Size2d a, Size2d b) => new(a.Width + b.Width, a.Height + b.Height);
        /// <summary>尺寸相减，负的分量截断为零。</summary>
        public static Size2d operator -(Size2d a, Size2d b) => new(Math.Max(0, a.Width - b.Width), Math.Max(0, a.Height - b.Height));
        /// <summary>按非负标量缩放尺寸。</summary>
        public static Size2d operator *(Size2d size, double scalar)
        {
            if (scalar < 0) throw new ArgumentOutOfRangeException(nameof(scalar), "Scalar must be non-negative.");
            return new Size2d(size.Width * scalar, size.Height * scalar);
        }
        public static Size2d operator *(double scalar, Size2d size) => size * scalar;
        /// <summary>尺寸除以正标量；零值和负值分别抛出异常。</summary>
        public static Size2d operator /(Size2d size, double scalar)
        {
            if (scalar == 0) throw new DivideByZeroException("Cannot divide by zero.");
            if (scalar < 0) throw new ArgumentOutOfRangeException(nameof(scalar), "Scalar must be positive.");
            return new Size2d(size.Width / scalar, size.Height / scalar);
        }

        /// <summary>将宽、高作为 X、Y 分量转换为点。</summary>
        public static implicit operator Point2d(Size2d size) => new(size.Width, size.Height);

        /// <summary>使用几何容差比较宽、高。</summary>
        public bool Equals(Size2d other) =>
            MathHelper.AreEqualGeometric(Width, other.Width) && MathHelper.AreEqualGeometric(Height, other.Height);
        public override bool Equals(object obj) => obj is Size2d other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            MathHelper.QuantizeGeometric(Width), MathHelper.QuantizeGeometric(Height));
        public static bool operator ==(Size2d left, Size2d right) => left.Equals(right);
        public static bool operator !=(Size2d left, Size2d right) => !left.Equals(right);
    }
}
