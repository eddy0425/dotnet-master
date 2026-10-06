using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DotNet.HalconCore;

namespace DotNet.HalconUI
{
    /// <summary>
    /// 按策略声明的 <see cref="ParamItem"/> 生成参数面板，并双向绑定。
    /// </summary>
    /// <remarks>
    /// 取代原来 ParaForm.Designer 里的固定槽位（<c>lbl/cmb/btn_100..115</c>、<c>ckb_disp0..4</c>）：
    /// 参数多一个、来源多一个，不再需要改 Designer。控件是本类自己创建并显式持有的，
    /// 不再按私有字段名反射查找。
    /// <para>
    /// 写回时机：控件值通过校验、且与 getter 的结果不同时才调用 setter，随后触发一次 <see cref="Committed"/>；
    /// "点运行"不再隐含回存。校验失败的值不写回，错误显示在控件旁。
    /// </para>
    /// <para>
    /// 外观沿用旧版 ParaForm：深绿底、定宽槽位（标签 + 130 宽编辑框 + 图标按钮），
    /// 每列排满 <see cref="RowsPerColumn"/> 行后换到下一列；隐藏的项不占位置。
    /// 声明了 <see cref="ParamItem.Group"/> 的项画进同名分组框，分组框依次排在槽位列的右边。
    /// <see cref="Control.Dock"/> 为 <see cref="DockStyle.Top"/> 时，面板高度自动跟随内容。
    /// </para>
    /// </remarks>
    public sealed class ParamPanel : UserControl
    {
        private sealed class Row
        {
            public ParamItem Item;
            public Label Label;
            public Control Editor;
            public Button Button;
        }

        private sealed class Group
        {
            public GroupBox Box;
            public readonly List<Row> Rows = new List<Row>();
        }

        // 槽位尺寸（96 DPI 下的像素），取自旧版 ParaForm.Designer 的 lbl/cmb/btn_100..115
        private const int SlotLeft = 9;
        private const int SlotTop = 9;
        private const int RowPitch = 33;
        private const int LabelWidth = 78;     // 旧版是 65，放不下"最大重叠率"这样的五字标签
        private const int EditorWidth = 130;
        private const int EditorHeight = 23;
        private const int ButtonGap = 13;
        private const int ButtonWidth = 30;
        private const int ButtonHeight = 20;
        private const int ColumnWidth = 283;   // 按钮后留出 ErrorProvider 图标的位置

        // 分组框尺寸，取自旧版"显示输出"页的 grb_Display / grb_Font
        private const int GroupLeft = 20;
        private const int GroupGap = 30;
        private const int GroupPadLeft = 16;
        private const int GroupPadTop = 24;
        private const int GroupRowPitch = 32;
        private const int GroupLabelWidth = 60;
        private const int GroupEditorWidth = 90;
        private const int GroupFlagWidth = 150;     // 只有开关的分组
        private const int GroupButtonGap = 6;
        private const int ErrorIconWidth = 20;      // ErrorProvider 图标 16 + 间距；分组框会裁掉框外的图标

        private static readonly Color PanelBackColor = Color.FromArgb(30, 40, 30);
        private static readonly Color ButtonIconColor = Color.FromArgb(70, 110, 160);

        private readonly ErrorProvider _errors;
        private readonly ToolTip _toolTip;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Group> _groups = new List<Group>();
        private readonly Dictionary<ParamItem, string> _errorText = new Dictionary<ParamItem, string>();
        private Bitmap _sourceIcon;
        private int _rowsPerColumn = 6;
        private float _scale = 1f;
        private bool _updating;
        private bool _selectionClearPending;

        public ParamPanel()
        {
            AutoScroll = true;
            BackColor = PanelBackColor;
            ForeColor = Color.White;
            Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _toolTip = new ToolTip();
        }

        /// <summary> 每列的行数，排满后换到下一列；默认 6，与旧版槽位一致 </summary>
        public int RowsPerColumn
        {
            get => _rowsPerColumn;
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
                _rowsPerColumn = value;
                LayoutRows();
            }
        }

        /// <summary> 当前绑定的参数项 </summary>
        public IReadOnlyList<ParamItem> Items => _rows.Select(r => r.Item).ToList();

