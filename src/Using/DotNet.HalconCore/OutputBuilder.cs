using System;
using System.Collections.Generic;
using DotNet.Drawing;
using HalconDotNet;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 一个可被下游引用的输出：同一份声明同时生成变量树节点和解析器。
    /// </summary>
    /// <remarks>
    /// 原来变量树（<c>GenTreeNode</c>）和解析路径（<c>RegisterOutput</c>）要各写一遍同样的路径，
    /// 写得不一致只能在运行时发现。现在只声明一次，子节点（点的行 / 列、坐标系的原点 / 角度…）自动带出。
    /// </remarks>
    public sealed class OutputItem
    {
        private readonly Func<object> _getter;
        private readonly Func<Point2d?> _template;
        private readonly List<OutputItem> _children = new List<OutputItem>();

        internal OutputItem(string path, OutEnum type, Func<object> getter, Func<Point2d?> template = null)
        {
            Path = path;
            Type = type;
            _getter = getter;
            _template = template;
        }

        /// <summary> 工具内的完整路径，例如 <c>坐标系/原点/行</c> </summary>
        public string Path { get; }

        /// <summary> 路径的最后一段，即树节点上显示的文字 </summary>
        public string Name => Path.Substring(Path.LastIndexOf('/') + 1);

        public OutEnum Type { get; }

        public IReadOnlyList<OutputItem> Children => _children;

        /// <summary> 当前值；读不到（例如尚未运行）时可能是 null 或默认值 </summary>
        public object GetValue() => _getter();

        /// <summary>
        /// 坐标系输出的示教模板点：下游跟随时用它与当前坐标系一起算刚体变换。
        /// 不是坐标系、或尚未示教时返回 false。
        /// </summary>
        public bool TryGetTemplate(out Point2d template)
        {
            var value = _template?.Invoke();
            template = value ?? default(Point2d);
            return value.HasValue;
        }

        internal OutputItem Add(OutputItem child)
        {
            _children.Add(child);
            return this;
        }

        public override string ToString() => $"{Path} ({Type})";
    }

    /// <summary>
    /// 输出声明。策略在 <c>DeclareOutputs</c> 里调用；getter 每次解析时求值，因此可以直接读策略上的结果属性。
    /// </summary>
    public sealed class OutputBuilder
    {
        private readonly List<OutputItem> _roots = new List<OutputItem>();

        public IReadOnlyList<OutputItem> Roots => _roots;

        public OutputBuilder Image(string name, Func<HObject> get) => Add(new OutputItem(name, OutEnum.Image, () => get()));

        public OutputBuilder Region(string name, Func<HObject> get) => Add(new OutputItem(name, OutEnum.Region, () => get()));

        public OutputBuilder Number(string name, Func<double> get) => Add(new OutputItem(name, OutEnum.Number, () => get()));

        /// <summary> 点；自动带出 <c>行</c> / <c>列</c> </summary>
        public OutputBuilder Point(string name, Func<Point2d> get) => Add(PointItem(name, get));

        /// <summary> 线段；自动带出 <c>起点</c> / <c>终点</c>（各自再带出行 / 列） </summary>
        public OutputBuilder Line(string name, Func<CvLine> get)
        {
            return Add(new OutputItem(name, OutEnum.Line, () => get())
                .Add(PointItem(name + "/起点", () => get()?.Start ?? default(Point2d)))
                .Add(PointItem(name + "/终点", () => get()?.End ?? default(Point2d))));
        }

        /// <summary>
        /// 坐标系；自动带出 <c>原点</c>（行 / 列）与 <c>角度</c>（弧度），并登记下游跟随所需的示教模板点。
        /// </summary>
        /// <param name="template">示教模板点；尚未示教时返回 null，下游据此报"未示教"而不是静默算错。</param>
        public OutputBuilder Coord(string name, Func<CvCoord> current, Func<Point2d?> template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return Add(new OutputItem(name, OutEnum.Coord, () => current(), template)
                .Add(PointItem(name + "/原点", () => current().Center))
                .Add(new OutputItem(name + "/角度", OutEnum.Number, () => current().Angle.Radians)));
        }

        private static OutputItem PointItem(string path, Func<Point2d> get)
        {
            return new OutputItem(path, OutEnum.Point, () => get())
                .Add(new OutputItem(path + "/行", OutEnum.Number, () => get().Y))
                .Add(new OutputItem(path + "/列", OutEnum.Number, () => get().X));
        }

        internal OutputBuilder Add(OutputItem item)
        {
            if (string.IsNullOrEmpty(item.Path) || item.Path.IndexOf('/') >= 0)
                throw new ArgumentException($"输出名不能为空, 也不能含 '/': '{item.Path}'");
            foreach (var root in _roots)
            {
                if (root.Path == item.Path) throw new ArgumentException($"输出 '{item.Path}' 重复声明");
            }
            _roots.Add(item);
            return this;
        }

        internal OutputBuilder AddCommon(string name, OutEnum type, Func<object> get)
        {
            _roots.Add(new OutputItem(name, type, get));
            return this;
        }

        /// <summary> 按路径展开成字典（含自动带出的子节点） </summary>
        internal static Dictionary<string, OutputItem> Index(IEnumerable<OutputItem> roots)
        {
            var index = new Dictionary<string, OutputItem>(StringComparer.Ordinal);
            var stack = new Stack<OutputItem>(roots);
            while (stack.Count > 0)
            {
                var item = stack.Pop();
                index[item.Path] = item;
                foreach (var child in item.Children) stack.Push(child);
            }
            return index;
        }
    }
}
