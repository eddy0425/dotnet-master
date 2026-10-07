namespace DotNet.Drawing
{
    /// <summary>
    /// 绘制样式：颜色、尺寸、线宽、填充模式的载体。
    /// </summary>
    /// <remarks>
    /// 存在的理由是消灭「每个图元都要有一个带 color、一个不带 color 的重载」这种成对膨胀
    /// ——那 22 对重载之间唯一的差别就是首行多一句 <c>SetColor(color)</c>。
    /// <para>
    /// 所有属性都是「可选」语义：<c>null</c> 或 <see cref="HColor.IsEmpty"/> 表示
    /// <b>沿用窗口当前状态、不做修改</b>，而不是回落到某个默认值。
    /// 这样 <c>Disp(shape)</c>（不传样式）与旧的不带 color 重载行为完全一致。
    /// </para>
    /// </remarks>
    public sealed record DrawStyle
    {
        /// <summary>图元尺寸的历史默认值（十字臂长 / 箭头头部大小）。</summary>
        public const double DefaultSize = 20;

        /// <summary>画笔颜色。<see cref="HColor.IsEmpty"/> 时沿用当前颜色。</summary>
        public HColor Color { get; init; }

        /// <summary>
        /// 图元尺寸。对十字/点是臂长，对箭头是头部大小，对文本是字号。
        /// <c>null</c> 表示沿用各图元的历史默认值（文本则是不改字号）。
        /// </summary>
        public double? Size { get; init; }

        /// <summary>线宽。<c>null</c> 表示不修改窗口当前线宽。</summary>
        public int? LineWidth { get; init; }

        /// <summary>填充模式："margin" 只画轮廓，"fill" 填充。<c>null</c> 表示不修改。</summary>
        public string DrawMode { get; init; }

        /// <summary>只指定颜色。</summary>
        public static DrawStyle Of(HColor color) => new DrawStyle { Color = color };

        /// <summary>指定颜色与尺寸。</summary>
        public static DrawStyle Of(HColor color, double size) => new DrawStyle { Color = color, Size = size };

        /// <summary>取尺寸，未指定时回落到 <paramref name="fallback"/>。</summary>
        public static double SizeOr(DrawStyle style, double fallback = DefaultSize)
            => style?.Size ?? fallback;
    }
}
