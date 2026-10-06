using System.IO;

namespace DotNet.HalconAlgo
{
    /// <summary>
    /// 算法侧的路径约定. 只有 <see cref="ProjectDir"/> 是可配置的根,
    /// 其余派生路径改为只读属性: 静态字段初始化只跑一次, 之前改 ProjectDir
    /// 不会带动 SchemeDir/JobDir/System 更新, 会静默指向旧目录.
    /// </summary>
    public static class AlgoPaths
    {
        /// <summary>配置根目录. 唯一允许外部改写的路径.</summary>
        public static string ProjectDir = "Config";

        public static string SchemeDir => Path.Combine(ProjectDir, "Scheme");
        public static string JobDir => Path.Combine(SchemeDir, "Job");
        public static string System => Path.Combine(ProjectDir, "System.json");

        public static string SchemeInfo => "SchemeInfo.json";
        public static string JobInfo => "JobInfo.json";
    }
}
