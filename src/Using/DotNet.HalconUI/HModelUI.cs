using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Windows.Forms;
using DotNet.HalconCore;
using DotNet.HalconRuntime;


namespace DotNet.HalconUI
{
    public partial class HModelUI : UserControl
    {
        HObject _srcImage;
        HObject _modeRect;
        HObject _contour;
        CvCoord _coord;
        readonly HDisplay display;
        readonly HWindowMouse mouse;

        public HModelUI()
        {
            InitializeComponent();
            this.Dock = DockStyle.Fill;
            display = new HDisplay(hWindowControl);
            mouse = new HWindowMouse(hWindowControl, display);

            HOperatorSet.GenEmptyObj(out _srcImage);
            HOperatorSet.GenEmptyObj(out _modeRect);
            HOperatorSet.GenEmptyObj(out _contour);
        }

        /// <remarks>
        /// 新图像与变换结果全部生成成功后才换入字段：原先先 Dispose 旧字段再 ReadImage，
        /// 路径失效时字段停在已释放的句柄上，之后每次鼠标移动都拿它重绘。
        /// </remarks>
        public void DisplayModel(string modelPath, HObject ho_ModeRect, HObject ho_Contour, ModelResult result)
        {
            using (var preview = TemplatePreview.Load(modelPath, ho_ModeRect, ho_Contour, result))
            {
                _coord = preview.Coord;
                preview.Detach(out HObject srcImage, out HObject modeRect, out HObject contour);
                Replace(ref _srcImage, ref srcImage);
                Replace(ref _modeRect, ref modeRect);
                Replace(ref _contour, ref contour);
            }

            // 区域 / 轮廓 / 坐标系交给叠加层，由显示对象在每次重画图像后重放。原来直接画在窗口上：
            // 所在页未选中时画不上，切到页面后只补画了图像，要等鼠标移动（OnMouseMove 重画）才出来。
            // 叠加层先建好再显示：建失败时不换窗口里的图（字段已是新模板，窗口仍停在上一帧）
            var overlay = new OverlayList();
            try
            {
                overlay.Add(_modeRect, DrawStyle.Of(HColor.Blue));
                overlay.Add(_contour, DrawStyle.Of(HColor.Green));
                overlay.Add(_coord, DrawStyle.Of(HColor.OrangeRed));
            }
            catch
            {
                overlay.Dispose();
                throw;
            }
            // 先恢复可见再显示：隐藏时窗口画不了，这一帧会被挂起
            hWindowControl.Visible = true;
            display.ShowFrame(_srcImage, overlay);
        }

        /// <summary> 清空缩略图（工具还没有模板或匹配结果时），下次 <see cref="DisplayModel"/> 再显示 </summary>
        /// <remarks>
        /// 窗口里的图由 <see cref="HWindowImage"/> 持有，尺寸变化时会重画；只清窗会让上一张模板图又冒出来，
        /// 所以直接把窗口藏起来。
        /// </remarks>
        public void ClearModel()
        {
            // 建一个换一个：中途失败时已建好的空对象不会没人接手
            HOperatorSet.GenEmptyObj(out HObject srcImage);
            Replace(ref _srcImage, ref srcImage);
            HOperatorSet.GenEmptyObj(out HObject modeRect);
            Replace(ref _modeRect, ref modeRect);
            HOperatorSet.GenEmptyObj(out HObject contour);
            Replace(ref _contour, ref contour);
            _coord = default(CvCoord);
            // 先藏再清：清叠加层会重画图像，窗口还可见时上一张模板图会闪一下
            hWindowControl.Visible = false;
            display.ClearOverlay();
        }

        /// <summary> 用 <paramref name="value"/> 换下 <paramref name="field"/> 并释放旧对象；<paramref name="value"/> 置空，所有权转入字段。 </summary>
        internal static void Replace(ref HObject field, ref HObject value)
        {
            HObject old = field;
            field = value;
            value = null;
            old?.Dispose();
        }

        /// <summary>
        /// 释放本控件持有的 HALCON 资源，由 <see cref="Dispose(bool)"/> 的 disposing 分支调用。
        /// </summary>
        /// <remarks>
        /// 三个 HObject 字段与显示/鼠标对象原先从未释放：控件反复创建销毁时，
        /// HALCON 侧的非托管句柄会持续累积。
        /// </remarks>
        private void ReleaseDisplayResources()
        {
            try { mouse?.Dispose(); }
            catch (Exception ex) { Log.Error(nameof(HModelUI), "释放鼠标交互资源失败.", ex); }

            try { display?.Dispose(); }
            catch (Exception ex) { Log.Error(nameof(HModelUI), "释放显示资源失败.", ex); }

            try
            {
                _srcImage?.Dispose();
                _modeRect?.Dispose();
                _contour?.Dispose();
            }
            catch (Exception ex) { Log.Error(nameof(HModelUI), "释放图像资源失败.", ex); }
        }
    }
}
