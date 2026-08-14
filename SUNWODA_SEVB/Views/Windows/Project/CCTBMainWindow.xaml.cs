using HandyControl.Controls;
using SUNWODA_SEVB.ViewModels.Windows.Project;
using System.ComponentModel;
using System.Diagnostics;


//using System.Windows;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Views.Windows.Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class CCTBMainWindow : Window
    {
        private bool _isClosingConfirmed = false;
        private SemaphoreSlim _closingSemaphore = new(1, 1);
        public CCTBMainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 窗口关闭前的确认处理
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // 如果已经确认过退出，直接通过
            if (_isClosingConfirmed) return;

            // 阻止关闭，先执行确认流程
            e.Cancel = true;

            // 获取 ViewModel
            if (DataContext is VM_CCTBMainWindow viewModel)
            {
                // 调用 ViewModel 的退出确认方法
                RequestExitAsync(viewModel);
            }
            else
            {
                // 如果获取不到 ViewModel，直接弹窗确认
                ShowSimpleExitConfirmation();
            }
        }

        /// <summary>
        /// 请求退出（异步处理）
        /// </summary>
        private async void RequestExitAsync(VM_CCTBMainWindow viewModel)
        {
            try
            {
                // 调用 ViewModel 的退出确认逻辑
                bool canExit = await viewModel.RequestExitAsync();

                if (canExit)
                {
                    _isClosingConfirmed = true; 
                    Process.GetCurrentProcess().Kill();
                    Environment.Exit(0);
                    this.Close(); // 再次触发 Close，此时会通过
                }
            }
            catch (Exception ex)
            {
                // 记录错误日志
                HandyControl.Controls.MessageBox.Show(
                    $"退出时发生错误：{ex.Message}",
                    "错误"
                );
            }
        }

        /// <summary>
        /// 简单的退出确认（备用方案）
        /// </summary>
        private void ShowSimpleExitConfirmation()
        {
            var result = HandyControl.Controls.MessageBox.Show(
                "确定要退出软件吗？",
                "确认退出"
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _isClosingConfirmed = true;
                Process.GetCurrentProcess().Kill();
                Environment.Exit(0);
                this.Close();
            }
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
            this.Close();
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

        private void AccountButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.contextMenu.PlacementTarget = AccountButton;
            this.contextMenu.Placement = PlacementMode.Bottom;
            this.contextMenu.VerticalOffset = 10;
            this.contextMenu.IsOpen = true;
        }
    }
}
