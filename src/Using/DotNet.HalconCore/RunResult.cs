using System;

namespace DotNet.HalconCore
{
    public enum RunStatus
    {
        /// <summary> 正常完成 </summary>
        Ok,

        /// <summary> 有结果，但结果是降级的（例如部分来源无效） </summary>
        Warning,

        /// <summary> 没有结果；所有输出已复位为默认值 </summary>
        Error,
    }

    /// <summary>
    /// 一次执行的状态：取代原来的 <c>bool</c> 返回值 + 各策略自己在屏幕上画红字。
    /// </summary>
    /// <remarks>
    /// 失败原因由此返回给宿主 / 流程引擎，可以记日志、汇总、在工具树上标红；
    /// 屏幕上的状态文本由基类按统一规则画出，策略不再各画各的。
    /// </remarks>
    public sealed class RunResult
    {
        private RunResult(RunStatus status, string message)
        {
            Status = status;
            Message = message;
        }

        public RunStatus Status { get; }

        /// <summary> 给人看的一行说明；成功时通常是结果摘要，可为 null </summary>
        public string Message { get; }

        /// <summary> 执行耗时（只含 Execute，由基类计时） </summary>
        public TimeSpan Elapsed { get; internal set; }

        public bool IsOk => Status == RunStatus.Ok;

        public static RunResult Ok(string message = null) => new RunResult(RunStatus.Ok, message);

        public static RunResult Warn(string message) => new RunResult(RunStatus.Warning, message);

        public static RunResult Fail(string message) => new RunResult(RunStatus.Error, message);

        public override string ToString() => Message == null ? Status.ToString() : $"{Status}: {Message}";
    }
}
