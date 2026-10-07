using DotNet.Drawing;
using HalconDotNet;
using DotNet.HalconCore;


namespace DotNet.HalconUI
{
    /// <summary>
    /// 模板显示处理器
    /// 鼠标移动时重绘模板区域、轮廓与原点坐标（文档原为 EraseRectMouse 的复制品，与实现无关）
    /// </summary>
    public class DispModelMouse : IMouseHandler
    {
        // 两段式初始化，见 EraseRectMouse 同名字段的说明：
        // 事件只在 HDisplayUI.DrawType == DrawEnum.DispModel 时分发，而该赋值与 SetUp 同在 SetModelPara 里。
        private IHDisplay _display = null;
        // 这两个只是转交给 Disp(HObject, DrawStyle) 的借用句柄, 空句柄由那边按"不画"处理。
        private HObject _findMode;
        private HObject _contour;
        private CvCoord _coord;

        public void SetUp(IHDisplay display, HObject shrFindMode, HObject shrContour, CvCoord shrCoord)
        {
            _display = display;
            _findMode = shrFindMode;
            _contour = shrContour;
            _coord = shrCoord;
        }

        /// <summary> 只替换模板区域句柄，不重绘、不动 DrawType（供擦除时同步，见 HEditModelUI.SyncEraseResult） </summary>
        internal void UpdateFindMode(HObject shrFindMode)
        {
            _findMode = shrFindMode;
        }

        public void OnMouseDown(HMouseEventArgs e)
        {
            // 无操作
        }

        public void OnMouseUp(HMouseEventArgs e)
        {
            // 无操作
        }

        public void OnMouseWheel(HMouseEventArgs e)
        {
            // 无操作
        }

        public void OnMouseMove(HMouseEventArgs e)
        {
            // DrawType 是公开可写的，外部可能不经 SetModelPara 直接切到 DispModel：此时还没 SetUp，什么都不画
            if (_display == null) return;
            try
            {
                _display.Disp(_findMode, DrawStyle.Of(HColor.Blue));
                _display.Disp(_contour, DrawStyle.Of(HColor.Green));
                _display.Disp(_coord, DrawStyle.Of(HColor.OrangeRed));
            }
            catch (System.Exception ex)
            {
                // 运行在鼠标移动回调里，异常不能冒到 HWindowControl 的事件总线上
                Log.Warn(nameof(DispModelMouse), "显示模板失败.", ex);
            }
        }

    }
}
