using Newtonsoft.Json;
using System;
using System.Runtime.CompilerServices;
using DotNet.Drawing.Internal;

namespace DotNet.Drawing
{
    /// <summary>
    /// 表示坐标系（包含位置和角度）
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - readonly struct: 不可变值类型，天生线程安全，零GC分配
    /// - 用于表示物体的位置和朝向（位姿）
    /// - 属性使用 init 访问器，保证不可变语义
    /// </remarks>
    public readonly struct CvCoord : IEquatable<CvCoord>
    {
        #region Properties

        /// <summary>
        /// X坐标
        /// </summary>
        public double X { get; init; }

        /// <summary>
        /// Y坐标
        /// </summary>
        public double Y { get; init; }

        private readonly Angle _angle;

        /// <summary>
        /// 朝向角
        /// </summary>
        /// <remarks>
        /// 类型是 <see cref="DotNet.Drawing.Angle"/> 而非裸 <c>double</c>：历史上这里是弧度，
        /// 但调用方屡屡再补一次 <c>ToRadians()</c>（审查项 B5），编译器无从发现。
        /// 现在取值必须显式写 <c>.Radians</c> 或 <c>.Degrees</c>，单位由类型保证。
        /// JSON 落盘形状不变，仍是一个弧度数字（见 <see cref="AngleJsonConverter"/>）。
        /// <para>
        /// 归一化到 [-π, π) 放在 init 访问器里：<c>coord with { Angle = ... }</c> 与 JSON 反序列化
        /// 都不经过构造函数，否则 2π 与 0 会被判为不同朝向。
        /// </para>
        /// </remarks>
        public Angle Angle
        {
            get => _angle;
            init => _angle = value.Normalized;
        }

        /// <summary>
        /// 角度（度数）
        /// </summary>
        /// <remarks>由 Angle 推导, 只读; 落盘会写出却无法读回, 标 JsonIgnore 免得污染 job 文件。</remarks>
        [JsonIgnore]
        public double AngleDegrees
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Angle.Degrees;
        }

        /// <summary>
        /// 中心点
        /// </summary>
        /// <remarks>由 X / Y 推导, 只读; 落盘会写出却无法读回, 标 JsonIgnore 免得污染 job 文件。</remarks>
        [JsonIgnore]
        public Point2d Center
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(X, Y);
        }

        /// <summary>
        /// 单位方向向量
        /// </summary>
        /// <remarks>由 Angle 推导, 只读; 落盘会写出却无法读回, 标 JsonIgnore 免得污染 job 文件。</remarks>
        [JsonIgnore]
        public Point2d Direction
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Angle.Direction;
        }

        /// <summary>
        /// 是否为单位坐标系（位于原点且无旋转）
        /// </summary>
        /// <remarks>由 X / Y / Angle 推导, 只读; 落盘会写出却无法读回, 标 JsonIgnore 免得污染 job 文件。</remarks>
        [JsonIgnore]
        public bool IsIdentity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            // X/Y 是像素量走几何容差；Angle 是弧度，属纯数值恒等判定，保留严格容差。
            get => MathHelper.IsZeroGeometric(X) && MathHelper.IsZeroGeometric(Y) && MathHelper.IsZero(Angle.Radians);
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="angle">朝向角，缺省为零角</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CvCoord(double x, double y, Angle angle = default)
        {
            X = x;
            Y = y;
            _angle = angle.Normalized;
        }

        /// <summary>
        /// 从点和角度构造
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="angle">朝向角，缺省为零角</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public CvCoord(Point2d center, Angle angle = default)
        {
            X = center.X;
            Y = center.Y;
            _angle = angle.Normalized;
        }

        /// <summary>
        /// 从弧度角度创建坐标系
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CvCoord FromRadians(double x, double y, double angleRadians)
        {
            return new CvCoord(x, y, Angle.FromRadians(angleRadians));
        }

        /// <summary>
        /// 从度数角度创建坐标系
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static CvCoord FromDegrees(double x, double y, double angleDegrees)
        {
            return new CvCoord(x, y, Angle.FromDegrees(angleDegrees));
        }

        #endregion

        #region Equality

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(CvCoord other)
        {
            return MathHelper.AreEqualGeometric(X, other.X) &&
                   MathHelper.AreEqualGeometric(Y, other.Y) &&
                   Angle.Equals(other.Angle);
        }

        public override bool Equals(object obj) => obj is CvCoord other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(
            MathHelper.QuantizeGeometric(X),
            MathHelper.QuantizeGeometric(Y),
            MathHelper.QuantizeToTolerance(Angle.Radians));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(CvCoord left, CvCoord right) => left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(CvCoord left, CvCoord right) => !left.Equals(right);

        #endregion

        #region Formatting

        public override string ToString()
        {
            return $"Coord[({X:G6}, {Y:G6}), {Angle:F4}rad ({AngleDegrees:F2}°)]";
        }

        /// <summary>
        /// 格式化输出
        /// </summary>
        public string ToString(string format)
        {
            return $"Coord[({X.ToString(format)}, {Y.ToString(format)}), {Angle.ToString(format)}rad]";
        }

        #endregion

        #region Static Members

        /// <summary>
        /// 单位坐标系常量（原点，无旋转）
        /// </summary>
        public static readonly CvCoord Identity = new(0, 0, Angle.Zero);

        /// <summary>
        /// 零坐标系常量（同 Identity）
        /// </summary>
        public static readonly CvCoord Zero = Identity;

        #endregion
    }
}

