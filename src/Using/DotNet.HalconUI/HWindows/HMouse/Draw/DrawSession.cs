using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 一次交互绘图会话：把 <see cref="DrawRenderer"/>(画布) 与 <see cref="DrawShape"/>(图元状态机)
    /// 组合起来，负责鼠标事件分发、阻塞等待与资源释放。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>多窗口安全</b>：会话按窗口对象注册在 <see cref="Sessions"/> 里，
    /// <see cref="ActiveFor"/> 只返回同一个 <see cref="HWindow"/> 上最新的会话。
    /// 原实现用单个静态 <c>_active</c> 字段，第二个窗口发起绘制会顶掉第一个窗口的会话。
    /// </para>
    /// <para>
    /// <b>事件驱动的等待</b>：<see cref="WaitForCompletionAsync"/> 返回一个由
    /// <see cref="TaskCompletionSource{TResult}"/> 支撑的 <see cref="Task{TResult}"/>，
    /// 结果由鼠标事件 / 取消 / 超时三条路径填入，不再有 <c>Application.DoEvents()</c> 轮询循环。
    /// </para>
    /// <para>
    /// <b>续体会内联，注销必须抢在前面</b>：调用方在 UI 线程发起绘制，<c>await</c> 捕获的是
    /// <c>WindowsFormsSynchronizationContext</c>。当 <see cref="Finish"/> 在鼠标事件回调里被调用时，
    /// 捕获的 context 与 <c>SynchronizationContext.Current</c> 是同一个实例，BCL 会走
    /// <c>SynchronizationContextAwaitTaskContinuation</c> 的内联快路径，
    /// <b>直接在当前调用栈上跑续体</b>，而不是 <c>Post</c> 回消息队列。
    /// 目标框架 .NET Framework 4.5 也没有 <c>TaskCreationOptions.RunContinuationsAsynchronously</c>
    /// (4.6+) 可以关掉它。
    /// </para>
    /// <para>
    /// 因此 <see cref="Finish"/> 的顺序是<b>先 <see cref="Unregister"/> 再填结果</b>：
    /// 续体里的用户代码(建模板、<c>MessageBox</c>…)会在鼠标事件的栈上执行，一旦它泵消息，
    /// 而本会话还留在 <see cref="Sessions"/> 里，鼠标事件就会被派发给这个已定稿、
    /// 甚至已 <see cref="Dispose"/>(renderer 已还原窗口状态) 的会话。先摘掉自己就没有这个窗口期。
    /// </para>
    /// </remarks>
    internal sealed class DrawSession : IDisposable
    {
        /// <summary>等待的默认上限。宿主没有转发鼠标事件时靠它兜底，不会让会话永远悬着。</summary>
        internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

        private static readonly object Gate = new object();
        private static readonly List<DrawSession> Sessions = new List<DrawSession>();

        private readonly DrawRenderer _renderer;
        private readonly DrawShape _shape;

        /// <summary>
        /// 会话结果槽：用户右键确认填 true，取消 / 超时 / 被顶掉填 false。
        /// 只由 <see cref="Finish"/> 写入，保证结果唯一。
        /// </summary>
        private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>();

        // 超时用的 CTS 与两个注册句柄, 全部在 Finish 里释放, 避免 Timer / 回调链泄漏。
        // 写入在 UI 线程, 释放可能在定时器线程 —— CancellationTokenRegistration 是多字段结构体,
        // 赋值不是原子的, 撕裂读之后 Dispose 会踩到半初始化的句柄。所以一律走 _regGate。
        private readonly object _regGate = new object();
        private CancellationTokenSource _timeoutCts;
        private CancellationTokenRegistration _timeoutReg;
        private CancellationTokenRegistration _callerReg;

        /// <summary>
        /// 会话是否已定稿。不能拿 <c>TrySetResult</c> 的返回值当这个标志用：
        /// 它会内联执行 <c>await</c> 的续体，清理代码反而排到了续体之后(见类注释)。
        /// </summary>
        private int _finished;

        private int _disposed;

        private DrawSession(DrawRenderer renderer, DrawShape shape)
        {
            _renderer = renderer;
            _shape = shape;
            shape.Attach(renderer);
        }

        internal HWindow Window => _renderer.Window;

        /// <summary>用户是否右键确认（相对于超时 / 取消 / 被新会话顶掉）。</summary>
        internal bool Completed => _shape.Completed;

        #region 会话注册表

        /// <summary>开启一次会话：抓背景 → 切双缓冲 → 注册为该窗口的当前会话。</summary>
        internal static DrawSession Begin(HWindow window, DrawShape shape)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (shape == null) throw new ArgumentNullException(nameof(shape));

            var session = new DrawSession(new DrawRenderer(window), shape);
            lock (Gate) { Sessions.Add(session); }
            return session;
        }

        /// <summary>取该窗口上最新的活动会话；没有则返回 null。</summary>
        internal static DrawSession ActiveFor(HWindow window)
        {
            if (window == null) return null;
            lock (Gate)
            {
                for (int i = Sessions.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(Sessions[i].Window, window)) return Sessions[i];
                }
            }
            return null;
        }

        /// <summary>取消指定窗口上的全部会话；<paramref name="window"/> 为 null 时取消所有窗口。</summary>
        internal static void CancelAll(HWindow window)
        {
            List<DrawSession> victims;
            lock (Gate)
            {
                victims = new List<DrawSession>();
                foreach (var s in Sessions)
                {
                    if (window == null || ReferenceEquals(s.Window, window)) victims.Add(s);
                }
            }

            // 旧会话的 await 尚未返回时就可能发起新的 Draw*(用户连点两个按钮)。
            // 这里立即给旧会话填入 false 并释放，避免 flush 状态被新旧会话交叉还原。
            foreach (var s in victims)
            {
                s.Finish(false);
                s.Dispose();
            }
        }

        #endregion

        #region 鼠标事件

        internal void OnMouseDown(HMouseEventArgs e)
        {
            _shape.OnDown(e);

            // 宿主 (HDisplayUI.OnMouseDown) 会先 ReDispImage 把纯图像写入 backbuffer (无 ROI).
            // 若此处只 Flush, 屏幕会闪过一帧"无 ROI"画面, 直到下一次 MouseMove 才补上.
            // 因此在 swap 前按当前 phase 重绘一次 ROI, 与下一次 Move 帧无缝衔接.
            //
            // 关键: OnDown 在 Editing 阶段会把 Dragging 设为 true. 若直接走 Edit 的 dragging 分支,
            // 会立即把控制点 "吸附" 到鼠标位置 —— 由于鼠标在命中阈值 (10px) 内但并不重合, 控制点会瞬间偏移.
            // 对 Rect2/Ellipse 而言, Phi = Atan2(CY - e.Y, e.X - CX) 在 10px 偏差下可能跳变 10°+,
            // 矩形与箭头瞬间旋转, 这就是 "Rect2 ROI 闪烁" 的根因.
            // 临时清零 Dragging, 让本帧只按现有几何重绘; 真正的拖拽留给下一次 MouseMove.
            bool savedDragging = _shape.Dragging;
            _shape.Dragging = false;
            try { RedrawAndFlush(e); }
            finally { _shape.Dragging = savedDragging; }

            TryComplete();
        }

        internal void OnMouseUp(HMouseEventArgs e)
        {
            _shape.OnUp(e);
            // 同 OnMouseDown: HDisplayUI.OnMouseUp 同样会先 ReDispImage, 必须重画 ROI 再 swap, 避免闪烁
            RedrawAndFlush(e);

            TryComplete();
        }

        internal void OnMouseMove(HMouseEventArgs e)
        {
            // 进入会话时已设置 flush=false, 所有绘图都在 backbuffer 中累积.
            // 这里不再切换 flush 状态, 避免 SetWindowParam("flush",...) 反复触发隐式刷新导致闪烁.
            RedrawAndFlush(e);
        }

        internal void OnMouseWheel(HMouseEventArgs e) { }

        /// <summary>
        /// 图元报告"已右键确认"时结束会话。先渲染完最后一帧再调用，
        /// 保证用户看到的定格画面与返回的几何一致。
        /// </summary>
        private void TryComplete()
        {
            if (_shape.Completed) Finish(true);
        }

        // 按当前 phase 把 ROI 完整画到 backbuffer, 再一次性 swap 到屏幕 (双缓冲核心).
        // OnMouseDown/Up/Move 共用同一条渲染路径, 保证任何鼠标事件后屏幕状态一致,
        // 不会出现"先无 ROI、再有 ROI"的闪烁帧.
        private void RedrawAndFlush(HMouseEventArgs e)
        {
            try
            {
                // 单次刷新内只计算一次, 避免每个 Cross/IsNear 都触发 GetPart+GetWindowExtents
                _renderer.RefreshPixelSize();
                _shape.Render(e);
            }
            finally
            {
                _renderer.Flush();
            }
        }

        /// <summary>
        /// Draw*Mod 在阻塞等待之前调用一次，用传入的初始几何把 ROI 画出来。
        /// 否则用户必须先移动鼠标才能看到初始 ROI，体验割裂。
        /// </summary>
        internal void RenderInitial()
        {
            try
            {
                _renderer.RefreshPixelSize();
                _renderer.RestoreBackground();
                _shape.Hover = DrawHandle.None;
                _shape.Dragging = false;
                _shape.RenderStatic();
            }
            finally
            {
                _renderer.Flush();
            }
        }

        #endregion

        #region 完成 / 等待

        /// <summary>
        /// 等待用户右键确认、会话被取消、<paramref name="token"/> 取消或超时。
        /// 不阻塞线程：返回的 <see cref="Task{TResult}"/> 由鼠标事件驱动完成。
        /// </summary>
        /// <param name="timeout">等待上限；小于等于 <see cref="TimeSpan.Zero"/> 表示不限时。</param>
        /// <param name="token">调用方的取消令牌。</param>
        /// <returns>用户确认返回 true；取消 / 超时返回 false。</returns>
        /// <remarks>
        /// 必须在 UI 线程调用：<c>await</c> 要在这里捕获 <c>WindowsFormsSynchronizationContext</c>，
        /// 续体才会回到 UI 线程操作 <see cref="HWindow"/>。注意续体<b>会</b>内联在
        /// <see cref="Finish"/> 的调用栈上执行(见类注释)，调用方 <c>await</c> 之后的代码
        /// 等同于跑在鼠标事件处理函数里。
        /// </remarks>
        internal Task<bool> WaitForCompletionAsync(TimeSpan timeout, CancellationToken token)
        {
            // 极端情况: 等待还没挂上就被 CancelAll 顶掉 / 图元在 RenderInitial 阶段已确认。
            // 判 _finished 而非 Task.IsCompleted —— Finish 是先置标志再填结果的, 前者更早为真。
            if (Volatile.Read(ref _finished) == 1) return _completion.Task;

            // 下面两处 Register 都可能"就地回调": timeout 极小时 CTS 构造完就已取消,
            // token 传进来时也可能已经取消。就地回调意味着 Finish 会在 Register 返回之前跑完,
            // 它清理的是当时字段里的值(还是 default)。定时器线程也可能恰好在 Register 返回、
            // 赋值语句尚未执行的那个窗口期触发 —— 结果一样: 真句柄再没人释放。
            // 所以统一按"先落库, 再复查一次 IsCompleted, 已结束就地补释放"处理
            // (CancellationTokenRegistration.Dispose 幂等, 重复释放无副作用)。
            if (timeout > TimeSpan.Zero)
            {
                var cts = new CancellationTokenSource(timeout);
                lock (_regGate) { _timeoutCts = cts; }   // 先落库: 就地回调时 Finish 才能把它 Dispose 掉
                var reg = cts.Token.Register(() =>
                {
                    Log.Warn(DrawSafe.Category,
                        $"交互绘制等待超过 {timeout.TotalSeconds:F0}s 未确认, 自动结束. " +
                        "常见原因: 宿主没有把 HMouseDown/Up/Move 转发给当前绘图会话.");
                    Finish(false);
                });
                lock (_regGate) { _timeoutReg = reg; }

                if (Volatile.Read(ref _finished) == 1)
                {
                    // 已经结束: 句柄与 CTS 就地补释放, 也不必再挂调用方的 token
                    reg.Dispose();
                    cts.Dispose();
                    lock (_regGate) { _timeoutReg = default; _timeoutCts = null; }
                    return _completion.Task;
                }
            }

            // token 已取消时 Register 会就地回调, 无需额外判一次 IsCancellationRequested
            if (token.CanBeCanceled)
            {
                var reg = token.Register(() =>
                {
                    Log.Info(DrawSafe.Category, "交互绘制被调用方取消.");
                    Finish(false);
                });
                lock (_regGate) { _callerReg = reg; }

                if (Volatile.Read(ref _finished) == 1)
                {
                    reg.Dispose();
                    lock (_regGate) { _callerReg = default; }
                }
            }

            return _completion.Task;
        }

        /// <summary>
        /// 填入会话结果并停止接收鼠标事件。幂等——先到者胜，后续调用被忽略。
        /// </summary>
        /// <remarks>
        /// <b>顺序不能动</b>：<c>TrySetResult</c> 会<b>内联</b>执行 <c>await</c> 的续体
        /// (见类注释)，也就是说它<b>不会返回</b>，直到调用方那边 <c>session.Dispose()</c>、
        /// 取几何、建模板、弹窗……整条后续代码跑完。所以注销与句柄释放必须全部排在它前面：
        /// 放在后面等于"续体跑完才注销"，续体里任何一次消息泵都会把鼠标事件派给这个
        /// 已定稿甚至已 <see cref="Dispose"/> 的会话，画到已还原的窗口上。
        /// 窗口状态还原仍留给 <see cref="Dispose"/>，因为定格画面要保留到调用方取走结果为止。
        /// </remarks>
        private void Finish(bool completed)
        {
            // 幂等靠这个标志, 不能靠 TrySetResult 的返回值 —— 它一旦返回, 续体已经跑完了
            if (Interlocked.Exchange(ref _finished, 1) == 1) return;

            Unregister();

            // 计时器与回调链在这里断开, 避免会话已结束还挂着一个 5 分钟的 Timer。
            // 取出后置空再释放: Dispose 期间不持锁, 免得与就地回调的 Register 互等
            CancellationTokenRegistration timeoutReg, callerReg;
            CancellationTokenSource cts;
            lock (_regGate)
            {
                timeoutReg = _timeoutReg; _timeoutReg = default;
                callerReg = _callerReg; _callerReg = default;
                cts = _timeoutCts; _timeoutCts = null;
            }
            timeoutReg.Dispose();
            callerReg.Dispose();
            cts?.Dispose();

            // 最后才填结果: 这一行会就地跑完调用方 await 之后的全部代码
            _completion.TrySetResult(completed);
        }

        // 按引用移除: 同一窗口上可能并存新旧两个会话(旧会话的 await 尚未返回), 只摘掉自己这一条
        private void Unregister()
        {
            lock (Gate) { Sessions.Remove(this); }
        }

        #endregion

        /// <summary>结束会话：注销、还原窗口状态、释放背景快照。幂等。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

            // 兜底: 走到这里还没有结果, 说明调用方在 await 之前就异常退出了
            // (Finish 内部已经 Unregister, 不必再调一次)
            Finish(false);

            _renderer.Dispose();
        }
    }
}
