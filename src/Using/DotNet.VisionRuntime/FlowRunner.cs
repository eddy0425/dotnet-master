using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.VisionRuntime
{
    /// <summary> 流程里某个工具失败之后怎么办 </summary>
    public enum FlowFailurePolicy
    {
        /// <summary> 停止，后面的工具不再执行 </summary>
        Stop,

        /// <summary> 继续执行后面的工具（它们的上游输出已复位，引用它的工具会各自报错） </summary>
        Continue,
    }

    /// <summary> 流程中一个工具的执行结果；拥有本步的叠加层 </summary>
    public sealed class FlowStepResult : IDisposable
    {
        public FlowStepResult(int index, IParaStrategy tool, RunResult result, OverlayList overlay = null, HObject image = null)
        {
            Index = index;
            Tool = tool;
            Result = result;
            Overlay = overlay;
            Image = image;
        }

        public int Index { get; }
        public IParaStrategy Tool { get; }
        public RunResult Result { get; }

        /// <summary>
        /// 本步要显示的内容；不绘制（<see cref="FlowRunner.Render"/> 为 false）或已被取走时为 null。随本对象释放
        /// </summary>
        public OverlayList Overlay { get; private set; }

        /// <summary> 取走本步的叠加层，所有权随之转移给调用方 </summary>
        public OverlayList TakeOverlay()
        {
            var overlay = Overlay;
            Overlay = null;
            return overlay;
        }

        /// <summary>
        /// 本步之后的"当前图像"，也是单步显示时的底图：成功的图像工具取它自己的输出，其余取它看到的当前图像。
        /// 借用句柄，属于产出它的工具（或调用方传入的初始图像），下一轮运行后可能失效。
        /// </summary>
        public HObject Image { get; }

        public void Dispose() => Overlay?.Dispose();

        public override string ToString() => $"{Index}. {Tool?.Name}: {Result}";
    }

    /// <summary> 一次流程运行的汇总；拥有各步的叠加层 </summary>
    public sealed class FlowRunResult : IDisposable
    {
        internal FlowRunResult(IReadOnlyList<FlowStepResult> steps, bool stopped, TimeSpan elapsed, HObject image)
        {
            Steps = steps;
            Stopped = stopped;
            Elapsed = elapsed;
            Image = image;
        }

        public IReadOnlyList<FlowStepResult> Steps { get; }

        /// <summary> 是否因为失败而提前停止 </summary>
        public bool Stopped { get; }

        public TimeSpan Elapsed { get; }

        /// <summary>
        /// 整帧显示的底图：流程结束时的"当前图像"（最后一个成功的图像工具的输出，没有则为初始图像）。借用句柄，规则同 <see cref="FlowStepResult.Image"/>
        /// </summary>
        public HObject Image { get; }

        /// <summary> 全部步骤都成功（没有警告、没有失败） </summary>
        public bool AllOk => Steps.All(s => s.Result.Status == RunStatus.Ok);

        /// <summary> 第一个失败的步骤；没有失败时为 null </summary>
        public FlowStepResult FirstError => Steps.FirstOrDefault(s => s.Result.Status == RunStatus.Error);

        /// <summary>
        /// 把各步的叠加层按执行顺序合成一份交给调用方（所有权随之转移），各步的叠加层随之变空。
        /// </summary>
        public OverlayList TakeOverlay()
        {
            var merged = new OverlayList();
            foreach (var step in Steps)
            {
                using (var overlay = step.TakeOverlay())
                {
                    if (overlay != null && !overlay.IsDisposed) merged.Absorb(overlay);
                }
            }
            return merged;
        }

        public void Dispose()
        {
            foreach (var step in Steps) step.Dispose();
        }
    }

    /// <summary> 运行前校验发现的一个问题 </summary>
    public sealed class FlowIssue
    {
        public FlowIssue(int index, IParaStrategy tool, string label, string message)
        {
            Index = index;
            Tool = tool;
            Label = label;
            Message = message;
        }

        /// <summary> 有问题的工具在流程中的位置 </summary>
        public int Index { get; }
        public IParaStrategy Tool { get; }

        /// <summary> 有问题的参数项（来源）的标签 </summary>
        public string Label { get; }

        public string Message { get; }

        public override string ToString() => $"{Tool?.Name} [{Label}]: {Message}";
    }

    /// <summary>
    /// 流程引擎：按顺序执行整个流程，为每个工具构造只含上游的 <see cref="RunContext"/>，收集每个工具的结果。
    /// </summary>
    /// <remarks>
    /// 本地图像来源取"当前图像"：从初始图像开始，每经过一个成功的 <see cref="IImageProducer"/>
    /// （文件图像、旋转图像…）就换成它的输出 —— 与原来"取显示窗口里的图"的语义一致，但不再依赖显示。
    /// 算法类不需要为流程运行写任何代码。
    /// </remarks>
    public sealed class FlowRunner
    {
        private readonly IReadOnlyList<IParaStrategy> _tools;

        public FlowRunner(IReadOnlyList<IParaStrategy> tools)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        }

        public FlowFailurePolicy OnFailure { get; set; } = FlowFailurePolicy.Stop;

        /// <summary> 是否为每步生成叠加层；无界面运行设为 false，只计算不绘制 </summary>
        public bool Render { get; set; } = true;

        /// <summary>
        /// 执行 [<paramref name="from"/>, <paramref name="to"/>] 区间内的工具（默认全部）。
        /// 从中间开始时，前面的工具不重新执行，它们上一轮的输出照常可用。
        /// 返回的结果拥有各步的叠加层，调用方用完要释放（或用 <see cref="FlowRunResult.TakeOverlay"/> 取走）。
        /// </summary>
        public FlowRunResult Run(HObject initialImage, int from = 0, int? to = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            int last = Math.Min(to ?? _tools.Count - 1, _tools.Count - 1);
            if (from < 0 || from > _tools.Count) throw new ArgumentOutOfRangeException(nameof(from));

            var watch = Stopwatch.StartNew();
            var steps = new List<FlowStepResult>();
            HObject current = CurrentImageBefore(from, initialImage);
            bool stopped = false;
            try
            {
                for (int i = from; i <= last; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var tool = _tools[i];
                    var context = new RunContext(current, Upstream(i), cancellation);
                    var overlay = Render ? new OverlayList() : null;
                    RunResult result;
                    try
                    {
                        result = tool.Run(context, overlay);
                    }
                    catch
                    {
                        overlay?.Dispose();
                        throw;
                    }

                    if (result.Status != RunStatus.Error && tool is IImageProducer producer && producer.Image.NotNull() && producer.Image.CountObj() > 0)
                        current = producer.Image;
                    steps.Add(new FlowStepResult(i, tool, result, overlay, current));
                    if (result.Status == RunStatus.Error)
                    {
                        Log.Warn(nameof(FlowRunner), $"{i}. {tool.Name} 失败: {result.Message}");
                        if (OnFailure == FlowFailurePolicy.Stop)
                        {
                            stopped = i < last;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // 取消 / 意外异常: 已完成各步的叠加层没有人接手, 在这里释放
                foreach (var step in steps) step.Dispose();
                throw;
            }
            watch.Stop();
            return new FlowRunResult(steps, stopped, watch.Elapsed, current);
        }

        /// <summary> 单步：只执行第 <paramref name="index"/> 个工具；返回的结果拥有本步的叠加层 </summary>
        public FlowStepResult RunStep(int index, HObject initialImage, CancellationToken cancellation = default(CancellationToken))
        {
            if (index < 0 || index >= _tools.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Run(initialImage, index, index, cancellation).Steps.Single();
        }

        /// <summary> 第 <paramref name="index"/> 个工具能看到的上游（它之前的全部工具） </summary>
        public IReadOnlyList<IParaStrategy> Upstream(int index) => _tools.Take(index).ToList();

        /// <summary>
        /// 运行前校验：每个来源引用的工具必须存在、排在前面、输出存在且类型匹配。
        /// </summary>
        public IReadOnlyList<FlowIssue> Validate()
        {
            var issues = new List<FlowIssue>();
            for (int i = 0; i < _tools.Count; i++)
            {
                if (!(_tools[i] is IParaBinding binding)) continue;
                foreach (var source in binding.DescribeParams().OfType<SourceParam>())
                {
                    var issue = Check(i, source);
                    if (issue != null) issues.Add(new FlowIssue(i, _tools[i], source.Label, issue));
                }
            }
            return issues;
        }

        private string Check(int index, SourceParam param)
        {
            var source = param.Value;
            if (source.IsLocal) return null;

            int at = -1;
            for (int j = 0; j < _tools.Count; j++)
            {
                if (_tools[j].Id == source.ToolId) { at = j; break; }
            }
            if (at < 0) return $"引用的工具不存在 ({source.Output})";
            if (at >= index) return $"引用的工具 '{_tools[at].Name}' 必须排在本工具之前";

            var output = _tools[at].FindOutput(source.Output);
            if (output == null) return $"'{_tools[at].Name}' 没有输出 '{source.Output}'";
            if (output.Type != param.SourceType) return $"'{_tools[at].Name}/{source.Output}' 的类型是 {output.Type}, 需要 {param.SourceType}";
            return null;
        }

        /// <summary> 从中间开始时，"当前图像"取前面最后一个图像工具上一轮的输出 </summary>
        private HObject CurrentImageBefore(int index, HObject initialImage)
        {
            for (int i = Math.Min(index, _tools.Count) - 1; i >= 0; i--)
            {
                if (_tools[i] is IImageProducer producer && producer.Image.NotNull() && producer.Image.CountObj() > 0)
                    return producer.Image;
            }
            return initialImage;
        }
    }
}
