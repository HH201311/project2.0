using HandyControl.Controls;
using SUNWODA_SEVB.ViewModels.Windows.Common;
//using System.Windows;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Views.Windows.Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class CCTBMainWindow : Window
    {
        public CCTBMainWindow()
        {
            InitializeComponent();
        }

        private void Minimize_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private void Reduction_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Normal)
            {
                WindowState = System.Windows.WindowState.Maximized;
                return;
            }
            if (WindowState == System.Windows.WindowState.Maximized)
            {
                WindowState = System.Windows.WindowState.Normal;
                return;
            }
        }

        private void Close_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var re = MessageBox.Show("是否确认关闭软件？", "提示", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (re == System.Windows.MessageBoxResult.Yes) this.Close();
        }

        private DateTime mouseLeftButtonClickLastTime = DateTime.MinValue;
        private int mouseLeftButtonClickCount = 0;
        private void TopHeader_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var now = DateTime.Now;
            if ((now - mouseLeftButtonClickLastTime).TotalMilliseconds > 300)
            {
                mouseLeftButtonClickLastTime = now;
                mouseLeftButtonClickCount = 1;
                return;
            }

            mouseLeftButtonClickLastTime = now;
            mouseLeftButtonClickCount++;

            if (mouseLeftButtonClickCount == 2)
            {
                if (WindowState == System.Windows.WindowState.Normal)
                {
                    WindowState = System.Windows.WindowState.Maximized;
                    mouseLeftButtonClickCount = 0;
                    return;
                }
                if (WindowState == System.Windows.WindowState.Maximized)
                {
                    WindowState = System.Windows.WindowState.Normal;
                    mouseLeftButtonClickCount = 0;
                    return;
                }
            }
        }

        private void TopHeader_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

            DragMove();
        }
    }
}
