using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Common.Commands;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;
using SUNWODA_SEVB.Views.Windows.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.ViewModels.Pages.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化切片页面 ViewModel（partial：RoiPreview）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void RefreshHistogramRoiDisplay()
        {
            HistogramRoiDisplayText = _histogramRoiWidth > 0 && _histogramRoiHeight > 0
                ? $"X={_histogramRoiX}, Y={_histogramRoiY}, W={_histogramRoiWidth}, H={_histogramRoiHeight}"
                : "未标定";
        }

        private void RefreshLutSegmentPointsDisplay()
        {
            LutSegmentPointsDisplayTextPositive = !_lutSegmentPointsCalibratedPositive
                ? "未标定"
                : $"P1=({_lutSegmentPoint1XPositive},{_lutSegmentPoint1YPositive}) " +
                  $"P2=({_lutSegmentPoint2XPositive},{_lutSegmentPoint2YPositive})";

            LutSegmentPointsDisplayTextNegative = !_lutSegmentPointsCalibratedNegative
                ? "未标定"
                : $"P1=({_lutSegmentPoint1XNegative},{_lutSegmentPoint1YNegative}) " +
                  $"P2=({_lutSegmentPoint2XNegative},{_lutSegmentPoint2YNegative})";
        }

        private void RefreshVision3dRoiDisplay()
        {
            Vision3dRoiDisplayText = _vision3dRoiWidth > 0 && _vision3dRoiHeight > 0
                ? $"X={_vision3dRoiX}, Y={_vision3dRoiY}, W={_vision3dRoiWidth}, H={_vision3dRoiHeight}"
                : "未标定";
        }

        private void RefreshVisionTopViewRoiDisplay()
        {
            VisionTopViewRoiDisplayText = _visionTopViewRoiWidth > 0 && _visionTopViewRoiHeight > 0
                ? $"X={_visionTopViewRoiX}, Y={_visionTopViewRoiY}, W={_visionTopViewRoiWidth}, H={_visionTopViewRoiHeight}"
                : "未标定";
        }

        private void SetRoiPreviewFlag(
            ref bool field,
            string propertyName,
            bool value,
            RoiPreviewKind kind)
        {
            if (_isUpdatingRoiPreviewFlags)
            {
                field = value;
                OnPropertyChanged(propertyName);
                return;
            }

            if (field == value)
            {
                return;
            }

            if (value)
            {
                if (!TryGetRoiRect(kind, out var rect, out var label, out var error))
                {
                    MessageShower = error;
                    field = false;
                    OnPropertyChanged(propertyName);
                    return;
                }

                // 互斥：只预览当前勾选的一块 ROI
                ClearOtherRoiPreviewFlags(kind);
                field = true;
                OnPropertyChanged(propertyName);
                ShowRoiPreviewOverlay(rect, label);
            }
            else
            {
                field = false;
                OnPropertyChanged(propertyName);
                CloseRoiPreviewOverlay(restoreMainWindow: true);
            }
        }

        private void ClearOtherRoiPreviewFlags(RoiPreviewKind keep)
        {
            _isUpdatingRoiPreviewFlags = true;
            try
            {
                if (keep != RoiPreviewKind.Histogram && _isPreviewHistogramRoi)
                {
                    _isPreviewHistogramRoi = false;
                    OnPropertyChanged(nameof(IsPreviewHistogramRoi));
                }

                if (keep != RoiPreviewKind.Vision3d && _isPreviewVision3dRoi)
                {
                    _isPreviewVision3dRoi = false;
                    OnPropertyChanged(nameof(IsPreviewVision3dRoi));
                }

                if (keep != RoiPreviewKind.VisionTopView && _isPreviewVisionTopViewRoi)
                {
                    _isPreviewVisionTopViewRoi = false;
                    OnPropertyChanged(nameof(IsPreviewVisionTopViewRoi));
                }
            }
            finally
            {
                _isUpdatingRoiPreviewFlags = false;
            }
        }

        private bool TryGetRoiRect(
            RoiPreviewKind kind,
            out System.Drawing.Rectangle rect,
            out string label,
            out string error)
        {
            rect = default;
            label = string.Empty;
            error = string.Empty;

            switch (kind)
            {
                case RoiPreviewKind.Histogram:
                    if (_histogramRoiWidth <= 0 || _histogramRoiHeight <= 0)
                    {
                        error = "直方图 ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _histogramRoiX, _histogramRoiY, _histogramRoiWidth, _histogramRoiHeight);
                    label = "直方图 ROI";
                    return true;

                case RoiPreviewKind.Vision3d:
                    if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
                    {
                        error = "3D ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _vision3dRoiX, _vision3dRoiY, _vision3dRoiWidth, _vision3dRoiHeight);
                    label = "3D 视图 ROI";
                    return true;

                case RoiPreviewKind.VisionTopView:
                    if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
                    {
                        error = "「上」视图 ROI 尚未标定，无法预览";
                        return false;
                    }

                    rect = new System.Drawing.Rectangle(
                        _visionTopViewRoiX, _visionTopViewRoiY, _visionTopViewRoiWidth, _visionTopViewRoiHeight);
                    label = "「上」视图 ROI";
                    return true;

                default:
                    error = "未知 ROI 类型";
                    return false;
            }
        }

        private void ShowRoiPreviewOverlay(System.Drawing.Rectangle rect, string label)
        {
            CloseRoiPreviewOverlay(restoreMainWindow: false);

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null && mainWindow.WindowState != WindowState.Minimized)
            {
                _roiPreviewPreviousMainWindowState = mainWindow.WindowState;
                mainWindow.WindowState = WindowState.Minimized;
                Thread.Sleep(250);
            }

            var overlay = new RoiPreviewOverlayWindow(rect, label);
            _activeRoiPreviewOverlay = overlay;
            overlay.Closed += (_, _) =>
            {
                if (!ReferenceEquals(_activeRoiPreviewOverlay, overlay))
                {
                    return;
                }

                _activeRoiPreviewOverlay = null;
                _isUpdatingRoiPreviewFlags = true;
                try
                {
                    _isPreviewHistogramRoi = false;
                    _isPreviewVision3dRoi = false;
                    _isPreviewVisionTopViewRoi = false;
                    OnPropertyChanged(nameof(IsPreviewHistogramRoi));
                    OnPropertyChanged(nameof(IsPreviewVision3dRoi));
                    OnPropertyChanged(nameof(IsPreviewVisionTopViewRoi));
                }
                finally
                {
                    _isUpdatingRoiPreviewFlags = false;
                }

                RestoreMainWindowAfterRoiPreview();
            };

            overlay.Show();
        }

        private void CloseRoiPreviewOverlay(bool restoreMainWindow)
        {
            if (_activeRoiPreviewOverlay == null)
            {
                if (restoreMainWindow)
                {
                    RestoreMainWindowAfterRoiPreview();
                }

                return;
            }

            var overlay = _activeRoiPreviewOverlay;
            _activeRoiPreviewOverlay = null;
            try
            {
                overlay.Close();
            }
            catch
            {
                // 忽略关闭异常
            }

            if (restoreMainWindow)
            {
                RestoreMainWindowAfterRoiPreview();
            }
        }

        private void RestoreMainWindowAfterRoiPreview()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null || _roiPreviewPreviousMainWindowState is not { } previous)
            {
                _roiPreviewPreviousMainWindowState = null;
                return;
            }

            _roiPreviewPreviousMainWindowState = null;
            mainWindow.WindowState = previous;
            mainWindow.Activate();
        }

        private void RefreshMainWindowSizeDisplay()
        {
            MainWindowSizeDisplayText = _mainWindowRememberSize
                ? $"已启用沿用：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})"
                : $"未启用沿用（参数 {_mainWindowWidth}×{_mainWindowHeight}）；启动仍默认全屏";
        }

        /// <summary>按参数区宽高立即调整主窗（不强制改 Remember 开关）。</summary>
        private void ExecuteApplyMainWindowSize()
        {
            try
            {
                ApplyMainWindowBounds(
                    MainWindowWidth,
                    MainWindowHeight,
                    MainWindowLeft,
                    MainWindowTop);
                MessageShower = $"已应用窗体大小：{MainWindowWidth}×{MainWindowHeight}";
                AppendFlowLog(MessageShower);
            }
            catch (Exception ex)
            {
                MessageShower = $"应用窗体大小失败：{ex.Message}";
                _logger.Error("应用窗体大小失败", ex);
            }
        }

        /// <summary>读取当前主窗实际尺寸并持久化，下次进入本项目自动沿用。</summary>
        private void ExecuteSaveCurrentMainWindowSize()
        {
            try
            {
                if (!TryReadMainWindowBounds(out var width, out var height, out var left, out var top))
                {
                    MessageShower = "无法读取主窗尺寸";
                    return;
                }

                EnsureSettingExists(nameof(MainWindowRememberSize), 0, "int", nameof(MainWindowRememberSize));
                EnsureSettingExists(nameof(MainWindowWidth), 1280, "int", nameof(MainWindowWidth));
                EnsureSettingExists(nameof(MainWindowHeight), 720, "int", nameof(MainWindowHeight));
                EnsureSettingExists(nameof(MainWindowLeft), 80, "int", nameof(MainWindowLeft));
                EnsureSettingExists(nameof(MainWindowTop), 80, "int", nameof(MainWindowTop));

                _isLoadingSettings = true;
                _mainWindowWidth = Math.Max(800, width);
                _mainWindowHeight = Math.Max(450, height);
                _mainWindowLeft = left;
                _mainWindowTop = top;
                _mainWindowRememberSize = true;
                OnPropertyChanged(nameof(MainWindowWidth));
                OnPropertyChanged(nameof(MainWindowHeight));
                OnPropertyChanged(nameof(MainWindowLeft));
                OnPropertyChanged(nameof(MainWindowTop));
                OnPropertyChanged(nameof(MainWindowRememberSize));
                RefreshMainWindowSizeDisplay();
                _isLoadingSettings = false;

                SaveSetting(nameof(MainWindowWidth), _mainWindowWidth);
                SaveSetting(nameof(MainWindowHeight), _mainWindowHeight);
                SaveSetting(nameof(MainWindowLeft), _mainWindowLeft);
                SaveSetting(nameof(MainWindowTop), _mainWindowTop);
                SaveSetting(nameof(MainWindowRememberSize), 1);

                ApplyMainWindowBounds(_mainWindowWidth, _mainWindowHeight, _mainWindowLeft, _mainWindowTop);
                MessageShower =
                    $"已保存当前窗体大小：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})，下次启动本项目将沿用";
                AppendFlowLog(MessageShower);
                _logger.Info(MessageShower);
            }
            catch (Exception ex)
            {
                _isLoadingSettings = false;
                MessageShower = $"保存窗体大小失败：{ex.Message}";
                _logger.Error("保存窗体大小失败", ex);
            }
        }

        /// <summary>关闭自定义尺寸沿用，并恢复默认最大化。</summary>
        private void ExecuteRestoreDefaultMainWindowSize()
        {
            try
            {
                MainWindowRememberSize = false;
                TryRestoreDefaultMainWindowMaximized();
                MessageShower = "已恢复默认全屏，并关闭下次启动沿用";
                AppendFlowLog(MessageShower);
            }
            catch (Exception ex)
            {
                MessageShower = $"恢复默认全屏失败：{ex.Message}";
                _logger.Error("恢复默认全屏失败", ex);
            }
        }

        private void TryApplyRememberedMainWindowSize()
        {
            if (!_mainWindowRememberSize)
            {
                return;
            }

            try
            {
                ApplyMainWindowBounds(
                    _mainWindowWidth,
                    _mainWindowHeight,
                    _mainWindowLeft,
                    _mainWindowTop);
                _logger.Info(
                    $"已按项目设置恢复主窗：{_mainWindowWidth}×{_mainWindowHeight} @ ({_mainWindowLeft},{_mainWindowTop})");
            }
            catch (Exception ex)
            {
                _logger.Warn($"恢复主窗尺寸失败：{ex.Message}");
            }
        }

        private static void TryRestoreDefaultMainWindowMaximized()
        {
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
            {
                return;
            }

            mainWindow.WindowState = WindowState.Maximized;
        }

        private static bool TryReadMainWindowBounds(
            out int width,
            out int height,
            out int left,
            out int top)
        {
            width = 0;
            height = 0;
            left = 0;
            top = 0;
            var mainWindow = Application.Current?.MainWindow;
            if (mainWindow == null)
            {
                return false;
            }

            Rect bounds;
            if (mainWindow.WindowState == WindowState.Maximized
                || mainWindow.WindowState == WindowState.Minimized)
            {
                bounds = mainWindow.RestoreBounds;
                if (bounds.Width < 1 || bounds.Height < 1)
                {
                    bounds = new Rect(
                        mainWindow.Left,
                        mainWindow.Top,
                        mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width,
                        mainWindow.ActualHeight > 0 ? mainWindow.ActualHeight : mainWindow.Height);
                }
            }
            else
            {
                bounds = new Rect(
                    mainWindow.Left,
                    mainWindow.Top,
                    mainWindow.ActualWidth > 0 ? mainWindow.ActualWidth : mainWindow.Width,
                    mainWindow.ActualHeight > 0 ? mainWindow.ActualHeight : mainWindow.Height);
            }

            width = Math.Max(800, (int)Math.Round(bounds.Width));
            height = Math.Max(450, (int)Math.Round(bounds.Height));
            left = (int)Math.Round(bounds.Left);
            top = (int)Math.Round(bounds.Top);
            return true;
        }

        private static void ApplyMainWindowBounds(int width, int height, int left, int top)
        {
            var mainWindow = Application.Current?.MainWindow
                ?? throw new InvalidOperationException("主窗未就绪");

            var w = Math.Max(800, width);
            var h = Math.Max(450, height);

            // 先切 Normal，再设尺寸；避免 Maximized 下改 Width/Height 无效
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Width = w;
            mainWindow.Height = h;
            mainWindow.Left = left;
            mainWindow.Top = top;

            // 尽量落在当前屏幕工作区内，避免拖出屏幕
            try
            {
                var work = SystemParameters.WorkArea;
                if (mainWindow.Left + mainWindow.Width > work.Right)
                {
                    mainWindow.Left = Math.Max(work.Left, work.Right - mainWindow.Width);
                }

                if (mainWindow.Top + mainWindow.Height > work.Bottom)
                {
                    mainWindow.Top = Math.Max(work.Top, work.Bottom - mainWindow.Height);
                }

                if (mainWindow.Left < work.Left)
                {
                    mainWindow.Left = work.Left;
                }

                if (mainWindow.Top < work.Top)
                {
                    mainWindow.Top = work.Top;
                }
            }
            catch
            {
                // 忽略屏幕边界校正失败
            }

            mainWindow.Activate();
        }
    }
}
