using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class Rect2dTests
    {
        [TestMethod]
        public void Constructor_RejectsNegativeSize()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Rect2d(0.0, 0.0, -1.0, 1.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Rect2d(0.0, 0.0, 1.0, -1.0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Rect2d(default(Point2d), new Size2d(-1, 1)));
            Geom.AreClose(0, 0, 0, 0, new Rect2d());
        }

        [TestMethod]
        public void RegionSetRectByCorners_RejectsInvertedCorners()
        {
            // 参数校验发生在访问区域前，无需创建依赖 HALCON 原生库的 CvRegion。
            CvRegion region = null;
            Assert.ThrowsException<ArgumentException>(() => region.SetRectByCorners(new Point2d(5, 0), new Point2d(4, 1)));
            Assert.ThrowsException<ArgumentException>(() => region.SetRectByCorners(new Point2d(0, 5), new Point2d(1, 4)));
        }

        [TestMethod]
        public void OffsetOperators()
        {
            var r = new Rect2d(1.0, 1.0, 2.0, 2.0);
            Geom.AreClose(4, 5, 2, 2, r + new Point2d(3, 4));
            Geom.AreClose(-2, -3, 2, 2, r - new Point2d(3, 4));
            Geom.AreClose(1, 1, 5, 6, r + new Size2d(3, 4));
            Geom.AreClose(1, 1, 1, 1, r - new Size2d(1, 1));
            // 越界时截断为零，与 Size2d 相减语义一致
            Geom.AreClose(1, 1, 0, 1, r - new Size2d(5, 1));
        }

        [TestMethod]
        public void Equality()
        {
            var a = new Rect2d(1.0, 2.0, 3.0, 4.0);
            var b = new Rect2d(1.004, 2.0, 3.0, 4.004);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a != new Rect2d(1.0, 2.0, 3.0, 5.0));
            Rect2d n1 = null, n2 = null;
            Assert.IsTrue(n1 == n2);
            Assert.IsFalse(a == n1);
            Assert.IsFalse(n1 == a);
            Assert.IsFalse(a.Equals(null));
        }

        [TestMethod]
        public void Json_RoundTrips()
        {
            var r = new Rect2d(1.5, 2.0, 3.0, 4.25);
            var back = JsonConvert.DeserializeObject<Rect2d>(JsonConvert.SerializeObject(r));
            Geom.AreClose(1.5, 2, 3, 4.25, back);
        }

        [TestMethod]
        public void Json_NegativeSize_IsRejected()
        {
            // [JsonConstructor] 走构造函数校验，落盘数据也不能造出非法矩形；
            // Newtonsoft 不包装构造函数抛出的异常，调用方收到的是原始 ArgumentOutOfRangeException
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => JsonConvert.DeserializeObject<Rect2d>("{\"X\":0,\"Y\":0,\"Width\":-1,\"Height\":1}"));
        }
    }
}
