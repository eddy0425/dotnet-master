using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System.Threading.Tasks;

namespace DotNet.HalconAlgo
{
    /// <summary> 边缘拟合类工具（拟合直线 / 圆弧中点）的公共参数 </summary>
    public abstract class EdgeFitPara : DisplayOptions
    {
        protected EdgeFitPara()
        {
            HoRect.Type = RectEnum.AffRect;
        }

        /// <summary> 图像来源 </summary>
        public SourceRef ImageIn { get; set; } = SourceRef.Local;

        /// <summary> 区域来源；本地取 <see cref="HoRect"/> </summary>
        public SourceRef RegionIn { get; set; } = SourceRef.Local;

        /// <summary> 跟随坐标 </summary>
        public SourceRef CoordIn { get; set; } = SourceRef.Local;

        /// <summary> 测量区域（同时决定测量方向与范围） </summary>
        public CvRegion HoRect { get; set; } = new CvRegion();

        /// <summary> 过渡方向 </summary>
        public Transition Transition { get; set; } = Transition.Positive;

        /// <summary> 每个测量矩形里取第几条边 </summary>
        public EdgeSelect ContourType { get; set; } = EdgeSelect.First;

        /// <summary> 滤波（measure_pos 的 Sigma；小于 0.4 时按 0.4 处理） </summary>
        public int Sigma { get; set; } = 1;

        /// <summary> 边缘幅值阈值：只接受幅值不低于它的边缘 </summary>
        public int Threshold { get; set; } = 60;

        /// <summary> 步距 </summary>
        public int StepPace { get; set; } = 10;

        /// <summary> 步宽 </summary>
        public int StepWidth { get; set; } = 5;

        /// <summary> 最大偏差（像素） </summary>
        public int MaxErr { get; set; } = 5;

        /// <summary> 是否裁剪筛选后的首尾点 </summary>
        public bool TrimEnds { get; set; } = true;

        /// <summary> 点大小 </summary>
        public int PointSize { get; set; } = 15;

        /// <summary> 显示查找区域 </summary>
        public bool DispRegion { get; set; } = true;

        /// <summary> 显示拟合点 </summary>
        public bool DispFixPoint { get; set; } = true;

        /// <summary> 显示测量矩形 </summary>
        public bool DispFixRegion { get; set; } = false;

        /// <summary> 显示结果 </summary>
        public bool DispResult { get; set; } = true;
    }

    /// <summary>
    /// 边缘拟合类工具的公共部分：参数声明、ROI 交互、取图 / 取区域 / 坐标跟随 / 边缘测量。
    /// 子类只写拟合本身与结果绘制。
    /// </summary>
    public abstract class EdgeFitStrategyBase<TPara> : ParaStrategyBase<TPara>, IRoiEditable
        where TPara : EdgeFitPara, new()
    {
        /// <summary> 子类在 <c>base.DeclareParams(p)</c> 之后追加自己的参数 </summary>
        protected override void DeclareParams(ParamBuilder p)
        {
            p.Tab(TabPageEnum.Parameter)
             .Source("图像来源", () => inPara.ImageIn, v => inPara.ImageIn = v, OutEnum.Image)
             .Source("区域来源", () => inPara.RegionIn, v => inPara.RegionIn = v, OutEnum.Region)
             .Choice("过渡方向", () => inPara.Transition, v => inPara.Transition = v,
                     Option.Of(Transition.Positive, "由黑到白"), Option.Of(Transition.Negative, "由白到黑"), Option.Of(Transition.All, "全部"))
             .Choice("选择", () => inPara.ContourType, v => inPara.ContourType = v,
                     Option.Of(EdgeSelect.First, "第一条边"), Option.Of(EdgeSelect.Second, "第二条边"),
                     Option.Of(EdgeSelect.Last, "最后一条"), Option.Of(EdgeSelect.All, "全部"))
             .Int("滤波", () => inPara.Sigma, v => inPara.Sigma = v, presets: new[] { 0, 1 }, min: 0)
             .Int("阈值", () => inPara.Threshold, v => inPara.Threshold = v, presets: new[] { 30, 50 }, min: 0, max: 255)
             .Int("步距", () => inPara.StepPace, v => inPara.StepPace = v, presets: new[] { 2, 5, 10 }, min: 1)
             .Int("步宽", () => inPara.StepWidth, v => inPara.StepWidth = v, presets: new[] { 2, 5, 10 }, min: 1)
             .Int("最大偏差", () => inPara.MaxErr, v => inPara.MaxErr = v, presets: new[] { 1, 3, 5, 10 }, min: 0)
             .Choice("裁剪首尾", () => inPara.TrimEnds, v => inPara.TrimEnds = v, Option.Of(false, "否"), Option.Of(true, "是"));
            p.Tab(TabPageEnum.Region)
             .Source("跟随坐标", () => inPara.CoordIn, v => inPara.CoordIn = v, OutEnum.Coord);
            p.Tab(TabPageEnum.Display)
             .Flag("查找区域", () => inPara.DispRegion, v => inPara.DispRegion = v)
             .Flag("拟合区域", () => inPara.DispFixRegion, v => inPara.DispFixRegion = v)
             .Flag("拟合点", () => inPara.DispFixPoint, v => inPara.DispFixPoint = v)
             .Flag("显示结果", () => inPara.DispResult, v => inPara.DispResult = v)
             .Int("点大小", () => inPara.PointSize, v => inPara.PointSize = v, presets: new[] { 5, 15, 30 }, min: 1);
            p.Tab(TabPageEnum.Parameter);
        }

        /// <summary>
        /// 取图、取查找区域、按跟随坐标搬移测量几何，然后逐步测量边缘点。
        /// </summary>
        /// <param name="searchRegion">本轮实际使用的查找区域（已跟随），归调用方所有。</param>
        protected EdgeMeasureResult Measure(RunContext context, out HObject searchRegion)
        {
            HObject image = context.ResolveImage(inPara.ImageIn);
            HObject region = context.ResolveRegion(inPara.RegionIn, inPara.HoRect);
            CoordFollow follow = context.ResolveCoord(inPara.CoordIn);

            // 本地测量几何独立跟随，不能因使用上游区域而跳过；上游区域已在当前图像坐标系，只变换本地配置区域
            Point2d center = follow.TransPoint(inPara.HoRect.Center);
            Angle phi = follow.TransAngle(Angle.FromRadians(inPara.HoRect.Phi.D));
            searchRegion = inPara.RegionIn.IsLocal ? follow.TransRegion(region) : region.CopyObj(1, -1);

            // 尺寸取自待测图像本身，而不是显示窗口：图像来源是上游时，两者可以完全无关
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            var setup = new EdgeMeasureSetup(
                center, phi,
                inPara.HoRect.Width / 2, inPara.HoRect.Height / 2,
                inPara.StepPace, inPara.StepWidth,
                inPara.Sigma, inPara.Threshold,
                inPara.Transition.ToHalcon(), inPara.ContourType.ToHalcon(),
                width.I, height.I);

            HOperatorSet.ReduceDomain(image, searchRegion, out HObject reduced);
            try
            {
                return EdgeMeasurePipeline.Run(reduced, setup);
            }
            finally
            {
                reduced.Dispose();
            }
        }

        public Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI) => RoiEditing.DrawAsync(host, inPara.HoRect, type, newROI);

        public void DispROI(IRoiHost host) => host.SetRectPara(inPara.HoRect);

        protected override void Dispose(bool disposing)
        {
            inPara.HoRect?.Dispose();
            base.Dispose(disposing);
        }
    }
}
