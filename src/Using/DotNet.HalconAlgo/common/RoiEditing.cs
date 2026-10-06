using System.Threading.Tasks;
using DotNet.Drawing;
using DotNet.HalconCore;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 交互式绘制 / 修改一个配置 ROI：所有带本地 ROI 的策略共用（原来 7 个类各抄一份）。
    /// </summary>
    internal static class RoiEditing
    {
        /// <returns>用户确认返回 true；取消 / 超时返回 false，此时 ROI 的几何与类型都保持原样。</returns>
        public static async Task<bool> DrawAsync(IRoiHost host, CvRegion roi, RectEnum type, bool newROI)
        {
            bool confirmed;
            if (newROI)
            {
                // Type 必须在绘制前写入(HDisplay 按它分发图元), 但取消时几何不会被回写,
                // 所以要连 Type 一起还原, 否则 Type 与 HoRegion / 外接框对不上, 还会存进配置.
                var prevType = roi.Type;
                roi.Type = type;
                confirmed = await host.DrawRegionAsync(roi);
                if (!confirmed) roi.Type = prevType;
            }
            else confirmed = await host.DrawRegionModAsync(roi);

            // 这里故意不短路: 取消后仍要把原 ROI 重画回去(宿主事先 ReDispImage 已清屏)
            host.Display.Disp(roi, DrawStyle.Of(HColor.Blue));
            host.SetRectPara(roi);
            return confirmed;
        }
    }
}
