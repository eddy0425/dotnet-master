using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconAlgo.Tests;
using DotNet.HalconCore;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconRuntime.Tests
{
    /// <summary>
    /// <see cref="FlowSession"/>：专用线程、请求严格串行、取消在两个工具之间生效、连续运行、参数写回排在两帧之间。
    /// </summary>
    [TestClass]
    public class FlowSessionTests : HalconTestBase
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private sealed class ProbePara : DisplayOptions
        {
            public int Value { get; set; }
        }

        /// <summary> 记录自己在哪个线程、是否与别的执行重叠；可以睡一会儿、失败、写一条叠加层 </summary>
        private sealed class Probe : ParaStrategyBase<ProbePara>
        {
            private static int _active;
            public static int MaxConcurrent;

            public int SleepMs;
            public bool Fail;
            public int Runs;
            public int ThreadId;
            public readonly ConcurrentQueue<int> SeenValues = new ConcurrentQueue<int>();
            public Action OnExecute;

            public Probe(string name) { Name = name; inPara.DispText = false; }

            public static void ResetCounters() { _active = 0; MaxConcurrent = 0; }

            /// <summary> 此刻正在执行的工具数 </summary>
            public static int Active => Volatile.Read(ref _active);

            protected override void DeclareParams(ParamBuilder p) { }
            protected override void DeclareOutputs(OutputBuilder o) { }
            protected override void ResetOutputs() { }

            protected override RunResult Execute(RunContext context)
            {
                int now = Interlocked.Increment(ref _active);
                int max;
                while (now > (max = Volatile.Read(ref MaxConcurrent)) && Interlocked.CompareExchange(ref MaxConcurrent, now, max) != max) { }
                try
                {
                    ThreadId = Thread.CurrentThread.ManagedThreadId;
                    SeenValues.Enqueue(inPara.Value);
                    OnExecute?.Invoke();
                    if (SleepMs > 0) Thread.Sleep(SleepMs);
                    Interlocked.Increment(ref Runs);
                    return Fail ? RunResult.Fail("失败") : RunResult.Ok();
                }
                finally { Interlocked.Decrement(ref _active); }
            }

            protected override void Render(IOverlay overlay, RunResult result) => overlay.Text(Name, new Point2d(0, 0));
        }

        [TestInitialize]
        public void SetUp() => Probe.ResetCounters();

        private static T Get<T>(Task<T> task)
        {
            Assert.IsTrue(task.Wait(Timeout), "任务超时");
            return task.Result;
        }

        private static void WaitUntil(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) Assert.Fail("等待超时: " + message);
                Thread.Sleep(5);
            }
        }

        [TestMethod]
        public void RunAsync_RunsOnSessionThread_ReturnsOwnedFrame()
        {
            var probe = new Probe("A");
            using (var session = new FlowSession(new IParaStrategy[] { probe }))
            using (var image = Rectangle1(0, 0, 9, 9))
            {
                var task = session.RunAsync(image);
                image.Dispose();    // 输入已复制: 调用方随即释放不影响排队中的请求

                using (var frame = Get(task))
                {
                    Assert.AreNotEqual(Thread.CurrentThread.ManagedThreadId, probe.ThreadId);
                    Assert.IsTrue(frame.Result.AllOk);
                    Assert.IsTrue(frame.Image.IsInitialized(), "底图是帧自己的副本");
                    Assert.AreEqual("A", frame.Overlay.Items.Single().Value);
                    var overlay = frame.TakeOverlay();
                    Assert.IsNull(frame.Overlay);
                    overlay.Dispose();
                }
                Assert.IsFalse(session.IsBusy, "await 回来时请求已经结束");
            }
        }

        [TestMethod]
        public void Requests_AreStrictlySerial()
        {
            var a = new Probe("A") { SleepMs = 30 };
            var b = new Probe("B") { SleepMs = 30 };
            var order = new ConcurrentQueue<string>();
            using (var session = new FlowSession(new IParaStrategy[] { a, b }))
            {
                var tasks = new List<Task>();
                for (int i = 0; i < 3; i++)
                {
                    int n = i;
                    tasks.Add(session.RunAsync(null).ContinueWith(t => { order.Enqueue("run" + n); t.Result.Dispose(); }));
                    tasks.Add(session.InvokeAsync(() => order.Enqueue("invoke" + n)));
                }
                Assert.IsTrue(Task.WaitAll(tasks.ToArray(), Timeout));

                Assert.AreEqual(1, Probe.MaxConcurrent, "同一会话里从不并发执行");
                Assert.AreEqual(3, a.Runs);
                var invokes = order.Where(o => o.StartsWith("invoke")).ToArray();
                CollectionAssert.AreEqual(new[] { "invoke0", "invoke1", "invoke2" }, invokes, "按提交顺序执行");
            }
        }

        [TestMethod]
        public void InvokeAsync_RunsOnSessionThread_PropagatesExceptions()
        {
            var probe = new Probe("A");
            using (var session = new FlowSession(new IParaStrategy[] { probe }))
            {
                Get(session.RunAsync(null)).Dispose();
                Assert.IsTrue(Get(session.InvokeAsync(() => session.IsSessionThread)));
                Assert.AreEqual(probe.ThreadId, Get(session.InvokeAsync(() => Thread.CurrentThread.ManagedThreadId)), "专用线程, 每次都是同一个");

                var failed = session.InvokeAsync(() => { throw new InvalidOperationException("坏了"); });
                var ex = Assert.ThrowsException<AggregateException>(() => failed.Wait(Timeout));
                Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
            }
        }

        [TestMethod]
        public void Cancel_TakesEffectBetweenTools()
        {
            var cts = new CancellationTokenSource();
            var a = new Probe("A") { SleepMs = 100 };
            a.OnExecute = () => cts.Cancel();
            var b = new Probe("B");
            using (var session = new FlowSession(new IParaStrategy[] { a, b }))
            {
                var task = session.RunAsync(null, cts.Token);

                var ex = Assert.ThrowsException<AggregateException>(() => task.Wait(Timeout));
                Assert.IsInstanceOfType(ex.InnerException, typeof(TaskCanceledException));
                Assert.AreEqual(1, a.Runs, "正在执行的工具跑完");
                Assert.AreEqual(0, b.Runs, "下一个工具不再执行");
                WaitUntil(() => !session.IsBusy, "取消后空闲");
            }
        }

        [TestMethod]
        public void RunStepAsync_RunsOnlyThatTool()
        {
            var a = new Probe("A");
            var b = new Probe("B");
            using (var session = new FlowSession(new IParaStrategy[] { a, b }))
            using (var frame = Get(session.RunStepAsync(1, null)))
            {
                Assert.AreEqual(0, a.Runs);
                Assert.AreEqual(1, b.Runs);
                Assert.AreSame(b, frame.Result.Steps.Single().Tool);
                Assert.ThrowsException<ArgumentOutOfRangeException>(() => session.RunStepAsync(2, null));
            }
        }

        [TestMethod]
        public void Loop_DeliversFrames_StopsOnRequest()
        {
            var probe = new Probe("A");
            int frames = 0;
            LoopStoppedEventArgs stopped = null;
            using (var session = new FlowSession(new IParaStrategy[] { probe }) { LoopInterval = TimeSpan.FromMilliseconds(5) })
            {
                session.FrameCompleted += (s, e) =>
                {
                    Assert.IsTrue(session.IsSessionThread);
                    Interlocked.Increment(ref frames);
                    using (var frame = e.TakeFrame()) Assert.AreEqual(1, frame.Overlay.Count);
                };
                session.LoopStopped += (s, e) => stopped = e;

                session.StartLoop(null);
                Assert.IsTrue(session.IsLooping);
                WaitUntil(() => Volatile.Read(ref frames) >= 3, "连续交出帧");

                session.StopLoop();
                Assert.IsFalse(session.IsLooping, "停止立刻生效");
                WaitUntil(() => stopped != null, "停下通知");
                Assert.IsTrue(stopped.ByRequest);
                WaitUntil(() => !session.IsBusy, "停下之后空闲");
                int runs = probe.Runs;
                Thread.Sleep(50);
                Assert.AreEqual(runs, probe.Runs, "停下之后不再执行");
            }
        }

        [TestMethod]
        public void Loop_StopsOnFailure_AfterDeliveringThatFrame()
        {
            var probe = new Probe("A") { Fail = true };
            FlowFrame last = null;
            LoopStoppedEventArgs stopped = null;
            using (var session = new FlowSession(new IParaStrategy[] { probe }))
            {
                session.FrameCompleted += (s, e) => last = e.TakeFrame();
                session.LoopStopped += (s, e) => stopped = e;

                session.StartLoop(null);
                WaitUntil(() => stopped != null, "失败后停下");

                Assert.AreEqual(1, probe.Runs);
                Assert.IsNotNull(last, "失败的那一帧先交出来");
                Assert.AreSame(probe, stopped.Failure.Tool);
                Assert.IsFalse(session.IsLooping);
                last.Dispose();
            }
        }

        [TestMethod]
        public void Loop_UntakenFrames_AreReleasedBySession()
        {
            var probe = new Probe("A");
            var seen = new ConcurrentQueue<OverlayList>();
            using (var session = new FlowSession(new IParaStrategy[] { probe }) { LoopInterval = TimeSpan.Zero })
            {
                session.FrameCompleted += (s, e) => seen.Enqueue(e.Frame.Overlay);   // 看一眼, 不取走
                session.StartLoop(null);
                WaitUntil(() => seen.Count >= 3, "交出帧");
                session.StopLoop();
                WaitUntil(() => !session.IsBusy, "停下");
            }
            Assert.IsTrue(seen.All(o => o.IsDisposed), "没人取走的帧由会话释放");
        }

        /// <summary> 连续运行中投递的写回在两帧之间执行：不与执行交错，下一帧就能看到 </summary>
        [TestMethod]
        public void Loop_InvokeRunsBetweenFrames_NextFrameSeesIt()
        {
            var probe = new Probe("A") { SleepMs = 20 };
            using (var session = new FlowSession(new IParaStrategy[] { probe }) { LoopInterval = TimeSpan.Zero })
            {
                session.StartLoop(null);
                WaitUntil(() => probe.Runs >= 2, "已经在跑");

                bool overlapped = Get(session.InvokeAsync(() =>
                {
                    bool running = Probe.Active > 0;
                    probe.inPara.Value = 42;
                    return running;
                }));
                int runsAtWrite = probe.Runs;
                WaitUntil(() => probe.Runs >= runsAtWrite + 2, "之后又跑了几帧");
                session.StopLoop();
                WaitUntil(() => !session.IsBusy, "停下");

                Assert.IsFalse(overlapped, "写回执行时没有工具在跑");
                Assert.AreEqual(1, Probe.MaxConcurrent, "写回与执行从不重叠");
                var values = probe.SeenValues.ToArray();
                int first42 = Array.IndexOf(values, 42);
                Assert.IsTrue(first42 > 0, "写回之后的帧看到新值");
                Assert.IsTrue(values.Skip(first42).All(v => v == 42), "一旦生效, 之后每帧都是新值");
            }
        }

        [TestMethod]
        public void Loop_UsesOwnCopyOfInputImage()
        {
            var probe = new Probe("A");
            int withImage = 0;
            using (var session = new FlowSession(new IParaStrategy[] { probe }) { LoopInterval = TimeSpan.Zero })
            using (var image = Rectangle1(0, 0, 9, 9))
            {
                session.FrameCompleted += (s, e) =>
                {
                    using (var f = e.TakeFrame())
                        if (f.Image.IsInitialized() && AreaCenter(f.Image, out _) == 100) Interlocked.Increment(ref withImage);
                };
                session.StartLoop(image);
                image.Dispose();
                WaitUntil(() => Volatile.Read(ref withImage) >= 3, "调用方释放输入之后, 每帧的底图仍是那张图");
                session.StopLoop();
                WaitUntil(() => !session.IsBusy, "停下");
            }
        }

        [TestMethod]
        public void Dispose_StopsLoop_CancelsQueuedRequests()
        {
            var slow = new Probe("A") { SleepMs = 100 };
            var session = new FlowSession(new IParaStrategy[] { slow });
            var first = session.RunAsync(null);
            var queued = session.RunAsync(null);
            WaitUntil(() => slow.ThreadId != 0, "第一个请求已开始");

            session.Dispose();

            Assert.IsTrue(first.Wait(Timeout) || first.IsCanceled || first.IsFaulted);
            Assert.IsTrue(SpinUntilDone(queued), "排队中的请求被取消");
            Assert.IsTrue(queued.IsCanceled);
            Assert.ThrowsException<ObjectDisposedException>(() => session.RunAsync(null));
            session.Dispose();
        }

        private static bool SpinUntilDone(Task task)
        {
            try { task.Wait(Timeout); } catch (AggregateException) { }
            return task.IsCompleted;
        }
    }
}
