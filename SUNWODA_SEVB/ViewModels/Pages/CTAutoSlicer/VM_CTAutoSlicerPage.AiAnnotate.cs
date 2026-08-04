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
    /// CT 自动化切片页面 ViewModel（partial：AiAnnotate）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void RefreshLatestAiAnnotationPreview()
        {
            try
            {
                var path = OfflineCtAnnotationHelper.TryFindLatestAiResultImage(SliceExportFolder);
                ApplyAiAnnotationPreviewPath(path, allowClearWhenMissing: true);
            }
            catch (Exception ex)
            {
                LatestAiAnnotationPreview = null;
                LatestAiAnnotationPreviewPath = string.Empty;
                LatestAiAnnotationPreviewPixelWidth = 1;
                LatestAiAnnotationPreviewPixelHeight = 1;
                LatestAiAnnotationPreviewInfo = $"预览加载失败：{ex.Message}";
                LatestAiAnnotationSummaryText = string.Empty;
                OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
                OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
                _logger.Warn($"最新 AI 标注预览加载失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 用指定结果图路径立即更新预览（批处理逐张推送）；路径无效时按 <paramref name="allowClearWhenMissing"/> 决定是否清空。
        /// </summary>
        private void ApplyAiAnnotationPreviewPath(string? path, bool allowClearWhenMissing)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (!allowClearWhenMissing)
                {
                    return;
                }

                LatestAiAnnotationPreview = null;
                LatestAiAnnotationPreviewPath = string.Empty;
                LatestAiAnnotationPreviewPixelWidth = 1;
                LatestAiAnnotationPreviewPixelHeight = 1;
                LatestAiAnnotationPreviewInfo = string.IsNullOrWhiteSpace(SliceExportFolder)
                    ? "请先配置标注结果根目录，并完成 AI 标注"
                    : "暂无 AI 标注结果图（请先「开始流程」生成标注）";
                LatestAiAnnotationSummaryText = string.Empty;
                OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
                OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
                return;
            }

            var source = LoadAiAnnotationPreviewBitmap(path);
            LatestAiAnnotationPreview = source;
            LatestAiAnnotationPreviewPath = path;
            LatestAiAnnotationPreviewPixelWidth = Math.Max(1, source.PixelWidth);
            LatestAiAnnotationPreviewPixelHeight = Math.Max(1, source.PixelHeight);
            var time = File.GetLastWriteTime(path);
            LatestAiAnnotationPreviewInfo =
                $"{Path.GetFileName(path)}  ·  {source.PixelWidth}×{source.PixelHeight}  ·  {time:yyyy-MM-dd HH:mm:ss}";
            LatestAiAnnotationSummaryText = OfflineCtAnnotationHelper.TryBuildAiDimensionSummaryText(
                path,
                OfflineCtPixelSizeUm);
            OnPropertyChanged(nameof(HasLatestAiAnnotationPreview));
            OnPropertyChanged(nameof(HasLatestAiAnnotationSummary));
        }

        /// <summary>
        /// 加载预览图：按像素尺寸显示，并将 DPI 归一为 96，避免元数据导致宽高比失真。
        /// </summary>
        private static BitmapSource LoadAiAnnotationPreviewBitmap(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();

            if (Math.Abs(bmp.DpiX - 96.0) < 0.01
                && Math.Abs(bmp.DpiY - 96.0) < 0.01)
            {
                return bmp;
            }

            var stride = (bmp.PixelWidth * bmp.Format.BitsPerPixel + 31) / 32 * 4;
            var pixels = new byte[stride * bmp.PixelHeight];
            bmp.CopyPixels(pixels, stride, 0);
            var normalized = BitmapSource.Create(
                bmp.PixelWidth,
                bmp.PixelHeight,
                96,
                96,
                bmp.Format,
                bmp.Palette,
                pixels,
                stride);
            normalized.Freeze();
            return normalized;
        }

        /// <summary>
        /// 二次手动标注：与「标注结果根目录」(SliceExportFolder) 同步；
        /// 有路径则直开，无路径则弹窗自选并写回结果根目录。
        /// </summary>
        private void OpenManualMeasureFolder(object? _)
        {
            string? root = null;
            if (!string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                root = SliceExportFolder.Trim();
                if (!Directory.Exists(root))
                {
                    MessageBox.Show(
                        $"标注结果根目录不存在：\n{root}\n\n请在「AI图像标注」中重新选择，或清空后自选。",
                        "二次手动标注",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                var dialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "选择标注结果根目录（二次手动标注将打开此文件夹）",
                    InitialDirectory = Directory.Exists(@"D:\3Ddeliever\output")
                        ? @"D:\3Ddeliever\output"
                        : null,
                };

                if (dialog.ShowDialog() != CommonFileDialogResult.Ok
                    || string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    return;
                }

                root = dialog.FileName;
                SliceExportFolder = root;
            }

            var images = ManualMeasureExportHelper.EnumerateImagesRecursive(root);
            if (images.Count == 0)
            {
                MessageBox.Show(
                    "所选文件夹（含子目录）未找到图片（jpg/png/bmp/tif）。",
                    "二次手动标注",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var win = new ManualMeasureAnnotateWindow(
                root,
                OfflineCtPixelSizeUm,
                OfflineCtBlackHeight)
            {
                Owner = Application.Current?.MainWindow,
            };
            win.ShowDialog();
            MessageShower = $"二次手动标注已结束：{root}（共 {images.Count} 张）";
            RefreshLatestAiAnnotationPreview();
        }

        private void EnsureStatsDemoInitialized()
        {
            if (_statsDemoInitialized)
            {
                return;
            }

            RefreshStatsDemoData(randomize: false);
            _statsDemoInitialized = true;
        }

        /// <summary>
        /// 刷新检测统计 Demo 假数据（后续可替换为真实 OK/NG 判定落库查询）。
        /// </summary>
        private void RefreshStatsDemoData(bool randomize)
        {
            var today = DateTime.Today;
            StatsDemoDateText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            int total;
            int ok;
            int ng;
            int[] hourlyOk;
            int[] hourlyNg;

            if (randomize)
            {
                var rng = new Random();
                total = rng.Next(80, 180);
                ng = rng.Next(5, Math.Max(6, total / 8));
                ok = Math.Max(0, total - ng);
                hourlyOk = new int[8];
                hourlyNg = new int[8];
                var remainOk = ok;
                var remainNg = ng;
                for (var i = 0; i < 8; i++)
                {
                    var last = i == 7;
                    hourlyOk[i] = last ? remainOk : rng.Next(0, remainOk + 1);
                    hourlyNg[i] = last ? remainNg : rng.Next(0, remainNg + 1);
                    remainOk -= hourlyOk[i];
                    remainNg -= hourlyNg[i];
                }
            }
            else
            {
                // 固定种子 Demo，便于首次打开对照
                total = 128;
                ok = 115;
                ng = 13;
                hourlyOk = [8, 12, 15, 18, 16, 14, 17, 15];
                hourlyNg = [1, 2, 1, 2, 1, 2, 2, 2];
            }

            StatsTodayTotal = total;
            StatsTodayOk = ok;
            StatsTodayNg = ng;
            StatsDemoNote = randomize
                ? $"已刷新 Demo 假数据（{DateTime.Now:HH:mm:ss}）。真实检测判定接入后将改为读库。"
                : "当前为 Demo 假数据，尚未接入真实检测判定。";

            StatsOkNgPieModel = BuildOkNgPieModel(ok, ng);
            StatsHourlyBarModel = BuildHourlyBarModel(hourlyOk, hourlyNg);

            if (randomize)
            {
                MessageShower = $"统计 Demo 已刷新：今日 {total}（OK {ok} / NG {ng}）";
            }
        }

        private static PlotModel BuildOkNgPieModel(int ok, int ng)
        {
            var model = new PlotModel
            {
                Title = "当日 OK / NG 占比",
                TitleFontSize = 14,
                PlotMargins = new OxyThickness(8, 8, 8, 8),
            };
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendPlacement = LegendPlacement.Outside,
            });

            var pie = new PieSeries
            {
                StrokeThickness = 1,
                Stroke = OxyColors.White,
                InsideLabelFormat = "{1}",
                OutsideLabelFormat = "{0}: {1} ({2:0.0}%)",
                AngleSpan = 360,
                StartAngle = 90,
                FontSize = 12,
            };

            if (ok + ng <= 0)
            {
                pie.Slices.Add(new PieSlice("无数据", 1) { Fill = OxyColors.LightGray });
            }
            else
            {
                if (ok > 0)
                {
                    pie.Slices.Add(new PieSlice("OK", ok)
                    {
                        Fill = OxyColor.FromRgb(0x2E, 0xA8, 0x5A),
                    });
                }

                if (ng > 0)
                {
                    pie.Slices.Add(new PieSlice("NG", ng)
                    {
                        Fill = OxyColor.FromRgb(0xD3, 0x2F, 0x2F),
                    });
                }
            }

            model.Series.Add(pie);
            return model;
        }

        private static PlotModel BuildHourlyBarModel(IReadOnlyList<int> hourlyOk, IReadOnlyList<int> hourlyNg)
        {
            var model = new PlotModel
            {
                Title = "当日分时段产量（Demo）",
                TitleFontSize = 14,
                PlotMargins = new OxyThickness(56, 8, 16, 16),
            };
            model.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.TopRight,
                LegendPlacement = LegendPlacement.Inside,
            });

            // OxyPlot 2.x：BarSeries 为水平条；类别轴放左侧
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "时段",
                FontSize = 11,
                GapWidth = 0.25,
            };
            var nowHour = DateTime.Now.Hour;
            var startHour = Math.Max(0, nowHour - 7);
            for (var i = 0; i < 8; i++)
            {
                var h = startHour + i;
                categoryAxis.Labels.Add($"{h:00}:00");
            }

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "数量",
                MinimumPadding = 0.05,
                AbsoluteMinimum = 0,
                FontSize = 11,
            };

            var okSeries = new BarSeries
            {
                Title = "OK",
                FillColor = OxyColor.FromRgb(0x2E, 0xA8, 0x5A),
                StrokeThickness = 0,
                IsStacked = true,
            };
            var ngSeries = new BarSeries
            {
                Title = "NG",
                FillColor = OxyColor.FromRgb(0xD3, 0x2F, 0x2F),
                StrokeThickness = 0,
                IsStacked = true,
            };

            var n = Math.Min(8, Math.Min(hourlyOk.Count, hourlyNg.Count));
            for (var i = 0; i < n; i++)
            {
                okSeries.Items.Add(new BarItem(hourlyOk[i]));
                ngSeries.Items.Add(new BarItem(hourlyNg[i]));
            }

            model.Axes.Add(categoryAxis);
            model.Axes.Add(valueAxis);
            model.Series.Add(okSeries);
            model.Series.Add(ngSeries);
            return model;
        }

        private bool IsNavAllowedForCurrentPipeline(int navIndex) =>
            navIndex switch
            {
                NavOverview or NavPreferences or NavStepTest => true,
                NavAiAnnotate or NavManualMeasure or NavStats => IsAiPipelineMode,
                NavApp or NavImport or NavRender or NavHistogram or NavVision or NavPostAlign or NavSlice
                    => IsSlicePipelineMode,
                _ => true,
            };

        private void EnsureNavValidForPipelineMode()
        {
            if (!IsNavAllowedForCurrentPipeline(SelectedNavIndex))
            {
                // AI 模式回落带预览的主界面；切图模式回落总览
                SelectedNavIndex = IsAiPipelineMode ? NavManualMeasure : NavOverview;
            }
        }

        private void ExecuteBrowseOfflineCtModelPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择离线 CT ONNX 模型",
                Filter = "ONNX 模型 (*.onnx)|*.onnx|所有文件 (*.*)|*.*",
                CheckFileExists = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtModelPath))
            {
                try
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(OfflineCtModelPath);
                    dialog.FileName = Path.GetFileName(OfflineCtModelPath);
                }
                catch
                {
                    // ignore
                }
            }

            if (dialog.ShowDialog() == true)
            {
                OfflineCtModelPath = dialog.FileName;
            }
        }

        private void ExecuteResetOfflineCtModelPath()
        {
            OfflineCtModelPath = string.Empty;
            MessageShower = $"已恢复默认模型路径：{CTAutoSlicerSettings.GetDefaultOfflineCtModelPath()}";
        }

        private void ExecuteBrowseOfflineCtManualInputImage()
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择 AI 标注输入图片",
                Filter = "图像 (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath))
            {
                var dir = Path.GetDirectoryName(OfflineCtManualInputImagePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                    dialog.FileName = Path.GetFileName(OfflineCtManualInputImagePath);
                }
            }

            if (dialog.ShowDialog() == true)
            {
                OfflineCtManualInputImagePath = dialog.FileName;
            }
        }

        private void ExecuteBrowseOfflineCtManualOutputFolder()
        {
            using var dialog = new CommonOpenFileDialog
            {
                Title = "选择 AI 标注输出文件夹（可留空则输出到输入图目录）",
                IsFolderPicker = true,
            };
            if (!string.IsNullOrWhiteSpace(OfflineCtManualOutputFolder)
                && Directory.Exists(OfflineCtManualOutputFolder))
            {
                dialog.InitialDirectory = OfflineCtManualOutputFolder;
            }
            else if (!string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath))
            {
                var dir = Path.GetDirectoryName(OfflineCtManualInputImagePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    dialog.InitialDirectory = dir;
                }
            }

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                OfflineCtManualOutputFolder = dialog.FileName;
            }
        }

        /// <summary>AI 标注配置页：按指定输入图独立执行标注。</summary>
        private async Task ExecuteRunOfflineCtManualAnnotationAsync()
        {
            if (!IsEnableSimulationBtn || _ctAutoSlicerService.IsRunning)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(OfflineCtManualInputImagePath)
                || !File.Exists(OfflineCtManualInputImagePath))
            {
                MessageShower = "请先选择有效的输入图片路径";
                return;
            }

            var outputFolder = string.IsNullOrWhiteSpace(OfflineCtManualOutputFolder)
                ? null
                : OfflineCtManualOutputFolder.Trim();
            if (outputFolder != null && !Directory.Exists(outputFolder))
            {
                try
                {
                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    MessageShower = $"无法创建输出目录：{ex.Message}";
                    return;
                }
            }

            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 独立标注开始 ———");
                AppendFlowLog($"输入图：{OfflineCtManualInputImagePath}");
                AppendFlowLog(
                    string.IsNullOrWhiteSpace(outputFolder)
                        ? "输出目录：默认（输入图所在文件夹）"
                        : $"输出目录：{outputFolder}");

                var progress = new Progress<string>(HandleSimulationProgress);
                var flowToken = BeginActiveFlow();
                await _ctAutoSlicerService.TestOfflineCtVisionAnnotationAsync(
                    BuildSettings(),
                    flowToken,
                    progress,
                    OfflineCtManualInputImagePath.Trim(),
                    outputFolder);
                AppendFlowLog("——— AI 独立标注结束 ———");
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                MessageShower = ex.Message;
                AppendFlowLog($"AI 独立标注失败：{ex.Message}");
                _logger.Error("AI 独立标注失败", ex);
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

        private async Task ExecuteAiAnnotationBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(TopViewHandoffFolder) || !Directory.Exists(TopViewHandoffFolder))
            {
                MessageShower = "请先选择有效的上视图交接目录";
                return;
            }

            if (string.IsNullOrWhiteSpace(SliceExportFolder))
            {
                MessageShower = "请先配置标注结果根目录";
                return;
            }

            var images = OfflineCtAnnotationHelper.EnumerateHandoffImages(TopViewHandoffFolder);
            if (images.Count == 0)
            {
                MessageShower = $"交接目录内未找到可标注图像：{TopViewHandoffFolder}";
                return;
            }

            var flowToken = BeginActiveFlow();
            try
            {
                IsEnableSimulationBtn = false;
                _lastFlowLogMessage = null;
                AppendFlowLog("——— AI 标注批次开始 ———");
                var progress = new Progress<string>(HandleSimulationProgress);
                await _ctAutoSlicerService
                    .RunAiAnnotationBatchAsync(BuildSettings(), flowToken, progress);
            }
            catch (OperationCanceledException)
            {
                await HandleFlowCanceledAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => MessageShower = ex.Message);
                _logger.Error("AI 标注批处理异常", ex);
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
