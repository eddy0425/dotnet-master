using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class CvLineTests
    {
        [TestMethod]
        public void DerivedProperties()
        {
            var line = new CvLine(1, 1, 4, 5);
            Geom.AreClose(5, line.Length);
            Geom.AreClose(25, line.LengthSquared);
            Geom.AreClose(Math.Atan2(4, 3), line.Angle);
            Geom.AreClose(2.5, 3, line.MidPoint);
            Geom.AreClose(3, 4, line.Direction);
            Geom.AreClose(90, new CvLine(0, 0, 0, 1).AngleDegrees);
        }

        [TestMethod]
        public void Factories()
        {
            var a = CvLine.FromAngle(new Point2d(1, 1), Math.PI / 2, 4);
            Geom.AreClose(1, 1, a.Start);
            Geom.AreClose(1, 5, a.End);

            var c = CvLine.FromCenterAngle(new Point2d(5, 5), 0, 4);
            Geom.AreClose(3, 5, c.Start);
            Geom.AreClose(7, 5, c.End);
        }

        [TestMethod]
        public void Distance_BetweenPoints()
        {
            Geom.AreClose(5, CvLine.Distance(new Point2d(0, 0), new Point2d(3, 4)));
            Geom.AreClose(0, CvLine.Distance(new Point2d(3, 4), new Point2d(3, 4)));
        }

        [TestMethod]
        public void Equality_IsToleranceBased()
        {
            var a = new CvLine(0, 0, 10, 0);
            var b = new CvLine(0.004, 0, 10, 0.004);
            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, new CvLine(a.End, a.Start), "线段有方向");
            Assert.IsFalse(a.Equals(null));
        }
    }
}
