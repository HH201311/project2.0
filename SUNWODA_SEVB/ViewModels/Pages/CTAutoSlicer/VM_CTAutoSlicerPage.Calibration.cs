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
    /// CT 自动化切片页面 ViewModel（partial：Calibration）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void RefreshSimple321ToolbarClickDisplay()
        {
            Simple321ToolbarClickDisplayText = _simple321ToolbarClickCalibrated
                ? $"X={_simple321ToolbarClickX}, Y={_simple321ToolbarClickY}"
                : "未标定";
        }

        private void RefreshSimple321OffsetEditDisplay()
        {
            Simple321OffsetEditDisplayText = _simple321OffsetEditCalibrated
                ? $"X={_simple321OffsetEditX}, Y={_simple321OffsetEditY}"
                : "未标定";
        }

        private void RefreshPostAlignCaptureButtonDisplay()
        {
            PostAlignCaptureButtonDisplayText = _postAlignCaptureButtonCalibrated
                ? $"X={_postAlignCaptureButtonX}, Y={_postAlignCaptureButtonY}（UIA 索引={_postAlignCaptureButtonIndex}）"
                : "未标定";
        }

        private void RefreshPostAlignDragDisplay()
        {
            if (!_postAlignDragCalibrated)
            {
                PostAlignDragDisplayText = "未标定";
                return;
            }

            var dx = _postAlignDragEndX - _postAlignDragStartX;
            var dy = _postAlignDragEndY - _postAlignDragStartY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            PostAlignDragDisplayText =
                $"({_postAlignDragStartX},{_postAlignDragStartY}) → ({_postAlignDragEndX},{_postAlignDragEndY}) Δ=({dx},{dy}) L={len:F0}px";
        }

        private void RefreshPre321ViewClickDisplay()
        {
            Pre321ViewClickDisplayText = _pre321ViewClickCalibrated
                ? $"X={_pre321ViewClickX}, Y={_pre321ViewClickY}"
                : "未标定";
        }

        private void RefreshPre321ViewDragDisplay()
        {
            if (!_pre321ViewDragCalibrated)
            {
                Pre321ViewDragDisplayText = "未标定";
                return;
            }

            var dx = _pre321ViewDragEndX - _pre321ViewDragStartX;
            var dy = _pre321ViewDragEndY - _pre321ViewDragStartY;
            var len = Math.Sqrt(dx * dx + dy * dy);
            Pre321ViewDragDisplayText =
                $"({_pre321ViewDragStartX},{_pre321ViewDragStartY}) → ({_pre321ViewDragEndX},{_pre321ViewDragEndY}) Δ=({dx},{dy}) L={len:F0}px";
        }

        private static string NormalizeMaskColor(string? value)
        {
            if (HistogramGrayScaleAnalyzer.TryParseHexColor(value, out var c))
            {
                return HistogramGrayScaleAnalyzer.ToHex(c);
            }

            return string.IsNullOrWhiteSpace(value) ? "#6E6E6E" : value.Trim();
        }

        private void RefreshMaskColorBrush()
        {
            if (HistogramGrayScaleAnalyzer.TryParseHexColor(_histogramMaskColor, out var c))
            {
                HistogramMaskColorBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(c.R, c.G, c.B));
            }
        }

        private void ExecutePickHistogramMaskColor(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再取色";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenColorPickerOverlayWindow();
                    var ok = picker.ShowDialog() == true;
                    if (!ok || string.IsNullOrWhiteSpace(picker.SelectedHex))
                    {
                        MessageShower = "已取消掩膜取色";
                        AppendFlowLog("直方图掩膜取色已取消");
                        return;
                    }

                    HistogramMaskColor = picker.SelectedHex;
                    var msg = $"直方图掩膜颜色已取色：{HistogramMaskColor}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"掩膜取色失败：{ex.Message}";
                AppendFlowLog(MessageShower);
                _logger.Error("直方图掩膜取色失败", ex);
            }
        }

        private void ExecuteCalibrateHistogramRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "直方图 ROI",
                windowTitle: "直方图 ROI 标定",
                hintText: "拖拽框选直方图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选直方图区域",
                applyRect: rect =>
                {
                    _histogramRoiX = rect.X;
                    _histogramRoiY = rect.Y;
                    _histogramRoiWidth = rect.Width;
                    _histogramRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(HistogramRoiX));
                    OnPropertyChanged(nameof(HistogramRoiY));
                    OnPropertyChanged(nameof(HistogramRoiWidth));
                    OnPropertyChanged(nameof(HistogramRoiHeight));
                    RefreshHistogramRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(HistogramRoiX), rect.X);
                    SaveSetting(nameof(HistogramRoiY), rect.Y);
                    SaveSetting(nameof(HistogramRoiWidth), rect.Width);
                    SaveSetting(nameof(HistogramRoiHeight), rect.Height);
                });
        }

        /// <summary>
        /// 标定 LUT 预设关键段两端点。CommandParameter：Positive=正极 / Negative=负极（默认）。
        /// 须先在 VG 中应用对应电极预设后再点选折线控制点。
        /// </summary>
        private void ExecuteCalibrateLutSegmentPoints(object? parameter)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            var isPositive = string.Equals(
                parameter?.ToString(),
                "Positive",
                StringComparison.OrdinalIgnoreCase);
            var polarityLabel = isPositive ? "正极" : "负极";
            var presetHint = isPositive
                ? RenderingPresetNamePositive
                : RenderingPresetNameNegative;

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenTwoPointPickerOverlayWindow(
                        $"第 1 步（{polarityLabel}/{presetHint}）：对准 LUT 关键段第一个控制点（空心圆），左键确认；右键/Esc 取消",
                        $"第 2 步（{polarityLabel}/{presetHint}）：对准同一关键段第二个控制点，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = $"已取消{polarityLabel} LUT 关键段两点标定";
                        AppendFlowLog($"{polarityLabel} LUT 关键段两点标定已取消");
                        return;
                    }

                    if (picker.Point1X == picker.Point2X && picker.Point1Y == picker.Point2Y)
                    {
                        MessageShower = "两端点重合，请重新标定两个不同的控制点";
                        AppendFlowLog($"{polarityLabel} LUT 关键段两点标定失败：两端点重合");
                        return;
                    }

                    PersistLutSegmentCalibration(isPositive, picker.Point1X, picker.Point1Y, picker.Point2X, picker.Point2Y);

                    var midX = (picker.Point1X + picker.Point2X) / 2;
                    var midY = (picker.Point1Y + picker.Point2Y) / 2;
                    var msg =
                        $"{polarityLabel} LUT 关键段两点已标定：P1=({picker.Point1X},{picker.Point1Y}) " +
                        $"P2=({picker.Point2X},{picker.Point2Y}) 中点=({midX},{midY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"{polarityLabel} LUT 关键段两点标定失败：{ex.Message}";
                _logger.Error($"{polarityLabel} LUT 关键段两点标定失败", ex);
            }
        }

        private void PersistLutSegmentCalibration(bool isPositive, int x1, int y1, int x2, int y2)
        {
            if (isPositive)
            {
                EnsureSettingExists(nameof(LutSegmentPoint1XPositive), 0, "int", nameof(LutSegmentPoint1XPositive));
                EnsureSettingExists(nameof(LutSegmentPoint1YPositive), 0, "int", nameof(LutSegmentPoint1YPositive));
                EnsureSettingExists(nameof(LutSegmentPoint2XPositive), 0, "int", nameof(LutSegmentPoint2XPositive));
                EnsureSettingExists(nameof(LutSegmentPoint2YPositive), 0, "int", nameof(LutSegmentPoint2YPositive));
                EnsureSettingExists(
                    nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedPositive),
                    0,
                    "int",
                    nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedPositive));

                _isLoadingSettings = true;
                _lutSegmentPoint1XPositive = x1;
                _lutSegmentPoint1YPositive = y1;
                _lutSegmentPoint2XPositive = x2;
                _lutSegmentPoint2YPositive = y2;
                _lutSegmentPointsCalibratedPositive = true;
                OnPropertyChanged(nameof(LutSegmentPoint1XPositive));
                OnPropertyChanged(nameof(LutSegmentPoint1YPositive));
                OnPropertyChanged(nameof(LutSegmentPoint2XPositive));
                OnPropertyChanged(nameof(LutSegmentPoint2YPositive));
                RefreshLutSegmentPointsDisplay();
                _isLoadingSettings = false;

                SaveSetting(nameof(LutSegmentPoint1XPositive), x1);
                SaveSetting(nameof(LutSegmentPoint1YPositive), y1);
                SaveSetting(nameof(LutSegmentPoint2XPositive), x2);
                SaveSetting(nameof(LutSegmentPoint2YPositive), y2);
                SaveSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedPositive), 1);
                return;
            }

            EnsureSettingExists(nameof(LutSegmentPoint1XNegative), 0, "int", nameof(LutSegmentPoint1XNegative));
            EnsureSettingExists(nameof(LutSegmentPoint1YNegative), 0, "int", nameof(LutSegmentPoint1YNegative));
            EnsureSettingExists(nameof(LutSegmentPoint2XNegative), 0, "int", nameof(LutSegmentPoint2XNegative));
            EnsureSettingExists(nameof(LutSegmentPoint2YNegative), 0, "int", nameof(LutSegmentPoint2YNegative));
            EnsureSettingExists(
                nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedNegative),
                0,
                "int",
                nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedNegative));

            _isLoadingSettings = true;
            _lutSegmentPoint1XNegative = x1;
            _lutSegmentPoint1YNegative = y1;
            _lutSegmentPoint2XNegative = x2;
            _lutSegmentPoint2YNegative = y2;
            _lutSegmentPointsCalibratedNegative = true;
            OnPropertyChanged(nameof(LutSegmentPoint1XNegative));
            OnPropertyChanged(nameof(LutSegmentPoint1YNegative));
            OnPropertyChanged(nameof(LutSegmentPoint2XNegative));
            OnPropertyChanged(nameof(LutSegmentPoint2YNegative));
            RefreshLutSegmentPointsDisplay();
            _isLoadingSettings = false;

            SaveSetting(nameof(LutSegmentPoint1XNegative), x1);
            SaveSetting(nameof(LutSegmentPoint1YNegative), y1);
            SaveSetting(nameof(LutSegmentPoint2XNegative), x2);
            SaveSetting(nameof(LutSegmentPoint2YNegative), y2);
            SaveSetting(nameof(CTAutoSlicerSettings.LutSegmentPointsCalibratedNegative), 1);
        }

        private void ExecuteCalibrateVision3dRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "3D 视图 ROI",
                windowTitle: "3D 视图 ROI 标定",
                hintText: "拖拽框选第四象限 3D 视图截图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选 3D 视图区域",
                applyRect: rect =>
                {
                    _vision3dRoiX = rect.X;
                    _vision3dRoiY = rect.Y;
                    _vision3dRoiWidth = rect.Width;
                    _vision3dRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(Vision3dRoiX));
                    OnPropertyChanged(nameof(Vision3dRoiY));
                    OnPropertyChanged(nameof(Vision3dRoiWidth));
                    OnPropertyChanged(nameof(Vision3dRoiHeight));
                    RefreshVision3dRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(Vision3dRoiX), rect.X);
                    SaveSetting(nameof(Vision3dRoiY), rect.Y);
                    SaveSetting(nameof(Vision3dRoiWidth), rect.Width);
                    SaveSetting(nameof(Vision3dRoiHeight), rect.Height);
                });
        }

        private void ExecuteCalibrateVisionTopViewRoi(object? _)
        {
            ExecuteCalibrateScreenRoi(
                regionName: "「上」视图 ROI",
                windowTitle: "「上」视图 ROI 标定",
                hintText: "拖拽框选第二象限「上」视图截图区域；框下方 ✓ 提交 / ✕ 重选；Esc 取消标定",
                confirmBarText: "确认框选「上」视图区域",
                applyRect: rect =>
                {
                    _visionTopViewRoiX = rect.X;
                    _visionTopViewRoiY = rect.Y;
                    _visionTopViewRoiWidth = rect.Width;
                    _visionTopViewRoiHeight = rect.Height;
                    OnPropertyChanged(nameof(VisionTopViewRoiX));
                    OnPropertyChanged(nameof(VisionTopViewRoiY));
                    OnPropertyChanged(nameof(VisionTopViewRoiWidth));
                    OnPropertyChanged(nameof(VisionTopViewRoiHeight));
                    RefreshVisionTopViewRoiDisplay();
                },
                persistRect: rect =>
                {
                    SaveSetting(nameof(VisionTopViewRoiX), rect.X);
                    SaveSetting(nameof(VisionTopViewRoiY), rect.Y);
                    SaveSetting(nameof(VisionTopViewRoiWidth), rect.Width);
                    SaveSetting(nameof(VisionTopViewRoiHeight), rect.Height);
                });
        }

        /// <summary>
        /// 标定测量工具栏「简单对齐」下拉的屏幕绝对点击位置（Qt 控件 UIA 不可靠）。
        /// </summary>
        private void ExecuteCalibrateSimple321ToolbarClick(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准 VG 测量工具栏「简单对齐」下拉按钮中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消「简单对齐」下拉标定";
                        AppendFlowLog("「简单对齐」下拉点击标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Simple321ToolbarClickX), 0, "int", nameof(Simple321ToolbarClickX));
                    EnsureSettingExists(nameof(Simple321ToolbarClickY), 0, "int", nameof(Simple321ToolbarClickY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated));

                    _isLoadingSettings = true;
                    _simple321ToolbarClickX = picker.SelectedX;
                    _simple321ToolbarClickY = picker.SelectedY;
                    _simple321ToolbarClickCalibrated = true;
                    OnPropertyChanged(nameof(Simple321ToolbarClickX));
                    OnPropertyChanged(nameof(Simple321ToolbarClickY));
                    RefreshSimple321ToolbarClickDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Simple321ToolbarClickX), picker.SelectedX);
                    SaveSetting(nameof(Simple321ToolbarClickY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Simple321ToolbarClickCalibrated), 1);

                    var msg =
                        $"「简单对齐」下拉已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"「简单对齐」下拉标定失败：{ex.Message}";
                _logger.Error("「简单对齐」下拉点击标定失败", ex);
            }
        }

        /// <summary>
        /// 标定三点定平面后、向量两点前左侧无名偏移 Edit（常显示「x.xx mm」）的点击位置。
        /// </summary>
        private void ExecuteCalibrateSimple321OffsetEdit(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请对准三点后左侧显示「x.xx mm」的无名编辑框中心，左键确认；右键/Esc 取消（置 0 后「上」视图才正确）");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消偏移 Edit 标定";
                        AppendFlowLog("三点后偏移 Edit 标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Simple321OffsetEditX), 0, "int", nameof(Simple321OffsetEditX));
                    EnsureSettingExists(nameof(Simple321OffsetEditY), 0, "int", nameof(Simple321OffsetEditY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated));

                    _isLoadingSettings = true;
                    _simple321OffsetEditX = picker.SelectedX;
                    _simple321OffsetEditY = picker.SelectedY;
                    _simple321OffsetEditCalibrated = true;
                    OnPropertyChanged(nameof(Simple321OffsetEditX));
                    OnPropertyChanged(nameof(Simple321OffsetEditY));
                    RefreshSimple321OffsetEditDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Simple321OffsetEditX), picker.SelectedX);
                    SaveSetting(nameof(Simple321OffsetEditY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Simple321OffsetEditCalibrated), 1);

                    var msg = $"偏移 Edit 已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"偏移 Edit 标定失败：{ex.Message}";
                _logger.Error("三点后偏移 Edit 标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3-2-1 完成后、导出前「3D图正视图切换按钮」的屏幕绝对点击位置（UIA 匹配 + 兜底坐标）。
        /// </summary>
        private void ExecuteCalibratePostAlignCaptureButton(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准 VG「3D图正视图切换按钮」中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消按钮位置标定";
                        AppendFlowLog("对齐后拖拽：按钮位置标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(PostAlignCaptureButtonX), 0, "int", nameof(PostAlignCaptureButtonX));
                    EnsureSettingExists(nameof(PostAlignCaptureButtonY), 0, "int", nameof(PostAlignCaptureButtonY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated));
                    EnsureSettingExists(nameof(PostAlignCaptureButtonIndex), 0, "int", nameof(PostAlignCaptureButtonIndex));

                    _isLoadingSettings = true;
                    _postAlignCaptureButtonX = picker.SelectedX;
                    _postAlignCaptureButtonY = picker.SelectedY;
                    _postAlignCaptureButtonCalibrated = true;
                    OnPropertyChanged(nameof(PostAlignCaptureButtonX));
                    OnPropertyChanged(nameof(PostAlignCaptureButtonY));
                    RefreshPostAlignCaptureButtonDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(PostAlignCaptureButtonX), picker.SelectedX);
                    SaveSetting(nameof(PostAlignCaptureButtonY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.PostAlignCaptureButtonCalibrated), 1);

                    var msg = $"3D图正视图切换按钮位置已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"按钮位置标定失败：{ex.Message}";
                _logger.Error("对齐后拖拽：按钮位置标定失败", ex);
            }
        }

        /// <summary>
        /// 标定点击 3D图正视图切换按钮后的鼠标拖拽方向与距离（全屏箭头）。
        /// </summary>
        private void ExecuteCalibratePostAlignDragArrow(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var overlay = new ScreenArrowCalibrationOverlayWindow(
                        "在 VG 上按住左键拖拽 3D 图：起点→终点；需拖至可同时看见3个面；Esc/右键取消");
                    var ok = overlay.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消拖拽箭头标定";
                        AppendFlowLog("对齐后拖拽箭头标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(PostAlignDragStartX), 0, "int", nameof(PostAlignDragStartX));
                    EnsureSettingExists(nameof(PostAlignDragStartY), 0, "int", nameof(PostAlignDragStartY));
                    EnsureSettingExists(nameof(PostAlignDragEndX), 0, "int", nameof(PostAlignDragEndX));
                    EnsureSettingExists(nameof(PostAlignDragEndY), 0, "int", nameof(PostAlignDragEndY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.PostAlignDragCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.PostAlignDragCalibrated));

                    _isLoadingSettings = true;
                    _postAlignDragStartX = overlay.StartX;
                    _postAlignDragStartY = overlay.StartY;
                    _postAlignDragEndX = overlay.EndX;
                    _postAlignDragEndY = overlay.EndY;
                    _postAlignDragCalibrated = true;
                    OnPropertyChanged(nameof(PostAlignDragStartX));
                    OnPropertyChanged(nameof(PostAlignDragStartY));
                    OnPropertyChanged(nameof(PostAlignDragEndX));
                    OnPropertyChanged(nameof(PostAlignDragEndY));
                    RefreshPostAlignDragDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(PostAlignDragStartX), overlay.StartX);
                    SaveSetting(nameof(PostAlignDragStartY), overlay.StartY);
                    SaveSetting(nameof(PostAlignDragEndX), overlay.EndX);
                    SaveSetting(nameof(PostAlignDragEndY), overlay.EndY);
                    SaveSetting(nameof(CTAutoSlicerSettings.PostAlignDragCalibrated), 1);

                    var msg =
                        $"拖拽箭头已标定：({overlay.StartX},{overlay.StartY}) → ({overlay.EndX},{overlay.EndY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"拖拽箭头标定失败：{ex.Message}";
                _logger.Error("对齐后拖拽箭头标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3D 找点前「切换视图」屏幕绝对点击位置。
        /// </summary>
        private void ExecuteCalibratePre321ViewClick(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var picker = new ScreenPointPickerOverlayWindow(
                        "请将十字准星对准「切换视图」按钮中心，左键确认；右键/Esc 取消");
                    var ok = picker.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消切换视图点击标定";
                        AppendFlowLog("3D找点前视角：切换视图点击标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Pre321ViewClickX), 0, "int", nameof(Pre321ViewClickX));
                    EnsureSettingExists(nameof(Pre321ViewClickY), 0, "int", nameof(Pre321ViewClickY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated));

                    _isLoadingSettings = true;
                    _pre321ViewClickX = picker.SelectedX;
                    _pre321ViewClickY = picker.SelectedY;
                    _pre321ViewClickCalibrated = true;
                    OnPropertyChanged(nameof(Pre321ViewClickX));
                    OnPropertyChanged(nameof(Pre321ViewClickY));
                    RefreshPre321ViewClickDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Pre321ViewClickX), picker.SelectedX);
                    SaveSetting(nameof(Pre321ViewClickY), picker.SelectedY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Pre321ViewClickCalibrated), 1);

                    var msg = $"切换视图点击已标定：X={picker.SelectedX}, Y={picker.SelectedY}";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"切换视图点击标定失败：{ex.Message}";
                _logger.Error("3D找点前视角：切换视图点击标定失败", ex);
            }
        }

        /// <summary>
        /// 标定 3D 找点前视角拖拽方向与距离（全屏箭头）。
        /// </summary>
        private void ExecuteCalibratePre321ViewDragArrow(object? _)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                try
                {
                    Thread.Sleep(250);
                    var overlay = new ScreenArrowCalibrationOverlayWindow(
                        "在 VG 上按住左键拖拽 3D 图：起点→终点；拖至可清晰看见上平面；Esc/右键取消");
                    var ok = overlay.ShowDialog() == true;
                    if (!ok)
                    {
                        MessageShower = "已取消找点前拖拽箭头标定";
                        AppendFlowLog("3D找点前视角：拖拽箭头标定已取消");
                        return;
                    }

                    EnsureSettingExists(nameof(Pre321ViewDragStartX), 0, "int", nameof(Pre321ViewDragStartX));
                    EnsureSettingExists(nameof(Pre321ViewDragStartY), 0, "int", nameof(Pre321ViewDragStartY));
                    EnsureSettingExists(nameof(Pre321ViewDragEndX), 0, "int", nameof(Pre321ViewDragEndX));
                    EnsureSettingExists(nameof(Pre321ViewDragEndY), 0, "int", nameof(Pre321ViewDragEndY));
                    EnsureSettingExists(
                        nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated),
                        0,
                        "int",
                        nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated));

                    _isLoadingSettings = true;
                    _pre321ViewDragStartX = overlay.StartX;
                    _pre321ViewDragStartY = overlay.StartY;
                    _pre321ViewDragEndX = overlay.EndX;
                    _pre321ViewDragEndY = overlay.EndY;
                    _pre321ViewDragCalibrated = true;
                    OnPropertyChanged(nameof(Pre321ViewDragStartX));
                    OnPropertyChanged(nameof(Pre321ViewDragStartY));
                    OnPropertyChanged(nameof(Pre321ViewDragEndX));
                    OnPropertyChanged(nameof(Pre321ViewDragEndY));
                    RefreshPre321ViewDragDisplay();
                    _isLoadingSettings = false;

                    SaveSetting(nameof(Pre321ViewDragStartX), overlay.StartX);
                    SaveSetting(nameof(Pre321ViewDragStartY), overlay.StartY);
                    SaveSetting(nameof(Pre321ViewDragEndX), overlay.EndX);
                    SaveSetting(nameof(Pre321ViewDragEndY), overlay.EndY);
                    SaveSetting(nameof(CTAutoSlicerSettings.Pre321ViewDragCalibrated), 1);

                    var msg =
                        $"找点前拖拽箭头已标定：({overlay.StartX},{overlay.StartY}) → ({overlay.EndX},{overlay.EndY})";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"找点前拖拽箭头标定失败：{ex.Message}";
                _logger.Error("3D找点前视角：拖拽箭头标定失败", ex);
            }
        }

        /// <summary>
        /// 通用屏幕 ROI 框选标定：最小化上位机 → 全屏遮罩拖拽 → 写入 DB。
        /// 标定得到的 (X,Y) 为截图区域左上角物理像素，供 DLL 相对坐标换算：screen = ROI起点 + 相对点。
        /// </summary>
        private void ExecuteCalibrateScreenRoi(
            string regionName,
            string windowTitle,
            string hintText,
            string confirmBarText,
            Action<System.Drawing.Rectangle> applyRect,
            Action<System.Drawing.Rectangle> persistRect)
        {
            if (_ctAutoSlicerService.IsRunning)
            {
                MessageShower = "流程运行中，请先结束后再标定";
                return;
            }

            try
            {
                // 标定需手动框选，必须解除 BlockInput
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current.MainWindow;
                WindowState? previousState = null;
                if (mainWindow != null)
                {
                    previousState = mainWindow.WindowState;
                    mainWindow.WindowState = WindowState.Minimized;
                }

                HistogramRoiCalibrationOverlayWindow? overlay = null;
                try
                {
                    // 稍等主窗口最小化完成，避免遮罩盖住上位机而非 VG
                    Thread.Sleep(250);
                    overlay = new HistogramRoiCalibrationOverlayWindow(windowTitle, hintText, confirmBarText);
                    var ok = overlay.ShowDialog() == true;
                    if (!ok || overlay.SelectedScreenRect is not { } rect)
                    {
                        MessageShower = $"已取消{regionName}标定";
                        AppendFlowLog($"{regionName}标定已取消");
                        return;
                    }

                    _isLoadingSettings = true;
                    applyRect(rect);
                    _isLoadingSettings = false;
                    persistRect(rect);

                    var msg =
                        $"{regionName}已标定：X={rect.X}, Y={rect.Y}, W={rect.Width}, H={rect.Height}（起点用于相对坐标换算）";
                    MessageShower = msg;
                    AppendFlowLog(msg);
                    _logger.Info(msg);
                }
                finally
                {
                    if (mainWindow != null)
                    {
                        mainWindow.WindowState = previousState ?? WindowState.Maximized;
                        mainWindow.Activate();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageShower = $"{regionName}标定失败：{ex.Message}";
                AppendFlowLog(MessageShower);
                _logger.Error($"{regionName}标定失败", ex);
            }
        }
    }
}
