using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;

namespace DotNet.HalconAlgo
{
    /// <summary> 旋转类图像工具的公共参数：图像来源 </summary>
    public abstract class RotatePara : DisplayOptions
    {
        /// <summary> 图像来源；本地取当前图像 </summary>
        public SourceRef ImageIn { get; set; } = SourceRef.Local;
    }

    /// <summary>
    /// 旋转类图像工具的公共流程（原 RotateImage / LineRotImage 里逐字相同的 RunWithReset）：
    /// 取图 → 子类算出变换 → 产出新图。子类只决定"怎么转"；新图作为底图显示由宿主按 <see cref="IImageProducer"/> 决定。
    /// </summary>
    public abstract class RotateStrategyBase<TPara> : ParaStrategyBase<TPara>, IImageProducer
        where TPara : RotatePara, new()
    {
        // 上一轮的输出要等本轮算完才释放: 本地来源下传入的当前图像可能正是上一轮的输出
        private HObject _retired;

        protected RotateStrategyBase()
        {
            HOperatorSet.GenEmptyObj(out HObject empty);
            Image = empty;
        }

        /// <summary> 本轮输出图像；失败时为空对象 </summary>
        public HObject Image { get; private set; }

        protected override void DeclareOutputs(OutputBuilder o)
        {
            o.Image("图像", () => Image);
        }

        protected override void ResetOutputs()
        {
            if (_retired == null) _retired = Image;
            else Image?.Dispose();
            HOperatorSet.GenEmptyObj(out HObject empty);
            Image = empty;
        }

        protected sealed override RunResult Execute(RunContext context)
        {
            try
            {
                HObject source = context.ResolveImage(inPara.ImageIn);
                var result = Transform(context, source, out HObject transformed);
                Image.Dispose();
                Image = transformed;
                return result;
            }
            finally
            {
                _retired?.Dispose();
                _retired = null;
            }
        }

        /// <summary>
        /// 算出新图。<paramref name="transformed"/> 归基类所有；失败直接抛异常即可。
        /// </summary>
        protected abstract RunResult Transform(RunContext context, HObject image, out HObject transformed);

        /// <summary> 绕 (row, col) 旋转 <paramref name="radians"/>，画幅不变 </summary>
        protected static HObject RotateAbout(HObject image, double radians, double row, double col)
        {
            HOperatorSet.HomMat2dIdentity(out HTuple identity);
            HOperatorSet.HomMat2dRotate(identity, radians, row, col, out HTuple rotate);
            HOperatorSet.AffineTransImage(image, out HObject result, rotate, "constant", "false");
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            Image?.Dispose();
            _retired?.Dispose();
            base.Dispose(disposing);
        }
    }
}
