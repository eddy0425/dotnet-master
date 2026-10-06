using DotNet.Drawing;
using HalconDotNet;
using System;
using System.Windows.Forms;
using DotNet.HalconCore;


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

            hWindowControl.HMouseMove += OnMouseMove;
        }

        /// <remarks>
        /// 新图像与变换结果全部生成成功后才换入字段：原先先 Dispose 旧字段再 ReadImage，
        /// 路径失效时字段停在已释放的句柄上，之后每次鼠标移动都拿它重绘。
        /// </remarks>
        public void DisplayModel(string modelPath, HObject ho_ModeRect, HObject ho_Contour, ModelResult result)
        {
            hWindowControl.Focus();

            using (var preview = TemplatePreview.Load(modelPath, ho_ModeRect, ho_Contour, result))
            {
                _coord = preview.Coord;
                preview.Detach(out HObject srcImage, out HObject modeRect, out HObject contour);
                Replace(ref _srcImage, ref srcImage);
                Replace(ref _modeRect, ref modeRect);
                Replace(ref _contour, ref contour);
            }

            display.DispImage(_srcImage);
            display.Disp(_modeRect, DrawStyle.Of(HColor.Blue));
            display.Disp(_contour, DrawStyle.Of(HColor.Green));
            display.Disp(_coord, DrawStyle.Of(HColor.Red));
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
            hWindowControl.HMouseMove -= OnMouseMove;

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

        public void OnMouseMove(object sender, HMouseEventArgs e)
        {
            display.Disp(_modeRect, DrawStyle.Of(HColor.Blue));
            display.Disp(_contour, DrawStyle.Of(HColor.Green));
            display.Disp(_coord, DrawStyle.Of(HColor.OrangeRed));
        }

    }
}
