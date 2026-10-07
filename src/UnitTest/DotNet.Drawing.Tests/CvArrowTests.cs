using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class CvArrowTests
    {
        [TestMethod]
        public void Constructors_ForwardToLine()
        {
            var a = new CvArrow(0, 0, 3, 4);
            Geom.AreClose(0, 0, a.Start);
            Geom.AreClose(3, 4, a.End);
            Geom.AreClose(5, a.Length);
            Geom.AreClose(Math.Atan2(4, 3), a.Angle);
            Geom.AreClose(Math.Atan2(4, 3) * 180 / Math.PI, a.AngleDegrees);
            Geom.AreClose(10, a.HeadSize);
            Geom.AreClose(30, a.HeadAngle);

            var b = new CvArrow(new Point2d(1, 1), new Point2d(1, 3), 5, 45);
            Geom.AreClose(2, b.Length);
            Geom.AreClose(5, b.HeadSize);
            Geom.AreClose(45, b.HeadAngle);

            var line = new CvLine(0, 0, 1, 0);
            Assert.AreSame(line, new CvArrow(line).Line);
        }

        [TestMethod]
        public void Factories()
        {
            var a = CvArrow.FromAngle(new Point2d(1, 1), Math.PI / 2, 4, 6, 20);
            Geom.AreClose(1, 1, a.Start);
            Geom.AreClose(1, 5, a.End);
            Geom.AreClose(6, a.HeadSize);
            Geom.AreClose(20, a.HeadAngle);

            var c = CvArrow.FromCenterAngle(new Point2d(5, 5), 0, 4);
            Geom.AreClose(3, 5, c.Start);
            Geom.AreClose(7, 5, c.End);
            Geom.AreClose(10, c.HeadSize);
        }

        [TestMethod]
        public void NullLine_IsRejected_IncludingWithExpression()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new CvArrow((CvLine)null));
            var a = new CvArrow(0, 0, 1, 0);
            Assert.ThrowsException<ArgumentNullException>(() => a with { Line = null });
        }

        [TestMethod]
        public void Equality_IsToleranceBased()
        {
            var a = new CvArrow(0, 0, 10, 0);
            var b = new CvArrow(0.004, 0, 10, 0.004, 10.004);
            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, new CvArrow(10, 0, 0, 0), "箭头有方向");
            Assert.AreNotEqual(a, new CvArrow(0, 0, 10, 0, headSize: 11));
            Assert.AreNotEqual(a, new CvArrow(0, 0, 10, 0, headAngle: 31));
            Assert.IsFalse(a.Equals(null));
        }

        [TestMethod]
        public void Equality_HeadAngle_ConsistentWithHash()
        {
            // 两值相差不足 1e-9，但分处 1e-9 网格边界两侧：判等若为真，哈希必须相同
            var line = new CvLine(0, 0, 10, 0);
            var a = new CvArrow(line, 10, 0.4e-9);
            var b = new CvArrow(line, 10, 0.6e-9);
            if (a.Equals(b))
                Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

            Assert.AreEqual(new CvArrow(line, 10, 30), new CvArrow(line, 10, 30 + 1e-12));
        }

        [TestMethod]
        public void ToString_ShowsGeometryAndHead()
        {
            Assert.AreEqual("Arrow[(0, 0) → (3, 4), Length=5, Head=10@30°]", new CvArrow(0, 0, 3, 4).ToString());
        }
    }
}
