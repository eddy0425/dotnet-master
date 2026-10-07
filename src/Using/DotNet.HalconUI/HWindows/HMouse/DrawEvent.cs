using DotNet.Drawing;
using DotNet.HalconUI;
using HalconDotNet;
using System;
using DotNet.HalconCore;

namespace DotNet.HalconUI
{
    // 事件参数全部为不可变对象 (只读属性), 由 EventHandler<T> 标准委托承载.
    // 不再为每个 Args 单独定义委托类型, 直接使用 BCL 的 EventHandler<TEventArgs>.

    public class DrawModelUIArgs : EventArgs
    {
        public DrawModelUIArgs(string modelPath, HObject ho_ModeRect, HObject ho_Contour, ModelResult result)
        {
            ModelPath = modelPath;
            HoModeRect = ho_ModeRect;
            HoContour = ho_Contour;
            Result = result;
        }

        /// <summary> 模版路径 </summary>
        public string ModelPath { get; }

        /// <summary> 模版区域 </summary>
        /// <remarks>
        /// 借用引用：句柄归触发方（策略的 inPara）所有，订阅方<b>不得 Dispose</b>，
        /// 也不应在处理函数返回后继续持有——策略重建模板时会释放它。需要保留请自行复制
        /// （现有订阅方均经 <c>TransObject</c> 生成新对象）。
        /// </remarks>
        public HObject HoModeRect { get; }

        /// <summary> 模版轮廓 </summary>
        /// <remarks> 借用引用，所有权约定同 <see cref="HoModeRect"/>。 </remarks>
        public HObject HoContour { get; }

        /// <summary> 匹配结果 </summary>
        public ModelResult Result { get; }

    }

}
