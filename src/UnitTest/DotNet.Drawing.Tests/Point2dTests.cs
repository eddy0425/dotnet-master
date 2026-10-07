using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class Point2dTests
    {
        [TestMethod]
        public void Operators()
        {
            var a = new Point2d(1, 2);
            var b = new Point2d(3, 4);
            Geom.AreClose(4, 6, a + b);
            Geom.AreClose(-2, -2, a - b);
            Geom.AreClose(2, 4, a * 2);
            Geom.AreClose(2, 4, 2 * a);
            Geom.AreClose(0.5, 1, a / 2);
            Geom.AreClose(-1, -2, -a);
        }

        [TestMethod]
        public void Divide_OnlyExactZeroThrows()
        {
            Assert.ThrowsException<DivideByZeroException>(() => new Point2d(1, 1) / 0);
            Geom.AreClose(1e10, 2e10, new Point2d(1, 2) / 1e-10, 1);
        }

        [TestMethod]
        public void Equality_UsesPixelGrid()
        {
            var a = new Point2d(1, 1);
            var near = new Point2d(1.004, 1);
            Assert.IsTrue(a == near);
            Assert.IsTrue(a.Equals((object)near));
            Assert.AreEqual(a.GetHashCode(), near.GetHashCode());
            Assert.IsTrue(a != new Point2d(1.006, 1));
            Assert.IsFalse(a.Equals("(1, 1)"));

            var set = new HashSet<Point2d> { a };
            Assert.IsTrue(set.Contains(near), "判等为真的两点必须落入同一哈希桶");
        }

        [TestMethod]
        public void ToString_ShowsCoordinates()
        {
            Assert.AreEqual("(1.5, 2)", new Point2d(1.5, 2).ToString());
            StringAssert.Contains(new CvLine(0, 0, 3, 4).ToString(), "(0, 0) → (3, 4)");
        }

        [TestMethod]
        public void Json_RoundTripsOnlyCoordinates()
        {
            string json = JsonConvert.SerializeObject(new Point2d(1.5, -2));
            Assert.AreEqual("{\"X\":1.5,\"Y\":-2.0}", json);
            var back = JsonConvert.DeserializeObject<Point2d>(json);
            Geom.AreClose(1.5, -2, back);
        }
    }
}
