using System.Windows.Forms;
using HalconDotNet;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 图元状态机（<see cref="DrawShape"/>）需要的全部绘制能力。
    /// </summary>
    /// <remarks>
    /// <see cref="DrawRenderer"/> 是唯一的生产实现（HALCON 窗口 + 双缓冲）；
    /// 抽成接口后图元状态机可以脱离 HALCON 窗口单测，测试里换成记录调用的画布即可。
    /// 坐标一律是图像坐标 (col = X, row = Y)。
    /// </remarks>
    internal interface IDrawCanvas
    {
        /// <summary> 图像像素 / 屏幕像素；命中测试按它把屏幕阈值换算到图像坐标 </summary>
        double PixelSize { get; }

        /// <summary> 把背景快照还原到后台缓冲，开始画新的一帧 </summary>
        void RestoreBackground();

        void Cross(double col, double row, string color, double screenSize = 20);
        void Rect1(double col1, double row1, double col2, double row2, string color);
        void Rect2(double cx, double cy, double phi, double len1, double len2, string color);
        void Rect2Arrow(double cx, double cy, double phi, double len1, double len2, string color);
        void Arrow(double col1, double row1, double col2, double row2, string color);
        void Circle(double col, double row, double radius, string color);
        void Ellipse(double cx, double cy, double phi, double r1, double r2, string color);
        void Line(double col1, double row1, double col2, double row2, string color);
    }

    /// <summary>
    /// 图元状态机看到的一次鼠标输入：位置（图像坐标）+ 按键。
    /// </summary>
    /// <remarks>
    /// 取代直接使用 <see cref="HMouseEventArgs"/>：后者只有 internal 构造函数，状态机测试只能靠反射造事件。
    /// </remarks>
    internal readonly struct MouseInput
    {
        public MouseInput(double x, double y, MouseButtons button = MouseButtons.None)
        {
            X = x;
            Y = y;
            Button = button;
        }

        /// <summary> 列 </summary>
        public double X { get; }

        /// <summary> 行 </summary>
        public double Y { get; }

        public MouseButtons Button { get; }

        public static implicit operator MouseInput(HMouseEventArgs e) => new MouseInput(e.X, e.Y, e.Button);

        public override string ToString() => $"({X:F1},{Y:F1}) {Button}";
    }
}
