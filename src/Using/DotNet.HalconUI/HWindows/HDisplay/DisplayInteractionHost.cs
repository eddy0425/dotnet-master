using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconUI
{
    /// <summary>
    /// <see cref="IInteractionHost"/> 的实现：把策略的交互请求转给显示控件。
    /// </summary>
    /// <remarks>
    /// 原来由 <see cref="HDisplayUI"/>（一个 UserControl）直接实现交互契约，控件的公开面就是算法能碰到的面。
    /// 现在单独一个适配器，算法只看到 <see cref="IInteractionHost"/> 的几个成员；控件上其余的方法仍只属于宿主。
    /// 只能在 UI 线程上使用。
    /// </remarks>
    public sealed class DisplayInteractionHost : IInteractionHost
    {
        private readonly HDisplayUI _display;

        public DisplayInteractionHost(HDisplayUI display)
        {
            _display = display ?? throw new ArgumentNullException(nameof(display));
            Feedback = new DisplayFeedback(display);
        }

        public HObject CurrentImage => _display.Display.HoImage;

        public IOverlay Feedback { get; }

        /// <summary> 最近一次 <see cref="ShowRoi"/> 的 ROI；宿主的模板编辑器叠加模板轮廓时一并显示它 </summary>
        public CvRegion ShownRoi { get; private set; }

        public Task<bool> DrawRegionAsync(CvRegion region) => _display.DrawRegionAsync(region);

        public Task<bool> DrawRegionModAsync(CvRegion region) => _display.DrawRegionModAsync(region);

        public void ShowRoi(CvRegion roi)
        {
            ShownRoi = roi;
            _display.SetRectPara(roi);
        }

        /// <summary> 交互提示：直接画在窗口上（即时模式），下次重画前有效 </summary>
        private sealed class DisplayFeedback : IOverlay
        {
            private readonly HDisplayUI _display;

            public DisplayFeedback(HDisplayUI display) => _display = display;

            private IHDisplay Window => _display.Display;

            public void Add(HObject obj, DrawStyle style = null) => Window.Disp(obj, style);
            public void Add(Point2d point, DrawStyle style = null) => Window.Disp(point, style);
            public void Add(IReadOnlyList<Point2d> points, DrawStyle style = null) => Window.Disp(points, style);
            public void Add(CvLine line, DrawStyle style = null) => Window.Disp(line, style);
            public void Add(CvArrow arrow, DrawStyle style = null) => Window.Disp(arrow, style);
            public void Add(CvCircle circle, DrawStyle style = null) => Window.Disp(circle, style);
            public void Add(CvCoord coord, DrawStyle style = null) => Window.Disp(coord, style);
            public void Add(CvRegion region, DrawStyle style = null) => Window.Disp(region, style);
            public void AddRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
                => Window.DispRect2(center, phi, length1, length2, style);
            public void Text(string message, Point2d position, DrawStyle style = null) => Window.DispText(message, position, style);
        }
    }
}
