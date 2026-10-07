using DotNet.HalconCore;
using HalconDotNet;
using Newtonsoft.Json.Linq;

namespace DotNet.SamplePlugin
{
    /// <summary>
    /// 外置插件样例：统计上游区域的面积。整个算法就是这一个文件 —— 一个策略类 + 一个参数类，
    /// 只依赖 DotNet.HalconCore（与 halcondotnet），不认识宿主里的任何控件。
    /// <para>
    /// 也是参数迁移的样例：1 版的参数类把最小面积存成 <c>Min</c>，2 版改名为 <c>MinArea</c> ——
    /// 版本号写在 <see cref="AlgoAttribute.ParaVersion"/> 上，旧方案读入时由 <see cref="MigratePara"/> 改名，不会被静默读成默认值。
    /// </para>
    /// </summary>
    [Algo("sample.region-area", "区域面积", Group = "示例", Order = 10, ParaVersion = 2)]
    public class RegionAreaStrategy : ParaStrategyBase<RegionAreaPara>
    {
        /// <summary> 本轮面积；失败时为 0 </summary>
        public double Area { get; private set; }

        protected override void DeclareParams(ParamBuilder p)
        {
            p.Page(Pages.Parameter)
             .Source("区域来源", () => inPara.RegionIn, v => inPara.RegionIn = v, OutEnum.Region)
             .Int("最小面积", () => inPara.MinArea, v => inPara.MinArea = v, presets: new[] { 0, 100 }, min: 0);
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            o.Number("面积", () => Area);
        }

        protected override void ResetOutputs() => Area = 0;

        protected override void MigratePara(JObject para, int fromVersion)
        {
            // 1 → 2: Min 改名为 MinArea
            if (fromVersion < 2 && para["Min"] != null)
            {
                para["MinArea"] = para["Min"];
                para.Remove("Min");
            }
        }

        protected override RunResult Execute(RunContext context)
        {
            HObject region = context.ResolveRegion(inPara.RegionIn, null);
            HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
            Area = area.TupleSum().D;
            string message = $"面积:{Area:F0}";
            return Area < inPara.MinArea ? RunResult.Warn(message + $" 小于 {inPara.MinArea}") : RunResult.Ok(message);
        }
    }

    public class RegionAreaPara : DisplayOptions
    {
        public SourceRef RegionIn { get; set; } = SourceRef.Local;

        public int MinArea { get; set; }
    }
}
