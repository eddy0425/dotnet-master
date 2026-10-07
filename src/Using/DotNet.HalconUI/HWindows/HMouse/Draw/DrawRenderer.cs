using DotNet.Drawing;
using HalconDotNet;
using System;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 交互绘图的“画布”：独占窗口的双缓冲状态、背景快照与全部图元绘制原语。
    /// </summary>
    /// <remarks>
    /// 职责边界：只管“怎么画”，不管“画什么形状”(<see cref="DrawShape"/>)，
    /// 也不管“什么时候结束”(<see cref="DrawSession"/>)。
    /// 生命周期与一次交互会话严格对应：构造即进入双缓冲模式并抓取背景，
    /// <see cref="Dispose"/> 还原窗口状态并释放背景快照。
    /// </remarks>
    internal sealed class DrawRenderer : IDrawCanvas, IDisposable
    {
        private readonly HTuple _handle;

        private HObject _bgImage;
        private HTuple _partR1, _partC1, _partR2, _partC2;

        private HTuple _savedFlush;
        private bool _windowConfigured;

        private double _pixelSize = 1;

        // 本次交互会话内是否已经记录过绘制失败，用于抑制高频重复日志
        private bool _drawFailureLogged;
        private bool _disposed;

        /// <summary>会话绑定的窗口对象，同时用作会话注册表的身份键。</summary>
        internal HWindow Window { get; }

        internal DrawRenderer(HWindow window)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            _handle = window;

            // 顺序不能换: 必须在 SetupWindow 之前 Capture,
            // 因为 DumpWindowImage 只有在 flush=true 下才能看到当前画面.
            CaptureBackground();
            SetupWindow();
        }

        #region 缩放比例

        /// <summary>
        /// 窗口缩放比例 (图像像素 / 屏幕像素)。读取的是缓存值，
        /// 由 <see cref="RefreshPixelSize"/> 在每帧开头刷新一次。
        /// </summary>
        public double PixelSize => _pixelSize;

        /// <summary>每帧开头刷新一次缩放比例，避免每个图元都触发 GetPart + GetWindowExtents。</summary>
        internal void RefreshPixelSize() => _pixelSize = ComputePixelSize();

        // 实际通过 PInvoke 计算窗口缩放比例, 比较昂贵
        private double ComputePixelSize()
        {
            try
            {
                HOperatorSet.GetPart(_handle, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                HOperatorSet.GetWindowExtents(_handle, out _, out _, out HTuple ww, out HTuple wh);
                double scaleX = (c2.D - c1.D + 1) / Math.Max(1, ww.D);
                double scaleY = (r2.D - r1.D + 1) / Math.Max(1, wh.D);
                return Math.Max(scaleX, scaleY);
            }
            catch (Exception ex)
            {
                // 取不到缩放比例时按 1:1 处理：十字/箭头尺寸略有偏差，但不影响交互可用
                Log.Debug(DrawSafe.Category, $"计算窗口缩放比例失败, 按 1 处理: {ex.Message}");
                return 1;
            }
        }

        #endregion

        #region 背景快照

        private void CaptureBackground()
        {
            // 使用临时变量, 避免 DumpWindowImage 抛出后 _bgImage 处于不确定状态
            HObject img = null;
            try
            {
                HOperatorSet.GetPart(_handle, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                _partR1 = r1; _partC1 = c1; _partR2 = r2; _partC2 = c2;
                HOperatorSet.DumpWindowImage(out img, _handle);
                _bgImage = img;
                img = null;
            }
            catch (Exception ex)
            {
                // 抓不到背景就退化成“不还原背景”，交互本身仍可继续
                _bgImage = null;
                Log.Debug(DrawSafe.Category, $"抓取窗口背景失败(已忽略): {ex.Message}");
            }
            finally
            {
                DrawSafe.Dispose(img);
            }
        }

        /// <summary>把背景快照重新铺回 backbuffer，作为一帧绘制的起点。</summary>
        public void RestoreBackground()
        {
            if (_bgImage == null) return;
            // 重新捕获时使用临时变量, 防止 DumpWindowImage 异常导致 _bgImage 引用泄漏或悬空
            HObject newImg = null;
            bool shouldRestorePart = false;
            try
            {
                HOperatorSet.GetPart(_handle, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                if (!IsSamePart(r1, c1, r2, c2))
                {
                    HOperatorSet.DumpWindowImage(out newImg, _handle);
                    var old = _bgImage;
                    _bgImage = newImg;
                    newImg = null;
                    _partR1 = r1; _partC1 = c1; _partR2 = r2; _partC2 = c2;
                    DrawSafe.Dispose(old);
                }

                HOperatorSet.GetImageSize(_bgImage, out HTuple w, out HTuple h);
                HOperatorSet.SetPart(_handle, 0, 0, h - 1, w - 1);
                shouldRestorePart = true;
                HOperatorSet.DispObj(_bgImage, _handle);
            }
            catch (Exception ex)
            {
                Log.Debug(DrawSafe.Category, $"还原窗口背景失败(已忽略): {ex.Message}");
            }
            finally
            {
                if (shouldRestorePart)
                {
                    DrawSafe.WindowOp("还原 SetPart",
                        () => HOperatorSet.SetPart(_handle, _partR1, _partC1, _partR2, _partC2));
                }
                DrawSafe.Dispose(newImg);
            }
        }

        private void ReleaseBackground()
        {
            var img = _bgImage;
            _bgImage = null;
            DrawSafe.Dispose(img);
        }

        private bool IsSamePart(HTuple r1, HTuple c1, HTuple r2, HTuple c2)
        {
            return IsSameTupleValue(r1, _partR1)
                && IsSameTupleValue(c1, _partC1)
                && IsSameTupleValue(r2, _partR2)
                && IsSameTupleValue(c2, _partC2);
        }

        private static bool IsSameTupleValue(HTuple current, HTuple saved)
        {
            return current != null
                && saved != null
                && Math.Abs(current.D - saved.D) < 0.000001;
        }

        #endregion

        #region 图元绘制

        public void Cross(double col, double row, string color, double screenSize = 20)
        {
            try
            {
                double half = screenSize / 2 * PixelSize;
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetLineWidth(_handle, 1);
                HOperatorSet.DispLine(_handle, row - half, col, row + half, col);
                HOperatorSet.DispLine(_handle, row, col - half, row, col + half);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Cross), ex); }
        }

        public void Rect1(double col1, double row1, double col2, double row2, string color)
        {
            try
            {
                DrawGeometry.NormalizeRect1(col1, row1, col2, row2,
                    out double left, out double top, out double right, out double bottom);
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetDraw(_handle, "margin");
                HOperatorSet.DispRectangle1(_handle, top, left, bottom, right);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Rect1), ex); }
        }

        public void Rect2(double cx, double cy, double phi, double len1, double len2, string color)
        {
            try
            {
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetDraw(_handle, "margin");
                HOperatorSet.DispRectangle2(_handle, cy, cx, phi, len1, len2);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Rect2), ex); }
        }

        /// <summary>矩形 + 沿 phi 方向(主轴方向)的箭头: 起点在矩形中心, 终点在主轴端点。</summary>
        public void Rect2Arrow(double cx, double cy, double phi, double len1, double len2, string color)
        {
            Rect2(cx, cy, phi, len1, len2, color);

            double endCol = cx + len1 * Math.Cos(phi);
            double endRow = cy - len1 * Math.Sin(phi);

            Arrow(cx, cy, endCol, endRow, color);
        }

        public void Arrow(double col1, double row1, double col2, double row2, string color)
        {
            try
            {
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetLineWidth(_handle, 1);
                // disp_arrow 的 Size 为图像坐标下箭头头长度, 用屏幕固定像素换算保持视觉稳定
                double size = 2 * PixelSize;
                HOperatorSet.DispArrow(_handle, row1, col1, row2, col2, size);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Arrow), ex); }
        }

        public void Circle(double col, double row, double radius, string color)
        {
            try
            {
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetDraw(_handle, "margin");
                HOperatorSet.DispCircle(_handle, row, col, radius);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Circle), ex); }
        }

        public void Ellipse(double cx, double cy, double phi, double r1, double r2, string color)
        {
            try
            {
                double major = Math.Max(r1, r2);
                double minor = Math.Min(r1, r2);
                double adjPhi = r1 >= r2 ? phi : phi + Math.PI / 2;
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.SetDraw(_handle, "margin");
                HOperatorSet.DispEllipse(_handle, cy, cx, adjPhi, major, minor);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Ellipse), ex); }
        }

        public void Line(double col1, double row1, double col2, double row2, string color)
        {
            try
            {
                HOperatorSet.SetColor(_handle, color);
                HOperatorSet.DispLine(_handle, row1, col1, row2, col2);
            }
            catch (Exception ex) { OnDrawFailure(nameof(Line), ex); }
        }

        /// <summary>
        /// 交互绘制失败：每个会话只记一条，避免鼠标移动回调把日志刷爆。
        /// </summary>
        private void OnDrawFailure(string operation, Exception ex)
        {
            if (_drawFailureLogged) return;
            _drawFailureLogged = true;
            Log.Warn(DrawSafe.Category, $"{operation} 绘制失败，本次交互会话内不再重复记录.", ex);
        }

        #endregion

        #region 窗口状态

        // 进入交互会话: 一次性切换为双缓冲模式, 整个会话保持稳定.
        // - flush=false : 禁用自动刷新, 所有 disp_* 累积到 backbuffer
        // 原来还会 set_system('autodraw', 'false') 并在结束时还原: HALCON 22.11 没有这个系统参数
        // (get/set 都报 #1301), 每次会话都静默失败一次, 已删除。
        // 参考 Halcon 官方文档 set_window_param 中关于 flush 的双缓冲建议.
        private void SetupWindow()
        {
            if (_windowConfigured) return;

            // 取不到原值就置 null，RestoreWindow 会退回到默认值 flush=true
            try
            {
                HOperatorSet.GetWindowParam(_handle, "flush", out HTuple flush);
                _savedFlush = flush;
            }
            catch (Exception ex)
            {
                _savedFlush = null;
                Log.Debug(DrawSafe.Category, $"读取 flush 原值失败, 还原时将走默认值: {ex.Message}");
            }

            // 开关设置失败只是退化为非双缓冲(可能闪烁)，不影响绘制结果
            DrawSafe.WindowOp("关闭 flush", () => HOperatorSet.SetWindowParam(_handle, "flush", "false"));

            _windowConfigured = true;
        }

        // 离开交互会话: 还原 flush. 把 flush 切回 true 会顺带触发一次刷新,
        // 让 RestoreBackground 恢复的背景立即可见.
        private void RestoreWindow()
        {
            if (!_windowConfigured) return;

            DrawSafe.WindowOp("还原 flush", () =>
            {
                if (_savedFlush != null)
                    HOperatorSet.SetWindowParam(_handle, "flush", _savedFlush);
                else
                    HOperatorSet.SetWindowParam(_handle, "flush", "true");
            });

            _savedFlush = null;
            _windowConfigured = false;
        }

        /// <summary>
        /// 一帧绘制完毕, 把 backbuffer 一次性 swap 到屏幕。
        /// 这是 Halcon 推荐的“双缓冲”操作 (set_window_param flush=false + flush_buffer)。
        /// </summary>
        internal void Flush()
        {
            if (!_windowConfigured) return;
            DrawSafe.WindowOp("FlushBuffer", () => HOperatorSet.FlushBuffer(_handle));
        }

        #endregion

        /// <summary>
        /// 结束交互：先把背景铺回 backbuffer，再还原窗口状态 (切回 flush=true 时自动 swap 出去)，
        /// 最后释放背景快照。幂等。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                // RestoreBackground 在 flush=false 下绘制到 backbuffer
                RestoreBackground();
            }
            finally
            {
                // RestoreWindow 内部会把 flush 切回 true, 自动触发一次 swap, 让背景可见
                RestoreWindow();
                ReleaseBackground();
            }
        }
    }
}
