using SUNWODA_SEVB.Core.Enumerations;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class ConfigToVisibilityMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // -TODO    values个数判断
            if (values == null || parameter == null)
                return Visibility.Collapsed;

            if (!(values[0] is EClosedLoopType closedLoopType)) return Visibility.Collapsed;
            //// -TODO    当前用户权限
            //if (!(values[1] is UserRole userRole)) return Visibility.Collapsed;
            //// -TODO    控件名称
            //if (!(values[2] is string controlName)) return Visibility.Collapsed;
            //// -TODO    用户配置
            //if (!(values[3] is Dictionary<string, bool> config)) return Visibility.Collapsed;

            // -TODO    先判断用户权限

            // 在判断闭环类型
            var checkValue = closedLoopType.ToString();
            if (checkValue == null) return Visibility.Collapsed;
            if (!(parameter is IEnumerable ps))
            {
                var targetValue = parameter.ToString();
                return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var p in ps)
            {
                var targetValue = p.ToString();
                if (checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
