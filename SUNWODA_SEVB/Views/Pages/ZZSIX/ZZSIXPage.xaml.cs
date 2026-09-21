using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SUNWODA_SEVB.ViewModels.Pages.ZZSIX;

namespace SUNWODA_SEVB.Views.Pages.ZZSIX
{
    public partial class ZZSIXPage : Page
    {
        private Pack6NgAlarmWindow? _ngAlarmWindow;
        private Pack6MissingShotAlarmWindow? _missingShotWindow;
        /// <summary>当前未关闭警示窗内展示的产品码；关窗后清空，下次从新 NG 起算。</summary>
        private readonly List<string> _ngAlarmCodes = [];
        /// <summary>关窗过程中到达的 NG，Closed 后再弹新窗，避免与旧列表混在一起。</summary>
        private readonly List<string> _ngAlarmPendingWhileClosing = [];
        private bool _ngAlarmClosing;

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
                oldVm.RequestNgAlarm -= ShowOrUpdateNgAlarm;
                oldVm.RequestMissingShotAlarm -= ShowMissingShotAlarm;
            }
            if (e.NewValue is VM_ZZSIXPage vm)
            {
                vm.ConfigSummaryChanged += RefreshConfigSummary;
                vm.RequestOpenSettings += OpenSettingsWindow;
                vm.RequestOpenOfflineTest += OpenOfflineTestWindow;
                vm.RequestNgAlarm += ShowOrUpdateNgAlarm;
                vm.RequestMissingShotAlarm += ShowMissingShotAlarm;
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
                vm.RequestNgAlarm -= ShowOrUpdateNgAlarm;
                vm.RequestMissingShotAlarm -= ShowMissingShotAlarm;
            }

            _ngAlarmCodes.Clear();
            _ngAlarmPendingWhileClosing.Clear();
            _ngAlarmClosing = false;
            try
            {
                _ngAlarmWindow?.Close();
                _missingShotWindow?.Close();
            }
            catch
            {
                // ignore
            }

            _ngAlarmWindow = null;
            _missingShotWindow = null;
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

        /// <summary>
        /// 非模态 NG 警示：窗开着则追加产品码；已关闭则新窗只显示本次及之后未确认的 NG。
        /// </summary>
        private void ShowOrUpdateNgAlarm(string productCode)
        {
            var sn = (productCode ?? string.Empty).Trim();
            if (sn.Length == 0)
                return;

            void Apply()
            {
                // 正在关闭：先记下，等 Closed 后再开新窗（不带旧列表）
                if (_ngAlarmClosing)
                {
                    if (!_ngAlarmPendingWhileClosing.Contains(sn, StringComparer.OrdinalIgnoreCase))
                        _ngAlarmPendingWhileClosing.Add(sn);
                    return;
                }

                if (_ngAlarmWindow is null || !_ngAlarmWindow.IsLoaded)
                {
                    _ngAlarmCodes.Clear();
                    _ngAlarmCodes.Add(sn);
                    _ngAlarmWindow = new Pack6NgAlarmWindow
                    {
                        Owner = Window.GetWindow(this)
                    };
                    _ngAlarmWindow.Closing += OnNgAlarmClosing;
                    _ngAlarmWindow.Closed += OnNgAlarmClosed;
                    _ngAlarmWindow.UpdateNgList(_ngAlarmCodes);
                    _ngAlarmWindow.Show();
                    return;
                }

                if (!_ngAlarmCodes.Contains(sn, StringComparer.OrdinalIgnoreCase))
                    _ngAlarmCodes.Add(sn);
                _ngAlarmWindow.UpdateNgList(_ngAlarmCodes);
                if (!_ngAlarmWindow.IsActive)
                    _ngAlarmWindow.Activate();
            }

            if (Dispatcher.CheckAccess())
                Apply();
            else
                Dispatcher.Invoke(Apply);
        }

        private void OnNgAlarmClosing(object? sender, CancelEventArgs e)
        {
            _ngAlarmClosing = true;
            // 关窗即丢弃已确认列表；之后新 NG 进 pending，Closed 时另开新窗
            _ngAlarmCodes.Clear();
        }

        private void OnNgAlarmClosed(object? sender, EventArgs e)
        {
            if (sender is Pack6NgAlarmWindow win)
            {
                win.Closing -= OnNgAlarmClosing;
                win.Closed -= OnNgAlarmClosed;
            }

            if (ReferenceEquals(_ngAlarmWindow, sender))
                _ngAlarmWindow = null;

            _ngAlarmCodes.Clear();
            _ngAlarmClosing = false;

            if (_ngAlarmPendingWhileClosing.Count == 0)
                return;

            var pending = _ngAlarmPendingWhileClosing.ToList();
            _ngAlarmPendingWhileClosing.Clear();
            foreach (var code in pending)
                ShowOrUpdateNgAlarm(code);
        }

        /// <summary>缺张超时：非模态黄色报警，列出缺哪张；可同时开多个。</summary>
        private void ShowMissingShotAlarm(string productCode, string detail)
        {
            void Apply()
            {
                // 若上一扇仍开着且同产品，刷新明细；否则新开一扇
                if (_missingShotWindow is not null &&
                    _missingShotWindow.IsLoaded &&
                    string.Equals(_missingShotWindow.Tag as string, productCode, StringComparison.OrdinalIgnoreCase))
                {
                    _missingShotWindow.ShowAlarm(productCode, detail);
                    if (!_missingShotWindow.IsActive)
                        _missingShotWindow.Activate();
                    return;
                }

                var win = new Pack6MissingShotAlarmWindow
                {
                    Owner = Window.GetWindow(this),
                    Tag = productCode
                };
                win.Closed += (_, _) =>
                {
                    if (ReferenceEquals(_missingShotWindow, win))
                        _missingShotWindow = null;
                };
                _missingShotWindow = win;
                win.ShowAlarm(productCode, detail);
                win.Show();
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
