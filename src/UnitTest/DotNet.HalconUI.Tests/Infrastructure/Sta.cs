using System;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 在 STA 线程上执行测试体。
    /// </summary>
    /// <remarks>
    /// WinForms 控件与 DataBindings 要求 STA；MSTest 2.2.10 没有 <c>[STATestMethod]</c>，
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
                try { body(); }
                catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
            });
            // 被测代码里有 Convert.ToDouble / ToString 之类依赖区域设置的转换，
            // 固定为不变区域，免得在小数点是逗号的机器上结果不同。
            thread.CurrentCulture = CultureInfo.InvariantCulture;
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            thread.Join();

            error?.Throw();
        }
    }
}
