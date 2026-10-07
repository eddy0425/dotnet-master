using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace DotNet.Library.Extension
{
    /// <summary>
    /// 提供字符串操作的扩展方法
    /// </summary>
    public static class StringExtension
    {
        private static readonly Regex _numberRegex = new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Regex _nonDigitRegex = new Regex(@"[^\d]+", RegexOptions.Compiled);

        #region 空值判断
        /// <summary>
        /// 判断字符串是否为 null 或空字符串
        /// </summary>
        /// <param name="value">要验证的字符串</param>
        /// <returns>当值为 null 或空字符串时返回 true</returns>
        public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

        /// <summary>
        /// 判断字符串是否非 null 且非空字符串
        /// </summary>
        /// <param name="value">要验证的字符串</param>
        /// <returns>当值不为 null 且不为空字符串时返回 true</returns>
        public static bool IsNotNullOrEmpty(this string value) => !string.IsNullOrEmpty(value);

        /// <summary>
        /// 检查字符串数组中是否存在空元素
        /// </summary>
        /// <param name="arrStr">要检查的字符串数组</param>
        /// <returns>当数组包含 null、空字符串或空白字符时返回 true</returns>
        public static bool HasNullOrWhiteSpace(this string[] arrStr)
        {
            if (arrStr == null || arrStr.Length == 0) return true;
            foreach (var str in arrStr)
            {
                if (string.IsNullOrWhiteSpace(str)) return true;
            }
            return false;
        }

        /// <summary>
        /// 检查字符串数组是否全部为非空元素
        /// </summary>
        /// <param name="arrStr">要检查的字符串数组</param>
        /// <returns>当数组非空且所有元素均为非空字符串时返回 true</returns>
        public static bool IsNotNullOrEmpty(this string[] arrStr)
        {
            if (arrStr == null || arrStr.Length == 0) return false;
            foreach (var item in arrStr)
            {
                if (string.IsNullOrEmpty(item)) return false;
            }
            return true;
        }
        #endregion

        #region 字符串处理
        /// <summary>
        /// 使用指定分隔符分割字符串
        /// </summary>
        /// <param name="s">要分割的字符串</param>
        /// <param name="separator">分隔符</param>
        /// <param name="ignoreEmpty">是否移除空元素</param>
        /// <returns>分割后的字符串数组</returns>
        /// <remarks>当分隔符为 null 时会抛出 ArgumentNullException</remarks>
        public static string[] Split(this string s, string separator, bool ignoreEmpty = false)
        {
            if (s == null) return new string[0];
            if (separator == null) throw new ArgumentNullException(nameof(separator));

            var options = ignoreEmpty ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None;
            return s.Split(new[] { separator }, options);
        }

        /// <summary>
        /// 将字符串按分隔符转换为多行格式
        /// </summary>
        /// <param name="str">原始字符串</param>
        /// <param name="separator">分隔符（默认：分号）</param>
        /// <returns>按行分隔的字符串</returns>
        public static string JoinWithNewLine(this string str, string separator = ";")
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return string.Join("\r\n", str.Split(separator, true));
        }

        /// <summary>
        /// 将多行字符串转换为指定分隔符分隔的格式
        /// </summary>
        /// <param name="str">原始字符串</param>
        /// <param name="separator">分隔符（默认：分号）</param>
        /// <returns>单行分隔的字符串</returns>
        public static string JoinWithSeparator(this string str, string separator = ";")
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            var normalized = str.Replace("\r\n", "\n");
            return string.Join(separator, normalized.Split(new[] { "\n", separator }, StringSplitOptions.RemoveEmptyEntries));
        }
        #endregion

        #region 数字处理
        private static readonly Regex _digitRegex = new Regex(@"\p{N}", RegexOptions.Compiled);

        /// <summary>
        /// 提取字符串中的所有数字字符并合并为整数（支持全角/汉字数字）
        /// </summary>
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
        /// 与 DotNet.Drawing 的同名实现保持一致（以那份为准）：按字符逐个换成其数值后拼接。
        /// 原实现对数值只取 <c>ToString()</c> 的首字符，大于 9 的数字（如「⑫」= 12）被截成 "1"；
        /// 非整数值和无数值的字符直接跳过，不再拼出 "0" / "-"。
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

        #region 文件操作
        /// <summary>
        /// 判断路径对应的文件是否存在
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>存在时返回 true，否则返回 false</returns>
        public static bool FileExists(this string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        /// <summary>
        /// 判断路径对应的文件夹是否存在
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>存在时返回 true，否则返回 false</returns>
        public static bool FolderExists(this string folderPath)
        {
            return !string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath);
        }

        /// <summary>
        /// 检查文件夹是否存在，不存在则创建
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        public static void EnsureFolderExists(this string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                throw new ArgumentNullException(nameof(folderPath), "文件夹路径不能为空。");

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
            }
            catch (PathTooLongException ex)
            {
                // 提供中文提示：路径太长
                throw new InvalidOperationException(
                    $"路径超出了系统定义的最大长度。当前路径长度为 {folderPath.Length} 个字符，路径不能超过 260 个字符。",
                    ex
                );
            }
            catch (ArgumentException ex)
            {
                // 提供中文提示：路径中包含非法字符
                throw new ArgumentException("路径中包含非法字符，请检查路径是否符合系统要求。", nameof(folderPath), ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                // 提供中文提示：无权限访问路径
                throw new UnauthorizedAccessException("没有权限访问指定的路径，请检查权限设置。", ex);
            }
            catch (Exception ex)
            {
                // 捕获其他异常，提供统一的中文提示
                throw new InvalidOperationException($"创建文件夹时发生未知错误：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查文件路径对应的文件夹是否存在，不存在则创建
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public static void EnsureFileDirectoryExists(this string filePath)
        {
            // Null或空字符串检查
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("文件路径不能为空白字符", nameof(filePath));
            }

            try
            {
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (PathTooLongException ex)
            {
                // 提供中文提示：路径太长
                throw new InvalidOperationException(
                    $"路径超出了系统定义的最大长度。当前路径长度为 {filePath.Length} 个字符，路径不能超过 260 个字符。",
                    ex
                );
            }
            catch (ArgumentException ex)
            {
                // 提供中文提示：路径中包含非法字符
                throw new ArgumentException("路径中包含非法字符", nameof(filePath), ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                // 提供中文提示：无权限访问路径
                throw new UnauthorizedAccessException("没有权限访问指定的路径，请检查权限设置。", ex);
            }
            catch (Exception ex)
            {
                // 捕获其他异常，提供统一的中文提示
                throw new InvalidOperationException($"创建文件目录时发生未知错误：{ex.Message}", ex);
            }
        }

        #endregion

        #region 数值转换
        /// <summary>
        /// 转换为整型数组
        /// </summary>
        public static int[] ToIntArray(this string str, string separator = ";")
        {
            return ConvertStringToArray<int>(str, separator, (string s, out int num) =>
                int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num)
            );
        }

        /// <summary>
        /// 转换为长整型数组
        /// </summary>
        public static long[] ToLongArray(this string str, string separator = ";")
        {
            return ConvertStringToArray<long>(str, separator, (string s, out long num) =>
                long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num)
            );
        }

        /// <summary>
        /// 转换为浮点数组
        /// </summary>
        public static float[] ToFloatArray(this string str, string separator = ";")
        {
            return ConvertStringToArray<float>(str, separator, (string s, out float num) =>
                float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num) &&
                !float.IsNaN(num) &&
                !float.IsInfinity(num)
            );
        }

        /// <summary>
        /// 转换为双精度数组
        /// </summary>
        public static double[] ToDoubleArray(this string str, string separator = ";")
        {
            return ConvertStringToArray<double>(str, separator, (string s, out double num) =>
                double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num) &&
                !double.IsNaN(num) &&
                !double.IsInfinity(num)
            );
        }

        /// <summary>
        /// 转换为十进制数组
        /// </summary>
        public static decimal[] ToDecimalArray(this string str, string separator = ";")
        {
            return ConvertStringToArray<decimal>(str, separator, (string s, out decimal num) =>
                decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out num)
            );
        }

        /// <summary>
        /// 通用数值转换方法
        /// </summary>
        private static T[] ConvertStringToArray<T>(string str, string separator, TryParseHandler<T> tryParse)
        {
            if (str == null) return new T[0];
            if (separator == null) throw new ArgumentNullException(nameof(separator));

            var result = new List<T>();
            foreach (var part in str.Split(separator, true))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (tryParse(trimmed, out T value))
                {
                    result.Add(value);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 数值解析委托
        /// </summary>
        private delegate bool TryParseHandler<T>(string s, out T result);

        #endregion
    }
}