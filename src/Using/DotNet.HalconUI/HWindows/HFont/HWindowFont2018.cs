using DotNet.Drawing;
using HalconDotNet;
using System;

namespace DotNet.HalconUI
{
    public class HWindowFont2018 : IHWindowFont
    {
        HWindow hWindow;

        /// <summary> 读取窗口当前字体名；测试可替换，用来模拟新式命名的窗口 </summary>
        readonly Func<string> _currentFont;

        /// <summary> 创建新式命名下的设字号实现；测试可替换，用来确认委托与跳过行为 </summary>
        readonly Func<IHWindowFont> _createModernFont;

        /// <summary> 新式字体命名时的设字号实现；首次设字号时按窗口当前字体判断，之后不再查询 </summary>
        IHWindowFont _modernFont;
        bool _fontNamingChecked;
        /// <summary>
        /// 新式命名下上次设置的参数（不论成败）；相同则跳过：2022 版每次都会 query_font，代价高，
        /// 失败多是字体不存在，同参数重试结果不变，只会让每次重放都 query_font 并刷日志。
        /// 前提：窗口字体只经本实例修改。
        /// </summary>
        string _lastModernKey;

        public HWindowFont2018(HWindow _hWindow) : this(_hWindow, null) { }

        internal HWindowFont2018(HWindow _hWindow, Func<string> currentFont, Func<IHWindowFont> createModernFont = null)
        {
            hWindow = _hWindow;
            _currentFont = currentFont ?? (() =>
            {
                HOperatorSet.GetFont(hWindow, out HTuple current);
                return current.S;
            });
            _createModernFont = createModernFont ?? (() => new HWindowFont2022(hWindow));
        }

        /// <summary>
        /// 旧式（X 风格）字体名以 '-' 开头，例如 <c>-Arial-15-*-0-*-*-1-</c>；新式为 <c>Arial-Bold-15</c>。
        /// </summary>
        internal static bool IsLegacyFontName(string font) => !string.IsNullOrEmpty(font) && font[0] == '-';

