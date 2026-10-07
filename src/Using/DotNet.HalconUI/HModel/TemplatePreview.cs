using System;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 模板小图上的一份预览：模板图 + 摆回小图中心的模板区域 / 轮廓 / 坐标系。
    /// </summary>
    /// <remarks>
    /// 原来 <see cref="HModelUI"/> 与 <see cref="HEditModelUI"/> 各写一份读图与平移（<c>TransObject</c> 两处逐字相同），
    /// 模板编辑里的并集 / 差集也直接写在窗体里。收拢到这里，两个窗体只管显示。
    /// <para>所有对象都在全部生成成功后才交出；读图或变换失败时不留半成品。返回的对象归调用方所有。</para>
    /// </remarks>
    internal sealed class TemplatePreview : IDisposable
    {
        private TemplatePreview() { }

        /// <summary> 模板小图 </summary>
        public HObject Image { get; private set; }

        /// <summary> 平移到小图上的模板区域；原区域为空时是空对象 </summary>
        public HObject Region { get; private set; }

        /// <summary> 平移到小图上的匹配轮廓；原轮廓为空时是空对象 </summary>
        public HObject Contour { get; private set; }

        /// <summary> 平移到小图上的匹配坐标系 </summary>
        public CvCoord Coord { get; private set; }

        /// <summary>
        /// 读模板小图，并按 <paramref name="result"/> 的位姿把模板区域与轮廓平移到小图中心。
        /// </summary>
        /// <remarks>
        /// 平移目标取<b>新读入图像</b>的中心，而不是窗口里当前那张图：首次打开时窗口里还是占位图，
        /// 换了尺寸不同的模板图时又是上一张图的，两种情况模板都会被平移到错误位置。
        /// </remarks>
        public static TemplatePreview Load(string modelPath, HObject modelRegion, HObject contour, ModelResult result)
        {
            var preview = new TemplatePreview();
            try
            {
                HOperatorSet.ReadImage(out HObject image, modelPath);
                preview.Image = image;

                Point2d from = result.Coord.Center;
                Point2d to = ImageCentre(image);
                preview.Region = Move(from, to, modelRegion);
                preview.Contour = Move(from, to, contour);
                preview.Coord = new CvCoord(HalconController.TransPoint(from, to, new Point2d(result.Column, result.Row)),
                                            Angle.FromRadians(result.Angle));
                return preview;
            }
            catch
            {
                preview.Dispose();
                throw;
            }
        }

        /// <summary> 交出全部对象的所有权（调用方负责释放），之后本实例为空 </summary>
        public void Detach(out HObject image, out HObject region, out HObject contour)
        {
            image = Image; region = Region; contour = Contour;
            Image = Region = Contour = null;
        }

        /// <summary> 图像中心，与 <see cref="HDisplay.HoCentre"/> 同一约定（X = 列） </summary>
        public static Point2d ImageCentre(HObject image)
        {
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            return new Point2d(width.D / 2, height.D / 2);
        }

        /// <summary> 把区域或 XLD 平移（刚体）到新位置；空对象原样返回空对象。返回值归调用方所有 </summary>
        public static HObject Move(Point2d from, Point2d to, HObject obj)
        {
            if (obj == null || !obj.IsInitialized() || obj.CountObj() <= 0)
            {
                HOperatorSet.GenEmptyObj(out HObject empty);
                return empty;
            }

            HOperatorSet.GetObjClass(obj, out HTuple objClass);
            HObject moved;
            if (objClass.S.StartsWith("xld"))
                HalconController.TransContourXld(from, to, obj, out moved);
            else
                HalconController.TransRegion(from, to, obj, out moved);
            return moved;
        }

        /// <summary>
        /// 模板区域加上（<paramref name="add"/>）或减去一块手绘区域。返回新区域，归调用方所有；两个输入都不释放。
        /// </summary>
        public static HObject Combine(HObject templateRegion, HObject drawn, bool add)
        {
            HObject result;
            if (add) HOperatorSet.Union2(templateRegion, drawn, out result);
            else HOperatorSet.Difference(templateRegion, drawn, out result);
            return result;
        }

        public void Dispose()
        {
            Image?.Dispose();
            Region?.Dispose();
            Contour?.Dispose();
            Image = Region = Contour = null;
        }
    }
}
