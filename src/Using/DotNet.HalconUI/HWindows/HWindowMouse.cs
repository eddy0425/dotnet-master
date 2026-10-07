using System;
using System.Windows.Forms;
using DotNet.Drawing;
using HalconDotNet;
using DotNet.HalconCore;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 鼠标交互（双击复位 / 中键平移 / 滚轮缩放 / 灰度回显）。
    /// </summary>
    /// <remarks>
    /// 设计要点：
    /// - 所有事件处理都在 try/catch 内，绝不允许把异常抛回 HWindowControl 事件总线，以免冒泡到顶层 UI 崩溃。
    /// - 对外部传入的 <see cref="HObject"/> 一律使用 <see cref="HObjectExtension.NotNull"/> 检查，
    ///   而不是 <c>!= null</c>—— HObject 是 HalconDotNet 的 wrapper，可能"非 null 但未 Initialized"。
    /// - 不在鼠标事件里弹 <see cref="MessageBox"/>——会随着拖拽频率反复弹窗、阻塞 UI。
    /// - 双击判定用单调时钟 <see cref="Environment.TickCount"/> + 系统设置 <see cref="SystemInformation.DoubleClickTime"/>：
    ///   原实现用 <c>DateTime.Now.Ticks</c>（校时 / 夏令时会让差值跳变）配硬编码 200ms（与系统设置不一致）；
    ///   且判出双击后不复位，连续三击会被判成两次双击。
    /// - 原先公开的 <c>MouseDown</c> / <c>MouseDouble</c> 标志位本类只置位不复位、全仓也无人读取，已删除。
    /// </remarks>
    public class HWindowMouse : IDisposable
    {
        // 普通版 Halcon 能处理的图像最大尺寸 32K*32K，避免缩小过头导致 SetPart 崩溃
        const double MaxHalconViewArea = 32000d * 32000d;

        /// <summary> 滚轮每格的缩放倍率；缩小用其倒数，保证放大、缩小各一格后视野复原 </summary>
        const double ZoomStep = 1.5;

        // 上一次（未被双击消耗的）按下时刻，Environment.TickCount 毫秒；null 表示没有可配对的单击
        int? _lastClickMs;
        bool Mouse_hand = false;
        double RowDown;
        double ColDown;

        readonly HWindow _hWindow;
        readonly IHDisplay _display;
        readonly HWindowControl _hWindowControl;
        readonly bool _subscribed;

        bool _disposed;

        public event Action<HTuple, HTuple, HTuple> RefreshUI;

        public HWindowMouse(HWindowControl hWindowControl, IHDisplay display) : this(hWindowControl, display, subscribe: true) { }

        /// <param name="subscribe">
        /// false 时不自己订阅控件的鼠标事件，由宿主按固定顺序转发（见 <c>HDisplayUI</c>）：
        /// 同一控件上的多个订阅方按订阅先后执行，把顺序交给订阅时机是隐式依赖。
        /// </param>
        public HWindowMouse(HWindowControl hWindowControl, IHDisplay display, bool subscribe)
        {
            if (hWindowControl == null) throw new ArgumentNullException(nameof(hWindowControl));
            if (display == null) throw new ArgumentNullException(nameof(display));

            _hWindow = hWindowControl.HalconWindow;
            _hWindowControl = hWindowControl;
            _display = display;

            if (!subscribe) return;
            hWindowControl.HMouseDown += OnHMouseDown;
            hWindowControl.HMouseUp += OnHMouseUp;
            hWindowControl.HMouseWheel += OnHMouseWheel;
            _subscribed = true;
        }

        bool IsUsable()
        {
            if (_disposed) return false;
            if (_hWindow == null) return false;
            if (_hWindowControl == null || _hWindowControl.IsDisposed) return false;
            try { return _hWindow.IsInitialized(); }
            catch { return false; }
        }

        public void OnHMouseDown(object sender, HMouseEventArgs e)
        {
            if (!IsUsable() || e == null) return;
            try
            {
                HTuple Row = e.Y, Column = e.X;
                RowDown = Row;
                ColDown = Column;

                // TickCount 约 24.9 天回绕一次，int 减法在回绕处仍得到正确的差值
                int nowMs = Environment.TickCount;
                bool doubleClick = _lastClickMs.HasValue
                                   && unchecked(nowMs - _lastClickMs.Value) <= SystemInformation.DoubleClickTime;
                // 双击消耗掉这次配对：第三击重新作为单击起点，而不是与第二击再凑成一次双击
                _lastClickMs = doubleClick ? (int?)null : nowMs;

                if (doubleClick && _display.HoImage.NotNull())
                {
                    HOperatorSet.SetPart(_hWindow, 0, 0, _display.HoHeight - 1, _display.HoWidth - 1);
                    HOperatorSet.ClearWindow(_hWindow);
                    _display.ReDispImage();     // 经显示对象重画: 运行结果叠加层随图像一起重放
                }

                // 每次按下都重新判定：原先只在中键按下时置 true，中键若在控件外松开（收不到 Up），
                // 残留的 true 会让之后一次左键拖拽（如画 ROI）把视图平移走。
                Mouse_hand = e.Button == MouseButtons.Middle;
            }
            catch (Exception ex) { Log.Error(nameof(HWindowMouse), "处理鼠标按下失败.", ex); }
        }

        public void OnHMouseUp(object sender, HMouseEventArgs e)
        {
            if (!IsUsable() || e == null) return;
            try
            {
                HTuple Row = e.Y, Column = e.X;

                if (Mouse_hand)
                {
                    // 平移：始终重置 Mouse_hand，避免松开后状态卡住
                    Mouse_hand = false;

                    if (_display.HoImage.NotNull())
                    {
                        double RowMove = Row - RowDown;
                        double ColMove = Column - ColDown;
                        HOperatorSet.GetPart(_hWindow, out HTuple row1, out HTuple col1, out HTuple row2, out HTuple col2);
                        HOperatorSet.SetPart(_hWindow, row1 - RowMove, col1 - ColMove, row2 - RowMove, col2 - ColMove);
                        HOperatorSet.ClearWindow(_hWindow);
                        _display.ReDispImage();     // 经显示对象重画: 运行结果叠加层随图像一起重放
                    }
                    // 没有图像时静默忽略——鼠标事件不应该弹模态框
                }

                var handler = RefreshUI;
                if (handler != null && _display.HoImage.NotNull())
                {
                    HTuple egray = null;
                    bool gotGray = false;
                    try
                    {
                        HOperatorSet.GetGrayval(_display.HoImage, Row, Column, out egray);
                        gotGray = true;
                    }
                    catch (Exception ex)
                    {
                        // 鼠标落在图像范围外 GetGrayval 必然失败，属于正常路径，不升级为错误；
                        // 保留 Debug 级日志，便于排查"取灰度一直没反应"时区分是越界还是别的原因。
                        Log.Debug(nameof(HWindowMouse), $"取灰度失败(通常是鼠标在图像外): {ex.Message}");
                    }

                    // 订阅方异常单独记 Error：原先与取灰度同在一个 try 里，被当成“鼠标在图像外”吞成 Debug
                    // 用标志而不是 egray != null 判成功：HALCON 抛错前可能已给 out 参数赋值
                    if (gotGray)
                    {
                        try { handler.Invoke(Row, Column, egray); }
                        catch (Exception ex) { Log.Error(nameof(HWindowMouse), "RefreshUI 订阅方处理失败.", ex); }
                    }
                }
            }
            catch (Exception ex) { Log.Error(nameof(HWindowMouse), "处理鼠标抬起失败.", ex); }
        }

        public void OnHMouseWheel(object sender, HMouseEventArgs e)
        {
            if (!IsUsable() || e == null) return;
            if (!_display.HoImage.NotNull()) return;     // 没图像时滚轮无意义

            try
            {
                // 缩小系数取放大系数的倒数：原先是 1.5 / 0.5，放大一格再缩小一格视野变成原来的 4/3
                bool zoomIn = e.Delta > 0;
                double zoom = zoomIn ? ZoomStep : 1.0 / ZoomStep;
                double Row = e.Y, Column = e.X;

                HOperatorSet.GetPart(_hWindow, out HTuple Row0, out HTuple Column0, out HTuple Row00, out HTuple Column00);
                // Part 两端都是包含的像素坐标，宽高是像素个数要 +1；原先少算 1，每来回缩放一次视野都会漂移
                double Ht = Row00.D - Row0.D + 1;
                double Wt = Column00.D - Column0.D + 1;

                // 放大总是允许；缩小要按「缩小之后」的视图面积判上限（原先用缩小前的面积判，
                // 面积刚好低于上限时仍会再缩一次，越过 32K*32K）
                if (zoomIn || (Ht / zoom) * (Wt / zoom) < MaxHalconViewArea)
                {
                    double r1 = Row0.D + ((1 - (1.0 / zoom)) * (Row - Row0.D));
                    double c1 = Column0.D + ((1 - (1.0 / zoom)) * (Column - Column0.D));
                    double r2 = r1 + (Ht / zoom) - 1;
                    double c2 = c1 + (Wt / zoom) - 1;

                    HOperatorSet.SetPart(_hWindow, r1, c1, r2, c2);
                    HOperatorSet.ClearWindow(_hWindow);
                    _display.ReDispImage();     // 经显示对象重画: 运行结果叠加层随图像一起重放
                }
            }
            catch (Exception ex) { Log.Error(nameof(HWindowMouse), "处理鼠标滚轮失败.", ex); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_subscribed && _hWindowControl != null && !_hWindowControl.IsDisposed)
            {
                _hWindowControl.HMouseDown -= OnHMouseDown;
                _hWindowControl.HMouseUp -= OnHMouseUp;
                _hWindowControl.HMouseWheel -= OnHMouseWheel;
            }

            RefreshUI = null;
            GC.SuppressFinalize(this);
        }
    }
}
