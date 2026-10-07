using System;
using System.Diagnostics;
using System.IO;
using DotNet.Drawing;
using DotNet.HalconAlgo;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionRuntime.Tests
{
    /// <summary>
    /// 验收：长时间循环运行时内存平稳 —— 流程里每个工具每轮都换新的 HObject，旧的必须及时释放。
    /// </summary>
    [TestClass]
    public class SoakTests : HalconTestBase
    {
        [TestMethod]
        [TestCategory("Soak")]
        public void Flow_RepeatedRuns_MemoryStaysFlat()
        {
            string dir = Path.Combine(Path.GetTempPath(), "DotNet.VisionRuntime.Tests", Guid.NewGuid().ToString("N"));
            var shape = new ShapeModelStrategy { DataDir = Path.Combine(dir, "shape") };
            var roi = new CreateROIStrategy();
            var fit = new FitLineStrategy();
            using (var dark = ConstImage(400, 300, 0))
            using (var bar = Rectangle1(80, 100, 220, 200))
            using (var image = Paint(dark, bar, 255))
            {
                OverlayList shown = null;
                try
                {
                    // 匹配: 模板框住亮块左上角; 创建ROI 跟随匹配坐标系; 拟合直线测亮块左边缘 (列 100)
                    var host = new FakeRoiHost
                    {
                        OnDraw = r =>
                        {
                            r.Bounds = new Rect2d(70.0, 60.0, 80.0, 80.0);
                            r.RebuildRegion();
                        },
                    };
                    host.FakeDisplay.SetImage(image);
                    shape.SetTemplateAsync(host, RectEnum.Rectangle, true).GetAwaiter().GetResult();
                    shape.inPara.HoRect.Dispose();
                    shape.inPara.HoRect = NewRegion(RectEnum.Rectangle, 0, 0, 399, 299);

                    roi.inPara.HoRect.Dispose();
                    roi.inPara.HoRect = NewRegion(RectEnum.Rectangle, 60, 70, 100, 160);
                    roi.inPara.CoordIn = shape.Ref("坐标系");

                    fit.inPara.HoRect.Dispose();
                    fit.inPara.HoRect = NewAffRect(new Point2d(100, 150), 40, 100);
                    fit.inPara.RegionIn = roi.Ref("区域");

                    var tools = new IParaStrategy[] { shape, roi, fit };
                    var runner = new FlowRunner(tools);
                    // 与显示控件相同的用法: 每轮取走合成的叠加层换上去, 旧的随之释放
                    Func<int, long> runMany = n =>
                    {
                        for (int i = 0; i < n; i++)
                        {
                            using (var result = runner.Run(image))
                            {
                                Assert.IsTrue(result.AllOk, string.Join("; ", result.Steps));
                                var next = result.TakeOverlay();
                                Assert.IsTrue(next.Count > 0, "前提：每轮都生成了叠加层");
                                shown?.Dispose();
                                shown = next;
                            }
                        }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();
                        return Process.GetCurrentProcess().PrivateMemorySize64;
                    };

                    runMany(50);                     // 预热: 首轮分配、JIT、HALCON 内部缓存
                    long before = runMany(50);
                    long after = runMany(2000);

                    // 灵敏度: 每轮漏一张图 (几十 KB 起) 必然超限; 每轮只漏一个小区域 (几 KB) 时增长在阈值附近, 未必拦得住
                    double growthMb = (after - before) / 1024.0 / 1024.0;
                    Assert.IsTrue(growthMb < 8, $"2000 轮后私有内存增长 {growthMb:F1} MB, 疑似句柄泄漏");
                    Assert.AreEqual(99.5, fit.Line.Start.X, 0.6, "前提：流程每轮都真正算出了结果");
                }
                finally
                {
                    shown?.Dispose();
                    shape.Dispose();
                    roi.Dispose();
                    fit.Dispose();
                    if (Directory.Exists(dir)) Directory.Delete(dir, true);
                }
            }
        }
    }
}
