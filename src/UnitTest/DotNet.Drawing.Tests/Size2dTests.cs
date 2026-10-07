using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class Size2dTests
    {
        [TestMethod]
        public void Operators()
        {
            var a = new Size2d(3, 4);
            var b = new Size2d(1, 5);
            Assert.AreEqual(new Size2d(4, 9), a + b);
            Assert.AreEqual(new Size2d(2, 0), a - b, "负分量截断为 0");
            Assert.AreEqual(new Size2d(6, 8), a * 2);
            Assert.AreEqual(new Size2d(6, 8), 2 * a);
            Assert.AreEqual(new Size2d(0, 0), a * 0);
            Assert.AreEqual(new Size2d(1.5, 2), a / 2);
        }

        [TestMethod]
        public void Operators_RejectInvalidScalars()
        {
            var a = new Size2d(3, 4);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => a * -1);
            Assert.ThrowsException<DivideByZeroException>(() => a / 0);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => a / -2);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Size2d(-1, 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Size2d(0, -1));
        }

        [TestMethod]
        public void ImplicitPoint_AndEquality()
        {
            Point2d p = new Size2d(3, 4);
            Geom.AreClose(3, 4, p);

            var a = new Size2d(3, 4);
            var b = new Size2d(3.004, 3.996);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a != new Size2d(3.1, 4));
            Assert.IsFalse(a.Equals((object)new Point2d(3, 4)));
        }

        [TestMethod]
        public void Json_RoundTrips()
        {
            var s = new Size2d(3.0, 4.25);
            var back = JsonConvert.DeserializeObject<Size2d>(JsonConvert.SerializeObject(s));
            Assert.AreEqual(3.0, back.Width, 1e-12);
            Assert.AreEqual(4.25, back.Height, 1e-12);
        }

        [TestMethod]
        public void Json_NegativeSize_IsRejected()
        {
            // [JsonConstructor] 走构造函数校验，落盘数据也不能造出非法尺寸；
            // Newtonsoft 不包装构造函数抛出的异常，调用方收到的是原始 ArgumentOutOfRangeException
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => JsonConvert.DeserializeObject<Size2d>("{\"Width\":-1,\"Height\":1}"));
        }
    }
}
