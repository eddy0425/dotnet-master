using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// 在 STA 线程上执行测试体。
    /// </summary>
    /// <remarks>
    /// WinForms 控件要求 STA；MSTest 2.2.10 没有 <c>[STATestMethod]</c>，
    /// 而 vstest 的默认线程是 MTA。异常会原样（保留堆栈）抛回测试线程，Assert 失败照常生效。
    /// </remarks>
    internal static class Sta
    {
        public static void Run(Action body)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                var saved = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                try { body(); }
                finally { Thread.CurrentThread.CurrentCulture = saved; }
                return;
            }

            ExceptionDispatchInfo error = null;
            var thread = new Thread(() =>
            {
                // 窗口过程里的未处理异常默认会被 WinForms 弹成模态对话框，无人值守时整个测试就卡死了；
                // 改为直接抛出，让它像普通异常一样判测试失败。必须在本线程建第一个窗口之前设置。
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException, true);
                try { body(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            });
            thread.CurrentCulture = CultureInfo.InvariantCulture;
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            error?.Throw();
        }
    }
}
