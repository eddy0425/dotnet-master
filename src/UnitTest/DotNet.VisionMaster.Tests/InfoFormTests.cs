using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="InfoForm"/>：按级别计数与筛选、报警历史、容量淘汰、暂停刷新、跨线程写入。
    /// </summary>
    [TestClass]
    public class InfoFormTests
    {
        private static void Run(Action<InfoForm> body) =>
            Sta.Run(() =>
            {
                var info = new InfoForm();
                using (WindowHost.ShowOffscreen(info))
                {
                    body(info);
                }
            });

        private static ToolStripButton Button(InfoForm info, string name) => Priv.Get<ToolStripButton>(info, name);

        /// <summary> 模拟点击筛选按钮：CheckOnClick 先翻转选中状态，再触发 Click </summary>
        private static void ClickFilter(InfoForm info, string name)
        {
            var button = Button(info, name);
            button.Checked = !button.Checked;
            Priv.Click(info, "tsb_filter_Click", button);
        }

        private static void WriteOneOfEach(InfoForm info)
        {
            info.Info("提示");
            info.Warn("警告");
            info.Error("错误");
        }

        [TestMethod]
        public void Write_CountsByLevel()
        {
            Run(info =>
            {
                WriteOneOfEach(info);
                info.Info("提示2");
                Assert.AreEqual(4, info.VisibleCount);
                Assert.AreEqual("提示(2)", Button(info, "tsb_tip").Text);
                Assert.AreEqual("警告(1)", Button(info, "tsb_warn").Text);
                Assert.AreEqual("错误(1)", Button(info, "tsb_error").Text);
                Assert.AreEqual("报警(1)", Button(info, "tsb_alarm").Text);
            });
        }

        /// <summary> 筛选按钮互斥；再点一次取消筛选 </summary>
        [TestMethod]
        public void Filter_ShowsOnlyMatchingLevel()
        {
            Run(info =>
            {
                WriteOneOfEach(info);
                ClickFilter(info, "tsb_warn");
                Assert.AreEqual(1, info.VisibleCount);

                info.Warn("警告2");
                info.Info("提示2");
                Assert.AreEqual(2, info.VisibleCount, "筛选期间新来的信息也要按级别过滤");

                ClickFilter(info, "tsb_tip");
                Assert.IsFalse(Button(info, "tsb_warn").Checked);
                Assert.AreEqual(2, info.VisibleCount);

                ClickFilter(info, "tsb_tip");
                Assert.AreEqual(5, info.VisibleCount);
            });
        }

        /// <summary> 清除信息不影响报警历史；查看报警时清除只清报警 </summary>
        [TestMethod]
        public void Clear_KeepsAlarmHistory()
        {
            Run(info =>
            {
                WriteOneOfEach(info);
                Priv.Click(info, "mnu_clear_Click");
                Assert.AreEqual(0, info.VisibleCount);
                Assert.AreEqual("错误(0)", Button(info, "tsb_error").Text);
                Assert.AreEqual("报警(1)", Button(info, "tsb_alarm").Text);

                ClickFilter(info, "tsb_alarm");
                Assert.AreEqual(1, info.VisibleCount);
                Priv.Click(info, "mnu_clear_Click");
                Assert.AreEqual("报警(0)", Button(info, "tsb_alarm").Text);
            });
        }

        [TestMethod]
        public void Write_BeyondCapacity_DropsOldest()
        {
            Run(info =>
            {
                for (int i = 0; i < InfoForm.Capacity + 1; i++) info.Info($"第 {i} 条");
                Assert.IsTrue(info.VisibleCount <= InfoForm.Capacity);
                Assert.AreEqual($"提示({info.VisibleCount})", Button(info, "tsb_tip").Text, "计数要与保留的条目一致");
                var list = Priv.Get<ListView>(info, "lst_log");
                Assert.AreEqual($"第 {InfoForm.Capacity} 条", list.Items[0].SubItems[1].Text, "默认降序：最新的在最上面");
            });
        }

        /// <summary> 暂停期间照常计数，恢复后补上 </summary>
        [TestMethod]
        public void Pause_DefersListUntilResumed()
        {
            Run(info =>
            {
                var pause = Priv.Get<ToolStripMenuItem>(info, "mnu_pause");
                pause.Checked = true;
                WriteOneOfEach(info);
                Assert.AreEqual(0, info.VisibleCount);
                Assert.AreEqual("错误(1)", Button(info, "tsb_error").Text);

                pause.Checked = false;
                Priv.Click(info, "mnu_pause_Click");
                Assert.AreEqual(3, info.VisibleCount);
            });
        }

        /// <summary> 后台线程写入：封送到界面线程，不抛跨线程异常 </summary>
        [TestMethod]
        public void Write_FromWorkerThread_MarshalsToUiThread()
        {
            Run(info =>
            {
                var task = Task.Run(() => info.Error("后台错误"));
                WindowHost.PumpUntil(() => task.IsCompleted && info.VisibleCount == 1);
                task.Wait();
                Assert.AreEqual("报警(1)", Button(info, "tsb_alarm").Text);
            });
        }

        [TestMethod]
        public void Write_MultiLine_FlattenedToOneLine()
        {
            Run(info =>
            {
                info.Info("第一行" + Environment.NewLine + "第二行");
                var list = Priv.Get<ListView>(info, "lst_log");
                Assert.AreEqual("第一行    第二行", list.Items[0].SubItems[1].Text);
            });
        }
    }
}
