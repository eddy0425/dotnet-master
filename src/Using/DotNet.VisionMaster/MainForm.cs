using System;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.HalconAlgo;
using System.Windows.Forms;
using System.Collections.Generic;


namespace DotNet.VisionMaster
{
    public partial class MainForm : Form
    {
        HDisplayUI _display;
        ParaForm _formPara;
        private int _index;
        private IParaStrategy _currentStrategy => _strategys[_index];
        private List<IParaStrategy> _strategys = new List<IParaStrategy>();
        private readonly Dictionary<string, VsControlModel> _vsControls = new Dictionary<string, VsControlModel>();

        public MainForm()
        {
            InitializeComponent();

            _display = new HDisplayUI();
            panel1.Controls.Add(_display);

            _formPara = new ParaForm(_display);
            panel2.Controls.Add(_formPara);

            _strategys.Add(new FileImageStrategy());
            _strategys.Add(new CreateROIStrategy());
            _strategys.Add(new ShapeModelStrategy());
            _strategys.Add(new FitLineStrategy());
            _strategys.Add(new FitArcMidpointStrategy());
            _strategys.Add(new NccModelStrategy());
            _strategys.Add(new ScaledModelStrategy());
            _strategys.Add(new GenericModelStrategy());

            for (int i = 0; i < _strategys.Count; i++)
            {
                _strategys[i].Init(_display);
                // 模板图目录按 RunIndex 区分: 不赋值时 4 个匹配工具的模板图都写进 JobDir/0/, 互相覆盖
                _strategys[i].RunIndex = i;
            }

            // 挂 Disposed 而不是重写 Dispose(bool): 后者已在 Designer 里定义。
            // 此时子控件(含 _display)都已销毁, 不会再有绘制去碰策略持有的句柄。
            Disposed += MainForm_Disposed;
        }

        /// <summary>
        /// 释放持有 HALCON 句柄的策略。
        /// </summary>
        /// <remarks>逐个释放，避免一个策略失败影响其余策略。</remarks>
        private void MainForm_Disposed(object sender, EventArgs e)
        {
            foreach (var disposable in _strategys.OfType<IDisposable>())
            {
                try { disposable.Dispose(); }
                catch (Exception ex) { Log.Warn(nameof(MainForm), $"释放策略 {disposable.GetType().Name} 失败.", ex); }
            }
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
        private void button6_Click(object sender, EventArgs e)
        {
            SwitchStrategy(5);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            SwitchStrategy(6);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SwitchStrategy(7);
        }

        /// <summary>
        /// 切换算法策略：解绑旧控件 → 清空 → 设置新策略 → 显示新参数
        /// </summary>
        private void SwitchStrategy(int index)
        {
            // 绘制未结束就切换会出两件事: 下面的 DispROI → SetRectPara 会改写 DrawType,
            // 进行中的绘制会话从此收不到鼠标事件、一直卡到 5 分钟超时; 且本窗体已经切到新策略,
            // ParaForm 的索引却停在旧的, 之后 ParaForm 上的操作会静默作用到错误的策略上。
            // 所以必须在改动任何状态之前拦下来。
            if (_formPara.IsDrawBusy)
            {
                Prompt.Show("当前正在绘制 ROI / 模板，请先在图像上右键确认或取消后再切换工具。");
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

                if (_currentStrategy is IParaBinding binding)
                    binding.SavePara(new WinFormsParaUiHost(_formPara, _vsControls));
                // 失败原因由策略画在屏幕上; 这里至少留一条日志, 不再整个丢弃返回值
                if (!_currentStrategy.Fun_action(_display.Display, _strategys))
                    Log.Warn(nameof(MainForm), $"工具 '{_currentStrategy.Name}' 运行未成功.");
            }
            catch (Exception ex)
            {
                Prompt.Show(ex.Message);
            }
        }

        
    }
}
