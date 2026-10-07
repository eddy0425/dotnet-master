using HalconDotNet;
using System;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 空绘图处理器：本身不做任何交互，只把鼠标事件转发给该窗口上正在进行的
    /// <see cref="DrawHelper"/> 绘制会话。当不需要绘图时使用。
    /// </summary>
    /// <remarks>
    /// 必须持有窗口对象：<see cref="HMouseEventArgs"/> 不带窗口标识，
    /// 而绘制会话是按窗口注册的，多窗口场景下只有调用方知道事件来自哪个窗口。
    /// </remarks>
    public class NoneMouse : IMouseHandler
    {
        private readonly HWindow _window;

        public NoneMouse(HWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void OnMouseDown(HMouseEventArgs e) => DrawHelper.ForwardMouseDown(_window, e);

        public void OnMouseUp(HMouseEventArgs e) => DrawHelper.ForwardMouseUp(_window, e);

        public void OnMouseWheel(HMouseEventArgs e) => DrawHelper.ForwardMouseWheel(_window, e);

        public void OnMouseMove(HMouseEventArgs e) => DrawHelper.ForwardMouseMove(_window, e);
    }
}
