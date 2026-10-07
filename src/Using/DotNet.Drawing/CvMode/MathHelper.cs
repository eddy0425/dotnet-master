using System;
using System.Runtime.CompilerServices;

namespace DotNet.Drawing
{
    /// <summary>
    /// 数学辅助类，提供浮点数比较、角度转换等工具方法
    /// </summary>
    /// <remarks>
    /// 设计特点：
    /// - 静态类，无状态，线程安全
    /// - 使用 AggressiveInlining 优化性能关键路径
    /// - 提供常用的数学常量和工具方法
    /// </remarks>
    internal static class MathHelper
    {
        #region Constants

        /// <summary>
        /// 数值恒等容差：用于判断"是否为同一个数"，例如判零、判等价参数。
        /// </summary>
        /// <remarks>
        /// 注意：本档 <b>远严于像素精度</b>（约为 1 像素的十亿分之一）。
        /// 涉及坐标、长度、半径等<b>几何量</b>的比较请改用 <see cref="PixelTolerance"/>；
        /// 量级敏感（结果量纲为坐标平方、叉积等）的判定请改用 <see cref="AreEqualRelative"/>。
        /// </remarks>
        internal const double Tolerance = 1e-9;

        /// <summary>
        /// 较宽松的容差：用于累积了多步浮点运算、但仍要求"数值上相同"的场景。
        /// </summary>
        internal const double LooseTolerance = 1e-6;

        /// <summary>
        /// 像素级容差：几何量（坐标 / 长度 / 半径 / 距离）比较的默认档位。
        /// </summary>
        internal const double PixelTolerance = 0.01;

        /// <summary>
        /// Pi 的两倍
        /// </summary>
        internal const double TwoPi = 2 * Math.PI;

        /// <summary>
        /// Pi 的一半
        /// </summary>
        internal const double HalfPi = Math.PI / 2;

        /// <summary>
        /// 弧度到度数的转换系数
        /// </summary>
        internal const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// 度数到弧度的转换系数
        /// </summary>
        internal const double DegToRad = Math.PI / 180.0;

        #endregion

        #region Equality Comparisons

        /// <summary>
        /// 判断两个浮点数是否近似相等
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreEqual(double a, double b)
        {
            return Math.Abs(a - b) < Tolerance;
        }

        /// <summary>
        /// 以<b>相对容差</b>判断两个浮点数是否近似相等
        /// </summary>
        /// <remarks>
        /// 适用于量级不确定的中间量（叉积、行列式等）：这类值的量纲是坐标的平方，
        /// 在 4000x3000 图像上轻易达到 1e7 量级，此时任何绝对容差都失去意义。
        /// 判据为 <c>|a-b| &lt;= tolerance * max(1, |a|, |b|)</c>，
        /// 其中 <c>max</c> 的 1 用于保证在两值都接近 0 时退化为绝对容差。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreEqualRelative(double a, double b, double tolerance = LooseTolerance)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Math.Abs(a - b) <= tolerance * scale;
        }

        /// <summary>
        /// 以<b>相对容差</b>判断浮点数是否近似为零（相对于给定的参考量级）
        /// </summary>
        /// <param name="value">待判定的值</param>
        /// <param name="scale">该值的期望量级，例如叉积判平行时传两向量长度之积</param>
        /// <param name="tolerance">相对容差</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsZeroRelative(double value, double scale, double tolerance = LooseTolerance)
        {
            return Math.Abs(value) <= tolerance * Math.Max(1.0, Math.Abs(scale));
        }

