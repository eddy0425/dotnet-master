using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.HalconCore;
using DotNet.HalconUI.Draw;
using DotNet.VisionRuntime;

namespace DotNet.HalconUI
{
    public class HDisplay : IHDisplay
    {
        bool _disposed;
        // default(HColor).Name 返回空字符串，不存在 null 语义，无需额外初始化
        HColor _color;

        readonly HWindow _hWindow;
        readonly IHWindowFont _hWindowFont;
        readonly HWindowImage _hWindowImage;
        readonly RoiInteraction _roi;

        public bool IsCross { get; set; }           //是否画十字
        public bool Adaptive { get; set; } = true;   //自适应
        public double HoWidth => _hWindowImage?.HoWidth ?? 0;
        public double HoHeight => _hWindowImage?.HoHeight ?? 0;

        /// <summary>
        /// 当前图像。句柄由 <see cref="HWindowImage"/> 独家持有（审查项 C16），本类只转发引用，
        /// 调用方拿到的是借用句柄，不得释放。
        /// </summary>
        public HObject HoImage => _hWindowImage?.HoImage;

        /// <summary>当前图像尺寸。</summary>
        public Size2d HoSize => new Size2d(HoWidth, HoHeight);

        /// <summary>当前图像中心点 (X, Y)。</summary>
        public Point2d HoCentre => new Point2d(HoWidth / 2, HoHeight / 2);

        public HDisplay(HWindowControl hWindowControl) : this(hWindowControl, null) { }

