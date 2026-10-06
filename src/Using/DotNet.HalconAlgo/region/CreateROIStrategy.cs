using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System.Threading.Tasks;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 创建 ROI：把配置的区域（可随上游坐标系跟随）作为"区域"与"坐标系"输出给下游。
    /// </summary>
    [Algo("region.create-roi", "创建ROI", Group = "区域", Order = 210)]
    public class CreateROIStrategy : ParaStrategyBase<CreateROI>, IRoiEditable
    {
        public CreateROIStrategy()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Region = empty;
        }

        /// <summary> 本轮输出区域（已跟随）；失败时为空对象 </summary>
        public HObject Region { get; private set; }

        /// <summary> 本轮输出坐标系：原点为 ROI 中心（已跟随），角度取跟随坐标系的角度 </summary>
        public CvCoord Coord { get; private set; }

        protected override void DeclareParams(ParamBuilder p)
        {
            p.Tab(TabPageEnum.Region)
             .Source("跟随坐标", () => inPara.CoordIn, v => inPara.CoordIn = v, OutEnum.Coord);
            p.Tab(TabPageEnum.Display)
             .Flag("查找区域", () => inPara.DispRegion, v => inPara.DispRegion = v);
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            // 模板点是配置态 (零角) 的 ROI 中心, 下游跟随时用它与本轮坐标系算刚体变换; 未绘制 ROI 时没有模板点
            o.Coord("坐标系", () => Coord, () => inPara.HoRect.HoRegion.IsUsableRegion() ? inPara.HoRect.Center : (Point2d?)null)
             // 只给区域句柄: CvRegion 的外接框 / 类型不随本轮结果更新, 对外只交付确实有效的那部分
             .Region("区域", () => Region);
        }

        protected override void ResetOutputs()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            var old = Region;
            Region = empty;
            old?.Dispose();
            Coord = new CvCoord();
        }

        protected override RunResult Execute(RunContext context)
        {
            var roi = inPara.HoRect;
            HObject local = context.ResolveRegion(SourceRef.Local, roi);
            CoordFollow follow = context.ResolveCoord(inPara.CoordIn);

            // 先算完再换上: 变换抛异常时输出仍是 ResetOutputs 放进去的空对象
            HObject region = follow.TransRegion(local);
            Region.Dispose();
            Region = region;
            // 亚像素刚体变换, 不取区域光栅化重心
            Coord = new CvCoord(follow.TransPoint(roi.Center), follow.IsActive ? follow.Current.Angle : Angle.Zero);

            return RunResult.Ok($"中心:({Coord.X:F2},{Coord.Y:F2}) 宽:{roi.Width:F0} 高:{roi.Height:F0}");
        }

        protected override void Render(IHDisplay display, RunResult result)
        {
            if (inPara.DispRegion && Region.IsUsableRegion()) display.Disp(Region, DrawStyle.Of(HColor.Blue));
        }

        public Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI) => RoiEditing.DrawAsync(host, inPara.HoRect, type, newROI);

        public void DispROI(IRoiHost host) => host.SetRectPara(inPara.HoRect);

        /// <summary> 工具页关闭：只清运行结果，重新打开仍可复用配置 ROI </summary>
        public override void Close(IRoiHost host)
        {
            if (!IsDisposed) ResetOutputs();
        }

        protected override void Dispose(bool disposing)
        {
            Region?.Dispose();
            inPara.HoRect?.Dispose();
            base.Dispose(disposing);
        }
    }

    public class CreateROI : DisplayOptions
    {
        /// <summary> 跟随坐标 </summary>
        public SourceRef CoordIn { get; set; } = SourceRef.Local;

        /// <summary> 配置 ROI </summary>
        public CvRegion HoRect { get; set; } = new CvRegion();

        /// <summary> 显示区域 </summary>
        public bool DispRegion { get; set; } = true;
    }
}
