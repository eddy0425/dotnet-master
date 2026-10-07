using System;
using System.Reflection;
using System.Windows.Forms;
using HalconDotNet;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 构造 <see cref="HMouseEventArgs"/>。
    /// </summary>
    /// <remarks>
    /// halcondotnet 只提供 internal 构造函数 (button, clicks, x, y, delta)，这里经反射调用。
    /// 坐标约定与 HALCON 一致：<c>X = column</c>，<c>Y = row</c>。
    /// </remarks>
    internal static class Mouse
    {
        private static readonly ConstructorInfo Ctor = typeof(HMouseEventArgs).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null,
            new[] { typeof(MouseButtons), typeof(int), typeof(double), typeof(double), typeof(int) }, null)
            ?? throw new MissingMethodException("HMouseEventArgs 缺少 (MouseButtons, int, double, double, int) 构造函数，halcondotnet 版本可能已变化。");

        public static HMouseEventArgs Create(MouseButtons button, double x, double y, int delta = 0)
            => (HMouseEventArgs)Ctor.Invoke(new object[] { button, button == MouseButtons.None ? 0 : 1, x, y, delta });

        public static HMouseEventArgs Left(double x, double y) => Create(MouseButtons.Left, x, y);

        public static HMouseEventArgs Right(double x, double y) => Create(MouseButtons.Right, x, y);

        public static HMouseEventArgs Middle(double x, double y) => Create(MouseButtons.Middle, x, y);

        public static HMouseEventArgs Move(double x, double y) => Create(MouseButtons.None, x, y);

        /// <summary>滚轮：<paramref name="delta"/> &gt; 0 为向前（放大），&lt; 0 为向后（缩小）。</summary>
        public static HMouseEventArgs Wheel(double x, double y, int delta) => Create(MouseButtons.None, x, y, delta);
    }
}
