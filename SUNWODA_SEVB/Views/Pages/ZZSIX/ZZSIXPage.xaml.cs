using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.ViewModels.Pages.ZZSIX;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    public partial class ZZSIXPage : Page
    {
        private readonly HashSet<Pack6NgAlarmWindow> _defectAlarmWindows = [];

        public ZZSIXPage()
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
            if (e.OldValue is VM_ZZSIXPage oldVm)
            {
                oldVm.ConfigSummaryChanged -= RefreshConfigSummary;
                oldVm.RequestOpenSettings -= OpenSettingsWindow;
                oldVm.RequestOpenOfflineTest -= OpenOfflineTestWindow;
                oldVm.RequestDefectAlarm -= ShowDefectAlarm;
            }
            if (e.NewValue is VM_ZZSIXPage vm)
            {
                vm.ConfigSummaryChanged += RefreshConfigSummary;
                vm.RequestOpenSettings += OpenSettingsWindow;
                vm.RequestOpenOfflineTest += OpenOfflineTestWindow;
                vm.RequestDefectAlarm += ShowDefectAlarm;
                RefreshConfigSummary();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is VM_ZZSIXPage vm)
            {
                vm.ConfigSummaryChanged -= RefreshConfigSummary;
                vm.RequestOpenSettings -= OpenSettingsWindow;
                vm.RequestOpenOfflineTest -= OpenOfflineTestWindow;
                vm.RequestDefectAlarm -= ShowDefectAlarm;
            }

            foreach (var win in _defectAlarmWindows.ToList())
            {
                try
                {
                    win.ForceCloseForPageUnload();
                }
                catch
                {
                    // ignore
                }
            }
            _defectAlarmWindows.Clear();
        }

        private void OpenSettingsWindow()
        {
            if (DataContext is not VM_ZZSIXPage vm)
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
            if (DataContext is not VM_ZZSIXPage vm)
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

        /// <summary>整包 NG/缺张：每产品一扇窗；扫码匹配 SN 后关闭；开窗写 PLC true，全关写 false。</summary>
        private void ShowDefectAlarm(string productCode, string defectText)
        {
            var sn = (productCode ?? string.Empty).Trim();
            if (sn.Length == 0)
                return;

            void Apply()
            {
                var alarmTime = DateTime.Now;
                var win = new Pack6NgAlarmWindow
                {
                    Owner = Window.GetWindow(this)
                };
                win.Bind(sn, defectText, alarmTime);
                _defectAlarmWindows.Add(win);

                win.Closed += async (_, _) =>
                {
                    _defectAlarmWindows.Remove(win);
                    if (DataContext is VM_ZZSIXPage vm)
                        await vm.NotifyDefectAlarmWindowClosedAsync();
                };

                win.Show();
                if (DataContext is VM_ZZSIXPage pageVm)
                    _ = pageVm.NotifyDefectAlarmWindowOpenedAsync();
            }

            if (Dispatcher.CheckAccess())
                Apply();
            else
                Dispatcher.Invoke(Apply);
        }

        private void RefreshConfigSummary()
        {
            if (DataContext is not VM_ZZSIXPage vm || ConfigSummaryText is null)
                return;

            ConfigSummaryText.Inlines.Clear();
            ConfigSummaryText.Inlines.Add(new Run("当前配置：")
            {
                FontWeight = FontWeights.Normal,
                Foreground = Brushes.DimGray
            });
            ConfigSummaryText.Inlines.Add(new Run(vm.ProductCodeProfileName)
            {
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black
            });
        }
    }
}
