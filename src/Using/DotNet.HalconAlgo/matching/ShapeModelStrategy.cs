using DotNet.HalconCore;
using HalconDotNet;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary> 形状匹配 </summary>
    [Algo("match.shape", "形状匹配", Group = "定位", Order = 110)]
    public class ShapeModelStrategy : MatchStrategyBase<ShapeModel>
    {
        protected override string ModelFileExtension => ".shm";

        protected override HTuple CreateModel(HObject templateImage)
        {
            HOperatorSet.CreateShapeModel(templateImage, inPara.NumLevels, Rad(inPara.AngleStart), Rad(inPara.AngleExtent),
                "auto", "auto", "use_polarity", "auto", "auto", out HTuple modelId);
            return modelId;
        }

        protected override List<MatchHit> FindModel(HObject image, HTuple modelId, int numMatches)
        {
            HOperatorSet.FindShapeModel(image, modelId, Rad(inPara.AngleStart), Rad(inPara.AngleExtent),
                inPara.MinScore, numMatches, inPara.MaxOverlap, inPara.SubPixel, inPara.NumLevels, inPara.Greediness,
                out HTuple row, out HTuple column, out HTuple angle, out HTuple score);

            var hits = new List<MatchHit>();
            HOperatorSet.GetShapeModelContours(out HObject model, modelId, 1);
            try
            {
                for (int i = 0; i < score.Length; i++)
                {
                    var result = new ModelResult(row[i], column[i], angle[i], score[i]);
                    hits.Add(new MatchHit(result, RigidContour(model, result)));
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

    public class ShapeModel : MatchParaBase
    {
        /// <summary> 亚像素：不等于 "none" 时为亚像素精度 </summary>
        public string SubPixel { get; set; } = "least_squares";

        /// <summary> 贪婪系数（0：安全但慢；1：快但可能漏匹配） </summary>
        public double Greediness { get; set; } = 0.7;
    }
}
