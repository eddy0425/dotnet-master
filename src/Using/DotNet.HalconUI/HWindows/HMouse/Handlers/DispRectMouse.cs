using DotNet.Drawing;
using HalconDotNet;
using System;
using DotNet.HalconCore;


namespace DotNet.HalconUI
{
    /// <summary>
    /// 矩形区域显示处理器
    /// 鼠标移动时重绘区域中心与区域轮廓
    /// </summary>
    public class DispRectMouse : IMouseHandler
    {
        // 两段式初始化，见 EraseRectMouse 同名字段的说明：
        // 事件只在 HDisplayUI.DrawType == DrawEnum.DispRect 时分发，而该赋值与 SetUp 同在 SetRectPara 里。
        private IHDisplay _display = null;
        private CvRegion _shrRegion = null;

        public void SetUp(IHDisplay display, CvRegion shrRegion)
        {
            _display = display;
            _shrRegion = shrRegion;
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
            // 未经 SetRectPara 直接切到 DispRect 时还没 SetUp：什么都不画，也不必每次移动都告警
            // (SetUp 过但传了 null 区域属于调用方错误，仍走下面的告警)
            if (_display == null) return;
            try
            {
                Point2d TopLeft = _shrRegion.TopLeft;
                Point2d BottomRight = _shrRegion.BottomRight;
                Point2d Center = _shrRegion.Center;

                // 显示最终结果
                //_display.Disp(TopLeft, DrawStyle.Of(HColor.OrangeRed, 50));
                //_display.Disp(BottomRight, DrawStyle.Of(HColor.OrangeRed, 50));
                _display.Disp(Center, DrawStyle.Of(HColor.Orange, 50));

                _display.Disp(_shrRegion, DrawStyle.Of(HColor.Blue));
            }
            catch (Exception ex)
            {
                // 运行在鼠标移动回调里，抛出会连带打断整个拖拽交互；
                // 但一次都不记录的话，区域显示异常在现场完全无迹可循。
                Log.Warn(nameof(DispRectMouse), "显示区域失败.", ex);
            }
  
        }

    }
}
