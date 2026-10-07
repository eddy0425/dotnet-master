using System;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using DotNet.Drawing;
using DotNet.HalconCore;
using DotNet.VisionRuntime;

namespace DotNet.VisionMaster
{
    /// <summary>
    /// 流程窗口：工具列表、增删改名排序、运行按钮。主窗把它嵌在右侧，参考旧项目的 Form_Job。
    /// </summary>
    /// <remarks>
    /// 只是视图：工具、算法资源和连续运行都由主窗持有，这里的操作都转给主窗，
    /// 主窗通过 <see cref="MainForm.FlowChanged"/> / <see cref="MainForm.RunStateChanged"/> 回调刷新。
    /// </remarks>
    public partial class JobForm : Form
    {
        private readonly MainForm _host;
        private bool _syncingList;

        // 供 WinForms Designer 使用；未绑定宿主时禁用运行和编辑。
        public JobForm() : this(null) { }

        public JobForm(MainForm host)
        {
            _host = host;
            InitializeComponent();
            lst_tools.ItemHeight = lst_tools.Font.Height + 16;  // 行高要放下图标和状态标记; Designer 里的值放不下中文
            lst_tools.Resize += (s, e) => lst_tools.Invalidate();   // 状态标记靠右画, 宽度变了要整行重画
            if (_host != null)
            {
                // 主窗改动流程、开停连续运行时跟着刷新；本窗口先释放时退订，免得主窗回调到已释放的控件
                _host.FlowChanged += Host_FlowChanged;
                _host.RunStateChanged += Host_RunStateChanged;
                Disposed += (s, e) =>
                {
                    _host.FlowChanged -= Host_FlowChanged;
                    _host.RunStateChanged -= Host_RunStateChanged;
                };
            }
            RefreshFlow();
        }

        private bool Bound => _host != null && !_host.IsDisposed;

        internal bool IsLoopRunning => Bound && _host.IsLoopRunning;

        /// <summary> 主窗正在运行（连续运行，或单次运行还没显示完） </summary>
        private bool HostBusy => Bound && _host.IsBusy;

        /// <summary> 按主窗的流程重建列表；只有选中项变了时不重建，免得点选时列表跳回顶部 </summary>
        internal void RefreshFlow()
        {
            _syncingList = true;
            lst_tools.BeginUpdate();
            try
            {
                int count = Bound ? _host.Tools.Count : 0;
                bool same = lst_tools.Items.Count == count;
                for (int i = 0; same && i < count; i++) same = Equals(lst_tools.Items[i], ItemText(i));
                if (!same)
                {
                    int top = lst_tools.TopIndex;
                    lst_tools.Items.Clear();
                    for (int i = 0; i < count; i++) lst_tools.Items.Add(ItemText(i));
                    if (count > 0) lst_tools.TopIndex = Math.Min(top, count - 1);
                }
                int selected = Bound ? _host.SelectedIndex : -1;
                lst_tools.SelectedIndex = selected >= 0 && selected < count ? selected : -1;
            }
            finally
            {
                lst_tools.EndUpdate();
                _syncingList = false;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool bound = Bound;
            bool hasTools = bound && _host.Tools.Count > 0;
            btn_runOnce.Enabled = !HostBusy && hasTools;
            // 循环中始终能点停止
            btn_runLoop.Enabled = bound && (IsLoopRunning || (!HostBusy && hasTools));
            btn_runLoop.Text = IsLoopRunning ? "停止运行" : "连续运行";
        }

        private void Host_FlowChanged(object sender, EventArgs e) => RefreshFlow();

        private void Host_RunStateChanged(object sender, EventArgs e) => UpdateButtons();

        private void Form_Job_Load(object sender, EventArgs e) => RefreshFlow();

        #region 列表

        /// <summary> 列表项文字带完整标记（含运行状态），供读屏与测试读取；界面上的行由 <see cref="lst_tools_DrawItem"/> 自绘 </summary>
        private string ItemText(int i)
        {
            string mark = string.Empty;
            switch (_host.Tools[i].LastResult?.Status)
            {
                case RunStatus.Ok: mark = " ✔"; break;
                case RunStatus.Error: mark = " ✘"; break;
                case RunStatus.Warning: mark = " !"; break;
            }
            // 每种状态都要有标记：RefreshFlow 靠比较文字判断要不要重建, 否则 null → Ok 时行尾的 ✔ 不会重画
            return ItemCaption(i) + mark;
        }

        /// <summary> 列表行的文字（不含运行状态, 状态在行尾单独画） </summary>
        private string ItemCaption(int i)
        {
            var tool = _host.Tools[i];
            string mark = tool is MissingTool ? " (缺失)" : _host.IsInvalid(tool) ? " (引用错误)" : string.Empty;
            return $"{i + 1}. {tool.Name}{mark}";
        }

        private void lst_tools_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 选中被拦下（正在绘制）时主窗会恢复原选中项并触发 FlowChanged，本窗口随之刷新
            if (!_syncingList && Bound) _host.SelectTool(lst_tools.SelectedIndex);
        }

