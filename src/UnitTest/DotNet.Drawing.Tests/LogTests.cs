using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class LogTests
    {
        private sealed class CapturingLogger : ILogger
        {
            public readonly List<Tuple<LogLevel, string, string, Exception>> Entries = new List<Tuple<LogLevel, string, string, Exception>>();

            public void Log(LogLevel level, string category, string message, Exception exception)
                => Entries.Add(Tuple.Create(level, category, message, exception));
        }

        private sealed class ThrowingLogger : ILogger
        {
            public void Log(LogLevel level, string category, string message, Exception exception)
                => throw new InvalidOperationException("logger broken");
        }

        private ILogger _saved;

        [TestInitialize]
        public void SaveLogger() => _saved = Log.Current;

        [TestCleanup]
        public void RestoreLogger() => Log.Current = _saved;

        [TestMethod]
        public void Default_IsTraceLogger()
        {
            Assert.IsInstanceOfType(_saved, typeof(TraceLogger));
        }

        [TestMethod]
        public void Current_Null_FallsBackToNullLogger()
        {
            Log.Current = null;
            Assert.IsInstanceOfType(Log.Current, typeof(NullLogger));
            Log.Warn("c", "m");
        }

        [TestMethod]
        public void Methods_ForwardLevelCategoryMessageAndException()
        {
            var logger = new CapturingLogger();
            Log.Current = logger;
            var ex = new Exception("boom");

            Log.Debug("cat", "d");
            Log.Info("cat", "i");
            Log.Warn("cat", "w", ex);
            Log.Error("cat", "e");

            CollectionAssert.AreEqual(
                new[] { LogLevel.Debug, LogLevel.Info, LogLevel.Warn, LogLevel.Error },
                logger.Entries.ConvertAll(e => e.Item1));
            Assert.AreEqual("cat", logger.Entries[2].Item2);
            Assert.AreEqual("w", logger.Entries[2].Item3);
            Assert.AreSame(ex, logger.Entries[2].Item4);
            Assert.IsNull(logger.Entries[3].Item4);
        }

        [TestMethod]
        public void LoggerFailure_IsSwallowed()
        {
            Log.Current = new ThrowingLogger();
            Log.Error("cat", "must not throw", new Exception());
        }

        [TestMethod]
        public void TraceLogger_AcceptsNullCategoryAndException()
        {
            new TraceLogger().Log(LogLevel.Info, null, "message", null);
            new TraceLogger().Log(LogLevel.Error, "cat", "message", new Exception("x"));
        }
    }
}
