using SUNWODA_SEVB.Core.Enumerations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.JRCW
{
    public class UserRoleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            if (!(value is UserRole urV)) return Visibility.Collapsed;
            if (!int.TryParse(parameter.ToString(), out int urP)) return Visibility.Collapsed;

            if (urV == UserRole.Guest) return Visibility.Collapsed;
            var re = urP >= (int)urV ? Visibility.Visible : Visibility.Collapsed;
            return re;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