        /// <param name="font">文本实现；为 null 时按窗口图形栈自动选择（<see cref="HWindowFonts.Create"/>）。</param>
        public HDisplay(HWindowControl hWindowControl, IHWindowFont font)
        {
            if (hWindowControl == null) throw new ArgumentNullException(nameof(hWindowControl));

            _hWindow = hWindowControl.HalconWindow;
            _hWindowFont = font ?? HWindowFonts.Create(_hWindow);
            _hWindowImage = new HWindowImage(hWindowControl);
            _hWindowImage.Redisplayed += (s, e) => ReplayOverlay();
            _roi = new RoiInteraction(_hWindow, IsWindowUsable);

            // 用占位灰图初始化窗口，避免首帧到来前窗口处于未设置 Part 的状态。
            // HWindowImage 接管时会 CopyImage，所以这里 using 释放是安全的。
            using (HImage hImage = new HImage("byte", 800, 600))
            {
                DispImage(hImage);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 图像句柄由 _hWindowImage 独家持有（审查项 C16），随它一起释放。
            // 注意：hWindow / _hWindowControl 由宿主 UserControl (HDisplayUI) 通过 Designer 的 Dispose(bool) 负责释放，本类不再主动释放，避免双重释放。

            try { _hWindowImage?.Dispose(); } catch { /* swallow: release-time best effort */ }

            var overlay = _overlay;
            _overlay = null;
            try { overlay?.Dispose(); } catch { /* swallow: release-time best effort */ }

            GC.SuppressFinalize(this);
        }

        #region 叠加层

        // 运行结果的叠加层: 本类拥有。图像重画之后 (缩放 / 平移 / 双击复位 / 尺寸变化 / ReDispImage) 都重放它,
        // 换新图 (DispImage) 时丢弃 —— 叠加层是对旧图算出来的。
        OverlayList _overlay;

        /// <summary> 当前保留的叠加层；没有时为 null。借用，不得释放 </summary>
        public OverlayList Overlay => _overlay;

        /// <summary>
        /// 换上一份叠加层并接管它的所有权（旧的随之释放），然后重画图像与叠加层。传 null 等于 <see cref="ClearOverlay"/>。
        /// </summary>
        public void SetOverlay(OverlayList overlay)
        {
            if (_disposed)
            {
                overlay?.Dispose();
                return;
            }
            ReplaceOverlay(overlay);
            Redraw();
        }

        /// <summary> 清掉叠加层，只留图像 </summary>
        public void ClearOverlay() => SetOverlay(null);

        /// <summary>
        /// 显示一帧运行结果：底图 + 叠加层（接管所有权）。底图就是当前图像时不重新复制。
        /// </summary>
        public void ShowFrame(HObject image, OverlayList overlay)
        {
            if (_disposed)
            {
                overlay?.Dispose();
                return;
            }
            if (image.NotNull() && !ReferenceEquals(image, HoImage)) DispImage(image);
            SetOverlay(overlay);
        }

        void ReplaceOverlay(OverlayList overlay)
        {
            var old = _overlay;
            _overlay = overlay;
            if (!ReferenceEquals(old, overlay)) old?.Dispose();
        }

        /// <summary> 清窗后重画图像；叠加层经 <see cref="HWindowImage.Redisplayed"/> 重放 </summary>
        void Redraw()
        {
            if (!IsWindowUsable()) return;
            try { _hWindow.ClearWindow(); }
            catch (Exception ex) { Log.Warn(nameof(HDisplay), "清窗失败.", ex); }
            _hWindowImage?.Fun_ReDisplay();
        }

        void ReplayOverlay()
        {
            if (_overlay == null || !IsWindowUsable()) return;
            _overlay.DrawTo(this);
        }

        #endregion

        /// <summary>
        /// DispCvRegion 显示时叠加到 Row 上的像素偏移。
        /// </summary>
        /// <remarks>
        /// 这两个偏移是从原实现原样保留下来的经验值，仅用于"显示"，不参与任何几何计算。
        /// 需要说明的是：它们并非对称（Row 加 0.5、Column 加 1），原代码没有留下任何推导依据，
        /// 按 HALCON 的像素中心约定，理论上两个方向应当取同一个值（0.5）。
        /// 这里先提为具名常量，使其可被检索、可被讨论；是否修正为对称值需要用实际图像比对后再定，
        /// 因此本次不改变数值，避免在没有验证手段的情况下引入肉眼可见的位移。
        /// </remarks>
        const double DispRowOffset = 0.5;

        /// <summary> DispCvRegion 显示时叠加到 Column 上的像素偏移，见 <see cref="DispRowOffset"/> </summary>
        const double DispColOffset = 1;

        /// <summary> 窗口/控件是否仍可用于 Halcon 调用 </summary>
        bool IsWindowUsable()
        {
            if (_disposed) return false;
            if (_hWindow == null) return false;
            try
            {
                return _hWindow.IsInitialized();
            }
            catch
            {
                return false;
            }
        }

        /// <summary> 设置绘制模式 </summary>
        /// <param name="mode">"margin" 外接矩形, "fill" 填充矩形</param>
        public void SetDraw(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) mode = "margin";
            if (mode != "margin" && mode != "fill")
            {
                Log.Warn(nameof(HDisplay), $"未知绘制模式 '{mode}'，回退为 margin.");
                mode = "margin";
            }

            if (!IsWindowUsable()) return;

            try
            {
                HOperatorSet.SetDraw(_hWindow, mode);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "设置绘制模式失败.", ex);
            }
        }

        #region HWindowImage

        /// <summary>
        /// 设置图像：由 <see cref="HWindowImage"/> 复制一份并接管，避免持有外部对象的悬挂引用。
        /// </summary>
        /// <exception cref="ObjectDisposedException">本实例已释放</exception>
        /// <exception cref="ArgumentException"><paramref name="image"/> 为 null 或未初始化</exception>
        public void SetImage(HObject image)
        {
            // 原来这三处一律抛 NullReferenceException，用「意外的空引用」表达业务错误，
            // 现场看到的堆栈与真实原因对不上（审查项 D10）。
            if (_disposed) throw new ObjectDisposedException(nameof(HDisplay), "HDisplay已释放！");
            if (!image.NotNull()) throw new ArgumentException("图像为空或未初始化！", nameof(image));
            if (_hWindowImage == null) throw new InvalidOperationException("HWindowImage为空！");

            _hWindowImage.Fun_SetImage(image);
        }

