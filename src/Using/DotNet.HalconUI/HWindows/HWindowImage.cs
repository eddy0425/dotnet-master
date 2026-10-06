using System;
using DotNet.Drawing;
using HalconDotNet;

namespace DotNet.HalconUI
{
    public class HWindowImage : IDisposable
    {
        readonly HWindow _hWindow;
        readonly HWindowControl _hWindowControl;

        ZoomImage getInfo;
        ZoomImage zoomInfo;

        // 可空: Dispose 会把它置 null(C16 先置空再释放), 窗口释放后 HoImage 读到的就是 null。
        HObject _hoImage;
        bool _disposed;

        // LayoutControlToImage 改控件 Width/Height 时会同步触发本控件的 Resize，
        // 用此标志让那次回调直接返回，避免布局过程中重入再布局、再重绘一遍。
        bool _inLayout;

        // 控件画不了时收到的图像，其布局/SetPart 要等到能画时补上（见 Fun_DispImage）
        bool _pendingLayout;
        bool _pendingSetPart;

        // 画笔默认 margin 只在首次布局时设一次，之后尊重调用方自己设的模式（见 LayoutControlToImage）
        bool _drawModeInitialised;

        /// <summary>
        /// 当前显示的图像。所有权在本类：外部传入的句柄一律 <c>copy_image</c> 一份后接管，
        /// 释放由 <see cref="Dispose"/> 或下一次接管时的换出动作负责，调用方不得释放本属性。
        /// </summary>
        /// <remarks>
        /// 审查项 C16：原实现由 <see cref="HDisplay"/> 持有句柄、本类只存引用，
        /// 而 <see cref="HDisplay"/> 每次显示都是「先 Dispose 旧图、再 CopyImage 新图、最后回写本类」。
        /// 中间那段窗口里本属性指向的是已释放对象；控件不可见时更糟——回写那一步被
        /// <see cref="CanDraw"/> 挡掉，本属性会一直停在已释放的旧图上。
        /// 现在所有权集中到一处，句柄的换入换出与引用更新是同一个动作。
        /// </remarks>
        public HObject HoImage { get { return _hoImage; } }
        public double HoWidth { get { return getInfo.width; } }
        public double HoHeight { get { return getInfo.height; } }

        public HWindowImage(HWindowControl hWindowControl)
        {
            if (hWindowControl == null) throw new ArgumentNullException(nameof(hWindowControl));

            _hWindow = hWindowControl.HalconWindow;
            _hWindowControl = hWindowControl;

            getInfo = new ZoomImage();
            // zoomInfo 记录的是「控件已按哪个尺寸布局过」，初始必须是未布局(0x0)。
            // 原先与 getInfo 同为默认 1248x2200，首张图恰好是这个尺寸（本项目相机的实际分辨率）时
            // 两者相等、跳过布局，控件保持铺满父容器，竖图被拉伸。
            zoomInfo = new ZoomImage();
            HOperatorSet.GenEmptyObj(out _hoImage);

            hWindowControl.Resize += HWindowControl_Resize;
            hWindowControl.VisibleChanged += HWindowControl_VisibleChanged;
        }

        // 所在 TabPage 被选中等父链变为可见时，子控件同样会收到 VisibleChanged
        private void HWindowControl_VisibleChanged(object sender, EventArgs e)
        {
            if ((_pendingLayout || _pendingSetPart) && CanDraw()) Fun_ReDisplay();
        }

        bool CanDraw()
        {
            if (_disposed) return false;
            if (_hWindowControl == null || _hWindowControl.IsDisposed) return false;
            if (_hWindowControl.Parent == null) return false;
            if (!_hWindowControl.Visible) return false;
            try { return _hWindow != null && _hWindow.IsInitialized(); }
            catch { return false; }
        }

        private void HWindowControl_Resize(object sender, EventArgs e)
        {
            try
            {
                if (_disposed || _inLayout) return;

                HWindowControl control = sender as HWindowControl;
                if (control == null || control.Parent == null) return;
                if (!control.Visible) return;

                if (getInfo.parent.Width != control.Parent.Width || getInfo.parent.Height != control.Parent.Height)
                {
                    getInfo.parent.Width = control.Parent.Width;
                    getInfo.parent.Height = control.Parent.Height;

                    LayoutControlToImage(getInfo);
                    Fun_ReDisplay();
                }
            }
            catch (Exception ex)
            {
                // 尺寸变化回调运行在控件布局路径上，抛出会打断整个 Layout；
                // 但完全静默会让"窗口不跟随缩放"这类问题无从查起，所以记日志后忽略。
                Log.Warn(nameof(HWindowImage), "自适应缩放失败.", ex);
            }
        }

        /// <summary>
        /// 接管一份新图像：复制 → 换引用 → 释放旧句柄，并同步尺寸缓存。
        /// </summary>
        /// <remarks>
        /// 三步的顺序不能调整。若先释放旧句柄再赋值，<see cref="HoImage"/> 在两条语句之间
        /// 指向已释放对象；而本类的 <c>Resize</c> 回调会同步调用 <see cref="Fun_ReDisplay"/>，
        /// 正好会撞上这个窗口（审查项 C16 要求的「先置空引用再 Dispose」）。
        /// </remarks>
        void AdoptImage(HObject image)
        {
            HOperatorSet.CopyImage(image, out HObject copy);

            HObject old = _hoImage;
            _hoImage = copy;
            old?.Dispose();

            HOperatorSet.GetImageSize(copy, out getInfo.width, out getInfo.height);
        }

