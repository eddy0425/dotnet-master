using HalconDotNet;
using System;
using System.Collections.Generic;


namespace DotNet.Drawing
{
    public static class RegionExtension
    {
        /// <summary>换入新句柄；HoRegion 的 setter 负责释放旧句柄。</summary>
        private static void ReplaceHandle(CvRegion hRegion, HObject newHandle)
        {
            hRegion.HoRegion = newHandle;
        }

        /// <summary>
        /// 根据区域类型和几何参数重新生成 Halcon 区域
        /// </summary>
        public static void RebuildRegion(this CvRegion hRegion)
        {
            if (hRegion == null) return;
            switch (hRegion.Type)
            {
                case RectEnum.Rectangle:
                    {
                        HOperatorSet.GenRectangle1(out HObject rectangle, hRegion.TopLeft.Y, hRegion.TopLeft.X,
                                               hRegion.BottomRight.Y, hRegion.BottomRight.X);
                        ReplaceHandle(hRegion, rectangle);
                    }
                    break;
                case RectEnum.AffRect:
                    {
                        HOperatorSet.GenRectangle2(out HObject rectangle, hRegion.CenterY, hRegion.CenterX, hRegion.Phi,
                                               hRegion.Width / 2, hRegion.Height / 2);
                        ReplaceHandle(hRegion, rectangle);
                    }
                    break;
                case RectEnum.Circle:
                    {
                        HOperatorSet.GenCircle(out HObject circle, hRegion.CenterY, hRegion.CenterX, hRegion.Width / 2);
                        ReplaceHandle(hRegion, circle);
                    }
                    break;
                case RectEnum.Ellipse:
                    {
                        HOperatorSet.GenEllipse(out HObject ellipse, hRegion.CenterY, hRegion.CenterX, hRegion.Phi,
                                               hRegion.Width / 2, hRegion.Height / 2);
                        ReplaceHandle(hRegion, ellipse);
                    }
                    break;
                case RectEnum.Polygon:
                    {
                        // 点集为空时直接放弃重建：原实现会把 null 交给 gen_region_polygon，
                        // 抛出的是与真实原因（多边形从未绘制 / 反序列化没带上点集）无关的 HALCON 原生异常。
                        if (hRegion.PolygonX == null || hRegion.PolygonY == null)
                        {
                            Log.Warn(nameof(RegionExtension), "多边形点集为空，跳过区域重建。");
                            return;
                        }
                        // gen_region_polygon(Region, Rows, Columns)：首参为 Row，而 PolygonX/PolygonY 分别存 Column/Row
                        HOperatorSet.GenRegionPolygon(out HObject region, hRegion.PolygonY, hRegion.PolygonX);
                        ReplaceHandle(hRegion, region);
                    }
                    break;
                case RectEnum.Ring:
                    ReplaceHandle(hRegion, RegionShapes.GenRing(hRegion.CenterY, hRegion.CenterX, hRegion.MaxRadius, hRegion.MinRadius));
                    break;
            }
        }

        /// <summary>
        /// 以各坐标为中心、使用当前区域的宽高生成矩形，并合并到现有 Halcon 区域
        /// </summary>
        /// <param name="hRegion">要合并矩形的区域</param>
        /// <param name="coords">矩形的中心坐标集合</param>
        public static void GenCoordsRegion(this CvRegion hRegion, List<CvCoord> coords)
        {
            // hRegion 判空与 coords 同等对待：原实现只判了 coords，
            // 传 null 区域进来会在 hRegion.Height 处 NRE。
            if (hRegion == null || coords == null) return;
            // 已释放（HoRegion 为 null）的区域没法参与 union2，直接放弃而不是在算子里炸。
            if (hRegion.HoRegion == null) return;

            HObject imgReduced; HOperatorSet.GenEmptyObj(out imgReduced);

            try
            {
                for (int i = 0; i < coords.Count; i++)
                {
                    HTuple row1 = coords[i].Y - hRegion.Height / 2;
                    HTuple column1 = coords[i].X - hRegion.Width / 2;
                    HTuple row2 = coords[i].Y + hRegion.Height / 2;
                    HTuple column2 = coords[i].X + hRegion.Width / 2;

                    imgReduced.Dispose();
                    HOperatorSet.GenRectangle1(out imgReduced, row1, column1, row2, column2);
                    HOperatorSet.Union2(hRegion.HoRegion, imgReduced, out HObject regionUnion);
                    ReplaceHandle(hRegion, regionUnion);
                }
            }
            finally
            {
                imgReduced.Dispose();
            }
        }

        /// <summary>
        /// 设置区域中心点，并保持当前宽高不变
        /// </summary>
        /// <param name="center">新的中心点</param>
        internal static void SetCenter(this CvRegion hRegion, Point2d center)
        {
            Point2d location = new Point2d(center.X - hRegion.Width / 2, center.Y - hRegion.Height / 2);
            hRegion.Bounds = new Rect2d(location, hRegion.Size);
        }

        /// <summary>
        /// 通过中心点和尺寸设置区域矩形
        /// </summary>
        /// <param name="center">中心点</param>
        /// <param name="size">矩形尺寸</param>
        public static void SetRectByCenter(this CvRegion hRegion, Point2d center, Size2d size)
        {
            Point2d TopLeft = new Point2d(center.X - size.Width / 2, center.Y - size.Height / 2);
            var rect = new Rect2d(TopLeft, size);
            hRegion.Bounds = rect;
        }

        /// <summary>
        /// 通过左上角和右下角设置区域矩形
        /// </summary>
        /// <param name="topLeft">左上角</param>
        /// <param name="bottomRight">右下角</param>
        public static void SetRectByCorners(this CvRegion hRegion, Point2d topLeft, Point2d bottomRight)
        {
            if (bottomRight.X < topLeft.X)
                throw new ArgumentException("right must be >= left", nameof(bottomRight));
            if (bottomRight.Y < topLeft.Y)
                throw new ArgumentException("bottom must be >= top", nameof(bottomRight));

            var rect = new Rect2d(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
            hRegion.Bounds = rect;
        }

        /// <summary>
        /// 通过 Halcon 左上角和右下角的行列坐标设置区域矩形
        /// </summary>
        /// <param name="row1">左上角行坐标</param>
        /// <param name="column1">左上角列坐标</param>
        /// <param name="row2">右下角行坐标</param>
        /// <param name="column2">右下角列坐标</param>
        public static void SetRectByCorners(this CvRegion hRegion, HTuple row1, HTuple column1, HTuple row2, HTuple column2)
        {
            var rect = new Rect2d(row1, column1, row2, column2);
            hRegion.Bounds = rect;
        }
    }
}
