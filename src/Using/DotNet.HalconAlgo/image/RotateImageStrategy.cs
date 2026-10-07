using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 旋转图像：绕图像中心转固定角度，或按上游坐标系把它的 X / Y 轴摆正。
    /// </summary>
    [Algo("image.rotate", "旋转图像", Group = "图像", Order = 20)]
    public class RotateImageStrategy : RotateStrategyBase<RotateImage>
    {
        protected override void DeclareParams(ParamBuilder p)
        {
            p.Page(Pages.Parameter)
             .Source("图像来源", () => inPara.ImageIn, v => inPara.ImageIn = v, OutEnum.Image)
             .Choice("选择方式", () => inPara.RotateType, v => inPara.RotateType = v,
                     Option.Of(RotateMode.ImageCenter, "图像中心"), Option.Of(RotateMode.Coord, "坐标系"),
                     Option.Of(RotateMode.CoordXAxis, "坐标系X轴"), Option.Of(RotateMode.CoordYAxis, "坐标系Y轴"))
             // 原来"旋转角度"与"坐标系"共用一个槽位、随方式切换含义; 现在是两项, 各带显示条件
             .Double("旋转角度", () => inPara.RotateAngle, v => inPara.RotateAngle = v, presets: new double[] { 0, 90, 180, 270 })
             .When(() => inPara.RotateType == RotateMode.ImageCenter)
             .Source("坐标系", () => inPara.CoordIn, v => inPara.CoordIn = v, OutEnum.Coord)
             .When(() => inPara.RotateType != RotateMode.ImageCenter);
        }

        protected override RunResult Transform(RunContext context, HObject image, out HObject transformed)
        {
            if (inPara.RotateType == RotateMode.ImageCenter)
            {
                if (inPara.RotateAngle != 0)
                    HOperatorSet.RotateImage(image, out transformed, inPara.RotateAngle, "constant");   // Phi 单位为度
                else
                    HOperatorSet.CopyObj(image, out transformed, 1, 1);
                return RunResult.Ok($"方式:图像中心 角度:{inPara.RotateAngle:F2}°");
            }

            if (inPara.CoordIn.IsLocal)
                throw new InvalidOperationException("按坐标系旋转时必须选择坐标系");
            CvCoord coord = context.Resolve<CvCoord>(inPara.CoordIn);

            // 先归一化到 [-π, π) 再换算成度数, 不再手写 %360 三段式 (原写法在 ±180 处的取舍不明确)
            double baseDeg = coord.Angle.Normalized.Degrees;
            double rotateDeg;
            switch (inPara.RotateType)
            {
                case RotateMode.CoordYAxis:
                    // 把 Y 轴摆正所需旋转量为 (±90 - baseDeg); baseDeg == 0 时归入 ">= 0" 分支取 +90,
                    // 与 baseDeg → 0⁺ 的极限连续 (原实现把 0 漏在两个分支之外, 退化成不旋转)。
                    rotateDeg = baseDeg >= 0 ? 90 - baseDeg : -90 - baseDeg;
                    break;
                case RotateMode.Coord:
                case RotateMode.CoordXAxis:
                    rotateDeg = -baseDeg;
                    break;
                default:
                    // 配置文件里手改出来的非法值: 明确报错, 不按某个默认方式静默旋转
                    throw new ArgumentOutOfRangeException(nameof(inPara.RotateType), inPara.RotateType, "旋转方式无效");
            }

            transformed = RotateAbout(image, rotateDeg.ToRadians(), coord.Y, coord.X);
            return RunResult.Ok($"方式:{Text(inPara.RotateType)} 坐标:({coord.X:F2},{coord.Y:F2}) 角度:{rotateDeg:F2}°");
        }

        private static string Text(RotateMode mode)
        {
            switch (mode)
            {
                case RotateMode.CoordXAxis: return "坐标系X轴";
                case RotateMode.CoordYAxis: return "坐标系Y轴";
                case RotateMode.Coord: return "坐标系";
                default: return "图像中心";
            }
        }
    }

    public class RotateImage : RotatePara
    {
        /// <summary> 坐标系来源（按坐标系旋转时用） </summary>
        public SourceRef CoordIn { get; set; } = SourceRef.Local;

        /// <summary> 旋转方式 </summary>
        public RotateMode RotateType { get; set; } = RotateMode.ImageCenter;

        /// <summary> 旋转角度（度，按图像中心旋转时用） </summary>
        public double RotateAngle { get; set; } = 0;
    }
}
