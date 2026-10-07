using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.HalconUI.Tests
{
    /// <summary>架构守卫：显示控件只认契约，不认识任何具体算法（内置算法与外置插件一视同仁）。</summary>
    [TestClass]
    public class ArchitectureTests
    {
        [TestMethod]
        public void HalconUI_DoesNotReferenceHalconAlgo()
        {
            var actual = typeof(HDisplayUI).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            CollectionAssert.DoesNotContain(actual, "DotNet.HalconAlgo", "实际引用: " + string.Join(", ", actual));
        }
    }
}
