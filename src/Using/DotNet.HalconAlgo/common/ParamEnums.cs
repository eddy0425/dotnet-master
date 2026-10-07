using System;

namespace DotNet.HalconAlgo
{
    // 参数类里只存枚举，界面文字只出现在各策略 DeclareParams 的 Choice 声明里。
    // 原来存的是「由黑到白」「是 / 否」这类界面文字，改一个字旧配置就读不回来。

    /// <summary> 边缘过渡方向 </summary>
    public enum Transition
    {
        /// <summary> 由黑到白 </summary>
        Positive,

        /// <summary> 由白到黑 </summary>
        Negative,

        /// <summary> 全部 </summary>
        All,
    }

    /// <summary> 每个测量矩形里取第几条边 </summary>
    public enum EdgeSelect
    {
        First,
        Second,
        Last,

        /// <summary> 全部：与 First 相同，取第一条（measure_pos 的 'all' 之后按下标挑） </summary>
        All,
    }

    /// <summary> 图像镜像 </summary>
    public enum MirrorMode
    {
        None,

        /// <summary> 行镜像（上下翻转） </summary>
        Row,

        /// <summary> 列镜像（左右翻转） </summary>
        Column,

        /// <summary> 原点镜像：关于图像中心点对称，等价于旋转 180° </summary>
        Origin,
    }

    /// <summary> 旋转图像的方式 </summary>
    public enum RotateMode
    {
        /// <summary> 绕图像中心转固定角度 </summary>
        ImageCenter,

        /// <summary> 把坐标系 X 轴摆正（与 CoordXAxis 相同，保留以兼容原有选项） </summary>
        Coord,

        CoordXAxis,

        /// <summary> 把坐标系 Y 轴摆正 </summary>
        CoordYAxis,
    }

    /// <summary> 直线摆正到哪条轴 </summary>
    public enum AlignAxis
    {
        ParallelX,
        ParallelY,
    }

    internal static class ParamEnumExtensions
    {
        public static string ToHalcon(this Transition transition)
        {
            switch (transition)
            {
                case Transition.Positive: return "positive";
                case Transition.Negative: return "negative";
                case Transition.All: return "all";
                default: throw new ArgumentOutOfRangeException(nameof(transition), transition, "过渡方向无效");
            }
        }

        public static string ToHalcon(this EdgeSelect select)
        {
            switch (select)
            {
                case EdgeSelect.First: return "first";
                case EdgeSelect.Second: return "second";
                case EdgeSelect.Last: return "last";
                case EdgeSelect.All: return "all";
                default: throw new ArgumentOutOfRangeException(nameof(select), select, "边缘选择无效");
            }
        }
    }
}
