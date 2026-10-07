using DotNet.Drawing;
using DotNet.HalconUI;
using DotNet.HalconCore;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;


namespace DotNet.VisionMaster
{
    /// <summary>
    /// 工具参数页：按策略声明的参数生成面板，承载 ROI / 模板这两类固定交互，以及取消编辑 / 运行测试 / 保存参数的编辑会话。
    /// </summary>
    /// <remarks>
    /// 本类只认识 <see cref="IParaStrategy"/> 与能力接口（<see cref="IParaBinding"/>、<see cref="IRoiEditable"/>、
    /// <see cref="ITemplateEditable"/>），不认识任何具体算法：新增算法不需要改这里，也不需要改 Designer。
    /// </remarks>
    public partial class ParaForm : UserControl
    {
        private readonly HDisplayUI _display;
        private HModelUI _hModel;
        private ValueForm _valueForm;
        private HEditModelUI _editModel;

        private IParaStrategy _tool;
        private IReadOnlyList<IParaStrategy> _flow = new IParaStrategy[0];

        private Dictionary<RadioButton, RectEnum> _rectDrawMap;
        private Dictionary<RadioButton, RectEnum> _modelDrawMap;
        private Dictionary<TabPageEnum, TabPage> _pages;
        private Dictionary<TabPageEnum, ParamPanel> _panels;

        /// <summary>
        /// 交互绘制是否正在进行中，用于挡住重入。
        /// </summary>
        /// <remarks>
        /// 绘制入口是 <c>async void</c>，<c>await</c> 期间 UI 线程继续泵消息，用户完全可以再点另一个按钮：
        /// 新的绘制会 <c>CancelDraw</c> 掉上一次的会话，两条协程交叉读写同一个策略对象与模板句柄。
        /// <para>
        /// <b>不变式</b>：同一 <c>HDisplayUI</c> 上任何时刻只能有一条绘制在飞。<c>DrawSession.Finish</c> 的
        /// <c>TrySetResult</c> 会<b>就地内联</b>旧会话 await 之后的代码，若允许 busy 时再发起绘制，
        /// 旧续体的 <c>finally</c> 就会在新会话开始之前把闸门清掉；<see cref="_drawEpoch"/> 让每一轮只清自己那一轮。
        /// </para>
        /// </remarks>
        private bool _drawBusy;
        private int _drawEpoch;

        /// <summary> 编辑会话的起点：进入工具或保存参数时拍下；取消编辑回到这里。null 表示不支持撤销 </summary>
        private JToken _snapshot;
        private bool _dirty;
        private bool _hostBusy;

        public ParaForm(HDisplayUI displayUI)
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            _display = displayUI;

            _pages = new Dictionary<TabPageEnum, TabPage>
            {
                { TabPageEnum.FileImage, tabFile },
                { TabPageEnum.Parameter, tabParam },
                { TabPageEnum.Region, tabRegion },
                { TabPageEnum.Matching, tabMatching },
                { TabPageEnum.Display, tabDisplay },
            };
            _panels = new Dictionary<TabPageEnum, ParamPanel>
            {
                { TabPageEnum.FileImage, filePanel },
                { TabPageEnum.Parameter, paramPanel },
                { TabPageEnum.Region, regionPanel },
                { TabPageEnum.Matching, matchingPanel },
                { TabPageEnum.Display, displayPanel },
            };
            foreach (var panel in _panels.Values)
            {
                panel.SourcePicker = PickSource;
                panel.SourceFormatter = source => _flow.Describe(source);
                panel.Committed += Panel_Committed;
            }

            _rectDrawMap = new Dictionary<RadioButton, RectEnum>
            {
                { btn_rectRectangle, RectEnum.Rectangle },
                { btn_rectAffRect,   RectEnum.AffRect },
                { btn_rectCircle,    RectEnum.Circle },
                { btn_rectEllipse,   RectEnum.Ellipse },
                { btn_rectPolygon,   RectEnum.Polygon },
            };
            _modelDrawMap = new Dictionary<RadioButton, RectEnum>
            {
                { btn_modelRectangle, RectEnum.Rectangle },
                { btn_modelAffRect,   RectEnum.AffRect },
                { btn_modelCircle,    RectEnum.Circle },
                { btn_modelEllipse,   RectEnum.Ellipse },
                { btn_modelPolygon,   RectEnum.Polygon },
            };

