using System;
using System.Collections.Generic;
using System.Threading;
using DotNet.Drawing;
using HalconDotNet;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 一次执行的上下文：当前图像 + 上游工具。"本地 / 上游"的解析全部收在这里。
    /// </summary>
    /// <remarks>
    /// 取代原来各策略里散落的 <c>if (inPara.ImageIn == "默认") ... else strategys.ResolveFrom(...)</c>。
    /// <see cref="Upstream"/> 只含当前工具之前的工具：引用自己或下游，一律按"找不到"处理。
    /// 解析出的句柄都是<b>借用</b>的，所有权仍属于上游，调用方不得释放。
    /// </remarks>
    public sealed class RunContext
    {
        private static readonly IReadOnlyList<IParaStrategy> NoUpstream = new IParaStrategy[0];

        public RunContext(HObject currentImage, IReadOnlyList<IParaStrategy> upstream = null, CancellationToken cancellation = default(CancellationToken))
        {
            CurrentImage = currentImage;
            Upstream = upstream ?? NoUpstream;
            Cancellation = cancellation;
        }

        /// <summary> 单图验证：没有上游 </summary>
        public static RunContext ForImage(HObject image) => new RunContext(image);

        /// <summary> 本地图像来源对应的图像；单图验证时就是传入的图 </summary>
        public HObject CurrentImage { get; }

        /// <summary> 当前工具之前的工具 </summary>
        public IReadOnlyList<IParaStrategy> Upstream { get; }

        public CancellationToken Cancellation { get; }

        /// <summary> 图像：本地取 <see cref="CurrentImage"/>；空句柄 / 空对象一律报"图像来源为空" </summary>
        public HObject ResolveImage(SourceRef source)
        {
            var image = source.IsLocal ? CurrentImage : Resolve<HObject>(source);
            if (!image.NotNull() || image.CountObj() == 0)
                throw new InvalidOperationException(source.IsLocal ? "图像来源为空，无法执行！" : $"图像来源 '{Describe(source)}' 为空，无法执行！");
            return image;
        }

        /// <summary>
        /// 区域：本地取配置 ROI，上游接受 <see cref="CvRegion"/> 或裸 <see cref="HObject"/>。
        /// 两条路径同一口径：未绘制 / 已清空 / 0 个对象都报错，保证拿到的句柄能直接交给 HALCON 算子。
        /// </summary>
        public HObject ResolveRegion(SourceRef source, CvRegion local)
        {
            if (source.IsLocal)
            {
                var region = local?.HoRegion;
                if (!region.IsUsableRegion()) throw new InvalidOperationException("尚未绘制 ROI");
                return region;
            }
            if (TryResolveRegion(source, out HObject upstream)) return upstream;
            throw new AlgoOutputNotFoundException(Describe(source), typeof(CvRegion));
        }

        /// <summary> 上游区域的安全版本：解析不到、或句柄不可用时返回 false </summary>
        public bool TryResolveRegion(SourceRef source, out HObject region)
        {
            region = null;
            if (source.IsLocal) return false;
            var value = Find(source)?.GetValue();
            var candidate = (value as CvRegion)?.HoRegion ?? value as HObject;
            if (!candidate.IsUsableRegion()) return false;
            region = candidate;
            return true;
        }

        /// <summary>
        /// 坐标系跟随：本地返回恒等跟随；上游返回 { 当前坐标系, 示教模板点 }。
        /// 上游尚未示教时抛异常 —— 响亮失败好过静默算错。
        /// </summary>
        public CoordFollow ResolveCoord(SourceRef source)
        {
            if (source.IsLocal) return CoordFollow.None;

            var item = Find(source);
            if (!(item?.GetValue() is CvCoord current))
                throw new AlgoOutputNotFoundException(Describe(source), typeof(CvCoord), item?.GetValue()?.GetType());
            if (!item.TryGetTemplate(out Point2d template))
                throw new InvalidOperationException($"跟随坐标 '{Describe(source)}' 还没有示教模板点");
            return new CoordFollow(template, current);
        }

        public T Resolve<T>(SourceRef source)
        {
            var value = Find(source)?.GetValue();
            if (value == null) throw new AlgoOutputNotFoundException(Describe(source), typeof(T));
            if (!(value is T typed)) throw new AlgoOutputNotFoundException(Describe(source), typeof(T), value.GetType());
            return typed;
        }

        /// <remarks>返回 false 时 <paramref name="value"/> 为 default，调用方不得读取。</remarks>
        public bool TryResolve<T>(SourceRef source, out T value)
        {
            if (Find(source)?.GetValue() is T typed)
            {
                value = typed;
                return true;
            }
            value = default(T);
            return false;
        }

        /// <summary> 界面 / 报错用的 "工具名/输出" </summary>
        public string Describe(SourceRef source) => Upstream.Describe(source);

        private OutputItem Find(SourceRef source)
        {
            if (source.IsLocal) return null;
            var tool = Upstream.FindTool(source.ToolId);
            return tool?.FindOutput(source.Output);
        }
    }

    /// <summary>
    /// 坐标系跟随：把示教时的几何搬到当前工件位姿（刚体变换）。
    /// <see cref="None"/> 是恒等跟随，所有方法原样返回。
    /// </summary>
    public readonly struct CoordFollow
    {
        public static readonly CoordFollow None = default(CoordFollow);

        public CoordFollow(Point2d template, CvCoord current)
        {
            Template = template;
            Current = current;
            IsActive = true;
        }

        /// <summary> 是否真的在跟随（false = 恒等） </summary>
        public bool IsActive { get; }

        /// <summary> 示教时的参考原点（零角） </summary>
        public Point2d Template { get; }

        /// <summary> 当前坐标系 </summary>
        public CvCoord Current { get; }

        public Point2d TransPoint(Point2d point)
            => IsActive ? HalconController.TransPoint(new CvCoord(Template), Current, point) : point;

        public Angle TransAngle(Angle angle)
            => IsActive ? (angle + Current.Angle).Normalized : angle;

        /// <summary> 变换区域；返回的新对象归调用方所有（恒等时是副本），用完必须释放 </summary>
        public HObject TransRegion(HObject region)
        {
            if (!IsActive) return region.CopyObj(1, -1);
            HalconController.TransRegion(new CvCoord(Template), Current, region, out HObject transformed);
            return transformed;
        }
    }

    /// <summary>
    /// 产出"当前图像"的工具（文件图像、旋转图像…）。流程运行时，其后工具的本地图像来源取它的输出。
    /// </summary>
    public interface IImageProducer
    {
        /// <summary> 本轮输出的图像；失败时为空对象 </summary>
        HObject Image { get; }
    }

    /// <summary>
    /// 匹配模板的只读视图：宿主的模板编辑窗 / 缩略图据此显示，不再按具体匹配类型逐个下转。
    /// </summary>
    public sealed class TemplateView
    {
        public TemplateView(string modelPath, HObject modelRegion, HObject contour, ModelResult? best)
        {
            ModelPath = modelPath;
            ModelRegion = modelRegion;
            Contour = contour;
            Best = best;
        }

        /// <summary> 模板小图路径；尚未建模板时为空串 </summary>
        public string ModelPath { get; }

        /// <summary> 模板区域（借用句柄，可能为 null） </summary>
        public HObject ModelRegion { get; }

        /// <summary> 最近一次的最佳匹配轮廓（借用句柄） </summary>
        public HObject Contour { get; }

        /// <summary> 最近一次的最佳匹配；没有结果时为 null </summary>
        public ModelResult? Best { get; }
    }
}
