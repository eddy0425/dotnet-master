using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DotNet.HalconCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>
    /// <see cref="ParamPanel"/>：按参数声明生成控件并双向绑定；只有值真的变了才写回并通知。
    /// </summary>
    [TestClass]
    public class ParamPanelTests
    {
        private enum Mode { A, B }

        /// <summary> 被绑定的"参数类" </summary>
        private sealed class Para
        {
            public bool Flag = true;
            public Mode Mode = Mode.A;
            public int Count = 5;
            public double Score = 0.5;
            public SourceRef Source = SourceRef.Local;
            public string Folder = @"C:\img";
        }

        private static List<ParamItem> Declare(Para para)
        {
            var p = new ParamBuilder();
            p.Flag("开关", () => para.Flag, v => para.Flag = v)
             .Choice("模式", () => para.Mode, v => para.Mode = v, Option.Of(Mode.A, "甲"), Option.Of(Mode.B, "乙"))
             .Int("数量", () => para.Count, v => para.Count = v, presets: new[] { 1, 5 }, min: 0, max: 10)
             .Double("得分", () => para.Score, v => para.Score = v, presets: new[] { 0.5, 0.7 })
             .When(() => para.Mode == Mode.B)
             .Source("来源", () => para.Source, v => para.Source = v, OutEnum.Region)
             .Folder("目录", () => para.Folder, v => para.Folder = v);
            return p.Items.ToList();
        }

        private static void Run(Action<ParamPanel, Para, List<ParamItem>, List<ParamItem>> body)
        {
            Sta.Run(() =>
            {
                var para = new Para();
                var items = Declare(para);
                var committed = new List<ParamItem>();
                using (var panel = new ParamPanel { Dock = DockStyle.Fill })
                using (WindowHost.ShowOffscreen(panel, 500, 400))
                {
                    panel.Committed += (s, e) => committed.AddRange(e.Changed);
                    panel.Bind(items);
                    body(panel, para, items, committed);
                }
            });
        }

        private static ParamItem Item(List<ParamItem> items, string label) => items.Single(i => i.Label == label);

        [TestMethod]
        public void Bind_ShowsCurrentValues()
        {
            Run((panel, para, items, committed) =>
            {
                Assert.IsTrue(((CheckBox)panel.EditorOf(Item(items, "开关"))).Checked);
                Assert.AreEqual("甲", panel.EditorOf(Item(items, "模式")).Text);
                Assert.AreEqual("5", panel.EditorOf(Item(items, "数量")).Text);
                Assert.AreEqual("默认", panel.EditorOf(Item(items, "来源")).Text);
                Assert.AreEqual(@"C:\img", panel.EditorOf(Item(items, "目录")).Text);
                Assert.IsNotNull(panel.ButtonOf(Item(items, "来源")), "来源项带选择按钮");
                Assert.IsNull(panel.ButtonOf(Item(items, "数量")));
                Assert.AreEqual(0, committed.Count, "绑定本身不写回");
            });
        }

        [TestMethod]
        public void Flag_Toggle_WritesBackAndNotifies()
        {
            Run((panel, para, items, committed) =>
            {
                ((CheckBox)panel.EditorOf(Item(items, "开关"))).Checked = false;

                Assert.IsFalse(para.Flag);
                CollectionAssert.AreEqual(new[] { Item(items, "开关") }, committed);
            });
        }

        [TestMethod]
        public void Choice_Select_WritesOptionValueNotText()
        {
            Run((panel, para, items, committed) =>
            {
                ((ComboBox)panel.EditorOf(Item(items, "模式"))).SelectedIndex = 1;

                Assert.AreEqual(Mode.B, para.Mode);
                Assert.AreEqual(1, committed.Count);
            });
        }

        [TestMethod]
        public void Number_ValidText_WritesBack()
        {
            Run((panel, para, items, committed) =>
            {
                var item = Item(items, "数量");
                panel.EditorOf(item).Text = "8";

                Assert.IsTrue(panel.CommitText(item));
                Assert.AreEqual(8, para.Count);
                Assert.IsNull(panel.ErrorOf(item));
                Assert.AreEqual(1, committed.Count);
            });
        }

        [TestMethod]
        public void Number_InvalidOrOutOfRange_ShowsErrorAndKeepsValue()
        {
            Run((panel, para, items, committed) =>
            {
                var item = Item(items, "数量");

                panel.EditorOf(item).Text = "abc";
                Assert.IsFalse(panel.CommitText(item));
                StringAssert.Contains(panel.ErrorOf(item), "不是整数");

                panel.EditorOf(item).Text = "11";
                Assert.IsFalse(panel.CommitText(item));
                StringAssert.Contains(panel.ErrorOf(item), "不能大于");

                Assert.AreEqual(5, para.Count, "校验失败的值不得写回");
                Assert.AreEqual(0, committed.Count);

                panel.EditorOf(item).Text = "3";
                Assert.IsTrue(panel.CommitText(item));
                Assert.IsNull(panel.ErrorOf(item), "改对之后错误清除");
            });
        }

        /// <summary> 值没变（只是写法不同）时不调用 setter、不通知 —— 策略不必自己比较新旧值 </summary>
        [TestMethod]
        public void Number_SameValue_DoesNotNotify_AndNormalizesText()
        {
            Run((panel, para, items, committed) =>
            {
                var item = Item(items, "数量");
                panel.EditorOf(item).Text = "05";

                Assert.IsTrue(panel.CommitText(item));
                Assert.AreEqual(0, committed.Count);
                Assert.AreEqual("5", panel.EditorOf(item).Text);
            });
        }

        [TestMethod]
        public void When_RowVisibilityFollowsOtherParams()
        {
            Run((panel, para, items, committed) =>
            {
                var score = Item(items, "得分");
                Assert.IsFalse(panel.IsRowVisible(score), "模式为甲时隐藏");

                ((ComboBox)panel.EditorOf(Item(items, "模式"))).SelectedIndex = 1;
                Assert.IsTrue(panel.IsRowVisible(score), "任一参数写回后重新求值");
            });
        }

        [TestMethod]
        public void Source_Pick_WritesBackAndShowsFormattedName()
        {
            Run((panel, para, items, committed) =>
            {
                var picked = new SourceRef(Guid.NewGuid(), "区域");
                SourceParam asked = null;
                panel.SourcePicker = p => { asked = p; return picked; };
                panel.SourceFormatter = s => s.IsLocal ? "默认" : "上游/" + s.Output;

                panel.ButtonOf(Item(items, "来源")).PerformClick();

                Assert.AreEqual(OutEnum.Region, asked.SourceType, "选择器拿到来源的类型, 据此过滤");
                Assert.AreEqual(picked, para.Source);
                Assert.AreEqual("上游/区域", panel.EditorOf(Item(items, "来源")).Text);
                Assert.AreEqual(1, committed.Count);
            });
        }

        [TestMethod]
        public void Source_PickCancelled_NoChange()
        {
            Run((panel, para, items, committed) =>
            {
                panel.SourcePicker = p => null;
                panel.ButtonOf(Item(items, "来源")).PerformClick();

                Assert.AreEqual(SourceRef.Local, para.Source);
                Assert.AreEqual(0, committed.Count);
            });
        }

        [TestMethod]
        public void Folder_Pick_WritesBack()
        {
            Run((panel, para, items, committed) =>
            {
                panel.FolderPicker = current => current + @"\sub";
                panel.ButtonOf(Item(items, "目录")).PerformClick();

                Assert.AreEqual(@"C:\img\sub", para.Folder);
                Assert.AreEqual(1, committed.Count);
            });
        }

        [TestMethod]
        public void Folder_Open_ExistingDirectory_CallsOpener()
        {
            Run((panel, para, items, committed) =>
            {
                var folder = Item(items, "目录");
                string opened = null;
                panel.FolderOpener = path => opened = path;
                panel.EditorOf(folder).Text = System.IO.Path.GetTempPath();
                panel.OpenButtonOf(folder).PerformClick();

                Assert.AreEqual(System.IO.Path.GetTempPath(), opened);
                Assert.IsNull(panel.ErrorOf(folder));
                Assert.IsNull(panel.OpenButtonOf(Item(items, "来源")), "只有文件夹项有打开按钮");
            });
        }

        [TestMethod]
        public void Folder_Open_MissingDirectory_ShowsError()
        {
            Run((panel, para, items, committed) =>
            {
                var folder = Item(items, "目录");
                bool opened = false;
                panel.FolderOpener = path => opened = true;
                panel.EditorOf(folder).Text = @"Z:\不存在的目录\" + Guid.NewGuid();
                panel.OpenButtonOf(folder).PerformClick();

                Assert.IsFalse(opened);
                Assert.IsNotNull(panel.ErrorOf(folder));
            });
        }

        [TestMethod]
        public void Folder_Open_OpenerThrows_ShowsError()
        {
            Run((panel, para, items, committed) =>
            {
                var folder = Item(items, "目录");
                panel.FolderOpener = path => throw new System.ComponentModel.Win32Exception(5);
                panel.EditorOf(folder).Text = System.IO.Path.GetTempPath();
                panel.OpenButtonOf(folder).PerformClick();

                Assert.IsNotNull(panel.ErrorOf(folder), "打开失败显示在控件旁, 不抛出");
            });
        }

        [TestMethod]
        public void Folder_Ungrouped_IsWideRowOnTop()
        {
            Run((panel, para, items, committed) =>
            {
                var folder = panel.EditorOf(Item(items, "目录"));
                var count = panel.EditorOf(Item(items, "数量"));

                Assert.IsTrue(folder.Bottom <= count.Top, "不分组的文件夹排在槽位上面");
                Assert.IsTrue(folder.Width > count.Width, "整行编辑框比槽位宽");
                Assert.IsTrue(panel.OpenButtonOf(Item(items, "目录")).Left > panel.ButtonOf(Item(items, "目录")).Right);
            });
        }

        [TestMethod]
        public void Bind_Again_ReplacesRows()
        {
            Run((panel, para, items, committed) =>
            {
                var other = new ParamBuilder().Flag("另一个", () => false, v => { }).Items;
                panel.Bind(other);

                CollectionAssert.AreEqual(other.ToList(), panel.Items.ToList());
                Assert.IsNull(panel.EditorOf(Item(items, "开关")));

                panel.Bind(null);
                Assert.AreEqual(0, panel.Items.Count);
            });
        }

        [TestMethod]
        public void Bind_Again_ReleasesOldEditorsFromErrorProvider()
        {
            Run((panel, para, items, committed) =>
            {
                // ErrorProvider 内部按控件登记, 控件释放后也不移除: 反复换绑不能让登记数一直涨
                // items 是 .NET Framework 里 ErrorProvider 的内部字段, 换运行时要跟着改
                var errorsField = typeof(ParamPanel).GetField("_errors", BindingFlags.Instance | BindingFlags.NonPublic);
                var itemsField = typeof(ErrorProvider).GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(errorsField, "ParamPanel._errors 字段不存在");
                Assert.IsNotNull(itemsField, "ErrorProvider.items 是框架内部字段, 当前运行时没有");
                var registered = (ICollection)itemsField.GetValue((ErrorProvider)errorsField.GetValue(panel));

                panel.Bind(Declare(para));
                int once = registered.Count;
                Assert.IsTrue(once > 0, "基线没有登记任何控件, 测试失去意义");
                for (int i = 0; i < 5; i++) panel.Bind(Declare(para));

                Assert.AreEqual(once, registered.Count);
                panel.Bind(null);
                Assert.AreEqual(0, registered.Count);
            });
        }

        [TestMethod]
        public void Group_SameTitleSharesOneGroupBox_TabEndsGroup()
        {
            Run((panel, para, items, committed) =>
            {
                bool a = true, b = false;
                int size = 15;
                var grouped = new ParamBuilder()
                    .Page(Pages.Display)
                    .Group("显示设置").Flag("甲", () => a, v => a = v)
                    .Group("字体设置").Int("字号", () => size, v => size = v)
                    .Group("显示设置").Flag("乙", () => b, v => b = v)
                    .Page(Pages.Parameter).Flag("不分组", () => a, v => a = v)
                    .Items.ToList();
                panel.Bind(grouped);

                var first = panel.EditorOf(grouped[0]).Parent as GroupBox;
                Assert.IsNotNull(first, "分组项画进分组框");
                Assert.AreEqual("显示设置", first.Text);
                Assert.AreSame(first, panel.EditorOf(grouped[2]).Parent, "同名分组合并");
                Assert.AreEqual("字体设置", ((GroupBox)panel.EditorOf(grouped[1]).Parent).Text);
                Assert.IsNull(grouped[3].Group, "换页结束分组");
                Assert.AreSame(panel, panel.EditorOf(grouped[3]).Parent);
            });
        }

        [TestMethod]
        public void Group_NullOrBlankTitleEndsGroup()
        {
            bool a = true;
            var items = new ParamBuilder()
                .Group("显示设置").Flag("甲", () => a, v => a = v)
                .Group(null).Flag("乙", () => a, v => a = v)
                .Group("显示设置").Flag("丙", () => a, v => a = v)
                .Group("  ").Flag("丁", () => a, v => a = v)
                .Items;

            Assert.AreEqual("显示设置", items[0].Group);
            Assert.IsNull(items[1].Group);
            Assert.AreEqual("显示设置", items[2].Group);
            Assert.IsNull(items[3].Group);
        }

        [TestMethod]
        public void DockTop_HeightFollowsContent()
        {
            Run((panel, para, items, committed) =>
            {
                int one = 0, two = 0;
                var single = new ParamBuilder().Int("一", () => one, v => one = v).Items.ToList();
                var twoRows = new ParamBuilder().Int("一", () => one, v => one = v).Int("二", () => two, v => two = v).Items.ToList();
                panel.Dock = DockStyle.Top;

                panel.Bind(single);
                int singleHeight = panel.Height;
                panel.Bind(twoRows);

                Assert.IsTrue(panel.Height > singleHeight, "多一行面板跟着变高");
                Assert.IsTrue(panel.EditorOf(twoRows[1]).Bottom <= panel.ClientSize.Height, "末行完整可见");
            });
        }

        #region 文本 / 文件 / 按钮 / 来源列表

        private sealed class More
        {
            public string Text = "abc";
            public string File = "";
            public int Pressed;
            public List<SourceRef> Sources = new List<SourceRef>();
        }

        private static void RunMore(Action<ParamPanel, More, List<ParamItem>, List<ParamItem>> body, int maxCount = 0)
        {
            Sta.Run(() =>
            {
                var more = new More();
                var items = new ParamBuilder()
                    .Text("条码", () => more.Text, v => more.Text = v)
                    .File("模型", () => more.File, v => more.File = v, "模型|*.shm")
                    .Action("重新示教", () => more.Pressed++)
                    .SourceList("输入区域", () => more.Sources, v => more.Sources = v.ToList(), OutEnum.Region, maxCount)
                    .Items.ToList();
                var committed = new List<ParamItem>();
                using (var panel = new ParamPanel { Dock = DockStyle.Fill })
                using (WindowHost.ShowOffscreen(panel, 600, 400))
                {
                    panel.Committed += (s, e) => committed.AddRange(e.Changed);
                    panel.Bind(items);
                    body(panel, more, items, committed);
                }
            });
        }

        [TestMethod]
        public void Text_CommitsRawText()
        {
            RunMore((panel, more, items, committed) =>
            {
                var item = Item(items, "条码");
                Assert.AreEqual("abc", panel.EditorOf(item).Text);
                panel.EditorOf(item).Text = " X1 ";

                Assert.IsTrue(panel.CommitText(item));

                Assert.AreEqual(" X1 ", more.Text);
                Assert.AreEqual(1, committed.Count);
            });
        }

        [TestMethod]
        public void File_PickerGetsFilter_WritesBack()
        {
            RunMore((panel, more, items, committed) =>
            {
                var item = Item(items, "模型");
                string seenFilter = null;
                panel.FilePicker = (filter, current) => { seenFilter = filter; return @"D:\m\a.shm"; };

                panel.ButtonOf(item).PerformClick();

                Assert.AreEqual("模型|*.shm", seenFilter);
                Assert.AreEqual(@"D:\m\a.shm", more.File);
                Assert.AreEqual(@"D:\m\a.shm", panel.EditorOf(item).Text);
            });
        }

        [TestMethod]
        public void Action_ButtonRunsActionAndNotifies()
        {
            RunMore((panel, more, items, committed) =>
            {
                var item = Item(items, "重新示教");
                var button = (Button)panel.EditorOf(item);
                Assert.AreEqual("重新示教", button.Text, "按钮文字就是标签");

                button.PerformClick();
                button.PerformClick();

                Assert.AreEqual(2, more.Pressed);
                Assert.AreEqual(2, committed.Count(i => i == item));
            });
        }

        [TestMethod]
        public void SourceList_AddAndRemove()
        {
            RunMore((panel, more, items, committed) =>
            {
                var item = Item(items, "输入区域");
                var a = new SourceRef(Guid.NewGuid(), "区域");
                var b = new SourceRef(Guid.NewGuid(), "区域");
                var picks = new Queue<SourceRef?>(new SourceRef?[] { a, b, null });
                panel.SourceListPicker = p => picks.Dequeue();
                panel.SourceFormatter = s => s.IsLocal ? "默认" : s.ToolId == a.ToolId ? "A/区域" : "B/区域";
                var combo = (ComboBox)panel.EditorOf(item);

                panel.ButtonOf(item).PerformClick();
                panel.ButtonOf(item).PerformClick();
                panel.ButtonOf(item).PerformClick();   // 取消

                CollectionAssert.AreEqual(new[] { a, b }, more.Sources);
                CollectionAssert.AreEqual(new[] { "A/区域", "B/区域" }, combo.Items.Cast<string>().ToArray());

                combo.SelectedIndex = 0;
                panel.OpenButtonOf(item).PerformClick();

                CollectionAssert.AreEqual(new[] { b }, more.Sources);
                Assert.AreEqual(3, committed.Count);
            });
        }

        [TestMethod]
        public void SourceList_MaxCount_RefusesMore()
        {
            RunMore((panel, more, items, committed) =>
            {
                var item = Item(items, "输入区域");
                panel.SourceListPicker = p => new SourceRef(Guid.NewGuid(), "区域");

                panel.ButtonOf(item).PerformClick();
                panel.ButtonOf(item).PerformClick();

                Assert.AreEqual(1, more.Sources.Count);
                StringAssert.Contains(panel.ErrorOf(item), "最多 1 项");
            }, maxCount: 1);
        }

        #endregion

        #region 宿主写回

        /// <summary> 宿主排队执行写回（连续运行中排到两帧之间）：执行完之前显示为待生效，执行完才通知 </summary>
        [TestMethod]
        public void ValueWriter_Queued_ShowsPendingUntilDone()
        {
            Run((panel, para, items, committed) =>
            {
                var item = Item(items, "数量");
                var editor = panel.EditorOf(item);
                var normal = editor.BackColor;
                var queued = new System.Threading.Tasks.TaskCompletionSource<bool>();
                Func<bool> write = null;
                panel.ValueWriter = (i, v) =>
                {
                    write = () => i.TrySetValue(v);
                    return queued.Task;
                };
                editor.Text = "8";

                Assert.IsTrue(panel.CommitText(item));

                Assert.AreEqual(5, para.Count, "还没执行");
                Assert.IsTrue(panel.IsPending(item));
                Assert.AreEqual(ParamPanel.PendingBackColor, editor.BackColor, "待生效");
                Assert.AreEqual(0, committed.Count);

                queued.SetResult(write());
                WindowHost.PumpUntil(() => !panel.IsPending(item));

                Assert.AreEqual(8, para.Count);
                Assert.AreEqual(normal, editor.BackColor);
                Assert.AreEqual(1, committed.Count, "执行完、值真的变了才通知");
                Assert.AreEqual("8", editor.Text);
            });
        }

        [TestMethod]
        public void ValueWriter_CompletedSynchronously_BehavesLikeDirectWrite()
        {
            Run((panel, para, items, committed) =>
            {
                panel.ValueWriter = (i, v) => System.Threading.Tasks.Task.FromResult(i.TrySetValue(v));
                var check = (CheckBox)panel.EditorOf(Item(items, "开关"));

                check.Checked = false;

                Assert.IsFalse(para.Flag);
                Assert.IsFalse(panel.IsPending(Item(items, "开关")));
                Assert.AreEqual(1, committed.Count, "同步完成时与就地写回时序相同");
            });
        }

        [TestMethod]
        public void ValueWriter_Fails_ShowsErrorWithoutNotifying()
        {
            Run((panel, para, items, committed) =>
            {
                var item = Item(items, "数量");
                var queued = new System.Threading.Tasks.TaskCompletionSource<bool>();
                panel.ValueWriter = (i, v) => queued.Task;
                panel.EditorOf(item).Text = "8";
                panel.CommitText(item);

                queued.SetException(new InvalidOperationException("会话已关闭"));
                WindowHost.PumpUntil(() => !panel.IsPending(item));

                Assert.AreEqual("会话已关闭", panel.ErrorOf(item));
                Assert.AreEqual(0, committed.Count);
            });
        }

        #endregion
    }
}
