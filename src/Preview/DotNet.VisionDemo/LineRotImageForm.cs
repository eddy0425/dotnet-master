using System;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.HalconRuntime;
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
                _strategys[i].Init(new DisplayInteractionHost(_display));
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
            _formPara.ShowTool(_currentStrategy, _strategys);
        }

        private void but_Run_Click(object sender, EventArgs e)
        {
            try
            {
                var step = new FlowRunner(_strategys).RunStep(_index, _display.Display.HoImage);
                _display.ShowResult(step.Image, step.TakeOverlay());
                if (step.Result.Status == RunStatus.Error) MessageBox.Show(step.Result.Message);
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
                var result = new FlowRunner(_strategys).Run(_display.Display.HoImage);
                _display.ShowResult(result.Image, result.TakeOverlay());
                var error = result.FirstError;
                if (error != null) MessageBox.Show($"{error.Tool.Name}: {error.Result.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

    }
}
