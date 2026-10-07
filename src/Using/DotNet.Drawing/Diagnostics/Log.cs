using System;
using System.Diagnostics;

namespace DotNet.Drawing
{
    /// <summary>日志级别.</summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    /// <summary>
    /// 库层日志抽象. 库代码不应该弹 MessageBox, 也不应该直接 Console.WriteLine
    /// (前者在无人值守/非 UI 线程上会卡死流程, 后者在 WinForms 进程里根本没有输出目标),
    /// 统一走这个接口, 由宿主决定落到哪里.
    /// </summary>
    public interface ILogger
    {
        /// <param name="exception">可为 null: 多数 Debug/Info 日志并不伴随异常.</param>
        void Log(LogLevel level, string category, string message, Exception exception);
    }

    /// <summary>
    /// 默认实现: 写入 <see cref="Trace"/>. 附加到调试器 / DebugView 即可看到,
    /// 且不依赖控制台窗口, 不阻塞流程.
    /// </summary>
    public sealed class TraceLogger : ILogger
    {
        public void Log(LogLevel level, string category, string message, Exception exception)
        {
            var text = string.Format("[{0:HH:mm:ss.fff}] [{1}] [{2}] {3}",
                DateTime.Now, level, category ?? "-", message);
            if (exception != null) text += Environment.NewLine + exception;
            Trace.WriteLine(text);
        }
    }

    /// <summary>什么都不做. 供单元测试或明确不需要日志的场合替换. </summary>
    public sealed class NullLogger : ILogger
    {
        public void Log(LogLevel level, string category, string message, Exception exception) { }
    }

    /// <summary>
    /// 全局日志入口. 宿主启动时可通过 <see cref="Current"/> 换成自己的实现 (NLog / log4net / 文件日志等).
    /// 所有方法都保证不抛异常: 日志本身失败绝不能反过来打断业务流程.
    /// </summary>
    public static class Log
    {
        private static ILogger _current = new TraceLogger();

        /// <summary>当前日志实现. 赋 null 等价于 <see cref="NullLogger"/>.</summary>
        public static ILogger Current
        {
            get { return _current; }
            set { _current = value ?? (ILogger)new NullLogger(); }
        }

        public static void Debug(string category, string message) { Write(LogLevel.Debug, category, message, null); }
        public static void Info(string category, string message) { Write(LogLevel.Info, category, message, null); }
        public static void Warn(string category, string message, Exception ex = null) { Write(LogLevel.Warn, category, message, ex); }
        public static void Error(string category, string message, Exception ex = null) { Write(LogLevel.Error, category, message, ex); }

        private static void Write(LogLevel level, string category, string message, Exception ex)
        {
            var logger = _current;
            if (logger == null) return;
            try
            {
                logger.Log(level, category, message, ex);
            }
            catch
            {
                // 日志实现自身异常一律吞掉: 这是唯一允许空 catch 的地方.
            }
        }
    }
}
