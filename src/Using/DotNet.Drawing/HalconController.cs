using HalconDotNet;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;


namespace DotNet.Drawing
{
    public static class HalconController
    {
        /// <summary>
        /// 获取文件路径
        /// </summary>
        public static string[] GetPaths(string imageFolder)
        {
            var imagePaths = Directory.GetFiles(imageFolder);
            if (imagePaths.Length == 0)
                throw new InvalidOperationException("图片路径为空！");

            var numericFiles = new List<Tuple<int, string>>();
            var nonNumericFiles = new List<string>();

            foreach (var path in imagePaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                int number;
                if (int.TryParse(fileName, out number))
                    numericFiles.Add(Tuple.Create(number, path));
                else
                    nonNumericFiles.Add(path);
            }

            var sorted = numericFiles.OrderBy(x => x.Item1)
                                    .Select(x => x.Item2)
                                    .Concat(nonNumericFiles)
                                    .ToArray();
            return sorted;
        }

        // 已删除 Cal2P / IsNearPoint：两者都是散着 4 个 double 的裸坐标 API（C1 要消除的形态），
        // 且全工程无调用方；中点与距离可直接由坐标计算。

        #region AffineTrans

        /// <summary> 获取仿射变换矩阵 </summary>
        public static void VectorAngleToRigid(Point2d point, Point2d pointTrans, out HTuple hv_HomMat2D)
        {
            HOperatorSet.VectorAngleToRigid(point.Y, point.X, 0, pointTrans.Y, pointTrans.X, 0, out hv_HomMat2D);
        }

        /// <summary> 获取仿射变换矩阵 </summary>
        public static void VectorAngleToRigid(CvCoord coord, CvCoord coordTrans, out HTuple hv_HomMat2D)
        {
            // vector_angle_to_rigid 要求弧度；CvCoord.Angle 是强类型 Angle，取 .Radians 即可，
            // 不存在也不可能再出现「多补一次 ToRadians」的单位错误（B5）。
            HOperatorSet.VectorAngleToRigid(coord.Y, coord.X, coord.Angle.Radians,
                                            coordTrans.Y, coordTrans.X, coordTrans.Angle.Radians,
                                            out hv_HomMat2D);
        }

        /// <summary>
        /// 按刚体变换映射单点
        /// </summary>
        /// <remarks>
        /// 对应审查项 C1：对外只认 <see cref="Point2d"/>(X, Y)，HALCON 的 (Row, Column) 顺序
        /// 只在本方法内部翻转一次。原来的 <c>TransPixel(..., HTuple row, HTuple col, out ...)</c>
        /// 把行列序暴露给了每一个调用方，且两端都是 <c>HTuple</c>，传反了编译器不会报错。
        /// </remarks>
        public static Point2d TransPoint(Point2d origin, Point2d target, Point2d source)
        {
            VectorAngleToRigid(origin, target, out HTuple hv_HomMat2D);
            return AffineTransPoint(hv_HomMat2D, source);
        }

        /// <summary> 按刚体变换映射单点（含旋转） </summary>
        public static Point2d TransPoint(CvCoord origin, CvCoord target, Point2d source)
        {
            VectorAngleToRigid(origin, target, out HTuple hv_HomMat2D);
            return AffineTransPoint(hv_HomMat2D, source);
        }

        /// <summary>
        /// (X, Y) → HALCON (Row, Column) 的唯一翻转点：单点。
        /// </summary>
        private static Point2d AffineTransPoint(HTuple homMat2D, Point2d source)
        {
            HOperatorSet.AffineTransPixel(homMat2D, source.Y, source.X, out HTuple rowTrans, out HTuple colTrans);
            return new Point2d(colTrans.D, rowTrans.D);
        }

        /// <summary> 获取仿射变换区域 </summary>
        public static void TransRegion(Point2d point, Point2d pointTrans, HObject region, out HObject regionTrans)
        {
            VectorAngleToRigid(point, pointTrans, out HTuple hv_HomMat2D);
            HOperatorSet.AffineTransRegion(region, out regionTrans, hv_HomMat2D, "nearest_neighbor");
        }

        /// <summary> 获取仿射变换区域 </summary>
        public static void TransRegion(CvCoord coord, CvCoord coordTrans, HObject region, out HObject regionTrans)
        {
            VectorAngleToRigid(coord, coordTrans, out HTuple hv_HomMat2D);
            HOperatorSet.AffineTransRegion(region, out regionTrans, hv_HomMat2D, "nearest_neighbor");
        }

        /// <summary> 获取仿射变换轮廓 </summary>
        public static void TransContourXld(Point2d point, Point2d pointTrans, HObject contours, out HObject contoursTrans)
        {
            VectorAngleToRigid(point, pointTrans, out HTuple hv_HomMat2D);
            HOperatorSet.AffineTransContourXld(contours, out contoursTrans, hv_HomMat2D);
        }

        /// <summary> 获取仿射变换轮廓 </summary>
        public static void TransContourXld(CvCoord coord, CvCoord coordTrans, HObject contours, out HObject contoursTrans)
        {
            VectorAngleToRigid(coord, coordTrans, out HTuple hv_HomMat2D);
            HOperatorSet.AffineTransContourXld(contours, out contoursTrans, hv_HomMat2D);
        }

        #endregion

        #region SaveImage

        /// <summary>
        /// 确保文件路径所在的文件夹存在，不存在则创建
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private static void EnsureDirectoryOf(string filePath)
        {
            // Null或空字符串检查
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("文件路径不能为空白字符", nameof(filePath));
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (PathTooLongException ex)
            {
                // 提供中文提示：路径太长
                throw new InvalidOperationException(
                    $"路径超出了系统定义的最大长度。当前路径长度为 {filePath.Length} 个字符，路径不能超过 260 个字符。",
                    ex
                );
            }
            catch (ArgumentException ex)
            {
                // 提供中文提示：路径中包含非法字符
                throw new ArgumentException("路径中包含非法字符", nameof(filePath), ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                // 提供中文提示：无权限访问路径
                throw new UnauthorizedAccessException("没有权限访问指定的路径，请检查权限设置。", ex);
            }
            catch (Exception ex)
            {
                // 捕获其他异常，提供统一的中文提示
                throw new InvalidOperationException($"创建文件目录时发生未知错误：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 保存小区域图像
        /// </summary>
        public static void SaveSmallestRectImage(HObject hImage, HObject imgReduced, string ModelPath, string format = "bmp")
        {
            HObject rectangle; HOperatorSet.GenEmptyObj(out rectangle);
            HObject imageReduced; HOperatorSet.GenEmptyObj(out imageReduced);
            HObject imagePart; HOperatorSet.GenEmptyObj(out imagePart);

            try
            {
                HOperatorSet.SmallestRectangle1(imgReduced, out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2);
               
                rectangle.Dispose();
                HOperatorSet.GenRectangle1(out rectangle, row1 - 20, column1 - 20, row2 + 20, column2 + 20);

                imageReduced.Dispose();
                HOperatorSet.ReduceDomain(hImage, rectangle, out imageReduced);

                imagePart.Dispose();
                HOperatorSet.CropDomain(imageReduced, out imagePart);

                EnsureDirectoryOf(ModelPath);
                HOperatorSet.WriteImage(imagePart, format, 0, ModelPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"保存小区域图像失败：{ex.Message}", ex);
            }
            finally
            {
                rectangle.Dispose();
                imageReduced.Dispose();
                imagePart.Dispose();
            }
        }

        #endregion
    }
}
