using System.Collections.Generic;
using DotNet.Drawing;
using HalconDotNet;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 运行结果的叠加层：算法只<b>描述</b>要画什么，不对窗口下命令。
    /// </summary>
    /// <remarks>
    /// 取代 <c>Render(IHDisplay, ...)</c>：显示窗口能换图、清屏、发起交互，叠加层只能画。
    /// 画在哪个线程、缩放 / 平移 / 窗口尺寸变化后怎么重画，都由宿主负责 —— 宿主保存叠加层，每次重画图像后重放。
    /// <para>
    /// <see cref="Add(HObject, DrawStyle)"/> 复制句柄，叠加层拥有副本：算法可以在 <c>Render</c> 之后立即释放自己的对象。
    /// 叠加层由调用 <c>Run</c> 的一方创建、拥有、释放。
    /// </para>
    /// </remarks>
    public interface IOverlay
    {
        /// <summary> HALCON 对象（区域 / 轮廓）；复制句柄，空句柄忽略 </summary>
        void Add(HObject obj, DrawStyle style = null);

        /// <summary> 点（十字标记），<see cref="DrawStyle.Size"/> 为臂长 </summary>
        void Add(Point2d point, DrawStyle style = null);

        /// <summary> 一组点 </summary>
        void Add(IReadOnlyList<Point2d> points, DrawStyle style = null);

        /// <summary> 线段 </summary>
        void Add(CvLine line, DrawStyle style = null);

        /// <summary> 箭头，<see cref="DrawStyle.Size"/> 覆盖 <c>CvArrow.HeadSize</c> </summary>
        void Add(CvArrow arrow, DrawStyle style = null);

        /// <summary> 圆 </summary>
        void Add(CvCircle circle, DrawStyle style = null);

        /// <summary> 坐标系（带方向的十字） </summary>
        void Add(CvCoord coord, DrawStyle style = null);

        /// <summary> ROI 已生成的区域（<c>CvRegion.HoRegion</c>）；复制句柄 </summary>
        void Add(CvRegion region, DrawStyle style = null);

        /// <summary> 有向矩形 </summary>
        /// <param name="phi">弧度</param>
        /// <param name="length1">沿 phi 方向的半长</param>
        /// <param name="length2">垂直方向的半长</param>
        void AddRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null);

        /// <summary> 文本，<paramref name="position"/> 为 (X=列, Y=行)；<see cref="DrawStyle.Size"/> 为字号 </summary>
        void Text(string message, Point2d position, DrawStyle style = null);
    }
}
