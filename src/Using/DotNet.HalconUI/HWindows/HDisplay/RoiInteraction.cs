using DotNet.Drawing;
using DotNet.HalconUI.Draw;
using HalconDotNet;
using System;
using System.Threading.Tasks;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 在一个 HALCON 窗口上交互式绘制 / 修改 ROI，并把确认后的几何写回 <see cref="CvRegion"/>。
    /// </summary>
    /// <remarks>
    /// 从 <see cref="HDisplay"/> 拆出：HDisplay 只管显示与画笔状态，交互绘制的流程（发起会话、等待确认、
    /// 写回几何、转移句柄所有权）都在这里。用户取消 / 超时一律不改动传入的 <see cref="CvRegion"/>。
    /// </remarks>
    internal sealed class RoiInteraction
    {
        private readonly HWindow _window;
        private readonly Func<bool> _isUsable;

        internal RoiInteraction(HWindow window, Func<bool> isUsable)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _isUsable = isUsable ?? throw new ArgumentNullException(nameof(isUsable));
        }

        /// <summary> 交互式新建区域 </summary>
        /// <returns>用户确认并写回几何返回 true；取消 / 超时 / 窗口不可用 / 绘制异常返回 false。</returns>
        internal async Task<bool> DrawAsync(CvRegion hRegion)
        {
            if (!_isUsable() || hRegion == null) return false;

            DrawHelper.CancelDraw(_window);

            try
            {
                switch (hRegion.Type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1Async(_window);
                            if (!r.Completed) return false;
                            ApplyRect1(hRegion, r.Row1, r.Column1, r.Row2, r.Column2);
                            return true;
                        }
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2Async(_window);
                            if (!r.Completed) return false;
                            ApplyRect2(hRegion, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                            return true;
                        }
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleAsync(_window);
                            if (!r.Completed) return false;
                            ApplyCircle(hRegion, r.Row, r.Column, r.Radius);
                            return true;
                        }
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseAsync(_window);
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
        /// <returns>语义同 <see cref="DrawAsync(CvRegion)"/>。</returns>
        internal async Task<bool> ModifyAsync(CvRegion hRegion)
        {
            if (!_isUsable() || hRegion == null) return false;

            DrawHelper.CancelDraw(_window);

            try
            {
                switch (hRegion.Type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1ModAsync(_window,
                                hRegion.Top, hRegion.Left, hRegion.Bottom, hRegion.Right);
                            if (!r.Completed) return false;
                            ApplyRect1(hRegion, r.Row1, r.Column1, r.Row2, r.Column2);
                            return true;
                        }
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2ModAsync(_window,
                                hRegion.CenterY, hRegion.CenterX, hRegion.Phi.D,
                                hRegion.Width / 2, hRegion.Height / 2);
                            if (!r.Completed) return false;
                            ApplyRect2(hRegion, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                            return true;
                        }
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleModAsync(_window,
                                hRegion.CenterY, hRegion.CenterX, hRegion.Width / 2);
                            if (!r.Completed) return false;
                            ApplyCircle(hRegion, r.Row, r.Column, r.Radius);
                            return true;
                        }
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseModAsync(_window,
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

        /// <summary> 交互式新建指定类型的区域，直接返回结果 </summary>
        /// <returns>
        /// 新绘制的区域，所有权归调用方。取消 / 超时 / 窗口不可用返回<b>空对象元组</b>（<c>count_obj == 0</c>），
        /// 不会是 null；它不是"空区域"，调用方必须先用 <c>CountObj</c> 判空再喂给 <c>union2</c> / <c>difference</c>。
        /// </returns>
        internal async Task<HObject> DrawAsync(RectEnum type)
        {
            HOperatorSet.GenEmptyObj(out HObject result);
            if (!_isUsable()) return result;

            DrawHelper.CancelDraw(_window);

            HObject created = null;
            try
            {
                switch (type)
                {
                    case RectEnum.Rectangle:
                        {
                            var r = await DrawHelper.DrawRectangle1Async(_window);
                            if (r.Completed)
                                HOperatorSet.GenRectangle1(out created, r.Row1, r.Column1, r.Row2, r.Column2);
                        }
                        break;
                    case RectEnum.AffRect:
                        {
                            var r = await DrawHelper.DrawRectangle2Async(_window);
                            if (r.Completed)
                                HOperatorSet.GenRectangle2(out created, r.Row, r.Column, r.Phi, r.Length1, r.Length2);
                        }
                        break;
                    case RectEnum.Circle:
                        {
                            var r = await DrawHelper.DrawCircleAsync(_window);
                            if (r.Completed)
                                HOperatorSet.GenCircle(out created, r.Row, r.Column, r.Radius);
                        }
                        break;
                    case RectEnum.Ellipse:
                        {
                            var r = await DrawHelper.DrawEllipseAsync(_window);
                            if (r.Completed)
                                HOperatorSet.GenEllipse(out created, r.Row, r.Column, r.Phi, r.Radius1, r.Radius2);
                        }
                        break;
                    case RectEnum.Polygon:
                        {
                            var r = await DrawHelper.DrawRegionAsync(_window);
                            // 未确认时拿到的是空区域, 就地释放; 确认则直接接管所有权
                            if (r.Completed) created = r.Region;
                            else r.Region?.Dispose();
                        }
                        break;
                    case RectEnum.Ring:
                        {
                            var outer = await DrawHelper.DrawCircleAsync(_window);
                            if (!outer.Completed) break;

                            var inner = await DrawHelper.DrawCircleModAsync(_window, outer.Row, outer.Column, outer.Radius / 2);
                            if (!inner.Completed) break;

                            created = RegionShapes.GenRing(outer.Row, outer.Column, outer.Radius, inner.Radius);
                        }
                        break;
                    default:
                        throw new NotSupportedException($"DrawRegionAsync: 不支持的 ROI 类型: {type}");
                }

                if (created.NotNull())
                {
                    result.Dispose();       // 释放占位的空对象
                    result = created;
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

            return result;
        }

        #region 结果写回

        // 下面几个 Apply* 负责"绘制结果 → CvRegion"的写回：生成 HObject、写外接框/角度、转移所有权。
        // 新建与修改两条路径的写回逻辑完全一致。只在用户确认后调用。

        /// <summary>
        /// 把新生成的 HObject 转移到 <paramref name="hRegion"/> 上：先置 null 取走所有权，再赋值（setter 释放旧句柄），
        /// 即使释放旧句柄时抛异常，调用方的 finally 也不会再次释放新句柄。
        /// </summary>
        private static void ReplaceRegion(CvRegion hRegion, ref HObject created)
        {
            if (hRegion == null || created == null) return;
            var handle = created;
            created = null;
            hRegion.HoRegion = handle;
        }

        private static void ApplyRect1(CvRegion hRegion, double row1, double column1, double row2, double column2)
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

        private static void ApplyRect2(CvRegion hRegion, double row, double column, double phi, double length1, double length2)
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

        private static void ApplyCircle(CvRegion hRegion, double row, double column, double radius)
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

        private static void ApplyEllipse(CvRegion hRegion, double row, double column, double phi, double radius1, double radius2)
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

        /// <summary> 多边形：新建 / 修改共用；任何一步失败都不泄漏区域句柄 </summary>
        private async Task<bool> DrawPolygonIntoAsync(CvRegion hRegion)
        {
            var result = await DrawHelper.DrawRegionAsync(_window);

            // Region 的所有权已交到这里，无论确认与否都由本方法负责释放（未确认时是空区域）
            HObject region = result.Region;
            try
            {
                if (!result.Completed || !region.NotNull()) return false;

                HOperatorSet.GetRegionPolygon(region, 1, out HTuple rows, out HTuple columns);
                HOperatorSet.SmallestRectangle1(region, out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);

                hRegion.PolygonX = columns;
                hRegion.PolygonY = rows;
                // 与其它 ROI 类型一样写外接框（Center 随之为外接框中心）
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
        /// 圆环：先画（或调整）外圆，再以外圆圆心调整内圆半径 —— <see cref="CvRegion"/> 的圆环模型只能表达同心圆环。
        /// 任意一步取消都整次放弃，保留原 ROI 比写半成品更安全。
        /// </summary>
        private async Task<bool> DrawRingIntoAsync(CvRegion hRegion, bool modify)
        {
            DrawCircleResult outerResult = modify
                ? await DrawHelper.DrawCircleModAsync(_window, hRegion.CenterY, hRegion.CenterX, hRegion.MaxRadius)
                : await DrawHelper.DrawCircleAsync(_window);
            if (!outerResult.Completed) return false;

            // 新建时给内圆一个可见的初值(外圆一半), 修改时沿用已有的 MinRadius.
            double innerSeed = modify ? hRegion.MinRadius : outerResult.Radius / 2;
            var innerResult = await DrawHelper.DrawCircleModAsync(_window, outerResult.Row, outerResult.Column, innerSeed);
            if (!innerResult.Completed) return false;

            double outer = Math.Max(outerResult.Radius, innerResult.Radius);
            double inner = Math.Min(outerResult.Radius, innerResult.Radius);

            HObject ring = RegionShapes.GenRing(outerResult.Row, outerResult.Column, outer, inner);
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
    }
}
