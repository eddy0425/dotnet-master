using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class HColorTests
    {
        [TestMethod]
        public void Default_IsEmpty_WithEmptyName()
        {
            var c = default(HColor);
            Assert.IsTrue(c.IsEmpty);
            Assert.AreEqual(string.Empty, c.Name);
            Assert.AreEqual(string.Empty, c.ToString());
            Assert.IsTrue(new HColor("").IsEmpty);
            Assert.IsTrue(c == new HColor(null));
            Assert.IsFalse(HColor.Red.IsEmpty);
        }

        [TestMethod]
        public void ImplicitConversions_RoundTripString()
        {
            HColor c = "red";
            string s = c;
            Assert.AreEqual("red", s);
            Assert.AreEqual(HColor.Red, c);
            string fromDefault = default(HColor);
            Assert.AreEqual(string.Empty, fromDefault, "隐式转成字符串不得产出 null");
        }

        [TestMethod]
        public void Equality_IsOrdinal()
        {
            Assert.IsTrue(new HColor("red") == HColor.Red);
            Assert.AreEqual(new HColor("red").GetHashCode(), HColor.Red.GetHashCode());
            Assert.IsTrue(new HColor("Red") != HColor.Red, "HALCON 颜色名区分大小写");
            Assert.IsFalse(HColor.Red.Equals((object)"red"));
            Assert.IsTrue(HColor.Red.Equals((object)new HColor("red")));
        }

        [TestMethod]
        public void Presets_AreNonEmptyDistinctAndWellFormed()
        {
            var presets = typeof(HColor)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(HColor))
                .Select(f => new { f.Name, Color = (HColor)f.GetValue(null) })
                .ToList();

            Assert.IsTrue(presets.Count > 0);
            var format = new Regex("^(#[0-9a-f]{6}|[a-z]+( [a-z]+)*)$");
            foreach (var p in presets)
                Assert.IsTrue(format.IsMatch(p.Color.Name), $"{p.Name} = '{p.Color.Name}'");

            var duplicates = presets.GroupBy(p => p.Color.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.AreEqual(0, duplicates.Count, "重复的预设颜色：" + string.Join(", ", duplicates));
        }
    }

    [TestClass]
    public class DrawStyleTests
    {
        [TestMethod]
        public void Of_SetsOnlyGivenProperties()
        {
            var a = DrawStyle.Of(HColor.Red);
            Assert.AreEqual(HColor.Red, a.Color);
            Assert.IsNull(a.Size);
            Assert.IsNull(a.LineWidth);
            Assert.IsNull(a.DrawMode);

            var b = DrawStyle.Of(HColor.Blue, 5);
            Assert.AreEqual(HColor.Blue, b.Color);
            Assert.AreEqual(5.0, b.Size);
        }

        [TestMethod]
        public void SizeOr_FallsBackWhenUnspecified()
        {
            Assert.AreEqual(DrawStyle.DefaultSize, DrawStyle.SizeOr(null));
            Assert.AreEqual(7.0, DrawStyle.SizeOr(null, 7));
            Assert.AreEqual(7.0, DrawStyle.SizeOr(new DrawStyle(), 7));
            Assert.AreEqual(3.0, DrawStyle.SizeOr(DrawStyle.Of(HColor.Red, 3), 7));
        }

        [TestMethod]
        public void Equality_IsByValue()
        {
            Assert.AreEqual(DrawStyle.Of(HColor.Red, 3), DrawStyle.Of("red", 3));
            Assert.AreEqual(new DrawStyle { LineWidth = 2, DrawMode = "fill" }, new DrawStyle { LineWidth = 2, DrawMode = "fill" });
            Assert.AreNotEqual(DrawStyle.Of(HColor.Red), DrawStyle.Of(HColor.Red) with { LineWidth = 1 });
            Assert.IsTrue(new DrawStyle().Color.IsEmpty, "未指定颜色表示沿用窗口当前颜色");
        }
    }
}
