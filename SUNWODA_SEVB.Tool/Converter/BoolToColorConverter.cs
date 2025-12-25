using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SUNWODA_SEVB.Tool.Converter
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled && isEnabled)
                return new SolidColorBrush(Colors.Green);
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