        /// <summary> 双击工具回到参数页：单击已选中的工具不会触发切换，下方停在信息窗口时用它切回 </summary>
        private void lst_tools_DoubleClick(object sender, EventArgs e)
        {
            if (Bound && _host.SelectedIndex >= 0) _host.FocusParameters();
        }

        private static readonly Color RowBack = Color.FromArgb(32, 33, 42);
        private static readonly Color RowSelected = Color.FromArgb(0, 102, 180);
        private static readonly Color RowLine = Color.FromArgb(90, 92, 105);
        private static readonly Color BadText = Color.FromArgb(255, 96, 96);
        private static readonly Font BadgeFont = new Font("微软雅黑", 7.5F, FontStyle.Bold);

        /// <summary>
        /// 深色行：分组图标 + 序号名称 + 行尾运行状态。引用有问题、缺失、上一轮失败的工具文字标红。
        /// </summary>
        private void lst_tools_DrawItem(object sender, DrawItemEventArgs e)
        {
            var g = e.Graphics;
            var bounds = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(selected ? RowSelected : RowBack))
                g.FillRectangle(back, bounds);
            if (!Bound || e.Index < 0 || e.Index >= _host.Tools.Count) return;

            var tool = _host.Tools[e.Index];
            using (var line = new Pen(RowLine) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
                g.DrawLine(line, bounds.Left + 4, bounds.Bottom - 1, bounds.Right - 4, bounds.Bottom - 1);

            int x = bounds.Left + 8;
            int size = Math.Min(16, bounds.Height - 4);
            _host.DrawGroupIcon(g, new Rectangle(x, bounds.Top + (bounds.Height - size) / 2, size, size), tool);
            x += 22;

            const int badge = 16;
            var status = tool.LastResult?.Status;
            var textBounds = new Rectangle(x, bounds.Top, bounds.Right - x - badge - 12, bounds.Height);
            bool bad = tool is MissingTool || _host.IsInvalid(tool) || status == RunStatus.Error;
            TextRenderer.DrawText(g, ItemCaption(e.Index), e.Font, textBounds, bad ? BadText : Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (status != null)
                DrawStatusBadge(g, new Rectangle(bounds.Right - badge - 10, bounds.Top + (bounds.Height - badge) / 2, badge, badge), status.Value);
        }

        private static void DrawStatusBadge(Graphics g, Rectangle r, RunStatus status)
        {
            Color color;
            string glyph;
            switch (status)
            {
                case RunStatus.Ok: color = Color.FromArgb(76, 175, 80); glyph = "✔"; break;
                case RunStatus.Warning: color = Color.Orange; glyph = "!"; break;
                default: color = Color.FromArgb(229, 57, 53); glyph = "✘"; break;
            }
            var mode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(color)) g.FillEllipse(brush, r);
            g.SmoothingMode = mode;
            TextRenderer.DrawText(g, glyph, BadgeFont, r, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary> 标签页画成参考界面的蓝底白字 </summary>
        private void tbc_jobs_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tbc_jobs.TabPages.Count) return;
            bool selected = e.Index == tbc_jobs.SelectedIndex;
            using (var brush = new SolidBrush(selected ? Color.DodgerBlue : SystemColors.Control))
                e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tbc_jobs.TabPages[e.Index].Text, e.Font, e.Bounds,
                selected ? Color.White : SystemColors.ControlText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private string DraggedAlgorithmKey(IDataObject data)
        {
            if (!Bound || data == null || !data.GetDataPresent(ToolForm.AlgorithmDragFormat)) return null;
            var key = data.GetData(ToolForm.AlgorithmDragFormat) as string;
            return _host.Catalog.Find(key) != null ? key : null;
        }

        private void lst_tools_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = (e.AllowedEffect & DragDropEffects.Copy) != 0 && DraggedAlgorithmKey(e.Data) != null
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void lst_tools_DragDrop(object sender, DragEventArgs e)
        {
            var key = DraggedAlgorithmKey(e.Data);
            // 等 OLE 拖放循环结束再添加：添加时可能弹模态提示，拖放中弹出会卡住工具箱的拖拽状态
            if ((e.AllowedEffect & DragDropEffects.Copy) != 0 && key != null)
                BeginInvoke(new Action(() => _host.AddToolFromToolbox(key)));
        }

        /// <summary>
        /// 右键先选中鼠标下的工具再弹菜单；空白处不弹。选中被拦下（正在绘制）时也不弹，免得菜单操作错对象。
        /// </summary>
        private void lst_tools_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || !Bound) return;
            int index = lst_tools.IndexFromPoint(e.Location);
            if (index < 0 || index >= _host.Tools.Count) return;
            _host.SelectTool(index);
            if (_host.SelectedIndex != index) return;
            mnu_up.Enabled = index > 0;
            mnu_down.Enabled = index < _host.Tools.Count - 1;
            cms_tool.Show(lst_tools, e.Location);
        }

