using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// 枚举类型转换器
    /// 备注：通过string方式
    /// </summary>
    public class EnumToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将枚举值转换为 Visibility
        /// </summary>
        /// <param name="value">绑定的枚举值</param>
        /// <param name="targetType">目标类型 (Visibility)</param>
        /// <param name="parameter">
        /// 对比参数，支持以下格式：
        /// - 单个值：直接对比枚举值
        /// - 多个值（逗号分隔）：匹配任意一个即返回 Visible
        /// - 带 "!" 前缀：反向匹配（不匹配时返回 Visible）
        /// </param>
        /// <param name="culture">区域信息</param>
        /// <returns>Visibility.Visible 或 Visibility.Collapsed</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;

            var enumValue = value.ToString();
            var paramString = parameter.ToString();

            if (string.IsNullOrEmpty(paramString)) return Visibility.Collapsed;

            // 处理反向逻辑（以 ! 开头）
            var isInverted = paramString.StartsWith("!");
            if (isInverted) paramString = paramString.Substring(1);

            // 支持多个值（逗号分隔）
            var expectedValues = paramString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            bool isMatch = false;
            foreach (var expected in expectedValues)
            {
                if (string.Equals(enumValue, expected.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    isMatch = true;
                    break;
                }
            }

            // 根据是否反向和是否匹配返回结果
            var shouldBeVisible = isInverted ? !isMatch : isMatch;

            return shouldBeVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 将 Visibility 转换回枚举值（通常不实现，因为不可逆）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Visibility 到枚举的转换通常没有意义，抛出异常或返回 null
            throw new NotSupportedException("EnumToVisibilityConverter 不支持双向绑定");
        }
    }
}
