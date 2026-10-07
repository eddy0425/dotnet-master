using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotNet.Drawing.Tests
{
    [TestClass]
    public class StringExtensionTests
    {
        [DataTestMethod]
        [DataRow("线宽3", 3)]
        [DataRow("a1b2c3", 123)]
        [DataRow("Ｗ１２", 12)]     // 全角
        [DataRow("⑫", 12)]         // 数值 > 9 不再被截成 1
        [DataRow("十", 0)]          // 汉字数词是 Lo 类，不算数字
        [DataRow("abc", 0)]
        [DataRow("", 0)]
        [DataRow("99999999999", 0)] // 超出 int 范围
        public void ExtractNumber(string input, int expected)
        {
            Assert.AreEqual(expected, input.ExtractNumber());
        }

        [TestMethod]
        public void ExtractNumber_Null_ReturnsZero()
        {
            Assert.AreEqual(0, ((string)null).ExtractNumber());
            Assert.AreEqual(string.Empty, ((string)null).ExtractNumberAsString());
        }

        [DataTestMethod]
        [DataRow("②", "2")]
        [DataRow("⑩①", "101")]     // 逐字符拼接，不做数词进位解析
        [DataRow("x½y", "12")]      // NFKC 先把 ½ 分解成 1⁄2
        [DataRow("R0.5", "05")]
        public void ExtractNumberAsString(string input, string expected)
        {
            Assert.AreEqual(expected, input.ExtractNumberAsString());
        }
    }
}
