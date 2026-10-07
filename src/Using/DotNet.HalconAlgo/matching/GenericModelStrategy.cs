using DotNet.HalconCore;
using HalconDotNet;
using System.Collections.Generic;


namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 通用匹配（generic shape model）。查找类参数在每次查找前写进模型，改了参数下一轮即生效；
    /// 层数 / 缩放范围属于"修改模型"类参数，要重建模板才生效。
    /// </summary>
    [Algo("match.generic", "通用匹配", Group = "定位", Order = 140)]
    public class GenericModelStrategy : MatchStrategyBase<GenericModel>
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
            HOperatorSet.CreateGenericShapeModel(out HTuple modelId);
            try
            {
                // 训练前必须设置的"修改模型"类参数
                HOperatorSet.SetGenericShapeModelParam(modelId, "num_levels", inPara.NumLevels);
                HOperatorSet.SetGenericShapeModelParam(modelId, "iso_scale_min", inPara.ScaleMin);
                HOperatorSet.SetGenericShapeModelParam(modelId, "iso_scale_max", inPara.ScaleMax);
                HOperatorSet.SetGenericShapeModelParam(modelId, "optimization", "auto");
                HOperatorSet.SetGenericShapeModelParam(modelId, "metric", "use_polarity");
                HOperatorSet.TrainGenericShapeModel(templateImage, modelId);
                return modelId;
            }
            catch
            {
                HOperatorSet.ClearHandle(modelId);
                throw;
            }
        }

        protected override List<MatchHit> FindModel(HObject image, HTuple modelId, int numMatches)
        {
            ApplySearchParams(modelId, numMatches);
            HOperatorSet.FindGenericShapeModel(image, modelId, out HTuple matchResultId, out HTuple numMatchResult);
            var hits = new List<MatchHit>();
            try
            {
                if (numMatchResult.I <= 0) return hits;
                HOperatorSet.GetGenericShapeModelResult(matchResultId, "all", "row", out HTuple row);
                HOperatorSet.GetGenericShapeModelResult(matchResultId, "all", "column", out HTuple column);
                HOperatorSet.GetGenericShapeModelResult(matchResultId, "all", "angle", out HTuple angle);
                HOperatorSet.GetGenericShapeModelResult(matchResultId, "all", "score", out HTuple score);
                for (int i = 0; i < score.Length; i++)
                {
                    var result = new ModelResult(row[i], column[i], angle[i], score[i]);
                    HOperatorSet.GetGenericShapeModelResultObject(out HObject contour, matchResultId, i, "contours");
                    hits.Add(new MatchHit(result, contour));
                }
                return hits;
            }
            catch
            {
                foreach (var hit in hits) hit.Contour?.Dispose();
                throw;
            }
            finally
            {
                // 取出的轮廓是独立对象, 结果句柄用完即释放 (22.11 没有专门的 clear 算子, clear_handle 可用)
                HOperatorSet.ClearHandle(matchResultId);
            }
        }

        /// <summary> 查找类参数（角度为弧度）在每次查找前写入模型 </summary>
        private void ApplySearchParams(HTuple modelId, int numMatches)
        {
            HOperatorSet.SetGenericShapeModelParam(modelId, "angle_start", Rad(inPara.AngleStart));
            HOperatorSet.SetGenericShapeModelParam(modelId, "angle_end", Rad(inPara.AngleStart + inPara.AngleExtent));
            HOperatorSet.SetGenericShapeModelParam(modelId, "max_overlap", inPara.MaxOverlap);
            HOperatorSet.SetGenericShapeModelParam(modelId, "min_score", inPara.MinScore);
            HOperatorSet.SetGenericShapeModelParam(modelId, "greediness", inPara.Greediness);
            HOperatorSet.SetGenericShapeModelParam(modelId, "subpixel", inPara.SubPixel);
            HOperatorSet.SetGenericShapeModelParam(modelId, "num_matches", numMatches);
        }

        protected override void ClearModel(HTuple modelId) => HOperatorSet.ClearHandle(modelId);

        protected override void WriteModel(HTuple modelId, string path) => HOperatorSet.WriteShapeModel(modelId, path);

        protected override HTuple ReadModel(string path)
        {
            HOperatorSet.ReadShapeModel(path, out HTuple modelId);
            return modelId;
        }
    }

    public class GenericModel : MatchParaBase
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
