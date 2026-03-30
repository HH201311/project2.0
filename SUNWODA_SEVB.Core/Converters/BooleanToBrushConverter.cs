using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        // 可在 XAML 中自定义颜色，默认绿/灰
        public Brush TrueBrush { get; set; } = Brushes.LimeGreen;
        public Brush FalseBrush { get; set; } = Brushes.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? TrueBrush : FalseBrush;
            //                           ↑ Brush        ↑ Brush  → 输出颜色
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing; // 颜色一般不需要反向转换
        }
    }
}
