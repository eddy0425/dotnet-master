using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class CvCoordTests
    {
        private static void AreClose(double x, double y, double radians, CvCoord actual, double eps = Geom.Eps)
        {
            Assert.AreEqual(x, actual.X, eps, $"X of {actual}");
            Assert.AreEqual(y, actual.Y, eps, $"Y of {actual}");
            Assert.AreEqual(radians, actual.Angle.Radians, eps, $"Angle of {actual}");
        }

        [TestMethod]
        public void Constructor_NormalizesAngle()
        {
            AreClose(1, 2, -Math.PI / 2, CvCoord.FromDegrees(1, 2, 270));
            AreClose(0, 0, -Math.PI, CvCoord.FromDegrees(0, 0, 180));
            AreClose(0, 0, -Math.PI, CvCoord.FromRadians(0, 0, 3 * Math.PI));
            AreClose(3, 4, 0.5, new CvCoord(new Point2d(3, 4), Angle.FromRadians(0.5)));
            AreClose(3, 4, 0, new CvCoord(3, 4));
        }

        [TestMethod]
        public void DerivedProperties()
        {
            var c = CvCoord.FromDegrees(1, 2, 90);
            Geom.AreClose(90, c.AngleDegrees);
            Geom.AreClose(1, 2, c.Center);
            Geom.AreClose(0, 1, c.Direction);
            Assert.IsTrue(CvCoord.Identity.IsIdentity);
            Assert.IsTrue(CvCoord.Zero.IsIdentity);
            Assert.IsFalse(c.IsIdentity);
        }

        [TestMethod]
        public void Equality()
        {
            var a = CvCoord.FromRadians(1, 2, 0.5);
            var b = CvCoord.FromRadians(1.004, 2, 0.5 + 1e-12);
            Assert.IsTrue(a == b);
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a != CvCoord.FromRadians(1, 2, 0.5001));
            Assert.IsFalse(a.Equals("x"));
            // π 与 -π 经构造规范化后是同一个朝向
            Assert.AreEqual(CvCoord.FromRadians(0, 0, Math.PI), CvCoord.FromRadians(0, 0, -Math.PI));
        }

        [TestMethod]
        public void Json_RoundTrips()
        {
            var c = CvCoord.FromRadians(1.5, -2, 0.75);
            string json = JsonConvert.SerializeObject(c);
            Assert.AreEqual("{\"X\":1.5,\"Y\":-2.0,\"Angle\":0.75}", json);
            AreClose(1.5, -2, 0.75, JsonConvert.DeserializeObject<CvCoord>(json));
        }

        [TestMethod]
        public void WithExpression_NormalizesAngle()
        {
            // with 绕过构造函数，归一化必须由 init 访问器兜底
            var c = CvCoord.Identity with { Angle = Angle.FromRadians(2 * Math.PI) };
            AreClose(0, 0, 0, c);
            Assert.AreEqual(CvCoord.Identity, c);
            AreClose(0, 0, -Math.PI / 2, CvCoord.Identity with { Angle = Angle.FromDegrees(270) });
        }

        [TestMethod]
        public void Json_UnnormalizedAngle_IsNormalized()
        {
            AreClose(0, 0, 7 - 2 * Math.PI, JsonConvert.DeserializeObject<CvCoord>("{\"X\":0,\"Y\":0,\"Angle\":7}"));
        }

        [TestMethod]
        public void ToString_IsReadable()
        {
            StringAssert.Contains(CvCoord.FromDegrees(1, 2, 90).ToString(), "90");
        }
    }
}
