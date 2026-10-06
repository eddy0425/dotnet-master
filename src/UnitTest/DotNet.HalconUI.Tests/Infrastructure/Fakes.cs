using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 记录绘制调用的 <see cref="IHDisplay"/>：不接真实窗口，只供断言鼠标处理器「画了什么」。
    /// </summary>
    internal sealed class FakeDisplay : IHDisplay
    {
        public readonly List<Drawn<Point2d>> Points = new List<Drawn<Point2d>>();
        public readonly List<Drawn<HObject>> Objects = new List<Drawn<HObject>>();
        public readonly List<Drawn<CvRegion>> Regions = new List<Drawn<CvRegion>>();
        public readonly List<Drawn<CvCoord>> Coords = new List<Drawn<CvCoord>>();

        /// <summary>SetDraw 的调用序列，用于断言「填充后恢复为 margin」。</summary>
        public readonly List<string> DrawModes = new List<string>();

        /// <summary>置为非 null 时，Disp(Point2d) 抛出该异常，模拟窗口已销毁等绘制失败。</summary>
        public Exception ThrowOnDispPoint;

        /// <summary>置为非 null 时，Disp(HObject) 抛出该异常。</summary>
        public Exception ThrowOnDispObject;

        public bool IsCross { get; set; }
        public bool Adaptive { get; set; }
        public double HoWidth => 0;
        public double HoHeight => 0;
        public Size2d HoSize => new Size2d(HoWidth, HoHeight);
        public Point2d HoCentre => new Point2d(HoWidth / 2, HoHeight / 2);
        public HObject HoImage => null;

        public HColor GetColor() => HColor.Green;
        public void SetColor(HColor color) { }
        public void SetDraw(string mode) => DrawModes.Add(mode);
        public void SetFontSize(HTuple size) { }

        public void SetImage(HObject image) { }
        public void DispImage(HObject image) { }
        public void DispImage(HObject image, bool isSetPart) { }
        public void ReDispImage() { }
        public void ClearWinDisp(HObject objectVal) { }

        /// <summary> 最近一次 Disp(Point2d) 调用所在的线程 </summary>
        public int LastCallThread;

        public void Disp(Point2d point, DrawStyle style = null)
        {
            LastCallThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (ThrowOnDispPoint != null) throw ThrowOnDispPoint;
            Points.Add(new Drawn<Point2d>(point, style));
        }

        public void Disp(IReadOnlyList<Point2d> points, DrawStyle style = null)
        {
            foreach (var p in points) Disp(p, style);
        }

        public void Disp(CvCoord coord, DrawStyle style = null) => Coords.Add(new Drawn<CvCoord>(coord, style));
        public void Disp(CvLine line, DrawStyle style = null) { }
        public void Disp(CvArrow arrow, DrawStyle style = null) { }
        public void Disp(CvCircle circle, DrawStyle style = null) { }
        public void Disp(CvRegion region, DrawStyle style = null) => Regions.Add(new Drawn<CvRegion>(region, style));
        public void Disp(HObject region, DrawStyle style = null)
        {
            if (ThrowOnDispObject != null) throw ThrowOnDispObject;
            Objects.Add(new Drawn<HObject>(region, style));
        }

        public void DispText(string message, Point2d position, DrawStyle style = null) { }
        public void DispRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null) { }
        public void DispRegionOutline(CvRegion region, DrawStyle style = null) { }
        public void DispLineWithEndMarker(CvLine line, double markerRadius, DrawStyle style = null) { }
        public void DispSegmentWithCrosses(Point2d start, Point2d end, double armLength, DrawStyle style = null) { }

        public Task<bool> DrawRegionAsync(CvRegion region) => Task.FromResult(false);
        public Task<bool> DrawRegionModAsync(CvRegion region) => Task.FromResult(false);
        public Task<HObject> DrawRegionAsync(RectEnum type) => Task.FromResult<HObject>(null);

        public void Dispose() { }
    }

    /// <summary>捕获 <see cref="Log"/> 输出；Dispose 时恢复原 logger。</summary>
    internal sealed class CapturingLogger : ILogger, IDisposable
    {
        private readonly ILogger _previous;
        private readonly object _gate = new object();
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public CapturingLogger()
        {
            _previous = DotNet.Drawing.Log.Current;
            DotNet.Drawing.Log.Current = this;
        }

        /// <summary>快照。超时回调跑在定时器线程上，读写都要加锁。</summary>
        public List<LogEntry> Entries
        {
            get { lock (_gate) return _entries.ToList(); }
        }

        public void Log(LogLevel level, string category, string message, Exception exception)
        {
            lock (_gate)
                _entries.Add(new LogEntry { Level = level, Category = category, Message = message, Exception = exception });
        }

        public IEnumerable<string> Messages(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message);

        public void Dispose() => DotNet.Drawing.Log.Current = _previous;
    }

    internal sealed class LogEntry
    {
        public LogLevel Level;
        public string Category;
        public string Message;
        public Exception Exception;
    }

    /// <summary>一次绘制调用：绘制对象 + 样式。</summary>
    internal sealed class Drawn<T>
    {
        public Drawn(T item, DrawStyle style)
        {
            Item = item;
            Style = style;
        }

        public T Item { get; }
        public DrawStyle Style { get; }

        public string ColorName => Style?.Color.Name;
    }
}
