using System;
using System.Linq;
using DotNet.Drawing;
using DotNet.HalconUI.Draw;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary><see cref="DrawSafe"/>：清理路径吞异常但必须留下 Debug 日志。</summary>
    [TestClass]
    public class DrawSafeTests : HalconTestBase
    {
        [TestMethod]
        public void Category_IsStable()
        {
            // 现场日志按该分类检索，改名需同步运维文档
            Assert.AreEqual("DrawHelper", DrawSafe.Category);
        }

        [TestMethod]
        public void Dispose_Null_IsNoOp()
        {
            using (var log = new CapturingLogger())
            {
                DrawSafe.Dispose(null);
                Assert.AreEqual(0, log.Entries.Count);
            }
        }

        [TestMethod]
        public void Dispose_ReleasesObject_AndIsSafeToRepeat()
        {
            var obj = Rectangle1(0, 0, 5, 5);

            DrawSafe.Dispose(obj);
            DrawSafe.Dispose(obj);

            Assert.IsFalse(obj.IsInitialized());
        }

        [TestMethod]
        public void WindowOp_RunsAction()
        {
            bool ran = false;
            DrawSafe.WindowOp("op", () => ran = true);
            Assert.IsTrue(ran);
        }

        [TestMethod]
        public void WindowOp_SwallowsException_AndLogsDebug()
        {
            using (var log = new CapturingLogger())
            {
                DrawSafe.WindowOp("关闭 flush", () => throw new InvalidOperationException("boom"));

                var entry = log.Entries.Single();
                Assert.AreEqual(LogLevel.Debug, entry.Level);
                Assert.AreEqual(DrawSafe.Category, entry.Category);
                Assert.AreEqual("关闭 flush 失败(已忽略): boom", entry.Message);
            }
        }
    }
}
