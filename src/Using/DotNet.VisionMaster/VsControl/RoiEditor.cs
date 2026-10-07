using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconCore;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// ROI 编辑器（<see cref="IRoiEditable"/>）："区域设置"页上的形状选择、新建 / 修改区域与几何读数。
    /// </summary>
    internal partial class RoiEditor : UserControl, ICapabilityView
    {
        private readonly EditorContext _context;
        private readonly Dictionary<RadioButton, RectEnum> _shapes;

        // 供 WinForms Designer 使用
        public RoiEditor() : this(null) { }

        internal RoiEditor(EditorContext context)
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            _context = context;
            _shapes = new Dictionary<RadioButton, RectEnum>
            {
                { btn_rectRectangle, RectEnum.Rectangle },
                { btn_rectAffRect,   RectEnum.AffRect },
                { btn_rectCircle,    RectEnum.Circle },
                { btn_rectEllipse,   RectEnum.Ellipse },
                { btn_rectPolygon,   RectEnum.Polygon },
            };
            if (context == null) return;

            // 几何读数跟着显示窗口走: 策略交出 ROI (ShowRoi) 时显示控件发 RoiShown, 策略不碰这些文本框
            context.Display.RoiShown += Display_RoiShown;
            Disposed += (s, e) => context.Display.RoiShown -= Display_RoiShown;
        }

        public void Bind(IParaStrategy tool)
        {
            // 可编辑 ROI 的工具在这之前已经 ShowRoi, 读数已是它的; 其余工具清空, 不留上一个工具的读数
            if (!(tool is IRoiEditable)) ShowRoiInfo(null);
        }

        public void UpdateState()
        {
            // 绘制会改动工具持有的 HObject, 执行会话忙时不能做
            bool idle = _context != null && !_context.IsHostBusy;
            btn_drawRegion.Enabled = idle;
            but_editRegion.Enabled = idle;
        }

        private void Display_RoiShown(object sender, CvRegion region) => ShowRoiInfo(region);

        internal void ShowRoiInfo(CvRegion region)
        {
            txt_Width.Text = region == null ? string.Empty : region.Width.ToString("F2");
            txt_Height.Text = region == null ? string.Empty : region.Height.ToString("F2");
            txt_TopLeft.Text = region == null ? string.Empty : $"{region.TopLeft.X:F2};{region.TopLeft.Y:F2}";
            txt_BottomRight.Text = region == null ? string.Empty : $"{region.BottomRight.X:F2};{region.BottomRight.Y:F2}";
            txt_Center.Text = region == null ? string.Empty : $"{region.Center.X:F2};{region.Center.Y:F2}";
        }

        private RectEnum Shape => _shapes.FirstOrDefault(kv => kv.Key.Checked).Value;

        private void btn_drawRegion_Click(object sender, EventArgs e)
        {
            if (_context == null) return;
            // 新建 ROI 的默认形状由算法自己声明 ([Algo(DefaultRoi = ...)]), 宿主不按算法分支
            var shape = AlgoInfo.Of(_context.Tool)?.DefaultRoi ?? RectEnum.Rectangle;
            var radio = _shapes.FirstOrDefault(kv => kv.Value == shape).Key;
            if (radio != null) radio.Checked = true;
            _context.RunDraw(tool => (tool as IRoiEditable)?.DrawROIAsync(_context.Host, Shape, true));
        }

        private void but_editRegion_Click(object sender, EventArgs e)
            => _context?.RunDraw(tool => (tool as IRoiEditable)?.DrawROIAsync(_context.Host, Shape, false));
    }
}
