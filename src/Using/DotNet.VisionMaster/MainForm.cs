using System;
using System.IO;
using System.Linq;
using System.Drawing;
using DotNet.Drawing;
using DotNet.HalconUI;
using DotNet.HalconCore;
using DotNet.HalconAlgo;
using System.Windows.Forms;
using System.Collections.Generic;


namespace DotNet.VisionMaster
{
    /// <summary>
    /// 宿主主窗：工具列表（流程）、参数页、显示窗口、运行与方案读写。
    /// </summary>
    /// <remarks>
    /// 只认识 <see cref="AlgoCatalog"/>、<see cref="IParaStrategy"/> 与能力接口，不认识任何具体算法：
    /// 可添加的工具来自目录扫描（内置 HalconAlgo + <c>plugins\</c>），新增算法不需要改这里。
    /// </remarks>
    public partial class MainForm : Form
    {
        private readonly HDisplayUI _display;
        private readonly ParaForm _formPara;
        private readonly List<IParaStrategy> _tools = new List<IParaStrategy>();
        private readonly HashSet<Guid> _invalid = new HashSet<Guid>();
        private int _index = -1;
        private bool _syncingList;

        public MainForm() : this(LoadCatalog(), createDefaultFlow: true) { }

        /// <param name="catalog">可添加的算法。</param>
        /// <param name="createDefaultFlow">为 true 时每种算法各建一个工具，作为初始流程。</param>
        internal MainForm(AlgoCatalog catalog, bool createDefaultFlow)
        {
            InitializeComponent();
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

            _display = new HDisplayUI();
            panel1.Controls.Add(_display);

            _formPara = new ParaForm(_display);
            panel2.Controls.Add(_formPara);

            lst_tools.DrawMode = DrawMode.OwnerDrawFixed;
            lst_tools.ItemHeight = lst_tools.Font.Height + 6;   // Designer 里的 12px 放不下中文, 自绘时各行会叠在一起
            lst_tools.DrawItem += lst_tools_DrawItem;
            BuildAddMenu();

            if (createDefaultFlow)
            {
                foreach (var info in Catalog.Algorithms) AddTool(info.Key, select: false);
                RefreshToolList();
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

        private void BuildAddMenu()
        {
            menu_add.Items.Clear();
            foreach (var group in Catalog.Algorithms.GroupBy(a => a.Group))
            {
                var groupItem = new ToolStripMenuItem(group.Key);
                foreach (var info in group)
                {
                    string key = info.Key;
                    groupItem.DropDownItems.Add(new ToolStripMenuItem(info.DisplayName, null, (s, e) => AddTool(key, select: true)) { Tag = key });
                }
                menu_add.Items.Add(groupItem);
            }
        }

        /// <summary> 在当前工具之后插入一个新工具（没有选中时追加到末尾） </summary>
        internal IParaStrategy AddTool(string key, bool select)
        {
            if (!EnsureNotDrawing()) return null;
            var tool = Catalog.Create(key);
            tool.Name = UniqueName(tool.Name);
            InitTool(tool);
            int at = _index >= 0 ? _index + 1 : _tools.Count;
            _tools.Insert(at, tool);
            if (select)
            {
                RefreshToolList();
                SelectTool(at);
            }
            return tool;
        }

        internal void RemoveTool(int index)
        {
            if (index < 0 || index >= _tools.Count || !EnsureNotDrawing()) return;
            var tool = _tools[index];
            _tools.RemoveAt(index);
            DisposeTool(tool);
            _index = -1;
            RefreshToolList();
            SelectTool(Math.Min(index, _tools.Count - 1));
            ValidateFlow(showStatus: true);
        }

        internal void MoveTool(int index, int delta)
        {
            int target = index + delta;
            if (index < 0 || index >= _tools.Count || target < 0 || target >= _tools.Count || !EnsureNotDrawing()) return;
            var tool = _tools[index];
            _tools.RemoveAt(index);
            _tools.Insert(target, tool);
            _index = target;
            RefreshToolList();
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
            RefreshToolList();
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
                SyncListSelection();
                return;
            }

            _index = index;
            SyncListSelection();
            var tool = CurrentTool;
            txt_name.Text = tool?.Name ?? string.Empty;
            if (tool != null) _formPara.ShowTool(tool, _tools);
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

        private void RefreshToolList()
        {
            _syncingList = true;
            try
            {
                lst_tools.BeginUpdate();
                lst_tools.Items.Clear();
                for (int i = 0; i < _tools.Count; i++) lst_tools.Items.Add(ItemText(i));
                lst_tools.EndUpdate();
            }
            finally { _syncingList = false; }
            SyncListSelection();
        }

        private string ItemText(int i)
        {
            var tool = _tools[i];
            string mark = tool is MissingTool ? " (缺失)" : _invalid.Contains(tool.Id) ? " (引用错误)" : string.Empty;
            switch (tool.LastResult?.Status)
            {
                case RunStatus.Error: mark += " ✘"; break;
                case RunStatus.Warning: mark += " !"; break;
            }
            return $"{i + 1}. {tool.Name}{mark}";
        }

        private void SyncListSelection()
        {
            _syncingList = true;
            try { lst_tools.SelectedIndex = _index >= 0 && _index < lst_tools.Items.Count ? _index : -1; }
            finally { _syncingList = false; }
        }

        private void lst_tools_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!_syncingList) SelectTool(lst_tools.SelectedIndex);
        }

