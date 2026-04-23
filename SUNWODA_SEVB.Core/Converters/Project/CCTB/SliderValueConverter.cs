using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class SliderValueConverter : IValueConverter
    {
        /// <summary>
        /// 将滑块值转换为显示文本
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue)
            {
                doubleValue = Math.Truncate(Math.Round(doubleValue * 100, 10)) / 100;
                // 根据精度格式化显示
                return doubleValue.ToString(culture);
            }
            return value;
        }

        /// <summary>
        /// 将文本框文本转换为滑块值
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <param name="parameter"></param>
        /// <param name="culture"></param>
        /// <returns></returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                // 输入小数点时不更新源
                if (stringValue.EndsWith('.')) return Binding.DoNothing;
                if (double.TryParse(stringValue, NumberStyles.Any, culture, out double result))
                {
                    return result;
                }
            }
            return Binding.DoNothing; // 输入无效时不更新源
        }
    }
}