        /// <summary>
        /// 将两个几何量量化到像素容差网格后判断是否相等。
        /// </summary>
        /// <remarks>
        /// 适用对象：坐标、长度、半径、宽高等以像素为单位的量。
        /// 网格判等是等价关系，并与 <see cref="QuantizeGeometric"/> 生成的哈希分量严格一致；
        /// 避免使用 <c>|a-b| &lt; tolerance</c> 时因不具传递性、跨网格边界而破坏
        /// <c>Equals/GetHashCode</c> 契约。
        /// NaN 与 NaN 判等（与 <see cref="Angle.Equals(Angle)"/> 一致），保证含 NaN 的对象 <c>x.Equals(x)</c> 自反，
        /// 放进哈希集合后仍能查到。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreEqualGeometric(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.IsNaN(a) && double.IsNaN(b);
            return QuantizeGeometric(a).Equals(QuantizeGeometric(b));
        }

        /// <summary>
        /// 将两个值量化到 <see cref="Tolerance"/> 网格后判断是否相等。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="QuantizeToTolerance"/> 生成的哈希分量严格一致，用于判等必须与 1e-9 网格哈希配套的场景
        /// （弧度、以及 <see cref="CvRegion"/> 中沿用 1e-9 网格的半径 / 多边形坐标等）。
        /// 不能改用 <see cref="AreEqual"/>：|a-b| &lt; 1e-9 的两值可能跨越网格边界，判等为真而哈希不同。
        /// 修改调用处的判等网格时，必须同步修改对应的 GetHashCode。
        /// NaN 规则同 <see cref="AreEqualGeometric"/>。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool AreEqualQuantized(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.IsNaN(a) && double.IsNaN(b);
            return QuantizeToTolerance(a).Equals(QuantizeToTolerance(b));
        }

        /// <summary>
        /// 按几何量网格判等规则判断是否为零。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsZeroGeometric(double value)
        {
            return AreEqualGeometric(value, 0);
        }

        /// <summary>
        /// <see cref="AreEqualGeometric"/> 配套的哈希量化（像素级网格）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double QuantizeGeometric(double value)
        {
            return QuantizeToTolerance(value, PixelTolerance);
        }

        /// <summary>
        /// 判断浮点数是否近似为零
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsZero(double value)
        {
            return Math.Abs(value) < Tolerance;
        }

        /// <summary>
        /// 将浮点值量化到容差网格上，用于实现"容差版 GetHashCode"
        /// </summary>
        /// <remarks>
        /// 用法：当类型的 <c>Equals</c> 使用容差比较 (<see cref="AreEqual(double,double)"/>)
        /// 时，<c>GetHashCode</c> 必须保证 <c>x.Equals(y) ⇒ x.GetHashCode() == y.GetHashCode()</c>。
        /// 直接 <c>x.GetHashCode()</c> 会让 1e-15 的舍入误差产生不同哈希。
        /// 把值量化到容差网格 (默认 <see cref="Tolerance"/>) 后再哈希，可以让"近似相等"的值
        /// 在绝大多数情况下落入同一桶；唯一例外是恰好跨越桶边界 (e.g. 1.4999e-9 vs 1.5000e-9) 的极端值，
        /// 这是任何"容差等价 + 离散哈希"方案的固有限制。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double QuantizeToTolerance(double value, double tolerance = Tolerance)
        {
            // NaN 统一为规范 NaN：不同载荷位的 NaN 在 .NET Framework 上哈希不同，而判等视其相等
            if (double.IsNaN(value)) return double.NaN;
            if (tolerance <= 0 || double.IsInfinity(value)) return value;
            return Math.Round(value / tolerance) * tolerance;
        }

        #endregion

        #region Angle Conversions

        /// <summary>
        /// 弧度转度数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ToDegrees(double radians)
        {
            return radians * RadToDeg;
        }

        /// <summary>
        /// 度数转弧度
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double ToRadians(double degrees)
        {
            return degrees * DegToRad;
        }

        /// <summary>
        /// 将角度规范化到 [-π, π) 范围
        /// </summary>
        /// <remarks>
        /// 使用 <c>floor</c> 一次性折算而非循环加减：循环版本对 1e18 这类极大输入需要迭代
        /// 上亿次，实际表现为挂起。NaN / 无穷输入原样返回，避免产生无意义的结果。
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double NormalizeAngle(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle)) return angle;
            // IEEERemainder 是精确取余，结果在 [-π, π]；原先的 angle - 2π·Floor(...) 在 |angle| 很大时
            // 乘积与 angle 同量级、相减丢光有效位（1e18 得到 121.7，落在区间外）。
            double result = Math.IEEERemainder(angle, TwoPi);
            // 恰好落在开区间端点 π 上时回落到 -π。
            return result >= Math.PI ? result - TwoPi : result;
        }

        /// <summary>
        /// 将角度规范化到 [0, 2π) 范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double NormalizeAnglePositive(double angle)
        {
            if (double.IsNaN(angle) || double.IsInfinity(angle)) return angle;
            // % 是精确取余（符号随被除数），理由同 NormalizeAngle。
            double result = angle % TwoPi;
            if (result < 0) result += TwoPi;
            // 极小的负余数加 2π 后可能舍入成 2π
            return result >= TwoPi ? 0 : result;
        }

        /// <summary>
        /// 将角度规范化到 [-180, 180) 度数范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double NormalizeAngleDegrees(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return degrees;
            double result = Math.IEEERemainder(degrees, 360.0);
            return result >= 180.0 ? result - 360.0 : result;
        }

        /// <summary>
        /// 将角度规范化到 [0, 360) 度数范围
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double NormalizeAngleDegreesPositive(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees)) return degrees;
            double result = degrees % 360.0;
            if (result < 0) result += 360.0;
            return result >= 360.0 ? 0 : result;
        }

        /// <summary>
        /// 计算两个角度之间的最短差值（弧度）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double AngleDifference(double from, double to)
        {
            return NormalizeAngle(to - from);
        }

        /// <summary>
        /// 计算两个角度之间的最短差值（度数）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double AngleDifferenceDegrees(double from, double to)
        {
            return NormalizeAngleDegrees(to - from);
        }

        #endregion

        /// <summary>
        /// 计算两点间的欧几里得距离
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static double Distance(double x1, double y1, double x2, double y2)
        {
            double dx = x2 - x1;
            double dy = y2 - y1;
            return Math.Sqrt(dx * dx + dy * dy);
        }

    }
}
