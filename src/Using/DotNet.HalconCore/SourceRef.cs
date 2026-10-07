using System;
using Newtonsoft.Json;

namespace DotNet.HalconCore
{
    /// <summary>
    /// 对上游工具某个输出的引用：取代原来的 <c>"默认"</c> 与 <c>"工具名/路径"</c> 字符串。
    /// </summary>
    /// <remarks>
    /// 按 <see cref="ToolId"/> 定位工具，而不是按可变的显示名：重名、改名都不会让引用断开。
    /// 界面上显示的 "工具名/路径" 在运行时由宿主拼出，不存储。
    /// <para><see cref="Local"/>（ToolId 为 <see cref="Guid.Empty"/>）表示"用本工具自己的数据"：
    /// 图像取当前图像、区域取本地 ROI、坐标系取恒等（不跟随）。</para>
    /// </remarks>
    public readonly struct SourceRef : IEquatable<SourceRef>
    {
        /// <summary> 本地：不引用任何上游输出（原来的 "默认"） </summary>
        public static readonly SourceRef Local = default(SourceRef);

        [JsonConstructor]
        public SourceRef(Guid toolId, string output)
        {
            ToolId = toolId;
            Output = toolId == Guid.Empty ? null : output;
        }

        /// <summary> 被引用工具的稳定标识；<see cref="Guid.Empty"/> 表示本地 </summary>
        public Guid ToolId { get; }

        /// <summary> 工具内的输出路径，例如 <c>坐标系</c>、<c>直线/起点</c>；本地时为 null </summary>
        public string Output { get; }

        [JsonIgnore]
        public bool IsLocal => ToolId == Guid.Empty;

        public static SourceRef To(IAlgoStrategy tool, string output)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (tool.Id == Guid.Empty) throw new ArgumentException($"工具 '{tool.Name}' 还没有分配 Id", nameof(tool));
            if (string.IsNullOrEmpty(output)) throw new ArgumentNullException(nameof(output));
            return new SourceRef(tool.Id, output);
        }

        public bool Equals(SourceRef other) => ToolId == other.ToolId && string.Equals(Output, other.Output, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SourceRef other && Equals(other);
        public override int GetHashCode() => ToolId.GetHashCode() * 397 ^ (Output == null ? 0 : StringComparer.Ordinal.GetHashCode(Output));
        public static bool operator ==(SourceRef left, SourceRef right) => left.Equals(right);
        public static bool operator !=(SourceRef left, SourceRef right) => !left.Equals(right);

        /// <summary> 调试用；界面文字请用宿主按工具列表拼出的显示名 </summary>
        public override string ToString() => IsLocal ? "默认" : $"{ToolId:N}/{Output}";
    }
}
