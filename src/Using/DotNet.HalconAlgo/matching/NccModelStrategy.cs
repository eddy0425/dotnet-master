using DotNet.HalconCore;
using HalconDotNet;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary> 灰度匹配（NCC）。"轮廓"是模板区域，按命中位姿搬过去 </summary>
    [Algo("match.ncc", "灰度匹配", Group = "定位", Order = 120)]
    public class NccModelStrategy : MatchStrategyBase<NccModel>
    {
        protected override string ModelFileExtension => ".ncm";

        protected override HTuple CreateModel(HObject templateImage)
        {
            HOperatorSet.CreateNccModel(templateImage, inPara.NumLevels, Rad(inPara.AngleStart), Rad(inPara.AngleExtent),
                "auto", "use_polarity", out HTuple modelId);
            return modelId;
        }

        protected override List<MatchHit> FindModel(HObject image, HTuple modelId, int numMatches)
        {
            HOperatorSet.FindNccModel(image, modelId, Rad(inPara.AngleStart), Rad(inPara.AngleExtent),
                inPara.MinScore, numMatches, inPara.MaxOverlap, inPara.SubPixel, inPara.NumLevels,
                out HTuple row, out HTuple column, out HTuple angle, out HTuple score);

            var hits = new List<MatchHit>();
            HOperatorSet.GetNccModelRegion(out HObject model, modelId);
            try
            {
                for (int i = 0; i < score.Length; i++)
                {
                    var result = new ModelResult(row[i], column[i], angle[i], score[i]);
                    HOperatorSet.VectorAngleToRigid(0, 0, 0, result.Row, result.Column, result.Angle, out HTuple homMat2D);
                    HOperatorSet.AffineTransRegion(model, out HObject region, homMat2D, "nearest_neighbor");
                    hits.Add(new MatchHit(result, region));
                }
            }
            finally
            {
                model.Dispose();
            }
            return hits;
        }

        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearNccModel(modelId);

        protected override void WriteModel(HTuple modelId, string path) => HOperatorSet.WriteNccModel(modelId, path);

        protected override HTuple ReadModel(string path)
        {
            HOperatorSet.ReadNccModel(path, out HTuple modelId);
            return modelId;
        }
    }

    public class NccModel : MatchParaBase
    {
        /// <summary> 亚像素 </summary>
        public string SubPixel { get; set; } = "true";
    }
}
