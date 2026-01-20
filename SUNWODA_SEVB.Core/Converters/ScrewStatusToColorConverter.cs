using SUNWODA_SEVB.Core.Models.Project.STC;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SUNWODA_SEVB.Core.Converters
{
    /// <summary>
    /// 螺丝状态到颜色转换器
    /// </summary>
    public class ScrewStatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush OkBrush = new(Color.FromRgb(76, 175, 80));      // 绿色
        private static readonly SolidColorBrush NgBrush = new(Color.FromRgb(244, 67, 54));     // 红色
        private static readonly SolidColorBrush PendingBrush = new(Color.FromRgb(158, 158, 158)); // 灰色

        static ScrewStatusToColorConverter()
        {
            // 冻结画刷以提高性能
            OkBrush.Freeze();
            NgBrush.Freeze();
            PendingBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ScrewStatus status)
            {
                return status switch
                {
                    ScrewStatus.OK => OkBrush,
                    ScrewStatus.NG => NgBrush,
                    ScrewStatus.Pending => PendingBrush,
                    _ => PendingBrush
                };
            }
            return PendingBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
