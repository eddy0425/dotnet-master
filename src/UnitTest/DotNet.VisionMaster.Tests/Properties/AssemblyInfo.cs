using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyTitle("DotNet.VisionMaster.Tests")]
[assembly: AssemblyProduct("DotNet.VisionMaster.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: ComVisible(false)]
[assembly: Guid("34fe2984-4ed6-4ba7-bbf2-8c45d254e89f")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// AlgoPaths.UIBlock、绘制会话注册表都是进程级静态状态，不并行执行。
[assembly: DoNotParallelize]
