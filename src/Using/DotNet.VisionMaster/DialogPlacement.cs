using System;
using System.Drawing;
using System.Windows.Forms;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 弹出窗(引用窗 / 工具窗)的摆放位置：贴在主窗右侧，但不能跑出屏幕工作区。
    /// </summary>
    internal static class DialogPlacement
    {
        /// <summary>主窗最大化或没有主窗时，相对工作区左上角的默认偏移。</summary>
        internal static readonly Point Default = new Point(500, 300);

        public static Point Beside(Form owner, Size size)
        {
            bool besideOwner = owner != null && owner.WindowState != FormWindowState.Maximized;
            // 主窗最大化时仍取主窗所在的屏；没有主窗才看光标
            var screen = owner != null ? Screen.FromControl(owner) : Screen.FromPoint(Cursor.Position);
            return Beside(besideOwner ? owner.Bounds : (Rectangle?)null, size, screen.WorkingArea);
        }

        /// <remarks>
        /// 此前总是贴在主窗右侧；主窗贴右边时弹窗整个落在屏幕外，而它是置顶的模态窗，看起来就像程序卡死。
        /// 放不下时贴工作区边缘；先保证左上角可见(标题栏能拖)，再尽量让右下角也在屏幕内。
        /// 没有主窗时 <see cref="Default"/> 按工作区偏移，多显示器下不会落到别的屏上。
        /// </remarks>
        internal static Point Beside(Rectangle? owner, Size size, Rectangle workingArea)
        {
            int left = owner?.Right ?? workingArea.Left + Default.X;
            int top = owner?.Top ?? workingArea.Top + Default.Y;

            int x = Math.Max(workingArea.Left, Math.Min(left, workingArea.Right - size.Width));
            int y = Math.Max(workingArea.Top, Math.Min(top, workingArea.Bottom - size.Height));
            return new Point(x, y);
        }
    }
}
