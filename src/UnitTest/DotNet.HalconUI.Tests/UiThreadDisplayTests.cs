using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="UiThreadDisplay"/>：后台线程上的显示调用切回控件所在的 UI 线程执行。
    /// </summary>
    [TestClass]
    public class UiThreadDisplayTests
    {
        [TestMethod]
        public void BackgroundCall_RunsOnOwnerThread()
        {
            Sta.Run(() =>
            {
                using (var owner = new Control())
                {
                    GC_KeepHandle(owner);
                    int uiThread = Thread.CurrentThread.ManagedThreadId;
                    var inner = new FakeDisplay();
                    var display = new UiThreadDisplay(inner, owner);

                    var background = Task.Run(() =>
                    {
                        display.Disp(new Point2d(1, 2));
                        return Thread.CurrentThread.ManagedThreadId;
                    });
                    while (!background.IsCompleted) Application.DoEvents();

                    Assert.AreNotEqual(uiThread, background.Result, "前提：调用确实来自后台线程");
                    Assert.AreEqual(1, inner.Points.Count);
                    Assert.AreEqual(uiThread, inner.LastCallThread, "实际绘制必须发生在 UI 线程");
                }
            });
        }

        [TestMethod]
        public void UiThreadCall_RunsInline()
        {
            Sta.Run(() =>
            {
                using (var owner = new Control())
                {
                    GC_KeepHandle(owner);
                    var inner = new FakeDisplay();
                    new UiThreadDisplay(inner, owner).Disp(new Point2d(3, 4));

                    Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, inner.LastCallThread);
                }
            });
        }

        private static void GC_KeepHandle(Control control) => System.GC.KeepAlive(control.Handle);
    }
}
