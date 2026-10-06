using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System.Collections.Generic;
using System.Linq;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 区域合并: 把多路上游区域输出并成一个区域。
    /// </summary>
    /// <remarks>
    /// 没有自己的配置 ROI，形状完全来自 <see cref="RegionMerge.RegionSources"/>，因此不实现 <see cref="IRoiEditable"/>。
    /// <para>
    /// 对外的"坐标系"原点是合并结果的重心。它的示教模板点只能隐式示教：首轮全部来源有效时记一次；
    /// 输入来源或跟随坐标一改就作废，下一轮重新示教。
    /// </para>
    /// </remarks>
    [Algo("region.merge", "区域合并", Group = "区域", Order = 220)]
    public class MergeRegionStrategy : ParaStrategyBase<RegionMerge>
    {
        /// <summary> 输入区域的槽位数 </summary>
        public const int SourceCount = 6;

        private int _merged;
        private int _missing;

        public MergeRegionStrategy()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Region = empty;
        }

        /// <summary> 本轮合并结果（已跟随）；失败时为空对象 </summary>
        public HObject Region { get; private set; }

        /// <summary> 本轮坐标系：原点为合并结果的重心 </summary>
        public CvCoord Coord { get; private set; }

        protected override void DeclareParams(ParamBuilder p)
        {
            var sources = Sources();
            p.Tab(TabPageEnum.Parameter)
             .Source("跟随坐标", () => inPara.CoordIn, v => inPara.CoordIn = v, OutEnum.Coord);
            for (int i = 0; i < sources.Length; i++)
            {
                int slot = i;
                p.Source($"输入区域{slot}", () => Sources()[slot], v => Sources()[slot] = v, OutEnum.Region);
            }
            p.Tab(TabPageEnum.Display)
             .Flag("查找区域", () => inPara.DispRegion, v => inPara.DispRegion = v);
        }

        /// <summary> 来源或跟随坐标一改，旧的示教原点就失效；只改显示选项时保留 </summary>
        protected override void OnParamsChanged(IReadOnlyList<ParamItem> changed)
        {
            if (changed.Any(item => item.Kind == ParamKind.Source)) inPara.TmplPoint = null;
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            // 未示教 → 模板点为 null, 下游跟随时报"未示教" (响亮失败好过静默算错)
            o.Coord("坐标系", () => Coord, () => inPara.TmplPoint)
             .Region("区域", () => Region);
        }

        protected override void ResetOutputs()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            var old = Region;
            Region = empty;
            old?.Dispose();
            Coord = new CvCoord();
            _merged = _missing = 0;
        }

        protected override RunResult Execute(RunContext context)
        {
            HOperatorSet.GenEmptyObj(out HObject collected);
            HObject merged = null;
            try
            {
                foreach (var source in Sources())
                {
                    if (source.IsLocal) continue;   // 空槽位

                    // 解析不到 / 空句柄 / 没有像素都按"来源无效"计数, 不中断整轮合并:
                    // 全空时 area_center 的 (0,0) 会被当成重心发布, 部分为空时残缺重心会被示教成模板点。
                    if (!context.TryResolveRegion(source, out HObject region)) { _missing++; continue; }
                    HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
                    if (area.TupleSum().D <= 0) { _missing++; continue; }

                    HOperatorSet.ConcatObj(collected, region, out HObject concat);
                    collected.Dispose();
                    collected = concat;
                    _merged++;
                }

                if (_merged == 0) return RunResult.Fail("无有效输入区域");

                HOperatorSet.Union1(collected, out merged);
                CoordFollow follow = context.ResolveCoord(inPara.CoordIn);
                HObject result = follow.TransRegion(merged);
                Region.Dispose();
                Region = result;

                HOperatorSet.AreaCenter(Region, out _, out HTuple row, out HTuple column);
                // 角度只有跟随时才有意义: 本工具只拿到上游的区域句柄, 推断不出来源的朝向
                Coord = new CvCoord(new Point2d(column, row), follow.IsActive ? follow.Current.Angle : Angle.Zero);

                // 全部来源有效的首轮结果才算示教态; 重心取"变换前"的 merged —— 跟随时 Region 已被搬到
                // 当前位姿, 拿它当零角参考会让下游平移量恒为 0
                if (_missing == 0 && !inPara.TmplPoint.HasValue)
                {
                    HOperatorSet.AreaCenter(merged, out _, out HTuple tmplRow, out HTuple tmplCol);
                    inPara.TmplPoint = new Point2d(tmplCol, tmplRow);
                }

                string message = $"合并数量:{_merged}" + (_missing > 0 ? $" 无效来源:{_missing}" : string.Empty)
                               + $" 中心:({Coord.X:F2},{Coord.Y:F2})";
                // 有无效来源时重心是残缺的 (也因此不会示教), 降级结果不该长得跟成功一样
                return _missing > 0 ? RunResult.Warn(message) : RunResult.Ok(message);
            }
            finally
            {
                collected.Dispose();
                merged?.Dispose();
            }
        }

        protected override void Render(IHDisplay display, RunResult result)
        {
            if (inPara.DispRegion && Region.IsUsableRegion()) display.Disp(Region, DrawStyle.Of(HColor.Blue));
        }

        /// <summary>
        /// 输入区域槽位。旧配置里 <c>"RegionSources": null</c> 或长度不足时补齐，参数页与执行都不会因此出错。
        /// </summary>
        private SourceRef[] Sources()
        {
            var sources = inPara.RegionSources;
            if (sources == null || sources.Length < SourceCount)
            {
                var padded = new SourceRef[SourceCount];
                if (sources != null) System.Array.Copy(sources, padded, sources.Length);
                inPara.RegionSources = sources = padded;
            }
            return sources;
        }

        /// <summary> 工具页关闭：只清运行结果，配置与示教原点保留 </summary>
        public override void Close(IRoiHost host)
        {
            if (!IsDisposed) ResetOutputs();
        }

        protected override void Dispose(bool disposing)
        {
            Region?.Dispose();
            base.Dispose(disposing);
        }
    }

    public class RegionMerge : DisplayOptions
    {
        /// <summary> 跟随坐标 </summary>
        public SourceRef CoordIn { get; set; } = SourceRef.Local;

        /// <summary> 区域来源；本地表示空槽位 </summary>
        public SourceRef[] RegionSources { get; set; } = new SourceRef[MergeRegionStrategy.SourceCount];

        /// <summary>
        /// 示教态合并重心（变换前）：首轮全部来源有效时记录，作为下游跟随的零角参考原点。
        /// 为 null 表示尚未示教。
        /// </summary>
        public Point2d? TmplPoint { get; set; }

        /// <summary> 显示区域 </summary>
        public bool DispRegion { get; set; } = true;
    }
}
