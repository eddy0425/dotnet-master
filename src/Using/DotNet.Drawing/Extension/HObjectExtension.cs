using HalconDotNet;
using System;

namespace DotNet.Drawing
{
    public static class HObjectExtension
    {
        /// <summary>
        /// 句柄非 null 且已初始化。
        /// </summary>
        /// <remarks>
        /// 允许传 null: 这两个扩展方法的全部价值就在于"允许传 null 进来问一句"。
        /// </remarks>
        public static bool NotNull(this HObject image)
        {
            // is object: 绕开可能的 == 重载做纯引用判空。
            if (image is object)
            {
                return image.IsInitialized();
            }
            return false;
        }

        /// <summary>
        /// 区域句柄是否可直接交给 HALCON 算子: 非 null、已初始化、且 count_obj 大于 0。
        /// </summary>
        /// <remarks>
        /// gen_empty_obj 产出的是"已初始化但长度为 0"的元组, <see cref="NotNull"/> 判不出来。
        /// 这种空元组送进 reduce_domain 会抛出与真实原因(ROI 未绘制 / 上游未运行)毫无关系的
        /// HALCON 原生异常; 在匹配里则因 count_obj 为 0 让循环一次都不进, 静默跑出 0 个结果。
        /// 上游解析路径由 <c>TryResolveRegionFrom</c> 复用本方法, 本地配置 ROI 需各策略自行调用。
        /// </remarks>
        public static bool IsUsableRegion(this HObject region)
        {
            return region.NotNull() && region.CountObj() > 0;
        }

        /// <summary>
        /// 取图像句柄，空句柄时抛出指明工具名的异常。
        /// </summary>
        /// <remarks>
        /// <c>IHDisplay.HoImage</c> 在「窗口已释放 / 尚未载入图像」时就是 null，
        /// 直接往下送会在 reduce_domain 之类的算子里炸出与真实原因无关的 HALCON 原生异常，
        /// 或者更糟 —— 一个只有堆栈没有说明的 NRE。各策略原本零散写着
        /// <c>if (ho_Image == null || !ho_Image.NotNull()) throw new NullReferenceException("图像来源为空！")</c>，
        /// 这里统一成一处，并把异常类型从 NullReferenceException（应由运行时抛出，不该手写）换成
        /// <see cref="InvalidOperationException"/>，与 ROI 未绘制时的守卫同一口径。
        /// </remarks>
        /// <param name="toolName">出错时写进消息的工具名，通常传策略的 <c>Name</c>。</param>
        public static HObject RequireImage(this HObject image, string toolName)
        {
            // 空对象（0 个 object）同样视为没有图像：上游图像类工具失败时会把输出复位成空对象，
            // 放行的话下游要么静默产出空结果，要么在后续算子里炸出与真实原因无关的原生异常。
            if (!image.NotNull() || image.CountObj() == 0)
                throw new InvalidOperationException($"{toolName} : 图像来源为空，无法执行！");

            return image;
        }
    }
}
