using Microsoft.VisualStudio.TestTools.UnitTesting;
using HashCode = DotNet.Drawing.Internal.HashCode;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class HashCodeTests
    {
        private const int FnvOffsetBasis = unchecked((int)2166136261);

        private static int Accumulate(params object[] values)
        {
            var h = new HashCode();
            foreach (var v in values) h.Add(v);
            return h.ToHashCode();
        }

        [TestMethod]
        public void Empty_ReturnsOffsetBasis()
        {
            Assert.AreEqual(FnvOffsetBasis, new HashCode().ToHashCode());
        }

        [TestMethod]
        public void Combine_MatchesIncrementalAdd()
        {
            // 各元数的 Combine 是手工展开的，逐一对照 Add 序列防止抄漏一行
            Assert.AreEqual(Accumulate(1, 2), HashCode.Combine(1, 2));
            Assert.AreEqual(Accumulate(1, 2, 3), HashCode.Combine(1, 2, 3));
            Assert.AreEqual(Accumulate(1, 2, 3, 4), HashCode.Combine(1, 2, 3, 4));
            Assert.AreEqual(Accumulate(1, 2, 3, 4, 5), HashCode.Combine(1, 2, 3, 4, 5));
            Assert.AreEqual(Accumulate(1, 2, 3, 4, 5, 6), HashCode.Combine(1, 2, 3, 4, 5, 6));
            Assert.AreEqual(Accumulate(1, 2, 3, 4, 5, 6, 7), HashCode.Combine(1, 2, 3, 4, 5, 6, 7));
            Assert.AreEqual(Accumulate(1, 2, 3, 4, 5, 6, 7, 8), HashCode.Combine(1, 2, 3, 4, 5, 6, 7, 8));
            Assert.AreEqual(Accumulate(1, 2, 3, 4, 5, 6, 7, 8, 9), HashCode.Combine(1, 2, 3, 4, 5, 6, 7, 8, 9));
        }

        [TestMethod]
        public void Combine_IsOrderSensitive_AndTreatsNullAsZero()
        {
            Assert.AreNotEqual(HashCode.Combine(1, 2), HashCode.Combine(2, 1));
            Assert.AreEqual(HashCode.Combine(0, "a"), HashCode.Combine((string)null, "a"));
        }
    }
}
