using DotNet.Drawing;
using HalconDotNet;
using System;

namespace DotNet.HalconUI.Draw
{
    /// <summary>
    /// 交互绘图流程里的"吞异常"入口集中地。
    /// </summary>
    /// <remarks>
    /// 这些吞异常并非可有可无：调用点要么跑在鼠标事件回调里 (约 100Hz)，要么跑在 finally 的清理
    /// 路径上，抛出去只会把一个可忽略的竞态 (窗口在拖拽中被销毁) 变成崩溃，或吞掉正在传播的原始异常。
    /// 但"不外抛"不等于"不记录"——统一从这里走，理由写在一处，日志级别按噪音程度选择。
    /// </remarks>
    internal static class DrawSafe
    {
        /// <summary>所有交互绘图相关日志共用的分类名，改名会影响现场日志检索，谨慎调整。</summary>
        internal const string Category = "DrawHelper";

        /// <summary>
        /// 清理路径专用：释放 Halcon 对象，失败只记 Debug 日志。
        /// </summary>
        /// <remarks>
        /// 调用点全部在 finally / 覆盖旧句柄的位置。此时要么主流程已成功，
        /// 要么已有异常正在向外传播——释放失败再抛一次会把真正的错误覆盖掉。
        /// </remarks>
        internal static void Dispose(HObject obj)
        {
            if (obj == null) return;
            try { obj.Dispose(); }
            catch (Exception ex) { Log.Debug(Category, $"释放 HObject 失败(已忽略): {ex.Message}"); }
        }

        /// <summary>
        /// 窗口状态操作专用：进入/退出交互会话时的双缓冲开关与状态还原，失败只记 Debug 日志。
        /// </summary>
        internal static void WindowOp(string operation, Action action)
        {
            try { action(); }
            catch (Exception ex) { Log.Debug(Category, $"{operation} 失败(已忽略): {ex.Message}"); }
        }
    }
}
