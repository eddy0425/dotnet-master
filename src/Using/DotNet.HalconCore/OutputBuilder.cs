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

        internal OutputItem(string path, OutEnum type, Type valueType, Func<object> getter, Func<Point2d?> template = null)
        {
            Path = path;
            Type = type;
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
            _getter = getter;
            _template = template;
        }

        /// <summary> 工具内的完整路径，例如 <c>坐标系/原点/行</c> </summary>
        public string Path { get; }

        /// <summary> 路径的最后一段，即树节点上显示的文字 </summary>
        public string Name => Path.Substring(Path.LastIndexOf('/') + 1);

        /// <summary> 输出种类：决定图标与显示方式；声明了种类的来源还要求种类相同（图像和区域都是 HObject） </summary>
        public OutEnum Type { get; }

        /// <summary> 值的 CLR 类型；来源兼容按它的可赋值关系判断（见 <see cref="SourceCompatibility"/>） </summary>
        public Type ValueType { get; }

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
    /// 来源与输出是否兼容。宿主的运行前校验与来源选择窗共用这一条规则，二者不会一个放行、一个报错。
    /// </summary>
    /// <remarks>
    /// 先看 CLR 类型的可赋值关系（来源声明了类型时）；再看种类：声明了种类的来源要求种类相同 ——
    /// 图像和区域都是 <see cref="HObject"/>，只看类型会把图像接到区域上。
    /// 两条历史上的宽松规则保留：<see cref="OutEnum.String"/> 接受各种标量，<see cref="OutEnum.CalOrOut"/> 接受角度 / 数值 / 文本。
    /// </remarks>
    public static class SourceCompatibility
    {
        /// <param name="wanted">来源要求的种类；<see cref="OutEnum.Undefined"/> 表示只按类型判断。</param>
        /// <param name="wantedType">来源要求的 CLR 类型；null 表示只按种类判断。</param>
        public static bool Accepts(OutEnum wanted, Type wantedType, OutputItem output)
        {
            if (output == null) return false;
            if (wantedType != null)
            {
                if (!wantedType.IsAssignableFrom(output.ValueType)) return false;
                if (wanted == OutEnum.Undefined) return true;
            }
            if (output.Type == wanted) return true;
            switch (wanted)
            {
                case OutEnum.String:
                    return output.Type != OutEnum.HTuple && output.Type != OutEnum.Outline &&
                           output.Type != OutEnum.Image && output.Type != OutEnum.Region;
                case OutEnum.CalOrOut:
                    return output.Type == OutEnum.Angle || output.Type == OutEnum.Number || output.Type == OutEnum.String;
                default:
                    return false;
            }
        }

        /// <summary> 报错与提示用的"需要什么" </summary>
        public static string Describe(OutEnum wanted, Type wantedType)
            => wantedType == null ? wanted.ToString()
             : wanted == OutEnum.Undefined ? wantedType.Name
             : $"{wanted}({wantedType.Name})";
    }

    /// <summary>
    /// 输出声明。策略在 <c>DeclareOutputs</c> 里调用；getter 每次解析时求值，因此可以直接读策略上的结果属性。
    /// </summary>
    public sealed class OutputBuilder
    {
        private readonly List<OutputItem> _roots = new List<OutputItem>();

        public IReadOnlyList<OutputItem> Roots => _roots;

        public OutputBuilder Image(string name, Func<HObject> get) => Add(new OutputItem(name, OutEnum.Image, typeof(HObject), () => get()));

        public OutputBuilder Region(string name, Func<HObject> get) => Add(new OutputItem(name, OutEnum.Region, typeof(HObject), () => get()));

        public OutputBuilder Number(string name, Func<double> get) => Add(new OutputItem(name, OutEnum.Number, typeof(double), () => get()));

        /// <summary> 圆；自动带出 <c>圆心</c>（行 / 列）与 <c>半径</c> </summary>
        public OutputBuilder Circle(string name, Func<CvCircle> get)
        {
            return Add(new OutputItem(name, OutEnum.Circle, typeof(CvCircle), () => get())
                .Add(PointItem(name + "/圆心", () => get()?.Center ?? default(Point2d)))
                .Add(new OutputItem(name + "/半径", OutEnum.Number, typeof(double), () => get()?.Radius ?? 0d)));
        }

        /// <summary> 文本（读到的条码、分类结果…） </summary>
        public OutputBuilder Text(string name, Func<string> get) => Add(new OutputItem(name, OutEnum.String, typeof(string), () => get()));

        /// <summary> 开关量（是否合格、是否找到…） </summary>
        public OutputBuilder Flag(string name, Func<bool> get) => Add(new OutputItem(name, OutEnum.Result, typeof(bool), () => get()));

        /// <summary> 一组数（各个匹配的得分、各段的长度…） </summary>
        public OutputBuilder Numbers(string name, Func<IReadOnlyList<double>> get)
            => Add(new OutputItem(name, OutEnum.Array, typeof(IReadOnlyList<double>), () => get()));

        /// <summary>
        /// 自定义类型的输出：只有按同一个 CLR 类型声明来源（<see cref="ParamBuilder.Source{T}"/>）的下游能引用它 ——
        /// 上下游必须引用定义这个类型的<b>同一个</b>程序集，各自定义一个同名类型会被判为不兼容。
        /// </summary>
        /// <param name="type">树上的图标与显示方式；默认 <see cref="OutEnum.Undefined"/>。</param>
        public OutputBuilder Value<T>(string name, Func<T> get, OutEnum type = OutEnum.Undefined)
            => Add(new OutputItem(name, type, typeof(T), () => get()));

        /// <summary> 点；自动带出 <c>行</c> / <c>列</c> </summary>
        public OutputBuilder Point(string name, Func<Point2d> get) => Add(PointItem(name, get));

        /// <summary> 线段；自动带出 <c>起点</c> / <c>终点</c>（各自再带出行 / 列） </summary>
        public OutputBuilder Line(string name, Func<CvLine> get)
        {
            return Add(new OutputItem(name, OutEnum.Line, typeof(CvLine), () => get())
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
            return Add(new OutputItem(name, OutEnum.Coord, typeof(CvCoord), () => current(), template)
                .Add(PointItem(name + "/原点", () => current().Center))
                .Add(new OutputItem(name + "/角度", OutEnum.Number, typeof(double), () => current().Angle.Radians)));
        }

        private static OutputItem PointItem(string path, Func<Point2d> get)
        {
            return new OutputItem(path, OutEnum.Point, typeof(Point2d), () => get())
                .Add(new OutputItem(path + "/行", OutEnum.Number, typeof(double), () => get().Y))
                .Add(new OutputItem(path + "/列", OutEnum.Number, typeof(double), () => get().X));
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

        internal OutputBuilder AddCommon<T>(string name, OutEnum type, Func<T> get)
        {
            _roots.Add(new OutputItem(name, type, typeof(T), () => get()));
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
