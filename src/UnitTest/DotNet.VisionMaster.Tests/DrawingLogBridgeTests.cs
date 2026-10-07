using System;
using System.Collections.Generic;
using DotNet.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DrawingLog = DotNet.Drawing;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// <see cref="DrawingLogBridge"/>：库层日志必须进到应用日志里，否则 HalconUI / HalconAlgo 的日志只写 Trace、落不了盘。
    /// </summary>
    [TestClass]
    public class DrawingLogBridgeTests
    {
        private sealed class CapturingSink : ILogSink
        {
            public readonly List<LogEntry> Entries = new List<LogEntry>();
            public void Write(LogEntry entry) { lock (Entries) Entries.Add(entry); }
            public void WriteBatch(IReadOnlyList<LogEntry> entries) { foreach (var e in entries) Write(e); }
            public void Flush() { }
            public void Dispose() { }
        }

        [TestCleanup]
        public void Cleanup() => Log.Logger = null;

        [TestMethod]
        public void Log_ForwardsLevelCategoryMessageAndException()
        {
            var sink = new CapturingSink();
            Log.Initialize(b => b.MinimumLevel(LogLevel.Trace).WriteTo(sink));
            var ex = new InvalidOperationException("boom");

            new DrawingLogBridge().Log(DrawingLog.LogLevel.Warn, "HDisplay", "显示文本失败.", ex);
            Log.Shutdown();   // Flush 只等队列清空、不等在途批次写完; Shutdown 才保证全部落到 sink

            Assert.AreEqual(1, sink.Entries.Count);
            var entry = sink.Entries[0];
            Assert.AreEqual(LogLevel.Warning, entry.Level);
            Assert.AreEqual("HDisplay", entry.Source);
            Assert.AreEqual("显示文本失败.", entry.Message);
            Assert.AreSame(ex, entry.Exception);
        }

        [TestMethod]
        public void Map_CoversEveryLibraryLevel()
        {
            Assert.AreEqual(LogLevel.Debug, DrawingLogBridge.Map(DrawingLog.LogLevel.Debug));
            Assert.AreEqual(LogLevel.Information, DrawingLogBridge.Map(DrawingLog.LogLevel.Info));
            Assert.AreEqual(LogLevel.Warning, DrawingLogBridge.Map(DrawingLog.LogLevel.Warn));
            Assert.AreEqual(LogLevel.Error, DrawingLogBridge.Map(DrawingLog.LogLevel.Error));
        }

        /// <summary>应用日志尚未初始化（或已 Shutdown）时，桥接不得抛异常。</summary>
        [TestMethod]
        public void Log_WithoutAppLogger_DoesNotThrow()
        {
            Log.Logger = null;
            new DrawingLogBridge().Log(DrawingLog.LogLevel.Error, "x", "y", null);
        }
    }
}
