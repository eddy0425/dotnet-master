using System;
using System.Linq;
using DotNet.Drawing;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="HWindowFont2018"/> / <see cref="HWindowFont2022"/>：字体参数校验与文本显示后的窗口状态还原。
    /// </summary>
    [TestClass]
    public class HWindowFontTests : HalconTestBase
    {
        private static string Rgb(HWindow window)
        {
            HOperatorSet.GetRgb(window, out HTuple r, out HTuple g, out HTuple b);
            return $"{r.I},{g.I},{b.I}";
        }

        private static string Part(HWindow window)
        {
            window.GetPart(out int r1, out int c1, out int r2, out int c2);
            return $"{r1},{c1},{r2},{c2}";
        }

        /// <summary>
        /// HWindowControl 建出的是旧图形栈的 WIN32-Window，在它上面 2022 版的 disp_text 必然报 #5123；
        /// 工厂必须按窗口类型选 2018 版，否则产品里的状态文本整行消失。
        /// </summary>
        [TestMethod]
        public void Create_LegacyWindow_Picks2018()
        {
            WindowHost.Run(host =>
            {
                HOperatorSet.GetWindowType(host.Window, out HTuple type);
                Assert.AreEqual(HWindowFonts.LegacyWindowType, type.S, "前提：测试窗口与产品窗口同为旧图形栈");

                Assert.IsInstanceOfType(HWindowFonts.Create(host.Window), typeof(HWindowFont2018));
            });
        }

        [TestMethod]
        public void Create_NullWindow_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(() => HWindowFonts.Create(null));
        }

        [TestMethod]
        public void Font2018_DispText_RestoresPartAndColor()
        {
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2018(host.Window);
                host.Window.SetPart(10, 20, 110, 220);
                host.Window.SetRgb(0, 255, 0);

                using (var log = new CapturingLogger())
                {
                    font.DispText("abc\ndef", 30, 40, "red", "image");
                    Assert.AreEqual(0, log.Entries.Count, string.Join("; ", log.Entries.Select(e => e.Message)));
                }

                Assert.AreEqual("10,20,110,220", Part(host.Window));
                Assert.AreEqual("0,255,0", Rgb(host.Window));
            });
        }

        [TestMethod]
        public void Font2018_DispText_Failure_StillRestoresPartAndColor()
        {
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2018(host.Window);
                host.Window.SetPart(10, 20, 110, 220);
                host.Window.SetRgb(0, 255, 0);

                using (var log = new CapturingLogger())
                {
                    // 非法颜色名让 set_color 在 Part 已切到窗口坐标之后抛出
                    font.DispText("abc", 30, 40, "no_such_colour", "image");
                    Assert.AreEqual(1, log.Messages(LogLevel.Warn).Count(), "失败只记日志，不向外抛");
                }

                Assert.AreEqual("10,20,110,220", Part(host.Window), "失败后 Part 应还原，否则之后的图像全部错位");
                Assert.AreEqual("0,255,0", Rgb(host.Window));
            });
        }

        [TestMethod]
        public void Font2018_SetFontSize_ValidatesBoldAndSlant()
        {
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2018(host.Window);
                font.SetFontSize(16, "mono", "true", "false");

                // 原实现 new HalconException(HTuple) 把字符串当错误码解析，抛出的是 HTupleAccessException
                var bold = Assert.ThrowsException<HalconException>(() => font.SetFontSize(16, "mono", "yes", "false"));
                StringAssert.Contains(bold.Message, "Bold");
                var slant = Assert.ThrowsException<HalconException>(() => font.SetFontSize(16, "mono", "true", "maybe"));
                StringAssert.Contains(slant.Message, "Slant");
            });
        }

        /// <summary>
        /// 产品进程里 WIN32-Window 用的是新式字体命名（当前字体 default-Normal-12），旧式名报 #5137 导致文本不显示；
        /// 测试宿主是旧式命名，造不出新式窗口，这里只校验格式判断。
        /// </summary>
        [TestMethod]
        public void Font2018_IsLegacyFontName_ByLeadingDash()
        {
            Assert.IsTrue(HWindowFont2018.IsLegacyFontName("-fixed-"));
            Assert.IsTrue(HWindowFont2018.IsLegacyFontName("-Arial-15-*-0-*-*-1-"));
            Assert.IsFalse(HWindowFont2018.IsLegacyFontName("default-Normal-12"));
            Assert.IsFalse(HWindowFont2018.IsLegacyFontName("Arial-Bold-15"));
            Assert.IsFalse(HWindowFont2018.IsLegacyFontName(""));
            Assert.IsFalse(HWindowFont2018.IsLegacyFontName(null));
        }

        /// <summary> 记录设字号调用的假实现；可设成每次都失败 </summary>
        private sealed class RecordingFont : IHWindowFont
        {
            public readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
            public bool Fail;

            public void SetFontSize(HTuple hv_Size, string font, string bold, string slant)
            {
                Calls.Add($"{hv_Size}|{font}|{bold}|{slant}");
                if (Fail) throw new HalconException("Wrong value of control parameter Font");
            }

            public void DispText(string message, HTuple hv_Row, HTuple hv_Column, string color, string coordSystem) { }
        }

        /// <summary>
        /// 注入当前字体名模拟新式命名的窗口：设字号应交给新式实现，不走旧式 "-Arial-..." 拼接。
        /// 命名方式只判断一次，后续设字号不再读当前字体；同参数重复设置直接跳过。
        /// </summary>
        [TestMethod]
        public void Font2018_ModernFontNaming_DelegatesToModern_CheckedOnce()
        {
            WindowHost.Run(host =>
            {
                HOperatorSet.GetFont(host.Window, out HTuple before);
                int reads = 0;
                var modern = new RecordingFont();
                var font = new HWindowFont2018(host.Window, () => { reads++; return "default-Normal-12"; }, () => modern);

                font.SetFontSize(15, "sans", "true", "false");
                font.SetFontSize(15, "sans", "true", "false");
                font.SetFontSize(30, "sans", "true", "false");

                CollectionAssert.AreEqual(new[] { "15|sans|true|false", "30|sans|true|false" }, modern.Calls);
                Assert.AreEqual(1, reads);
                HOperatorSet.GetFont(host.Window, out HTuple after);
                Assert.AreEqual(before.S, after.S, "新式命名下不应走旧式 set_font");
            });
        }

        /// <summary>
        /// 新式实现设字号失败（字体不存在）时只在首次抛出；同参数不再重试，避免每次重放都 query_font 并刷日志。
        /// 换了参数仍会再试。
        /// </summary>
        [TestMethod]
        public void Font2018_ModernFontNaming_FailureNotRetriedForSameArgs()
        {
            WindowHost.Run(host =>
            {
                var modern = new RecordingFont { Fail = true };
                var font = new HWindowFont2018(host.Window, () => "default-Normal-12", () => modern);

                Assert.ThrowsException<HalconException>(() => font.SetFontSize(15, "sans", "true", "false"));
                font.SetFontSize(15, "sans", "true", "false");
                Assert.ThrowsException<HalconException>(() => font.SetFontSize(30, "sans", "true", "false"));

                Assert.AreEqual(2, modern.Calls.Count);
            });
        }

        [TestMethod]
        public void Font2018_LegacyFontNaming_UsesXStyleName_CheckedOnce()
        {
            WindowHost.Run(host =>
            {
                int reads = 0;
                var font = new HWindowFont2018(host.Window, () => { reads++; return "-fixed-"; });

                font.SetFontSize(15, "sans", "true", "false");
                font.SetFontSize(30, "sans", "true", "false");

                HOperatorSet.GetFont(host.Window, out HTuple current);
                Assert.AreEqual("-Arial-30-*-0-*-*-1-", current.S);
                Assert.AreEqual(1, reads);
            });
        }

        [TestMethod]
        public void Font2018_SetFontFailure_RethrowsHalconException()
        {
            // set_font 失败后转抛的是 HDevelop 异常元组（首元素为错误码），
            // 若误取 .S 会变成 HTupleAccessException，只接 HalconException 的调用方接不住。
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2018(host.Window);
                var ex = Assert.ThrowsException<HalconException>(() => font.SetFontSize(-5, "mono", "false", "false"));
                Assert.IsNotInstanceOfType(ex, typeof(HTupleAccessException));
            });
        }

        [TestMethod]
        public void Font2022_InvalidArguments_ThrowHalconException()
        {
            // 2022 版依赖 HALCON 新图形栈；HWindowControl 在这里是 WIN32-Window，
            // query_font 返回旧式字体名、disp_text 直接报 #5123，产品里也没有使用该类，只校验参数检查。
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2022(host.Window);

                var bold = Assert.ThrowsException<HalconException>(() => font.SetFontSize(16, "mono", "yes", "false"));
                StringAssert.Contains(bold.Message, "Bold");
                var slant = Assert.ThrowsException<HalconException>(() => font.SetFontSize(16, "mono", "true", "maybe"));
                StringAssert.Contains(slant.Message, "Slant");
                var name = Assert.ThrowsException<HalconException>(() => font.SetFontSize(16, "NoSuchFont_7f3a", "false", "false"));
                StringAssert.Contains(name.Message, "Font");
            });
        }

        [TestMethod]
        public void Font2022_DispTextFailure_IsLoggedNotThrown()
        {
            // 这里的 WIN32-Window 上 disp_text 必然报 #5123，正好用来验证失败路径；与 2018 版一致只记日志
            WindowHost.Run(host =>
            {
                var font = new HWindowFont2022(host.Window);
                host.Window.SetPart(10, 20, 110, 220);

                using (var log = new CapturingLogger())
                {
                    font.DispText("abc", 30, 40, "no_such_colour", "image");

                    var warn = log.Entries.Single(e => e.Level == LogLevel.Warn);
                    Assert.AreEqual(nameof(HWindowFont2022), warn.Category);
                    Assert.IsInstanceOfType(warn.Exception, typeof(HalconException));
                }
                Assert.AreEqual("10,20,110,220", Part(host.Window));
            });
        }
    }
}
