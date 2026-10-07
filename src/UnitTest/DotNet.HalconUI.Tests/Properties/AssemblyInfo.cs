using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: AssemblyTitle("DotNet.HalconUI.Tests")]
[assembly: AssemblyProduct("DotNet.HalconUI.Tests")]
[assembly: AssemblyCopyright("Copyright ©  2026")]
[assembly: ComVisible(false)]
[assembly: Guid("95cd61ca-c6db-4b4e-8988-5093dcc219f6")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// 绘制会话注册表、DrawHelper.Timeout、Log.Current 都是进程级静态状态，不并行执行。
[assembly: DoNotParallelize]
