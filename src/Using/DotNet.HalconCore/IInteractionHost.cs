using DotNet.Drawing;
using HalconDotNet;
using System.Threading.Tasks;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 交互宿主：策略在「画 ROI / 建模板」这类交互里需要宿主提供的全部能力。
    /// </summary>
    /// <remarks>
    /// 取代原来的 <c>IRoiHost</c>：不再暴露整个显示窗口（<see cref="IHDisplay"/> 能换图、清屏），
    /// 也不再带匹配算法族专用的回调（<c>SetModelPara</c> / <c>DrawDone</c>）—— 模板的变化改由
    /// <see cref="ITemplateEditable.TemplateChanged"/> 通知，宿主的模板编辑器自己去读 <see cref="TemplateView"/>。
    /// 宿主用一个适配器类实现本接口，算法层不认识任何控件类型。
    /// <para>交互都在 UI 线程上进行，执行会话忙时宿主不会发起交互。</para>
    /// </remarks>
    public interface IInteractionHost
    {
        /// <summary> 当前图像（建模板用）；借用句柄，不得释放 </summary>
        HObject CurrentImage { get; }

        /// <summary> 交互过程中的提示图形与文字（"新建模板成功！"、取消后重画的旧 ROI…），直接画在显示窗口上，下次重画前有效 </summary>
        IOverlay Feedback { get; }

        /// <summary> 交互式绘制（新建）区域；按 <see cref="CvRegion.Type"/> 分发图元 </summary>
        /// <returns>
        /// 用户右键确认返回 true；取消 / 超时 / 绘制失败返回 false，此时 <paramref name="region"/> 未被改动。
        /// 有副作用的后续操作（如据此重建模板）必须先判断本返回值。
        /// </returns>
        Task<bool> DrawRegionAsync(CvRegion region);

        /// <summary> 交互式修改区域：以现有几何为初值进入编辑；返回值语义同 <see cref="DrawRegionAsync"/> </summary>
        Task<bool> DrawRegionModAsync(CvRegion region);

        /// <summary> 显示一个 ROI，并让显示窗口持续显示它；宿主同时刷新自己的几何读数 </summary>
        void ShowRoi(CvRegion roi);
    }
}
