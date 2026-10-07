using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyTitle("DotNet.HalconAlgo.Tests")]
[assembly: AssemblyProduct("DotNet.HalconAlgo.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: ComVisible(false)]
[assembly: Guid("cdbb7a88-aa92-41b6-96b7-caa986ba06b4")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// 策略共享静态状态（AlgoPaths.ProjectDir、Log.Current、HALCON 系统参数），不并行执行。
[assembly: DoNotParallelize]
