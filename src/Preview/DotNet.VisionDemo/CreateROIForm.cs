using DotNet.HalconAlgo;
using DotNet.HalconUI;
using DotNet.HalconCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DotNet.VisionMaster;

namespace DotNet.VisionDemo
{
    public partial class CreateROIForm : Form
    {
        HDisplayUI _display;
        ParaForm _formPara;
        private int _index;
        private IParaStrategy _currentStrategy => _strategys[_index];
        private List<IParaStrategy> _strategys = new List<IParaStrategy>();


        public CreateROIForm()
        {
            InitializeComponent();

            _display = new HDisplayUI();
            panel1.Controls.Add(_display);

            _formPara = new ParaForm(_display);
            panel2.Controls.Add(_formPara);

            string path1 = Path.Combine("D:\\", "Recipes", "Data", "Pick", "mapVision1.json");
            var shapeModel = new ShapeModelStrategy();
            DotNet.Data.JsonHelper.Load(path1, out shapeModel);

            _strategys.Add(new FileImageStrategy());
            _strategys.Add(shapeModel);
            _strategys.Add(new CreateROIStrategy());
            _strategys.Add(new FitLineStrategy());

            for (int i = 0; i < _strategys.Count; i++)
            {
                _strategys[i].Init(_display);
            }

            var fileImage = ((FileImageStrategy)_strategys[0]).inPara;
            fileImage.ImageFolder = "D:\\testImage\\123";

            //ShapeModelStrategy strategy1 = (ShapeModelStrategy)_strategys[1];
            //strategy1.inPara = shapeModel;
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
                _display.ReDispImage();
                var step = new FlowRunner(_strategys).RunStep(_index, _display.Display.HoImage, _display.Display);
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
                var error = new FlowRunner(_strategys).Run(_display.Display.HoImage, _display.Display).FirstError;
                if (error != null) MessageBox.Show($"{error.Tool.Name}: {error.Result.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void button4_old(object sender, EventArgs e)
        {
            string path1 = Path.Combine("D:\\", "Recipes", "Data", "Pick", "mapVision1.json");

            var shapeModelStrategy = _strategys[1] as ShapeModelStrategy;

            DotNet.Data.JsonHelper.Save(path1, shapeModelStrategy);
        }
    }
}
