using System.Globalization;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// 枚举类型转布尔值转换器
    /// 备注：通过string方式
    /// </summary>
    public class EnumToBoolConverter : IValueConverter
    {
        /// <summary>
        /// 将枚举值转换为 bool
        /// </summary>
        /// <param name="value">绑定的枚举值</param>
        /// <param name="targetType">目标类型 (bool)</param>
        /// <param name="parameter">
        /// 对比参数，支持以下格式：
        /// - 单个值：直接对比枚举值
        /// - 多个值（逗号分隔）：匹配任意一个即返回 true
        /// - 带 "!" 前缀：反向匹配（不匹配时返回 true）
        /// </param>
        /// <param name="culture">区域信息</param>
        /// <returns>true 或 false</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            var enumValue = value.ToString();
            var paramString = parameter.ToString();

            if (string.IsNullOrEmpty(paramString)) return false;

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
            return isInverted ? !isMatch : isMatch;
        }

        /// <summary>
        /// 将 bool 转换回枚举值（通常不实现，因为不可逆）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool boolValue && boolValue && parameter != null)) return Binding.DoNothing;

            var paramString = parameter.ToString();
            if (string.IsNullOrEmpty(paramString)) return Binding.DoNothing;

            // 处理反向逻辑
            var isInverted = paramString.StartsWith("!");
            if (isInverted) paramString = paramString.Substring(1);

            // 取第一个值作为返回的枚举值
            var expectedValue = paramString.Split(',')[0].Trim();

            // 从 targetType 获取枚举类型
            var enumType = targetType;
            if (enumType.IsEnum)
            {
                return Enum.Parse(enumType, expectedValue);
            }

            // 如果 targetType 是 Nullable<Enum>，提取基础类型
            if (Nullable.GetUnderlyingType(enumType)?.IsEnum == true)
            {
                return Enum.Parse(Nullable.GetUnderlyingType(enumType), expectedValue);
            }

            return Binding.DoNothing;
        }
    }
}
