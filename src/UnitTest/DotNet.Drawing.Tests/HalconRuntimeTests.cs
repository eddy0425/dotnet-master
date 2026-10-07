using System;
using System.Collections.Generic;
using System.IO;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DotNet.Drawing.Tests
{
    /// <summary>
    /// 依赖 HALCON 原生运行时（halcon.dll + 许可）的测试。
    /// </summary>
    /// <remarks>运行时不可用时整组标记为 Inconclusive，而不是失败。</remarks>
    public abstract class HalconTestBase
    {
        private static readonly Lazy<string> RuntimeError = new Lazy<string>(() =>
        {
            try
            {
                HOperatorSet.GenEmptyObj(out HObject probe);
                probe.Dispose();
                // 默认按系统图像尺寸（初始 128×128，随创建过的最大图像增长）裁剪区域，
                // 结果会随测试执行顺序变化；这里只验证本库逻辑，关掉裁剪。
                HOperatorSet.SetSystem("clip_region", "false");
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        });

        [TestInitialize]
        public void RequireHalcon()
        {
            if (RuntimeError.Value != null)
                Assert.Inconclusive("HALCON 运行时不可用：" + RuntimeError.Value);
        }

        /// <summary>取区域的面积与中心（X=Column, Y=Row）。</summary>
        protected static double AreaCenter(HObject region, out Point2d center)
        {
            HOperatorSet.AreaCenter(region, out HTuple area, out HTuple row, out HTuple column);
            center = new Point2d(column.D, row.D);
            return area.D;
        }

        protected static HObject Rectangle1(double row1, double column1, double row2, double column2)
        {
            HOperatorSet.GenRectangle1(out HObject rect, row1, column1, row2, column2);
            return rect;
        }

        protected static CvRegion NewRegion(RectEnum type, double x, double y, double width, double height)
            => new CvRegion { Type = type, Bounds = new Rect2d(x, y, width, height) };
    }

    [TestClass]
    public class CvRegionTests : HalconTestBase
    {
        [TestMethod]
        public void NewRegion_HoldsInitializedEmptyObject()
        {
            using (var r = new CvRegion())
            {
                Assert.IsTrue(r.HoRegion.NotNull(), "构造函数应生成已初始化的空对象，而不是空壳 HObject");
                Assert.IsFalse(r.HoRegion.IsUsableRegion(), "空对象 count_obj 为 0，不可直接送进算子");
                Assert.AreEqual(RectEnum.Rectangle, r.Type);
                Assert.AreEqual(0.0, r.Phi.D);
                Assert.IsTrue(r.AddOrDecrease);
            }
        }

        [TestMethod]
        public void HoRegion_Assignment_DisposesPreviousHandle()
        {
            using (var r = new CvRegion())
            {
                var first = Rectangle1(0, 0, 10, 10);
                r.HoRegion = first;
                Assert.IsTrue(first.IsInitialized());

                r.HoRegion = first;
                Assert.IsTrue(first.IsInitialized(), "赋入相同引用不应释放它");

                var second = Rectangle1(0, 0, 5, 5);
                r.HoRegion = second;
                Assert.IsFalse(first.IsInitialized(), "旧句柄应被释放");
                Assert.AreSame(second, r.HoRegion);
            }
        }

        [TestMethod]
        public void Dispose_ReleasesHandle_AndIsIdempotent()
        {
            var r = new CvRegion();
            var handle = r.HoRegion;
            r.Dispose();
            Assert.IsNull(r.HoRegion);
            Assert.IsFalse(handle.IsInitialized());
            r.Dispose();
            Assert.ThrowsException<ObjectDisposedException>(() => r.Clone());
        }

        [TestMethod]
        public void BoundsForwarding_AndDerivedProperties()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 10, 20, 30, 40))
            {
                Assert.AreEqual(10.0, r.Left);
                Assert.AreEqual(20.0, r.Top);
                Assert.AreEqual(40.0, r.Right);
                Assert.AreEqual(60.0, r.Bottom);
                Geom.AreClose(25, 40, r.Center);
                Assert.AreEqual(new Size2d(30, 40), r.Size);

                r.X = 0;
                r.Height = 10;
                Geom.AreClose(0, 20, 30, 10, r.Bounds);

                Assert.ThrowsException<ArgumentOutOfRangeException>(() => r.Width = -1);
                Assert.ThrowsException<ArgumentNullException>(() => r.Bounds = null);
            }
        }

        [TestMethod]
        public void RectSetters()
        {
            using (var r = new CvRegion())
            {
                r.Center = new Point2d(50, 50);
                Geom.AreClose(50, 50, 0, 0, r.Bounds);

                r.SetRectByCenter(new Point2d(50, 50), new Size2d(20, 10));
                Geom.AreClose(40, 45, 20, 10, r.Bounds);

                r.Center = new Point2d(0, 0);
                Geom.AreClose(-10, -5, 20, 10, r.Bounds);

                r.SetRectByCorners(new Point2d(1, 2), new Point2d(4, 8));
                Geom.AreClose(1, 2, 3, 6, r.Bounds);
                Assert.ThrowsException<ArgumentException>(() => r.SetRectByCorners(new Point2d(4, 2), new Point2d(1, 8)));
                Assert.ThrowsException<ArgumentException>(() => r.SetRectByCorners(new Point2d(1, 8), new Point2d(4, 2)));

                r.SetRectByCorners(new HTuple(2.0), new HTuple(1.0), new HTuple(8.0), new HTuple(4.0));
                Geom.AreClose(1, 2, 3, 6, r.Bounds);
            }
        }

        [TestMethod]
        public void Clone_IsDeepAndIndependent()
        {
            using (var r = NewRegion(RectEnum.Polygon, 1, 2, 3, 4))
            {
                r.Phi = new HTuple(0.5);
                r.PolygonX = new HTuple(new[] { 0.0, 10, 10 });
                r.PolygonY = new HTuple(new[] { 0.0, 0, 10 });
                r.MaxRadius = 50;
                r.AddOrDecrease = false;
                r.HoRegion = Rectangle1(0, 0, 9, 9);

                using (var c = r.Clone())
                {
                    Assert.AreEqual(r, c);
                    Assert.AreEqual(r.GetHashCode(), c.GetHashCode());
                    Assert.AreNotSame(r.HoRegion, c.HoRegion);
                    Assert.AreNotSame(r.Phi, c.Phi);
                    Assert.AreNotSame(r.PolygonX, c.PolygonX);

                    r.PolygonX[0] = 99.0;
                    Assert.AreEqual(0.0, c.PolygonX[0].D, "HTuple 必须深拷贝");

                    r.Dispose();
                    Assert.AreEqual(100.0, AreaCenter(c.HoRegion, out _), "原件释放后克隆的句柄仍然有效");
                }
            }
        }

        [TestMethod]
        public void Equality_IgnoresHandle_ComparesParameters()
        {
            using (var a = NewRegion(RectEnum.AffRect, 0, 0, 10, 10))
            using (var b = NewRegion(RectEnum.AffRect, 0.004, 0, 10, 10))
            {
                a.HoRegion = Rectangle1(0, 0, 1, 1);
                Assert.AreEqual(a, b, "HoRegion 不参与判等，外接矩形按几何容差比较");
                Assert.IsTrue(a == b);
                Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

                b.Phi = new HTuple(1e-12);
                Assert.AreEqual(a, b);
                Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

                b.Phi = new HTuple(0.1);
                Assert.AreNotEqual(a, b);
                b.Phi = new HTuple(0.0);

                b.PolygonX = new HTuple(1.0);
                Assert.AreNotEqual(a, b, "null 与非 null 点集不相等");
                b.PolygonX = null;

                b.Type = RectEnum.Ellipse;
                Assert.AreNotEqual(a, b);
                b.Type = RectEnum.AffRect;

                b.MinRadius = 99;
                Assert.AreNotEqual(a, b);
            }

            using (var r = new CvRegion())
            {
                Assert.IsFalse(r.Equals(null));
                Assert.IsFalse(r.Equals((object)new Rect2d()), "CvRegion 与 Rect2d 互不相等");
                Assert.IsTrue((CvRegion)null == null);
                Assert.IsFalse(r == null);
            }
        }
    }

    [TestClass]
    public class RegionExtensionHalconTests : HalconTestBase
    {
        [TestMethod]
        public void Rebuild_Rectangle()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 10, 20, 30, 40))
            {
                r.RebuildRegion();
                // gen_rectangle1 的行列都是闭区间
                Assert.AreEqual(31.0 * 41, AreaCenter(r.HoRegion, out var center));
                Geom.AreClose(25, 40, center);
            }
        }

        [TestMethod]
        public void Rebuild_AffineRectangle_UsesPhi()
        {
            using (var r = NewRegion(RectEnum.AffRect, 0, 0, 100, 20))
            {
                r.RebuildRegion();
                HOperatorSet.SmallestRectangle1(r.HoRegion, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                Assert.IsTrue(c2.D - c1.D > r2.D - r1.D, "Phi=0 时长边沿 X 方向");

                r.Phi = Math.PI / 2;
                r.RebuildRegion();
                HOperatorSet.SmallestRectangle1(r.HoRegion, out r1, out c1, out r2, out c2);
                Assert.IsTrue(r2.D - r1.D > c2.D - c1.D, "Phi=π/2 时长边沿 Y 方向");

                Assert.AreEqual(100 * 20, AreaCenter(r.HoRegion, out var center), 150);
                Geom.AreClose(50, 10, center);
            }
        }

        [TestMethod]
        public void Rebuild_Circle_AndEllipse()
        {
            using (var r = NewRegion(RectEnum.Circle, 0, 0, 100, 100))
            {
                r.RebuildRegion();
                Assert.AreEqual(Math.PI * 50 * 50, AreaCenter(r.HoRegion, out var center), 150);
                Assert.AreEqual(50, center.X, 0.5);
                Assert.AreEqual(50, center.Y, 0.5);

                r.Type = RectEnum.Ellipse;
                r.Height = 40;
                r.RebuildRegion();
                Assert.AreEqual(Math.PI * 50 * 20, AreaCenter(r.HoRegion, out center), 150);
                Assert.AreEqual(50, center.X, 0.5);
                Assert.AreEqual(20, center.Y, 0.5);
            }
        }

        [TestMethod]
        public void Rebuild_Ring()
        {
            using (var r = NewRegion(RectEnum.Ring, 200, 200, 0, 0))
            {
                r.MaxRadius = 50;
                r.MinRadius = 30;
                r.RebuildRegion();
                Assert.AreEqual(Math.PI * (50 * 50 - 30 * 30), AreaCenter(r.HoRegion, out var center), 300);
                Assert.AreEqual(200, center.X, 1);
                Assert.AreEqual(200, center.Y, 1);
            }
        }

        [TestMethod]
        public void Rebuild_Polygon_TreatsPolygonXAsColumns()
        {
            // 细长三角形：X 方向 0..100，Y 方向 0..10。PolygonX 存 Column、PolygonY 存 Row（与 HDisplay 采集时一致）
            using (var r = NewRegion(RectEnum.Polygon, 0, 0, 0, 0))
            {
                r.PolygonX = new HTuple(new[] { 0.0, 100, 0 });
                r.PolygonY = new HTuple(new[] { 0.0, 0, 10 });
                r.RebuildRegion();

                HOperatorSet.SmallestRectangle1(r.HoRegion, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                Assert.AreEqual(100, c2.D - c1.D, 1, "X 跨度");
                Assert.AreEqual(10, r2.D - r1.D, 1, "Y 跨度");
            }
        }

        [TestMethod]
        public void Rebuild_Polygon_WithoutPoints_KeepsHandle()
        {
            using (var r = NewRegion(RectEnum.Polygon, 0, 0, 10, 10))
            {
                var before = r.HoRegion;
                r.RebuildRegion();
                Assert.AreSame(before, r.HoRegion);
                Assert.IsTrue(before.IsInitialized());
            }
        }

        [TestMethod]
        public void Rebuild_ReplacesAndReleasesOldHandle()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 0, 0, 5, 5))
            {
                var before = r.HoRegion;
                r.RebuildRegion();
                Assert.AreNotSame(before, r.HoRegion);
                Assert.IsFalse(before.IsInitialized());
            }
        }

        [TestMethod]
        public void GenCoordsRegion_UnionsRectanglesAtEachCoord()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10))
            {
                r.GenCoordsRegion(new List<CvCoord> { new CvCoord(100, 100), new CvCoord(300, 100) });
                HOperatorSet.Connection(r.HoRegion, out HObject parts);
                using (parts)
                {
                    Assert.AreEqual(2, parts.CountObj());
                }
                Assert.AreEqual(2.0 * 11 * 11, AreaCenter(r.HoRegion, out var center));
                Geom.AreClose(200, 100, center);

                r.GenCoordsRegion(new List<CvCoord>());
                Assert.AreEqual(2.0 * 11 * 11, AreaCenter(r.HoRegion, out _), "空坐标集不改变区域");
            }
        }

        [TestMethod]
        public void GenCoordsRegion_DisposedRegion_IsIgnored()
        {
            var r = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10);
            r.Dispose();
            r.GenCoordsRegion(new List<CvCoord> { CvCoord.Identity });
            Assert.IsNull(r.HoRegion);
        }
    }

    [TestClass]
    public class HObjectExtensionHalconTests : HalconTestBase
    {
        [TestMethod]
        public void NotNull_And_IsUsableRegion()
        {
            using (var shell = new HObject())
            {
                Assert.IsFalse(shell.NotNull(), "未初始化的空壳");
                Assert.IsFalse(shell.IsUsableRegion());
            }

            HOperatorSet.GenEmptyObj(out HObject empty);
            using (empty)
            {
                Assert.IsTrue(empty.NotNull());
                Assert.IsFalse(empty.IsUsableRegion());
            }

            using (var rect = Rectangle1(0, 0, 1, 1))
            {
                Assert.IsTrue(rect.IsUsableRegion());
                rect.Dispose();
                Assert.IsFalse(rect.NotNull(), "已释放的句柄");
            }
        }

        [TestMethod]
        public void RequireImage_ReturnsSameInstance()
        {
            HOperatorSet.GenImageConst(out HObject image, "byte", 8, 8);
            using (image)
            {
                Assert.AreSame(image, image.RequireImage("T"));
            }
            Assert.ThrowsException<InvalidOperationException>(() => new HObject().RequireImage("T"));
        }

        [TestMethod]
        public void RequireImage_EmptyObject_ThrowsWithToolName()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            using (empty)
            {
                var ex = Assert.ThrowsException<InvalidOperationException>(() => empty.RequireImage("Blob1"));
                StringAssert.Contains(ex.Message, "Blob1");
            }
        }
    }

    [TestClass]
    public class HalconControllerHalconTests : HalconTestBase
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
        public void TransPoint_Translation()
        {
            var p = HalconController.TransPoint(new Point2d(10, 20), new Point2d(15, 18), new Point2d(100, 200));
            Geom.AreClose(105, 198, p);
        }

        [TestMethod]
        public void TransPoint_Rotation_UsesPixelConvention()
        {
            // TransPoint 走 affine_trans_pixel：像素 (r, c) 先平移到 (r+0.5, c+0.5) 再变换、最后减回 0.5，
            // 与 affine_trans_region / affine_trans_contour_xld 一致，但不同于 affine_trans_point_2d。
            // 因此绕原点转 +90° 时 (X=10, Y=0) 落到 (0, -11) 而不是 (0, -10)。
            var p = HalconController.TransPoint(CvCoord.FromDegrees(0, 0, 0), CvCoord.FromDegrees(0, 0, 90), new Point2d(10, 0));
            Assert.AreEqual(0, p.X, 1e-9);
            Assert.AreEqual(-11, p.Y, 1e-9);

            // 纯平移不受约定影响：origin 精确映射到 target
            var moved = HalconController.TransPoint(CvCoord.FromDegrees(5, 5, 30), CvCoord.FromDegrees(50, 60, 30), new Point2d(5, 5));
            Geom.AreClose(50, 60, moved);
        }

        [TestMethod]
        public void VectorAngleToRigid_IdentityForSameCoord()
        {
            var c = CvCoord.FromDegrees(12, 34, 56);
            HalconController.VectorAngleToRigid(c, c, out HTuple mat);
            HOperatorSet.HomMat2dIdentity(out HTuple identity);
            Assert.AreEqual(identity.Length, mat.Length);
            for (int i = 0; i < mat.Length; i++)
                Assert.AreEqual(identity[i].D, mat[i].D, 1e-9);
        }

        [TestMethod]
        public void TransRegion_MovesRegion()
        {
            using (var rect = Rectangle1(0, 0, 10, 10))
            {
                HalconController.TransRegion(new Point2d(0, 0), new Point2d(100, 50), rect, out HObject moved);
                using (moved)
                {
                    Assert.AreEqual(121.0, AreaCenter(moved, out var center));
                    Geom.AreClose(105, 55, center);
                }
            }
        }

        [TestMethod]
        public void TransRegion_AgreesWithTransPoint()
        {
            // 单像素区域旋转 90° 无插值误差：区域落点必须与 TransPoint 的结果完全一致
            var origin = CvCoord.FromDegrees(50, 50, 0);
            var target = CvCoord.FromDegrees(80, 70, 90);
            using (var pixel = Rectangle1(20, 30, 20, 30))
            {
                HalconController.TransRegion(origin, target, pixel, out HObject moved);
                using (moved)
                {
                    Assert.AreEqual(1.0, AreaCenter(moved, out var center));
                    Geom.AreClose(HalconController.TransPoint(origin, target, new Point2d(30, 20)), center);
                }
            }
        }

        [TestMethod]
        public void TransContourXld_MovesContour_ConsistentWithTransPoint()
        {
            HOperatorSet.GenContourPolygonXld(out HObject contour, new HTuple(new[] { 0.0, 0 }), new HTuple(new[] { 0.0, 10 }));
            using (contour)
            {
                HalconController.TransContourXld(new Point2d(0, 0), new Point2d(3, 4), contour, out HObject moved);
                using (moved)
                {
                    HOperatorSet.GetContourXld(moved, out HTuple rows, out HTuple cols);
                    Assert.AreEqual(4, rows[0].D, 1e-9);
                    Assert.AreEqual(3, cols[0].D, 1e-9);
                    Assert.AreEqual(13, cols[1].D, 1e-9);
                }

                var origin = CvCoord.Identity;
                var target = CvCoord.FromDegrees(20, 30, 135);
                HalconController.TransContourXld(origin, target, contour, out HObject rotated);
                using (rotated)
                {
                    HOperatorSet.GetContourXld(rotated, out HTuple rows, out HTuple cols);
                    // XLD 坐标以 float32 存储
                    const double xldEps = 1e-5;
                    Geom.AreClose(HalconController.TransPoint(origin, target, new Point2d(0, 0)), new Point2d(cols[0].D, rows[0].D), xldEps);
                    Geom.AreClose(HalconController.TransPoint(origin, target, new Point2d(10, 0)), new Point2d(cols[1].D, rows[1].D), xldEps);
                }
            }
        }

        [TestMethod]
        public void SaveSmallestRectImage_CropsWith20pxMargin_AndCreatesFolder()
        {
            HOperatorSet.GenImageConst(out HObject image, "byte", 200, 200);
            using (image)
            using (var roi = Rectangle1(50, 60, 70, 100))
            {
                string path = Path.Combine(_dir, "sub", "dir", "part.bmp");
                HalconController.SaveSmallestRectImage(image, roi, path);

                Assert.IsTrue(File.Exists(path));
                HOperatorSet.ReadImage(out HObject saved, path);
                using (saved)
                {
                    HOperatorSet.GetImageSize(saved, out HTuple width, out HTuple height);
                    Assert.AreEqual(41 + 40, width.I);
                    Assert.AreEqual(21 + 40, height.I);
                }
            }
        }

        [TestMethod]
        public void SaveSmallestRectImage_InvalidPath_WrapsException()
        {
            HOperatorSet.GenImageConst(out HObject image, "byte", 50, 50);
            using (image)
            using (var roi = Rectangle1(20, 20, 30, 30))
            {
                var ex = Assert.ThrowsException<Exception>(() => HalconController.SaveSmallestRectImage(image, roi, null));
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentNullException));
                StringAssert.StartsWith(ex.Message, "保存小区域图像失败");
            }
        }
    }

    [TestClass]
    public class JsonConvertHObjectHalconTests : HalconTestBase
    {
        [TestMethod]
        public void CvRegion_RoundTripsGeometryAndHandle()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 10, 20, 30, 40))
            {
                r.Phi = new HTuple(0.25);
                r.RebuildRegion();

                string json = JsonConvert.SerializeObject(r);
                using (var back = JsonConvert.DeserializeObject<CvRegion>(json))
                {
                    Assert.AreEqual(r, back);
                    Assert.AreEqual(0.25, back.Phi.D, 1e-12);
                    Assert.IsTrue(back.HoRegion.IsUsableRegion());
                    Assert.AreEqual(AreaCenter(r.HoRegion, out var c1), AreaCenter(back.HoRegion, out var c2));
                    Geom.AreClose(c1, c2);
                }
            }
        }

        [TestMethod]
        public void DisposedRegion_WritesNull()
        {
            var r = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10);
            r.Dispose();
            // HoRegion 为 null 时 Newtonsoft 直接写 null，不经过转换器
            StringAssert.Contains(JsonConvert.SerializeObject(r), "\"HoRegion\":null");
        }

        [TestMethod]
        public void ReleasedHandle_WritesDestroyed_AndReadsBackAsEmptyShell()
        {
            using (var r = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10))
            {
                r.HoRegion.Dispose(); // 句柄对象还在，但已释放
                string json = JsonConvert.SerializeObject(r);
                StringAssert.Contains(json, "\"HoRegion\":\"Destroyed\"");

                using (var back = JsonConvert.DeserializeObject<CvRegion>(json))
                {
                    Assert.IsNotNull(back.HoRegion, "读回的句柄不能是 null");
                    Assert.IsFalse(back.HoRegion.NotNull());
                    Geom.AreClose(0, 0, 10, 10, back.Bounds);
                }
            }
        }

        [TestMethod]
        public void NullLiteral_ReadsBackAsEmptyShell()
        {
            using (var back = JsonConvert.DeserializeObject<CvRegion>("{\"Width\":5,\"HoRegion\":null}"))
            {
                Assert.IsNotNull(back.HoRegion);
                Assert.IsFalse(back.HoRegion.NotNull());
                Assert.AreEqual(5.0, back.Width);
            }
        }
    }
}