            tabControl1.TabPages.Clear();
            _display.RoiShown += Display_RoiShown;
            _display.DrawDoneEvent += DrawDoneEvent;
        }

        private void ParaForm_Load(object sender, EventArgs e)
        {
            _hModel = new HModelUI();
            _editModel = new HEditModelUI();
            _valueForm = new ValueForm(FindForm());
            panel1.Controls.Add(_hModel);

            // _editModel / _valueForm 没有父容器, Designer 的 Dispose(bool) 不会释放它们;
            // 挂 HandleDestroyed 而不是重写 Dispose(bool): 后者已在 Designer 里定义。
            HandleDestroyed += ParaForm_HandleDestroyed;
        }

        /// <summary>
        /// 释放 <see cref="ParaForm_Load"/> 里自行 new、又没挂进控件树的两个窗体。
        /// </summary>
        /// <remarks>
        /// <b>必须判 <see cref="Control.Disposing"/></b>：句柄重建同样会触发本事件，那时控件还活着。
        /// 也正因为句柄可能重建，这里<b>不能</b>顺手退订本事件。
        /// </remarks>
        private void ParaForm_HandleDestroyed(object sender, EventArgs e)
        {
            if (!Disposing && !IsDisposed) return;

            _display.RoiShown -= Display_RoiShown;
            _display.DrawDoneEvent -= DrawDoneEvent;

            try { _editModel?.Dispose(); }
            catch (Exception ex) { Log.Warn(nameof(ParaForm), "释放模板编辑窗失败.", ex); }

            try { _valueForm?.Dispose(); }
            catch (Exception ex) { Log.Warn(nameof(ParaForm), "释放来源选择窗失败.", ex); }
        }

        /// <summary> 当前显示的工具；还没选过时为 null </summary>
        public IParaStrategy Tool => _tool;

        /// <summary>
        /// 交互绘制是否正在进行中。宿主在切换工具<b>之前</b>必须先问一次：
        /// 绘制期间切换工具会改写鼠标模式，进行中的会话就此收不到鼠标事件，一直卡到超时。
        /// </summary>
        public bool IsDrawBusy => _drawBusy;

        /// <summary> 运行当前工具；由宿主注入，未注入时"运行测试"不可用 </summary>
        public Action TestRunner
        {
            get => _testRunner;
            set { _testRunner = value; UpdateActions(); }
        }
        private Action _testRunner;

        /// <summary> 宿主正在运行（例如连续运行）：期间不允许运行测试、取消或保存参数 </summary>
        public bool HostBusy
        {
            get => _hostBusy;
            set { _hostBusy = value; UpdateActions(); }
        }

        /// <summary> 自进入本工具或上次保存参数以来，参数 / ROI 是否可能被改过 </summary>
        public bool IsDirty => _dirty;

        /// <summary>
        /// 显示一个工具：按它声明的参数生成各页面板，并把它的 ROI 交给显示窗口。
        /// </summary>
        /// <param name="flow">整个流程；来源选择只列出本工具之前的工具，来源的显示名也按它拼出。</param>
        /// <remarks> 换了工具才开始新一轮编辑；同一工具重新显示（改名、取消编辑）保留编辑起点 </remarks>
        public void ShowTool(IParaStrategy tool, IReadOnlyList<IParaStrategy> flow)
        {
            bool switched = !ReferenceEquals(tool, _tool);
            _tool = tool;
            _flow = flow ?? new IParaStrategy[0];

            var items = (tool as IParaBinding)?.DescribeParams() ?? new ParamItem[0];
            foreach (var pair in _panels)
                pair.Value.Bind(items.Where(i => i.Tab == pair.Key));

            tabControl1.TabPages.Clear();
            foreach (TabPageEnum tab in Enum.GetValues(typeof(TabPageEnum)))
            {
                bool show = items.Any(i => i.Tab == tab)
                    || (tab == TabPageEnum.Region && tool is IRoiEditable)
                    || (tab == TabPageEnum.Matching && tool is ITemplateEditable);
                if (show) tabControl1.TabPages.Add(_pages[tab]);
            }
            if (tabControl1.TabPages.Count > 0) tabControl1.SelectedIndex = 0;

            ShowRoiInfo(null);
            if (tool is IRoiEditable roi) roi.DispROI(_display);
            else _display.SetNonePara();

            if (switched) BeginEdit();
        }

        /// <summary> 当前显示的参数项（各页合并），测试与宿主定位用 </summary>
        internal IEnumerable<ParamItem> ParamItems => _panels.Values.SelectMany(p => p.Items);

