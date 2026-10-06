namespace DotNet.HalconCore
{
    /// <summary>
    /// 所有参数类的公共显示选项（原 HalconAlgo 的 <c>AlgoFont</c>）。
    /// </summary>
    /// <remarks>
    /// 下沉到契约层是因为基类要用它统一画状态文本；参数面板上的"显示文本 / 字体"几项也由基类统一追加，
    /// 策略的 <c>DeclareParams</c> 不必再各写一遍。
    /// </remarks>
    public class DisplayOptions
    {
        /// <summary> 成功时是否显示结果文本；失败 / 警告始终显示 </summary>
        public bool DispText { get; set; } = true;

        /// <summary> 文本列坐标 </summary>
        public int FontX { get; set; } = 50;

        /// <summary> 文本行坐标 </summary>
        public int FontY { get; set; } = 50;

        /// <summary> 字号 </summary>
        public int FontSize { get; set; } = 15;
    }
}
