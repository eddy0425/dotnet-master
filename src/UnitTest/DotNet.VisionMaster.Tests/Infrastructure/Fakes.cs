using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    internal sealed class FakePara : DisplayOptions
    {
        public SourceRef Image { get; set; } = SourceRef.Local;
        public SourceRef Coord { get; set; } = SourceRef.Local;
        public bool Flag { get; set; }
    }

    /// <summary>
    /// 可配置的算法：记录 ParaForm / MainForm 对它发起的调用，绘制入口返回可由测试控制完成时机的任务。
    /// 默认声明一个图像来源（参数页）、一个跟随坐标（区域页）和一个开关（显示页）。
    /// </summary>
    [Algo("test.fake", "fake")]
    internal class FakeStrategy : ParaStrategyBase<FakePara>, IRoiEditable, ITemplateEditable
    {
        public FakeStrategy(string name = "fake")
        {
            Name = name;
        }

        /// <summary> 输出声明 </summary>
        public Action<OutputBuilder> Outs;

        /// <summary>DrawROIAsync / SetTemplateAsync 的调用记录：(类型, 是否新建)。</summary>
        public readonly List<Tuple<RectEnum, bool>> RoiDraws = new List<Tuple<RectEnum, bool>>();
        public readonly List<Tuple<RectEnum, bool>> TemplateDraws = new List<Tuple<RectEnum, bool>>();

        /// <summary> 宿主通知过的参数改动 </summary>
        public readonly List<string> ChangedLabels = new List<string>();

        /// <summary>绘制入口 await 的任务；测试调用 <see cref="FinishDraw"/> 才算绘制结束。</summary>
        private TaskCompletionSource<bool> _pending = new TaskCompletionSource<bool>();

        public void FinishDraw()
        {
            var done = _pending;
            _pending = new TaskCompletionSource<bool>();
            done.SetResult(true);
        }

        /// <summary>非 null 时绘制入口直接以它失败（模拟 HALCON 报错、绘制被取消等）。</summary>
        public Exception DrawError;

        /// <summary>非 null 时执行抛出它（基类转成失败结果）。</summary>
        public Exception RunError;

        /// <summary> 模板视图；默认"尚未建模板" </summary>
        public TemplateView View = new TemplateView(string.Empty, null, null, null);

        public int Runs;
        public int DispRoiCount;

        /// <summary> 执行时先睡这么久（模拟耗时的 HALCON 算子） </summary>
        public int RunDelayMs;

        /// <summary> 最近一次执行所在的线程 </summary>
        public int ExecuteThreadId;
        public bool Disposed => IsDisposed;

        protected override void DeclareParams(ParamBuilder p)
        {
            p.Page(Pages.Parameter).Source("图像来源", () => inPara.Image, v => inPara.Image = v, OutEnum.Image);
            p.Page(Pages.Region).Source("跟随坐标", () => inPara.Coord, v => inPara.Coord = v, OutEnum.Coord);
            p.Page(Pages.Display).Flag("开关", () => inPara.Flag, v => inPara.Flag = v);
        }

        protected override void OnParamsChanged(IReadOnlyList<ParamItem> changed)
        {
            foreach (var item in changed) ChangedLabels.Add(item.Label);
        }

        protected override void DeclareOutputs(OutputBuilder o) => Outs?.Invoke(o);

        protected override void ResetOutputs() { }

        protected override RunResult Execute(RunContext context)
        {
            ExecuteThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (RunDelayMs > 0) System.Threading.Thread.Sleep(RunDelayMs);
            System.Threading.Interlocked.Increment(ref Runs);
            if (RunError != null) throw RunError;
            return RunResult.Ok("完成");
        }

        public Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI)
        {
            RoiDraws.Add(Tuple.Create(type, newROI));
            return DrawError != null ? Faulted() : _pending.Task;
        }

        public Task SetTemplateAsync(IRoiHost host, RectEnum type, bool newModel)
        {
            TemplateDraws.Add(Tuple.Create(type, newModel));
            return DrawError != null ? Faulted() : _pending.Task;
        }

        public TemplateView GetTemplateView() => View;

        public void DispROI(IRoiHost host) => DispRoiCount++;

        private Task Faulted()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(DrawError);
            return tcs.Task;
        }
    }

    /// <summary> 声明了 <c>DefaultRoi = AffRect</c> 的算法（拟合类的写法） </summary>
    [Algo("test.fake-aff", "fake-aff", DefaultRoi = RectEnum.AffRect)]
    internal sealed class FakeAffStrategy : FakeStrategy
    {
        public FakeAffStrategy(string name = "fake-aff") : base(name) { }
    }

    internal static class ToolRefs
    {
        /// <summary> 某个工具某个输出的引用 </summary>
        public static SourceRef Ref(this IAlgoStrategy tool, string output) => SourceRef.To(tool, output);
    }

    internal static class SamplePlugin
    {
        /// <summary> 样例插件的编译输出（只引用 Drawing + HalconCore） </summary>
        public static string Dll()
        {
            string config = Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
            string path = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "DotNet.SamplePlugin", "bin", config, "DotNet.SamplePlugin.dll"));
            Assert.IsTrue(File.Exists(path), "前提：样例插件已编译 " + path);
            return path;
        }
    }
}
