using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using SharpHook;
using SharpHook.Native;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public partial class VGAutomationService
    {
        // 公开 API：直方图截图 / 附着进程 / Lock / UI 线程
        public Task<HistogramCaptureResult> CaptureHistogramRoiAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null,
            string? namingSourcePath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!settings.HasHistogramRoi)
            {
                throw new InvalidOperationException("尚未标定直方图截图区域");
            }

            if (!HistogramGrayScaleAnalyzer.TryParseHexColor(settings.HistogramMaskColor, out var maskColor))
            {
                throw new InvalidOperationException(
                    $"直方图掩膜颜色无效「{settings.HistogramMaskColor}」，请取色或填写 #RRGGBB");
            }

            var roiX = settings.HistogramRoiX;
            var roiY = settings.HistogramRoiY;
            var roiW = settings.HistogramRoiWidth;
            var roiH = settings.HistogramRoiHeight;

            var dir = settings.EnsureHistogramCaptureDayDir();
            var cleanup = settings.CleanupExpiredHistogramCaptures();
            if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
            {
                progress?.Report(
                    $"已清理过期直方图截图：目录 {cleanup.DeletedDirs} 个，散落文件 {cleanup.DeletedFiles} 个" +
                    $"（保留 {settings.HistogramCaptureKeepDays} 天）");
                _logger.Info(
                    $"直方图截图清理：deletedDirs={cleanup.DeletedDirs}, deletedFiles={cleanup.DeletedFiles}, " +
                    $"keepDays={settings.HistogramCaptureKeepDays}");
            }

            var productTag = SliceExportNamingHelper.BuildCaptureProductTag(namingSourcePath);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(dir, $"histogram_{productTag}_{stamp}.png");
            var comparePath = Path.Combine(dir, $"histogram_compare_{productTag}_{stamp}.png");
            var peaksPath = Path.Combine(dir, $"histogram_peaks_{productTag}_{stamp}.png");

            progress?.Report(
                $"截取直方图 ROI ({roiX},{roiY},{roiW}x{roiH}) → {dir}（产品={productTag}）");
            _logger.Info($"截取直方图 ROI → {filePath}（产品标签={productTag}）");

            ScreenCaptureHelper.CaptureRegionAndSave(filePath, roiX, roiY, roiW, roiH);

            int? redColumn;
            HistogramOptimizeResult optimize;
            using (var bmp = new System.Drawing.Bitmap(filePath))
            {
                redColumn = HistogramRedLineDetector.FindRedLineColumn(bmp);
                progress?.Report(
                    $"颜色替换：{HistogramGrayScaleAnalyzer.ToHex(maskColor)} → 纯绿，容差 {settings.HistogramMaskTolerance}");
                optimize = HistogramGrayScaleAnalyzer.Optimize(
                    bmp,
                    maskColor,
                    settings.HistogramMaskTolerance,
                    comparePath,
                    peaksPath);
                _logger.Info(
                    $"直方图掩膜完成：raw={optimize.RawMaskPixelCount}, cleaned={optimize.MaskPixelCount}, " +
                    $"peaksOk={optimize.Peaks.Success}, compare={optimize.CompareImagePath}");
            }

            int? redScreenX = redColumn.HasValue ? roiX + redColumn.Value : null;
            var dragY = roiY + Math.Max(1, roiH / 2);
            int? targetScreenX = optimize.Peaks.Success
                ? roiX + optimize.Peaks.TargetX
                : null;

            return Task.FromResult(new HistogramCaptureResult(
                filePath,
                roiX,
                roiY,
                roiW,
                roiH,
                redColumn,
                redScreenX,
                dragY,
                optimize.CompareImagePath,
                optimize.PeaksImagePath,
                optimize.MaskPixelCount,
                optimize.Peaks.Success,
                optimize.Peaks.FailReason,
                optimize.Peaks.LeftPeakX,
                optimize.Peaks.ValleyX,
                optimize.Peaks.RightPeakX,
                optimize.Peaks.TargetX,
                targetScreenX));
        }

        public async Task<(Process Process, bool IsColdStart)> GetCTViewerProcess(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken = default)
        {
            var processName = Path.GetFileNameWithoutExtension(settings.VGAppPath);
            if (string.IsNullOrWhiteSpace(processName))
            {
                throw new InvalidOperationException("无法从 VG 应用路径解析进程名");
            }

            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                return (processes[0], IsColdStart: false);
            }

            var started = Process.Start(new ProcessStartInfo(settings.VGAppPath)
            {
                UseShellExecute = true
            }) ?? throw new InvalidOperationException($"无法启动 VG 应用：{settings.VGAppPath}");
            return (started, IsColdStart: true);
        }

        public void Lock()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                WinAPIHelper.BlockInput(true);
            });
        }

        public void Unlock()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                WinAPIHelper.BlockInput(false);
            });
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            return Application.Current.Dispatcher.InvokeAsync(action).Task;
        }

        private async Task InvokeOnVgUiThreadAsync(Process ctViewerProcess, Action action)
        {
            Unlock();
            try
            {
                EnsureVgForeground(ctViewerProcess);
                await Task.Delay(80).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(action).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private static Task<T> InvokeOnUiThreadAsync<T>(Func<T> func)
        {
            return Application.Current.Dispatcher.InvokeAsync(func).Task;
        }
    }
}
