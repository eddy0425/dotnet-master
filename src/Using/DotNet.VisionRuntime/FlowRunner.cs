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

    /// <summary> 流程中一个工具的执行结果 </summary>
    public sealed class FlowStepResult
    {
        public FlowStepResult(int index, IParaStrategy tool, RunResult result)
        {
            Index = index;
            Tool = tool;
            Result = result;
        }

        public int Index { get; }
        public IParaStrategy Tool { get; }
        public RunResult Result { get; }

        public override string ToString() => $"{Index}. {Tool?.Name}: {Result}";
    }

    /// <summary> 一次流程运行的汇总 </summary>
    public sealed class FlowRunResult
    {
        internal FlowRunResult(IReadOnlyList<FlowStepResult> steps, bool stopped, TimeSpan elapsed)
        {
            Steps = steps;
            Stopped = stopped;
            Elapsed = elapsed;
        }

        public IReadOnlyList<FlowStepResult> Steps { get; }

        /// <summary> 是否因为失败而提前停止 </summary>
        public bool Stopped { get; }

        public TimeSpan Elapsed { get; }

        /// <summary> 全部步骤都成功（没有警告、没有失败） </summary>
        public bool AllOk => Steps.All(s => s.Result.Status == RunStatus.Ok);

        /// <summary> 第一个失败的步骤；没有失败时为 null </summary>
        public FlowStepResult FirstError => Steps.FirstOrDefault(s => s.Result.Status == RunStatus.Error);
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

        /// <summary>
        /// 执行 [<paramref name="from"/>, <paramref name="to"/>] 区间内的工具（默认全部）。
        /// 从中间开始时，前面的工具不重新执行，它们上一轮的输出照常可用。
        /// </summary>
        public FlowRunResult Run(HObject initialImage, IHDisplay display, int from = 0, int? to = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            int last = Math.Min(to ?? _tools.Count - 1, _tools.Count - 1);
            if (from < 0 || from > _tools.Count) throw new ArgumentOutOfRangeException(nameof(from));

            var watch = Stopwatch.StartNew();
            var steps = new List<FlowStepResult>();
            HObject current = CurrentImageBefore(from, initialImage);
            bool stopped = false;
            for (int i = from; i <= last; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                var tool = _tools[i];
                var context = new RunContext(current, Upstream(i), cancellation);
                var result = tool.Run(context, display);
                steps.Add(new FlowStepResult(i, tool, result));

                if (result.Status != RunStatus.Error && tool is IImageProducer producer && producer.Image.NotNull() && producer.Image.CountObj() > 0)
                    current = producer.Image;
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
            watch.Stop();
            return new FlowRunResult(steps, stopped, watch.Elapsed);
        }

        /// <summary> 单步：只执行第 <paramref name="index"/> 个工具 </summary>
        public FlowStepResult RunStep(int index, HObject initialImage, IHDisplay display, CancellationToken cancellation = default(CancellationToken))
        {
            if (index < 0 || index >= _tools.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return Run(initialImage, display, index, index, cancellation).Steps.Single();
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
