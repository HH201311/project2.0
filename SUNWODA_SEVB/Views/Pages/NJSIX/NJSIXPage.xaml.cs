using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SUNWODA_SEVB.ViewModels.Pages.NJSIX;

namespace SUNWODA_SEVB.Views.Pages.NJSIX
{
    public partial class NJSIXPage : Page
    {
        public NJSIXPage()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
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
            if (e.OldValue is VM_NJSIXPage oldVm)
            {
                oldVm.ConfigSummaryChanged -= RefreshConfigSummary;
                oldVm.RequestOpenSettings -= OpenSettingsWindow;
                oldVm.RequestOpenOfflineTest -= OpenOfflineTestWindow;
            }
            if (e.NewValue is VM_NJSIXPage vm)
            {
                vm.ConfigSummaryChanged += RefreshConfigSummary;
                vm.RequestOpenSettings += OpenSettingsWindow;
                vm.RequestOpenOfflineTest += OpenOfflineTestWindow;
                RefreshConfigSummary();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_NJSIXPage vm)
            {
                vm.ConfigSummaryChanged -= RefreshConfigSummary;
                vm.RequestOpenSettings -= OpenSettingsWindow;
                vm.RequestOpenOfflineTest -= OpenOfflineTestWindow;
            }
        }

        private void OpenSettingsWindow()
        {
            if (DataContext is not VM_NJSIXPage vm)
                return;
            if (!vm.CanEditRuntimeConfig)
                return;
            var win = new Pack6SettingsWindow
            {
                Owner = Window.GetWindow(this),
                DataContext = vm
            };
            win.ShowDialog();
            RefreshConfigSummary();
        }

        private void OpenOfflineTestWindow()
        {
            if (DataContext is not VM_NJSIXPage vm)
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

        private void RefreshConfigSummary()
        {
            if (DataContext is not VM_NJSIXPage vm || ConfigSummaryText is null)
                return;

            ConfigSummaryText.Inlines.Clear();
            ConfigSummaryText.Inlines.Add(new Run("当前配置：")
            {
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.DimGray
            });
            // 南京暂不核产品档位，摘要只显示收图路径
            ConfigSummaryText.Inlines.Add(new Run(
                $"上盖={vm.YOkRootPath} | 底部={vm.ZOkRootPath} | 四周={vm.SideCamPath}（档位校验关）")
            {
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            });
        }
    }
}
