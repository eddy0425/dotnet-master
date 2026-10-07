using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace DotNet.Drawing
{
    /// <summary>
    /// 强类型角度：内部恒以弧度存储，单位只能通过命名工厂进出。
    /// </summary>
    /// <remarks>
    /// 引入动机（对应审查项 B5）：工程里曾出现四处 <c>coord.Angle.ToRadians()</c> ——
    /// <c>CvCoord.Angle</c> 本就是弧度，再乘一次 π/180 会让角度缩小约 57 倍，
    /// 而两端都是 <c>double</c>，编译器无从察觉。把「角度」提升为独立类型后，
    /// 弧度与度数不再能互相赋值，这类单位混淆在编译期即被挡住。
    /// <para>
    /// 契约：
    /// - <see cref="Radians"/> / <see cref="Degrees"/> 是仅有的两个取值出口，命名即单位；
    /// - 构造只能经 <see cref="FromRadians"/> / <see cref="FromDegrees"/>，没有 <c>double</c> 隐式转换；
    /// - 不在构造时归一化（保留调用方给的圈数信息），需要时显式取 <see cref="Normalized"/>；
    /// - 相等性沿用 <see cref="MathHelper"/> 的容差口径，与 <see cref="CvCoord"/> 保持一致。
    /// </para>
    /// <para>
    /// 序列化：<see cref="AngleJsonConverter"/> 让它在 JSON 中仍是一个「弧度数字」，
    /// 与改造前 <c>double Angle</c> 的落盘形状完全相同，历史配置文件可直接读回。
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(AngleJsonConverter))]
    public readonly struct Angle : IEquatable<Angle>, IComparable<Angle>, IFormattable
    {
        /// <summary> 弧度值 </summary>
        public double Radians { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Angle(double radians)
        {
            Radians = radians;
        }

        /// <summary> 度数值 </summary>
        public double Degrees
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => MathHelper.ToDegrees(Radians);
        }

        /// <summary> 零角 </summary>
        public static readonly Angle Zero = default;

        /// <summary> 由弧度构造 </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle FromRadians(double radians) => new Angle(radians);

        /// <summary> 由度数构造 </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle FromDegrees(double degrees) => new Angle(MathHelper.ToRadians(degrees));

        /// <summary> 归一化到 [-π, π) </summary>
        public Angle Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Angle(MathHelper.NormalizeAngle(Radians));
        }

        /// <summary> 归一化到 [0, 2π) </summary>
        public Angle NormalizedPositive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Angle(MathHelper.NormalizeAnglePositive(Radians));
        }

        /// <summary> 是否为有限值（Halcon 拟合失败会返回 NaN，需由调用方判定） </summary>
        public bool IsFinite
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !double.IsNaN(Radians) && !double.IsInfinity(Radians);
        }

        /// <summary> 单位方向向量 </summary>
        public Point2d Direction
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Point2d(Math.Cos(Radians), Math.Sin(Radians));
        }

        /// <summary> 到另一角度的最短差值（结果落在 [-π, π)） </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Angle DifferenceTo(Angle other) => new Angle(MathHelper.AngleDifference(Radians, other.Radians));

        #region Operators

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator +(Angle a, Angle b) => new Angle(a.Radians + b.Radians);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator -(Angle a, Angle b) => new Angle(a.Radians - b.Radians);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator -(Angle a) => new Angle(-a.Radians);

        /// <summary> 角度按标量缩放（插值、取半弧等） </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator *(Angle a, double scale) => new Angle(a.Radians * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator *(double scale, Angle a) => new Angle(a.Radians * scale);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Angle operator /(Angle a, double divisor) => new Angle(a.Radians / divisor);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Angle left, Angle right) => left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Angle left, Angle right) => !left.Equals(right);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(Angle left, Angle right) => left.CompareTo(right) < 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(Angle left, Angle right) => left.CompareTo(right) > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(Angle left, Angle right) => left.CompareTo(right) <= 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(Angle left, Angle right) => left.CompareTo(right) >= 0;

        #endregion

        #region Equality

        /// <remarks>
        /// 角度不是像素量，因此按纯数值容差网格判等；该规则与
        /// <see cref="GetHashCode"/> 使用完全相同的量化结果，满足哈希集合契约。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private double QuantizedRadians() => MathHelper.QuantizeToTolerance(Radians);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(Angle other) => QuantizedRadians().Equals(other.QuantizedRadians());

        public override bool Equals(object obj) => obj is Angle other && Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => QuantizedRadians().GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(Angle other) => QuantizedRadians().CompareTo(other.QuantizedRadians());

        #endregion

        #region Formatting

        public override string ToString() => $"{Radians:F4}rad ({Degrees:F2}°)";

        /// <remarks>
        /// 单参数格式化按弧度输出，与改造前 <c>double Angle</c> 的 <c>ToString(format)</c> 行为一致；
        /// 需要度数请显式写 <c>angle.Degrees.ToString(format)</c>。
        /// </remarks>
        public string ToString(string format) => Radians.ToString(format, CultureInfo.CurrentCulture);

        public string ToString(string format, IFormatProvider formatProvider)
            => Radians.ToString(format, formatProvider);

        #endregion
    }

    /// <summary>
    /// 让 <see cref="Angle"/> 在 JSON 中表现为一个弧度数字，保持与历史 <c>double</c> 字段的兼容。
    /// </summary>
    /// <remarks>
    /// 读取时兼容：裸数字（弧度，历史格式）、字符串数字、<c>{"Radians": x}</c>、<c>{"Degrees": x}</c>；
    /// 写入时恒为裸数字，因此新代码存出的配置仍能被旧版本读回。
    /// </remarks>
    public sealed class AngleJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
            => objectType == typeof(Angle) || Nullable.GetUnderlyingType(objectType) == typeof(Angle);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Angle angle) writer.WriteValue(angle.Radians);
            else writer.WriteNull();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    if (Nullable.GetUnderlyingType(objectType) == typeof(Angle)) return null;
                    throw new JsonSerializationException("非空角度字段不能为 null。");

                case JsonToken.Integer:
                case JsonToken.Float:
                    return Angle.FromRadians(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture));

                case JsonToken.String:
                    {
                        string text = (string)reader.Value;
                        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                            return Angle.FromRadians(parsed);
                        throw new JsonSerializationException($"无法把 '{text}' 解析为角度（弧度）。");
                    }

                case JsonToken.StartObject:
                    {
                        double radians = 0;
                        bool found = false;
                        while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                        {
                            if (reader.TokenType != JsonToken.PropertyName) continue;

                            string name = (string)reader.Value;
                            if (!reader.Read())
                                throw new JsonSerializationException("角度对象意外结束。");

                            if (string.Equals(name, nameof(Angle.Radians), StringComparison.OrdinalIgnoreCase))
                            {
                                if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float)
                                    throw new JsonSerializationException("Radians 必须是数值。");

                                radians = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                                found = true;
                            }
                            else if (!found && string.Equals(name, nameof(Angle.Degrees), StringComparison.OrdinalIgnoreCase))
                            {
                                if (reader.TokenType != JsonToken.Integer && reader.TokenType != JsonToken.Float)
                                    throw new JsonSerializationException("Degrees 必须是数值。");

                                radians = MathHelper.ToRadians(Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture));
                                found = true;
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }

                        if (!found)
                            throw new JsonSerializationException("角度对象必须包含 Radians 或 Degrees 字段。");

                        return Angle.FromRadians(radians);
                    }

                default:
                    throw new JsonSerializationException($"角度字段遇到无法处理的 JSON 记号：{reader.TokenType}。");
            }
        }
    }
}