        /// <summary>
        /// 显示图片：由 <see cref="HWindowImage"/> 复制一份并接管，避免持有外部对象的悬挂引用。
        /// </summary>
        /// <remarks>
        /// 是否重设显示区域取决于 <see cref="Adaptive"/>，因此不能写成
        /// <c>DispImage(image, true)</c> 这样的默认参数——那会在用户关掉自适应后仍强制重设 Part。
        /// </remarks>
        public void DispImage(HObject image)
        {
            // 原来这里与下面的双参重载逐字重复了 40 行，唯一差别就是 Adaptive / isSetPart。
            DispImage(image, Adaptive);
        }

        /// <summary> 显示图片 </summary>
        public void DispImage(HObject image, bool isSetPart)
        {
            if (_disposed) return;
            if (!image.NotNull()) return;
            if (_hWindowImage == null) return;

            try
            {
                _hWindowImage.Fun_DispImage(image, isSetPart);
                ReplaceOverlay(null);

                if (IsCross && IsWindowUsable())
                {
                    // 不能按 _color 缓存跳过：DrawRenderer 等会直接改窗口颜色（见 SetColor 注释），
                    // 缓存仍是 Red 时十字会沿用别人留下的颜色
                    SetColor(HColor.Red);

                    double w = HoWidth;
                    double h = HoHeight;
                    if (w > 0 && h > 0)
                    {
                        double size = w > h ? w : h;
                        try
                        {
                            HOperatorSet.DispCross(_hWindow, h / 2, w / 2, size, 0);
                        }
                        catch (Exception ex)
                        {
                            Log.Warn(nameof(HDisplay), "显示图像时叠加十字失败.", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "显示图像失败.", ex);
            }
        }

        /// <summary> 重新显示图片 </summary>
        public void ReDispImage()
        {
            if (_disposed) return;
            _hWindowImage?.Fun_ReDisplay();
        }

        #endregion


        #region IHWindowFont

        /// <summary> 设置字体大小 </summary>
        public void SetFontSize(HTuple hv_Size)
        {
            if (!IsWindowUsable() || _hWindowFont == null) return;
            try { _hWindowFont.SetFontSize(hv_Size); }
            catch (Exception ex) { Log.Warn(nameof(HDisplay), "设置字号失败.", ex); }
        }

        /// <summary>
        /// 显示文本。<paramref name="position"/> 为 (X=列, Y=行)。
        /// </summary>
        /// <remarks>
        /// <see cref="DrawStyle.Size"/> 表示字号：为 <c>null</c> 时沿用窗口当前字号
        /// （对应原来不带 size 的那个重载），指定时先 set_font_size 再输出。
        /// disp_text 的颜色是调用参数、不改画笔状态，因此这里不走 <see cref="SetColor"/>。
        /// </remarks>
        public void DispText(string message, Point2d position, DrawStyle style = null)
        {
            if (!IsWindowUsable() || _hWindowFont == null) return;
            try
            {
                if (style?.Size != null) _hWindowFont.SetFontSize(style.Size.Value);
                _hWindowFont.DispText(message, position.Y, position.X, ResolveTextColor(style).Name);
            }
            catch (Exception ex) { Log.Warn(nameof(HDisplay), "显示文本失败.", ex); }
        }

        /// <summary> 文本颜色：样式未指定则沿用当前画笔颜色，仍为空则回落红色 </summary>
        HColor ResolveTextColor(DrawStyle style)
        {
            HColor color = style?.Color ?? default(HColor);
            if (color.IsEmpty) color = GetColor();
            if (color.IsEmpty) color = HColor.Red;
            return color;
        }

        #endregion

        /// <summary> 获取当前颜色（未设置时为 <c>default(HColor)</c>，其 <see cref="HColor.Name"/> 为空字符串） </summary>
        public HColor GetColor()
        {
            return _color;
        }

        /// <summary>
        /// 设置画笔颜色。
        /// 设计要点：本方法被几乎所有 Disp* 显示重载在内部调用——一旦它抛硬异常，
        /// 整个上层调用栈会被打穿。这里改为"窗口不可用 → 静默忽略并记录日志"。
        /// 同一窗口还会被 DrawHelper 等组件直接修改颜色，因此不能根据本地缓存跳过调用。
        /// </summary>
        public void SetColor(HColor color)
        {
            // 未指定颜色时沿用历史行为：回落到红色（原实现是对 null 做同样处理）
            if (color.IsEmpty) color = HColor.Red;

            if (!IsWindowUsable())
            {
                _color = color;     // 即便窗口尚未就绪也缓存意图，下次窗口就绪即可恢复
                return;
            }

            try
            {
                _hWindow.SetColor(color.Name);
                _color = color;
            }
            catch (Exception ex)
            {
                Log.Warn(nameof(HDisplay), "设置颜色失败.", ex);
            }
        }

        public void ClearWinDisp(HObject objectVal)
        {
            if (!IsWindowUsable()) return;
            if (!objectVal.NotNull()) return;

            try
            {
                _hWindow.ClearWindow();
                _hWindow.DispObj(objectVal);
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "清空窗口失败.", ex);
            }
        }

        #region 图元绘制

        // 本区域统一遵循三条约定：
        // 1) 一种图元只有一个 Disp 重载，颜色/尺寸/线宽/填充模式全部通过 DrawStyle 传入；
        //    原来「不带 color」与「带 color」的成对重载（22 对）由 style == null 表达。
        // 2) 只有本区域最内层的方法接触 HALCON 的 (Row, Column) 顺序，对外一律 Point2d(X, Y)。
        // 3) 参数校验一律前置于任何副作用（含 ApplyStyle）；非法入参抛异常而不是静默 return。

        /// <summary> 应用样式中「已显式指定」的项；未指定的项保持窗口现状 </summary>
        void ApplyStyle(DrawStyle style)
        {
            if (style == null) return;
            if (!style.Color.IsEmpty) SetColor(style.Color);
            if (style.LineWidth.HasValue) SetLineWidth(style.LineWidth.Value);
            if (!string.IsNullOrEmpty(style.DrawMode)) SetDraw(style.DrawMode);
        }

        void SetLineWidth(int width)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "线宽必须为正数.");
            if (!IsWindowUsable()) return;
            try { HOperatorSet.SetLineWidth(_hWindow, width); }
            catch (Exception ex) { Log.Warn(nameof(HDisplay), "设置线宽失败.", ex); }
        }

