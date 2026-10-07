using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    /// <summary>
    /// 与 HALCON 交界、但不需要原生库即可验证的部分：参数守卫、空句柄处理、纯托管的 HTuple 取值。
    /// </summary>
    /// <remarks>
    /// 测试环境没有 halcon.dll，凡是会调用 HOperatorSet 的路径（包括 <see cref="CvRegion"/> 的构造函数）都不在这里覆盖。
    /// </remarks>
    [TestClass]
    public class HalconBoundaryTests
    {
        #region HObjectExtension

        [TestMethod]
        public void NullHandle_IsNotUsable()
        {
            HObject none = null;
            Assert.IsFalse(none.NotNull());
            Assert.IsFalse(none.IsUsableRegion());
        }

        [TestMethod]
        public void RequireImage_NullHandle_ThrowsWithToolName()
        {
            HObject none = null;
            var ex = Assert.ThrowsException<InvalidOperationException>(() => none.RequireImage("Blob1"));
            StringAssert.Contains(ex.Message, "Blob1");
        }

        #endregion

        #region RegionExtension

        [TestMethod]
        public void RegionExtension_NullRegion_IsIgnored()
        {
            CvRegion region = null;
            region.RebuildRegion();
            region.GenCoordsRegion(new List<CvCoord> { CvCoord.Identity });
            region.GenCoordsRegion(null);
        }

        #endregion

        #region JsonConvertHObject

        [TestMethod]
        public void JsonConvertHObject_ConvertsOnlyHObjectFamily()
        {
            var converter = new JsonConvertHObject();
            Assert.IsTrue(converter.CanConvert(typeof(HObject)));
            Assert.IsTrue(converter.CanConvert(typeof(HRegion)));
            Assert.IsTrue(converter.CanConvert(typeof(HImage)));
            Assert.IsFalse(converter.CanConvert(typeof(HTuple)));
            Assert.IsFalse(converter.CanConvert(typeof(string)));
            Assert.IsFalse(converter.CanConvert(null));
        }

        #endregion

        #region Rect2d(HTuple ...)

        [TestMethod]
        public void Rect2d_FromHalconCorners_MapsRowColumnToXY()
        {
            var r = new Rect2d(new HTuple(1.0), new HTuple(2.0), new HTuple(5.0), new HTuple(10.0));
            Geom.AreClose(2, 1, 8, 4, r);
        }

        [TestMethod]
        public void Rect2d_FromHalconCorners_RejectsNullAndInverted()
        {
            var one = new HTuple(1.0);
            Assert.ThrowsException<ArgumentNullException>(() => new Rect2d(null, one, one, one));
            Assert.ThrowsException<ArgumentNullException>(() => new Rect2d(one, null, one, one));
            Assert.ThrowsException<ArgumentNullException>(() => new Rect2d(one, one, null, one));
            Assert.ThrowsException<ArgumentNullException>(() => new Rect2d(one, one, one, null));

            var ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new Rect2d(new HTuple(0.0), new HTuple(5.0), new HTuple(1.0), new HTuple(4.0)));
            Assert.AreEqual("column2", ex.ParamName);
            ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new Rect2d(new HTuple(5.0), new HTuple(0.0), new HTuple(4.0), new HTuple(1.0)));
            Assert.AreEqual("row2", ex.ParamName);
        }

        #endregion
    }

    [TestClass]
    public class HalconControllerTests
    {
        private string _dir;

        [TestInitialize]
        public void CreateTempDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "DotNet.Drawing.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TestCleanup]
        public void DeleteTempDir()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [TestMethod]
        public void GetPaths_SortsNumericNamesNumerically_ThenOthers()
        {
            foreach (var name in new[] { "abc.bmp", "10.bmp", "2.png", "1.bmp" })
                File.WriteAllText(Path.Combine(_dir, name), "");

            var names = HalconController.GetPaths(_dir).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEqual(new[] { "1.bmp", "2.png", "10.bmp", "abc.bmp" }, names);
        }

        [TestMethod]
        public void GetPaths_EmptyFolder_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(() => HalconController.GetPaths(_dir));
        }
    }
}
