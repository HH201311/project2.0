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

        /// <summary>
        /// 用户/流程期望是否锁定键鼠。为 false 时 <see cref="Lock"/> 不再 BlockInput，
        /// 便于 Alt+O 解锁后自动化短暂 Unlock/Lock 不会再次锁死。
        /// </summary>
        private volatile bool _inputBlockEnabled = true;

        /// <summary>当前是否处于「期望锁定键鼠」状态（Alt+O 可切换）。</summary>
        public bool IsInputBlockEnabled => _inputBlockEnabled;

        public event EventHandler? InputBlockStateChanged;

        /// <summary>
        /// 流程开始时调用：默认重新开启键鼠锁定并立即 BlockInput。
        /// </summary>
        public void PrepareInputBlockForRun()
        {
            _inputBlockEnabled = true;
            ForceBlockInput(true);
            RaiseInputBlockStateChanged();
            _logger.Info("流程开始：已默认锁定键鼠（Alt+O 可解锁）");
        }

        /// <summary>
        /// Alt+O：切换键鼠锁定期望；立即生效。返回切换后是否锁定。
        /// </summary>
        public bool ToggleInputBlock()
        {
            _inputBlockEnabled = !_inputBlockEnabled;
            ForceBlockInput(_inputBlockEnabled);
            RaiseInputBlockStateChanged();
            _logger.Info(
                _inputBlockEnabled
                    ? "Alt+O：已锁定键鼠"
                    : "Alt+O：已解锁键鼠（自动化后续 Lock 将跳过，直至再次 Alt+O 或重新开始流程）");
            return _inputBlockEnabled;
        }

        /// <summary>流程结束/中断：强制解除 BlockInput（不改变「开始默认锁定」策略）。</summary>
        public void ReleaseInputBlock()
        {
            ForceBlockInput(false);
            RaiseInputBlockStateChanged();
        }

        public void Lock()
        {
            if (!_inputBlockEnabled)
            {
                return;
            }

            ForceBlockInput(true);
        }

        public void Unlock()
        {
            ForceBlockInput(false);
        }

        private void ForceBlockInput(bool block)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                WinAPIHelper.BlockInput(block);
                return;
            }

            if (dispatcher.CheckAccess())
            {
                WinAPIHelper.BlockInput(block);
            }
            else
            {
                dispatcher.Invoke(() => WinAPIHelper.BlockInput(block));
            }
        }

        private void RaiseInputBlockStateChanged()
        {
            try
            {
                InputBlockStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // 订阅方异常不影响自动化
            }
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
