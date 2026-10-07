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
