using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DotNet.HalconAlgo
{
    /// <summary> 四种匹配的公共参数 </summary>
    public abstract class MatchParaBase : DisplayOptions
    {
        /// <summary> 图像来源 </summary>
        public SourceRef ImageIn { get; set; } = SourceRef.Local;

        /// <summary> 区域来源；本地取 <see cref="HoRect"/> </summary>
        public SourceRef RegionIn { get; set; } = SourceRef.Local;

        /// <summary> 跟随坐标：本地查找区域随它搬到当前工件位姿 </summary>
        public SourceRef CoordIn { get; set; } = SourceRef.Local;

        /// <summary> 查找区域；可以是多个对象（逐个查找后取全局最佳） </summary>
        public CvRegion HoRect { get; set; } = new CvRegion();

        /// <summary> 模板区域 </summary>
        public CvRegion ModeRect { get; set; } = new CvRegion();

        /// <summary> 示教模板点（模板区域重心的匹配位置）；尚未建模板时为 null </summary>
        public Point2d? TmplPoint { get; set; }

        /// <summary> 起始角度（度） </summary>
        public double AngleStart { get; set; } = -90;

        /// <summary> 角度范围（度） </summary>
        public double AngleExtent { get; set; } = 180;

        /// <summary> 最低得分 </summary>
        public double MinScore { get; set; } = 0.6;

        /// <summary> 匹配数量；0 表示不限 </summary>
        public int NumMatches { get; set; } = 1;

        /// <summary> 最大重叠率 </summary>
        public double MaxOverlap { get; set; } = 0.5;

        /// <summary> 金字塔层数；0 表示由 HALCON 自动确定 </summary>
        public int NumLevels { get; set; } = 0;

        /// <summary> 显示查找区域 </summary>
        public bool DispRegion { get; set; } = true;

        /// <summary> 显示匹配轮廓 </summary>
        public bool DispContour { get; set; } = true;

        /// <summary> 显示匹配点 </summary>
        public bool DispPoint { get; set; } = true;
    }

    /// <summary>
    /// 四种匹配（形状 / 灰度 / 缩放 / 通用）的公共部分：模板事务、查找循环、结果与输出、模型文件与句柄生命周期。
    /// 子类只实现创建、查找、清除、读写模型这几个钩子，以及各自特有参数的声明。
    /// </summary>
    public abstract class MatchStrategyBase<TPara> : ParaStrategyBase<TPara>, IRoiEditable, ITemplateEditable
        where TPara : MatchParaBase, new()
    {
        /// <summary> 一次命中：位姿 + 已搬到命中位姿的模板轮廓（归调用方所有） </summary>
        protected sealed class MatchHit
        {
            public MatchHit(ModelResult result, HObject contour)
            {
                Result = result;
                Contour = contour;
            }

            public ModelResult Result { get; }
            public HObject Contour { get; }
        }

        private HObject _searchRegion;                       // 本轮实际查找区域 (已跟随), 仅用于显示
        private readonly List<MatchHit> _hits = new List<MatchHit>();   // 本轮全部命中, 仅用于显示

        protected MatchStrategyBase()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Contour = empty;
        }

        /// <summary> 当前模型句柄；尚未建模板时为 null </summary>
        public HTuple ModelID { get; internal set; }

        public bool HasModel => ModelID != null && ModelID.Length > 0;

        /// <summary> 本轮匹配结果，按得分降序；<c>Results[0]</c> 是全局最佳 </summary>
        public IReadOnlyList<ModelResult> Results { get; private set; } = new ModelResult[0];

        /// <summary> 全局最佳匹配的坐标系；没有匹配时为默认值 </summary>
        public CvCoord Coord { get; private set; }

        /// <summary> 全局最佳匹配的模板轮廓（与 <see cref="Coord"/> 配对显示）；没有匹配时为空对象 </summary>
        public HObject Contour { get; private set; }

        /// <summary> 模板小图路径（随数据目录走，不存进参数：方案换目录后不会留下失效的旧路径） </summary>
        public string ModelImagePath => Path.Combine(DataDir, "matching.bmp");

        /// <summary> 模型文件路径 </summary>
        protected string ModelFilePath => Path.Combine(DataDir, "model" + ModelFileExtension);

        #region 子类钩子

        /// <summary> 模型文件扩展名（含点） </summary>
        protected abstract string ModelFileExtension { get; }

        /// <summary> 用模板图像训练一个新模型 </summary>
        protected abstract HTuple CreateModel(HObject templateImage);

        /// <summary> 在 <paramref name="image"/> 的定义域里查找，最多 <paramref name="numMatches"/> 个（0 = 不限） </summary>
        protected abstract List<MatchHit> FindModel(HObject image, HTuple modelId, int numMatches);

        protected abstract void ClearModel(HTuple modelId);

        protected abstract void WriteModel(HTuple modelId, string path);

        protected abstract HTuple ReadModel(string path);

        /// <summary> 结果文本里在"角度范围"之前追加的部分（例如缩放范围），带结尾空格 </summary>
        protected virtual string SummaryExtra => string.Empty;

        #endregion

        #region 参数 / 输出

        protected override void DeclareParams(ParamBuilder p)
        {
            p.Page(Pages.Parameter)
             .Source("图像来源", () => inPara.ImageIn, v => inPara.ImageIn = v, OutEnum.Image)
             .Source("区域来源", () => inPara.RegionIn, v => inPara.RegionIn = v, OutEnum.Region)
             .Double("起始角度", () => inPara.AngleStart, v => inPara.AngleStart = v, presets: new double[] { -90, -45 }, min: -360, max: 360)
             .Double("增量角度", () => inPara.AngleExtent, v => inPara.AngleExtent = v, presets: new double[] { 90, 180 }, min: 0, max: 360)
             .Double("最大重叠率", () => inPara.MaxOverlap, v => inPara.MaxOverlap = v, presets: new[] { 0, 0.3, 0.5 }, min: 0, max: 1)
             .Choice("匹配数量", () => inPara.NumMatches, v => inPara.NumMatches = v,
                     Option.Of(1, "1"), Option.Of(2, "2"), Option.Of(3, "3"), Option.Of(0, "多个"))
             .Double("得分", () => inPara.MinScore, v => inPara.MinScore = v, presets: new[] { 0.5, 0.7 }, min: 0, max: 1)
             .Int("金字塔", () => inPara.NumLevels, v => inPara.NumLevels = v, presets: new[] { 0, 2 }, min: 0);
            p.Page(Pages.Region)
             .Source("跟随坐标", () => inPara.CoordIn, v => inPara.CoordIn = v, OutEnum.Coord);
            p.Page(Pages.Display)
             .Group(DisplayGroup)
             .Flag("查找区域", () => inPara.DispRegion, v => inPara.DispRegion = v)
             .Flag("显示轮廓", () => inPara.DispContour, v => inPara.DispContour = v)
             .Flag("显示点", () => inPara.DispPoint, v => inPara.DispPoint = v);
            p.Page(Pages.Parameter);
        }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            // 模板点随模板一起示教; 未建模板时为 null, 下游跟随时报"未示教"
            o.Coord("坐标系", () => Coord, () => inPara.TmplPoint);
        }

        #endregion

        #region 执行

        /// <summary>
        /// 复位本轮输出：结果、坐标系，以及与坐标系配对显示的轮廓
        /// （"编辑模板"会把两者一起取用，不清的话失败轮次会把旧轮廓配上零坐标系）。
        /// </summary>
        protected override void ResetOutputs()
        {
            Results = new ModelResult[0];
            Coord = new CvCoord();
            ReplaceContour(null);
        }

        protected override RunResult Execute(RunContext context)
        {
            ClearRenderData();

            // 未建模板属于可恢复的配置问题; 不拦的话空的 ModelID 会报出与真实原因无关的 HALCON 参数错误
            if (!HasModel) return RunResult.Fail("未建立模板，无法执行匹配！");

            HObject image = context.ResolveImage(inPara.ImageIn);
            HObject region = context.ResolveRegion(inPara.RegionIn, inPara.HoRect);
            // 跟随坐标只搬本地配置区域; 上游区域已在当前图像坐标系
            CoordFollow follow = context.ResolveCoord(inPara.CoordIn);
            _searchRegion = inPara.RegionIn.IsLocal ? follow.TransRegion(region) : region.CopyObj(1, -1);

            int count = _searchRegion.CountObj();
            for (int j = 1; j <= count; j++)
            {
                context.Cancellation.ThrowIfCancellationRequested();
                HOperatorSet.SelectObj(_searchRegion, out HObject one, j);
                HObject reduced = null;
                try
                {
                    HOperatorSet.ReduceDomain(image, one, out reduced);
                    _hits.AddRange(FindModel(reduced, ModelID, inPara.NumMatches));
                }
                finally
                {
                    reduced?.Dispose();
                    one.Dispose();
                }
            }

            // 单个 ROI 内的结果已按得分降序, 多 ROI 时要跨 ROI 稳定排序, 让 Results[0] 就是全局最佳:
            // 对外坐标系、"最佳得分"、编辑模板都取它, 轮廓也只留全局最佳那一个
            var ordered = _hits.OrderByDescending(h => h.Result.Score).ToList();
            Results = ordered.Select(h => h.Result).ToArray();
            string range = $"{SummaryExtra}角度范围:[{inPara.AngleStart}°,{inPara.AngleStart + inPara.AngleExtent}°]";
            if (ordered.Count == 0)
                return RunResult.Fail($"数量:0 {range}");

            Coord = ordered[0].Result.Coord;
            ReplaceContour(ordered[0].Contour.CopyObj(1, -1));
            return RunResult.Ok($"数量:{ordered.Count} 最佳得分:{ordered[0].Result.Score:F3} {range}");
        }

        /// <summary> 写完叠加层（它复制句柄）立即释放只供绘制的数据，不留到下一轮 </summary>
        protected override void Render(IOverlay overlay, RunResult result)
        {
            if (inPara.DispRegion && _searchRegion.IsUsableRegion())
                overlay.Add(_searchRegion, DrawStyle.Of(HColor.Blue));
            foreach (var hit in _hits)
            {
                if (inPara.DispContour) overlay.Add(hit.Contour, DrawStyle.Of(HColor.Green));
                if (inPara.DispPoint) overlay.Add(hit.Result.Coord, DrawStyle.Of(HColor.Red));
            }
            ClearRenderData();
        }

        private void ReplaceContour(HObject contour)
        {
            var old = Contour;
            if (contour == null) HOperatorSet.GenEmptyObj(out contour);
            Contour = contour;
            old?.Dispose();
        }

        private void ClearRenderData()
        {
            _searchRegion?.Dispose();
            _searchRegion = null;
            foreach (var hit in _hits) hit.Contour?.Dispose();
            _hits.Clear();
        }

        #endregion

        #region ROI / 模板

        public Task DrawROIAsync(IRoiHost host, RectEnum type, bool newROI) => RoiEditing.DrawAsync(host, inPara.HoRect, type, newROI);

        public void DispROI(IRoiHost host) => host.SetModelPara(inPara.HoRect.HoRegion, Contour, Coord);

        public TemplateView GetTemplateView()
            => new TemplateView(File.Exists(ModelImagePath) ? ModelImagePath : string.Empty,
                                inPara.ModeRect?.HoRegion, Contour, Results.Count > 0 ? Results[0] : (ModelResult?)null);

        /// <summary>
        /// 框选模板区域并重建模板。事务式：快照 → 绘制 → 训练 → 试匹配 → 落盘 → 提交；
        /// 走不到"提交"（取消、试匹配失败、任何异常）时模板区域换回快照，旧模型、旧示教点、旧文件原样保留。
        /// </summary>
        public async Task SetTemplateAsync(IRoiHost host, RectEnum type, bool newModel)
        {
            HObject reduced = null;
            HTuple modelId = null;
            MatchHit trial = null;
            // 绘制会就地改写 ModeRect (Type / 几何 / 区域), 走不到提交就整体换回快照
            CvRegion snapshot = inPara.ModeRect.Clone();

            try
            {
                inPara.ModeRect.Type = type;   // 必须在绘制前写入: 绘制按它分发图元
                bool confirmed = newModel
                    ? await host.DrawRegionAsync(inPara.ModeRect)
                    : await host.DrawRegionModAsync(inPara.ModeRect);

                // 取消 / 超时必须直接返回: 继续往下会按"旧几何 + 新参数"重建一份用户没有要求的模板
                if (!confirmed)
                {
                    RestoreModeRect(ref snapshot);
                    host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));
                    return;
                }

                var image = host.Display.HoImage.RequireImage(Name);
                HOperatorSet.ReduceDomain(image, inPara.ModeRect.HoRegion, out reduced);
                modelId = CreateModel(reduced);

                // 先用新模板试匹配 (只搜模板区域), 确认可用后再替换: 先替换的话, 试匹配失败时旧模板已被释放,
                // 新模板却配着旧模板示教出的模板点, 下游跟随会静默偏移
                var hits = FindModel(reduced, modelId, 1);
                trial = hits.FirstOrDefault();
                foreach (var extra in hits.Skip(1)) extra.Contour?.Dispose();
                if (trial == null)
                {
                    RestoreModeRect(ref snapshot);
                    host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));
                    host.Display.DispText("新建模板失败！", new Point2d(10, 10), DrawStyle.Of(HColor.Red));
                    return;
                }

                // 文件先落盘再提交: 落盘失败时新模型在 finally 里丢弃, 旧的一套 (含旧文件) 原样保留
                string imagePath = ModelImagePath;
                ModelImage.Save(image, reduced, imagePath);
                ModelImage.Stage(ModelFilePath, staged => WriteModel(modelId, staged));

                // 提交: 以下只做赋值与释放旧句柄, 模型 / 路径 / 结果 / 示教原点一起换
                if (HasModel) ClearModel(ModelID);
                ModelID = modelId;
                modelId = null;
                Results = new[] { trial.Result };
                Coord = trial.Result.Coord;
                ReplaceContour(trial.Contour);
                trial = null;
                inPara.TmplPoint = new Point2d(Coord.X, Coord.Y);
                snapshot.Dispose();
                snapshot = null;

                host.SetModelPara(inPara.HoRect.HoRegion, Contour, Coord);
                host.Display.Disp(inPara.ModeRect, DrawStyle.Of(HColor.Orange));
                host.DrawDone(imagePath, inPara.ModeRect.HoRegion, Contour, Results[0]);
                host.Display.DispText("新建模板成功！", new Point2d(10, 10), DrawStyle.Of(HColor.Green));
            }
            finally
            {
                if (modelId != null) ClearModel(modelId);
                if (snapshot != null) RestoreModeRect(ref snapshot);
                trial?.Contour?.Dispose();
                reduced?.Dispose();
            }
        }

        /// <summary>丢弃绘制改写过的 ModeRect, 换回绘制前的快照。</summary>
        private void RestoreModeRect(ref CvRegion snapshot)
        {
            inPara.ModeRect.Dispose();
            inPara.ModeRect = snapshot;
            snapshot = null;
        }

        #endregion

        #region 生命周期

        /// <summary>
        /// 工具页打开：模型句柄是运行态、不随参数落盘，从数据目录里的模型文件重建。
        /// 文件缺失或损坏只记日志，执行时报"未建立模板"。
        /// </summary>
        public override void Init(IRoiHost host)
        {
            if (HasModel || !File.Exists(ModelFilePath)) return;
            try
            {
                ModelID = ReadModel(ModelFilePath);
            }
            catch (HalconException ex)
            {
                Log.Warn(GetType().Name, $"{Name}: 读取模型文件失败 {ModelFilePath}", ex);
            }
        }

        /// <summary> 工具页关闭：丢弃显示数据；模型与配置保留 </summary>
        public override void Close(IRoiHost host) => ClearRenderData();

        protected override void Dispose(bool disposing)
        {
            ClearRenderData();
            if (HasModel)
            {
                try { ClearModel(ModelID); }
                catch (HalconException ex) { Log.Warn(GetType().Name, $"{Name}: 释放模型失败.", ex); }
                ModelID = null;
            }
            Contour?.Dispose();
            inPara.HoRect?.Dispose();
            inPara.ModeRect?.Dispose();
            base.Dispose(disposing);
        }

        #endregion

        /// <summary> 把模板轮廓（XLD）按命中位姿做刚体变换，形状 / 缩放 / 通用匹配共用 </summary>
        protected static HObject RigidContour(HObject modelContour, ModelResult result, double scale = 1)
        {
            HOperatorSet.HomMat2dIdentity(out HTuple identity);
            HOperatorSet.HomMat2dScale(identity, scale, scale, 0, 0, out HTuple scaled);
            HOperatorSet.HomMat2dRotate(scaled, result.Angle, 0, 0, out HTuple rotated);
            HOperatorSet.HomMat2dTranslate(rotated, result.Row, result.Column, out HTuple homMat2D);
            HOperatorSet.AffineTransContourXld(modelContour, out HObject contour, homMat2D);
            return contour;
        }

        /// <summary> 角度参数（度）换成弧度 </summary>
        protected static double Rad(double degrees) => degrees.ToRadians();
    }
}
