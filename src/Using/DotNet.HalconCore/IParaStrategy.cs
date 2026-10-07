using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNet.HalconCore
{
    #region 能力接口

    /// <summary>
    /// 算法执行：策略的核心职责，不涉及任何界面概念。
    /// </summary>
    public interface IAlgoStrategy
    {
        /// <summary> 实例的稳定标识：下游引用（<see cref="SourceRef"/>）、方案文件、数据目录都按它定位 </summary>
        Guid Id { get; set; }

        /// <summary> 显示名，用户可改；默认取 <see cref="AlgoAttribute.DisplayName"/> </summary>
        string Name { get; set; }

        /// <summary> 最近一次执行的结果；从未执行时为 null </summary>
        RunResult LastResult { get; }

        /// <summary>
        /// 执行一次，并把要显示的内容写进 <paramref name="overlay"/>；为 null 时只计算不绘制（无界面运行）。
        /// 叠加层由调用方创建、拥有、释放。
        /// 失败不抛异常，而是返回 <see cref="RunStatus.Error"/>，此时所有输出均已复位；
        /// 只有取消（<see cref="OperationCanceledException"/>）会向外传播。
        /// </summary>
        RunResult Run(RunContext context, IOverlay overlay);
    }

    /// <summary>
    /// 输出解析：把策略的计算结果按路径暴露给下游。
    /// </summary>
    public interface IOutputProvider
    {
        /// <summary> 声明的输出（树的根节点），含基类追加的"结果 / 文本显示" </summary>
        IReadOnlyList<OutputItem> Outputs { get; }

        /// <summary> 按完整路径（例如 <c>坐标系/原点/行</c>）查找输出；不存在返回 null </summary>
        OutputItem FindOutput(string path);
    }

    /// <summary>
    /// ROI 编辑：需要在画面上交互式绘制或显示区域的策略才实现。
    /// 新建 ROI 的默认形状由 <see cref="AlgoAttribute.DefaultRoi"/> 声明。
    /// </summary>
    public interface IRoiEditable
    {
        /// <summary>
        /// 交互式绘制 / 修改 ROI。要等用户在画面上右键确认，因此是异步的：
        /// 调用方必须在 UI 线程 await，不要 .Wait()（会死锁）。
        /// </summary>
        Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI);

        /// <summary>把已有 ROI 画到画面上，无交互，保持同步。</summary>
        void DispROI(IRoiHost host);
    }

    /// <summary>
    /// 模板编辑：支持新建或修改匹配模板的策略才实现。
    /// </summary>
    public interface ITemplateEditable
    {
        /// <summary>
        /// 交互式框选模板区域并创建模板。内含 ROI 绘制交互，故为异步；调用方须在 UI 线程 await。
        /// </summary>
        Task SetTemplateAsync(IRoiHost host, RectEnum type, bool newModel);

        /// <summary> 模板的只读视图，供宿主的模板编辑窗 / 缩略图使用 </summary>
        TemplateView GetTemplateView();
    }

    /// <summary>
    /// 参数面板：策略声明"有哪些参数"，宿主负责生成控件和双向绑定。
    /// </summary>
    public interface IParaBinding
    {
        /// <summary>
        /// 重新声明一遍参数。每次调用都返回新列表，getter / setter 绑定到当前的参数实例。
        /// </summary>
        IReadOnlyList<ParamItem> DescribeParams();

        /// <summary> 宿主在一轮写回中至少有一项真的变了之后调用一次；<paramref name="changed"/> 是真正变了的项 </summary>
        void ParamsChanged(IReadOnlyList<ParamItem> changed);
    }

    /// <summary>
    /// 宿主眼中的一个工具：执行、输出、生命周期。可选能力按接口判断，例如
    /// <c>if (s is IRoiEditable roi) await roi.DrawROIAsync(...)</c>。
    /// </summary>
    public interface IParaStrategy : IAlgoStrategy, IOutputProvider, IDisposable
    {
        /// <summary> 可序列化的配置（参数类实例）；宿主做通用序列化用 </summary>
        object Para { get; set; }

        /// <summary> 本工具的数据目录（模板图等），宿主按方案设置；未设置时按 <see cref="IAlgoStrategy.Id"/> 推导 </summary>
        string DataDir { get; set; }

        /// <summary> 工具页打开：申请运行期资源 </summary>
        void Init(IRoiHost host);

        /// <summary> 工具页关闭：只释放运行期临时对象，不销毁配置态 </summary>
        void Close(IRoiHost host);
    }

    #endregion

    /// <summary>
    /// 策略基类：执行模板、输出声明、参数声明、状态报告、生命周期都在这里统一实现，子类只填算法本身。
    /// </summary>
    /// <remarks>
    /// <b>一个算法 = 一个类</b>：继承本类（或同族中间基类）、打上 <see cref="AlgoAttribute"/>，
    /// 按需实现 <see cref="IRoiEditable"/> / <see cref="ITemplateEditable"/>，即可完整地加入一个算法。
    /// <para>类内分区，各自待在固定的方法里：</para>
    /// <list type="bullet">
    /// <item>配置：<typeparamref name="TPara"/>（只放可序列化的配置）+ <see cref="DeclareParams"/>；</item>
    /// <item>计算：<see cref="Execute"/>，纯计算、不碰显示，结果写到策略自己的属性上；</item>
    /// <item>绘制：<see cref="Render"/>，只读结果去画；状态文本由基类统一画；</item>
    /// <item>声明：<see cref="DeclareOutputs"/>，一次声明同时生成变量树和解析器。</item>
    /// </list>
    /// </remarks>
    public abstract class ParaStrategyBase<TPara> : IParaStrategy, IParaBinding
        where TPara : DisplayOptions, new()
    {
        private TPara _para = new TPara();
        private string _name;
        private string _dataDir;
        private List<OutputItem> _outputs;
        private Dictionary<string, OutputItem> _outputIndex;
        private bool _disposed;

        protected ParaStrategyBase()
        {
            Id = Guid.NewGuid();
        }

        /// <summary> 可序列化的配置 </summary>
        public TPara inPara
        {
            get => _para;
            set => _para = value ?? throw new ArgumentNullException(nameof(value));
        }

        object IParaStrategy.Para
        {
            get => _para;
            set => inPara = value as TPara ?? throw new ArgumentException($"参数类型应为 {typeof(TPara).Name}", nameof(value));
        }

        public Guid Id { get; set; }

        public string Name
        {
            get => _name ?? (_name = AlgoAttribute.Of(GetType())?.DisplayName ?? GetType().Name);
            set => _name = value;
        }

        public string DataDir
        {
            get => _dataDir ?? Path.Combine("Config", "Tools", Id.ToString("N"));
            set => _dataDir = value;
        }

        public RunResult LastResult { get; private set; }

        protected bool IsDisposed => _disposed;

        #region 执行

        /// <summary>
        /// 执行顺序固定：<see cref="ResetOutputs"/> → 计时 <see cref="Execute"/>（异常转成 Fail，失败再次复位）
        /// → <see cref="Render"/> → 状态文本。绘制与执行在同一个线程上，只写进叠加层，不碰窗口。
        /// </summary>
        public RunResult Run(RunContext context, IOverlay overlay)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (_disposed) throw new ObjectDisposedException(GetType().Name);

            ResetOutputs();
            var watch = Stopwatch.StartNew();
            RunResult result;
            try
            {
                context.Cancellation.ThrowIfCancellationRequested();
                result = Execute(context) ?? RunResult.Ok();
            }
            catch (OperationCanceledException)
            {
                ResetOutputs();
                throw;
            }
            catch (Exception ex)
            {
                result = RunResult.Fail(ex.Message);
                Log.Warn(GetType().Name, $"{Name} 执行失败: {ex.Message}", ex);
            }
            watch.Stop();
            result.Elapsed = watch.Elapsed;

            // 失败后所有输出都必须是默认值: 宿主不看返回值时, 下游也不能读到上一轮 / 半截的结果
            if (result.Status == RunStatus.Error) ResetOutputs();
            LastResult = result;

            if (overlay != null)
            {
                try { Render(overlay, result); }
                catch (Exception ex) { Log.Warn(GetType().Name, $"{Name} 绘制失败.", ex); }
                DrawStatus(overlay, result);
            }
            return result;
        }

        /// <summary> 把所有输出复位成默认值（"没有结果"）。每轮开头、失败后都会调用 </summary>
        protected abstract void ResetOutputs();

        /// <summary>
        /// 纯计算：不碰显示，结果写到策略的属性上。可以直接抛异常，基类会转成 <see cref="RunResult.Fail"/>；
        /// 消息里不必带工具名，状态文本会自动加上。
        /// </summary>
        protected abstract RunResult Execute(RunContext context);

        /// <summary>
        /// 只读运行结果，描述要画什么；失败时也会调用，可以画出已有的部分数据便于排查。
        /// 叠加层会复制句柄，只供绘制的对象可以在这里写完后立即释放。
        /// </summary>
        protected virtual void Render(IOverlay overlay, RunResult result) { }

        /// <summary>
        /// 状态文本规则：失败 / 警告始终显示（红字）；成功只在 <see cref="DisplayOptions.DispText"/> 为 true 时显示（绿字）。
        /// </summary>
        private void DrawStatus(IOverlay overlay, RunResult result)
        {
            if (string.IsNullOrEmpty(result.Message)) return;
            bool ok = result.Status == RunStatus.Ok;
            if (ok && !inPara.DispText) return;
            try
            {
                overlay.Text($"{Name} : {result.Message}", new Point2d(inPara.FontX, inPara.FontY),
                    DrawStyle.Of(ok ? HColor.Green : HColor.Red, inPara.FontSize));
            }
            catch (Exception ex) { Log.Warn(GetType().Name, $"{Name} 状态文本绘制失败.", ex); }
        }

        #endregion

        #region 参数

        /// <summary> 显示页分组：放显示开关 </summary>
        public const string DisplayGroup = "显示设置";

        /// <summary> 显示页分组：放文本坐标 / 字号 / 点大小 </summary>
        public const string FontGroup = "字体设置";

        /// <summary>
        /// 声明参数面板。"显示文本 / 字体"几项由基类自动追加到显示页；
        /// 策略自己的显示开关请归入 <see cref="DisplayGroup"/>，数值项归入 <see cref="FontGroup"/>。
        /// </summary>
        protected abstract void DeclareParams(ParamBuilder p);

        /// <summary>
        /// 宿主写回参数且至少有一项真的变了之后调用（例如清空示教态）。
        /// <paramref name="changed"/> 只含真正变了的项：只改了显示选项时，策略可以据此不动示教态。
        /// </summary>
        protected virtual void OnParamsChanged(IReadOnlyList<ParamItem> changed) { }

        public IReadOnlyList<ParamItem> DescribeParams()
        {
            var p = new ParamBuilder();
            DeclareParams(p);
            p.Tab(TabPageEnum.Display)
             .Group(DisplayGroup)
             .Flag("显示文本", () => inPara.DispText, v => inPara.DispText = v)
             .Group(FontGroup)
             .Int("文本X", () => inPara.FontX, v => inPara.FontX = v, presets: new[] { 20, 50 })
             .Int("文本Y", () => inPara.FontY, v => inPara.FontY = v, presets: new[] { 20, 50 })
             .Int("字号", () => inPara.FontSize, v => inPara.FontSize = v, presets: new[] { 15, 30 }, min: 1);
            return p.Items;
        }

        public void ParamsChanged(IReadOnlyList<ParamItem> changed)
        {
            if (changed != null && changed.Count > 0) OnParamsChanged(changed);
        }

        #endregion

        #region 输出

        /// <summary> 声明输出；getter 每次解析时求值，直接读策略上的结果属性即可 </summary>
        protected abstract void DeclareOutputs(OutputBuilder o);

        public IReadOnlyList<OutputItem> Outputs
        {
            get
            {
                EnsureOutputs();
                return _outputs;
            }
        }

        public OutputItem FindOutput(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            EnsureOutputs();
            return _outputIndex.TryGetValue(path, out var item) ? item : null;
        }

        private void EnsureOutputs()
        {
            if (_outputs != null) return;
            var o = new OutputBuilder();
            DeclareOutputs(o);
            o.AddCommon("结果", OutEnum.Result, () => LastResult?.IsOk ?? false);
            o.AddCommon("文本显示", OutEnum.String, () => LastResult?.Message);
            _outputs = o.Roots.ToList();
            _outputIndex = OutputBuilder.Index(_outputs);
        }

        #endregion

        #region 生命周期

        public virtual void Init(IRoiHost host) { }

        public virtual void Close(IRoiHost host) { }

        /// <summary> 幂等。子类覆盖 <see cref="Dispose(bool)"/> 释放自己的句柄，并调用基类实现 </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) { }

        #endregion

        public override string ToString() => $"{Name} ({GetType().Name})";
    }

    /// <summary>
    /// 工具集合的辅助方法：按 Id 查找、拼显示名。
    /// </summary>
    public static class StrategyExtensions
    {
        public static IParaStrategy FindTool(this IEnumerable<IParaStrategy> tools, Guid id)
        {
            if (tools == null || id == Guid.Empty) return null;
            foreach (var tool in tools)
            {
                if (tool != null && tool.Id == id) return tool;
            }
            return null;
        }

        /// <summary>
        /// 界面上显示的 "工具名/输出"；本地为 "默认"；工具不在列表里时标出"已失效"，而不是静默显示旧名字。
        /// </summary>
        public static string Describe(this IEnumerable<IParaStrategy> tools, SourceRef source)
        {
            if (source.IsLocal) return "默认";
            var tool = tools.FindTool(source.ToolId);
            return tool == null ? $"<已失效>/{source.Output}" : $"{tool.Name}/{source.Output}";
        }
    }

    /// <summary>
    /// 策略输出解析失败。携带路径与期望类型，避免退化成 NullReferenceException / InvalidCastException。
    /// </summary>
    public class AlgoOutputNotFoundException : Exception
    {
        public string Path { get; }
        public Type ExpectedType { get; }
        public Type ActualType { get; }

        public AlgoOutputNotFoundException(string fullPath, Type expectedType)
            : base(string.Format("未能解析策略输出 '{0}' (期望类型 {1}): 路径不存在或上游策略尚未产出结果.",
                                 fullPath, expectedType == null ? "?" : expectedType.Name))
        {
            Path = fullPath;
            ExpectedType = expectedType;
        }

        public AlgoOutputNotFoundException(string fullPath, Type expectedType, Type actualType)
            : base(actualType == null
                ? string.Format("未能解析策略输出 '{0}' (期望类型 {1}): 路径不存在或上游策略尚未产出结果.",
                                fullPath, expectedType == null ? "?" : expectedType.Name)
                : string.Format("策略输出 '{0}' 的类型不匹配: 期望 {1}, 实际 {2}.",
                                fullPath, expectedType == null ? "?" : expectedType.Name, actualType.Name))
        {
            Path = fullPath;
            ExpectedType = expectedType;
            ActualType = actualType;
        }
    }
}
