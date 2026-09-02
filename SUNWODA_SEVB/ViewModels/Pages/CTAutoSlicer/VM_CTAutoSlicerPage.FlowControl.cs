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
    /// CT 自动化切片页面 ViewModel（partial：FlowControl）。
    /// </summary>
    public partial class VM_CTAutoSlicerPage
    {
        private void HandleSimulationProgress(string message)
        {
            try
            {
                if (message.StartsWith("@@SLICE_QTY@@", StringComparison.Ordinal))
                {
                    var qtyText = message["@@SLICE_QTY@@".Length..];
                    if (int.TryParse(qtyText, out var quantity))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            SetSliceExportQuantityRuntime(quantity);
                            MessageShower = $"检测到 QJ 全局图像，导出数量已同步为 {quantity}";
                        });
                    }

                    return;
                }

                if (string.Equals(message, "@@MANUAL_INTERVENE_END@@", StringComparison.Ordinal))
                {
                    Dispatcher.Invoke(HideManualInterventionPanel);
                    return;
                }

                if (message.StartsWith("@@MANUAL_INTERVENE_VISION@@", StringComparison.Ordinal))
                {
                    var payload = message["@@MANUAL_INTERVENE_VISION@@".Length..];
                    var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                    var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                    var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                    Dispatcher.Invoke(() => ShowManualInterventionPanel(fileName, reason, isVisionFailure: true));
                    return;
                }

                if (message.StartsWith("@@MANUAL_INTERVENE@@", StringComparison.Ordinal))
                {
                    var payload = message["@@MANUAL_INTERVENE@@".Length..];
                    var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                    var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                    var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                    Dispatcher.Invoke(() => ShowManualInterventionPanel(fileName, reason, isVisionFailure: false));
                    return;
                }

                if (string.Equals(message, "@@PRE_EXPORT_BRIGHTNESS_CHECK_END@@", StringComparison.Ordinal))
                {
                    Dispatcher.Invoke(HidePreExportBrightnessCheckOverlay);
                    return;
                }

                if (string.Equals(message, "@@PRE_EXPORT_BRIGHTNESS_CHECK@@", StringComparison.Ordinal))
                {
                    Dispatcher.Invoke(ShowPreExportBrightnessCheckOverlay);
                    return;
                }

                if (message.StartsWith("@@IMPORT_FAIL@@", StringComparison.Ordinal))
                {
                    var payload = message["@@IMPORT_FAIL@@".Length..];
                    var separatorIndex = payload.IndexOf('|', StringComparison.Ordinal);
                    var fileName = separatorIndex >= 0 ? payload[..separatorIndex] : payload;
                    var reason = separatorIndex >= 0 ? payload[(separatorIndex + 1)..] : "未知原因";
                    Dispatcher.Invoke(() =>
                    {
                        MessageShower = $"导入失败：{fileName}（等待人工处理）";
                        AppendFlowLog($"{fileName} — {reason}", isFailure: true);
                    });
                    return;
                }

                // 单张结果图已落盘：立刻刷新「最新 AI 标注预览」，不必等整批结束
                if (message.StartsWith("@@AI_RESULT_PREVIEW@@", StringComparison.Ordinal))
                {
                    var previewPath = message["@@AI_RESULT_PREVIEW@@".Length..].Trim();
                    Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            ApplyAiAnnotationPreviewPath(previewPath, allowClearWhenMissing: false);
                            if (!string.IsNullOrWhiteSpace(previewPath) && File.Exists(previewPath))
                            {
                                MessageShower = $"已更新预览：{Path.GetFileName(previewPath)}";
                                AppendFlowLog($"预览已更新：{previewPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"AI 结果预览即时刷新失败：{ex.Message}");
                        }
                    });
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    MessageShower = message;
                    AppendFlowLog(message);
                });
            }
            catch (Exception ex)
            {
                // Progress 回调异常若上抛，可能打断后台流程；仅记日志
                _logger.Warn($"流程进度回调异常（已忽略）：{ex.Message}");
            }
        }

        private void ShowManualInterventionPanel(string fileName, string reason, bool isVisionFailure)
        {
            ManualInterventionFileName = string.IsNullOrWhiteSpace(fileName) ? "(未知文件)" : fileName;
            ManualInterventionReason = string.IsNullOrWhiteSpace(reason) ? "流程异常或超时" : reason;
            IsVisionManualIntervention = isVisionFailure;
            ManualInterventionInstructionText = isVisionFailure
                ? "视觉算法调用异常，自动化已暂停。请在 VG 中人工完成「简单 3-2-1 对齐」，确认无误后点击下方「人工处理完成」跳过视觉校验并继续后续步骤；若无法恢复则放入异常文件夹并跳过。"
                : "自动化已暂停。请检查 VG / 现场状态，确认后点击下方按钮将该文件放入异常文件夹并跳过，继续下一文件。";
            MessageShower = isVisionFailure
                ? $"视觉算法异常，请人工 321 对齐后跳过视觉校验：{ManualInterventionFileName}"
                : $"需要人工处理：{ManualInterventionFileName}";
            AppendFlowLog($"{MessageShower} — {ManualInterventionReason}", isFailure: true);

            _manualInterventionDeadlineUtc =
                DateTime.UtcNow + CTAutoSlicerSettings.DefaultManualInterventionTimeout;
            UpdateManualInterventionCountdownText();
            IsManualInterventionVisible = true;

            // 关键：把本软件从 VG 后面拉到前台，否则操作员看不到干预面板
            BringAutomationAppToForeground();

            _manualInterventionCountdownTimer?.Stop();
            _manualInterventionCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _manualInterventionCountdownTimer.Tick += (_, _) => UpdateManualInterventionCountdownText();
            _manualInterventionCountdownTimer.Start();
        }

        /// <summary>
        /// 人工干预时强制置前主窗口（恢复最小化 + Topmost 脉冲 + Win32 ForceForeground）。
        /// </summary>
        private void BringAutomationAppToForeground()
        {
            try
            {
                WinAPIHelper.BlockInput(false);

                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    return;
                }

                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }

                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }

                mainWindow.Activate();
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
                mainWindow.Focus();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(mainWindow).EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    WinAPIHelper.ForceForegroundWindow(hwnd);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"人工干预置前本软件失败（忽略）：{ex.Message}");
            }
        }

        private void HideManualInterventionPanel()
        {
            _manualInterventionCountdownTimer?.Stop();
            _manualInterventionCountdownTimer = null;
            IsManualInterventionVisible = false;
            IsVisionManualIntervention = false;
            ManualInterventionCountdownText = string.Empty;
            ManualInterventionInstructionText = string.Empty;
        }

        /// <summary>
        /// 导出前亮度校验遮罩：置前本软件，提示人工检查 VG 亮度；
        /// 与 Alt+P 暂停面板无关（不改 <see cref="IsFlowPaused"/>）。
        /// </summary>
        private void ShowPreExportBrightnessCheckOverlay()
        {
            MessageShower = "导出前亮度校验：请人工检查当前亮度状态是否合适";
            AppendFlowLog("导出前亮度校验：等待操作员确认（与 Alt+P 暂停相互独立）");
            IsPreExportBrightnessCheckVisible = true;
            BringAutomationAppToForeground();
        }

        private void HidePreExportBrightnessCheckOverlay()
        {
            IsPreExportBrightnessCheckVisible = false;
        }

        /// <summary>「校验完毕」：关闭遮罩 → 缩小本软件 → 放行导出门闩。</summary>
        private void ExecuteConfirmPreExportBrightnessCheck(object? _)
        {
            try
            {
                AppendFlowLog("操作员点击「校验完毕」，缩小窗口并继续切片导出");
                MessageShower = "亮度校验完毕，缩小窗口并继续导出…";
                HidePreExportBrightnessCheckOverlay();
                MinimizeAutomationAppForExport();
                _ctAutoSlicerService.ConfirmPreExportBrightnessCheck();
            }
            catch (Exception ex)
            {
                _logger.Error("确认导出前亮度校验失败", ex);
                MessageShower = $"确认失败：{ex.Message}";
            }
        }

        /// <summary>校验通过后缩小本软件，避免遮挡 VG 导出自动化。</summary>
        private void MinimizeAutomationAppForExport()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow == null)
                {
                    return;
                }

                mainWindow.WindowState = WindowState.Minimized;
            }
            catch (Exception ex)
            {
                _logger.Warn($"导出前缩小本软件失败（忽略）：{ex.Message}");
            }
        }

        private void UpdateManualInterventionCountdownText()
        {
            var remain = _manualInterventionDeadlineUtc - DateTime.UtcNow;
            if (remain < TimeSpan.Zero)
            {
                remain = TimeSpan.Zero;
            }

            ManualInterventionCountdownText =
                $"剩余等待 {remain.Hours:00}:{remain.Minutes:00}:{remain.Seconds:00}"
                + "（超时将关闭 VG，归档失败文件并继续下一文件）";
        }

        private void ExecuteConfirmManualIntervention(object? _)
        {
            try
            {
                AppendFlowLog("操作员点击「放入异常文件夹并跳过」");
                MessageShower = "已放入异常文件夹并跳过，继续流程…";
                _ctAutoSlicerService.ConfirmManualIntervention();
            }
            catch (Exception ex)
            {
                _logger.Error("确认人工干预失败", ex);
                MessageShower = $"确认失败：{ex.Message}";
            }
        }

        private void ExecuteConfirmManualInterventionRetry(object? _)
        {
            try
            {
                AppendFlowLog("操作员点击「人工处理完成，跳过视觉校验」");
                MessageShower = "人工 321 对齐已确认，跳过视觉校验，继续后续步骤…";
                _ctAutoSlicerService.ConfirmManualInterventionRetry();
            }
            catch (Exception ex)
            {
                _logger.Error("确认跳过视觉校验失败", ex);
                MessageShower = $"确认失败：{ex.Message}";
            }
        }

        private void AppendFlowLog(string message, bool isFailure = false)
        {
            if (!isFailure && message == _lastFlowLogMessage)
            {
                return;
            }

            _lastFlowLogMessage = message;
            var prefix = isFailure ? "失败" : "信息";
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {prefix}：{message}";
            var lines = string.IsNullOrWhiteSpace(ImportLogText)
                ? new List<string>()
                : ImportLogText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();

            lines.Add(line);
            if (lines.Count > 200)
            {
                lines = lines.Skip(lines.Count - 200).ToList();
            }

            ImportLogText = string.Join(Environment.NewLine, lines);
            WriteFlowLogToFile(line);
            _logger.Info($"[CT导入] {message}");

            if (!_isUserScrolledUp)
            {
                ImportLogScrollToEndRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ClearImportLog(object? _)
        {
            ImportLogText = string.Empty;
            _lastFlowLogMessage = null;
        }

        private static void WriteFlowLogToFile(string line)
        {
            var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "CTAutoSlicer");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "import_log.txt");
            File.AppendAllText(logPath, line + Environment.NewLine);
        }

        private CancellationToken BeginActiveFlow()
        {
            _activeFlowCts?.Cancel();
            _activeFlowCts?.Dispose();
            _activeFlowCts = new CancellationTokenSource();
            return _activeFlowCts.Token;
        }

        private async Task HandleFlowCanceledAsync()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                HidePreExportBrightnessCheckOverlay();
                MessageShower = "流程已取消";
                AppendFlowLog("流程已取消");
            });
            _logger.Info("CT 切片流程已取消");
        }

        private void InterruptActiveFlow(string trigger)
        {
            _ctAutoSlicerService.Cancel();
            _activeFlowCts?.Cancel();
            Dispatcher.Invoke(() =>
            {
                WinAPIHelper.BlockInput(false);
                IsFlowPaused = false;
                RefreshInputBlockStatus();
                HideManualInterventionPanel();
                HidePreExportBrightnessCheckOverlay();
                IsEnableSimulationBtn = true;
                MessageShower = $"流程已通过 {trigger} 中断";
                AppendFlowLog($"流程已通过 {trigger} 中断");
            });
        }

        private void OnAltXPressed()
        {
            InterruptActiveFlow("Alt+X");
        }

        private void OnAltPPressed()
        {
            if (!_ctAutoSlicerService.IsRunning)
            {
                Dispatcher.Invoke(() => MessageShower = "当前没有运行中的流程，Alt+P 无效");
                return;
            }

            // 导出前亮度校验与 Alt+P 暂停是两套功能：校验遮罩期间不切换协作式暂停，
            // 避免操作员误以为 Alt+P 能关掉遮罩，或留下暂停态导致校验后仍卡住。
            if (_ctAutoSlicerService.IsAwaitingPreExportBrightnessCheck)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageShower =
                        "当前为导出前亮度校验（非 Alt+P 暂停），请检查 VG 亮度后点击「校验完毕」";
                    AppendFlowLog("Alt+P 已忽略：当前处于导出前亮度校验，请点击「校验完毕」");
                });
                return;
            }

            var paused = _ctAutoSlicerService.TogglePause();
            Dispatcher.Invoke(() =>
            {
                IsFlowPaused = paused;
                if (paused)
                {
                    MessageShower = "暂停中 — 再按 Alt+P 继续";
                    AppendFlowLog("流程已暂停（Alt+P）");
                }
                else
                {
                    MessageShower = "流程已继续（Alt+P）";
                    AppendFlowLog("流程已继续（Alt+P）");
                }

                RefreshInputBlockStatus();
            });
        }

        private void OnAltOPressed()
        {
            var locked = _ctAutoSlicerService.ToggleInputBlock();
            Dispatcher.Invoke(() =>
            {
                RefreshInputBlockStatus();
                MessageShower = locked
                    ? "键鼠已锁定（Alt+O 可解锁）"
                    : "键鼠已解锁（Alt+O 可重新锁定）";
                AppendFlowLog(locked ? "Alt+O：已锁定键鼠" : "Alt+O：已解锁键鼠");
            });
        }

        private void RefreshInputBlockStatus()
        {
            IsInputBlockEnabled = _ctAutoSlicerService.IsInputBlockEnabled;
        }
    }
}
