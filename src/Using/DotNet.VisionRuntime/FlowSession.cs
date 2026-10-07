using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.VisionRuntime
{
    /// <summary>
    /// 一帧运行结果，可以安全地交给别的线程：底图是副本，叠加层已按执行顺序合成，二者都归本对象所有。
    /// </summary>
    public sealed class FlowFrame : IDisposable
    {
        internal FlowFrame(FlowRunResult result, HObject image, OverlayList overlay)
        {
            Result = result;
            Image = image;
            Overlay = overlay;
        }

        /// <summary> 各步的结果（叠加层已经合成到 <see cref="Overlay"/>）；单步运行时只有一步 </summary>
        public FlowRunResult Result { get; }

        /// <summary> 底图副本；没有图像时为 null。随本对象释放 </summary>
        public HObject Image { get; }

        /// <summary> 合成后的叠加层；已被取走时为 null。随本对象释放 </summary>
        public OverlayList Overlay { get; private set; }

        /// <summary> 取走叠加层，所有权随之转移给调用方（例如交给显示控件） </summary>
        public OverlayList TakeOverlay()
        {
            var overlay = Overlay;
            Overlay = null;
            return overlay;
        }

        public void Dispose()
        {
            TakeOverlay()?.Dispose();
            Image?.Dispose();
            Result.Dispose();
        }
    }

    /// <summary> 连续运行的一帧（<see cref="FlowSession.FrameCompleted"/>） </summary>
    public sealed class FlowFrameEventArgs : EventArgs
    {
        private FlowFrame _frame;

        internal FlowFrameEventArgs(FlowFrame frame) => _frame = frame;

        /// <summary> 本帧；已被取走时为 null </summary>
        public FlowFrame Frame => _frame;

        /// <summary> 取走本帧，所有权随之转移；没人取走的帧由会话在事件之后释放 </summary>
        public FlowFrame TakeFrame() => Interlocked.Exchange(ref _frame, null);
    }

    /// <summary> 连续运行停下的原因（<see cref="FlowSession.LoopStopped"/>） </summary>
    public sealed class LoopStoppedEventArgs : EventArgs
    {
        internal LoopStoppedEventArgs(FlowStepResult failure, Exception error)
        {
            Failure = failure;
            Error = error;
        }

        /// <summary> 因某个工具失败而停下（<see cref="FlowSession.StopLoopOnError"/>）；否则为 null </summary>
        public FlowStepResult Failure { get; }

        /// <summary> 因意外异常而停下；否则为 null </summary>
        public Exception Error { get; }

        /// <summary> 是调用 <see cref="FlowSession.StopLoop"/> 停下的 </summary>
        public bool ByRequest => Failure == null && Error == null;
    }

    /// <summary>
    /// 执行会话：流程在一个专用工作线程上运行，请求严格串行排队 —— <b>一个流程同时只有一个执行者</b>。
    /// </summary>
    /// <remarks>
    /// 工具实例同时是配置、运行结果与执行器（见 todo A15），所以除了本会话的线程，任何线程都不应在运行期间执行或改写它们：
    /// <list type="bullet">
    /// <item>运行（<see cref="RunAsync"/> / <see cref="RunStepAsync"/> / 连续运行）都在会话线程上；</item>
    /// <item>参数写回等对工具的改动用 <see cref="InvokeAsync{T}"/> 投递，在两帧之间、在会话线程上执行，不必停机；</item>
    /// <item>会改动 HObject 的交互（ROI、模板）与流程结构的增删排序不走会话，要求 <see cref="IsBusy"/> 为 false。</item>
    /// </list>
    /// 输入图像先复制再排队：调用方（例如显示窗口）随后换图释放旧图，不影响排队中的请求。
    /// 取消只在两个工具之间生效，正在执行的 HALCON 算子不能中断。
    /// <para>
    /// 结果的 <see cref="Task"/> 在线程池上完成：会话线程不会被调用方 await 之后的代码占住；
    /// 在 UI 线程上 await 时续体照常回到 UI 线程。
    /// </para>
    /// </remarks>
    public sealed class FlowSession : IDisposable
    {
        private sealed class WorkItem
        {
            // 在会话线程上执行; 返回的收尾动作在计数减掉之后执行 (完成任务、触发事件),
            // 这样调用方 await 回来时看到的 IsBusy 已经是请求结束后的状态
            public Func<Action> Execute;
            public Action Abandon;      // 会话关闭时还没执行的请求: 取消任务、释放输入
            public bool Counted;        // 计入 IsBusy (唤醒用的空请求不计)
        }

        private sealed class LoopState : IDisposable
        {
            public IParaStrategy[] Tools;
            public HObject Image;
            public readonly CancellationTokenSource Cancel = new CancellationTokenSource();
            public int NextDue = Environment.TickCount;

            public void Dispose()
            {
                Image?.Dispose();
                Cancel.Dispose();
            }
        }

        private readonly IReadOnlyList<IParaStrategy> _tools;
        private readonly BlockingCollection<WorkItem> _queue = new BlockingCollection<WorkItem>();
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Thread _thread;
        private readonly object _gate = new object();
        private LoopState _loop;            // 受 _gate 保护
        private int _pending;               // 已排队、还没执行完的请求数
        private bool _disposed;

        /// <param name="tools">
        /// 流程（按执行顺序）。每个请求在<b>提交时</b>拍一份快照：之后对列表的增删不影响已排队的请求，
        /// 但调用方仍应只在 <see cref="IsBusy"/> 为 false 时改动它。
        /// </param>
        public FlowSession(IReadOnlyList<IParaStrategy> tools)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _thread = new Thread(Work) { IsBackground = true, Name = nameof(FlowSession) };
            _thread.Start();
        }

        public FlowFailurePolicy OnFailure { get; set; } = FlowFailurePolicy.Stop;

        /// <summary> 连续运行时两帧开始之间至少相隔多久；期间照常处理排队的请求 </summary>
        public TimeSpan LoopInterval { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <summary> 连续运行中有工具失败时停下（默认）；为 false 时继续下一帧 </summary>
        public bool StopLoopOnError { get; set; } = true;

        public bool IsLooping
        {
            get { lock (_gate) return _loop != null; }
        }

        /// <summary> 正在连续运行，或还有请求没执行完 </summary>
        public bool IsBusy => IsLooping || Volatile.Read(ref _pending) > 0;

        /// <summary> 当前线程是不是本会话的工作线程 </summary>
        public bool IsSessionThread => Thread.CurrentThread == _thread;

        /// <summary>
        /// 连续运行的每一帧，在会话线程上触发。订阅方用 <see cref="FlowFrameEventArgs.TakeFrame"/> 接手（通常转交 UI 线程显示）；
        /// 处理要快 —— 下一帧要等它返回。
        /// </summary>
        public event EventHandler<FlowFrameEventArgs> FrameCompleted;

        /// <summary> 连续运行停下（请求停止、工具失败或意外异常），在会话线程上触发 </summary>
        public event EventHandler<LoopStoppedEventArgs> LoopStopped;

        #region 请求

        /// <summary> 运行整个流程。<paramref name="image"/> 先复制再排队，调用方可以随即释放它 </summary>
        public Task<FlowFrame> RunAsync(HObject image, CancellationToken cancellation = default(CancellationToken))
            => EnqueueRun(image, null, cancellation);

        /// <summary> 只运行第 <paramref name="index"/> 个工具；它的上游输出沿用上一轮的结果 </summary>
        public Task<FlowFrame> RunStepAsync(int index, HObject image, CancellationToken cancellation = default(CancellationToken))
        {
            if (index < 0 || index >= _tools.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return EnqueueRun(image, index, cancellation);
        }

        /// <summary> 在会话线程上、两帧之间执行 <paramref name="func"/>（例如参数写回）；异常经返回的任务传出 </summary>
        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var tcs = new TaskCompletionSource<T>();
            Enqueue(new WorkItem
            {
                Counted = true,
                Execute = () =>
                {
                    T value;
                    try { value = func(); }
                    catch (Exception ex)
                    {
                        return Complete(() => tcs.TrySetException(ex));
                    }
                    return Complete(() => tcs.TrySetResult(value));
                },
                Abandon = () => Complete(() => tcs.TrySetCanceled())(),
            });
            return tcs.Task;
        }

        /// <summary> 在会话线程上、两帧之间执行 <paramref name="action"/> </summary>
        public Task InvokeAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return InvokeAsync(() => { action(); return true; });
        }

        private Task<FlowFrame> EnqueueRun(HObject image, int? step, CancellationToken cancellation)
        {
            ThrowIfDisposed();
            var tools = _tools.ToArray();
            HObject input = CopyOf(image);
            var tcs = new TaskCompletionSource<FlowFrame>();
            Enqueue(new WorkItem
            {
                Counted = true,
                Execute = () =>
                {
                    try
                    {
                        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _shutdown.Token))
                        {
                            var frame = Execute(tools, input, step, linked.Token);
                            return Complete(() => { if (!tcs.TrySetResult(frame)) frame.Dispose(); });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return Complete(() => tcs.TrySetCanceled());
                    }
                    catch (Exception ex)
                    {
                        Log.Error(nameof(FlowSession), "流程运行失败.", ex);
                        return Complete(() => tcs.TrySetException(ex));
                    }
                    finally
                    {
                        input?.Dispose();
                    }
                },
                Abandon = () =>
                {
                    input?.Dispose();
                    Complete(() => tcs.TrySetCanceled())();
                },
            });
            return tcs.Task;
        }

        /// <summary> 在会话线程上跑一遍，把结果做成可以交给别的线程的一帧 </summary>
        private FlowFrame Execute(IParaStrategy[] tools, HObject image, int? step, CancellationToken cancellation)
        {
            var runner = new FlowRunner(tools) { OnFailure = OnFailure };
            var result = step.HasValue
                ? runner.Run(image, step.Value, step.Value, cancellation)
                : runner.Run(image, cancellation: cancellation);
            OverlayList overlay = null;
            try
            {
                overlay = result.TakeOverlay();
                // 底图是借用的 (属于图像工具或输入副本), 下一帧就会被换掉: 复制一份随帧交出去
                return new FlowFrame(result, CopyOf(result.Image), overlay);
            }
            catch
            {
                overlay?.Dispose();
                result.Dispose();
                throw;
            }
        }

        #endregion

        #region 连续运行

        /// <summary>
        /// 开始连续运行：每帧都以 <paramref name="image"/>（复制一份，整个循环共用）为输入跑整个流程，
        /// 结果经 <see cref="FrameCompleted"/> 交出。已经在运行时什么都不做。
        /// </summary>
        public void StartLoop(HObject image)
        {
            ThrowIfDisposed();
            lock (_gate)
            {
                if (_loop != null) return;
                _loop = new LoopState { Tools = _tools.ToArray(), Image = CopyOf(image) };
            }
            // 唤醒可能正阻塞在空队列上的会话线程
            Enqueue(new WorkItem { Execute = () => null, Abandon = () => { } });
        }

        /// <summary>
        /// 停止连续运行：正在执行的那一帧在两个工具之间被取消，不再交出。<see cref="IsLooping"/> 立刻变成 false，
        /// <see cref="IsBusy"/> 要等这一帧真正停下；停下后触发 <see cref="LoopStopped"/>。
        /// </summary>
        public void StopLoop()
        {
            LoopState loop;
            lock (_gate)
            {
                loop = _loop;
                _loop = null;
            }
            if (loop == null) return;
            loop.Cancel.Cancel();
            // 循环状态 (输入图像) 可能正被会话线程使用: 排一个请求在它跑完这一帧之后释放
            Enqueue(new WorkItem
            {
                Counted = true,
                Execute = () => EndLoop(loop, new LoopStoppedEventArgs(null, null)),
                Abandon = loop.Dispose,
            });
        }

        private void RunLoopFrame(LoopState loop)
        {
            FlowFrame frame;
            try
            {
                frame = Execute(loop.Tools, loop.Image, null, loop.Cancel.Token);
            }
            catch (OperationCanceledException) when (loop.Cancel.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
                return;     // StopLoop / Dispose 排的请求负责收尾
            }
            catch (Exception ex)
            {
                Log.Error(nameof(FlowSession), "连续运行失败.", ex);
                if (Detach(loop)) EndLoop(loop, new LoopStoppedEventArgs(null, ex))();
                return;
            }
            finally
            {
                loop.NextDue = unchecked(Environment.TickCount + (int)Math.Max(0, LoopInterval.TotalMilliseconds));
            }

            var failure = StopLoopOnError ? frame.Result.FirstError : null;
            Deliver(frame);
            if (failure != null && Detach(loop)) EndLoop(loop, new LoopStoppedEventArgs(failure, null))();
        }

        /// <summary> 本循环还是当前循环时把它摘下来；已经被 StopLoop 摘走时返回 false（由那边收尾） </summary>
        private bool Detach(LoopState loop)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_loop, loop)) return false;
                _loop = null;
                return true;
            }
        }

        /// <summary> 释放循环状态；返回触发 <see cref="LoopStopped"/> 的动作 </summary>
        private Action EndLoop(LoopState loop, LoopStoppedEventArgs args)
        {
            loop.Dispose();
            return () =>
            {
                try { LoopStopped?.Invoke(this, args); }
                catch (Exception ex) { Log.Error(nameof(FlowSession), "LoopStopped 订阅方处理失败.", ex); }
            };
        }

        private void Deliver(FlowFrame frame)
        {
            var args = new FlowFrameEventArgs(frame);
            try { FrameCompleted?.Invoke(this, args); }
            catch (Exception ex) { Log.Error(nameof(FlowSession), "FrameCompleted 订阅方处理失败.", ex); }
            args.TakeFrame()?.Dispose();
        }

        #endregion

        #region 工作线程

        private void Enqueue(WorkItem item)
        {
            ThrowIfDisposed();
            if (item.Counted) Interlocked.Increment(ref _pending);
            try
            {
                _queue.Add(item);
            }
            catch (InvalidOperationException)
            {
                // 与 Dispose 并发: 队列已关闭
                if (item.Counted) Interlocked.Decrement(ref _pending);
                item.Abandon();
                throw new ObjectDisposedException(nameof(FlowSession));
            }
        }

        private void Work()
        {
            var token = _shutdown.Token;
            try
            {
                while (true)
                {
                    LoopState loop;
                    lock (_gate) loop = _loop;

                    WorkItem item;
                    if (loop == null)
                    {
                        item = _queue.Take(token);
                    }
                    else
                    {
                        int wait = Math.Max(0, unchecked(loop.NextDue - Environment.TickCount));
                        if (!_queue.TryTake(out item, wait, token))
                        {
                            RunLoopFrame(loop);
                            continue;
                        }
                    }
                    Run(item);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (InvalidOperationException) when (_queue.IsAddingCompleted) { }
        }

        private void Run(WorkItem item)
        {
            Action after = null;
            try { after = item.Execute(); }
            catch (Exception ex) { Log.Error(nameof(FlowSession), "会话请求执行失败.", ex); }
            finally
            {
                if (item.Counted) Interlocked.Decrement(ref _pending);
            }
            after?.Invoke();
        }

        /// <summary> 返回"在线程池上完成任务"的动作：不让调用方 await 之后的代码跑在会话线程上 </summary>
        private static Action Complete(Action complete) => () => ThreadPool.QueueUserWorkItem(_ => complete());

        private static HObject CopyOf(HObject image)
            => image.NotNull() && image.CountObj() > 0 ? image.CopyObj(1, -1) : null;

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FlowSession));
        }

        #endregion

        /// <summary>
        /// 停止连续运行、等当前请求执行完（最多 <paramref name="timeout"/>），然后取消还没执行的请求。
        /// </summary>
        public void Dispose() => Dispose(TimeSpan.FromSeconds(10));

        /// <inheritdoc cref="Dispose()"/>
        public void Dispose(TimeSpan timeout)
        {
            if (_disposed) return;

            LoopState loop;
            lock (_gate)
            {
                loop = _loop;
                _loop = null;
            }
            loop?.Cancel.Cancel();

            _disposed = true;
            _shutdown.Cancel();
            _queue.CompleteAdding();
            if (!IsSessionThread && !_thread.Join(timeout))
                Log.Warn(nameof(FlowSession), $"会话线程在 {timeout.TotalSeconds:F0} 秒内没有停下 (单个工具耗时过长?), 放弃等待.");

            while (_queue.TryTake(out var item))
            {
                if (item.Counted) Interlocked.Decrement(ref _pending);
                try { item.Abandon(); }
                catch (Exception ex) { Log.Warn(nameof(FlowSession), "取消未执行的请求失败.", ex); }
            }
            // 线程已停 (或放弃等待): 循环状态由这里收尾
            if (loop != null && !_thread.IsAlive) loop.Dispose();
        }
    }
}
