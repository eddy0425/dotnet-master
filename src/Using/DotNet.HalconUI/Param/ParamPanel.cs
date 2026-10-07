using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DotNet.Drawing;
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
    /// 写回本身可以交给宿主（<see cref="ValueWriter"/>，例如投递到执行会话、在两帧之间执行），
    /// 执行完之前输入框显示为待生效。
    /// </para>
    /// <para>
    /// 外观沿用旧版 ParaForm：深绿底、定宽槽位（标签 + 130 宽编辑框 + 图标按钮），
    /// 每列排满 <see cref="RowsPerColumn"/> 行后换到下一列；隐藏的项不占位置。
    /// 声明了 <see cref="ParamItem.Group"/> 的项画进同名分组框，分组框依次排在槽位列的右边。
    /// 不分组的文件夹参数路径长，各占一整行排在最上面，槽位列和分组框排在它们下面。
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
            public Button OpenButton;   // 文件夹行的"打开路径"
            public int Pending;         // 还没执行完的写回 (宿主排队执行时)
            public Color SavedBackColor;
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
        private const int WideEditorWidth = 320;   // 文件夹路径的整行编辑框
        private const int OpenButtonGap = 11;      // "设置路径"与"打开路径"两个按钮之间

        // 分组框尺寸，取自旧版"显示输出"页的 grb_Display / grb_Font
        private const int GroupLeft = 20;
        private const int GroupGap = 30;
        private const int GroupPadLeft = 16;
        private const int GroupPadTop = 24;
        private const int GroupPadBottom = 8;       // 末行行距之外再留的底边距
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
        private Bitmap _folderIcon;
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
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<SourceParam, SourceRef?> SourcePicker { get; set; }

        /// <summary> 来源的显示文字（"默认" / "工具名/输出"），由宿主按工具列表拼出 </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<SourceRef, string> SourceFormatter { get; set; } = s => s.IsLocal ? "默认" : s.ToString();

        /// <summary> 选择文件夹；参数是当前路径，返回 null 表示取消。默认弹 <see cref="FolderBrowserDialog"/> </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, string> FolderPicker { get; set; } = PickFolder;

        /// <summary> 选择文件；参数是过滤器与当前路径，返回 null 表示取消。默认弹 <see cref="OpenFileDialog"/> </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<string, string, string> FilePicker { get; set; } = PickFile;

        /// <summary>
        /// 给来源列表添一项：宿主弹出变量树并按 <see cref="SourceListParam.SourceType"/> 过滤（不能选"默认"）。返回 null 表示取消。
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<SourceListParam, SourceRef?> SourceListPicker { get; set; }

        /// <summary> 打开文件夹（参数是已存在的目录）。默认用资源管理器打开 </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Action<string> FolderOpener { get; set; } = OpenFolder;

        /// <summary>
        /// 写回一项：参数是项与新值，返回的任务给出"值是否真的变了"（即 <see cref="ParamItem.TrySetValue"/> 的结果）。
        /// 宿主用它把写回排到执行会话里、在两帧之间执行；为 null 时就地写回。任务没完成之前，该项的输入框显示为待生效。
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Func<ParamItem, object, Task<bool>> ValueWriter { get; set; }

        /// <summary> 待生效的背景色：写回已提交、宿主还没执行完 </summary>
        public static readonly Color PendingBackColor = Color.Khaki;

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

        /// <summary> 文件夹项的"打开路径"按钮；其他项为 null </summary>
        public Button OpenButtonOf(ParamItem item) => RowOf(item)?.OpenButton;

        /// <summary> 某项是否有已提交、还没执行完的写回 </summary>
        public bool IsPending(ParamItem item) => RowOf(item)?.Pending > 0;

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
                    Commit(row, value, showAfter: true);   // 写回后规整显示格式（例如 "05" → "5"）
                    return true;
                case FolderParam _:
                case FileParam _:
                    SetError(row, null);
                    Commit(row, row.Editor.Text?.Trim() ?? string.Empty);
                    return true;
                case TextParam _:
                    SetError(row, null);
                    Commit(row, row.Editor.Text ?? string.Empty);   // 文本原样保存, 空格可能就是期望值的一部分
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
                row.OpenButton?.Dispose();
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
                    // 与旧版一致："|←" 设置路径，文件夹图标打开路径
                    row.Button = NewButton(null, SourceIcon, (s, e) => PickFolder(row));
                    row.OpenButton = NewButton(null, FolderIcon, (s, e) => OpenFolder(row));
                    _toolTip.SetToolTip(row.Button, "设置路径");
                    _toolTip.SetToolTip(row.OpenButton, "打开路径");
                    break;

                case TextParam text:
                    var textBox = NewTextBox(readOnly: false);
                    textBox.Validating += (s, e) => { if (!_updating) CommitText(text); };
                    textBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CommitText(text); e.SuppressKeyPress = true; } };
                    row.Editor = textBox;
                    break;

                case FileParam file:
                    var filePath = NewTextBox(readOnly: false);
                    filePath.Validating += (s, e) => { if (!_updating) CommitText(file); };
                    filePath.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CommitText(file); e.SuppressKeyPress = true; } };
                    filePath.TextChanged += (s, e) => _toolTip.SetToolTip(filePath, filePath.Text);
                    row.Editor = filePath;
                    row.Button = NewButton(null, FolderIcon, (s, e) => PickFile(row, file));
                    _toolTip.SetToolTip(row.Button, "选择文件");
                    break;

                case ActionParam action:
                    // 按钮本身就是这一行: 文字即标签, 按下时写回 Press, 和其它参数走同一条写回路径
                    var press = NewButton(action.Label, null, (s, e) => Commit(row, ActionParam.Press));
                    press.TabStop = true;
                    press.FlatAppearance.BorderSize = 1;
                    row.Editor = press;
                    break;

                case SourceListParam sources:
                    // 下拉里列出全部来源; "+" 从变量树添一项, "−" 去掉下拉里选中的那一项
                    row.Editor = NewComboBox(ComboBoxStyle.DropDownList);
                    row.Button = NewButton("+", null, (s, e) => AddListSource(row, sources));
                    row.OpenButton = NewButton("−", null, (s, e) => RemoveListSource(row, sources));
                    _toolTip.SetToolTip(row.Button, "添加来源");
                    _toolTip.SetToolTip(row.OpenButton, "移除选中的来源");
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
            if (!(item is FlagParam) && !(item is ActionParam))
            {
                row.Label = new Label { Text = item.Label, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
                host.Controls.Add(row.Label);
            }
            host.Controls.Add(row.Editor);
            if (row.Button != null) host.Controls.Add(row.Button);
            if (row.OpenButton != null) host.Controls.Add(row.OpenButton);

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

        /// <summary> "打开路径"按钮的图标：打开的文件夹，对应旧版 btn_openPath 的背景图 </summary>
        private Bitmap FolderIcon
        {
            get
            {
                if (_folderIcon != null) return _folderIcon;
                var bitmap = new Bitmap(18, 14);
                using (var g = Graphics.FromImage(bitmap))
                using (var pen = new Pen(ButtonIconColor, 1.5f))
                using (var brush = new SolidBrush(ButtonIconColor))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.DrawLines(pen, new[] { new PointF(1, 12), new PointF(1, 2), new PointF(6, 2), new PointF(8, 4), new PointF(15, 4), new PointF(15, 6) });
                    g.FillPolygon(brush, new[] { new PointF(1, 13), new PointF(4, 7), new PointF(17, 7), new PointF(14, 13) });   // 前片
                }
                return _folderIcon = bitmap;
            }
        }

        /// <summary> 不分组的路径 / 来源列表内容长，各占一整行 </summary>
        private static bool IsWide(Row row)
            => (row.Item is FolderParam || row.Item is FileParam || row.Item is SourceListParam) && row.Item.Group == null;

        private int Px(int pixels) => (int)Math.Round(pixels * _scale);

        /// <summary>
        /// 宽行（不分组的文件夹）各占一整行排在最上面；其余不分组的可见行在它们下面按"先列后行"排进定宽槽位，
        /// 分组框依次排在槽位右边；隐藏的行不占位置。
        /// </summary>
        private void LayoutRows()
        {
            SuspendLayout();
            try
            {
                var origin = AutoScrollPosition;   // 滚动后子控件的坐标是相对可视区域的
                int slot = 0, right = 0, bottom = 0, top = Px(SlotTop);
                foreach (var row in _rows)
                {
                    if (!row.Item.IsVisible || !IsWide(row)) continue;
                    int x = origin.X + Px(SlotLeft), y = origin.Y + top;
                    int editorX = x + Px(LabelWidth);
                    row.Label.Bounds = new Rectangle(x, y, Px(LabelWidth - 2), Px(EditorHeight));
                    row.Editor.SetBounds(editorX, y, Px(WideEditorWidth), Px(EditorHeight));
                    row.Button.Bounds = new Rectangle(
                        editorX + Px(WideEditorWidth + ButtonGap), y + Px((EditorHeight - ButtonHeight) / 2),
                        Px(ButtonWidth), Px(ButtonHeight));
                    if (row.OpenButton != null)
                        row.OpenButton.Bounds = new Rectangle(row.Button.Right + Px(OpenButtonGap), row.Button.Top, Px(ButtonWidth), Px(ButtonHeight));
                    int buttons = row.OpenButton != null ? ButtonGap + ButtonWidth + OpenButtonGap + ButtonWidth : ButtonGap + ButtonWidth;
                    _errors.SetIconPadding(row.Editor, Px(buttons + 2));

                    right = Math.Max(right, Px(SlotLeft + LabelWidth + WideEditorWidth + ButtonGap + ButtonWidth + OpenButtonGap + ButtonWidth + ErrorIconWidth));
                    bottom = Math.Max(bottom, top + Px(EditorHeight + SlotTop));
                    top += Px(RowPitch);
                }
                int wideRight = right;   // 宽行下面的槽位和分组框从左边重新排
                right = 0;
                foreach (var row in _rows)
                {
                    if (!row.Item.IsVisible || row.Item.Group != null || IsWide(row)) continue;
                    int x = origin.X + Px(SlotLeft + slot / _rowsPerColumn * ColumnWidth);
                    int y = origin.Y + top + Px(slot % _rowsPerColumn * RowPitch);
                    int editorX = x + Px(LabelWidth);

                    if (row.Label != null)
                    {
                        row.Label.Bounds = new Rectangle(x, y, Px(LabelWidth - 2), Px(EditorHeight));
                        row.Editor.SetBounds(editorX, y, Px(EditorWidth), Px(EditorHeight));
                    }
                    else if (row.Item is ActionParam)
                    {
                        row.Editor.SetBounds(x, y, Px(LabelWidth + EditorWidth), Px(EditorHeight));   // 按钮占标签 + 编辑框的宽度
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
                LayoutGroups(origin, top, ref right, ref bottom);
                right = Math.Max(right, wideRight);
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
        /// 分组框：组内一行一项，框高按内容算、取所有分组里最高的那个，排起来是一排等高的框；
        /// 不再拉伸到面板底部，免得只有两三行的分组下面空出一大块。
        /// </summary>
        private void LayoutGroups(Point origin, int top, ref int right, ref int bottom)
        {
            var visible = _groups.Where(g => g.Rows.Any(r => r.Item.IsVisible)).ToList();
            foreach (var group in _groups) group.Box.Visible = visible.Contains(group);
            if (visible.Count == 0) return;

            int height = Px(visible.Max(g => GroupPadTop + g.Rows.Count(r => r.Item.IsVisible) * GroupRowPitch) + GroupPadBottom);
            int x = right > 0 ? right : Px(GroupLeft);   // 槽位列的列宽里已经留了间距
            foreach (var group in visible)
            {
                bool valued = group.Rows.Any(r => !(r.Item is FlagParam));   // 按钮也按"标签 + 编辑框"的宽度算
                bool buttons = group.Rows.Any(r => r.Button != null);
                bool openButtons = group.Rows.Any(r => r.OpenButton != null);
                int width = valued ? GroupPadLeft * 2 + GroupLabelWidth + GroupEditorWidth + ErrorIconWidth : GroupFlagWidth;
                if (buttons) width += GroupButtonGap + ButtonWidth;
                if (openButtons) width += GroupButtonGap + ButtonWidth;
                group.Box.Bounds = new Rectangle(origin.X + x, origin.Y + top, Px(width), height);

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
                        int buttonCount = (row.Button != null ? 1 : 0) + (row.OpenButton != null ? 1 : 0);
                        _errors.SetIconPadding(row.Editor, Px(buttonCount * (GroupButtonGap + ButtonWidth) + 2));
                    }
                    else if (row.Item is ActionParam)
                    {
                        row.Editor.SetBounds(Px(GroupPadLeft), y, Px(GroupLabelWidth + GroupEditorWidth), Px(EditorHeight));
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
                    if (row.OpenButton != null)
                        row.OpenButton.Bounds = new Rectangle(row.Button.Right + Px(GroupButtonGap), row.Button.Top, Px(ButtonWidth), Px(ButtonHeight));
                }

                x += Px(width + GroupGap);
            }
            right = x;
            bottom = Math.Max(bottom, top + height + Px(SlotTop));
        }

        // 停靠在顶部时出现 / 消失横向滚动条会改变可视高度，要重算一次面板高度
        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            if (Dock == DockStyle.Top && _rows.Count > 0) LayoutRows();
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
                    case TextParam text:
                        row.Editor.Text = text.Value ?? string.Empty;
                        break;
                    case FileParam file:
                        row.Editor.Text = file.Value ?? string.Empty;
                        break;
                    case SourceListParam list:
                        var combo = (ComboBox)row.Editor;
                        combo.Items.Clear();
                        foreach (var source in list.Value) combo.Items.Add(SourceFormatter(source));
                        combo.SelectedIndex = combo.Items.Count - 1;
                        _toolTip.SetToolTip(combo, string.Join(Environment.NewLine, combo.Items.Cast<string>()));
                        break;
                }
            }
            finally
            {
                _updating = false;
            }
        }

        /// <summary>
        /// 写回一项。交给 <see cref="ValueWriter"/> 时可能要等宿主执行（连续运行中排在两帧之间），
        /// 等待期间输入框显示为待生效；就地写回或任务已完成时同步走完，时序与直接写回相同。
        /// </summary>
        private async void Commit(Row row, object value, bool showAfter = false)
        {
            bool changed;
            var writer = ValueWriter;
            if (writer == null)
            {
                changed = row.Item.TrySetValue(value);
            }
            else
            {
                Task<bool> task;
                try { task = writer(row.Item, value) ?? Task.FromResult(false); }
                catch (Exception ex)
                {
                    WriteFailed(row, ex);
                    return;
                }
                bool pending = !task.IsCompleted;
                if (pending) SetPending(row, true);
                try
                {
                    changed = await task;
                }
                catch (Exception ex)
                {
                    WriteFailed(row, ex);
                    return;
                }
                finally
                {
                    if (pending) SetPending(row, false);
                }
                // 等待期间面板可能已经换绑到别的工具: 这一行已不在面板上, 不再刷新它
                if (IsDisposed || !_rows.Contains(row)) return;
            }

            if (changed)
            {
                Committed?.Invoke(this, new ParamsCommittedEventArgs(new[] { row.Item }));
                RefreshVisibility();
            }
            if (showAfter) ShowValue(row);
        }

        private void WriteFailed(Row row, Exception ex)
        {
            Log.Warn(nameof(ParamPanel), $"参数 '{row.Item.Label}' 写回失败.", ex);
            if (!IsDisposed && _rows.Contains(row)) SetError(row, ex.Message);
        }

        private void SetPending(Row row, bool pending)
        {
            if (pending)
            {
                if (row.Pending++ == 0)
                {
                    row.SavedBackColor = row.Editor.BackColor;
                    row.Editor.BackColor = PendingBackColor;
                }
            }
            else if (row.Pending > 0 && --row.Pending == 0 && !row.Editor.IsDisposed)
            {
                row.Editor.BackColor = row.SavedBackColor;
            }
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
                if (row.OpenButton != null) row.OpenButton.Visible = visible;
            }
            LayoutRows();
        }

        private void PickSource(Row row, SourceParam source)
        {
            var picked = SourcePicker?.Invoke(source);
            if (!picked.HasValue) return;
            Commit(row, picked.Value, showAfter: true);
        }

        private void AddListSource(Row row, SourceListParam list)
        {
            if (list.MaxCount > 0 && list.Value.Count >= list.MaxCount)
            {
                SetError(row, $"最多 {list.MaxCount} 项");
                return;
            }
            var picked = SourceListPicker?.Invoke(list);
            if (!picked.HasValue || picked.Value.IsLocal) return;
            SetError(row, null);
            Commit(row, list.Value.Concat(new[] { picked.Value }).ToList(), showAfter: true);
        }

        private void RemoveListSource(Row row, SourceListParam list)
        {
            int index = ((ComboBox)row.Editor).SelectedIndex;
            if (index < 0 || index >= list.Value.Count) return;
            var next = list.Value.ToList();
            next.RemoveAt(index);
            SetError(row, null);
            Commit(row, next, showAfter: true);
        }

        private void PickFile(Row row, FileParam file)
        {
            var picked = FilePicker?.Invoke(file.Filter, row.Editor.Text);
            if (picked == null) return;
            row.Editor.Text = picked;
            CommitText(file);
        }

        private static string PickFile(string filter, string current)
        {
            using (var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = false })
            {
                try { if (!string.IsNullOrEmpty(current)) dialog.FileName = current; } catch (ArgumentException) { }
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }

        private void PickFolder(Row row)
        {
            var picked = FolderPicker?.Invoke(row.Editor.Text);
            if (picked == null) return;
            row.Editor.Text = picked;
            CommitText(row.Item);
        }

        private void OpenFolder(Row row)
        {
            string path = row.Editor.Text?.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                SetError(row, "目录不存在");
                return;
            }
            try
            {
                FolderOpener?.Invoke(path);
                SetError(row, null);
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
            {
                SetError(row, "无法打开目录: " + ex.Message);   // 在点击回调里, 不能让异常冒出去
            }
        }

        // ShellExecute 直接打开目录，不用自己拼引号（路径以 \ 结尾时 "C:\a\" 会被当成转义）
        private static void OpenFolder(string path)
        {
            using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })) { }
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
                _folderIcon?.Dispose();
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
