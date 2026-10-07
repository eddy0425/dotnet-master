using System;
using System.IO;
using System.Linq;
using System.Drawing;
using DotNet.Drawing;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.VisionRuntime;
using DotNet.HalconAlgo;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace DotNet.VisionMaster
{
    /// <summary>
    /// 宿主主窗：持有流程（工具列表）、参数页、显示窗口，负责运行与方案读写。
    /// </summary>
    /// <remarks>
    /// 只认识 <see cref="AlgoCatalog"/>、<see cref="IParaStrategy"/> 与能力接口，不认识任何具体算法：
    /// 可添加的工具来自目录扫描（内置 HalconAlgo + <c>plugins\</c>），新增算法不需要改这里。
    /// 界面只是三块容器，内容在运行时嵌入：显示窗口、下方页面（参数页 / 信息窗口，同一时间只显示一页）、流程窗口（<see cref="JobForm"/>）。
    /// </remarks>
    public partial class MainForm : Form
    {
        private readonly HDisplayUI _display;
        private readonly ParaForm _formPara;
        private readonly InfoForm _formInfo;
        private readonly ToolForm _formTool;
        private readonly JobForm _formJob;
        private readonly FlowSession _session;
        // 连续运行: 会话线程交出的最新一帧, 等 UI 线程取走显示; UI 跟不上时中间帧直接丢弃, 不排队
        private FlowFrame _latestFrame;
        private int _renderPosted;
        // 已提交、还没在 UI 线程上显示完的单次运行
        private int _runsInFlight;
        private IReadOnlyList<FlowIssue> _loopIssues = new FlowIssue[0];
        private readonly List<IParaStrategy> _tools = new List<IParaStrategy>();
        private readonly HashSet<Guid> _invalid = new HashSet<Guid>();
        private int _index = -1;

        public MainForm() : this(LoadCatalog(), createDefaultFlow: true) { }

        /// <param name="catalog">可添加的算法。</param>
        /// <param name="createDefaultFlow">为 true 时每种算法各建一个工具，作为初始流程。</param>
        internal MainForm(AlgoCatalog catalog, bool createDefaultFlow)
        {
            InitializeComponent();
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            _display = new HDisplayUI();
            panel1.Controls.Add(_display);

            _session = new FlowSession(_tools);
            _session.FrameCompleted += Session_FrameCompleted;
            _session.LoopStopped += Session_LoopStopped;

            _formPara = new ParaForm(_display) { TestRunner = RunTest, HostWriter = WriteToTools };
            _formPara.EditCancelled += (s, e) => ShowInfo();
            panel2.Controls.Add(_formPara);

            _formInfo = new InfoForm();
            panel2.Controls.Add(_formInfo);
            ShowInfo();

            _formTool = new ToolForm(Catalog);
            _formTool.ToolSelected += AddToolFromToolbox;

            // 流程窗口嵌在右侧（同旧项目 Fun_FormNoneBorder）；随主窗一起释放
            _formJob = new JobForm(this) { TopLevel = false, FormBorderStyle = FormBorderStyle.None, Dock = DockStyle.Fill };
            panel3.Controls.Add(_formJob);
            _formJob.Show();

            if (createDefaultFlow)
            {
                foreach (var info in Catalog.Algorithms) AddTool(info.Key, select: false);
                OnFlowChanged();
            }

            // 挂 Disposed 而不是重写 Dispose(bool): 后者已在 Designer 里定义。
            // 此时子控件(含 _display)都已销毁, 不会再有绘制去碰策略持有的句柄。
            Disposed += MainForm_Disposed;
        }

        /// <summary> 插件目录：程序目录下的 <c>plugins\</c> </summary>
        public static string PluginDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");

        /// <summary> 默认方案目录 </summary>
        public static string SchemeRoot => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Scheme");

        internal AlgoCatalog Catalog { get; }

        /// <summary> 当前流程（按执行顺序） </summary>
        internal IReadOnlyList<IParaStrategy> Tools => _tools;

        internal int SelectedIndex => _index;

        internal IParaStrategy CurrentTool => _index >= 0 && _index < _tools.Count ? _tools[_index] : null;

        /// <summary> 流程内容或选中项变了（增删、排序、改名、运行状态、切换工具） </summary>
        internal event EventHandler FlowChanged;

        /// <summary> 运行状态变了：连续运行开始 / 停止，单次运行开始 / 结束 </summary>
        internal event EventHandler RunStateChanged;

        /// <summary> 选择方案目录；参数是建议目录，返回 null 表示取消。测试可替换 </summary>
        internal Func<string, string> SchemeDirPicker { get; set; } = PickFolder;

        /// <summary>
        /// 扫描内置算法与插件目录。插件有问题时提示并退回只用内置算法 —— 内置算法本身有问题则直接抛出（属于程序错误）。
        /// </summary>
        internal static AlgoCatalog LoadCatalog()
        {
            var builtIn = new[] { typeof(FileImageStrategy).Assembly };
            try
            {
                return AlgoCatalog.Load(builtIn, PluginDir);
            }
            catch (AlgoCatalogException ex)
            {
                Log.Error(nameof(MainForm), "插件加载失败, 只使用内置算法.", ex);
                Prompt.Show(ex.Message);
                return AlgoCatalog.Load(builtIn);
            }
        }

        #region 工具列表

        /// <summary> 在当前工具之后插入一个新工具（没有选中时追加到末尾） </summary>
        internal IParaStrategy AddTool(string key, bool select)
        {
            if (!EnsureNotDrawing() || !EnsureIdle()) return null;
            var tool = Catalog.Create(key);
            tool.Name = UniqueName(tool.Name);
            InitTool(tool);
            int at = _index >= 0 ? _index + 1 : _tools.Count;
            _tools.Insert(at, tool);
            if (select)
            {
                OnFlowChanged();
                SelectTool(at);
            }
            return tool;
        }

        internal void RemoveTool(int index)
        {
            if (index < 0 || index >= _tools.Count || !EnsureNotDrawing() || !EnsureIdle()) return;
            var tool = _tools[index];
            _tools.RemoveAt(index);
            DisposeTool(tool);
            _index = -1;
            OnFlowChanged();
            SelectTool(Math.Min(index, _tools.Count - 1));
            ValidateFlow(showStatus: true);
        }

        internal void MoveTool(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _tools.Count || target < 0 || target >= _tools.Count || !EnsureNotDrawing() || !EnsureIdle()) return;
            var tool = _tools[index];
            _tools.RemoveAt(index);
            _tools.Insert(target, tool);
            _index = target;
            OnFlowChanged();
            SelectTool(target);
            ValidateFlow(showStatus: true);
        }

        internal void RenameTool(int index, string name)
        {
            if (index < 0 || index >= _tools.Count) return;
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name == _tools[index].Name) return;
            // 引用按 Id 保存, 改名不会断开下游的来源
            _tools[index].Name = name;
            OnFlowChanged();
            if (index == _index) _formPara.ShowTool(_tools[index], _tools);
        }

        /// <summary>
        /// 切换工具：绘制未结束就切换会让进行中的会话收不到鼠标事件、一直卡到超时，必须先拦下。
        /// </summary>
        internal void SelectTool(int index)
        {
            if (index == _index && index >= 0) return;
            if (!EnsureNotDrawing())
            {
                OnFlowChanged();
                return;
            }

            _index = index;
            OnFlowChanged();
            // 没有选中时也要刷新: 否则删掉最后一个工具、打开空方案后, 参数页仍持有已释放的工具
            _formPara.ShowTool(CurrentTool, _tools);
            if (CurrentTool != null) ShowParameters();
        }

        internal bool CanRunFlow() => EnsureNotDrawing();

        /// <summary> 清空当前流程，同时解除参数页对已释放工具的引用。 </summary>
        internal void ClearFlow()
        {
            if (!EnsureNotDrawing() || !EnsureIdle()) return;
            _formPara.ShowTool(null, new IParaStrategy[0]);
            foreach (var tool in _tools) DisposeTool(tool);
            _tools.Clear();
            _invalid.Clear();
            _index = -1;
            OnFlowChanged();
            ShowStatus("当前流程已清空");
        }

        /// <summary>
        /// 流程结构（增删排序、清空、打开方案）只能在执行会话空闲时改：会话线程正在用这些工具。
        /// </summary>
        private bool EnsureIdle()
        {
            if (!IsBusy) return true;
            Prompt.Show(IsLoopRunning ? "请先停止连续运行。" : "流程正在运行，请等本次运行结束。");
            return false;
        }

        private bool EnsureNotDrawing()
        {
            if (!_formPara.IsDrawBusy) return true;
            Prompt.Show("当前正在绘制 ROI / 模板，请先在图像上右键确认或取消后再切换工具。");
            return false;
        }

        private string UniqueName(string name)
        {
            if (_tools.All(t => t.Name != name)) return name;
            for (int i = 1; ; i++)
            {
                string candidate = $"{name}{i}";
                if (_tools.All(t => t.Name != candidate)) return candidate;
            }
        }

        private void InitTool(IParaStrategy tool)
        {
            try { tool.Init(_display); }
            catch (Exception ex) { Log.Warn(nameof(MainForm), $"工具 '{tool.Name}' 初始化失败.", ex); }
        }

        private static void DisposeTool(IParaStrategy tool)
        {
            try { tool.Dispose(); }
            catch (Exception ex) { Log.Warn(nameof(MainForm), $"释放工具 {tool.GetType().Name} 失败.", ex); }
        }

        /// <summary> 流程内容或选中项变了：通知流程窗口刷新 </summary>
        private void OnFlowChanged() => FlowChanged?.Invoke(this, EventArgs.Empty);

        /// <summary> 引用校验没通过的工具（列表里标红） </summary>
        internal bool IsInvalid(IParaStrategy tool) => _invalid.Contains(tool.Id);

        /// <summary> 画工具所属分组的图标，与工具箱一致 </summary>
        internal void DrawGroupIcon(Graphics g, Rectangle bounds, IParaStrategy tool) =>
            _formTool.DrawGroupIcon(g, bounds, AlgoInfo.Of(tool)?.Group);

        internal void ShowToolbox()
        {
            if (!_formTool.Visible) _formTool.Show(this);
            _formTool.Activate();
        }

        internal void FocusParameters()
        {
            ShowParameters();
            _formPara.Focus();
        }

        /// <summary> 下方区域同一时间只显示一页（同旧项目 HideAll + Show） </summary>
        private void ShowPage(Control page)
        {
            foreach (Control control in panel2.Controls) control.Visible = control == page;
        }

        internal void ShowParameters() => ShowPage(_formPara);

        internal void ShowInfo() => ShowPage(_formInfo);

        private void mnu_viewPara_Click(object sender, EventArgs e) => ShowParameters();

        private void mnu_viewInfo_Click(object sender, EventArgs e) => ShowInfo();

        internal void AddToolFromToolbox(string key)
        {
            try { AddTool(key, select: true); }
            catch (Exception ex)
            {
                Log.Error(nameof(MainForm), $"添加工具 '{key}' 失败.", ex);
                Prompt.Show(ex.Message);
            }
        }

        private void mnu_toolbox_Click(object sender, EventArgs e) => ShowToolbox();

        #endregion

        #region 运行

        /// <summary> 正在连续运行，或有单次运行还没显示完：期间不能改流程结构、不能绘制 ROI / 模板 </summary>
        internal bool IsBusy => _session.IsBusy || _runsInFlight > 0;

        internal bool IsLoopRunning => _session.IsLooping;

        /// <summary>
        /// 只运行当前工具；它的上游输出沿用上一轮的结果。在执行会话里运行，UI 线程不等；显示的是本步的底图 + 本步的叠加层
        /// </summary>
        internal async Task<RunResult> RunCurrentAsync()
        {
            if (CurrentTool == null) return null;
            BeginRun();
            try
            {
                using (var frame = await _session.RunStepAsync(_index, _display.Display.HoImage))
                {
                    var step = frame.Result.Steps.Single();
                    if (IsDisposed) return step.Result;
                    _display.ShowResult(frame.Image, frame.TakeOverlay());
                    ShowCycleTime(step.Result.Elapsed);
                    ShowStatus($"{step.Tool.Name}: {Describe(step.Result)}", LevelOf(step.Result.Status));
                    OnFlowChanged();
                    return step.Result;
                }
            }
            finally { EndRun(); }
        }

        /// <summary> 按顺序运行整个流程（遇到失败即停）；运行前先校验引用 </summary>
        internal async Task<FlowRunResult> RunFlowAsync()
        {
            var issues = ValidateFlow(showStatus: false);
            BeginRun();
            try
            {
                using (var frame = await _session.RunAsync(_display.Display.HoImage))
                {
                    if (!IsDisposed) ShowFrame(frame, issues);
                    return frame.Result;
                }
            }
            finally { EndRun(); }
        }

        /// <summary>
        /// 显示一帧：底图取流程结束时的当前图像，叠加层按执行顺序合成后交给显示控件（之后缩放 / 平移都会重放）。
        /// </summary>
        private void ShowFrame(FlowFrame frame, IReadOnlyList<FlowIssue> issues)
        {
            var result = frame.Result;
            _display.ShowResult(frame.Image, frame.TakeOverlay());
            string summary = $"流程: {result.Steps.Count}/{_tools.Count} 步, 用时 {result.Elapsed.TotalMilliseconds:F0} ms";
            var error = result.FirstError;
            if (error != null) summary += Environment.NewLine + $"失败: {error.Tool.Name}: {error.Result.Message}";
            if (issues.Count > 0) summary += Environment.NewLine + $"引用问题 {issues.Count} 处: {issues[0]}";
            ShowCycleTime(result.Elapsed);
            ShowStatus(summary, error != null ? InfoLevel.Error : issues.Count > 0 ? InfoLevel.Warn : InfoLevel.Info);
            OnFlowChanged();
        }

        private void BeginRun()
        {
            _runsInFlight++;
            OnRunStateChanged();
        }

        private void EndRun()
        {
            _runsInFlight--;
            if (!IsDisposed) OnRunStateChanged();
        }

        /// <summary> 校验各工具的来源引用；有问题的工具在列表里标红 </summary>
        internal IReadOnlyList<FlowIssue> ValidateFlow(bool showStatus)
        {
            var issues = new FlowRunner(_tools).Validate();
            _invalid.Clear();
            foreach (var issue in issues) _invalid.Add(issue.Tool.Id);
            if (showStatus && issues.Count > 0) ShowStatus($"引用问题 {issues.Count} 处: {issues[0]}", InfoLevel.Warn);
            OnFlowChanged();
            return issues;
        }

        /// <summary>
        /// 连续运行：在执行会话里反复跑整个流程，遇到失败或异常自动停下。每帧都以开始时窗口里的图为输入（复制一份）。
        /// </summary>
        internal void StartLoop()
        {
            if (IsLoopRunning || _tools.Count == 0 || !CanRunFlow() || IsBusy) return;
            _loopIssues = ValidateFlow(showStatus: false);
            _session.StartLoop(_display.Display.HoImage);
            OnRunStateChanged();
        }

        /// <summary> 停止连续运行：正在跑的那一帧在两个工具之间停下，不再显示 </summary>
        internal void StopLoop()
        {
            _session.StopLoop();
            OnRunStateChanged();
        }

        private void OnRunStateChanged()
        {
            _formPara.HostBusy = IsBusy;
            RunStateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 参数写回：会话忙时排进会话、在两帧之间执行（连续运行中改参数不用停机）；空闲时就地执行，会话线程这时不碰工具。
        /// </summary>
        private Task<bool> WriteToTools(Func<bool> write) =>
            _session.IsBusy ? _session.InvokeAsync(write) : Task.FromResult(write());

        /// <summary> 会话线程：只留最新一帧，没有待显示的请求时才 Post 一次，UI 线程跟不上就丢中间帧 </summary>
        private void Session_FrameCompleted(object sender, FlowFrameEventArgs e)
        {
            Interlocked.Exchange(ref _latestFrame, e.TakeFrame())?.Dispose();
            if (Interlocked.Exchange(ref _renderPosted, 1) == 0 && !PostToUi(ShowLatestFrame))
                Interlocked.Exchange(ref _latestFrame, null)?.Dispose();
        }

        private void ShowLatestFrame()
        {
            // 先清标记再取帧: 取帧之后才到的一帧会再 Post 一次, 不会被漏掉
            Volatile.Write(ref _renderPosted, 0);
            using (var frame = Interlocked.Exchange(ref _latestFrame, null))
            {
                if (frame != null && !IsDisposed) ShowFrame(frame, _loopIssues);
            }
        }

        /// <summary> 会话线程：连续运行停下了。因失败停下时切到信息窗口（同旧项目 ShowErro） </summary>
        private void Session_LoopStopped(object sender, LoopStoppedEventArgs e)
        {
            PostToUi(() =>
            {
                if (IsDisposed) return;
                if (e.Error != null)
                {
                    _formInfo.Error($"连续运行失败: {e.Error.Message}");
                    ShowInfo();
                    Prompt.Show(e.Error.Message);
                }
                else if (e.Failure != null)
                {
                    ShowInfo();
                }
                OnRunStateChanged();
            });
        }

        /// <summary> 从会话线程转到 UI 线程；窗体已经没有句柄（正在关闭）时返回 false </summary>
        private bool PostToUi(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return false;
                BeginInvoke(action);
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        /// <summary> 参数页的"运行测试"：与流程窗口的"运行当前"同一套检查 </summary>
        private void RunTest()
        {
            if (!EnsureLoopStopped() || !CanRunFlow() || IsBusy) return;
            Fire(RunCurrentAsync);
        }

        /// <summary> 从事件处理器里发起一次运行：意外异常记日志并提示，不冒到 WinForms 消息循环 </summary>
        internal async void Fire(Func<Task> run)
        {
            try { await run(); }
            catch (Exception ex)
            {
                Log.Error(nameof(MainForm), "运行失败.", ex);
                if (!IsDisposed) Prompt.Show(ex.Message);
            }
        }

        private static string Describe(RunResult result)
        {
            string text = result.Status == RunStatus.Ok ? "OK" : result.Status == RunStatus.Warning ? "警告" : "失败";
            return $"{text} {result.Message} ({result.Elapsed.TotalMilliseconds:F0} ms)";
        }

        private static InfoLevel LevelOf(RunStatus status) =>
            status == RunStatus.Ok ? InfoLevel.Info : status == RunStatus.Warning ? InfoLevel.Warn : InfoLevel.Error;

        /// <summary>
        /// 状态栏只有一行：多行内容压成一行显示，完整内容放在悬停提示里。同时记入信息窗口与日志文件
        /// </summary>
        private void ShowStatus(string text, InfoLevel level = InfoLevel.Info)
        {
            lbl_status.Text = text.Replace(Environment.NewLine, "    ");
            toolTip1.SetToolTip(lbl_status, text);
            _formInfo.Write(level, text);
            switch (level)
            {
                case InfoLevel.Warn: Log.Warn(nameof(MainForm), text); break;
                case InfoLevel.Error: Log.Error(nameof(MainForm), text); break;
                default: Log.Info(nameof(MainForm), text); break;
            }
        }

        private void ShowCycleTime(TimeSpan elapsed) => lbl_CT.Text = $"CT: {elapsed.TotalMilliseconds:F0} ms";

        #endregion

        #region 方案

        /// <summary>
        /// 保存方案。会话忙（连续运行）时排进会话、在两帧之间保存：保存会读参数、迁数据目录，不能和执行交错。
        /// </summary>
        internal async Task SaveSchemeAsync(string dir)
        {
            var tools = _tools.ToList();
            if (_session.IsBusy) await _session.InvokeAsync(() => FlowScheme.Save(dir, tools));
            else FlowScheme.Save(dir, tools);
            if (!IsDisposed) ShowStatus($"方案已保存: {dir}");
        }

        internal void OpenScheme(string dir)
        {
            if (!EnsureNotDrawing() || !EnsureIdle()) return;
            var loaded = FlowScheme.Load(dir, Catalog);
            foreach (var tool in _tools) DisposeTool(tool);
            _tools.Clear();
            _tools.AddRange(loaded);
            foreach (var tool in _tools) InitTool(tool);

            _index = -1;
            ValidateFlow(showStatus: false);
            SelectTool(_tools.Count > 0 ? 0 : -1);
            int missing = _tools.Count(t => t is MissingTool);
            ShowStatus($"方案已打开: {dir}" + (missing > 0 ? $" (缺失 {missing} 个工具, 配置已保留)" : string.Empty),
                missing > 0 ? InfoLevel.Warn : InfoLevel.Info);
        }

        /// <summary> 新建方案即清空当前流程；连续运行中不允许 </summary>
        private void mnu_new_Click(object sender, EventArgs e)
        {
            if (!EnsureLoopStopped()) return;
            if (_tools.Count > 0 && MessageBox.Show(this, "清空当前流程？未保存的工具配置将丢失。", "新建方案",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            ClearFlow();
        }

        private void btn_save_Click(object sender, EventArgs e)
        {
            try
            {
                string dir = SchemeDirPicker(SchemeRoot);
                if (dir != null) Fire(() => SaveSchemeAsync(dir));
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        /// <summary> 运行中不允许替换流程（新建、打开方案） </summary>
        private bool EnsureLoopStopped()
        {
            if (!IsLoopRunning) return EnsureIdle();
            Prompt.Show("请先停止连续运行。");
            return false;
        }

        private void btn_open_Click(object sender, EventArgs e)
        {
            if (!EnsureLoopStopped()) return;
            try
            {
                string dir = SchemeDirPicker(SchemeRoot);
                if (dir == null) return;
                if (!File.Exists(Path.Combine(dir, FlowScheme.FileName)))
                {
                    Prompt.Show($"目录里没有方案文件 {FlowScheme.FileName}: {dir}");
                    return;
                }
                OpenScheme(dir);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private static string PickFolder(string suggested)
        {
            Directory.CreateDirectory(suggested);
            using (var dialog = new FolderBrowserDialog { SelectedPath = suggested, ShowNewFolderButton = true })
            {
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        #endregion

        /// <summary>
        /// 释放全部工具。逐个释放，避免一个工具失败影响其余工具。
        /// </summary>
        private void MainForm_Disposed(object sender, EventArgs e)
        {
            // 先停会话并等它跑完当前工具: 之后才能释放它正在用的工具
            _session.Dispose();
            Interlocked.Exchange(ref _latestFrame, null)?.Dispose();
            _formTool.Dispose();
            foreach (var tool in _tools) DisposeTool(tool);
        }
    }
}