        /// <summary> 设置图像：只接管图像与尺寸，不做任何绘制 </summary>
        internal void Fun_SetImage(HObject _image)
        {
            if (_disposed || !_image.NotNull()) return;

            AdoptImage(_image);
        }

        /// <summary> 图像显示 </summary>
        public void Fun_ReDisplay()
        {
            if (!CanDraw()) return;

            try
            {
                if (!_hoImage.NotNull())
                {
                    HOperatorSet.ClearWindow(_hWindow);
                    return;
                }

                ApplyPendingFit();
                HOperatorSet.DispObj(_hoImage, _hWindow);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HWindowImage), "重绘图像失败.", ex);
            }
        }

        /// <summary> 图像显示 </summary>
        public void Fun_DispImage(HObject _image, bool isSetPart)
        {
            if (_disposed) return;

            if (!_image.NotNull())
            {
                // 空图像不接管所有权，只把窗口清空（保持原行为）
                if (!CanDraw()) return;
                try { HOperatorSet.ClearWindow(_hWindow); }
                catch (Exception ex) { Log.Error(nameof(HWindowImage), "清空窗口失败.", ex); }
                return;
            }

            // 接管所有权先于 CanDraw 判断：控件暂时画不了（例如所在 TabPage 未选中）
            // 不代表这一帧该被丢弃 —— 原实现在这里直接 return，HoImage 会停在上一张图上，
            // 而上一张图此时已被释放，对外就是一个悬挂句柄（审查项 C16）。
            AdoptImage(_image);

            // 画不了时把本帧的布局/SetPart 记下来，等能画时由 Fun_ReDisplay 补上。
            // 原先直接 return 丢掉，控件重新可见后一直按旧 Part 显示新图，直到下一次 DispImage。
            _pendingLayout = true;
            _pendingSetPart |= isSetPart;
            if (!CanDraw()) return;

            try
            {
                ApplyPendingFit();
                HOperatorSet.DispObj(_hoImage, _hWindow);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HWindowImage), "显示图像失败.", ex);
            }
        }

        /// <summary> 执行挂起的布局与 SetPart（图像尺寸变了才重新布局）。调用方负责先确认 <see cref="CanDraw"/>。 </summary>
        private void ApplyPendingFit()
        {
            if (_pendingLayout)
            {
                _pendingLayout = false;
                if (getInfo.width.D != zoomInfo.width.D || getInfo.height.D != zoomInfo.height.D)
                {
                    LayoutControlToImage(getInfo);
                }
            }

            if (_pendingSetPart)
            {
                _pendingSetPart = false;
                HOperatorSet.SetPart(_hWindow, 0, 0, getInfo.height - 1, getInfo.width - 1);
            }
        }

        /// <summary> 按图像宽高比把 HWindowControl 缩放并居中到父容器内（改的是控件布局，不是图像） </summary>
        /// <remarks> 原名 <c>Fun_ZoomImage</c>，名字像是在缩放图像，实际只动控件的 Width/Height/Location。 </remarks>
        private void LayoutControlToImage(ZoomImage info)
        {
            if (_disposed || _hWindowControl == null || _hWindowControl.IsDisposed) return;
            if (_hWindowControl.Parent == null) return;
            if (info == null) return;

            // 防御除零：父容器尚未布局时宽高可能为 0
            int parentW = _hWindowControl.Parent.Width;
            int parentH = _hWindowControl.Parent.Height;
            double imgW = info.width.D;
            double imgH = info.height.D;
            if (parentW <= 0 || parentH <= 0 || imgW <= 0 || imgH <= 0) return;

            _inLayout = true;
            try
            {
                if ((imgW / parentW) < (imgH / parentH))
                {
                    _hWindowControl.Width = (int)(imgW * parentH / imgH);
                    _hWindowControl.Height = parentH;
                    _hWindowControl.Location = new System.Drawing.Point((parentW - _hWindowControl.Width) / 2, 0);
                }
                else
                {
                    _hWindowControl.Height = (int)(imgH * parentW / imgW);
                    _hWindowControl.Width = parentW;
                    _hWindowControl.Location = new System.Drawing.Point(0, (parentH - _hWindowControl.Height) / 2);
                }
                zoomInfo.width = info.width;
                zoomInfo.height = info.height;
                HOperatorSet.ClearWindow(_hWindow);
                // 只在首次布局时给默认值：原先每次换尺寸都强设 margin，覆盖调用方 SetDraw("fill")，
                // 违背 HDisplay「未指定的项沿用窗口当前状态」的约定
                if (!_drawModeInitialised)
                {
                    HOperatorSet.SetDraw(_hWindow, "margin");
                    _drawModeInitialised = true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HWindowImage), "缩放图像失败.", ex);
            }
            finally
            {
                _inLayout = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_hWindowControl != null && !_hWindowControl.IsDisposed)
            {
                _hWindowControl.Resize -= HWindowControl_Resize;
                _hWindowControl.VisibleChanged -= HWindowControl_VisibleChanged;
            }

            // 先置空引用再释放：HoImage 对外暴露，置空后后续读取拿到的是 null，
            // 而不是一个已释放的句柄。
            HObject img = _hoImage;
            _hoImage = null;
            try { if (img is object) img.Dispose(); }
            catch (Exception ex) { Log.Warn(nameof(HWindowImage), "释放图像失败.", ex); }

            GC.SuppressFinalize(this);
        }
    }
}
