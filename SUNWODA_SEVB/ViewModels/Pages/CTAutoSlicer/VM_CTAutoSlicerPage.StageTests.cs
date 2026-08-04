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
    /// CT 自动化切片页面 ViewModel（partial：StageTests）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void OpenHistogramCaptureDir(object? _)
        {
            var initial = HistogramCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "histogram");
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                HistogramCaptureDir = dialog.FileName;
            }
        }

        private void OpenVisionCaptureDir(object? _)
        {
            var initial = VisionCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionCaptureDir = dialog.FileName;
            }
        }

        private void OpenVision2dVectorCaptureDir(object? _)
        {
            var initial = Vision2dVectorCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVision2dVectorCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                Vision2dVectorCaptureDir = dialog.FileName;
            }
        }

        private void OpenVision3dInitialCaptureDir(object? _)
        {
            var initial = Vision3dInitialCaptureDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVision3dInitialCaptureDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "选择 3D 初始原图保存文件夹（未切视角）",
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                Vision3dInitialCaptureDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：截取 3D 标定图保存目录选择。</summary>
        private void BrowseVisionLearn3dSampleDir(object? _)
        {
            var initial = VisionLearn3dSampleDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionLearn3dSampleDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：截取上视图标定图保存目录选择。</summary>
        private void BrowseVisionLearnTopViewSampleDir(object? _)
        {
            var initial = VisionLearnTopViewSampleDir;
            if (string.IsNullOrWhiteSpace(initial))
            {
                initial = CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir;
            }

            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                InitialDirectory = Directory.Exists(initial) ? initial : null,
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && !string.IsNullOrWhiteSpace(dialog.FileName))
            {
                VisionLearnTopViewSampleDir = dialog.FileName;
            }
        }

        /// <summary>单步测试：缩小本软件后按 3D ROI 截图。</summary>
        private void ExecuteCaptureVision3dSample(object? _)
        {
            if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
            {
                MessageShower = "尚未标定 3D 区域，请先标定后再截图";
                return;
            }

            var saveDir = string.IsNullOrWhiteSpace(VisionLearn3dSampleDir)
                ? CTAutoSlicerSettings.DefaultVisionLearn3dSampleDir
                : VisionLearn3dSampleDir.Trim();

            CaptureSimpleRoiSample(
                "3D 标定图",
                saveDir,
                "vision_3d",
                _vision3dRoiX,
                _vision3dRoiY,
                _vision3dRoiWidth,
                _vision3dRoiHeight);
        }

        /// <summary>单步测试：缩小本软件后按「上」视图 ROI 截图。</summary>
        private void ExecuteCaptureVisionTopViewSample(object? _)
        {
            if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
            {
                MessageShower = "尚未标定「上」视图区域，请先标定后再截图";
                return;
            }

            var saveDir = string.IsNullOrWhiteSpace(VisionLearnTopViewSampleDir)
                ? CTAutoSlicerSettings.DefaultVisionLearnTopViewSampleDir
                : VisionLearnTopViewSampleDir.Trim();

            CaptureSimpleRoiSample(
                "上视图标定图",
                saveDir,
                "vision_top",
                _visionTopViewRoiX,
                _visionTopViewRoiY,
                _visionTopViewRoiWidth,
                _visionTopViewRoiHeight);
        }

        /// <summary>
        /// 基础截图：最小化本软件 → 按 ROI 截图保存；截图后保持最小化。
        /// </summary>
        private void CaptureSimpleRoiSample(
            string regionName,
            string saveDir,
            string filePrefix,
            int x,
            int y,
            int width,
            int height)
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.WindowState = WindowState.Minimized;
                }

                Thread.Sleep(400);
                Directory.CreateDirectory(saveDir);
                var filePath = Path.Combine(saveDir, $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
                ScreenCaptureHelper.CaptureRegionAndSave(filePath, x, y, width, height);

                // 截图后保持最小化，方便连续采图；用户自行还原窗口
                var msg = $"{regionName}已保存：{filePath}";
                MessageShower = msg;
                AppendFlowLog(msg);
                _logger.Info(msg);
            }
            catch (Exception ex)
            {
                MessageShower = $"{regionName}截取失败：{ex.Message}";
                _logger.Error($"{regionName}临时截图失败", ex);
            }
        }

        private async Task ExecuteTestRenderingPresetAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(RenderingPresetName))
            {
                MessageShower = "请先填写渲染预设名称";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 渲染预设单步测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService
                    .TestRenderingPresetAsync(BuildSettings(), flowToken, progress);
                AppendFlowLog("——— 渲染预设单步测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"渲染预设测试失败：{ex.Message}");
                _logger.Error("渲染预设单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestThickPlateAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 厚板三象限测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestThickPlateAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 厚板三象限测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"厚板测试失败：{ex.Message}");
                _logger.Error("厚板三象限测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestSliceExportAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置切片导出目录";
                return;
            }

            if (string.IsNullOrWhiteSpace(CTFileFolder) || !Directory.Exists(CTFileFolder))
            {
                MessageShower = "请先选择有效的 CT 体素文件夹（用于命名规则）";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 切片导出三视图测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestSliceExportAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 切片导出三视图测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"切片导出测试失败：{ex.Message}");
                _logger.Error("切片导出单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestHistogramGrayScaleAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (_histogramRoiWidth <= 0 || _histogramRoiHeight <= 0)
            {
                MessageShower = "请先点击「标定直方图区域」框选直方图 ROI";
                return;
            }

            if (!_lutSegmentPointsCalibrated
                || (_lutSegmentPoint1X == _lutSegmentPoint2X && _lutSegmentPoint1Y == _lutSegmentPoint2Y))
            {
                MessageShower = "请先在「直方图标定」中点击「标定 LUT 关键段两点」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 直方图灰度调整测试开始（拖红线 + LUT中点对齐） ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestHistogramGrayScaleAdjustAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 直方图灰度调整测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"直方图灰度测试失败：{ex.Message}");
                _logger.Error("直方图灰度单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestSimple321AlignmentAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (_vision3dRoiWidth <= 0 || _vision3dRoiHeight <= 0)
            {
                MessageShower = "尚未标定 3D 视图区域，请先在「阶段四视觉对齐」中标定 3D 区域";
                return;
            }

            if (_visionTopViewRoiWidth <= 0 || _visionTopViewRoiHeight <= 0)
            {
                MessageShower = "尚未标定「上」视图区域，请先在「阶段四视觉对齐」中标定「上」视图（向量两点需要）";
                return;
            }

            if (!_simple321ToolbarClickCalibrated)
            {
                MessageShower = "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框";
                return;
            }

            if (!_simple321OffsetEditCalibrated)
            {
                MessageShower = "尚未标定三点后偏移 Edit，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框";
                return;
            }

            // 找点算法由 Simple321PointSource 配置（本地 / 视觉 DLL）

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                var sourceLabel = Simple321PointSourceDisplay;
                AppendFlowLog(
                    $"——— 简单 3-2-1 对齐测试开始（三点→偏移置0→向量两点→原点） ———");
                AppendFlowLog($"【找点算法】当前调用：{sourceLabel}");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestSimple321AlignmentAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 简单 3-2-1 对齐测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"简单 3-2-1 对齐测试失败：{ex.Message}");
                _logger.Error("简单 3-2-1 对齐测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        /// <summary>
        /// 打开本地 3D 斜俯视找点分步调试窗：对 vision_3d 截图逐步执行算法并显示中间图。
        /// </summary>
        private void ExecuteOpenVision3dPointsStepDebug()
        {
            string? initial = null;
            try
            {
                var root = string.IsNullOrWhiteSpace(VisionCaptureDir)
                    ? CTAutoSlicerSettings.DefaultVisionCaptureDir
                    : VisionCaptureDir.Trim();
                if (Directory.Exists(root))
                {
                    var latest = Directory.EnumerateFiles(root, "vision_3d_*.png", SearchOption.AllDirectories)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                    initial = latest;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"查找最新 vision_3d 截图失败：{ex.Message}");
            }

            var win = new Vision3dPointsStepDebugWindow(initial)
            {
                Owner = Application.Current?.MainWindow,
            };
            win.Show();
            MessageShower = initial == null
                ? "已打开本地 3D 找点分步调试（请自行浏览截图）"
                : $"已打开本地 3D 找点分步调试：{Path.GetFileName(initial)}";
        }

        private async Task ExecuteTestPostAlignCaptureDragAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (!_postAlignCaptureButtonCalibrated)
            {
                MessageShower = "尚未标定 3D图正视图切换按钮位置，请先在「对齐后拖拽」中点击「标定按钮位置」";
                return;
            }

            if (!_postAlignDragCalibrated
                || (_postAlignDragStartX == _postAlignDragEndX && _postAlignDragStartY == _postAlignDragEndY))
            {
                MessageShower = "尚未标定拖拽箭头，请先「标定拖拽箭头」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 对齐后拖拽测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestPostAlignCaptureDragAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 对齐后拖拽测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"对齐后拖拽测试失败：{ex.Message}");
                _logger.Error("对齐后拖拽测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestPre321ViewPrepAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (!_pre321ViewClickCalibrated)
            {
                MessageShower = "尚未标定切换视图点击，请先在「视觉对齐」中点击「标定切换视图点击」";
                return;
            }

            if (!_pre321ViewDragCalibrated
                || (_pre321ViewDragStartX == _pre321ViewDragEndX && _pre321ViewDragStartY == _pre321ViewDragEndY))
            {
                MessageShower = "尚未标定找点前拖拽箭头，请先「标定拖拽箭头」";
                return;
            }

            if (string.IsNullOrWhiteSpace(VGAppPath))
            {
                MessageShower = "请先配置 VG 应用路径";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— 临时·3D找点前视角准备测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestPre321ViewPrepAsync(
                    BuildSettings(),
                    flowToken,
                    progress);
                AppendFlowLog("——— 临时·3D找点前视角准备测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"3D找点前视角准备测试失败：{ex.Message}");
                _logger.Error("3D找点前视角准备测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() => IsEnableSimulationBtn = true);
            }
        }

        private async Task ExecuteTestOfflineCtVisionAnnotationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            string? explicitImage = null;
            var pick = MessageBox.Show(
                "是 = 手动选择一张图片测试\n否 = 按切片导出目录规则取「上」视图中间图\n取消 = 退出",
                "AI 图像标注测试",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (pick == MessageBoxResult.Cancel)
            {
                return;
            }

            if (pick == MessageBoxResult.Yes)
            {
                var dialog = new OpenFileDialog
                {
                    Title = "选择 AI 标注输入图",
                    Filter = "图像 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
                    CheckFileExists = true,
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                explicitImage = dialog.FileName;
            }
            else if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置切片导出目录，或改为手动选图测试";
                return;
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 图像标注测试开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestOfflineCtVisionAnnotationAsync(
                    BuildSettings(),
                    flowToken,
                    progress,
                    explicitImage);
                AppendFlowLog("——— AI 图像标注测试结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"AI 图像标注测试失败：{ex.Message}");
                _logger.Error("AI 图像标注单步测试失败", ex);
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    IsEnableSimulationBtn = true;
                    RefreshLatestAiAnnotationPreview();
                });
            }
        }
    }
}
