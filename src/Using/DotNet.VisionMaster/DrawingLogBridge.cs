using System;
using DrawingLog = DotNet.Drawing;
using AppLog = DotNet.Logging;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 把库层日志（<see cref="DrawingLog.Log"/>）转发到应用日志（<see cref="AppLog.Log"/>）。
    /// </summary>
    /// <remarks>
    /// HalconCore / HalconAlgo / HalconUI 以及外置插件只认 <c>DotNet.Drawing.Log</c> 这层抽象，
    /// 它默认只写 Trace；不在宿主启动时接上这座桥，库里的日志就一条都进不了文件。
    /// 库层不直接引用 DotNet.Logging：插件契约只依赖 Drawing + HalconCore。
    /// </remarks>
    internal sealed class DrawingLogBridge : DrawingLog.ILogger
    {
        public void Log(DrawingLog.LogLevel level, string category, string message, Exception exception)
        {
            AppLog.Log.Logger?.Write(Map(level), category, message, exception);
        }

        internal static AppLog.LogLevel Map(DrawingLog.LogLevel level)
        {
            switch (level)
            {
                case DrawingLog.LogLevel.Debug: return AppLog.LogLevel.Debug;
                case DrawingLog.LogLevel.Info: return AppLog.LogLevel.Information;
                case DrawingLog.LogLevel.Warn: return AppLog.LogLevel.Warning;
                default: return AppLog.LogLevel.Error;
            }
        }
    }
}
