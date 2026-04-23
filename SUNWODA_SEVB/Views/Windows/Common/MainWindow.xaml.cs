using HandyControl.Controls;
using SUNWODA_SEVB.ViewModels.Windows.Common;
//using System.Windows;
using System.ComponentModel;
using System.Windows.Controls.Primitives;

namespace SUNWODA_SEVB.Views.Windows.Common
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _isClosingConfirmed = false;
        private SemaphoreSlim _closingSemaphore = new(1, 1);
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (NavigationBar.Visibility == System.Windows.Visibility.Visible)
            {
                NavigationColumnDefinition.MinWidth = 200;
            }
        }
        /// <summary>
        /// 窗口关闭前的确认处理
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            // 如果已经确认过退出，直接通过
            if (_isClosingConfirmed)
                return;

            // 阻止关闭，先执行确认流程
            e.Cancel = true;

            // 获取 ViewModel
            if (DataContext is VM_MainWindow viewModel)
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
        private async void RequestExitAsync(VM_MainWindow viewModel)
        {
            try
            {
                // 调用 ViewModel 的退出确认逻辑
                bool canExit = await viewModel.RequestExitAsync();

                if (canExit)
                {
                    _isClosingConfirmed = true;
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
                this.Close();
            }
        }
    }
}