        /// <summary>
        /// 选择来源：宿主弹出变量树并按 <see cref="SourceParam.SourceType"/> 过滤。返回 null 表示取消。
        /// </summary>
        public Func<SourceParam, SourceRef?> SourcePicker { get; set; }

        /// <summary> 来源的显示文字（"默认" / "工具名/输出"），由宿主按工具列表拼出 </summary>
        public Func<SourceRef, string> SourceFormatter { get; set; } = s => s.IsLocal ? "默认" : s.ToString();

        /// <summary> 选择文件夹；参数是当前路径，返回 null 表示取消。默认弹 <see cref="FolderBrowserDialog"/> </summary>
        public Func<string, string> FolderPicker { get; set; } = PickFolder;

        /// <summary> 有参数真正被写回之后触发；参数里是变了的项 </summary>
        public event EventHandler<ParamsCommittedEventArgs> Committed;

        /// <summary> 用一组参数项重建面板；传 null 或空集合即清空 </summary>
        public void Bind(IEnumerable<ParamItem> items)
        {
            SuspendLayout();
            try
            {
                Clear();
                if (items == null) return;
                foreach (var item in items) AddRow(item);
                RefreshVisibility();
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        /// <summary> 从 getter 重新读一遍所有值（例如策略在执行中改了某个参数） </summary>
        public void RefreshValues()
        {
            foreach (var row in _rows) ShowValue(row);
            RefreshVisibility();
        }

        /// <summary> 某项的编辑控件；测试与宿主定位用 </summary>
        public Control EditorOf(ParamItem item) => RowOf(item)?.Editor;

        /// <summary> 某项的附加按钮（来源选择 / 文件夹选择）；没有时为 null </summary>
        public Button ButtonOf(ParamItem item) => RowOf(item)?.Button;

        /// <summary> 某项当前的校验错误；没有错误时为 null </summary>
        public string ErrorOf(ParamItem item) => item != null && _errorText.TryGetValue(item, out var text) ? text : null;

        /// <summary> 某项当前是否显示（按 <see cref="ParamItem.IsVisible"/> 求值后的结果） </summary>
        public bool IsRowVisible(ParamItem item)
        {
            var row = RowOf(item);
            return row != null && row.Editor.Visible;
        }

        /// <summary>
        /// 提交编辑控件里的文本（数值 / 文件夹）。控件失去焦点、按回车、选中下拉项时都会走到这里。
        /// </summary>
        /// <returns>校验通过返回 true（无论值是否真的变了）。</returns>
        public bool CommitText(ParamItem item)
        {
            var row = RowOf(item);
            if (row == null) return false;
            switch (item)
            {
                case NumberParam number:
                    if (!number.TryParse(row.Editor.Text, out object value, out string error))
                    {
                        SetError(row, error);
                        return false;
                    }
                    SetError(row, null);
                    Commit(row, value);
                    ShowValue(row);   // 规整显示格式（例如 "05" → "5"）
                    return true;
                case FolderParam _:
                    Commit(row, row.Editor.Text?.Trim() ?? string.Empty);
                    return true;
                default:
                    return true;
            }
        }

        #region 行

        private void Clear()
        {
            foreach (var row in _rows)
            {
                _errors.SetError(row.Editor, string.Empty);
                row.Label?.Dispose();
                row.Editor.Dispose();
                row.Button?.Dispose();
            }
            foreach (var group in _groups) group.Box.Dispose();
            _groups.Clear();
            _rows.Clear();
            _errorText.Clear();
            _toolTip.RemoveAll();
        }

        private void AddRow(ParamItem item)
        {
            var row = new Row { Item = item };

            switch (item)
            {
                case FlagParam flag:
                    var check = new CheckBox { Text = flag.Label, AutoSize = true };
                    check.CheckedChanged += (s, e) => { if (!_updating) Commit(row, check.Checked); };
                    row.Editor = check;
                    break;

                case ChoiceParam choice:
                    var list = NewComboBox(ComboBoxStyle.DropDownList);
                    list.Items.AddRange(choice.Options.Select(o => (object)o.Text).ToArray());
                    list.SelectedIndexChanged += (s, e) =>
                    {
                        if (_updating || list.SelectedIndex < 0) return;
                        Commit(row, choice.Options[list.SelectedIndex].Value);
                    };
                    row.Editor = list;
                    break;

                case NumberParam number:
                    var edit = NewComboBox(ComboBoxStyle.DropDown);
                    edit.Items.AddRange(number.Presets.Cast<object>().ToArray());
                    edit.SelectedIndexChanged += (s, e) => { if (!_updating && edit.SelectedIndex >= 0) CommitText(number); };
                    edit.Validating += (s, e) => { if (!_updating) CommitText(number); };
                    edit.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CommitText(number); e.SuppressKeyPress = true; } };
                    row.Editor = edit;
                    break;

                case SourceParam source:
                    row.Editor = NewTextBox(readOnly: true);
                    row.Button = NewButton(null, SourceIcon, (s, e) => PickSource(row, source));
                    break;

                case FolderParam folder:
                    var path = NewTextBox(readOnly: false);
                    path.Validating += (s, e) => { if (!_updating) CommitText(folder); };
                    path.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CommitText(folder); e.SuppressKeyPress = true; } };
                    path.TextChanged += (s, e) => _toolTip.SetToolTip(path, path.Text);   // 槽位窄, 完整路径看提示
                    row.Editor = path;
                    row.Button = NewButton("…", null, (s, e) => PickFolder(row));
                    break;