        /// <remarks>
        /// 同是 WIN32-Window，HALCON 用哪种字体命名取决于进程环境：测试宿主里是旧式（当前字体 <c>-fixed-</c>），
        /// 产品里是新式（<c>default-Normal-12</c>），这时旧式字体名报 #5137，文本整行消失。
        /// 因此按窗口当前字体名的格式选择：新式命名交给 <see cref="HWindowFont2022.SetFontSize"/>（set_font 与图形栈无关，
        /// 只有 disp_text 才区分），文本仍由本类的 write_string 输出。
        /// 命名方式在窗口生命周期内不变，只在首次设字号（尚未 set_font）时判断一次：叠加层每次重放都会设字号。
        /// </remarks>
        public void SetFontSize(HTuple hv_Size, string font, string bold, string slant)
        {
            if (!_fontNamingChecked)
            {
                if (!IsLegacyFontName(_currentFont())) _modernFont = _createModernFont();
                _fontNamingChecked = true;
            }
            if (_modernFont != null)
            {
                string key = $"{hv_Size}|{font}|{bold}|{slant}";
                if (key == _lastModernKey) return;
                _lastModernKey = key;   // 先记再设：失败也不重试，异常只在首次抛出
                _modernFont.SetFontSize(hv_Size, font, bold, slant);
                return;
            }

            HTuple hv_Font = new HTuple(font);
            HTuple hv_Bold = new HTuple(bold);
            HTuple hv_Slant = new HTuple(slant);

            // Local control variables 

            HTuple hv_OS, hv_Exception = new HTuple();
            HTuple hv_AllowedFontSizes = new HTuple(), hv_Distances = new HTuple();
            HTuple hv_Indices = new HTuple();

            HTuple hv_Bold_COPY_INP_TMP = hv_Bold.Clone();
            HTuple hv_Font_COPY_INP_TMP = hv_Font.Clone();
            HTuple hv_Size_COPY_INP_TMP = hv_Size.Clone();
            HTuple hv_Slant_COPY_INP_TMP = hv_Slant.Clone();

            // Initialize local and output iconic variables 

            //This procedure sets the text font of the current window with
            //the specified attributes.
            //It is assumed that following fonts are installed on the system:
            //Windows: Courier New, Arial Times New Roman
            //Linux: courier, helvetica, times
            //Because fonts are displayed smaller on Linux than on Windows,
            //a scaling factor of 1.25 is used the get comparable results.
            //For Linux, only a limited number of font sizes is supported,
            //to get comparable results, it is recommended to use one of the
            //following sizes: 9, 11, 14, 16, 20, 27
            //(which will be mapped internally on Linux systems to 11, 14, 17, 20, 25, 34)
            //
            //input parameters:
            //WindowHandle: The graphics window for which the font will be set
            //Size: The font size. If Size=-1, the default of 16 is used.
            //Bold: If set to 'true', a bold font is used
            //Slant: If set to 'true', a slanted font is used
            //
            HOperatorSet.GetSystem("operating_system", out hv_OS);
            if ((int)((new HTuple(hv_Size_COPY_INP_TMP.TupleEqual(new HTuple()))).TupleOr(
                new HTuple(hv_Size_COPY_INP_TMP.TupleEqual(-1)))) != 0)
            {
                hv_Size_COPY_INP_TMP = 16;
            }
            if ((int)(new HTuple((((hv_OS.TupleStrFirstN(2)).TupleStrLastN(0))).TupleEqual(
                "Win"))) != 0)
            {
                //set font on Windows systems
                if ((int)((new HTuple((new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("mono"))).TupleOr(
                    new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("Courier"))))).TupleOr(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual(
                    "courier")))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Courier New";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("sans"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Arial";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("serif"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "Times New Roman";
                }
                if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = 1;
                }
                else if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = 0;
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Bold";
                    throw new HalconException(hv_Exception.S);
                }
                if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = 1;
                }
                else if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = 0;
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Slant";
                    throw new HalconException(hv_Exception.S);
                }
                try
                {
                    HOperatorSet.SetFont(hWindow, ((((((("-" + hv_Font_COPY_INP_TMP) + "-") + hv_Size_COPY_INP_TMP) + "-*-") + hv_Slant_COPY_INP_TMP) + "-*-*-") + hv_Bold_COPY_INP_TMP) + "-");
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    // 这里的 hv_Exception 是 HDevelop 异常元组（首元素为错误码），必须走 HTuple 重载；取 .S 会抛 HTupleAccessException
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    throw new HalconException(hv_Exception);
                }
            }
            else
            {
                //set font for UNIX systems
                hv_Size_COPY_INP_TMP = hv_Size_COPY_INP_TMP * 1.25;
                hv_AllowedFontSizes = new HTuple();
                hv_AllowedFontSizes[0] = 11;
                hv_AllowedFontSizes[1] = 14;
                hv_AllowedFontSizes[2] = 17;
                hv_AllowedFontSizes[3] = 20;
                hv_AllowedFontSizes[4] = 25;
                hv_AllowedFontSizes[5] = 34;
                if ((int)(new HTuple(((hv_AllowedFontSizes.TupleFind(hv_Size_COPY_INP_TMP))).TupleEqual(
                    -1))) != 0)
                {
                    hv_Distances = ((hv_AllowedFontSizes - hv_Size_COPY_INP_TMP)).TupleAbs();
                    HOperatorSet.TupleSortIndex(hv_Distances, out hv_Indices);
                    hv_Size_COPY_INP_TMP = hv_AllowedFontSizes.TupleSelect(hv_Indices.TupleSelect(
                        0));
                }
                if ((int)((new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("mono"))).TupleOr(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual(
                    "Courier")))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "courier";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("sans"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "helvetica";
                }
                else if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("serif"))) != 0)
                {
                    hv_Font_COPY_INP_TMP = "times";
                }
                if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = "bold";
                }
                else if ((int)(new HTuple(hv_Bold_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Bold_COPY_INP_TMP = "medium";
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Bold";
                    throw new HalconException(hv_Exception.S);
                }
                if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("true"))) != 0)
                {
                    if ((int)(new HTuple(hv_Font_COPY_INP_TMP.TupleEqual("times"))) != 0)
                    {
                        hv_Slant_COPY_INP_TMP = "i";
                    }
                    else
                    {
                        hv_Slant_COPY_INP_TMP = "o";
                    }
                }
                else if ((int)(new HTuple(hv_Slant_COPY_INP_TMP.TupleEqual("false"))) != 0)
                {
                    hv_Slant_COPY_INP_TMP = "r";
                }
                else
                {
                    hv_Exception = "Wrong value of control parameter Slant";
                    throw new HalconException(hv_Exception.S);
                }
                try
                {
                    HOperatorSet.SetFont(hWindow, ((((((("-adobe-" + hv_Font_COPY_INP_TMP) + "-") + hv_Bold_COPY_INP_TMP) + "-") + hv_Slant_COPY_INP_TMP) + "-normal-*-") + hv_Size_COPY_INP_TMP) + "-*-*-*-*-*-*-*");
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    // 这里的 hv_Exception 是 HDevelop 异常元组（首元素为错误码），必须走 HTuple 重载；取 .S 会抛 HTupleAccessException
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    throw new HalconException(hv_Exception);
                }
            }

            return;
        }

        public void DispText(string message, HTuple hv_Row, HTuple hv_Column, string color, string coordSystem)
        {
            // 原窗口颜色与 Part 提到 try 外：中途任一算子抛异常（颜色名非法、窗口已销毁…）时，
            // 由 finally 还原，否则窗口会停留在"窗口坐标 Part + 文本颜色"，后续图像显示全部错位。
            HTuple hv_Red = null, hv_Green = null, hv_Blue = null;
            HTuple hv_Row1Part = null, hv_Column1Part = null, hv_Row2Part = null, hv_Column2Part = null;
            try
            {
                HTuple hv_String = new HTuple(message);
                HTuple hv_Box = new HTuple("false");
                HTuple hv_Color = new HTuple(color);
                HTuple hv_CoordSystem = new HTuple(coordSystem);

                // Local control variables 

                HTuple hv_RowWin;
                HTuple hv_ColumnWin, hv_WidthWin, hv_HeightWin, hv_MaxAscent;
                HTuple hv_MaxDescent, hv_MaxWidth, hv_MaxHeight, hv_R1 = new HTuple();
                HTuple hv_C1 = new HTuple(), hv_FactorRow = new HTuple(), hv_FactorColumn = new HTuple();
                HTuple hv_Width = new HTuple(), hv_Index = new HTuple(), hv_Ascent = new HTuple();
                HTuple hv_Descent = new HTuple(), hv_W = new HTuple(), hv_H = new HTuple();
                HTuple hv_FrameHeight = new HTuple(), hv_FrameWidth = new HTuple();
                HTuple hv_R2 = new HTuple(), hv_C2 = new HTuple(), hv_DrawMode = new HTuple();
                HTuple hv_Exception = new HTuple(), hv_CurrentColor = new HTuple();

                HTuple hv_Color_COPY_INP_TMP = hv_Color.Clone();
                HTuple hv_Column_COPY_INP_TMP = hv_Column.Clone();
                HTuple hv_Row_COPY_INP_TMP = hv_Row.Clone();
                HTuple hv_String_COPY_INP_TMP = hv_String.Clone();

                // Initialize local and output iconic variables 

                //This procedure displays text in a graphics window.
                //
                //Input parameters:
                //WindowHandle: The WindowHandle of the graphics window, where
                //   the message should be displayed
                //String: A tuple of strings containing the text message to be displayed
                //CoordSystem: If set to 'window', the text position is given
                //   with respect to the window coordinate system.
                //   If set to 'image', image coordinates are used.
                //   (This may be useful in zoomed images.)
                //Row: The row coordinate of the desired text position
                //   If set to -1, a default value of 12 is used.
                //Column: The column coordinate of the desired text position
                //   If set to -1, a default value of 12 is used.
                //Color: defines the color of the text as string.
                //   If set to [], '' or 'auto' the currently set color is used.
                //   If a tuple of strings is passed, the colors are used cyclically
                //   for each new textline.
                //Box: If set to 'true', the text is written within a white box.
                //
                //prepare window
                HOperatorSet.GetRgb(hWindow, out hv_Red, out hv_Green, out hv_Blue);
                HOperatorSet.GetPart(hWindow, out hv_Row1Part, out hv_Column1Part, out hv_Row2Part,
                    out hv_Column2Part);
                HOperatorSet.GetWindowExtents(hWindow, out hv_RowWin, out hv_ColumnWin,
                    out hv_WidthWin, out hv_HeightWin);
                HOperatorSet.SetPart(hWindow, 0, 0, hv_HeightWin - 1, hv_WidthWin - 1);
                //
                //default settings
                if ((int)(new HTuple(hv_Row_COPY_INP_TMP.TupleEqual(-1))) != 0)
                {
                    hv_Row_COPY_INP_TMP = 12;
                }
                if ((int)(new HTuple(hv_Column_COPY_INP_TMP.TupleEqual(-1))) != 0)
                {
                    hv_Column_COPY_INP_TMP = 12;
                }
                if ((int)(new HTuple(hv_Color_COPY_INP_TMP.TupleEqual(new HTuple()))) != 0)
                {
                    hv_Color_COPY_INP_TMP = "";
                }
                //
                hv_String_COPY_INP_TMP = ((("" + hv_String_COPY_INP_TMP) + "")).TupleSplit("\n");
                //
                //Estimate extentions of text depending on font size.
                HOperatorSet.GetFontExtents(hWindow, out hv_MaxAscent, out hv_MaxDescent,
                    out hv_MaxWidth, out hv_MaxHeight);
                if ((int)(new HTuple(hv_CoordSystem.TupleEqual("window"))) != 0)
                {
                    hv_R1 = hv_Row_COPY_INP_TMP.Clone();
                    hv_C1 = hv_Column_COPY_INP_TMP.Clone();
                }
                else
                {
                    //transform image to window coordinates
                    hv_FactorRow = (1.0 * hv_HeightWin) / ((hv_Row2Part - hv_Row1Part) + 1);
                    hv_FactorColumn = (1.0 * hv_WidthWin) / ((hv_Column2Part - hv_Column1Part) + 1);
                    hv_R1 = ((hv_Row_COPY_INP_TMP - hv_Row1Part) + 0.5) * hv_FactorRow;
                    hv_C1 = ((hv_Column_COPY_INP_TMP - hv_Column1Part) + 0.5) * hv_FactorColumn;
                }
                //
                //display text box depending on text size
                if ((int)(new HTuple(hv_Box.TupleEqual("true"))) != 0)
                {
                    //calculate box extents
                    hv_String_COPY_INP_TMP = (" " + hv_String_COPY_INP_TMP) + " ";
                    hv_Width = new HTuple();
                    for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_String_COPY_INP_TMP.TupleLength()
                        )) - 1); hv_Index = (int)hv_Index + 1)
                    {
                        HOperatorSet.GetStringExtents(hWindow, hv_String_COPY_INP_TMP.TupleSelect(
                            hv_Index), out hv_Ascent, out hv_Descent, out hv_W, out hv_H);
                        hv_Width = hv_Width.TupleConcat(hv_W);
                    }
                    hv_FrameHeight = hv_MaxHeight * (new HTuple(hv_String_COPY_INP_TMP.TupleLength()
                        ));
                    hv_FrameWidth = (((new HTuple(0)).TupleConcat(hv_Width))).TupleMax();
                    hv_R2 = hv_R1 + hv_FrameHeight;
                    hv_C2 = hv_C1 + hv_FrameWidth;
                    //display rectangles
                    HOperatorSet.GetDraw(hWindow, out hv_DrawMode);
                    HOperatorSet.SetDraw(hWindow, "fill");
                    HOperatorSet.SetColor(hWindow, "light gray");
                    HOperatorSet.DispRectangle1(hWindow, hv_R1 + 3, hv_C1 + 3, hv_R2 + 3, hv_C2 + 3);
                    HOperatorSet.SetColor(hWindow, "white");
                    HOperatorSet.DispRectangle1(hWindow, hv_R1, hv_C1, hv_R2, hv_C2);
                    HOperatorSet.SetDraw(hWindow, hv_DrawMode);
                }
                else if ((int)(new HTuple(hv_Box.TupleNotEqual("false"))) != 0)
                {
                    hv_Exception = "Wrong value of control parameter Box";
                    throw new HalconException(hv_Exception.S);
                }
                //Write text.
                for (hv_Index = 0; (int)hv_Index <= (int)((new HTuple(hv_String_COPY_INP_TMP.TupleLength()
                    )) - 1); hv_Index = (int)hv_Index + 1)
                {
                    hv_CurrentColor = hv_Color_COPY_INP_TMP.TupleSelect(hv_Index % (new HTuple(hv_Color_COPY_INP_TMP.TupleLength()
                        )));
                    if ((int)((new HTuple(hv_CurrentColor.TupleNotEqual(""))).TupleAnd(new HTuple(hv_CurrentColor.TupleNotEqual(
                        "auto")))) != 0)
                    {
                        HOperatorSet.SetColor(hWindow, hv_CurrentColor);
                    }
                    else
                    {
                        HOperatorSet.SetRgb(hWindow, hv_Red, hv_Green, hv_Blue);
                    }
                    hv_Row_COPY_INP_TMP = hv_R1 + (hv_MaxHeight * hv_Index);
                    HOperatorSet.SetTposition(hWindow, hv_Row_COPY_INP_TMP, hv_C1);
                    HOperatorSet.WriteString(hWindow, hv_String_COPY_INP_TMP.TupleSelect(
                        hv_Index));
                }
            }
            catch (Exception ex)
            {
                // 文本绘制失败不应打断调用方的整条显示流程（通常是窗口已销毁或字体不可用），
                // 但静默会让"文字不显示"变成无线索问题，因此记一条日志。
                Log.Warn(nameof(HWindowFont2018), "显示文本失败.", ex);
            }
            finally
            {
                RestoreWindow(hv_Red, hv_Green, hv_Blue, hv_Row1Part, hv_Column1Part, hv_Row2Part, hv_Column2Part);
            }
        }

        /// <summary> 还原 DispText 改动过的窗口设置；对应的 Get 没跑到（参数为 null）就说明没改过，跳过 </summary>
        private void RestoreWindow(HTuple red, HTuple green, HTuple blue, HTuple row1, HTuple col1, HTuple row2, HTuple col2)
        {
            try
            {
                if (red != null) HOperatorSet.SetRgb(hWindow, red, green, blue);
                if (row1 != null) HOperatorSet.SetPart(hWindow, row1, col1, row2, col2);
            }
            catch (Exception ex)
            {
                Log.Warn(nameof(HWindowFont2018), "还原窗口颜色/Part 失败.", ex);
            }
        }

    }
}
