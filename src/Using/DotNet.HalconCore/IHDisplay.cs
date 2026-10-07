using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNet.HalconCore
{
    /// <summary>
    /// HALCON 显示窗口的绘制契约。
    /// </summary>
    /// <remarks>
    /// 收敛说明：原接口 60 个成员，其中 46 个是 <c>Disp*</c> 重载，而这 46 个里有 22 个
    /// 只是「先 SetColor 再转发」的成对副本——每加一种图元就要新增 2~6 个签名，
    /// 且每个签名都要在 <c>HDisplay</c> 与 <c>HDisplayUI</c> 两处各写一遍。
    /// <para>
    /// 现按「图元类型」而非「图元类型 × 是否带颜色 × 参数写法」组织：
    /// 统一为 <c>Disp(图元, DrawStyle)</c>，样式差异全部收进 <see cref="DrawStyle"/>；
    /// 坐标一律用 <see cref="Point2d"/>(X, Y)，HALCON 的 (Row, Column) 顺序
    /// 只在 <c>HDisplay</c> 最内层做一次转换，不再渗透到调用方。
    /// </para>
    /// </remarks>
    public interface IHDisplay : IDisposable
    {
        #region 窗口状态

        /// <summary> 是否绘制十字光标 </summary>
        bool IsCross { get; set; }

        /// <summary> 是否自适应缩放 </summary>
        bool Adaptive { get; set; }

        /// <summary> 当前图像宽度 </summary>
        double HoWidth { get; }

        /// <summary> 当前图像高度 </summary>
        double HoHeight { get; }

        /// <summary> 当前图像尺寸 </summary>
        Size2d HoSize { get; }

        /// <summary> 当前图像中心点 (X, Y) </summary>
        Point2d HoCentre { get; }

        /// <summary> 当前图像 </summary>
        HObject HoImage { get; }

        #endregion

        #region 画笔

        /// <summary> 获取当前画笔颜色 </summary>
        HColor GetColor();

        /// <summary> 设置画笔颜色 </summary>
        void SetColor(HColor color);

        /// <summary> 设置绘制模式 </summary>
        /// <param name="mode">"margin" 只画轮廓, "fill" 填充</param>
        void SetDraw(string mode);

        /// <summary> 设置字体大小 </summary>
        void SetFontSize(HTuple size);

        #endregion

        #region 图像

        /// <summary> 设置图像（只换图不刷新） </summary>
        void SetImage(HObject image);

        /// <summary> 显示图片，是否重设显示区域取决于 <see cref="Adaptive"/> </summary>
        void DispImage(HObject image);

        /// <summary> 显示图片 </summary>
        void DispImage(HObject image, bool isSetPart);

        /// <summary> 重新显示当前图片 </summary>
        void ReDispImage();

        /// <summary> 清窗并显示指定对象 </summary>
        void ClearWinDisp(HObject objectVal);

        #endregion

        #region 图元绘制

        /// <summary> 画点（十字标记） </summary>
        void Disp(Point2d point, DrawStyle style = null);

        /// <summary> 批量画点 </summary>
        void Disp(IReadOnlyList<Point2d> points, DrawStyle style = null);

        /// <summary> 画坐标系（带方向的十字） </summary>
        void Disp(CvCoord coord, DrawStyle style = null);

        /// <summary> 画线段 </summary>
        void Disp(CvLine line, DrawStyle style = null);

        /// <summary> 画箭头 </summary>
        void Disp(CvArrow arrow, DrawStyle style = null);

        /// <summary> 画圆 </summary>
        void Disp(CvCircle circle, DrawStyle style = null);

        /// <summary> 显示 ROI 已生成的区域对象 </summary>
        void Disp(CvRegion region, DrawStyle style = null);

        /// <summary> 显示 HALCON 对象（区域 / 轮廓） </summary>
        void Disp(HObject region, DrawStyle style = null);

        /// <summary> 显示文本，<paramref name="position"/> 为 (X=列, Y=行)；<see cref="DrawStyle.Size"/> 为字号 </summary>
        void DispText(string message, Point2d position, DrawStyle style = null);

        /// <summary> 画有向矩形 </summary>
        /// <param name="phi">弧度</param>
        /// <param name="length1">沿 phi 方向的半长</param>
        /// <param name="length2">垂直方向的半长</param>
        void DispRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null);

        /// <summary>
        /// 按 ROI 的几何参数绘制轮廓。
        /// </summary>
        /// <remarks>
        /// 与 <c>Disp(CvRegion, DrawStyle)</c> 的区别：后者显示 <c>CvRegion.HoRegion</c>
        /// 这个已实体化的对象；本方法不依赖 HoRegion，直接按 Type/Center/Width... 等几何字段
        /// 用 <c>disp_*</c> 画轮廓，因此在区域尚未 RebuildRegion 时也能画。
        /// （原来两者分别叫 DispRegion / DispCvRegion，签名相同、命名不体现差异。）
        /// </remarks>
        void DispRegionOutline(CvRegion region, DrawStyle style = null);

        /// <summary> 线段 + 末端圆标记（圆恒为红色，沿用历史行为） </summary>
        void DispLineWithEndMarker(CvLine line, double markerRadius, DrawStyle style = null);

        /// <summary> 线段 + 两端十字标记 </summary>
        void DispSegmentWithCrosses(Point2d start, Point2d end, double armLength, DrawStyle style = null);

        #endregion

        #region 交互绘制

        /// <summary>
        /// 交互式绘制（新建）橡皮筋区域。用户右键确认后把几何写回 <paramref name="region"/>；
        /// 取消 / 超时则原样保留旧几何。
        /// </summary>
        /// <returns>
        /// 用户右键确认返回 true；取消 / 超时 / 绘制失败返回 false，此时 <paramref name="region"/> 未被改动。
        /// </returns>
        Task<bool> DrawRegionAsync(CvRegion region);

        /// <summary>
        /// 交互式绘制（修改）橡皮筋区域：以现有几何为初值进入编辑。
        /// 取消 / 超时不改动 <paramref name="region"/>。
        /// </summary>
        /// <returns>语义同 <see cref="DrawRegionAsync(CvRegion)"/>。</returns>
        Task<bool> DrawRegionModAsync(CvRegion region);

        /// <summary>
        /// 交互式绘制（新建）指定类型的区域，直接返回结果区域。
        /// </summary>
        /// <returns>
        /// 新绘制的区域，所有权归调用方，用完必须 <c>Dispose</c>。
        /// <para>
        /// 取消 / 超时 / 失败时返回<b>空对象元组</b>（<c>gen_empty_obj</c>，<c>count_obj == 0</c>），
        /// 不会是 null，调用方可无条件释放。注意它<b>不是</b>“空区域”(<c>gen_empty_region</c>,
        /// <c>count_obj == 1</c>)：把它喂给 <c>union2</c> / <c>difference</c> 这类算子并非无操作，
        /// 而是会报错或返回空结果。调用方必须先用 <c>count_obj</c> 判空并短路，参见 <c>HEditModelUI.DrawROIAsync</c>。
        /// </para>
        /// </returns>
        Task<HObject> DrawRegionAsync(RectEnum type);

        #endregion
    }
}
