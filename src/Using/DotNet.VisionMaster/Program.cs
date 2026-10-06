using System;
using System.IO;
using System.Windows.Forms;
using DotNet.Logging;

namespace DotNet.VisionMaster
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 初始化应用日志，并在退出时排空待写入的日志。
            Log.Initialize(b => b.WriteToFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"), "VisionMaster"));
            // 库层 (HalconCore / HalconAlgo / HalconUI / 插件) 走 DotNet.Drawing.Log, 默认只写 Trace; 接到应用日志上才会落盘
            DotNet.Drawing.Log.Current = new DrawingLogBridge();
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            finally
            {
                // 排空异步队列里还没写盘的日志
                Log.Shutdown();
            }
        }
    }
}
