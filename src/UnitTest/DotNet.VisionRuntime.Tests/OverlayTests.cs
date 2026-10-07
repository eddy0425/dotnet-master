using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DotNet.Drawing;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionRuntime.Tests
{
    /// <summary>
    /// <see cref="OverlayList"/>：复制句柄、按顺序重放、释放后句柄归零。
    /// </summary>
    [TestClass]
    public class OverlayListTests : HalconTestBase
    {
        [TestMethod]
        public void Add_HObject_KeepsOwnCopy_OriginalCanBeReleased()
        {
            using (var overlay = new OverlayList())
            {
                var region = Rectangle1(0, 0, 9, 9);
                overlay.Add(region, DrawStyle.Of(HColor.Blue));
                region.Dispose();

                var copy = (HObject)overlay.Items.Single().Value;
                Assert.AreNotSame(region, copy);
                Assert.IsTrue(copy.IsInitialized(), "算法释放自己的对象之后叠加层仍可重放");
                Assert.AreEqual(100, AreaCenter(copy, out _));
            }
        }

        [TestMethod]
        public void Dispose_ReleasesEveryCopy_Idempotent()
        {
            var overlay = new OverlayList();
            using (var a = Rectangle1(0, 0, 9, 9))
            using (var b = Rectangle1(0, 0, 4, 4))
            {
                overlay.Add(a);
                overlay.Add(b);
            }
            var copies = overlay.Items.Select(i => (HObject)i.Value).ToList();

            overlay.Dispose();
            overlay.Dispose();

            Assert.IsTrue(overlay.IsDisposed);
            Assert.AreEqual(0, overlay.Count);
            Assert.IsTrue(copies.All(c => !c.IsInitialized()), "释放后叠加层持有的句柄全部归零");
            Assert.ThrowsException<ObjectDisposedException>(() => overlay.Add(new Point2d(1, 1)));
        }

        [TestMethod]
        public void Add_EmptyOrNullObject_IsIgnored()
        {
            using (var overlay = new OverlayList())
            {
                HOperatorSet.GenEmptyObj(out HObject empty);
                using (empty) overlay.Add(empty);
                overlay.Add((HObject)null);
                overlay.Add((CvRegion)null);
                overlay.Text(null, new Point2d(0, 0));

                Assert.AreEqual(0, overlay.Count);
            }
        }

        [TestMethod]
        public void Add_Points_CopiesTheList()
        {
            using (var overlay = new OverlayList())
            {
                var points = new List<Point2d> { new Point2d(1, 2) };
                overlay.Add(points);
                points.Add(new Point2d(3, 4));

                CollectionAssert.AreEqual(new[] { new Point2d(1, 2) }, (Point2d[])overlay.Items.Single().Value, "算法之后再改自己的列表不影响已写入的叠加层");
            }
        }

        [TestMethod]
        public void Add_CvRegion_CopiesCurrentRegion()
        {
            using (var overlay = new OverlayList())
            using (var roi = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10))
            {
                overlay.Add(roi);
                Assert.AreEqual(OverlayKind.Object, overlay.Items.Single().Kind);
                Assert.AreNotSame(roi.HoRegion, overlay.Items.Single().Value);
            }
        }

        [TestMethod]
        public void DrawTo_ReplaysInOrderWithStyles()
        {
            using (var overlay = new OverlayList())
            using (var region = Rectangle1(0, 0, 9, 9))
            {
                overlay.Add(region, DrawStyle.Of(HColor.Blue));
                overlay.Add(new Point2d(5, 6), DrawStyle.Of(HColor.Red, 7));
                overlay.Add(new CvArrow(0, 0, 10, 10));
                overlay.Add(new CvCoord(1, 2));
                overlay.AddRect2(new Point2d(3, 4), 0.5, 10, 2);
                overlay.Text("结果", new Point2d(20, 30), DrawStyle.Of(HColor.Green, 15));

                var display = new FakeDisplay();
                overlay.DrawTo(display);
                overlay.DrawTo(display);

                Assert.AreEqual(2, display.Objects.Count, "每次重放都画同一份");
                Assert.AreEqual(HColor.Blue.Name, display.Objects[0].ColorName);
                Assert.AreEqual(new Point2d(5, 6), display.Points[0].Item);
                Assert.AreEqual(7.0, display.Points[0].Style.Size);
                Assert.AreEqual(2, display.Arrows.Count);
                Assert.AreEqual(new CvCoord(1, 2), display.Coords[0]);
                Assert.AreEqual(new Point2d(3, 4), display.Rect2Centers[0]);
                Assert.AreEqual("结果", display.Texts[0].Item);
                Assert.AreEqual(new Point2d(20, 30), display.Texts[0].Position);
            }
        }

        [TestMethod]
        public void Absorb_MovesItemsInOrder_WithoutCopying()
        {
            using (var target = new OverlayList())
            using (var source = new OverlayList())
            using (var region = Rectangle1(0, 0, 9, 9))
            {
                target.Text("a", new Point2d(0, 0));
                source.Add(region);
                var moved = source.Items[0].Value;

                target.Absorb(source);

                Assert.AreEqual(0, source.Count);
                Assert.AreEqual(2, target.Count);
                Assert.AreSame(moved, target.Items[1].Value, "移动而不是复制");
            }
        }

        [TestMethod]
        public void StrategyRun_WritesOverlay_StatusTextIncluded()
        {
            using (var overlay = new OverlayList())
            {
                var roi = new DotNet.HalconAlgo.CreateROIStrategy();
                try
                {
                    roi.inPara.HoRect.Dispose();
                    roi.inPara.HoRect = NewRegion(RectEnum.Rectangle, 0, 0, 10, 10);

                    Assert.IsTrue(roi.Run(RunContext.ForImage(null), overlay).IsOk);

                    Assert.IsTrue(overlay.Items.Any(i => i.Kind == OverlayKind.Object), "区域");
                    Assert.IsTrue(overlay.Items.Any(i => i.Kind == OverlayKind.Text && ((string)i.Value).StartsWith("创建ROI : ")), "状态文本也写进同一份叠加层");
                }
                finally { roi.Dispose(); }
            }
        }
    }

    /// <summary>
    /// <see cref="FlowRunner"/> 的叠加层：每步一份、整帧按顺序合成、所有权明确。
    /// </summary>
    [TestClass]
    public class FlowOverlayTests : HalconTestBase
    {
        private sealed class MarkPara : DisplayOptions { }

        /// <summary> 写一条文本、可选地产出图像 / 被取消 </summary>
        private sealed class Mark : ParaStrategyBase<MarkPara>, IImageProducer
        {
            public HObject Produce;
            public RunStatus Outcome = RunStatus.Ok;
            public Action OnExecute;
            public IOverlay Seen;

            public Mark(string name)
            {
                Name = name;
                inPara.DispText = false;
            }

            public HObject Image => Produce;

            protected override void DeclareParams(ParamBuilder p) { }
            protected override void DeclareOutputs(OutputBuilder o) { }
            protected override void ResetOutputs() { }

            protected override RunResult Execute(RunContext context)
            {
                OnExecute?.Invoke();
                return Outcome == RunStatus.Error ? RunResult.Fail("失败") : RunResult.Ok();
            }

            protected override void Render(IOverlay overlay, RunResult result)
            {
                Seen = overlay;
                overlay.Text(Name, new Point2d(0, 0));
            }
        }

        private static string[] Texts(OverlayList overlay)
            => overlay.Items.Where(i => i.Kind == OverlayKind.Text).Select(i => (string)i.Value).ToArray();

        [TestMethod]
        public void Run_EachStepOwnsItsOverlay_TakeOverlayMergesInOrder()
        {
            var a = new Mark("A");
            var b = new Mark("B");
            using (var result = new FlowRunner(new IParaStrategy[] { a, b }).Run(null))
            {
                CollectionAssert.AreEqual(new[] { "A" }, Texts(result.Steps[0].Overlay));
                CollectionAssert.AreEqual(new[] { "B" }, Texts(result.Steps[1].Overlay));

                using (var merged = result.TakeOverlay())
                {
                    CollectionAssert.AreEqual(new[] { "A", "B" }, Texts(merged));
                }
                Assert.IsTrue(result.Steps.All(s => s.Overlay == null), "取走之后各步不再持有");
            }
        }

        [TestMethod]
        public void Run_RenderOff_NoOverlays()
        {
            using (var result = new FlowRunner(new IParaStrategy[] { new Mark("A") }) { Render = false }.Run(null))
            {
                Assert.IsNull(result.Steps[0].Overlay);
                Assert.AreEqual(0, result.TakeOverlay().Count);
            }
        }

        [TestMethod]
        public void Dispose_ReleasesStepOverlays()
        {
            var result = new FlowRunner(new IParaStrategy[] { new Mark("A") }).Run(null);
            var overlay = result.Steps[0].Overlay;

            result.Dispose();

            Assert.IsTrue(overlay.IsDisposed);
        }

        [TestMethod]
        public void Image_IsCurrentImageAfterEachStep_AndAtTheEnd()
        {
            using (var initial = Rectangle1(0, 0, 1, 1))
            using (var produced = Rectangle1(0, 0, 2, 2))
            {
                var a = new Mark("A");
                var producer = new Mark("P") { Produce = produced };
                var c = new Mark("C");

                using (var result = new FlowRunner(new IParaStrategy[] { a, producer, c }).Run(initial))
                {
                    Assert.AreSame(initial, result.Steps[0].Image);
                    Assert.AreSame(produced, result.Steps[1].Image, "图像工具这一步的底图是它自己的输出");
                    Assert.AreSame(produced, result.Steps[2].Image);
                    Assert.AreSame(produced, result.Image, "整帧底图是最后一个成功的图像工具的输出");
                }

                producer.Outcome = RunStatus.Error;
                using (var result = new FlowRunner(new IParaStrategy[] { a, producer }).Run(initial))
                {
                    Assert.AreSame(initial, result.Image, "失败的图像工具不推进底图");
                }
            }
        }

        [TestMethod]
        public void RunStep_ImageIsWhatThatStepSees()
        {
            using (var initial = Rectangle1(0, 0, 1, 1))
            using (var produced = Rectangle1(0, 0, 2, 2))
            {
                var producer = new Mark("P") { Produce = produced };
                var c = new Mark("C");
                var runner = new FlowRunner(new IParaStrategy[] { producer, c });

                using (var step = runner.RunStep(1, initial))
                {
                    Assert.AreSame(produced, step.Image, "单步：底图取前面图像工具上一轮的输出");
                    using (var overlay = step.TakeOverlay())
                    {
                        CollectionAssert.AreEqual(new[] { "C" }, Texts(overlay));
                    }
                    Assert.IsNull(step.Overlay);
                }
            }
        }

        [TestMethod]
        public void Cancelled_ReleasesOverlaysOfFinishedSteps()
        {
            var cts = new CancellationTokenSource();
            var a = new Mark("A");
            var b = new Mark("B") { OnExecute = () => { cts.Cancel(); cts.Token.ThrowIfCancellationRequested(); } };
            var runner = new FlowRunner(new IParaStrategy[] { a, b, new Mark("C") });

            Assert.ThrowsException<OperationCanceledException>(() => runner.Run(null, cancellation: cts.Token));

            Assert.IsInstanceOfType(a.Seen, typeof(OverlayList));
            Assert.IsTrue(((OverlayList)a.Seen).IsDisposed, "取消时已完成步骤的叠加层没人接手, 由引擎释放");
        }
    }
}
