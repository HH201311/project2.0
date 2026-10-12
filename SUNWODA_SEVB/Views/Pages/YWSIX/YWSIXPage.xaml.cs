using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SUNWODA_SEVB.ViewModels.Pages.YWSIX;

namespace SUNWODA_SEVB.Views.Pages.YWSIX
{
    public partial class YWSIXPage : Page
    {
        public YWSIXPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void CameraPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
                return;
            if (sender is not FrameworkElement { DataContext: Pack6CameraSlotVm slot })
                return;
            if (string.IsNullOrWhiteSpace(slot.FullImagePath))
                return;
            Pack6PreviewFullscreenWindow.ShowFor(Window.GetWindow(this), slot.FullImagePath, slot.Title);
            e.Handled = true;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is VM_YWSIXPage oldVm)
                UnsubscribeVm(oldVm);
            if (e.NewValue is VM_YWSIXPage vm && IsLoaded)
                SubscribeVm(vm);
        }

        /// <summary>
        /// 导航离开会 Unloaded 退订；再进页若 View 缓存且 DataContext 未变，不会再触发 DataContextChanged，
        /// 必须在 Loaded 里重新订阅，否则「参数设置」Command 空转无弹窗。
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_YWSIXPage vm)
                SubscribeVm(vm);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_YWSIXPage vm)
                UnsubscribeVm(vm);
        }

        private void SubscribeVm(VM_YWSIXPage vm)
        {
            // 先减后加，避免重复订阅
            UnsubscribeVm(vm);
            vm.RequestOpenSettings += OpenSettingsWindow;
            vm.RequestOpenOfflineTest += OpenOfflineTestWindow;
            vm.RequestOpenPinSettings += OpenPinSettingsWindow;
        }

        private void UnsubscribeVm(VM_YWSIXPage vm)
        {
            vm.RequestOpenSettings -= OpenSettingsWindow;
            vm.RequestOpenOfflineTest -= OpenOfflineTestWindow;
            vm.RequestOpenPinSettings -= OpenPinSettingsWindow;
        }

        private void OpenSettingsWindow()
        {
            if (DataContext is not VM_YWSIXPage vm)
                return;
            if (!vm.CanEditRuntimeConfig)
                return;
            var win = new Pack6SettingsWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = vm
            };
            win.ShowDialog();
        }

        private void OpenPinSettingsWindow()
        {
            if (DataContext is not VM_YWSIXPage vm)
                return;
            if (!vm.CanEditPinSettings)
            {
                MessageBox.Show(
                    "当前账号无权限修改参数，请在侧边栏以管理员登录。",
                    "参数设置",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var win = new Pack6PinSettingsWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = vm
            };
            win.ShowDialog();
        }

        private void OpenOfflineTestWindow()
        {
            if (DataContext is not VM_YWSIXPage vm)
                return;
            if (!vm.CanEditRuntimeConfig)
            {
                MessageBox.Show("请先停止自动检测，再做离线测试。", "离线测试",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var win = new Pack6OfflineTestWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = vm
            };
            win.ShowDialog();
        }
    }
}
