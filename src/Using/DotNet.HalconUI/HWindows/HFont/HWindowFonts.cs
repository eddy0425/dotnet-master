using System;
using DotNet.Drawing;
using HalconDotNet;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 按窗口的图形栈挑选字体实现。
    /// </summary>
    /// <remarks>
    /// 两份实现的差别不在 HALCON 版本号，而在窗口类型：
    /// <see cref="HWindowFont2022"/> 直接调 <c>disp_text</c>（带阴影参数），只在新图形栈的窗口上可用；
    /// <c>HWindowControl</c> 建出的是 <c>WIN32-Window</c>，在它上面 <c>disp_text</c> 报 #5123，
    /// 文本整行消失。<see cref="HWindowFont2018"/> 用 <c>write_string</c> 自绘，两种窗口都能用。
    /// 因此按 <c>get_window_type</c> 选择，而不是写死某一个类。
    /// </remarks>
    public static class HWindowFonts
    {
        /// <summary> 旧图形栈（GDI）窗口类型，只能用 <see cref="HWindowFont2018"/> </summary>
        public const string LegacyWindowType = "WIN32-Window";

        public static IHWindowFont Create(HWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            return IsLegacy(window) ? (IHWindowFont)new HWindowFont2018(window) : new HWindowFont2022(window);
        }

        /// <remarks>查询失败时按旧图形栈处理：2018 版在两种窗口上都能出字，选错的代价只是少了阴影。</remarks>
        internal static bool IsLegacy(HWindow window)
        {
            try
            {
                HOperatorSet.GetWindowType(window, out HTuple type);
                return type.Length == 0 || type.S == LegacyWindowType;
            }
            catch (HalconException ex)
            {
                Log.Warn(nameof(HWindowFonts), "查询窗口类型失败, 按旧图形栈选择字体实现.", ex);
                return true;
            }
        }
    }
}
