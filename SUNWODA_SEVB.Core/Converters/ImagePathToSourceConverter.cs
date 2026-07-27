using System.Diagnostics;
using System.Globalization;
using System.IO;
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
                Debug.WriteLine($"[ImageConverter] 尝试加载图片: {path}");

                // 1. 已经是完整的 Pack URI
                if (path.StartsWith("pack://"))
                {
                    Debug.WriteLine("[ImageConverter] 使用 Pack URI 加载");
                    return CreateBitmapImage(new Uri(path, UriKind.Absolute));
                }

                // 2. 绝对文件路径 (如 C:\Images\xxx.png 或 \\server\share\xxx.png)
                if (Path.IsPathRooted(path))
                {
                    if (File.Exists(path))
                    {
                        Debug.WriteLine($"[ImageConverter] 使用绝对路径加载: {path}");
                        return CreateBitmapImageFromFile(path);
                    }
                    Debug.WriteLine($"[ImageConverter] 绝对路径文件不存在: {path}");
                    return null;
                }

                // 3. 相对路径 - 优先检查外部文件
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                // 移除开头的 / 或 \
                string normalizedPath = path.TrimStart('/', '\\');
                string externalFilePath = Path.Combine(appDirectory, normalizedPath);


                //externalFilePath = "D:\\sunwoda2.0.0.1\\SUNWODA_SEVB\\Assets\\Images\\shanggai.png";
                if (File.Exists(externalFilePath))
                {
                    Debug.WriteLine($"[ImageConverter] 使用外部文件加载: {externalFilePath}");
                    return CreateBitmapImageFromFile(externalFilePath);
                }

                // 4. 尝试作为程序集内嵌资源加载
                Debug.WriteLine("[ImageConverter] 外部文件不存在，尝试 Pack URI");
                string assemblyName = "SUNWODA_SEVB";

                // 确保路径以 / 开头
                string resourcePath = path.StartsWith("/") ? path : "/" + path;
                string packUri = $"pack://application:,,,/{assemblyName};component{resourcePath}";

                Debug.WriteLine($"[ImageConverter] Pack URI: {packUri}");
                return CreateBitmapImage(new Uri(packUri, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageConverter] 加载失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从URI创建BitmapImage
        /// </summary>
        private static BitmapImage CreateBitmapImage(Uri uri)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // 提高性能，允许跨线程访问
            return bitmap;
        }

        /// <summary>
        /// 从文件路径创建BitmapImage（支持文件被占用的情况）
        /// </summary>
        private static BitmapImage CreateBitmapImageFromFile(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}