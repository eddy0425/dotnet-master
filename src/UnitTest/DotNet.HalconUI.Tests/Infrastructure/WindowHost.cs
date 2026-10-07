using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using HalconDotNet;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// 承载一个真实 <see cref="HWindowControl"/> 的不可见窗体，供 HDisplay / HWindowImage / HWindowMouse 使用。
    /// </summary>
    /// <remarks>
    /// 必须在 STA 线程上创建（见 <see cref="Sta"/>）。窗体显示在屏幕外，
    /// 使 <see cref="HWindowControl.HalconWindow"/> 可用；控件放在一个固定大小的 <see cref="Panel"/> 里，
    /// 对应产品里 HDisplayUI 的布局，<c>LayoutControlToImage</c> 按这个父容器居中缩放。
    /// </remarks>
    internal sealed class WindowHost : Form
    {
        public WindowHost(int parentWidth = 400, int parentHeight = 300)
        {
            ShowInTaskbar = false;
            ClientSize = new Size(parentWidth + 20, parentHeight + 20);

            Panel = new Panel { Location = new Point(0, 0), Size = new Size(parentWidth, parentHeight) };
            // 与 HDisplayUI.Designer 一致：四边锚定，父容器缩放时控件跟着触发 Resize
            Control = new HWindowControl
            {
                Location = new Point(0, 0),
                Size = new Size(parentWidth, parentHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            };
            Panel.Controls.Add(Control);
            Controls.Add(Panel);

            ShowOffscreen(this);
        }

        /// <summary>
        /// 把窗体显示在屏幕外。
        /// </summary>
        /// <remarks>
        /// HWindowImage.CanDraw 要求控件 Visible（取的是含父链的实际可见性），只建句柄不够；
        /// 放到屏幕外显示，既满足可见性又不打扰桌面。
        /// </remarks>
        public static void ShowOffscreen(Form form)
        {
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.FormBorderStyle = FormBorderStyle.None;
            form.Show();
            Application.DoEvents();
        }

        /// <summary>把用户控件放进一个屏幕外的窗体里显示，调用方负责 Dispose 返回的窗体。</summary>
        public static Form ShowOffscreen(Control control, int width = 400, int height = 300)
        {
            var form = new Form { ClientSize = new Size(width, height) };
            control.Dock = DockStyle.Fill;
            form.Controls.Add(control);
            ShowOffscreen(form);
            return form;
        }

        /// <summary>
        /// 泵消息直到任务完成。
        /// </summary>
        /// <remarks>
        /// 窗体一建出来 WinForms 就在当前线程装上了 SynchronizationContext，
        /// 绘制会话确认后 await 的续体是 Post 回消息队列的，不泵就永远不会跑。
        /// </remarks>
        public static void Pump(Task task, int timeoutMs = 5000)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                if (watch.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException("等待任务完成超时.");
                Application.DoEvents();
                Thread.Sleep(1);
            }
            Application.DoEvents();
        }

        /// <summary> 泵消息直到条件成立 </summary>
        public static void PumpUntil(Func<bool> done, int timeoutMs = 5000)
        {
            var watch = Stopwatch.StartNew();
            while (!done())
            {
                if (watch.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException("等待条件成立超时.");
                Application.DoEvents();
                Thread.Sleep(1);
            }
            Application.DoEvents();
        }

        /// <summary>泵一轮消息，让已 Post 的续体跑完。</summary>
        public static void Pump()
        {
            for (int i = 0; i < 5; i++) Application.DoEvents();
        }

        public Panel Panel { get; }
        public HWindowControl Control { get; }
        public HWindow Window => Control.HalconWindow;

        /// <summary>在 STA 线程上建宿主并执行测试体，结束后释放。</summary>
        public static void Run(Action<WindowHost> body, int parentWidth = 400, int parentHeight = 300)
        {
            Sta.Run(() =>
            {
                using (var host = new WindowHost(parentWidth, parentHeight))
                {
                    body(host);
                }
            });
        }
    }
}
