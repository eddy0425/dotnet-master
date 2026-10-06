using DotNet.Drawing;
using DotNet.HalconCore;
using HalconDotNet;
using System;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 直线摆正：绕图像中心旋转，使上游直线平行于 X 轴或 Y 轴（取最小旋转量）。
    /// </summary>
    [Algo("image.line-rotate", "直线图像", Group = "图像", Order = 30)]
    public class LineRotImageStrategy : RotateStrategyBase<LineRotImage>
    {
        protected override void DeclareParams(ParamBuilder p)
        {
            p.Tab(TabPageEnum.Parameter)
             .Source("图像来源", () => inPara.ImageIn, v => inPara.ImageIn = v, OutEnum.Image)
             .Source("直线来源", () => inPara.LineIn, v => inPara.LineIn = v, OutEnum.Line)
             .Choice("对齐方式", () => inPara.AlignAxis, v => inPara.AlignAxis = v,
                     Option.Of(AlignAxis.ParallelX, "平行X轴"), Option.Of(AlignAxis.ParallelY, "平行Y轴"));
        }

        protected override RunResult Transform(RunContext context, HObject image, out HObject transformed)
        {
            if (inPara.LineIn.IsLocal)
                throw new InvalidOperationException("必须选择直线来源");
            CvLine line = context.Resolve<CvLine>(inPara.LineIn);
            // 退化直线是上游拟合结果无效, 属于业务错误而不是"意外的空引用"
            if (line.IsDegenerate)
                throw new InvalidOperationException($"直线数据退化 ({context.Describe(inPara.LineIn)})");

            double lineAngle = Math.Atan2(line.End.Y - line.Start.Y, line.End.X - line.Start.X);
            double rotate = inPara.AlignAxis == AlignAxis.ParallelY ? lineAngle - Math.PI / 2 : lineAngle;

            // 归一化到 [-π/2, π/2]，取最小旋转角度（直线无方向性）
            while (rotate > Math.PI / 2) rotate -= Math.PI;
            while (rotate < -Math.PI / 2) rotate += Math.PI;

            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            transformed = RotateAbout(image, rotate, height.D / 2, width.D / 2);

            string axis = inPara.AlignAxis == AlignAxis.ParallelY ? "平行Y轴" : "平行X轴";
            return RunResult.Ok($"对齐:{axis} 旋转:{rotate.ToDegrees():F2}°");
        }
    }

    public class LineRotImage : RotatePara
    {
        /// <summary> 直线来源 </summary>
        public SourceRef LineIn { get; set; } = SourceRef.Local;

        /// <summary> 对齐轴 </summary>
        public AlignAxis AlignAxis { get; set; } = AlignAxis.ParallelX;
    }
}
