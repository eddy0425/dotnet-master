using System.Drawing;
using HalconDotNet;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 图像缩放信息载体
    /// </summary>
    public class ZoomImage
    {
        /// <summary>
        /// 图像宽度；0 表示尚未载入图像
        /// </summary>
        public HTuple width;

        /// <summary>
        /// 图像高度；0 表示尚未载入图像
        /// </summary>
        public HTuple height;

        /// <summary>
        /// 父容器引用（用于尺寸计算）
        /// </summary>
        public Size parent;

        public ZoomImage()
        {
            // 原来写死 1248x2200 (某台相机的分辨率)。显示窗口构造时就会载入占位图, 这个默认值不应被读到;
            // 用 0 表示"未知", 读到了也是明显的空尺寸而不是一个看似合理的假尺寸
            width = 0;
            height = 0;
            parent = new Size();
        }
    }
}
