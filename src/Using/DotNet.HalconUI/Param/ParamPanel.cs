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

        private readonly TableLayoutPanel _table;
        private readonly ErrorProvider _errors;
        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<ParamItem, string> _errorText = new Dictionary<ParamItem, string>();
        private bool _updating;

        public ParamPanel()
        {
            AutoScroll = true;
            _errors = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };
            _table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Padding = new Padding(4, 2, 18, 2),   // 右侧给 ErrorProvider 的图标留位置
            };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            Controls.Add(_table);
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
            _table.SuspendLayout();
            try
            {
                Clear();
                if (items == null) return;
                foreach (var item in items) AddRow(item);
                _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                RefreshVisibility();
            }
            finally
            {
                _table.ResumeLayout(true);
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
            _rows.Clear();
            _errorText.Clear();
            _table.Controls.Clear();
            _table.RowStyles.Clear();
            _table.RowCount = 0;
        }

        private void AddRow(ParamItem item)
        {
            var row = new Row { Item = item };
            int index = _table.RowCount;
            _table.RowCount = index + 1;
            _table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            switch (item)
            {
                case FlagParam flag:
                    var check = new CheckBox { Text = flag.Label, AutoSize = true, Margin = new Padding(3, 4, 3, 2) };
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
                    row.Editor = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 2) };
                    row.Button = NewButton("…", (s, e) => PickSource(row, source));
                    break;

                case FolderParam folder:
                    var path = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 2) };
                    path.Validating += (s, e) => { if (!_updating) CommitText(folder); };
                    path.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { CommitText(folder); e.SuppressKeyPress = true; } };
                    row.Editor = path;
                    row.Button = NewButton("…", (s, e) => PickFolder(row));
                    break;

                default:
                    throw new NotSupportedException($"不支持的参数类型: {item.GetType().Name}");
            }

            if (!(item is FlagParam))
            {
                row.Label = new Label { Text = item.Label, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
                _table.Controls.Add(row.Label, 0, index);
                _table.Controls.Add(row.Editor, 1, index);
            }
            else
            {
                _table.Controls.Add(row.Editor, 0, index);
                _table.SetColumnSpan(row.Editor, 2);
            }
            if (row.Button != null) _table.Controls.Add(row.Button, 2, index);

            _rows.Add(row);
            ShowValue(row);
        }

        private static ComboBox NewComboBox(ComboBoxStyle style)
            => new ComboBox { DropDownStyle = style, Dock = DockStyle.Fill, Margin = new Padding(3, 2, 3, 2) };

        private static Button NewButton(string text, EventHandler click)
        {
            var button = new Button { Text = text, Width = 26, Height = 24, Margin = new Padding(2, 2, 2, 2) };
            button.Click += click;
            return button;
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
            _table.SuspendLayout();
            foreach (var row in _rows)
            {
                bool visible = row.Item.IsVisible;
                if (row.Label != null) row.Label.Visible = visible;
                row.Editor.Visible = visible;
                if (row.Button != null) row.Button.Visible = visible;
            }
            _table.ResumeLayout(true);
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
            if (disposing) _errors.Dispose();
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