                default:
                    throw new NotSupportedException($"不支持的参数类型: {item.GetType().Name}");
            }

            Control host = this;
            if (item.Group != null)
            {
                var group = GroupOf(item.Group);
                group.Rows.Add(row);
                host = group.Box;
            }
            if (!(item is FlagParam))
            {
                row.Label = new Label { Text = item.Label, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
                host.Controls.Add(row.Label);
            }
            host.Controls.Add(row.Editor);
            if (row.Button != null) host.Controls.Add(row.Button);

            _rows.Add(row);
            ShowValue(row);
        }

        private Group GroupOf(string title)
        {
            var group = _groups.FirstOrDefault(g => g.Box.Text == title);
            if (group != null) return group;
            group = new Group { Box = new GroupBox { Text = title, ForeColor = Color.White } };
            _groups.Add(group);
            Controls.Add(group.Box);
            return group;
        }

        // 编辑控件显式用白底黑字：面板本身是深色，不显式设置会继承白色前景
        private static ComboBox NewComboBox(ComboBoxStyle style)
            => new ComboBox { DropDownStyle = style, BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText };

        private static TextBox NewTextBox(bool readOnly)
            => new TextBox { ReadOnly = readOnly, TabStop = !readOnly, ForeColor = SystemColors.WindowText };   // 只读框拿到焦点会全选高亮

        private static Button NewButton(string text, Image image, EventHandler click)
        {
            var button = new Button
            {
                Text = text ?? string.Empty,
                Image = image,
                ImageAlign = ContentAlignment.MiddleCenter,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                ForeColor = Color.Black,
                UseVisualStyleBackColor = false,
                TabStop = false,
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += click;
            return button;
        }

        /// <summary> 来源按钮的图标："|←"（从上游取值），对应旧版 btn_100..115 的背景图 </summary>
        private Bitmap SourceIcon
        {
            get
            {
                if (_sourceIcon != null) return _sourceIcon;
                var bitmap = new Bitmap(18, 12);
                using (var g = Graphics.FromImage(bitmap))
                using (var pen = new Pen(ButtonIconColor, 2f))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLine(pen, 2, 1, 2, 11);       // 竖线
                    g.DrawLine(pen, 4, 6, 17, 6);       // 箭杆
                    g.DrawLine(pen, 4, 6, 8, 2);        // 箭头
                    g.DrawLine(pen, 4, 6, 8, 10);
                }
                return _sourceIcon = bitmap;
            }
        }

        private int Px(int pixels) => (int)Math.Round(pixels * _scale);

        /// <summary>
        /// 不分组的可见行按"先列后行"排进定宽槽位，分组框依次排在右边；隐藏的行不占位置。
        /// </summary>
        private void LayoutRows()
        {
            SuspendLayout();
            try
            {
                var origin = AutoScrollPosition;   // 滚动后子控件的坐标是相对可视区域的
                int slot = 0, right = 0, bottom = 0;
                foreach (var row in _rows)
                {
                    if (!row.Item.IsVisible || row.Item.Group != null) continue;
                    int x = origin.X + Px(SlotLeft + slot / _rowsPerColumn * ColumnWidth);
                    int y = origin.Y + Px(SlotTop + slot % _rowsPerColumn * RowPitch);
                    int editorX = x + Px(LabelWidth);

                    if (row.Label != null)
                    {
                        row.Label.Bounds = new Rectangle(x, y, Px(LabelWidth - 2), Px(EditorHeight));
                        row.Editor.SetBounds(editorX, y, Px(EditorWidth), Px(EditorHeight));
                    }
                    else
                    {
                        row.Editor.Location = new Point(x, y + Px(2));   // 开关独占标签 + 编辑框的宽度
                    }
                    if (row.Button != null)
                    {
                        row.Button.Bounds = new Rectangle(
                            editorX + Px(EditorWidth + ButtonGap), y + Px((EditorHeight - ButtonHeight) / 2),
                            Px(ButtonWidth), Px(ButtonHeight));
                        // ErrorProvider 的图标放到按钮右边，不压住按钮
                        _errors.SetIconPadding(row.Editor, Px(ButtonGap + ButtonWidth + 2));
                    }

                    right = Math.Max(right, x - origin.X + Px(ColumnWidth));
                    bottom = Math.Max(bottom, y - origin.Y + Px(EditorHeight + SlotTop));   // 末行下面只留边距, 不按行距算
                    slot++;
                }
                LayoutGroups(origin, ref right, ref bottom);
                AutoScrollMinSize = new Size(right, bottom);
                // 停靠在顶部时高度跟随内容，免得宿主写死的高度装不下新增的行
                if (Dock == DockStyle.Top) Height = bottom + (Height - ClientSize.Height);
            }
            finally
            {
                ResumeLayout(true);
            }
            ClearNumberSelection();
        }

        /// <summary>
        /// 分组框：组内一行一项，框高取所有分组里最高的那个、且不低于面板可视高度，排起来是一排等高的框。
        /// 滚动区域只按内容高度算：拉伸出来的部分不该在出现横向滚动条时再引出一条竖向滚动条。
        /// </summary>
        private void LayoutGroups(Point origin, ref int right, ref int bottom)
        {
            var visible = _groups.Where(g => g.Rows.Any(r => r.Item.IsVisible)).ToList();
            foreach (var group in _groups) group.Box.Visible = visible.Contains(group);
            if (visible.Count == 0) return;

            int contentHeight = visible.Max(g => GroupPadTop + g.Rows.Count(r => r.Item.IsVisible) * GroupRowPitch);
            int height = Math.Max(Px(contentHeight), ClientSize.Height - Px(SlotTop) * 2);
            int x = right > 0 ? right : Px(GroupLeft);   // 槽位列的列宽里已经留了间距
            foreach (var group in visible)
            {
                bool valued = group.Rows.Any(r => !(r.Item is FlagParam));
                bool buttons = group.Rows.Any(r => r.Button != null);
                int width = valued ? GroupPadLeft * 2 + GroupLabelWidth + GroupEditorWidth + ErrorIconWidth : GroupFlagWidth;
                if (buttons) width += GroupButtonGap + ButtonWidth;
                group.Box.Bounds = new Rectangle(origin.X + x, origin.Y + Px(SlotTop), Px(width), height);

                int index = 0;
                foreach (var row in group.Rows)
                {
                    if (!row.Item.IsVisible) continue;
                    int y = Px(GroupPadTop + index++ * GroupRowPitch);
                    int editorX = Px(GroupPadLeft + GroupLabelWidth);
                    if (row.Label != null)
                    {
                        row.Label.Bounds = new Rectangle(Px(GroupPadLeft), y, Px(GroupLabelWidth - 4), Px(EditorHeight));
                        row.Editor.SetBounds(editorX, y, Px(GroupEditorWidth), Px(EditorHeight));
                        // ErrorProvider 的图标画在分组框里，放到按钮右边、框宽里预留的位置
                        _errors.SetIconPadding(row.Editor, row.Button != null ? Px(GroupButtonGap + ButtonWidth + 2) : Px(2));
                    }
                    else
                    {
                        row.Editor.Location = new Point(Px(GroupPadLeft), y + Px(2));
                    }
                    if (row.Button != null)
                    {
                        row.Button.Bounds = new Rectangle(editorX + Px(GroupEditorWidth + GroupButtonGap), y + Px((EditorHeight - ButtonHeight) / 2),
                            Px(ButtonWidth), Px(ButtonHeight));
                    }
                }

                x += Px(width + GroupGap);
            }
            right = x;
            bottom = Math.Max(bottom, Px(SlotTop) * 2 + Px(contentHeight));
        }

        // 跟 ClientSize 而不是 Size：滚动条出现 / 消失只改可视区域，分组框的高度也要跟上
        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            if (_groups.Count > 0) LayoutRows();
        }

        /// <summary>
        /// 下拉可编辑的 ComboBox 在创建句柄、改尺寸后会把文本全选，看起来像被选中了；
        /// 句柄就绪后统一清一次（只处理没有焦点的，不打扰正在输入的）。
        /// </summary>
        private void ClearNumberSelection()
        {
            if (!IsHandleCreated || _selectionClearPending) return;   // 拖动改尺寸时连续布局, 只投递一次
            _selectionClearPending = true;
            BeginInvoke((Action)(() =>
            {
                _selectionClearPending = false;
                foreach (var row in _rows)
                {
                    if (row.Item is NumberParam && row.Editor is ComboBox combo && !combo.IsDisposed && !combo.Focused)
                        combo.SelectionLength = 0;
                }
            }));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (var g = CreateGraphics()) _scale = g.DpiX / 96f;
            LayoutRows();
        }

        private Row RowOf(ParamItem item) => _rows.FirstOrDefault(r => ReferenceEquals(r.Item, item));

        #endregion

        #region 值

        private void ShowValue(Row row)
        {
            _updating = true;
            try
            {
                switch (row.Item)
                {
                    case FlagParam flag:
                        ((CheckBox)row.Editor).Checked = flag.Value;
                        break;
                    case ChoiceParam choice:
                        ((ComboBox)row.Editor).SelectedIndex = choice.SelectedIndex;
                        break;
                    case NumberParam number:
                        row.Editor.Text = number.Format();
                        ((ComboBox)row.Editor).SelectionLength = 0;   // 程序赋值时 WinForms 会全选文本, 看起来像被选中了
                        break;
                    case SourceParam source:
                        row.Editor.Text = SourceFormatter(source.Value);
                        break;
                    case FolderParam folder:
                        row.Editor.Text = folder.Value ?? string.Empty;
                        break;
                }
            }
            finally
            {
                _updating = false;
            }
        }

        private void Commit(Row row, object value)
        {
            if (!row.Item.TrySetValue(value)) return;
            Committed?.Invoke(this, new ParamsCommittedEventArgs(new[] { row.Item }));
            RefreshVisibility();
        }

        private void SetError(Row row, string error)
        {
            if (error == null) _errorText.Remove(row.Item);
            else _errorText[row.Item] = error;
            _errors.SetError(row.Editor, error ?? string.Empty);
        }

        private void RefreshVisibility()
        {
            foreach (var row in _rows)
            {
                bool visible = row.Item.IsVisible;
                if (row.Label != null) row.Label.Visible = visible;
                row.Editor.Visible = visible;
                if (row.Button != null) row.Button.Visible = visible;
            }
            LayoutRows();
        }

        private void PickSource(Row row, SourceParam source)
        {
            var picked = SourcePicker?.Invoke(source);
            if (!picked.HasValue) return;
            Commit(row, picked.Value);
            ShowValue(row);
        }

        private void PickFolder(Row row)
        {
            var picked = FolderPicker?.Invoke(row.Editor.Text);
            if (picked == null) return;
            row.Editor.Text = picked;
            CommitText(row.Item);
        }

        private static string PickFolder(string current)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                try { dialog.SelectedPath = current; } catch (ArgumentException) { }
                return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _errors.Dispose();
                _toolTip.Dispose();
                _sourceIcon?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public sealed class ParamsCommittedEventArgs : EventArgs
    {
        public ParamsCommittedEventArgs(IReadOnlyList<ParamItem> changed)
        {
            Changed = changed;
        }

        /// <summary> 真正变了的项 </summary>
        public IReadOnlyList<ParamItem> Changed { get; }
    }
}
