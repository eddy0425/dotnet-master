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

        /// <summary> 字符串（条码期望值、文件名前缀…） </summary>
        Text,

        /// <summary> 文件路径，带过滤器 </summary>
        File,

        /// <summary> 按钮：执行策略的一个方法（"重新示教"、"清除模板"） </summary>
        Action,

        /// <summary> 可变长的上游输出引用列表 </summary>
        SourceList,
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
        internal ParamItem(string page, string label, ParamKind kind)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentNullException(nameof(label));
            Page = page;
            Label = label;
            Kind = kind;
        }

        /// <summary> 所在页（宿主按声明顺序生成页签）；内置页名见 <see cref="Pages"/> </summary>
        public string Page { get; }
        public string Label { get; }
        public ParamKind Kind { get; }

        internal Func<bool> VisibleWhen { get; set; }

        /// <summary> 所属分组（宿主画成一个分组框）；null 表示不分组 </summary>
        public string Group { get; internal set; }

        /// <summary> 当前是否显示；宿主在任一参数写回后重新求值 </summary>
        public bool IsVisible => VisibleWhen == null || VisibleWhen();

        public abstract object GetValue();

        /// <summary>
        /// 写回一个已校验的值。与当前值相同时不调用 setter，返回 false ——
        /// 宿主据此判断"配置是否真的变了"，策略不必再自己比较新旧值。
        /// </summary>
        public bool TrySetValue(object value)
        {
            if (SameValue(GetValue(), value)) return false;
            SetValue(value);
            return true;
        }

        /// <summary> 新值与当前值是否相同（相同则不写回）；列表一类按内容比较的项覆盖它 </summary>
        protected virtual bool SameValue(object current, object value) => Equals(current, value);

        protected abstract void SetValue(object value);

        public override string ToString() => $"[{Page}] {Label} ({Kind})";
    }

    /// <summary> 强类型参数项的公共实现 </summary>
    public abstract class ParamItem<T> : ParamItem
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal ParamItem(string page, string label, ParamKind kind, Func<T> get, Action<T> set)
            : base(page, label, kind)
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
        internal SourceParam(string page, string label, Func<SourceRef> get, Action<SourceRef> set, OutEnum sourceType)
            : base(page, label, ParamKind.Source, get, set)
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

        internal ChoiceParam(string page, string label, Func<object> get, Action<object> set, IReadOnlyList<ParamOption> options)
            : base(page, label, ParamKind.Choice)
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

        internal NumberParam(string page, string label, ParamKind kind, Func<object> get, Action<object> set,
            IReadOnlyList<string> presets, double? min, double? max)
            : base(page, label, kind)
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
        internal FlagParam(string page, string label, Func<bool> get, Action<bool> set)
            : base(page, label, ParamKind.Flag, get, set) { }
    }

    public sealed class FolderParam : ParamItem<string>
    {
        internal FolderParam(string page, string label, Func<string> get, Action<string> set)
            : base(page, label, ParamKind.Folder, get, set) { }
    }

    public sealed class TextParam : ParamItem<string>
    {
        internal TextParam(string page, string label, Func<string> get, Action<string> set)
            : base(page, label, ParamKind.Text, get, set) { }
    }

    public sealed class FileParam : ParamItem<string>
    {
        internal FileParam(string page, string label, Func<string> get, Action<string> set, string filter)
            : base(page, label, ParamKind.File, get, set)
        {
            Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter;
        }

        /// <summary> 文件对话框的过滤器，写法同 WinForms：<c>"图像|*.bmp;*.png|所有文件|*.*"</c> </summary>
        public string Filter { get; }
    }

    /// <summary>
    /// 按钮：执行策略的一个方法。没有值；宿主按下按钮时写回 <see cref="Press"/>，
    /// 于是和其它参数走同一条写回路径 —— 连续运行时排在两帧之间、在执行线程上执行，之后照常通知 <c>ParamsChanged</c>。
    /// </summary>
    public sealed class ActionParam : ParamItem
    {
        /// <summary> 宿主按下按钮时写回的值 </summary>
        public static readonly object Press = new object();

        private readonly Action _action;

        internal ActionParam(string page, string label, Action action)
            : base(page, label, ParamKind.Action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override object GetValue() => null;

        protected override bool SameValue(object current, object value) => !ReferenceEquals(value, Press);

        protected override void SetValue(object value) => _action();
    }

    /// <summary> 可变长的上游输出引用列表（例如"区域合并"的输入）；按内容判断是否变了 </summary>
    public sealed class SourceListParam : ParamItem<IReadOnlyList<SourceRef>>
    {
        internal SourceListParam(string page, string label, Func<IReadOnlyList<SourceRef>> get, Action<IReadOnlyList<SourceRef>> set,
            OutEnum sourceType, int maxCount)
            : base(page, label, ParamKind.SourceList, () => get() ?? new SourceRef[0], set)
        {
            if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount));
            SourceType = sourceType;
            MaxCount = maxCount;
        }

        public OutEnum SourceType { get; }

        /// <summary> 最多几项；0 表示不限 </summary>
        public int MaxCount { get; }

        protected override bool SameValue(object current, object value)
        {
            var a = current as IEnumerable<SourceRef> ?? new SourceRef[0];
            var b = value as IEnumerable<SourceRef> ?? new SourceRef[0];
            return a.SequenceEqual(b);
        }
    }

    /// <summary>
    /// 参数声明：一份声明同时生成面板、回存逻辑、来源类型与输入依赖。
    /// </summary>
    /// <remarks>
    /// <code>
    /// p.Page(Pages.Parameter)
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
        private string _page = Pages.Parameter;
        private string _group;

        public IReadOnlyList<ParamItem> Items => _items;

        /// <summary>
        /// 之后声明的项放到哪一页；默认 <see cref="Pages.Parameter"/>。换页同时结束分组。
        /// 宿主按页第一次出现的顺序生成页签，插件可以声明自己的页；内置页一律用 <see cref="Pages"/> 常量。
        /// </summary>
        public ParamBuilder Page(string page)
        {
            if (string.IsNullOrWhiteSpace(page)) throw new ArgumentNullException(nameof(page));
            _page = page.Trim();
            _group = null;
            return this;
        }

        /// <summary> 旧写法：映射到 <see cref="Pages"/> 里同名的页 </summary>
        [Obsolete("页签改为字符串: 用 Page(Pages.Parameter) 等")]
        public ParamBuilder Tab(TabPageEnum tab)
        {
            switch (tab)
            {
                case TabPageEnum.Region: return Page(Pages.Region);
                case TabPageEnum.Display: return Page(Pages.Display);
                default: return Page(Pages.Parameter);
            }
        }

        /// <summary>
        /// 之后声明的项归入哪个分组；传 null 结束分组。同页同名的分组合并显示，
        /// 例如策略声明的显示开关与基类追加的"显示文本"都归入"显示设置"。
        /// </summary>
        public ParamBuilder Group(string title)
        {
            _group = string.IsNullOrWhiteSpace(title) ? null : title;
            return this;
        }

        public ParamBuilder Source(string label, Func<SourceRef> get, Action<SourceRef> set, OutEnum type)
            => Add(new SourceParam(_page, label, get, set, type));

        public ParamBuilder Choice<T>(string label, Func<T> get, Action<T> set, params Option<T>[] options)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (options == null || options.Length == 0) throw new ArgumentException($"{label}: 至少要有一个选项", nameof(options));
            var list = options.Select(o => new ParamOption(o.Value, o.Text)).ToList();
            return Add(new ChoiceParam(_page, label, () => get(), v => set((T)v), list));
        }

        public ParamBuilder Int(string label, Func<int> get, Action<int> set, int[] presets = null, int? min = null, int? max = null)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            return Add(new NumberParam(_page, label, ParamKind.Int, () => get(), v => set((int)v),
                presets?.Select(p => NumberParam.Format(p)).ToList(), min, max));
        }

        public ParamBuilder Double(string label, Func<double> get, Action<double> set, double[] presets = null, double? min = null, double? max = null)
        {
            if (get == null) throw new ArgumentNullException(nameof(get));
            if (set == null) throw new ArgumentNullException(nameof(set));
            return Add(new NumberParam(_page, label, ParamKind.Double, () => get(), v => set((double)v),
                presets?.Select(p => NumberParam.Format(p)).ToList(), min, max));
        }

        public ParamBuilder Flag(string label, Func<bool> get, Action<bool> set)
            => Add(new FlagParam(_page, label, get, set));

        public ParamBuilder Folder(string label, Func<string> get, Action<string> set)
            => Add(new FolderParam(_page, label, get, set));

        public ParamBuilder Text(string label, Func<string> get, Action<string> set)
            => Add(new TextParam(_page, label, get, set));

        /// <param name="filter">文件对话框的过滤器，例如 <c>"图像|*.bmp;*.png"</c>；省略时为所有文件。</param>
        public ParamBuilder File(string label, Func<string> get, Action<string> set, string filter = null)
            => Add(new FileParam(_page, label, get, set, filter));

        /// <summary> 按钮：按下时执行 <paramref name="action"/>（宿主保证不与执行交错） </summary>
        public ParamBuilder Action(string label, Action action)
            => Add(new ActionParam(_page, label, action));

        /// <summary>
        /// 可变长的来源列表：每项都是一条输入依赖，宿主逐项校验。setter 收到的是新列表，策略自行复制保存。
        /// </summary>
        /// <param name="maxCount">最多几项；0 表示不限。</param>
        public ParamBuilder SourceList(string label, Func<IReadOnlyList<SourceRef>> get, Action<IReadOnlyList<SourceRef>> set,
            OutEnum type, int maxCount = 0)
            => Add(new SourceListParam(_page, label, get, set, type, maxCount));

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
            if (_items.Any(i => i.Page == item.Page && i.Label == item.Label))
                throw new ArgumentException($"参数 '{item.Label}' 在 '{item.Page}' 页重复声明");
            item.Group = _group;
            _items.Add(item);
            return this;
        }
    }
}
