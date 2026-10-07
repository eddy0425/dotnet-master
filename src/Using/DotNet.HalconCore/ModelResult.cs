using DotNet.Drawing;
using HalconDotNet;
using Newtonsoft.Json;


namespace DotNet.HalconCore
{
    public struct ModelResult
    {
        /// <summary> 行 </summary>
        public double Row { get; set; }

        /// <summary> 列 </summary>
        public double Column { get; set; }

        /// <summary> 角度 </summary>
        public double Angle { get; set; }

        /// <summary> 分数 </summary>
        public double Score { get; set; }

        public ModelResult(double row, double column, double angle, double score)
        {
            Row = row;
            Column = column;
            Angle = angle;
            Score = score;
        }

        [JsonIgnore]
        public double X { get { return Column; } }
        [JsonIgnore]
        public double Y { get { return Row; } }
        [JsonIgnore]
        public double ToDegrees { get { return Angle.ToDegrees(); } }
        [JsonIgnore]
        public CvCoord Coord { get { return new CvCoord(Column, Row, DotNet.Drawing.Angle.FromRadians(Angle)); } }

        #region Formatting

        public override string ToString()
        {
            return $"Row={Row:F4}, Column={Column:F4}, Angle={Angle:F4}, Score={Score:F4}";
        }

        /// <summary>
        /// 格式化输出
        /// </summary>
        /// <param name="format">
        /// 格式字符串，支持占位符：
        /// {row} / {r} — 行坐标
        /// {col} / {c} — 列坐标
        /// {angle} / {a} — 角度（弧度）
        /// {deg} / {d} — 角度（角度制）
        /// {score} / {s} — 分数
        /// 例："({row:F2}, {col:F2}) @ {deg:F1}°, score={score:F4}"
        /// </param>
        public string ToString(string format)
        {
            return format
                .Replace("{row}", Row.ToString())
                .Replace("{r}", Row.ToString())
                .Replace("{col}", Column.ToString())
                .Replace("{c}", Column.ToString())
                .Replace("{angle}", Angle.ToString())
                .Replace("{a}", Angle.ToString())
                .Replace("{deg}", ToDegrees.ToString())
                .Replace("{d}", ToDegrees.ToString())
                .Replace("{score}", Score.ToString())
                .Replace("{s}", Score.ToString());
        }

        #endregion

    }
}
