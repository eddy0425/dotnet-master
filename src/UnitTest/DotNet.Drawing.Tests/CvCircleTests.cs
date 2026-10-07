using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class CvCircleTests
    {
        [TestMethod]
        public void Constructors()
        {
            var a = new CvCircle(1, 2, 3);
            Geom.AreClose(1, 2, a.Center);
            Geom.AreClose(3, a.Radius);
            Assert.IsTrue(a.IsFullCircle);
            Assert.IsFalse(a.IsArc);

            var b = new CvCircle(new Point2d(0, 0), new Point2d(3, 4));
            Geom.AreClose(5, b.Radius);

            var arc = new CvCircle(0, 0, 2, 0, Math.PI / 2);
            Assert.IsTrue(arc.IsArc);
            Geom.AreClose(Math.PI / 2, arc.ArcSpan);
            Geom.AreClose(Math.PI, arc.ArcLength);
            Geom.AreClose(2, 0, arc.StartPoint);
            Geom.AreClose(0, 2, arc.EndPoint);

            Assert.IsTrue(new CvCircle(0, 0, 1, 0, 2 * Math.PI).IsFullCircle);
        }

        [TestMethod]
        public void NegativeRadius_IsRejected_IncludingWithExpression()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CvCircle(0, 0, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new CvCircle(default(Point2d), -1, 0, 1));
            var c = new CvCircle(0, 0, 1);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => c with { Radius = -1 });
        }

        [TestMethod]
        public void Measurements()
        {
            var c = new CvCircle(0, 0, 2);
            Geom.AreClose(4, c.Diameter);
            Geom.AreClose(4 * Math.PI, c.Circumference);
            Geom.AreClose(4 * Math.PI, c.Area);
            Assert.IsFalse(c.IsDegenerate);
            Assert.IsTrue(new CvCircle(0, 0, 0).IsDegenerate);
        }

        [TestMethod]
        public void FromThreePoints()
        {
            var c = CvCircle.FromThreePoints(new Point2d(0, 0), new Point2d(2, 0), new Point2d(1, 1));
            Assert.IsNotNull(c);
            Geom.AreClose(1, 0, c.Center);
            Geom.AreClose(1, c.Radius);

            Assert.IsNull(CvCircle.FromThreePoints(new Point2d(0, 0), new Point2d(1, 1), new Point2d(3, 3)));
            Assert.IsNull(CvCircle.FromThreePoints(new Point2d(1, 1), new Point2d(1, 1), new Point2d(5, 2)));
        }

        [TestMethod]
        public void FromThreePoints_IsTranslationInvariant()
        {
            // 以 p1 为局部原点计算：整体平移到 1e6 量级后结果不应劣化
            const double o = 1e6;
            var c = CvCircle.FromThreePoints(new Point2d(o, o), new Point2d(o + 2, o), new Point2d(o + 1, o + 1));
            Assert.IsNotNull(c);
            Geom.AreClose(o + 1, o, c.Center, 1e-6);
            Assert.AreEqual(1, c.Radius, 1e-6);

            Assert.IsNull(CvCircle.FromThreePoints(new Point2d(o, o), new Point2d(o + 1, o + 1), new Point2d(o + 3, o + 3)));
        }

        [TestMethod]
        public void PointAtAngle_ReturnsPointOnCircle()
        {
            var arc = new CvCircle(0, 0, 2, 0, Math.PI);
            Geom.AreClose(0, 2, arc.PointAtAngle(Math.PI / 2));
            Geom.AreClose(-2, 0, arc.PointAtAngle(Math.PI));
        }

        [TestMethod]
        public void Equality()
        {
            var a = new CvCircle(0, 0, 5);
            var b = new CvCircle(0.004, 0, 5.004);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, new CvCircle(0, 0, 5.1));
            Assert.AreNotEqual(a, new CvCircle(0, 0, 5, 0, 1));
            Geom.AreClose(0, 0, CvCircle.Unit.Center);
            Geom.AreClose(1, CvCircle.Unit.Radius);
        }

        [TestMethod]
        public void Equality_Phi_ConsistentWithHash()
        {
            // 相差不足 1e-9 但跨越 1e-9 网格边界：判等为真时哈希必须相同
            var a = new CvCircle(0, 0, 5, 0.4e-9, 1);
            var b = new CvCircle(0, 0, 5, 0.6e-9, 1);
            if (a.Equals(b))
                Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

            var c = new CvCircle(0, 0, 5, 0, 1 + 0.4e-9);
            var d = new CvCircle(0, 0, 5, 0, 1 + 0.6e-9);
            if (c.Equals(d))
                Assert.AreEqual(c.GetHashCode(), d.GetHashCode());

            Assert.AreEqual(new CvCircle(0, 0, 5, 0, 1), new CvCircle(0, 0, 5, 1e-12, 1));
        }
    }
}
