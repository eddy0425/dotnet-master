using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconCore;
using DotNet.HalconUI;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 模板编辑器（<see cref="ITemplateEditable"/>）："模版设置"页上的区域形状、新建 / 修改 / 编辑模板与模板缩略图。
    /// </summary>
    /// <remarks>
    /// 模板变了由策略发 <see cref="ITemplateEditable.TemplateChanged"/>，这里读 <see cref="TemplateView"/> 自己画；
    /// 原来是匹配算法回调宿主的 <c>SetModelPara</c> / <c>DrawDone</c>，通用的交互契约被一个算法族绑着。
    /// </remarks>
    internal partial class TemplateEditor : UserControl, ICapabilityView
    {
        private readonly EditorContext _context;
        private readonly Dictionary<RadioButton, RectEnum> _shapes;
        private readonly HModelUI _thumbnail;
        private readonly HEditModelUI _editWindow;
        private ITemplateEditable _watched;

        // 供 WinForms Designer 使用
        public TemplateEditor() : this(null) { }

        internal TemplateEditor(EditorContext context)
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            _context = context;
            _shapes = new Dictionary<RadioButton, RectEnum>
            {
                { btn_modelRectangle, RectEnum.Rectangle },
                { btn_modelAffRect,   RectEnum.AffRect },
                { btn_modelCircle,    RectEnum.Circle },
                { btn_modelEllipse,   RectEnum.Ellipse },
                { btn_modelPolygon,   RectEnum.Polygon },
            };
            if (context == null) return;

            _thumbnail = new HModelUI();
            panel1.Controls.Add(_thumbnail);
            _editWindow = new HEditModelUI();
            // 编辑窗没有父容器, 不会随本控件释放; 挂 Disposed 而不是重写 Dispose(bool): 后者在 Designer 里
            Disposed += (s, e) =>
            {
                Watch(null);
                try { _editWindow.Dispose(); }
                catch (Exception ex) { Log.Warn(nameof(TemplateEditor), "释放模板编辑窗失败.", ex); }
            };
        }

        /// <summary> 模板编辑窗（非模态，没有父容器） </summary>
        internal HEditModelUI EditWindow => _editWindow;

        public void Bind(IParaStrategy tool)
        {
            Watch(tool as ITemplateEditable);
            // 缩略图也要跟着换: 原来只在模板变了才刷新, 切到另一个匹配工具时还显示上一个工具的模板
            if (!(tool is ITemplateEditable template))
            {
                _thumbnail?.ClearModel();
                return;
            }
            var view = template.GetTemplateView();
            ShowOnDisplay(view);
            // 模板图读不出来不该挡住切换工具: 缩略图已清空, 记一笔即可; 主窗口的异常照旧抛出
            try { ShowThumbnail(view); }
            catch (Exception ex) { Log.Warn(nameof(TemplateEditor), "显示模板缩略图失败.", ex); }
        }

        public void UpdateState()
        {
            bool idle = _context != null && !_context.IsHostBusy;
            btn_newModel.Enabled = idle;
            but_modifyModel.Enabled = idle;
            but_editModel.Enabled = idle;
        }

        private RectEnum Shape => _shapes.FirstOrDefault(kv => kv.Key.Checked).Value;

        private void btn_newModel_Click(object sender, EventArgs e)
            => _context?.RunDraw(tool => (tool as ITemplateEditable)?.SetTemplateAsync(_context.Host, Shape, true), commitsData: true);

        private void but_modifyModel_Click(object sender, EventArgs e)
            => _context?.RunDraw(tool => (tool as ITemplateEditable)?.SetTemplateAsync(_context.Host, Shape, false), commitsData: true);

        private void but_editModel_Click(object sender, EventArgs e)
        {
            if (_context == null) return;
            if (_context.IsHostBusy)
            {
                Prompt.Show("流程正在运行，请先停止连续运行再编辑模板。");
                return;
            }
            // 绘制期间不能打开编辑窗：它会把模板区域的句柄交给编辑窗，而待完成的绘制稍后会释放这个旧句柄
            if (_context.IsDrawBusy)
            {
                Prompt.Show("当前正在绘制 ROI / 模板，请先在图像上右键确认或取消后再打开模板编辑窗。");
                return;
            }
            // 编辑窗自己也可能正在绘制(它是非模态的): 再次 DisplayModel 会把它挂起的会话饿死到超时
            if (_editWindow.IsDrawBusy)
            {
                Prompt.Show("模板编辑窗正在绘制区域，请先在图像上右键确认或取消后再打开。");
                return;
            }

            try
            {
                if (!(_context.Tool is ITemplateEditable template)) return;
                var view = template.GetTemplateView();

                // 编辑窗要读模板图, 再按最佳匹配的位姿把模板区域摆回模板图上; 两者缺一个就无从显示
                if (string.IsNullOrEmpty(view.ModelPath) || !File.Exists(view.ModelPath))
                {
                    Prompt.Show("尚未创建模板，请先新建模板。");
                    return;
                }
                if (!view.Best.HasValue)
                {
                    Prompt.Show("最近一次运行没有匹配结果，请先运行并匹配成功后再编辑模板。");
                    return;
                }

                _editWindow.Show();
                _editWindow.DisplayModel(view.ModelPath, view.ModelRegion, view.Contour, view.Best.Value);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        /// <summary> 只听当前工具的模板变化：换工具时退订上一个 </summary>
        private void Watch(ITemplateEditable template)
        {
            if (ReferenceEquals(template, _watched)) return;
            if (_watched != null) _watched.TemplateChanged -= Template_Changed;
            _watched = template;
            if (_watched != null) _watched.TemplateChanged += Template_Changed;
        }

        private void Template_Changed(object sender, EventArgs e)
        {
            if (_context == null || !ReferenceEquals(sender, _context.Tool) || !(sender is ITemplateEditable template)) return;
            try
            {
                var view = template.GetTemplateView();
                ShowOnDisplay(view);
                ShowThumbnail(view);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        /// <summary> 在显示窗口上持续显示查找 ROI + 模板轮廓 + 坐标系 </summary>
        private void ShowOnDisplay(TemplateView view)
        {
            var roi = _context.Tool is IRoiEditable ? _context.Host.ShownRoi?.HoRegion : null;
            _context.Display.SetModelPara(roi, view.Contour, view.Best?.Coord ?? default(CvCoord));
        }

        /// <summary> 刷新模板缩略图；没有模板图或匹配结果时清空，不留上一个工具的模板 </summary>
        private void ShowThumbnail(TemplateView view)
        {
            if (!view.Best.HasValue || string.IsNullOrEmpty(view.ModelPath) || !File.Exists(view.ModelPath))
            {
                _thumbnail.ClearModel();
                return;
            }
            try { _thumbnail.DisplayModel(view.ModelPath, view.ModelRegion, view.Contour, view.Best.Value); }
            catch
            {
                // 清空再失败也只记一笔: 不能盖掉原来读图失败的异常
                try { _thumbnail.ClearModel(); }
                catch (Exception clearEx) { Log.Warn(nameof(TemplateEditor), "清空模板缩略图失败.", clearEx); }
                throw;
            }
        }
    }
}
