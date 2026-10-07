using HalconDotNet;

namespace DotNet.HalconUI
{
    public interface IHWindowFont
    {
        /// <summary>
        /// 设置显示窗体字体大小
        /// </summary>
        /// <param name="hv_Size">字体大小</param>
        /// <param name="hv_Font">默认</param>
        /// <param name="hv_Bold">默认</param>
        /// <param name="hv_Slant">默认</param>
        void SetFontSize(HTuple hv_Size, string font = "sans", string bold = "true", string slant = "false");

        /// <summary>
        /// 显示文本
        /// </summary>
        /// <param name="message">文本信息</param>
        /// <param name="hv_Row">行坐标</param>
        /// <param name="hv_Column">列坐标</param>
        /// <param name="hv_Color">字体颜色</param>
        /// <param name="coordSystem">默认</param>
        void DispText(string message, HTuple hv_Row, HTuple hv_Column, string color, string coordSystem = "image");
    }
}
