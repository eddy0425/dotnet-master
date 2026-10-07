using HalconDotNet;


namespace DotNet.HalconUI
{
    /// <summary>
    /// 绘图处理器接口
    /// 所有绘图类型都需要实现此接口
    /// </summary>
    public interface IMouseHandler
    {
        /// <summary>
        /// 处理鼠标按下事件
        /// </summary>
        void OnMouseDown(HMouseEventArgs e);

        /// <summary>
        /// 处理鼠标释放事件
        /// </summary>
        void OnMouseUp(HMouseEventArgs e);

        /// <summary>
        /// 处理鼠标滚轮事件
        /// </summary>
        void OnMouseWheel(HMouseEventArgs e);

        /// <summary>
        /// 处理鼠标移动事件
        /// </summary>
        void OnMouseMove(HMouseEventArgs e);

    }
}
