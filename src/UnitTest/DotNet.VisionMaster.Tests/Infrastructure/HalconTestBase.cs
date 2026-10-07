using System;
using HalconDotNet;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.VisionMaster.Tests
{
    /// <summary>
    /// 依赖 HALCON 原生运行时（halcon.dll + 许可）的测试。
    /// </summary>
    /// <remarks>运行时不可用时整组标记为 Inconclusive，而不是失败。</remarks>
    public abstract class HalconTestBase
    {
        private static readonly Lazy<string> RuntimeError = new Lazy<string>(() =>
        {
            try
            {
                HOperatorSet.GenEmptyObj(out HObject probe);
                probe.Dispose();
                return null;
            }
            catch (Exception ex)
            {
                return ex.GetType().Name + ": " + ex.Message;
            }
        });

        [TestInitialize]
        public void RequireHalcon()
        {
            if (RuntimeError.Value != null)
                Assert.Inconclusive("HALCON 运行时不可用：" + RuntimeError.Value);
        }
    }
}
