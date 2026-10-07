using System;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
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
                // 默认按系统图像尺寸裁剪区域，结果会随测试执行顺序变化；这里只验证本库逻辑，关掉裁剪。
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

        /// <summary>
        /// 打开一个不可见的 buffer 窗口作为绘制目标，调用方负责 Dispose。
        /// </summary>
        /// <remarks>
        /// 交互绘图只需要一个能 DumpWindowImage / DispObj 的 <see cref="HWindow"/>，
        /// buffer 窗口不依赖桌面与消息循环，适合无人值守的测试宿主。
        /// </remarks>
        protected static HWindow BufferWindow(int width = 320, int height = 240)
        {
            var window = new HWindow(0, 0, width, height, new HTuple(0), "buffer", "");
            window.SetPart(0, 0, height - 1, width - 1);
            return window;
        }

        protected static HObject Rectangle1(double row1, double column1, double row2, double column2)
        {
            HOperatorSet.GenRectangle1(out HObject rect, row1, column1, row2, column2);
            return rect;
        }

        protected static HObject EmptyObj()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            return empty;
        }

        protected static int CountObj(HObject obj)
        {
            HOperatorSet.CountObj(obj, out HTuple count);
            return count.I;
        }

        protected static double Area(HObject region)
        {
            HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
            return area.TupleSum().D;
        }

        /// <summary>
        /// 把一张 <paramref name="width"/> × <paramref name="height"/> 的纯色图写到临时 PNG，返回完整路径，调用方负责删除。
        /// </summary>
        /// <remarks>DisplayModel 之类的入口只接受模板图路径，不接受 HObject。</remarks>
        protected static string WriteTempImage(int width, int height)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "halconui_" + Guid.NewGuid().ToString("N") + ".png");
            HOperatorSet.GenImageConst(out HObject image, "byte", width, height);
            using (image) HOperatorSet.WriteImage(image, "png", 0, path);
            return path;
        }

        /// <summary>区域或 XLD 的中心：区域取重心，XLD 取全部轮廓外接框的中心。</summary>
        protected static void Centre(HObject obj, out double row, out double column)
        {
            HOperatorSet.GetObjClass(obj, out HTuple cls);
            if (cls.S == "region")
            {
                HOperatorSet.AreaCenter(obj, out _, out HTuple r, out HTuple c);
                row = r.D; column = c.D;
                return;
            }
            HOperatorSet.SmallestRectangle1Xld(obj, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
            row = (r1.TupleMin().D + r2.TupleMax().D) / 2;
            column = (c1.TupleMin().D + c2.TupleMax().D) / 2;
        }

        /// <summary>点 (row, column) 是否落在区域内。</summary>
        protected static bool Contains(HObject region, double row, double column)
        {
            HOperatorSet.TestRegionPoint(region, row, column, out HTuple inside);
            return inside.I == 1;
        }
    }
}
