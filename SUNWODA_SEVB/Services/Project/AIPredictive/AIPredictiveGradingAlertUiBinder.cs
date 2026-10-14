using System.Windows;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;
using SUNWODA_SEVB.Views.Windows.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 将分级预警事件绑到 UI 线程：主窗置顶 + 红色遮罩；确认仅关闭遮罩（不抑制后续轮次）。
    /// </summary>
    public sealed class AIPredictiveGradingAlertUiBinder
    {
        private readonly IAIPredictiveGradingAlertService _grading;
        private readonly ILoggerService<AIPredictiveGradingAlertUiBinder> _logger;
        private readonly object _uiGate = new();
        private bool _overlayOpen;

        public AIPredictiveGradingAlertUiBinder(
            IAIPredictiveGradingAlertService grading,
            ILoggerService<AIPredictiveGradingAlertUiBinder> logger)
        {
            _grading = grading;
            _logger = logger;
        }

        public void Attach()
        {
            _grading.AlertsRaised -= OnAlertsRaised;
            _grading.AlertsRaised += OnAlertsRaised;
            _logger.Info("分级预警 UI 绑定已挂接", false);
        }

        public void Detach()
        {
            _grading.AlertsRaised -= OnAlertsRaised;
        }

        private void OnAlertsRaised(IReadOnlyList<AIPredictiveGradingAlertItem> alerts)
        {
            if (alerts is null || alerts.Count == 0)
                return;

            var app = Application.Current;
            if (app is null)
                return;

            app.Dispatcher.BeginInvoke(new Action(() => ShowOverlay(alerts)));
        }

        private void ShowOverlay(IReadOnlyList<AIPredictiveGradingAlertItem> alerts)
        {
            lock (_uiGate)
            {
                if (_overlayOpen)
                {
                    _logger.Info("分级预警遮罩已打开，忽略本轮弹窗", false);
                    return;
                }

                _overlayOpen = true;
            }

            try
            {
                BringMainWindowToFront();

                var overlay = new GradingAlertOverlayWindow(alerts)
                {
                    Owner = Application.Current?.MainWindow,
                };
                overlay.ShowDialog();
            }
            catch (Exception ex)
            {
                _logger.Error("显示分级预警遮罩失败", ex);
            }
            finally
            {
                lock (_uiGate)
                    _overlayOpen = false;
            }
        }

        private static void BringMainWindowToFront()
        {
            var main = Application.Current?.MainWindow;
            if (main is null)
                return;

            if (main.WindowState == WindowState.Minimized)
                main.WindowState = WindowState.Normal;

            main.Show();
            main.Activate();
            // 短暂 Topmost 强制置前（部分工控机焦点策略下 Activate 不够）
            var wasTop = main.Topmost;
            main.Topmost = true;
            main.Topmost = wasTop;
        }
    }
}
