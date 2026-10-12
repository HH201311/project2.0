using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    public partial class Pack6PreviewFullscreenWindow : Window
    {
        public Pack6PreviewFullscreenWindow(string imagePath, string title)
        {
            InitializeComponent();
            Title = string.IsNullOrWhiteSpace(title) ? "预览放大" : title;
            TryLoad(imagePath);
        }

        public static void ShowFor(Window? owner, string imagePath, string title)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return;

            var win = new Pack6PreviewFullscreenWindow(imagePath, title)
            {
                Owner = owner
            };
            win.ShowDialog();
        }

        private void TryLoad(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                // 全屏仍限制解码宽度，避免 5472 原图像素整幅进托管堆
                bmp.DecodePixelWidth = 2560;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                FullImage.Source = bmp;
            }
            catch
            {
                FullImage.Source = null;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
                Close();
        }
    }
}
