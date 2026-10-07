using System;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 依赖 HALCON 原生运行时（halcon.dll + 许可）的测试。
    /// </summary>
    /// <remarks>运行时不可用时整组标记为 Inconclusive，而不是失败。</remarks>
    public abstract class HalconTestBase
    {
        private static readonly Lazy<string> RuntimeError = new Lazy<string>(() =>
        {
            try
            {
                HOperatorSet.GenEmptyObj(out HObject probe);
                probe.Dispose();
                // 默认按系统图像尺寸裁剪区域，结果会随测试执行顺序变化；这里只验证本库逻辑，关掉裁剪。
                HOperatorSet.SetSystem("clip_region", "false");
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        });

        [TestInitialize]
        public void RequireHalcon()
        {
            if (RuntimeError.Value != null)
                Assert.Inconclusive("HALCON 运行时不可用：" + RuntimeError.Value);
        }

        /// <summary>取区域的面积与中心（X=Column, Y=Row）。</summary>
        protected static double AreaCenter(HObject region, out Point2d center)
        {
            HOperatorSet.AreaCenter(region, out HTuple area, out HTuple row, out HTuple column);
            center = new Point2d(column.D, row.D);
            return area.D;
        }

        protected static HObject Rectangle1(double row1, double column1, double row2, double column2)
        {
            HOperatorSet.GenRectangle1(out HObject rect, row1, column1, row2, column2);
            return rect;
        }

        /// <summary>按类型与外接框生成 ROI，并同步重建 HoRegion。</summary>
        protected static CvRegion NewRegion(RectEnum type, double x, double y, double width, double height, double phi = 0)
        {
            var region = new CvRegion { Type = type, Bounds = new Rect2d(x, y, width, height), Phi = phi };
            region.RebuildRegion();
            return region;
        }

        /// <summary>以中心点生成 AffRect ROI：Width 沿 phi 方向（测量方向），Height 为步进方向。</summary>
        protected static CvRegion NewAffRect(Point2d center, double width, double height, double phi = 0)
            => NewRegion(RectEnum.AffRect, center.X - width / 2, center.Y - height / 2, width, height, phi);

        /// <summary>纯色背景图像（byte）。</summary>
        protected static HObject ConstImage(int width, int height, int gray)
        {
            HOperatorSet.GenImageConst(out HObject image, "byte", width, height);
            using (image)
            using (var full = Rectangle1(0, 0, height - 1, width - 1))
            {
                return Paint(image, full, gray);
            }
        }

        /// <summary>在图像上把区域涂成指定灰度，返回新图像（原图不变）。</summary>
        protected static HObject Paint(HObject image, HObject region, int gray)
        {
            HOperatorSet.PaintRegion(region, image, out HObject painted, gray, "fill");
            return painted;
        }

        /// <summary>左暗右亮的竖直阶跃边：列 &lt; edgeColumn 为 0，其余为 255。</summary>
        protected static HObject VerticalStepImage(int width, int height, int edgeColumn)
        {
            using (var dark = ConstImage(width, height, 0))
            using (var bright = Rectangle1(0, edgeColumn, height - 1, width - 1))
            {
                return Paint(dark, bright, 255);
            }
        }

        /// <summary>暗背景上的亮圆盘。</summary>
        protected static HObject DiskImage(int width, int height, Point2d center, double radius)
        {
            HOperatorSet.GenCircle(out HObject disk, center.Y, center.X, radius);
            using (disk)
            using (var dark = ConstImage(width, height, 0))
            {
                return Paint(dark, disk, 255);
            }
        }

        protected static void ImageSize(HObject image, out int width, out int height)
        {
            HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
            width = w.I;
            height = h.I;
        }

        protected static int GrayAt(HObject image, int row, int column)
        {
            HOperatorSet.GetGrayval(image, row, column, out HTuple gray);
            return gray.I;
        }
    }
}