        /// <summary> 引用有问题、缺失、上一轮失败的工具标红 </summary>
        private void lst_tools_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _tools.Count) return;
            var tool = _tools[e.Index];
            bool bad = tool is MissingTool || _invalid.Contains(tool.Id) || tool.LastResult?.Status == RunStatus.Error;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            var color = bad ? Color.Red : selected ? SystemColors.HighlightText : SystemColors.ControlText;
            TextRenderer.DrawText(e.Graphics, lst_tools.Items[e.Index].ToString(), e.Font, e.Bounds, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void btn_add_Click(object sender, EventArgs e) => menu_add.Show(btn_add, new Point(0, btn_add.Height));

        private void btn_remove_Click(object sender, EventArgs e) => RemoveTool(_index);

        private void btn_up_Click(object sender, EventArgs e) => MoveTool(_index, -1);

        private void btn_down_Click(object sender, EventArgs e) => MoveTool(_index, +1);

        private void txt_name_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            RenameTool(_index, txt_name.Text);
            e.SuppressKeyPress = true;
        }

        private void txt_name_Leave(object sender, EventArgs e) => RenameTool(_index, txt_name.Text);

        #endregion

        #region 运行

        /// <summary> 只运行当前工具；它的上游输出沿用上一轮的结果 </summary>
        internal RunResult RunCurrent()
        {
            if (CurrentTool == null) return null;
            _display.ReDispImage();
            var step = new FlowRunner(_tools).RunStep(_index, _display.Display.HoImage, _display.Display);
            ShowStatus($"{step.Tool.Name}: {Describe(step.Result)}");
            RefreshToolList();
            return step.Result;
        }

        /// <summary> 按顺序运行整个流程（遇到失败即停）；运行前先校验引用 </summary>
        internal FlowRunResult RunFlow()
        {
            var issues = ValidateFlow(showStatus: false);
            var result = new FlowRunner(_tools).Run(_display.Display.HoImage, _display.Display);
            string summary = $"流程: {result.Steps.Count}/{_tools.Count} 步, 用时 {result.Elapsed.TotalMilliseconds:F0} ms";
            var error = result.FirstError;
            if (error != null) summary += Environment.NewLine + $"失败: {error.Tool.Name}: {error.Result.Message}";
            if (issues.Count > 0) summary += Environment.NewLine + $"引用问题 {issues.Count} 处: {issues[0]}";
            ShowStatus(summary);
            RefreshToolList();
            return result;
        }

        /// <summary> 校验各工具的来源引用；有问题的工具在列表里标红 </summary>
        internal IReadOnlyList<FlowIssue> ValidateFlow(bool showStatus)
        {
            var issues = new FlowRunner(_tools).Validate();
            _invalid.Clear();
            foreach (var issue in issues) _invalid.Add(issue.Tool.Id);
            if (showStatus && issues.Count > 0) ShowStatus($"引用问题 {issues.Count} 处: {issues[0]}");
            RefreshToolList();
            return issues;
        }

        private void but_Run_Click(object sender, EventArgs e)
        {
            try { RunCurrent(); }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private void but_RunFlow_Click(object sender, EventArgs e)
        {
            try { RunFlow(); }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private static string Describe(RunResult result)
        {
            string text = result.Status == RunStatus.Ok ? "OK" : result.Status == RunStatus.Warning ? "警告" : "失败";
            return $"{text} {result.Message} ({result.Elapsed.TotalMilliseconds:F0} ms)";
        }

        private void ShowStatus(string text)
        {
            lbl_status.Text = text;
            Log.Info(nameof(MainForm), text);
        }

        #endregion

        #region 方案

        internal void SaveScheme(string dir)
        {
            FlowScheme.Save(dir, _tools);
            ShowStatus($"方案已保存: {dir}");
        }

        internal void OpenScheme(string dir)
        {
            if (!EnsureNotDrawing()) return;
            var loaded = FlowScheme.Load(dir, Catalog);
            foreach (var tool in _tools) DisposeTool(tool);
            _tools.Clear();
            _tools.AddRange(loaded);
            foreach (var tool in _tools) InitTool(tool);

            _index = -1;
            ValidateFlow(showStatus: false);
            SelectTool(_tools.Count > 0 ? 0 : -1);
            int missing = _tools.Count(t => t is MissingTool);
            ShowStatus($"方案已打开: {dir}" + (missing > 0 ? $" (缺失 {missing} 个工具, 配置已保留)" : string.Empty));
        }

        private void btn_save_Click(object sender, EventArgs e)
        {
            try
            {
                string dir = SchemeDirPicker(SchemeRoot);
                if (dir != null) SaveScheme(dir);
            }
            catch (Exception ex) { Prompt.Show(ex.Message); }
        }

        private void btn_open_Click(object sender, EventArgs e)
        {
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
            foreach (var tool in _tools) DisposeTool(tool);
        }
    }
}
