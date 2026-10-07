using System;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>简单数据类型：默认值与枚举取值（枚举值会被序列化进配置文件，改动即不兼容）。</summary>
    [TestClass]
    public class MiscTypeTests
    {
        [TestMethod]
        public void ZoomImage_Defaults()
        {
            var z = new ZoomImage();
            Assert.AreEqual(0, z.width.I, "不再写死某台相机的分辨率");
            Assert.AreEqual(0, z.height.I);
            Assert.IsTrue(z.parent.IsEmpty);
        }

        [TestMethod]
        public void DrawEnum_Order_IsStable()
        {
            CollectionAssert.AreEqual(
                new[] { DrawEnum.None, DrawEnum.Erase, DrawEnum.DispRect, DrawEnum.DispModel },
                (DrawEnum[])Enum.GetValues(typeof(DrawEnum)));
        }

        [TestMethod]
        public void DrawModelUIArgs_ExposesConstructorArguments()
        {
            var rect = new HObject();
            var contour = new HObject();
            var result = new ModelResult { Row = 1, Column = 2, Angle = 0.5, Score = 0.9 };

            var args = new DrawModelUIArgs("model.shm", rect, contour, result);

            Assert.AreEqual("model.shm", args.ModelPath);
            Assert.AreSame(rect, args.HoModeRect);
            Assert.AreSame(contour, args.HoContour);
            Assert.AreEqual(2, args.Result.Column);
            Assert.AreEqual(0.9, args.Result.Score);
            Assert.IsInstanceOfType(args, typeof(EventArgs));
        }
    }
}
