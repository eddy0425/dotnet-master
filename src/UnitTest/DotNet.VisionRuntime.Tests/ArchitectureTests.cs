using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionRuntime.Tests
{
    /// <summary>
    /// 架构守卫：Runtime 是无界面的宿主内核 —— 不引用 WinForms，也不认识任何具体算法。
    /// </summary>
    [TestClass]
    public class ArchitectureTests
    {
        private static readonly Assembly Runtime = typeof(AlgoCatalog).Assembly;

        [TestMethod]
        public void Runtime_ReferencesOnlySdkHalconAndJson()
        {
            var allowed = new[]
            {
                "mscorlib", "System", "System.Core", "Microsoft.CSharp",
                "DotNet.Drawing", "DotNet.HalconCore", "halcondotnet", "Newtonsoft.Json",
            };
            var actual = Runtime.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
            CollectionAssert.DoesNotContain(actual, "System.Windows.Forms");
            CollectionAssert.DoesNotContain(actual, "DotNet.HalconAlgo");
        }

        /// <summary> 外置插件只看到 SDK 这一层：Core、Drawing、HALCON（可选 HalconKit、Newtonsoft），看不到 Runtime 与界面 </summary>
        [TestMethod]
        public void ExternalPlugin_ReferencesOnlySdk()
        {
            var allowed = new[]
            {
                "mscorlib", "System", "System.Core",
                "DotNet.Drawing", "DotNet.HalconCore", "DotNet.HalconKit", "halcondotnet", "Newtonsoft.Json",
            };
            var actual = AssemblyNameOf(AlgoCatalogTests.SamplePluginDll());

            CollectionAssert.IsSubsetOf(actual, allowed, "实际引用: " + string.Join(", ", actual));
        }

        private static string[] AssemblyNameOf(string path)
            => Assembly.LoadFrom(path).GetReferencedAssemblies().Select(a => a.Name).ToArray();
    }
}
