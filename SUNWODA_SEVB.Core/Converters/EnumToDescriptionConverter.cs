using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// 将枚举值转换为 DescriptionAttribute 描述文本。
    /// 如果枚举没有 Description 属性，则返回枚举名称。
    /// 用于 XAML 中显示 StepType 等枚举的中文描述。
    /// </summary>
    public class EnumToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var type = value.GetType();
            if (!type.IsEnum)
                return value.ToString()!;

            var fieldName = value.ToString()!;
            var fieldInfo = type.GetField(fieldName);
            if (fieldInfo == null)
                return fieldName;

            var attr = (DescriptionAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute));
            return attr?.Description ?? fieldName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}