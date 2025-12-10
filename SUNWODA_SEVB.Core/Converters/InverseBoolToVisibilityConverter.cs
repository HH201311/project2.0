using System.Globalization;
using System.Windows.Data;

namespace SUNWODA_SEVB.Core.Converters
{
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }
            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                if (visibility == System.Windows.Visibility.Visible)
                    return false;
                else if(visibility == System.Windows.Visibility.Collapsed)
                    return true;
            }
            return true;
        }
    }
}
