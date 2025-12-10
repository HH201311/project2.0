using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace SUNWODA_SEVB.Core.Converters
{
    public class ImagePathToSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path))
            {
                Debug.WriteLine("[ImageConverter] 路径为空");
                return null;
            }

            try
            {
                // 如果已经是完整URI，直接使用
                if (path.StartsWith("pack://"))
                {
                    return new BitmapImage(new Uri(path, UriKind.Absolute));
                }

                // 相对路径转换为 Pack URI
                // 假设程序集名称为 SUNWODA_SEVB
                string assemblyName = "SUNWODA_SEVB";
                string packUri = $"pack://application:,,,/{assemblyName};component{path}";

                return new BitmapImage(new Uri(packUri, UriKind.Absolute));
            }
            catch (Exception)
            {
                return null;  // 加载失败返回null，避免崩溃
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