        #endregion

        #region 编辑

        private void btn_add_Click(object sender, EventArgs e)
        {
            if (Bound) _host.ShowToolbox();
        }

        private void btn_remove_Click(object sender, EventArgs e)
        {
            if (Bound) _host.RemoveTool(_host.SelectedIndex);
        }

        private void mnu_up_Click(object sender, EventArgs e)
        {
            if (Bound) _host.MoveTool(_host.SelectedIndex, -1);
        }

        private void mnu_down_Click(object sender, EventArgs e)
        {
            if (Bound) _host.MoveTool(_host.SelectedIndex, +1);
        }

        private void mnu_editPara_Click(object sender, EventArgs e)
        {
            if (Bound) _host.FocusParameters();
        }

        /// <summary> 在当前行上原地显示名称输入框；回车或离开输入框后生效，Esc 取消 </summary>
        private void btn_rename_Click(object sender, EventArgs e)
        {
            if (!Bound || _host.CurrentTool == null) return;
            int index = _host.SelectedIndex;
            if (index >= lst_tools.Items.Count) return;
            // 当前行可能被滚轮滚出可视区（上方或下方），先滚回来，否则输入框落在客户区外看不见
            int visible = Math.Max(1, lst_tools.ClientSize.Height / lst_tools.ItemHeight);
            if (index < lst_tools.TopIndex) lst_tools.TopIndex = index;
            else if (index >= lst_tools.TopIndex + visible) lst_tools.TopIndex = index - visible + 1;
            var row = lst_tools.GetItemRectangle(index);
            txt_name.Text = _host.CurrentTool.Name;
            txt_name.SetBounds(row.Left + 28, row.Top + (row.Height - txt_name.Height) / 2, row.Width - 60, txt_name.Height);
            txt_name.Visible = true;
            txt_name.Focus();
            txt_name.SelectAll();
        }

        private void EndRename(bool commit)
        {
            if (!txt_name.Visible) return;
            txt_name.Visible = false;   // 先隐藏: 改名会刷新列表, 刷新时不能再触发一次 Leave
            if (commit && Bound) _host.RenameTool(_host.SelectedIndex, txt_name.Text);
            lst_tools.Focus();
        }

        private void txt_name_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
            EndRename(commit: e.KeyCode == Keys.Enter);
            e.SuppressKeyPress = true;
        }

        private void txt_name_Leave(object sender, EventArgs e) => EndRename(commit: true);

        #endregion

        #region 运行

        /// <summary> 只运行当前工具；它的上游输出沿用上一轮的结果 </summary>
        private void mnu_runCurrent_Click(object sender, EventArgs e)
        {
            if (!Bound || HostBusy || !_host.CanRunFlow()) return;
            _host.Fire(_host.RunCurrentAsync);
        }

        /// <summary>
        /// 运行一次整个流程。在主窗的执行会话里跑，UI 线程不等；运行中再点不会重入（按钮已禁用，入口也挡一次）。
        /// </summary>
        /// <returns> 本次运行；没有发起时为已完成的任务 </returns>
        internal Task RunOnce()
        {
            if (!Bound || HostBusy || _host.Tools.Count == 0 || !_host.CanRunFlow()) return Task.FromResult(0);
            var run = _host.RunFlowAsync();
            Observe(run);
            return run;
        }

        /// <summary> 流程运行的意外异常记日志并提示，不冒到 WinForms 消息循环 </summary>
        private async void Observe(Task run)
        {
            try { await run; }
            catch (Exception ex)
            {
                Log.Error(nameof(JobForm), "流程运行失败.", ex);
                if (!IsDisposed) Prompt.Show(ex.Message);
            }
            finally
            {
                if (!IsDisposed) RefreshFlow();
            }
        }

        private void btn_runOnce_Click(object sender, EventArgs e) => RunOnce();

        private void btn_runLoop_Click(object sender, EventArgs e)
        {
            if (!Bound) return;
            if (_host.IsLoopRunning) _host.StopLoop();
            else _host.StartLoop();
        }

        #endregion
    }
}