        /// <summary> 画点（十字标记），<see cref="DrawStyle.Size"/> 为十字臂长 </summary>
        public void Disp(Point2d point, DrawStyle style = null)
        {
            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispCross(point.Y, point.X, DrawStyle.SizeOr(style), 0);
        }

        /// <summary> 批量画点 </summary>
        public void Disp(IReadOnlyList<Point2d> points, DrawStyle style = null)
        {
            if (points is null) throw new ArgumentNullException(nameof(points));

            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            double size = DrawStyle.SizeOr(style);
            for (int i = 0; i < points.Count; i++)
            {
                _hWindow.DispCross(points[i].Y, points[i].X, size, 0);
            }
        }

        /// <summary> 画坐标系：带方向角的十字。<c>CvCoord.Angle</c> 是强类型角度，取 Radians 交给 Halcon </summary>
        public void Disp(CvCoord coord, DrawStyle style = null)
        {
            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispCross(coord.Y, coord.X, DrawStyle.SizeOr(style), coord.Angle.Radians);
        }

        /// <summary> 画线段 </summary>
        public void Disp(CvLine line, DrawStyle style = null)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));

            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispLine(line.Start.Y, line.Start.X, line.End.Y, line.End.X);
        }

        /// <summary> 画箭头，<see cref="DrawStyle.Size"/> 覆盖 <c>CvArrow.HeadSize</c> </summary>
        public void Disp(CvArrow arrow, DrawStyle style = null)
        {
            if (arrow is null) throw new ArgumentNullException(nameof(arrow));

            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispArrow(arrow.Start.Y, arrow.Start.X, arrow.End.Y, arrow.End.X,
                               DrawStyle.SizeOr(style, arrow.HeadSize));
        }

        /// <summary> 画圆 </summary>
        public void Disp(CvCircle circle, DrawStyle style = null)
        {
            if (circle is null) throw new ArgumentNullException(nameof(circle));

            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispCircle(circle.Center.Y, circle.Center.X, circle.Radius);
        }

        /// <summary> 线段 + 末端圆标记；圆恒为红色，沿用历史行为 </summary>
        public void DispLineWithEndMarker(CvLine line, double markerRadius, DrawStyle style = null)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));
            if (markerRadius <= 0) throw new ArgumentOutOfRangeException(nameof(markerRadius), markerRadius, "标记半径必须为正数.");

            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            _hWindow.DispLine(line.Start.Y, line.Start.X, line.End.Y, line.End.X);

            SetColor(HColor.Red);
            _hWindow.DispCircle(line.End.Y, line.End.X, markerRadius);
        }

        /// <summary> 线段 + 两端十字标记 </summary>
        public void DispSegmentWithCrosses(Point2d start, Point2d end, double armLength, DrawStyle style = null)
        {
            if (armLength <= 0) throw new ArgumentOutOfRangeException(nameof(armLength), armLength, "十字臂长必须为正数.");

            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            DispStepCross(start, armLength);
            DispStepCross(end, armLength);

            HOperatorSet.DispLine(_hWindow, start.Y, start.X, end.Y, end.X);
        }

        /// <summary> 以 point 为中心画一个臂长为 arm 的十字（两条正交线段） </summary>
        void DispStepCross(Point2d point, double arm)
        {
            HOperatorSet.DispLine(_hWindow, point.Y, point.X - arm, point.Y, point.X + arm);
            HOperatorSet.DispLine(_hWindow, point.Y - arm, point.X, point.Y + arm, point.X);
        }

        #endregion

        #region 交互绘制

        // 交互绘制的流程在 RoiInteraction 里: 本类只管显示与画笔状态。
        // 用户取消 / 超时一律不改动传入的 CvRegion, 返回 false。

        /// <summary> 交互式新建区域；用户确认并写回几何返回 true，取消 / 超时返回 false 且不改动 <paramref name="hRegion"/> </summary>
        public Task<bool> DrawRegionAsync(CvRegion hRegion) => _roi.DrawAsync(hRegion);

        /// <summary> 交互式修改区域：以现有几何为初值进入编辑；语义同 <see cref="DrawRegionAsync(CvRegion)"/> </summary>
        public Task<bool> DrawRegionModAsync(CvRegion hRegion) => _roi.ModifyAsync(hRegion);

        /// <summary>
        /// 交互式新建指定类型的区域，所有权归调用方。取消 / 超时返回<b>空对象元组</b>（不是 null，也不是空区域）。
        /// </summary>
        public Task<HObject> DrawRegionAsync(RectEnum type) => _roi.DrawAsync(type);

        #endregion

        #region Region

        /// <summary> 显示 HALCON 对象（区域 / 轮廓） </summary>
        public void Disp(HObject region, DrawStyle style = null)
        {
            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            if (region.NotNull())
                _hWindow.DispObj(region);
        }

        /// <summary> 显示 ROI 已生成的区域对象（<c>CvRegion.HoRegion</c>） </summary>
        public void Disp(CvRegion region, DrawStyle style = null)
        {
            // 原实现直接解引用 region.HoRegion，region 为 null 时是 NRE 而不是可忽略的空绘制。
            if (region == null) return;

            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            if (region.HoRegion.NotNull())
                _hWindow.DispObj(region.HoRegion);
        }

        /// <summary>
        /// 按 ROI 的几何参数绘制轮廓（原 <c>DispCvRegion</c>）。
        /// </summary>
        /// <remarks>
        /// 与 <see cref="Disp(CvRegion, DrawStyle)"/> 的区别：后者显示已实体化的 HoRegion，
        /// 本方法直接按 Type/Center/Width... 用 <c>disp_*</c> 画轮廓，不依赖 HoRegion。
        /// 原来两者签名相同、命名不体现差异（DispRegion / DispCvRegion），这里改名以示区分。
        /// <para>
        /// 修复点（Ring 分支）：
        /// 1) 原代码 <c>new HObject(); GenEmptyObj(out circle1);</c> 会立刻覆盖第一次创建的句柄→泄漏；
        ///    之后 <c>GenCircle(out circle1, …)</c> 又再次覆盖了 GenEmptyObj 的句柄。
        ///    本次改为直接使用 <c>GenCircle(out …)</c>，并用 finally 兜底释放。
        /// 2) "Display" 方法不应有副作用——原代码偷偷改了 <c>region.HoRegion</c>，
        ///    让调用方拿不到原始几何对象的所有权。现在只显示，差集 region 用完即弃。
        /// </para>
        /// </remarks>
        public void DispRegionOutline(CvRegion region, DrawStyle style = null)
        {
            if (region == null) return;

            ApplyStyle(style);
            if (!IsWindowUsable()) return;

            try
            {
                switch (region.Type)
                {
                    case RectEnum.Rectangle:
                        HOperatorSet.DispRectangle1(_hWindow, region.Top + DispRowOffset, region.Left + DispColOffset,
                                                            region.Bottom, region.Right);
                        break;
                    case RectEnum.AffRect:
                        HOperatorSet.DispRectangle2(_hWindow, region.CenterY + DispRowOffset, region.CenterX + DispColOffset, region.Phi,
                                                 region.Width / 2, region.Height / 2);
                        break;
                    case RectEnum.Circle:
                        HOperatorSet.DispCircle(_hWindow, region.CenterY + DispRowOffset, region.CenterX + DispColOffset, region.Width / 2);
                        break;
                    case RectEnum.Ellipse:
                        HOperatorSet.DispEllipse(_hWindow, region.CenterY + DispRowOffset, region.CenterX + DispColOffset, region.Phi,
                                                 region.Width / 2, region.Height / 2);
                        break;
                    case RectEnum.Polygon:
                        // disp_polygon(Window, Row, Column)：首参为 Row，而 PolygonX/PolygonY 分别存 Column/Row
                        HOperatorSet.DispPolygon(_hWindow, region.PolygonY, region.PolygonX);
                        break;
                    case RectEnum.Ring:
                        DispRingInternal(region);
                        break;
                    default:
                        throw new NotSupportedException("DispRegionOutline: 不支持的 ROI 类型: " + region.Type);
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "DispRegionOutline 失败.", ex);
            }
        }

        void DispRingInternal(CvRegion hRegion)
        {
            // 只显示：差集区域用完即弃，不碰 hRegion.HoRegion
            using (var ring = RegionShapes.GenRing(hRegion.CenterY + DispRowOffset, hRegion.CenterX + DispColOffset,
                                                   hRegion.MaxRadius, hRegion.MinRadius))
            {
                _hWindow.DispObj(ring);
            }
        }

        /// <summary> 画有向矩形。<paramref name="phi"/> 为弧度，length1/length2 为两个方向的半长 </summary>
        public void DispRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
        {
            ApplyStyle(style);
            if (!IsWindowUsable()) return;
            _hWindow.DispRectangle2(center.Y, center.X, phi, length1, length2);
        }

        #endregion

    }
}
