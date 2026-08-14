using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// byte[] 与 十六进制字符串 转换器
    /// 可通过 ConverterParameter 指定分隔符，如 "-" 或不填（默认空格）。
    /// </summary>
    public class ByteArrayToHexStringConverter : IValueConverter
    {
        /// <summary>
        /// 将 byte[] 转换为 十六进制字符串，默认使用空格分隔。
        /// 可通过 ConverterParameter 指定分隔符，如 "-" 或不填（默认空格）。
        /// ConvertBack 不支持，直接抛出异常。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not byte[] bytes || bytes.Length == 0)
                return string.Empty;

            // 分隔符：默认空格，可通过 ConverterParameter 自定义
            var separator = parameter as string ?? " ";

            var hex = BitConverter.ToString(bytes);
            if (separator != "-") hex = hex.Replace("-", separator);

            return hex;
        }

        /// <summary>
        /// 将 十六进制字符串 转换为 byte[]，默认使用空格分隔。
        /// 可通过 ConverterParameter 指定分隔符，如 "-" 或不填（默认空格）。
        /// ConvertBack 不支持，直接抛出异常。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string hexString) return Binding.DoNothing;

            // 空字符串 → 空数组
            if (string.IsNullOrWhiteSpace(hexString)) return Array.Empty<byte>();

            // 移除所有非十六进制字符（忽略分隔符，同时过滤掉非法字符）
            var hexOnly = Regex.Replace(hexString, @"[^0-9a-fA-F]", "");

            // 有效十六进制字符串长度必须是偶数
            if (hexOnly.Length % 2 != 0) return Binding.DoNothing;

            try
            {
                var bytes = new byte[hexOnly.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = System.Convert.ToByte(hexOnly.Substring(i * 2, 2), 16);
                }
                return bytes;
            }
            catch
            {
                return Binding.DoNothing; // 解析异常，保持原有绑定值不变
            }
        }
    }
}
