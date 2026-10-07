using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconAlgo.Tests
{
    /// <summary>
    /// 记录绘制调用的 <see cref="IHDisplay"/> / <see cref="IOverlay"/>：不接真实窗口，只供断言策略「画了什么」。
    /// </summary>
    /// <remarks>
    /// 同时是叠加层：<c>Run(context, display)</c> 时策略写进来的图元与交互时直接画的图元记在同一组列表里。
    /// 与真实叠加层不同，HALCON 对象只记引用、不复制 —— 便于断言"画的就是那个对象"。
    /// </remarks>
    internal sealed class FakeDisplay : IHDisplay, IOverlay
    {
        public readonly List<Drawn<string>> Texts = new List<Drawn<string>>();
        public readonly List<Drawn<Point2d>> Points = new List<Drawn<Point2d>>();
        public readonly List<Drawn<HObject>> Objects = new List<Drawn<HObject>>();
        public readonly List<Drawn<CvRegion>> Regions = new List<Drawn<CvRegion>>();
        public readonly List<Point2d> Rect2Centers = new List<Point2d>();
        public readonly List<CvArrow> Arrows = new List<CvArrow>();
        public readonly List<CvCoord> Coords = new List<CvCoord>();
        public int DispImageCount;

        /// <summary> 清掉已记录的绘制（长循环测试里避免记录本身占内存） </summary>
        public void Clear()
        {
            Texts.Clear(); Points.Clear(); Objects.Clear(); Regions.Clear();
            Rect2Centers.Clear(); Arrows.Clear(); Coords.Clear();
        }

        public bool IsCross { get; set; }
        public bool Adaptive { get; set; }
        public double HoWidth { get; private set; }
        public double HoHeight { get; private set; }
        public Size2d HoSize => new Size2d(HoWidth, HoHeight);
        public Point2d HoCentre => new Point2d(HoWidth / 2, HoHeight / 2);

        /// <summary>不接管所有权：图像由测试自己释放。</summary>
        public HObject HoImage { get; set; }

        /// <summary>最近一条文本；没有则为 null。</summary>
        public string LastText => Texts.Count == 0 ? null : Texts[Texts.Count - 1].Item;

        public HColor GetColor() => HColor.Green;
        public void SetColor(HColor color) { }
        public void SetDraw(string mode) { }
        public void SetFontSize(HTuple size) { }

        public void SetImage(HObject image)
        {
            HoImage = image;
            if (image.NotNull())
            {
                HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
                HoWidth = w.D;
                HoHeight = h.D;
            }
        }

        public void DispImage(HObject image) => DispImage(image, true);

        public void DispImage(HObject image, bool isSetPart)
        {
            DispImageCount++;
            SetImage(image);
        }

        public void ReDispImage() { }
        public void ClearWinDisp(HObject objectVal) { }

        public void Disp(Point2d point, DrawStyle style = null) => Points.Add(new Drawn<Point2d>(point, style));
        public void Disp(IReadOnlyList<Point2d> points, DrawStyle style = null)
        {
            foreach (var p in points) Points.Add(new Drawn<Point2d>(p, style));
        }
        public void Disp(CvCoord coord, DrawStyle style = null) => Coords.Add(coord);
        public void Disp(CvLine line, DrawStyle style = null) { }
        public void Disp(CvArrow arrow, DrawStyle style = null) => Arrows.Add(arrow);
        public void Disp(CvCircle circle, DrawStyle style = null) { }
        public void Disp(CvRegion region, DrawStyle style = null) => Regions.Add(new Drawn<CvRegion>(region, style));
        public void Disp(HObject region, DrawStyle style = null) => Objects.Add(new Drawn<HObject>(region, style));

        public void DispText(string message, Point2d position, DrawStyle style = null) => Texts.Add(new Drawn<string>(message, style, position));

        public void DispRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
            => Rect2Centers.Add(center);

        public void DispRegionOutline(CvRegion region, DrawStyle style = null) { }
        public void DispLineWithEndMarker(CvLine line, double markerRadius, DrawStyle style = null) { }
        public void DispSegmentWithCrosses(Point2d start, Point2d end, double armLength, DrawStyle style = null) { }

        public Task<bool> DrawRegionAsync(CvRegion region) => Task.FromResult(false);
        public Task<bool> DrawRegionModAsync(CvRegion region) => Task.FromResult(false);
        public Task<HObject> DrawRegionAsync(RectEnum type) => Task.FromResult<HObject>(null);

        public void Dispose()
        {
            foreach (var copy in _copies) copy.Dispose();
            _copies.Clear();
        }

        #region IOverlay

        private readonly List<HObject> _copies = new List<HObject>();

        /// <summary>
        /// 为 true 时 <see cref="IOverlay.Add(HObject, DrawStyle)"/> 像真实叠加层一样记副本（随 <see cref="Dispose"/> 释放）：
        /// 策略写完叠加层就释放自己的对象，要在运行之后检查画出的几何时打开。
        /// </summary>
        public bool CopyObjects { get; set; }

        public void Add(HObject obj, DrawStyle style = null)
        {
            if (CopyObjects && obj.NotNull())
            {
                obj = obj.CopyObj(1, -1);
                _copies.Add(obj);
            }
            Disp(obj, style);
        }
        public void Add(Point2d point, DrawStyle style = null) => Disp(point, style);
        public void Add(IReadOnlyList<Point2d> points, DrawStyle style = null) => Disp(points, style);
        public void Add(CvLine line, DrawStyle style = null) => Disp(line, style);
        public void Add(CvArrow arrow, DrawStyle style = null) => Disp(arrow, style);
        public void Add(CvCircle circle, DrawStyle style = null) => Disp(circle, style);
        public void Add(CvCoord coord, DrawStyle style = null) => Disp(coord, style);
        public void Add(CvRegion region, DrawStyle style = null) => Disp(region, style);
        public void AddRect2(Point2d center, double phi, double length1, double length2, DrawStyle style = null)
            => DispRect2(center, phi, length1, length2, style);
        public void Text(string message, Point2d position, DrawStyle style = null) => DispText(message, position, style);

        #endregion
    }

    /// <summary>
    /// 可编排的 <see cref="IInteractionHost"/>：「用户画框」由 <see cref="OnDraw"/> 模拟，返回值模拟确认 / 取消。
    /// 提示图形记在 <see cref="FakeDisplay"/> 上（它同时是叠加层）。
    /// </summary>
    internal sealed class FakeInteractionHost : IInteractionHost
    {
        public FakeDisplay FakeDisplay { get; } = new FakeDisplay();

        public HObject CurrentImage => FakeDisplay.HoImage;

        public IOverlay Feedback => FakeDisplay;

        /// <summary>绘制时对区域做的修改（模拟用户拖出的几何）。</summary>
        public Action<CvRegion> OnDraw { get; set; }

        /// <summary>DrawRegionAsync / DrawRegionModAsync 的返回值：false 表示用户取消。</summary>
        public bool Confirm { get; set; } = true;

        public int DrawCount, DrawModCount, ShowRoiCount, TemplateChanges;
        public RectEnum? TypeDuringDraw;
        public CvRegion ShownRoi;

        /// <summary> 最近一次模板变化后读到的模板视图（<see cref="Listen"/> 之后才记） </summary>
        public TemplateView ChangedView;

        /// <summary> 像宿主的模板编辑器一样订阅模板变化 </summary>
        public void Listen(ITemplateEditable template)
        {
            template.TemplateChanged += (s, e) =>
            {
                TemplateChanges++;
                ChangedView = template.GetTemplateView();
            };
        }

        public Task<bool> DrawRegionAsync(CvRegion hRegion)
        {
            DrawCount++;
            TypeDuringDraw = hRegion.Type;
            if (Confirm) OnDraw?.Invoke(hRegion);
            return Task.FromResult(Confirm);
        }

        public Task<bool> DrawRegionModAsync(CvRegion hRegion)
        {
            DrawModCount++;
            TypeDuringDraw = hRegion.Type;
            if (Confirm) OnDraw?.Invoke(hRegion);
            return Task.FromResult(Confirm);
        }

        public void ShowRoi(CvRegion roi)
        {
            ShowRoiCount++;
            ShownRoi = roi;
        }
    }

    /// <summary>把工具的输出树（<see cref="IOutputProvider.Outputs"/>）拍平成 "工具/节点/子节点" 路径，便于断言。</summary>
    internal sealed class FakeTree
    {
        public readonly List<string> Paths = new List<string>();
        public readonly Dictionary<string, OutEnum> Types = new Dictionary<string, OutEnum>();

        public static FakeTree Of(IParaStrategy tool)
        {
            var tree = new FakeTree();
            tree.Paths.Add(tool.Name);
            foreach (var item in tool.Outputs) tree.Add(tool.Name, item);
            return tree;
        }

        private void Add(string prefix, OutputItem item)
        {
            string path = prefix + "/" + item.Name;
            Paths.Add(path);
            Types[path] = item.Type;
            foreach (var child in item.Children) Add(path, child);
        }
    }

    internal sealed class StubPara : DisplayOptions { }

    /// <summary>上游策略替身：按名字声明输出，值由委托提供（每次解析时求值）。</summary>
    internal sealed class StubStrategy : ParaStrategyBase<StubPara>
    {
        private readonly List<Action<OutputBuilder>> _outputs = new List<Action<OutputBuilder>>();

        public StubStrategy(string name) { Name = name; }

        public StubStrategy Image(string name, Func<HObject> get) { _outputs.Add(o => o.Image(name, get)); return this; }
        public StubStrategy Region(string name, Func<HObject> get) { _outputs.Add(o => o.Region(name, get)); return this; }
        public StubStrategy Line(string name, Func<CvLine> get) { _outputs.Add(o => o.Line(name, get)); return this; }
        public StubStrategy Point(string name, Func<Point2d> get) { _outputs.Add(o => o.Point(name, get)); return this; }
        public StubStrategy Number(string name, Func<double> get) { _outputs.Add(o => o.Number(name, get)); return this; }
        public StubStrategy CoordOut(string name, Func<CvCoord> current, Func<Point2d?> template)
        {
            _outputs.Add(o => o.Coord(name, current, template));
            return this;
        }

        /// <summary>
        /// 模拟一个「坐标系」上游：输出 "坐标系"，模板点为示教原点，当前值为当前位姿。
        /// </summary>
        public static StubStrategy Coord(string name, Point2d? tmplPoint, CvCoord current)
            => new StubStrategy(name).CoordOut("坐标系", () => current, () => tmplPoint);

        protected override void DeclareParams(ParamBuilder p) { }
        protected override void DeclareOutputs(OutputBuilder o) { foreach (var declare in _outputs) declare(o); }
        protected override void ResetOutputs() { }
        protected override RunResult Execute(RunContext context) => RunResult.Ok();
    }

    internal static class Run
    {
        /// <summary> 某个工具某个输出的引用 </summary>
        public static SourceRef Ref(this IAlgoStrategy tool, string output) => SourceRef.To(tool, output);

        /// <summary> 以显示窗口里的图作为当前图像执行一次（相当于原来的 Fun_action(display, strategys)） </summary>
        public static RunResult On(this IAlgoStrategy strategy, FakeDisplay display, params IParaStrategy[] upstream)
            => strategy.Run(new RunContext(display.HoImage, upstream), display);

        /// <summary> 单图验证：没有上游（相当于原来的 Fun_action(image, display)） </summary>
        public static RunResult OnImage(this IAlgoStrategy strategy, HObject image, FakeDisplay display)
            => strategy.Run(RunContext.ForImage(image), display);
    }

    internal static class Params
    {
        public static ParamItem Param(this IParaBinding binding, string label, string page = null)
        {
            var found = binding.DescribeParams().Where(i => i.Label == label && (page == null || i.Page == page)).ToList();
            if (found.Count != 1) throw new InvalidOperationException($"参数 '{label}' 找到 {found.Count} 个");
            return found[0];
        }

        /// <summary> 像面板一样写回一项：值变了才调用 setter 并通知策略 </summary>
        public static bool SetParam(this IParaBinding binding, string label, object value, string page = null)
        {
            var item = binding.Param(label, page);
            bool changed = item.TrySetValue(value);
            if (changed) binding.ParamsChanged(new[] { item });
            return changed;
        }

        public static string[] Labels(this IParaBinding binding, string page)
            => binding.DescribeParams().Where(i => i.Page == page).Select(i => i.Label).ToArray();
    }

    /// <summary>捕获 <see cref="Log"/> 输出；Dispose 时恢复原 logger。</summary>
    internal sealed class CapturingLogger : ILogger, IDisposable
    {
        private readonly ILogger _previous;
        public readonly List<LogEntry> Entries = new List<LogEntry>();

        public CapturingLogger()
        {
            _previous = DotNet.Drawing.Log.Current;
            DotNet.Drawing.Log.Current = this;
        }

        public void Log(LogLevel level, string category, string message, Exception exception)
            => Entries.Add(new LogEntry { Level = level, Category = category, Message = message, Exception = exception });

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

    /// <summary>一次绘制调用：绘制对象 + 样式（文本另带位置）。</summary>
    internal sealed class Drawn<T>
    {
        public Drawn(T item, DrawStyle style, Point2d position = default)
        {
            Item = item;
            Style = style;
            Position = position;
        }

        public T Item { get; }
        public DrawStyle Style { get; }
        public Point2d Position { get; }

        public string ColorName => Style?.Color.Name;
    }

    internal static class Strategies
    {
        public static List<IParaStrategy> Of(params IParaStrategy[] items) => new List<IParaStrategy>(items);
    }
}
