using System;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;


namespace DotNet.Drawing
{
    public static class StringExtension
    {
        #region 数字处理
        private static readonly Regex _digitRegex = new Regex(@"\p{N}", RegexOptions.Compiled);

        /// <summary>
        /// 提取字符串中的所有数字字符并合并为整数（支持全角、带圈数字等 Unicode 数字）
        /// </summary>
        /// <remarks>
        /// 汉字数词（「一」「十」）的 Unicode 类别是 Lo 而不是数字，不会被提取；
        /// 结果超出 <see cref="int"/> 范围时返回 0。
        /// </remarks>
        public static int ExtractNumber(this string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;

            // 复用字符串处理方法
            var numberStr = str.ExtractNumberAsString();

            // 处理超大数字和无效格式
            return int.TryParse(numberStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }

        /// <summary>
        /// 提取字符串中的所有数字字符（支持全角/半角自动转换）
        /// </summary>
        public static string ExtractNumberAsString(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;

            var normalized = str.Normalize(NormalizationForm.FormKC);
            var matches = _digitRegex.Matches(normalized);

            return string.Join("",
                matches.Cast<Match>()
                      .Select(m => ConvertToWesternDigit(m.Value))
            );
        }

        /// <summary>
        /// 将 Unicode 数字转换为半角数字（全角、带圈数字等）
        /// </summary>
        /// <remarks>
        /// 按字符逐个换成其数值后拼接。原实现对数值只取 <c>ToString()</c> 的首字符，
        /// 大于 9 的数字（如「⑫」= 12）被截成 "1"；非整数值和无数值的字符
        /// （<c>GetNumericValue</c> 返回 -1）直接跳过，不再拼出 "0" / "-"。
        /// 注意调用方先做了 NFKC 规范化：「½」此时已被分解为 "1⁄2"，会提取出 "12"。
        /// </remarks>
        private static string ConvertToWesternDigit(string input)
        {
            return input.Aggregate(new StringBuilder(), (sb, c) =>
            {
                if (char.IsNumber(c))
                {
                    var num = char.GetNumericValue(c);
                    if (num >= 0 && num == Math.Floor(num))
                    {
                        sb.Append(((long)num).ToString(CultureInfo.InvariantCulture));
                    }
                }
                return sb;
            }).ToString();
        }
        #endregion

    }
}
