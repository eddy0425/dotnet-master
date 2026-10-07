using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyTitle("DotNet.VisionRuntime.Tests")]
[assembly: AssemblyProduct("DotNet.VisionRuntime.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: ComVisible(false)]
[assembly: Guid("d82f4b19-3e6a-4c07-b5d1-9a0e7c6f2b53")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// 策略共享静态状态（Log.Current、HALCON 系统参数），不并行执行。
[assembly: DoNotParallelize]