        internal ParamPanel PanelOf(TabPageEnum tab) => _panels[tab];

        /// <summary> 本工具之前的工具 </summary>
        internal IReadOnlyList<IParaStrategy> Upstream()
        {
            int index = -1;
            for (int i = 0; i < _flow.Count; i++)
            {
                if (ReferenceEquals(_flow[i], _tool)) { index = i; break; }
            }
            return index < 0 ? new IParaStrategy[0] : (IReadOnlyList<IParaStrategy>)_flow.Take(index).ToList();
        }

        private SourceRef? PickSource(SourceParam param)
        {
            try
            {
                return _valueForm.Pick(Upstream(), param);
            }
            catch (Exception ex)
            {
                Prompt.Show(ex.Message);
                return null;
            }
        }

        private void Panel_Committed(object sender, ParamsCommittedEventArgs e)
        {
            try
            {
                (_tool as IParaBinding)?.ParamsChanged(e.Changed);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
            MarkDirty();
        }

        #region 编辑会话

        /// <summary> 以工具当前的参数为起点开始一轮编辑 </summary>
        private void BeginEdit()
        {
            try { _snapshot = FlowScheme.CapturePara(_tool); }
            catch (Exception ex)
            {
                // 拍不了快照只是不能撤销, 不影响编辑本身
                _snapshot = null;
                Log.Warn(nameof(ParaForm), $"工具 '{_tool?.Name}' 的参数快照失败, 取消编辑不可用.", ex);
            }
            _dirty = false;
            UpdateActions();
        }

        private void MarkDirty()
        {
            _dirty = true;
            UpdateActions();
        }

        private bool IsIdle => _tool != null && !_drawBusy && !_hostBusy;
        private bool CanCancel => IsIdle && _dirty && _snapshot != null;
        private bool CanSave => IsIdle && _dirty;
        private bool CanRunTest => IsIdle && _testRunner != null;

        private void UpdateActions()
        {
            if (IsDisposed) return;
            btn_cancelEdit.Enabled = CanCancel;
            btn_saveEdit.Enabled = CanSave;
            btn_runTest.Enabled = CanRunTest;
        }

        /// <summary>
        /// 回到编辑起点。快照里已经是完整的配置（含示教态），所以<b>不</b>调用 <see cref="IParaBinding.ParamsChanged"/>：
        /// 那会让策略把刚还原的示教态又清掉。
        /// 模板绘制会写盘、替换模型句柄，无法撤销；它结束时已把编辑起点挪到当前（见 <see cref="RunDraw"/>）。
        /// </summary>
        internal void CancelEdit()
        {
            if (!CanCancel) return;
            try
            {
                FlowScheme.RestorePara(_tool, _snapshot);
                ShowTool(_tool, _flow);
                _dirty = false;
                UpdateActions();
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        /// <summary> 确认当前修改：之后取消编辑回到这里。写盘仍由宿主的方案保存负责 </summary>
        internal void SaveEdit()
        {
            if (!CanSave) return;
            BeginEdit();
        }

        internal void RunTest()
        {
            if (!CanRunTest) return;
            try { _testRunner(); }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private void btn_cancelEdit_Click(object sender, EventArgs e) => CancelEdit();

        private void btn_saveEdit_Click(object sender, EventArgs e) => SaveEdit();

        private void btn_runTest_Click(object sender, EventArgs e) => RunTest();

        #endregion

        #region ROI 读数

        private void Display_RoiShown(object sender, CvRegion region) => ShowRoiInfo(region);

        /// <summary> Region 页的几何读数由宿主自己填，策略不再碰这些控件 </summary>
        private void ShowRoiInfo(CvRegion region)
        {
            txt_Width.Text = region == null ? string.Empty : region.Width.ToString("F2");
            txt_Height.Text = region == null ? string.Empty : region.Height.ToString("F2");
            txt_TopLeft.Text = region == null ? string.Empty : $"{region.TopLeft.X:F2};{region.TopLeft.Y:F2}";
            txt_BottomRight.Text = region == null ? string.Empty : $"{region.BottomRight.X:F2};{region.BottomRight.Y:F2}";
            txt_Center.Text = region == null ? string.Empty : $"{region.Center.X:F2};{region.Center.Y:F2}";
        }

        #endregion

        #region 绘制

        private void btn_drawRegion_Click(object sender, EventArgs e)
        {
            // 新建 ROI 的默认形状由算法自己声明 ([Algo(DefaultRoi = ...)]), 宿主不再按算法分支
            var shape = AlgoInfo.Of(_tool)?.DefaultRoi ?? RectEnum.Rectangle;
            var radio = _rectDrawMap.FirstOrDefault(kv => kv.Value == shape).Key;
            if (radio != null) radio.Checked = true;
            RunDraw(tool => (tool as IRoiEditable)?.DrawROIAsync(_display, Checked(_rectDrawMap), true));
        }

        private void but_editRegion_Click(object sender, EventArgs e)
            => RunDraw(tool => (tool as IRoiEditable)?.DrawROIAsync(_display, Checked(_rectDrawMap), false));

        private void btn_newModel_Click(object sender, EventArgs e)
            => RunDraw(tool => (tool as ITemplateEditable)?.SetTemplateAsync(_display, Checked(_modelDrawMap), true), commitsData: true);

        private void but_modifyModel_Click(object sender, EventArgs e)
            => RunDraw(tool => (tool as ITemplateEditable)?.SetTemplateAsync(_display, Checked(_modelDrawMap), false), commitsData: true);

        private static RectEnum Checked(Dictionary<RadioButton, RectEnum> map)
            => map.FirstOrDefault(kv => kv.Key.Checked).Value;

        /// <summary>
        /// 4 个绘制入口共用：闸门 → 清屏 → 交给策略 → 异常提示 → 只清自己那一轮的闸门。
        /// </summary>
        /// <param name="commitsData">
        /// 绘制会改写数据目录里的文件（模板）：文件与模型句柄不在快照里、撤销不了，
        /// 还原参数只会得到"新模型 + 旧示教点"，所以结束后直接以当前状态为新的编辑起点。
        /// </param>
        private async void RunDraw(Func<IParaStrategy, Task> draw, bool commitsData = false)
        {
            if (_drawBusy || _tool == null) return;
            _drawBusy = true;
            int epoch = ++_drawEpoch;
            UpdateActions();
            try
            {
                _display.ReDispImage();
                var task = draw(_tool);
                if (task != null) await task;
            }
            catch (Exception ex)
            {
                // 这里可能是在显示控件 Dispose 里被 CancelDraw 内联唤醒的: 此时弹模态框会开一个消息循环,
                // 把控件销毁整个卡住。查 _display.IsReleasing 而不是本控件的 IsDisposed/Disposing ——
                // 那一刻两者都还没置位。
                if (!_display.IsReleasing && !IsDisposed && !Disposing) Prompt.Show(ex.Message);
                else Log.Warn(nameof(ParaForm), "绘制异常(显示控件正在释放, 不弹框).", ex);
            }
            finally
            {
                if (_drawEpoch == epoch)
                {
                    _drawBusy = false;
                    if (commitsData) BeginEdit();
                    // 右键取消、绘制失败也可能已经改过 ROI (策略自己负责回滚, 但宿主分不清), 一律当作改过
                    else MarkDirty();
                }
            }
        }

        #endregion

        #region 模板

        private void but_editModel_Click(object sender, EventArgs e)
        {
            // 绘制期间不能打开编辑窗：它会把模板区域的句柄交给 _editModel，而待完成的绘制稍后会释放这个旧句柄
            if (_drawBusy)
            {
                Prompt.Show("当前正在绘制 ROI / 模板，请先在图像上右键确认或取消后再打开模板编辑窗。");
                return;
            }
            // 编辑窗自己也可能正在绘制(它是非模态的): 再次 DisplayModel 会把它挂起的会话饿死到超时
            if (_editModel.IsDrawBusy)
            {
                Prompt.Show("模板编辑窗正在绘制区域，请先在图像上右键确认或取消后再打开。");
                return;
            }

            try
            {
                if (!(_tool is ITemplateEditable template)) return;
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

                _editModel.Show();
                _editModel.DisplayModel(view.ModelPath, view.ModelRegion, view.Contour, view.Best.Value);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private void DrawDoneEvent(object sender, DrawModelUIArgs e)
        {
            // 只有模板工具会发起模板绘制; 其它工具在场时收到的完成通知与本页无关
            if (!(_tool is ITemplateEditable)) return;
            try
            {
                _hModel?.DisplayModel(e.ModelPath, e.HoModeRect, e.HoContour, e.Result);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        #endregion
    }
}
