using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters.Project.CCTB
{
    public class StringFormatMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length <= 0)
                return string.Empty;

            if (parameter == null || !(parameter is string format))
                return string.Join("", values);

            return string.Format(format, values);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
