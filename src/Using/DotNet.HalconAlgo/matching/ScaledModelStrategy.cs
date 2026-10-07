using DotNet.HalconCore;
using HalconDotNet;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary> 缩放匹配：各向同性缩放的形状匹配，轮廓按找到的缩放系数显示 </summary>
    [Algo("match.scaled", "缩放匹配", Group = "定位", Order = 130)]
    public class ScaledModelStrategy : MatchStrategyBase<ScaledModel>
    {
        protected override string ModelFileExtension => ".shm";

        protected override string SummaryExtra => $"缩放:[{inPara.ScaleMin},{inPara.ScaleMax}] ";

        protected override void DeclareParams(ParamBuilder p)
        {
            base.DeclareParams(p);
            p.Double("最小缩放", () => inPara.ScaleMin, v => inPara.ScaleMin = v, presets: new[] { 0.8, 0.7 }, min: 0.01)
             .Double("最大缩放", () => inPara.ScaleMax, v => inPara.ScaleMax = v, presets: new[] { 1.2, 1.5 }, min: 0.01);
        }

        protected override HTuple CreateModel(HObject templateImage)
        {
            HOperatorSet.CreateScaledShapeModel(templateImage, inPara.NumLevels, Rad(inPara.AngleStart), Rad(inPara.AngleExtent), "auto",
                inPara.ScaleMin, inPara.ScaleMax, "auto", "auto", "use_polarity", "auto", "auto", out HTuple modelId);
            return modelId;
        }

        protected override List<MatchHit> FindModel(HObject image, HTuple modelId, int numMatches)
        {
            HOperatorSet.FindScaledShapeModel(image, modelId, Rad(inPara.AngleStart), Rad(inPara.AngleExtent),
                inPara.ScaleMin, inPara.ScaleMax, inPara.MinScore, numMatches,
                inPara.MaxOverlap, inPara.SubPixel, inPara.NumLevels, inPara.Greediness,
                out HTuple row, out HTuple column, out HTuple angle, out HTuple scale, out HTuple score);

            var hits = new List<MatchHit>();
            HOperatorSet.GetShapeModelContours(out HObject model, modelId, 1);
            try
            {
                for (int i = 0; i < score.Length; i++)
                {
                    var result = new ModelResult(row[i], column[i], angle[i], score[i]);
                    // 按找到的缩放系数显示轮廓: 只做刚体变换的话, 目标放大 / 缩小时轮廓仍是模板原尺寸
                    hits.Add(new MatchHit(result, RigidContour(model, result, scale[i].D)));
                }
            }
            finally
            {
                model.Dispose();
            }
            return hits;
        }

        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearShapeModel(modelId);

        protected override void WriteModel(HTuple modelId, string path) => HOperatorSet.WriteShapeModel(modelId, path);

        protected override HTuple ReadModel(string path)
        {
            HOperatorSet.ReadShapeModel(path, out HTuple modelId);
            return modelId;
        }
    }

    public class ScaledModel : MatchParaBase
    {
        /// <summary> 亚像素 </summary>
        public string SubPixel { get; set; } = "least_squares";

        /// <summary> 贪婪系数 </summary>
        public double Greediness { get; set; } = 0.7;

        /// <summary> 最小缩放 </summary>
        public double ScaleMin { get; set; } = 0.8;

        /// <summary> 最大缩放 </summary>
        public double ScaleMax { get; set; } = 1.2;
    }
}
