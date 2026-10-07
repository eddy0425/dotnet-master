using System;
using System.Collections.Generic;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.VisionRuntime
{
    /// <summary> 叠加层图元的种类 </summary>
    public enum OverlayKind
    {
        Object,
        Point,
        Points,
        Line,
        Arrow,
        Circle,
        Coord,
        Rect2,
        Text,
    }

    /// <summary> 有向矩形（<see cref="IOverlay.AddRect2"/> 的参数） </summary>
    public sealed class OverlayRect2
    {
        public OverlayRect2(Point2d center, double phi, double length1, double length2)
        {
            Center = center;
            Phi = phi;
            Length1 = length1;
            Length2 = length2;
        }

        public Point2d Center { get; }

        /// <summary> 弧度 </summary>
        public double Phi { get; }

        public double Length1 { get; }
        public double Length2 { get; }
    }

    /// <summary> 叠加层里的一个图元 </summary>
    public sealed class OverlayItem
    {
        internal OverlayItem(OverlayKind kind, object value, DrawStyle style, Point2d position = default(Point2d))
        {
            Kind = kind;
            Value = value;
            Style = style;
            Position = position;
        }

        public OverlayKind Kind { get; }

        /// <summary>
        /// 按 <see cref="Kind"/>：Object 为 <see cref="HObject"/>（叠加层拥有的副本）、Point 为 <see cref="Point2d"/>、
        /// Points 为 <see cref="Point2d"/>[]、Line / Arrow / Circle / Coord 为对应几何、Rect2 为 <see cref="OverlayRect2"/>、Text 为字符串。
        /// </summary>
        public object Value { get; }

        public DrawStyle Style { get; }

        /// <summary> 文本的位置 (X=列, Y=行)；其它种类无意义 </summary>
        public Point2d Position { get; }
    }

    /// <summary>
    /// 叠加层的实现：按顺序记下图元，HALCON 对象一律保存副本。
    /// </summary>
    /// <remarks>
    /// 由调用 <c>Run</c> 的一方创建：流程引擎每步一份，放进 <see cref="FlowStepResult.Overlay"/>。
    /// 不碰任何窗口，可以在执行线程上填写；显示控件在 UI 线程上用 <see cref="DrawTo"/> 重放，
    /// 缩放、平移、尺寸变化后重放同一份即可，不必重新运行。
    /// <para>不是线程安全的：填写与重放不能同时进行（先填完，再交给显示）。</para>
    /// </remarks>
    public sealed class OverlayList : IOverlay, IDisposable
    {
        private readonly List<OverlayItem> _items = new List<OverlayItem>();
        private bool _disposed;

        public IReadOnlyList<OverlayItem> Items => _items;

        public int Count => _items.Count;

        public bool IsDisposed => _disposed;

        public void Add(HObject obj, DrawStyle style = null)
        {
            ThrowIfDisposed();
            if (!obj.NotNull() || obj.CountObj() == 0) return;
            _items.Add(new OverlayItem(OverlayKind.Object, obj.CopyObj(1, -1), style));
        }

        public void Add(Point2d point, DrawStyle style = null) => Append(OverlayKind.Point, point, style);

        public void Add(IReadOnlyList<Point2d> points, DrawStyle style = null)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0) return;
            // 复制一份: 算法之后还会改它的列表
            Append(OverlayKind.Points, points.ToArray(), style);
        }

        public void Add(CvLine line, DrawStyle style = null) => Append(OverlayKind.Line, line ?? throw new ArgumentNullException(nameof(line)), style);

        public void Add(CvArrow arrow, DrawStyle style = null) => Append(OverlayKind.Arrow, arrow ?? throw new ArgumentNullException(nameof(arrow)), style);

        public void Add(CvCircle circle, DrawStyle style = null) => Append(OverlayKind.Circle, circle ?? throw new ArgumentNullException(nameof(circle)), style);

        public void Add(CvCoord coord, DrawStyle style = null) => Append(OverlayKind.Coord, coord, style);

        /// <summary> ROI 是可变的配置对象，只保存它此刻的区域副本 </summary>
        public void Add(CvRegion region, DrawStyle style = null)
        {
            if (region == null) return;
            Add(region.HoRegion, style);
        }

        public void AddRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
            => Append(OverlayKind.Rect2, new OverlayRect2(center, phi, length1, length2), style);

        public void Text(string message, Point2d position, DrawStyle style = null)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(message)) return;
            _items.Add(new OverlayItem(OverlayKind.Text, message, style, position));
        }

        /// <summary>
        /// 把 <paramref name="other"/> 的图元按顺序移到本层末尾（不复制句柄），<paramref name="other"/> 随之变空。
        /// </summary>
        public void Absorb(OverlayList other)
        {
            ThrowIfDisposed();
            if (other == null || ReferenceEquals(other, this)) return;
            other.ThrowIfDisposed();
            _items.AddRange(other._items);
            other._items.Clear();
        }

        /// <summary>
        /// 在显示窗口上重放全部图元。必须在窗口所在的线程上调用；单个图元失败只记日志，不影响其余图元。
        /// </summary>
        public void DrawTo(IHDisplay display)
        {
            if (display == null) throw new ArgumentNullException(nameof(display));
            if (_disposed) return;
            foreach (var item in _items)
            {
                try { Draw(display, item); }
                catch (Exception ex) { Log.Warn(nameof(OverlayList), $"重放叠加层图元 {item.Kind} 失败.", ex); }
            }
        }

        private static void Draw(IHDisplay display, OverlayItem item)
        {
            switch (item.Kind)
            {
                case OverlayKind.Object: display.Disp((HObject)item.Value, item.Style); break;
                case OverlayKind.Point: display.Disp((Point2d)item.Value, item.Style); break;
                case OverlayKind.Points: display.Disp((Point2d[])item.Value, item.Style); break;
                case OverlayKind.Line: display.Disp((CvLine)item.Value, item.Style); break;
                case OverlayKind.Arrow: display.Disp((CvArrow)item.Value, item.Style); break;
                case OverlayKind.Circle: display.Disp((CvCircle)item.Value, item.Style); break;
                case OverlayKind.Coord: display.Disp((CvCoord)item.Value, item.Style); break;
                case OverlayKind.Rect2:
                    var rect = (OverlayRect2)item.Value;
                    display.DispRect2(rect.Center, rect.Phi, rect.Length1, rect.Length2, item.Style);
                    break;
                case OverlayKind.Text: display.DispText((string)item.Value, item.Position, item.Style); break;
                default: throw new NotSupportedException("未知的叠加层图元: " + item.Kind);
            }
        }

        private void Append(OverlayKind kind, object value, DrawStyle style)
        {
            ThrowIfDisposed();
            _items.Add(new OverlayItem(kind, value, style));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OverlayList));
        }

        /// <summary> 释放全部句柄副本；幂等 </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var item in _items)
            {
                if (item.Kind == OverlayKind.Object) (item.Value as HObject)?.Dispose();
            }
            _items.Clear();
        }
    }
}
