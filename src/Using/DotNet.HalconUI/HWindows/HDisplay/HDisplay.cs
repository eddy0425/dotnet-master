using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.HalconCore;
using DotNet.HalconUI.Draw;

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

        public HDisplay(HWindowControl hWindowControl)
        {
            if (hWindowControl == null) throw new ArgumentNullException(nameof(hWindowControl));

            _hWindow = hWindowControl.HalconWindow;
            _hWindowFont = HWindowFonts.Create(_hWindow);
            _hWindowImage = new HWindowImage(hWindowControl);

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

            GC.SuppressFinalize(this);
        }

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

        #region 区域相关

        /// <summary> 显示橡皮筋区域 </summary>
        public void DispGenRegion(CvRegion hRegion)
        {
            if (!IsWindowUsable() || hRegion == null) return;

            try
            {
                hRegion.RebuildRegion();
                if (hRegion.HoRegion.NotNull())
                {
                    _hWindow.DispObj(hRegion.HoRegion);
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "生成并显示区域失败.", ex);
            }
        }

        /// <summary> 获取坐标区域并显示 </summary>
        public void GenCoordsRegion(CvRegion hRegion, List<CvCoord> coords)
        {
            if (!IsWindowUsable() || hRegion == null || coords == null) return;

            try
            {
                hRegion.GenCoordsRegion(coords);
                Disp(hRegion.HoRegion, DrawStyle.Of(HColor.Green));
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "由坐标生成区域失败.", ex);
            }
        }

        #endregion

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

        #region Draw Region

        /// <summary> 交互式新建区域 </summary>
        /// <remarks>
        /// 用户取消 / 超时（结果的 <c>Completed</c> 为 false）时直接返回，<b>不改动</b> <paramref name="hRegion"/>。
        /// 旧的阻塞实现不区分这一点，新建时取消会把全零几何写回去，把 ROI 变成 (0,0,0,0)。
        /// </remarks>
        /// <returns>
        /// 用户确认并写回几何返回 true；取消 / 超时 / 窗口不可用 / 绘制异常返回 false。
        /// 调用方据此决定要不要执行有副作用的后续动作（典型如重建模板）。
        /// </returns>
        public async Task<bool> DrawRegionAsync(CvRegion hRegion)
        {
            if (!IsWindowUsable() || hRegion == null) return false;

            DrawHelper.CancelDraw(_hWindow);

            try
            {
                switch (hRegion.Type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1Async(_hWindow);
                            if (!r.Completed) return false;
                            ApplyRect1(hRegion, r.Row1, r.Column1, r.Row2, r.Column2);
                            return true;
                        }
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2Async(_hWindow);
                            if (!r.Completed) return false;
                            ApplyRect2(hRegion, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                            return true;
                        }
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleAsync(_hWindow);
                            if (!r.Completed) return false;
                            ApplyCircle(hRegion, r.Row, r.Column, r.Radius);
                            return true;
                        }
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseAsync(_hWindow);
                            if (!r.Completed) return false;
                            ApplyEllipse(hRegion, r.Row, r.Column, r.Phi, r.Radius1, r.Radius2);
                            return true;
                        }
                    case RectEnum.Polygon:
                        return await DrawPolygonIntoAsync(hRegion);
                    case RectEnum.Ring:
                        return await DrawRingIntoAsync(hRegion, modify: false);
                    default:
                        throw new NotSupportedException($"DrawRegionAsync: 不支持的 ROI 类型: {hRegion.Type}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "DrawRegionAsync 失败.", ex);
                return false;
            }
        }

        /// <summary> 交互式修改区域：以现有几何为初值进入编辑 </summary>
        /// <remarks>取消 / 超时同样保持 <paramref name="hRegion"/> 原样，见 <see cref="DrawRegionAsync(CvRegion)"/>。</remarks>
        /// <returns>语义同 <see cref="DrawRegionAsync(CvRegion)"/>。</returns>
        public async Task<bool> DrawRegionModAsync(CvRegion hRegion)
        {
            if (!IsWindowUsable() || hRegion == null) return false;

            DrawHelper.CancelDraw(_hWindow);

            try
            {
                switch (hRegion.Type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1ModAsync(_hWindow,
                                hRegion.Top, hRegion.Left, hRegion.Bottom, hRegion.Right);
                            if (!r.Completed) return false;
                            ApplyRect1(hRegion, r.Row1, r.Column1, r.Row2, r.Column2);
                            return true;
                        }
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2ModAsync(_hWindow,
                                hRegion.CenterY, hRegion.CenterX, hRegion.Phi.D,
                                hRegion.Width / 2, hRegion.Height / 2);
                            if (!r.Completed) return false;
                            ApplyRect2(hRegion, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                            return true;
                        }
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleModAsync(_hWindow,
                                hRegion.CenterY, hRegion.CenterX, hRegion.Width / 2);
                            if (!r.Completed) return false;
                            ApplyCircle(hRegion, r.Row, r.Column, r.Radius);
                            return true;
                        }
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseModAsync(_hWindow,
                                hRegion.CenterY, hRegion.CenterX, hRegion.Phi.D,
                                hRegion.Width / 2, hRegion.Height / 2);
                            if (!r.Completed) return false;
                            ApplyEllipse(hRegion, r.Row, r.Column, r.Phi, r.Radius1, r.Radius2);
                            return true;
                        }
                    case RectEnum.Polygon:
                        return await DrawPolygonIntoAsync(hRegion);
                    case RectEnum.Ring:
                        return await DrawRingIntoAsync(hRegion, modify: true);
                    default:
                        throw new NotSupportedException($"DrawRegionModAsync: 不支持的 ROI 类型: {hRegion.Type}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "DrawRegionModAsync 失败.", ex);
                return false;
            }
        }

        /// <summary>
        /// 把新生成的 HObject 转移到 <paramref name="hRegion"/> 的 HoRegion 上：
        /// 先把 <paramref name="created"/> 置 null 取走所有权，再赋值（由 HoRegion 的 setter 释放旧句柄），
        /// 这样即使释放旧句柄时抛异常，调用方的 finally 也不会再次释放新句柄（即"所有权已转移"语义）。
        /// </summary>
        static void ReplaceRegion(CvRegion hRegion, ref HObject created)
        {
            if (hRegion == null || created == null) return;
            var handle = created;
            created = null;
            hRegion.HoRegion = handle;
        }

        #region 结果写回

        // 下面 4 个 Apply* 负责"绘制结果 → CvRegion"的写回：生成 HObject、写外接框/角度、转移所有权。
        // 新建与修改两条路径的写回逻辑完全一致，抽出来避免 8 处复制。
        // 只在 Completed == true 时调用；参数是 double，HTuple 有隐式转换，调用点无需再包一层。

        static void ApplyRect1(CvRegion hRegion, double row1, double column1, double row2, double column2)
        {
            HObject rectangle = null;
            try
            {
                HOperatorSet.GenRectangle1(out rectangle, row1, column1, row2, column2);
                hRegion.SetRectByCorners(row1, column1, row2, column2);
                ReplaceRegion(hRegion, ref rectangle);
            }
            finally { rectangle?.Dispose(); }
        }

        static void ApplyRect2(CvRegion hRegion, double row, double column, double phi, double length1, double length2)
        {
            HObject rectangle = null;
            try
            {
                HOperatorSet.GenRectangle2(out rectangle, row, column, phi, length1, length2);
                hRegion.SetRectByCenter(new Point2d(column, row), new Size2d(length1 * 2, length2 * 2));
                hRegion.Phi = phi;
                ReplaceRegion(hRegion, ref rectangle);
            }
            finally { rectangle?.Dispose(); }
        }

        static void ApplyCircle(CvRegion hRegion, double row, double column, double radius)
        {
            HObject circle = null;
            try
            {
                HOperatorSet.GenCircle(out circle, row, column, radius);
                hRegion.SetRectByCenter(new Point2d(column, row), new Size2d(radius * 2, radius * 2));
                ReplaceRegion(hRegion, ref circle);
            }
            finally { circle?.Dispose(); }
        }

        static void ApplyEllipse(CvRegion hRegion, double row, double column, double phi, double radius1, double radius2)
        {
            HObject ellipse = null;
            try
            {
                HOperatorSet.GenEllipse(out ellipse, row, column, phi, radius1, radius2);
                hRegion.SetRectByCenter(new Point2d(column, row), new Size2d(radius1 * 2, radius2 * 2));
                hRegion.Phi = phi;
                ReplaceRegion(hRegion, ref ellipse);
            }
            finally { ellipse?.Dispose(); }
        }

        #endregion

        /// <summary>
        /// 多边形 ROI 的共享实现：DrawRegionAsync / DrawRegionModAsync 都走这里。
        /// 使用 try/finally 兜底，避免 GetRegionPolygon 或 AreaCenter 出错时 region 句柄泄漏。
        /// </summary>
        async Task<bool> DrawPolygonIntoAsync(CvRegion hRegion)
        {
            var result = await DrawHelper.DrawRegionAsync(_hWindow);

            // Region 的所有权已交到这里，无论确认与否都由本方法负责释放（未确认时是空区域）
            HObject region = result.Region;
            try
            {
                if (!result.Completed || !region.NotNull()) return false;

                HOperatorSet.GetRegionPolygon(region, 1, out HTuple rows, out HTuple columns);
                HOperatorSet.SmallestRectangle1(region, out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);

                hRegion.PolygonX = columns;
                hRegion.PolygonY = rows;
                // 与其它 ROI 类型一样写外接框（Center 随之为外接框中心）。原先只设 Center(面积重心)，
                // 而 Center 的 setter 只平移 X/Y，新建 ROI 的 Width/Height 一直是 0。
                hRegion.SetRectByCorners(row1.D, column1.D, row2.D, column2.D);

                ReplaceRegion(hRegion, ref region);
                return true;
            }
            finally
            {
                region?.Dispose();
            }
        }

        /// <summary>
        /// 生成同心圆环区域 (外圆减内圆). 调用方获得返回句柄的所有权.
        /// 内外半径顺序不敏感: 自动取大者为外圆.
        /// </summary>
        static HObject GenRing(HTuple row, HTuple column, double radiusA, double radiusB)
        {
            double outer = Math.Max(radiusA, radiusB);
            double inner = Math.Min(radiusA, radiusB);

            HObject outerCircle = null;
            HObject innerCircle = null;
            try
            {
                HOperatorSet.GenCircle(out outerCircle, row, column, outer);
                HOperatorSet.GenCircle(out innerCircle, row, column, inner);
                HObject ring;
                HOperatorSet.Difference(outerCircle, innerCircle, out ring);
                return ring;
            }
            finally
            {
                outerCircle?.Dispose();
                innerCircle?.Dispose();
            }
        }

        /// <summary>
        /// 圆环 ROI 的共享实现: DrawRegionAsync / DrawRegionModAsync 都走这里.
        /// 交互分两步 —— 先画(或调整)外圆, 再调整内圆半径.
        /// 第二步用外圆的圆心作为内圆圆心, 强制保持同心; 用户在第二步移动圆心的操作会被忽略,
        /// 因为 <see cref="CvRegion"/> 的圆环模型 (MaxRadius / MinRadius) 只能表达同心圆环.
        /// </summary>
        /// <remarks>
        /// 两步交互中任意一步被取消都整次放弃：第一步取消就没有圆心可供第二步定位，
        /// 第二步取消则只有外圆、构不成圆环，此时保留 <paramref name="hRegion"/> 原样比写半成品更安全。
        /// </remarks>
        async Task<bool> DrawRingIntoAsync(CvRegion hRegion, bool modify)
        {
            DrawCircleResult outerResult = modify
                ? await DrawHelper.DrawCircleModAsync(_hWindow, hRegion.CenterY, hRegion.CenterX, hRegion.MaxRadius)
                : await DrawHelper.DrawCircleAsync(_hWindow);
            if (!outerResult.Completed) return false;

            // 新建时给内圆一个可见的初值(外圆一半), 修改时沿用已有的 MinRadius.
            double innerSeed = modify ? hRegion.MinRadius : outerResult.Radius / 2;
            var innerResult = await DrawHelper.DrawCircleModAsync(_hWindow,
                outerResult.Row, outerResult.Column, innerSeed);
            if (!innerResult.Completed) return false;

            double outer = Math.Max(outerResult.Radius, innerResult.Radius);
            double inner = Math.Min(outerResult.Radius, innerResult.Radius);

            HObject ring = GenRing(outerResult.Row, outerResult.Column, outer, inner);
            try
            {
                // 外接框按外圆直径写入, 保证 Width/Height/BoundingBox 与其它 ROI 类型语义一致.
                hRegion.SetRectByCenter(new Point2d(outerResult.Column, outerResult.Row), new Size2d(outer * 2, outer * 2));
                hRegion.MaxRadius = outer;
                hRegion.MinRadius = inner;
                hRegion.RingWidth = outer - inner;
                ReplaceRegion(hRegion, ref ring);
            }
            finally { ring?.Dispose(); }

            return true;
        }

        /// <summary> 交互式新建指定类型的区域，直接返回结果 </summary>
        /// <returns>
        /// 新绘制的区域，所有权归调用方，用完必须 <c>Dispose</c>。
        /// <para>
        /// 取消 / 超时 / 窗口不可用返回<b>空对象元组</b>（<c>gen_empty_obj</c>，<c>count_obj == 0</c>），
        /// 不会是 null。注意它<b>不是</b>“空区域”(<c>gen_empty_region</c>, <c>count_obj == 1</c>)：
        /// 把 0 长度的对象元组喂给 <c>union2</c> / <c>difference</c> 并非无操作，而是会报错或返回空结果。
        /// 调用方必须先用 <c>CountObj</c> 判空并短路，参见 <c>HEditModelUI.DrawROIAsync</c>。
        /// </para>
        /// </returns>
        /// <remarks>
        /// 修复点：原实现先 <c>GenEmptyObj(out rectangle)</c> 再被各 case 的 <c>GenXxx(out rectangle, …)</c> 覆盖，
        /// 第一次创建的空 HObject 句柄丢失 → 句柄泄漏。
        /// 现在改为先在 case 内创建临时变量，全部成功后再赋给返回值；
        /// 任何失败路径都会保证返回的是一个合法（可 Dispose）的空对象。
        /// </remarks>
        public async Task<HObject> DrawRegionAsync(RectEnum type)
        {
            HOperatorSet.GenEmptyObj(out HObject rectangle);
            if (!IsWindowUsable()) return rectangle;

            DrawHelper.CancelDraw(_hWindow);

            HObject created = null;
            try
            {
                switch (type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1Async(_hWindow);
                            if (r.Completed)
                                HOperatorSet.GenRectangle1(out created, r.Row1, r.Column1, r.Row2, r.Column2);
                        }
                        break;
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2Async(_hWindow);
                            if (r.Completed)
                                HOperatorSet.GenRectangle2(out created, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                        }
                        break;
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleAsync(_hWindow);
                            if (r.Completed)
                                HOperatorSet.GenCircle(out created, r.Row, r.Column, r.Radius);
                        }
                        break;
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseAsync(_hWindow);
                            if (r.Completed)
                                HOperatorSet.GenEllipse(out created, r.Row, r.Column, r.Phi, r.Radius1, r.Radius2);
                        }
                        break;
                    case RectEnum.Polygon:
                        {
                            var r = await DrawHelper.DrawRegionAsync(_hWindow);
                            // 未确认时拿到的是空区域, 就地释放; 确认则直接接管所有权
                            if (r.Completed) created = r.Region;
                            else r.Region?.Dispose();
                        }
                        break;
                    case RectEnum.Ring:
                        {
                            var outerResult = await DrawHelper.DrawCircleAsync(_hWindow);
                            if (!outerResult.Completed) break;

                            var innerResult = await DrawHelper.DrawCircleModAsync(_hWindow,
                                outerResult.Row, outerResult.Column, outerResult.Radius / 2);
                            if (!innerResult.Completed) break;

                            created = GenRing(outerResult.Row, outerResult.Column,
                                outerResult.Radius, innerResult.Radius);
                        }
                        break;
                    default:
                        throw new NotSupportedException($"DrawRegionAsync: 不支持的 ROI 类型: {type}");
                }

                if (created.NotNull())
                {
                    rectangle.Dispose();    // 释放占位的空对象
                    rectangle = created;
                    created = null;         // 所有权已转移
                }
            }
            catch (Exception ex)
            {
                Log.Error(nameof(HDisplay), "交互绘制区域失败.", ex);
            }
            finally
            {
                // 失败路径或中途异常下未转移所有权的对象兜底释放
                created?.Dispose();
            }

            return rectangle;
        }

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
            HObject circle1 = null;
            HObject circle2 = null;
            HObject regionDifference = null;
            try
            {
                HOperatorSet.GenCircle(out circle1, hRegion.CenterY + DispRowOffset, hRegion.CenterX + DispColOffset, hRegion.MaxRadius);
                HOperatorSet.GenCircle(out circle2, hRegion.CenterY + DispRowOffset, hRegion.CenterX + DispColOffset, hRegion.MinRadius);
                HOperatorSet.Difference(circle1, circle2, out regionDifference);
                _hWindow.DispObj(regionDifference);
            }
            finally
            {
                circle1?.Dispose();
                circle2?.Dispose();
                regionDifference?.Dispose();
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
