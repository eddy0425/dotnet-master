using HalconDotNet;
using System;

namespace DotNet.HalconUI.Draw
{
    /// <summary>交互绘图的阶段：空闲 → 拖拽定形 → 编辑控制点。</summary>
    internal enum DrawPhase
    {
        Idle,
        Drawing,
        Editing,
    }

    /// <summary>编辑阶段可拖拽的控制点。</summary>
    internal enum DrawHandle
    {
        None,
        P1,
        P2,
        Center,
        AxisEnd1,
        AxisEnd2,
        AxisEnd2Neg,
    }

    /// <summary>
    /// 一种可交互绘制的图元：自带状态机 (Idle/Drawing/Editing)、命中测试与自身的绘制。
    /// </summary>
    /// <remarks>
    /// 拆出这一层是为了消掉原先 <c>DrawHelper</c> 里四份 7 分支 switch
    /// (OnMouseDown / OnMouseUp / RedrawAndFlush / RenderInitial)：新增图元只需新增一个子类，
    /// 不必再同步修改四处分发。
    /// <para>
    /// 坐标约定沿用 <see cref="HMouseEventArgs"/>：<c>e.X = column</c>，<c>e.Y = row</c>。
    /// 各子类的几何字段一律以 (x = column, y = row) 存储，只有交给 Halcon 时才换成 (row, column)。
    /// </para>
    /// </remarks>
    internal abstract class DrawShape
    {
        private IDrawCanvas _renderer;

        /// <summary>绘制目标。由 <see cref="DrawSession"/> 在会话开始时注入。</summary>
        protected IDrawCanvas R =>
            _renderer ?? throw new InvalidOperationException("DrawShape 尚未绑定 DrawRenderer.");

        internal void Attach(IDrawCanvas renderer) => _renderer = renderer;

        internal DrawPhase Phase { get; set; } = DrawPhase.Idle;

        internal DrawHandle Hover { get; set; } = DrawHandle.None;

        /// <summary>
        /// 是否正在拖拽控制点。<see cref="DrawSession.OnMouseDown"/> 会临时清零它，
        /// 因此必须是 internal 可写而非纯内部状态。
        /// </summary>
        internal bool Dragging { get; set; }

        /// <summary>用户是否已右键确认。确认后 <see cref="DrawSession"/> 结束阻塞等待。</summary>
        internal bool Completed { get; private set; }

        /// <summary>右键确认。</summary>
        protected void Confirm() => Completed = true;

        /// <summary>结束一次拖拽（左键释放）。</summary>
        protected void EndDrag()
        {
            Dragging = false;
            Hover = DrawHandle.None;
        }

        /// <summary>编辑阶段的通用左/右键收尾：左键释放结束拖拽，右键确认。</summary>
        /// <returns>已被本方法处理时返回 true。</returns>
        protected bool HandleEditingMouseUp(MouseInput e)
        {
            if (Phase != DrawPhase.Editing) return false;
            if (e.Button == System.Windows.Forms.MouseButtons.Left) { EndDrag(); return true; }
            if (e.Button == System.Windows.Forms.MouseButtons.Right) { Confirm(); return true; }
            return false;
        }

        /// <summary>命中测试：鼠标是否落在控制点 (x, y) 的热区内。</summary>
        protected bool IsNear(double x, double y, MouseInput e)
            => DrawGeometry.IsNear(R.PixelSize, x, y, e.X, e.Y);

        /// <summary>把外部传入的初始几何直接置为可编辑状态（<c>Draw*Mod</c> 入口用）。</summary>
        internal void BeginEdit() => Phase = DrawPhase.Editing;

        internal abstract void OnDown(MouseInput e);

        internal abstract void OnUp(MouseInput e);

        /// <summary>
        /// 按当前阶段把图元画到 backbuffer（起手先铺背景，结尾不负责 swap）。
        /// </summary>
        internal abstract void Render(MouseInput e);

        /// <summary>
        /// 不依赖鼠标位置的稳态绘制，供 <c>Draw*Mod</c> 在阻塞等待前把初始 ROI 显示出来。
        /// 默认按 “hover=None、dragging=false” 的稳态渲染。
        /// </summary>
        internal virtual void RenderStatic() { }
    }
}
