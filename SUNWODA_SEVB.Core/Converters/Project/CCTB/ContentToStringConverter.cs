using SUNWODA_SEVB.Core.Models.Project.CCTB;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class ContentToStringConverter : IValueConverter
    {
        /// <summary>
        /// 将 Content 转换为文本
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            // 优先处理字符串
            if (value is string s) return s;

            // 处理常见 UIElement 的 Content 情况：如果是 FrameworkElement 且有一个 Content 属性
            // 这里只是简单示例，真正情况可更复杂
            if (value is System.Windows.FrameworkElement fe)
            {
                // 递归尝试获取内容（如果是 ContentControl、Header 等）
                var contentProp = fe.GetType().GetProperty("Content");
                if (contentProp != null)
                {
                    var inner = contentProp.GetValue(fe);
                    return Convert(inner, targetType, parameter, culture)?.ToString() ?? string.Empty;
                }
            }

            // 对于其他类型，直接使用 ToString
            return value.ToString();
        }

        /// <summary>
        /// 绑定回传一般不需要，将文本转换回 Content
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object? ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(string))
                return value?.ToString();
            return DependencyProperty.UnsetValue;
        }
    }
}
