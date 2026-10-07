using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 把 <see cref="IHDisplay"/> 的每次调用切回控件所在的 UI 线程。
    /// </summary>
    /// <remarks>
    /// HALCON 窗口只能在创建它的线程上操作。流程放到后台线程执行（相机回调、<c>FlowRunner</c>）时，
    /// 策略的 <c>Render</c> 会在后台线程调用显示接口；原来没有任何 <c>InvokeRequired</c> 保护，
    /// 直接跨线程改控件。这里统一转回 UI 线程同步执行：已经在 UI 线程上（或控件还没有句柄）时直接调用，零开销。
    /// <para>
    /// 同步 <see cref="Control.Invoke(Delegate)"/> 意味着后台线程会等 UI 线程画完；UI 线程不得反过来同步等待该后台线程，否则死锁。
    /// </para>
    /// </remarks>
    public sealed class UiThreadDisplay : IHDisplay
    {
        private readonly IHDisplay _inner;
        private readonly Control _owner;

        public UiThreadDisplay(IHDisplay inner, Control owner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary> 被包装的显示对象 </summary>
        public IHDisplay Inner => _inner;

        private void Run(Action action)
        {
            if (_owner.InvokeRequired) _owner.Invoke(action);
            else action();
        }

        private T Get<T>(Func<T> func)
        {
            return _owner.InvokeRequired ? (T)_owner.Invoke(func) : func();
        }

        public bool IsCross { get => Get(() => _inner.IsCross); set => Run(() => _inner.IsCross = value); }
        public bool Adaptive { get => Get(() => _inner.Adaptive); set => Run(() => _inner.Adaptive = value); }
        public double HoWidth => Get(() => _inner.HoWidth);
        public double HoHeight => Get(() => _inner.HoHeight);
        public Size2d HoSize => Get(() => _inner.HoSize);
        public Point2d HoCentre => Get(() => _inner.HoCentre);
        public HObject HoImage => Get(() => _inner.HoImage);

        public HColor GetColor() => Get(() => _inner.GetColor());
        public void SetColor(HColor color) => Run(() => _inner.SetColor(color));
        public void SetDraw(string mode) => Run(() => _inner.SetDraw(mode));
        public void SetFontSize(HTuple size) => Run(() => _inner.SetFontSize(size));

        public void SetImage(HObject image) => Run(() => _inner.SetImage(image));
        public void DispImage(HObject image) => Run(() => _inner.DispImage(image));
        public void DispImage(HObject image, bool isSetPart) => Run(() => _inner.DispImage(image, isSetPart));
        public void ReDispImage() => Run(() => _inner.ReDispImage());
        public void ClearWinDisp(HObject objectVal) => Run(() => _inner.ClearWinDisp(objectVal));

        public void Disp(Point2d point, DrawStyle style = null) => Run(() => _inner.Disp(point, style));
        public void Disp(IReadOnlyList<Point2d> points, DrawStyle style = null) => Run(() => _inner.Disp(points, style));
        public void Disp(CvCoord coord, DrawStyle style = null) => Run(() => _inner.Disp(coord, style));
        public void Disp(CvLine line, DrawStyle style = null) => Run(() => _inner.Disp(line, style));
        public void Disp(CvArrow arrow, DrawStyle style = null) => Run(() => _inner.Disp(arrow, style));
        public void Disp(CvCircle circle, DrawStyle style = null) => Run(() => _inner.Disp(circle, style));
        public void Disp(CvRegion region, DrawStyle style = null) => Run(() => _inner.Disp(region, style));
        public void Disp(HObject region, DrawStyle style = null) => Run(() => _inner.Disp(region, style));
        public void DispText(string message, Point2d position, DrawStyle style = null) => Run(() => _inner.DispText(message, position, style));
        public void DispRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
            => Run(() => _inner.DispRect2(center, phi, length1, length2, style));
        public void DispRegionOutline(CvRegion region, DrawStyle style = null) => Run(() => _inner.DispRegionOutline(region, style));
        public void DispLineWithEndMarker(CvLine line, double markerRadius, DrawStyle style = null)
            => Run(() => _inner.DispLineWithEndMarker(line, markerRadius, style));
        public void DispSegmentWithCrosses(Point2d start, Point2d end, double armLength, DrawStyle style = null)
            => Run(() => _inner.DispSegmentWithCrosses(start, end, armLength, style));

        // 交互绘制必须在 UI 线程发起（await 要捕获 UI 的同步上下文才能收到鼠标事件）；转过去发起，再把任务交回
        public Task<bool> DrawRegionAsync(CvRegion region) => Get(() => _inner.DrawRegionAsync(region));
        public Task<bool> DrawRegionModAsync(CvRegion region) => Get(() => _inner.DrawRegionModAsync(region));
        public Task<HObject> DrawRegionAsync(RectEnum type) => Get(() => _inner.DrawRegionAsync(type));

        /// <summary> 包装不拥有被包装的显示对象：释放由创建者（<see cref="HDisplayUI"/>）负责 </summary>
        public void Dispose() { }
    }
}
