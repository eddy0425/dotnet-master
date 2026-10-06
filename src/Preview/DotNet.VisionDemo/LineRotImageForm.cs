using System;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.HalconAlgo;
using System.Windows.Forms;
using System.Collections.Generic;
using DotNet.VisionMaster;


namespace DotNet.VisionDemo
{
    public partial class LineRotImageForm : Form
    {
        HDisplayUI _display;
        ParaForm _formPara;
        private int _index;
        private IParaStrategy _currentStrategy => _strategys[_index];
        private List<IParaStrategy> _strategys = new List<IParaStrategy>();
        private readonly Dictionary<string, VsControlModel> _vsControls = new Dictionary<string, VsControlModel>();


        public LineRotImageForm()
        {
            InitializeComponent();

            _display = new HDisplayUI();
            panel1.Controls.Add(_display);

            _formPara = new ParaForm(_display);
            panel2.Controls.Add(_formPara);

            _strategys.Add(new FileImageStrategy());
            _strategys.Add(new ShapeModelStrategy());
            _strategys.Add(new FitLineStrategy());
            _strategys.Add(new LineRotImageStrategy());
            _strategys.Add(new RotateImageStrategy());

            for (int i = 0; i < _strategys.Count; i++)
            {
                _strategys[i].Init(_display);
                _strategys[i].RunIndex = i;
            }

            var fileImage = ((FileImageStrategy)_strategys[0]).inPara;
            fileImage.ImageFolder = "D:\\testImage\\Blue ring-9030-B";
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SwitchStrategy(0);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SwitchStrategy(1);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SwitchStrategy(2);
        }
        private void button4_Click(object sender, EventArgs e)
        {
            SwitchStrategy(3);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SwitchStrategy(4);
        }
        /// <summary>
        /// 切换算法策略：解绑旧控件 → 清空 → 设置新策略 → 显示新参数
        /// </summary>
        private void SwitchStrategy(int index)
        {
            // 同 MainForm.SwitchStrategy: 绘制期间切换会让会话收不到鼠标事件(卡到超时),
            // 并使 ParaForm 的索引与本窗体脱节。必须在改动任何状态之前拦下来。
            if (_formPara.IsDrawBusy)
            {
                MessageBox.Show("当前正在绘制 ROI / 模板，请先在图像上右键确认或取消后再切换工具。");
                return;
            }

            _index = index;
            _vsControls.ClearAll();
            _formPara.SelectPara(_index, _strategys);
            if (_currentStrategy is IParaBinding binding)
                binding.DispPara(new WinFormsParaUiHost(_formPara, _vsControls));
            if (_currentStrategy is IRoiEditable roi)
                roi.DispROI(_display);
        }

        private void but_Run_Click(object sender, EventArgs e)
        {
            try
            {
                _display.ReDispImage();

                //switch (_strategys[_index].Name)
                //{
                //    case "ShapeMode":
                //        {
                //            _display.SetDrawMode("ShapeMode", DrawEnum.DispModel);
                //        }
                //        break;
                //}

                if (_currentStrategy is IParaBinding binding)
                    binding.SavePara(new WinFormsParaUiHost(_formPara, _vsControls));
                _currentStrategy.Fun_action(_display.Display, _strategys);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void but_Cycle_Click(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < _strategys.Count; i++)
                {
                    _strategys[i].Fun_action(_display.Display, _strategys);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

    }
}
