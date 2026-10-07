using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class AngleTests
    {
        [TestMethod]
        public void Factories_ConvertUnitsOnce()
        {
            Geom.AreClose(Math.PI, Angle.FromDegrees(180).Radians);
            Geom.AreClose(90, Angle.FromRadians(Math.PI / 2).Degrees);
            Assert.AreEqual(0.0, Angle.Zero.Radians);
        }

        [TestMethod]
        public void Normalized_And_NormalizedPositive()
        {
            Geom.AreClose(-Math.PI / 2, Angle.FromRadians(3 * Math.PI / 2).Normalized.Radians);
            Geom.AreClose(3 * Math.PI / 2, Angle.FromRadians(-Math.PI / 2).NormalizedPositive.Radians);
        }

        [TestMethod]
        public void DifferenceTo_TakesShortestWay()
        {
            Geom.AreClose(20, Angle.FromDegrees(170).DifferenceTo(Angle.FromDegrees(-170)).Degrees);
            Geom.AreClose(-20, Angle.FromDegrees(-170).DifferenceTo(Angle.FromDegrees(170)).Degrees);
        }

        [TestMethod]
        public void Direction_IsUnitVector()
        {
            Geom.AreClose(0, 1, Angle.FromDegrees(90).Direction);
            Geom.AreClose(-1, 0, Angle.FromDegrees(180).Direction);
        }

        [TestMethod]
        public void Arithmetic()
        {
            var a = Angle.FromRadians(1);
            var b = Angle.FromRadians(0.25);
            Geom.AreClose(1.25, (a + b).Radians);
            Geom.AreClose(0.75, (a - b).Radians);
            Geom.AreClose(-1, (-a).Radians);
            Geom.AreClose(2, (a * 2).Radians);
            Geom.AreClose(2, (2 * a).Radians);
            Geom.AreClose(0.5, (a / 2).Radians);
        }

        [TestMethod]
        public void Equality_UsesStrictGrid()
        {
            Assert.AreEqual(Angle.FromRadians(1), Angle.FromRadians(1 + 1e-12));
            Assert.IsTrue(Angle.FromRadians(1) == Angle.FromRadians(1 + 1e-12));
            Assert.AreEqual(Angle.FromRadians(1).GetHashCode(), Angle.FromRadians(1 + 1e-12).GetHashCode());
            Assert.IsTrue(Angle.FromRadians(1) != Angle.FromRadians(1 + 1e-6));
            Assert.IsFalse(Angle.FromRadians(1).Equals((object)1.0));
        }

        [TestMethod]
        public void Comparison()
        {
            var small = Angle.FromRadians(0.1);
            var big = Angle.FromRadians(0.2);
            Assert.IsTrue(small < big);
            Assert.IsTrue(big > small);
            Assert.IsTrue(small <= Angle.FromRadians(0.1));
            Assert.IsTrue(big >= small);
            Assert.AreEqual(0, small.CompareTo(Angle.FromRadians(0.1 + 1e-12)));
        }

        [TestMethod]
        public void IsFinite()
        {
            Assert.IsTrue(Angle.FromRadians(1).IsFinite);
            Assert.IsFalse(Angle.FromRadians(double.NaN).IsFinite);
            Assert.IsFalse(Angle.FromRadians(double.PositiveInfinity).IsFinite);
        }

        #region JSON：落盘形状是弧度数值

        private sealed class Holder
        {
            public Angle A { get; set; }
            public Angle? N { get; set; }
        }

        [TestMethod]
        public void Json_WritesRadiansAsNumber()
        {
            string json = JsonConvert.SerializeObject(new Holder { A = Angle.FromRadians(0.5) });
            Assert.AreEqual("{\"A\":0.5,\"N\":null}", json);
        }

        [TestMethod]
        public void Json_RoundTrips()
        {
            var back = JsonConvert.DeserializeObject<Holder>(
                JsonConvert.SerializeObject(new Holder { A = Angle.FromRadians(1.25), N = Angle.FromRadians(-2) }));
            Geom.AreClose(1.25, back.A.Radians);
            Assert.IsTrue(back.N.HasValue);
            Geom.AreClose(-2, back.N.Value.Radians);
        }

        [DataTestMethod]
        [DataRow("{\"A\":1}", 1.0)]
        [DataRow("{\"A\":\"1.5\"}", 1.5)]
        [DataRow("{\"A\":{\"Radians\":0.75}}", 0.75)]
        [DataRow("{\"A\":{\"degrees\":90}}", Math.PI / 2)]
        [DataRow("{\"A\":{\"Degrees\":90,\"Radians\":1}}", 1.0)]  // 同时给出时以 Radians 为准，与顺序无关
        [DataRow("{\"A\":{\"Radians\":1,\"Degrees\":90}}", 1.0)]
        [DataRow("{\"A\":{\"Other\":[1,2],\"Radians\":2}}", 2.0)]
        public void Json_AcceptsLegacyShapes(string json, double expectedRadians)
        {
            Geom.AreClose(expectedRadians, JsonConvert.DeserializeObject<Holder>(json).A.Radians);
        }

        [TestMethod]
        public void Json_NullableAcceptsNull()
        {
            Assert.IsNull(JsonConvert.DeserializeObject<Holder>("{\"A\":0,\"N\":null}").N);
        }

        [DataTestMethod]
        [DataRow("{\"A\":null}")]
        [DataRow("{\"A\":\"abc\"}")]
        [DataRow("{\"A\":{}}")]
        [DataRow("{\"A\":{\"Radians\":\"x\"}}")]
        [DataRow("{\"A\":true}")]
        public void Json_RejectsInvalidInput(string json)
        {
            Assert.ThrowsException<JsonSerializationException>(() => JsonConvert.DeserializeObject<Holder>(json));
        }

        #endregion
    }
}
