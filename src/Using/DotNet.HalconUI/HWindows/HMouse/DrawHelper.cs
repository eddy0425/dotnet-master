using DotNet.HalconUI.Draw;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 交互式 ROI 绘制的静态门面，行为对齐 HALCON 的 <c>draw_*</c> 算子，
    /// 但支持在 <see cref="HWindowControl"/> 上正常工作，并且可以取消 / 超时。
    /// </summary>
    /// <remarks>
    /// <para>职责已按单一职责原则拆分到 <c>HWindows/HMouse/Draw/</c> 下：</para>
    /// <list type="bullet">
    /// <item><description><see cref="DrawRenderer"/> —— 窗口双缓冲、背景快照与全部绘制图元；</description></item>
    /// <item><description><see cref="DrawShape"/> 及其子类 —— 每种 ROI 的状态机、命中测试与渲染；</description></item>
    /// <item><description><see cref="DrawSession"/> —— 会话生命周期、鼠标分发、由鼠标事件驱动的等待；</description></item>
    /// <item><description><see cref="DrawGeometry"/> —— 与 HALCON 无关的纯几何计算(可单测)。</description></item>
    /// </list>
    /// <para>
    /// 本类只剩“参数换算 + 调用模板”，12 个入口共用 <see cref="RunAsync{TShape}"/>，不再逐个复制会话样板代码。
    /// </para>
    /// <para>
    /// <b>全异步</b>：不再有 <c>Application.DoEvents()</c> 轮询，等待完全由
    /// <see cref="DrawSession.WaitForCompletionAsync"/> 返回的 <see cref="Task{TResult}"/> 驱动。
    /// 所有入口都必须在 UI 线程调用。由于 <c>async</c> 方法不能带 <c>out</c> 参数，
    /// 原来的 <c>out HTuple</c> 改为返回 <c>Draw*Result</c> 只读结构体(见 <c>DrawResults.cs</c>)；
    /// 其中的 <c>Completed</c> 表示“用户是否真的右键确认了”，为 <c>false</c> 时调用方不应把几何写回配置。
    /// </para>
    /// </remarks>
    public static class DrawHelper
    {
        /// <summary>
        /// 等待的默认上限，超时后自动结束绘制并返回 <c>Completed == false</c> 的结果。
        /// 各入口可用 <c>timeout</c> 参数单独指定；<see cref="TimeSpan.Zero"/> 或负值表示不限时。
        /// 原来是可写的静态属性，一处改动影响所有窗口的所有绘制，测试之间也互相干扰。
        /// </summary>
        public static readonly TimeSpan DefaultTimeout = DrawSession.DefaultTimeout;

        /// <summary> 开新会话前反复取消旧会话的轮数上限（见 <see cref="RunAsync{TShape}"/>）。 </summary>
        private const int MaxCancelRounds = 8;

        #region 调用模板

        /// <summary>
        /// 所有 <c>Draw*Async</c> / <c>Draw*ModAsync</c> 的公共骨架：
        /// 取消旧会话 → 开新会话 → (可选)按初始几何渲染一帧 → 异步等待 → 释放。
        /// </summary>
        /// <param name="window">目标窗口。会话按该对象注册，多窗口互不干扰。</param>
        /// <param name="shape">已填好初始几何的图元状态机；返回后其几何字段即为最终结果。</param>
        /// <param name="edit">true 表示 <c>Mod</c> 语义：直接进入编辑阶段并先画一帧。</param>
        /// <param name="token">调用方的取消令牌。</param>
        /// <param name="timeout">等待上限；null 取 <see cref="DefaultTimeout"/>。</param>
        /// <returns>用户右键确认返回 true；取消 / 超时 / 被新会话顶掉返回 false。</returns>
        /// <remarks>
        /// <c>using</c> 的释放点在 <c>await</c> 之后：窗口的 flush 状态与背景快照
        /// 会在结果交还调用方之前还原，与旧实现 <c>finally</c> 的时序一致。
        /// </remarks>
        private static async Task<bool> RunAsync<TShape>(HWindow window, TShape shape, bool edit, CancellationToken token, TimeSpan? timeout)
            where TShape : DrawShape
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            // 取消旧会话会内联执行它的续体(见 DrawSession 类注释)；续体里若又发起了一次绘制，
            // 那个会话会在本次 Begin 之前注册，随即被本会话遮住，再也收不到鼠标事件，
            // 只能等 5 分钟超时，期间 IsDrawing 一直为 true。所以取消到窗口上确实没有会话为止。
            // 设上限：续体若每次被取消都重新发起绘制，不设上限会在 UI 线程上死循环。
            int rounds = 0;
            do { CancelDraw(window); }
            while (DrawSession.ActiveFor(window) != null && ++rounds < MaxCancelRounds);

            if (DrawSession.ActiveFor(window) != null)
                DotNet.Drawing.Log.Warn(nameof(DrawHelper), "取消旧绘制会话后仍不断有新会话注册，已放弃继续取消。");

            using (var session = DrawSession.Begin(window, shape))
            {
                if (edit)
                {
                    shape.BeginEdit();
                    session.RenderInitial();
                }
                return await session.WaitForCompletionAsync(timeout ?? DefaultTimeout, token);
            }
        }

        #endregion

        #region 新建 ROI

        /// <summary>
        /// 交互式绘制一个点 ROI: 左键点击设置位置后进入编辑, 可拖拽十字调整, 右键确认.
        /// </summary>
        public static async Task<DrawPointResult> DrawPointAsync(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new PointShape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return new DrawPointResult(ok, s.Y, s.X);
        }

        /// <summary>
        /// 交互式绘制一条线段 ROI: 左键按下→拖拽→释放定义两端点后进入编辑,
        /// 可拖拽端点或中点平移整条线, 右键确认.
        /// </summary>
        public static async Task<DrawLineResult> DrawLineAsync(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new LineShape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return new DrawLineResult(ok, s.Y1, s.X1, s.Y2, s.X2);
        }

        /// <summary>交互式绘制一个轴对齐矩形 ROI, 右键确认。输出已归一化为左上/右下。</summary>
        public static async Task<DrawRectangle1Result> DrawRectangle1Async(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new Rect1Shape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return NormalizeRect1(ok, s);
        }

        /// <summary>交互式绘制一个可旋转矩形 ROI, 右键确认。</summary>
        public static async Task<DrawRectangle2Result> DrawRectangle2Async(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new Rect2Shape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return new DrawRectangle2Result(ok, s.CY, s.CX, s.Phi, s.HalfLen1, s.HalfLen2);
        }

        /// <summary>交互式绘制一个圆 ROI, 右键确认。</summary>
        public static async Task<DrawCircleResult> DrawCircleAsync(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new CircleShape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return new DrawCircleResult(ok, s.CY, s.CX, s.Radius);
        }

        /// <summary>交互式绘制一个椭圆 ROI, 右键确认。输出 radius1 &gt;= radius2, phi 为长轴方向。</summary>
        public static async Task<DrawEllipseResult> DrawEllipseAsync(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new EllipseShape();
            bool ok = await RunAsync(window, s, edit: false, token, timeout);
            return NormalizeEllipse(ok, s);
        }

        /// <summary>
        /// 交互式绘制一个多边形区域 ROI: 左键逐点添加顶点, 右键闭合并确认。
        /// 返回的 <see cref="DrawRegionResult.Region"/> 所有权归调用方，未确认时是一个可正常释放的空区域。
        /// </summary>
        public static async Task<DrawRegionResult> DrawRegionAsync(HWindow window,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            // 先备好空 region: 无论走哪条失败路径, 调用方拿到的都是可释放对象
            HOperatorSet.GenEmptyRegion(out HObject region);

            // RunAsync 抛出(如 window 为 null)时调用方拿不到结果对象，这里必须自己释放
            var s = new RegionShape();
            bool ok;
            try
            {
                ok = await RunAsync(window, s, edit: false, token, timeout);
            }
            catch
            {
                region.Dispose();
                throw;
            }
            if (!ok || s.Rows.Count < 3) return new DrawRegionResult(false, region);

            HObject contour = null;
            try
            {
                HTuple rows = new HTuple(ToArray(s.Rows));
                HTuple cols = new HTuple(ToArray(s.Cols));
                // 首尾相接才能围成闭合轮廓
                rows = rows.TupleConcat(s.Rows[0]);
                cols = cols.TupleConcat(s.Cols[0]);
                HOperatorSet.GenContourPolygonXld(out contour, rows, cols);

                HOperatorSet.GenRegionContourXld(contour, out HObject filled, "filled");
                DrawSafe.Dispose(region);
                region = filled;
            }
            catch
            {
                // 抛异常时调用方拿不到返回值, 占位对象只能在这里兜底释放
                DrawSafe.Dispose(region);
                throw;
            }
            finally
            {
                DrawSafe.Dispose(contour);
            }

            return new DrawRegionResult(true, region);
        }

        #endregion

        #region 修改已有 ROI

        /// <summary>
        /// 修改一个已有的点 ROI: 以 (rowIn, columnIn) 为初始位置进入编辑模式,
        /// 用户可拖拽十字调整位置, 右键确认.
        /// </summary>
        public static async Task<DrawPointResult> DrawPointModAsync(HWindow window,
            double rowIn, double columnIn, CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new PointShape { X = columnIn, Y = rowIn };
            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return new DrawPointResult(ok, s.Y, s.X);
        }

        /// <summary>
        /// 修改一条已有的线段 ROI: 以两端点为初始几何进入编辑模式,
        /// 可拖拽两端点或中点平移整条线, 右键确认.
        /// </summary>
        public static async Task<DrawLineResult> DrawLineModAsync(HWindow window,
            double row1In, double column1In, double row2In, double column2In,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new LineShape
            {
                X1 = column1In, Y1 = row1In,
                X2 = column2In, Y2 = row2In,
            };
            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return new DrawLineResult(ok, s.Y1, s.X1, s.Y2, s.X2);
        }

        /// <summary>
        /// 修改一个轴对齐矩形 ROI: 以 (row1,col1)-(row2,col2) 为初始对角点进入编辑模式,
        /// 可拖拽两个角点或中心点平移, 右键确认.
        /// </summary>
        public static async Task<DrawRectangle1Result> DrawRectangle1ModAsync(HWindow window,
            double row1In, double column1In, double row2In, double column2In,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new Rect1Shape
            {
                X1 = column1In, Y1 = row1In,
                X2 = column2In, Y2 = row2In,
            };
            s.SyncCenter();

            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return NormalizeRect1(ok, s);
        }

        /// <summary>
        /// 修改一个可旋转矩形 ROI: 以中心 (rowIn, columnIn)、方向 phiIn (弧度)、
        /// 半轴长 length1In/length2In 为初始几何进入编辑模式,
        /// 可拖拽中心 / 主轴端点 / 短轴端点, 右键确认.
        /// </summary>
        public static async Task<DrawRectangle2Result> DrawRectangle2ModAsync(HWindow window,
            double rowIn, double columnIn, double phiIn, double length1In, double length2In,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new Rect2Shape
            {
                CX = columnIn,
                CY = rowIn,
                Phi = phiIn,
                HalfLen1 = Math.Max(1, length1In),
                HalfLen2 = Math.Max(1, length2In),
            };
            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return new DrawRectangle2Result(ok, s.CY, s.CX, s.Phi, s.HalfLen1, s.HalfLen2);
        }

        /// <summary>
        /// 修改一个圆 ROI: 以中心 (rowIn, columnIn)、半径 radiusIn 为初始几何进入编辑模式,
        /// 可拖拽圆心或半径端点, 右键确认.
        /// </summary>
        public static async Task<DrawCircleResult> DrawCircleModAsync(HWindow window,
            double rowIn, double columnIn, double radiusIn, CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new CircleShape
            {
                CX = columnIn,
                CY = rowIn,
                Radius = Math.Max(1, radiusIn),
            };
            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return new DrawCircleResult(ok, s.CY, s.CX, s.Radius);
        }

        /// <summary>
        /// 修改一个椭圆 ROI: 以中心 (rowIn, columnIn)、方向 phiIn (弧度, 与 radius1 同向)、
        /// 长/短半径 radius1In/radius2In 为初始几何进入编辑模式,
        /// 可拖拽中心 / 主轴端点 / 副轴端点, 右键确认.
        /// 输出 radius1 始终 &gt;= radius2, phi 自动调整为长轴方向.
        /// </summary>
        public static async Task<DrawEllipseResult> DrawEllipseModAsync(HWindow window,
            double rowIn, double columnIn, double phiIn, double radius1In, double radius2In,
            CancellationToken token = default, TimeSpan? timeout = null)
        {
            var s = new EllipseShape
            {
                CX = columnIn,
                CY = rowIn,
                Phi = phiIn,
                R1 = Math.Max(1, radius1In),
                R2 = Math.Max(1, radius2In),
            };
            bool ok = await RunAsync(window, s, edit: true, token, timeout);
            return NormalizeEllipse(ok, s);
        }

        #endregion

        #region 取消 / 事件转发

        /// <summary>取消全部窗口上正在进行的交互绘制。</summary>
        public static void CancelDraw() => CancelDraw(null);

        /// <summary>取消指定窗口上正在进行的交互绘制；<paramref name="window"/> 为 null 时取消全部。</summary>
        /// <remarks>
        /// 不再调用 <c>HalconAPI.CancelDraw()</c>：它是进程级操作，会打断<b>所有</b>窗口上的原生 <c>draw_*</c>；
        /// 本库已不再使用原生 <c>draw_*</c>，留着它只剩跨窗口误伤的风险。
        /// </remarks>
        public static void CancelDraw(HWindow window)
        {
            DrawSession.CancelAll(window);
        }

        /// <summary>指定窗口上是否有正在进行的交互绘制会话。</summary>
        public static bool IsDrawing(HWindow window) => DrawSession.ActiveFor(window) != null;

        /// <summary>把鼠标按下事件转发给该窗口当前的绘制会话；没有会话时静默忽略。</summary>
        public static void ForwardMouseDown(HWindow window, HMouseEventArgs e)
            => DrawSession.ActiveFor(window)?.OnMouseDown(e);

        /// <summary>把鼠标抬起事件转发给该窗口当前的绘制会话；没有会话时静默忽略。</summary>
        public static void ForwardMouseUp(HWindow window, HMouseEventArgs e)
            => DrawSession.ActiveFor(window)?.OnMouseUp(e);

        /// <summary>把鼠标移动事件转发给该窗口当前的绘制会话；没有会话时静默忽略。</summary>
        public static void ForwardMouseMove(HWindow window, HMouseEventArgs e)
            => DrawSession.ActiveFor(window)?.OnMouseMove(e);

        /// <summary>把滚轮事件转发给该窗口当前的绘制会话；没有会话时静默忽略。</summary>
        public static void ForwardMouseWheel(HWindow window, HMouseEventArgs e)
            => DrawSession.ActiveFor(window)?.OnMouseWheel(e);

        #endregion

        #region 输出换算

        // 两个对角点的先后取决于用户往哪个方向拖, 统一归一化为 (左上, 右下)
        private static DrawRectangle1Result NormalizeRect1(bool completed, Rect1Shape s)
        {
            DrawGeometry.NormalizeRect1(s.X1, s.Y1, s.X2, s.Y2,
                out double left, out double top, out double right, out double bottom);
            return new DrawRectangle1Result(completed, top, left, bottom, right);
        }

        // HALCON 约定 radius1 为长半轴, 因此长短轴颠倒时要交换并把 phi 旋转 90°。
        // 最后统一折回 (-π, π]: 与交互绘制时 Atan2 给出的区间一致 (phiIn 由调用方传入, 本身也可能越界)
        private static DrawEllipseResult NormalizeEllipse(bool completed, EllipseShape s)
        {
            double phi = s.R1 >= s.R2 ? s.Phi : s.Phi + Math.PI / 2;
            phi = Math.IEEERemainder(phi, 2 * Math.PI);     // → [-π, π]
            if (phi <= -Math.PI) phi += 2 * Math.PI;         // → (-π, π]
            return new DrawEllipseResult(completed, s.CY, s.CX, phi,
                Math.Max(s.R1, s.R2), Math.Min(s.R1, s.R2));
        }

        private static double[] ToArray(IReadOnlyList<double> src)
        {
            var arr = new double[src.Count];
            for (int i = 0; i < src.Count; i++) arr[i] = src[i];
            return arr;
        }

        #endregion
    }
}
