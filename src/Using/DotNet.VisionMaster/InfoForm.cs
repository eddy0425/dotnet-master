using System;
using System.IO;
using System.Linq;
using System.Drawing;
using DotNet.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Collections.Generic;

namespace DotNet.VisionMaster
{
    /// <summary> 信息级别：决定计数归类与显示颜色 </summary>
    internal enum InfoLevel { Info, Warn, Error }

    /// <summary>
    /// 信息窗口：按时间列出运行信息，可按提示 / 警告 / 错误筛选；错误另存一份作为报警历史。
    /// </summary>
    /// <remarks>
    /// 参考旧项目 Form_Infor。<see cref="Write"/> 可在任意线程调用，统一封送到界面线程处理，
    /// 不依赖 CheckForIllegalCrossThreadCalls = false。信息与报警各保留最近 <see cref="Capacity"/> 条。
    /// </remarks>
    public partial class InfoForm : UserControl
    {
        internal const int Capacity = 1000;

        private struct Entry
        {
            public DateTime Time;
            public InfoLevel Level;
            public string Message;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _alarms = new List<Entry>();
        private bool _ascending;

        public InfoForm()
        {
            InitializeComponent();
            Dock = DockStyle.Fill;
            UpdateCount();
        }

        internal void Info(string message) => Write(InfoLevel.Info, message);

        internal void Warn(string message) => Write(InfoLevel.Warn, message);

        internal void Error(string message) => Write(InfoLevel.Error, message);

        /// <summary> 追加一条信息；多行内容压成一行（列表只显示一行） </summary>
        internal void Write(InfoLevel level, string message)
        {
            if (string.IsNullOrEmpty(message) || IsDisposed) return;
            var entry = new Entry { Time = DateTime.Now, Level = level, Message = message.Replace(Environment.NewLine, "    ") };
            if (!InvokeRequired)
            {
                Append(entry);
                return;
            }
            // 窗口正在销毁时 BeginInvoke 会抛异常, 此时丢掉这条信息即可
            try { BeginInvoke((Action)(() => Append(entry))); }
            catch (InvalidOperationException) { }
        }

        /// <summary> 当前视图里显示的条数（供测试） </summary>
        internal int VisibleCount => lst_log.Items.Count;

        private void Append(Entry entry)
        {
            if (IsDisposed) return;
            bool trimmed = Add(_entries, entry);
            if (entry.Level == InfoLevel.Error) trimmed |= Add(_alarms, entry);
            UpdateCount();
            if (mnu_pause.Checked) return;
            if (trimmed) RebuildList();
            else if (InView(entry)) ShowEntry(entry);
        }

        /// <summary> 超出容量时一次淘汰最旧的十分之一，避免满了以后每来一条都重建列表 </summary>
        private static bool Add(List<Entry> list, Entry entry)
        {
            list.Add(entry);
            if (list.Count <= Capacity) return false;
            list.RemoveRange(0, Capacity / 10);
            return true;
        }

        private InfoLevel? FilterLevel =>
            tsb_tip.Checked ? InfoLevel.Info :
            tsb_warn.Checked ? InfoLevel.Warn :
            tsb_error.Checked ? InfoLevel.Error : (InfoLevel?)null;

        private bool InView(Entry entry)
        {
            if (tsb_alarm.Checked) return entry.Level == InfoLevel.Error;
            var level = FilterLevel;
            return level == null || level == entry.Level;
        }

        /// <summary> 当前视图的数据源：报警历史，或按级别筛选后的信息 </summary>
        private IEnumerable<Entry> ViewEntries() => tsb_alarm.Checked ? _alarms : _entries.Where(InView);

        private void ShowEntry(Entry entry)
        {
            if (_ascending)
            {
                lst_log.Items.Add(CreateItem(entry));
                lst_log.EnsureVisible(lst_log.Items.Count - 1);
            }
            else
            {
                lst_log.Items.Insert(0, CreateItem(entry));
                lst_log.EnsureVisible(0);
            }
        }

        private void RebuildList()
        {
            var items = ViewEntries().Select(CreateItem);
            if (!_ascending) items = items.Reverse();
            lst_log.BeginUpdate();
            try
            {
                lst_log.Items.Clear();
                lst_log.Items.AddRange(items.ToArray());
                int count = lst_log.Items.Count;
                if (count > 0) lst_log.EnsureVisible(_ascending ? count - 1 : 0);
            }
            finally { lst_log.EndUpdate(); }
        }

        private static ListViewItem CreateItem(Entry entry)
        {
            var item = new ListViewItem(entry.Time.ToString("HH:mm:ss")) { ForeColor = ColorOf(entry.Level) };
            item.SubItems.Add(entry.Message);
            return item;
        }

        private static Color ColorOf(InfoLevel level)
        {
            switch (level)
            {
                case InfoLevel.Warn: return Color.Yellow;
                case InfoLevel.Error: return Color.Red;
                default: return Color.White;
            }
        }

        private void UpdateCount()
        {
            tsb_tip.Text = $"提示({_entries.Count(e => e.Level == InfoLevel.Info)})";
            tsb_warn.Text = $"警告({_entries.Count(e => e.Level == InfoLevel.Warn)})";
            tsb_error.Text = $"错误({_entries.Count(e => e.Level == InfoLevel.Error)})";
            tsb_alarm.Text = $"报警({_alarms.Count})";
        }

        /// <summary> 四个筛选按钮互斥；再点一次已选中的按钮即取消筛选 </summary>
        private void tsb_filter_Click(object sender, EventArgs e)
        {
            foreach (var button in new[] { tsb_tip, tsb_warn, tsb_error, tsb_alarm })
            {
                if (button != sender) button.Checked = false;
            }
            RebuildList();
        }

        /// <summary> 查看报警历史时只清报警，否则清全部信息（报警历史保留） </summary>
        private void mnu_clear_Click(object sender, EventArgs e)
        {
            if (tsb_alarm.Checked) _alarms.Clear();
            else _entries.Clear();
            UpdateCount();
            RebuildList();
        }

        private void mnu_desc_Click(object sender, EventArgs e) => SetOrder(ascending: false);

        private void mnu_asc_Click(object sender, EventArgs e) => SetOrder(ascending: true);

        private void SetOrder(bool ascending)
        {
            _ascending = ascending;
            mnu_asc.Checked = ascending;
            mnu_desc.Checked = !ascending;
            RebuildList();
        }

        /// <summary> 暂停期间信息照常记录、计数，恢复后一次补上 </summary>
        private void mnu_pause_Click(object sender, EventArgs e)
        {
            if (!mnu_pause.Checked) RebuildList();
        }

        /// <summary> 打开日志文件目录 </summary>
        private void mnu_history_Click(object sender, EventArgs e)
        {
            try
            {
                Directory.CreateDirectory(Program.LogDir);
                Process.Start(Program.LogDir);
            }
            catch (Exception ex)
            {
                Log.Warn(nameof(InfoForm), "打开日志目录失败.", ex);
                Error(ex.Message);
            }
        }

        private void lst_log_Resize(object sender, EventArgs e)
        {
            col_message.Width = Math.Max(100, lst_log.ClientSize.Width - col_time.Width);
        }
    }
}
