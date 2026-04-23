using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class StringToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            var checkValue = value.ToString();
            if (checkValue == null) return false;
            var targetValue = parameter.ToString();

            return checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Binding.DoNothing;

            if (!(value is bool v) || !v) return Binding.DoNothing;

            try
            {
                var parameterValue = parameter.ToString();
                return parameterValue ?? string.Empty;
            }
            catch
            {
                return Binding.DoNothing;
            }
        }
    }
}
