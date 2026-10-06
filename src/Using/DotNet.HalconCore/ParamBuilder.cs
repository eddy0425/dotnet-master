using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DotNet.HalconCore
{
    public enum ParamKind
    {
        /// <summary> 上游输出引用（<see cref="SourceRef"/>） </summary>
        Source,

        /// <summary> 从固定选项中选一个 </summary>
        Choice,

        /// <summary> 整数，可带常用值与上下限 </summary>
        Int,

        /// <summary> 浮点数，可带常用值与上下限 </summary>
        Double,

        /// <summary> 开关 </summary>
        Flag,

        /// <summary> 文件夹路径 </summary>
        Folder,
    }

    /// <summary>
    /// 参数面板上的一项：策略在 <c>DeclareParams</c> 里声明，宿主据此生成控件并双向绑定。
    /// </summary>
    /// <remarks>
    /// 取代原来"控件槽位名 + DispPara / SavePara 各写一遍"的隐藏契约：算法里不再出现任何控件名，
    /// 槽位的数量和位置也不再由宿主的 Designer 决定。本类型不依赖 WinForms。
    /// </remarks>
    public abstract class ParamItem
    {
        internal ParamItem(TabPageEnum tab, string label, ParamKind kind)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentNullException(nameof(label));
            Tab = tab;
            Label = label;
            Kind = kind;
        }

        public TabPageEnum Tab { get; }
        public string Label { get; }
        public ParamKind Kind { get; }

        internal Func<bool> VisibleWhen { get; set; }

        /// <summary> 当前是否显示；宿主在任一参数写回后重新求值 </summary>
        public bool IsVisible => VisibleWhen == null || VisibleWhen();

        public abstract object GetValue();

        /// <summary>
        /// 写回一个已校验的值。与当前值相同时不调用 setter，返回 false ——
        /// 宿主据此判断"配置是否真的变了"，策略不必再自己比较新旧值。
        /// </summary>
        public bool TrySetValue(object value)
        {
            if (Equals(GetValue(), value)) return false;
            SetValue(value);
            return true;
        }

        protected abstract void SetValue(object value);

        public override string ToString() => $"[{Tab}] {Label} ({Kind})";
    }

    /// <summary> 强类型参数项的公共实现 </summary>
    public abstract class ParamItem<T> : ParamItem
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal ParamItem(TabPageEnum tab, string label, ParamKind kind, Func<T> get, Action<T> set)
            : base(tab, label, kind)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
            _set = set ?? throw new ArgumentNullException(nameof(set));
        }

        public T Value => _get();

        public override object GetValue() => _get();

        protected override void SetValue(object value) => _set((T)value);
    }

    /// <summary> 上游输出引用；<see cref="SourceType"/> 决定可选哪种输出 </summary>
    public sealed class SourceParam : ParamItem<SourceRef>
    {
        internal SourceParam(TabPageEnum tab, string label, Func<SourceRef> get, Action<SourceRef> set, OutEnum sourceType)
            : base(tab, label, ParamKind.Source, get, set)
        {
            SourceType = sourceType;
        }

        public OutEnum SourceType { get; }

        /// <summary> 是否允许"本地"（"默认"）：图像 / 区域 / 坐标系有本地含义，其余类型没有 </summary>
        public bool AllowsLocal => SourceType == OutEnum.Image || SourceType == OutEnum.Region || SourceType == OutEnum.Coord;
    }

    /// <summary> 选项：存的是值（枚举 / bool / 数字），界面文字只出现在这里 </summary>
    public sealed class ParamOption
    {
        internal ParamOption(object value, string text)
        {
            Value = value;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public object Value { get; }
        public string Text { get; }

        public override string ToString() => Text;
    }

    /// <summary> <see cref="ParamBuilder.Choice{T}"/> 的强类型选项 </summary>
    public sealed class Option<T>
    {
        internal Option(T value, string text)
        {
            Value = value;
            Text = text;
        }

        public T Value { get; }
        public string Text { get; }
    }

    /// <summary> 构造选项：<c>Option.Of(Transition.Positive, "由黑到白")</c> </summary>
    public static class Option
    {
        public static Option<T> Of<T>(T value, string text) => new Option<T>(value, text);
    }

    public sealed class ChoiceParam : ParamItem
    {
        private readonly Func<object> _get;
        private readonly Action<object> _set;

        internal ChoiceParam(TabPageEnum tab, string label, Func<object> get, Action<object> set, IReadOnlyList<ParamOption> options)
            : base(tab, label, ParamKind.Choice)
        {
            _get = get;
            _set = set;
            Options = options;
        }

        public IReadOnlyList<ParamOption> Options { get; }

        /// <summary> 当前值对应的选项下标；当前值不在选项里（例如旧配置写了非法值）时为 -1 </summary>
        public int SelectedIndex
        {
            get
            {
                var value = _get();
                for (int i = 0; i < Options.Count; i++)
                {
                    if (Equals(Options[i].Value, value)) return i;
                }
                return -1;
            }
        }

        public override object GetValue() => _get();

        protected override void SetValue(object value) => _set(value);
    }

    /// <summary> 整数 / 浮点数；文本解析与范围校验在这里，宿主只负责显示错误 </summary>
    public sealed class NumberParam : ParamItem
    {
        private readonly Func<object> _get;
        private readonly Action<object> _set;

        internal NumberParam(TabPageEnum tab, string label, ParamKind kind, Func<object> get, Action<object> set,
            IReadOnlyList<string> presets, double? min, double? max)
            : base(tab, label, kind)
        {
            _get = get;
            _set = set;
            Presets = presets ?? new string[0];
            Min = min;
            Max = max;
        }

        public bool IsInteger => Kind == ParamKind.Int;

        /// <summary> 下拉里的常用值（仍可手输） </summary>
        public IReadOnlyList<string> Presets { get; }

        public double? Min { get; }
        public double? Max { get; }

        public override object GetValue() => _get();

        protected override void SetValue(object value) => _set(value);

        /// <summary> 当前值的显示文本 </summary>
        public string Format() => Format(_get());

        internal static string Format(object value) => Convert.ToString(value, CultureInfo.CurrentCulture);

        /// <summary>
        /// 解析界面输入。失败时 <paramref name="error"/> 给出可直接显示的原因，<paramref name="value"/> 无意义。
        /// </summary>
        public bool TryParse(string text, out object value, out string error)
        {
            value = null;
            error = null;
            text = text?.Trim();
            double number;
            if (IsInteger)
            {
                if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int i) &&
                    !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out i))
                {
                    error = $"{Label}: '{text}' 不是整数";
                    return false;
                }
                number = i;
                value = i;
            }
            else
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number) &&
                    !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    error = $"{Label}: '{text}' 不是数值";
                    return false;
                }
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    error = $"{Label}: '{text}' 不是有效数值";
                    return false;
                }
                value = number;
            }

            if (Min.HasValue && number < Min.Value)
            {
                error = $"{Label}: 不能小于 {Format(Min.Value)}";
                return false;
            }
            if (Max.HasValue && number > Max.Value)
            {
                error = $"{Label}: 不能大于 {Format(Max.Value)}";
                return false;
            }
            return true;
        }
    }

    public sealed class FlagParam : ParamItem<bool>
    {
        internal FlagParam(TabPageEnum tab, string label, Func<bool> get, Action<bool> set)
            : base(tab, label, ParamKind.Flag, get, set) { }
    }

    public sealed class FolderParam : ParamItem<string>
    {
        internal FolderParam(TabPageEnum tab, string label, Func<string> get, Action<string> set)
            : base(tab, label, ParamKind.Folder, get, set) { }
    }

    /// <summary>
    /// 参数声明：一份声明同时生成面板、回存逻辑、来源类型与输入依赖。
    /// </summary>
    /// <remarks>
    /// <code>
    /// p.Tab(TabPageEnum.Parameter)
    ///  .Source("图像来源", () => inPara.ImageIn, v => inPara.ImageIn = v, OutEnum.Image)
    ///  .Choice("过渡方向", () => inPara.Transition, v => inPara.Transition = v,
    ///          Option.Of(Transition.Positive, "由黑到白"), Option.Of(Transition.Negative, "由白到黑"))
    ///  .Int("阈值", () => inPara.Threshold, v => inPara.Threshold = v, presets: new[] { 30, 50 }, min: 0);
    /// </code>
    /// 每个 <see cref="Source"/> 项同时是一条输入依赖，宿主可据此校验"来源必须是前面的工具且类型匹配"。
    /// </remarks>
    public sealed class ParamBuilder
    {
        private readonly List<ParamItem> _items = new List<ParamItem>();
        private TabPageEnum _tab = TabPageEnum.Parameter;

        public IReadOnlyList<ParamItem> Items => _items;

        /// <summary> 之后声明的项放到哪一页；默认 <see cref="TabPageEnum.Parameter"/> </summary>
        public ParamBuilder Tab(TabPageEnum tab)
        {
            _tab = tab;
            return this;
        }

        public ParamBuilder Source(string label, Func<SourceRef> get, Action<SourceRef> set, OutEnum type)
            => Add(new SourceParam(_tab, label, get, set, type));

        public ParamBuilder Choice<T>(string label, Func<T> get, Action<T> set, params Option<T>[] options)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (options == null || options.Length == 0) throw new ArgumentException($"{label}: 至少要有一个选项", nameof(options));
            var list = options.Select(o => new ParamOption(o.Value, o.Text)).ToList();
            return Add(new ChoiceParam(_tab, label, () => get(), v => set((T)v), list));
        }

        public ParamBuilder Int(string label, Func<int> get, Action<int> set, int[] presets = null, int? min = null, int? max = null)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            return Add(new NumberParam(_tab, label, ParamKind.Int, () => get(), v => set((int)v),
                presets?.Select(p => NumberParam.Format(p)).ToList(), min, max));
        }

        public ParamBuilder Double(string label, Func<double> get, Action<double> set, double[] presets = null, double? min = null, double? max = null)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            return Add(new NumberParam(_tab, label, ParamKind.Double, () => get(), v => set((double)v),
                presets?.Select(p => NumberParam.Format(p)).ToList(), min, max));
        }

        public ParamBuilder Flag(string label, Func<bool> get, Action<bool> set)
            => Add(new FlagParam(_tab, label, get, set));

        public ParamBuilder Folder(string label, Func<string> get, Action<string> set)
            => Add(new FolderParam(_tab, label, get, set));

        /// <summary>
        /// 给上一项加显示条件；任一参数写回后宿主重新求值。
        /// 用于"同一位置随模式切换含义"的情况：声明两项、各带一个条件，而不是让一个槽位身兼两职。
        /// </summary>
        public ParamBuilder When(Func<bool> visible)
        {
            if (visible == null) throw new ArgumentNullException(nameof(visible));
            if (_items.Count == 0) throw new InvalidOperationException("When 必须跟在某个参数项之后");
            _items[_items.Count - 1].VisibleWhen = visible;
            return this;
        }

        private ParamBuilder Add(ParamItem item)
        {
            if (_items.Any(i => i.Tab == item.Tab && i.Label == item.Label))
                throw new ArgumentException($"参数 '{item.Label}' 在 {item.Tab} 页重复声明");
            _items.Add(item);
            return this;
        }
    }
}
