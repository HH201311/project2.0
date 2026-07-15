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
    public class VGAutomationService
    {
        private const string ImportFailProgressPrefix = "@@IMPORT_FAIL@@";
        private const string SaveDialogWindowName = "VGSTUDIO MAX 问题";
        private const string SaveDialogNoButtonName = "否(N)";
        private const string AutoSaveRecoveryDialogWindowName = "自动保存恢复";
        private const string AutoSaveRecoverySkipButtonName = "跳过";
        private const int AutoSaveRecoveryScanIntervalMs = 300;
        /// <summary>VG 启动后「自动保存恢复」弹窗可能延迟出现，持续扫描并点击「跳过」的上限（毫秒）</summary>
        private const int AutoSaveRecoveryDismissTimeoutMs = 15000;
        private const string QuickImportDialogNamePrefix = "快速导入";
        private const string QuickImportEditName = "文件名(N):";
        private const string QuickImportOpenButtonName = "打开(O)";
        private const string QuickImportOpenButtonNameFallback = "打开";
        private const string FileMenuItemName = "文件(F)";
        private const int VgReadyPollIntervalMs = 1000;
        private const string QuickImportMenuItemKeyword = "快速导入";
        private const string FileSelectionFinishButtonName = "完成(F)";
        private const string ImportProgressWindowName = "请耐心等待...";
        private const string RenderingPanelWindowName = "渲染";
        private const string ThickPlateOkButtonName = "确定";
        private const string SliceExportDialogNamePrefix = "保存";
        private const string SliceExport2DViewGroupName = "2D 视图";
        private const string SliceExport2DViewComboName = "2D 视图";
        private const string SliceExportImageSizeGroupName = "图像大小";
        private const string SliceExportStackSettingsGroupName = "堆栈设置";
        private const string SliceExportSliceRangeGroupName = "切片图范围";
        private const string SliceExportQuantityLabelKeyword = "切片图数量";
        private const string SliceExportRangeStartLabelKeyword = "开始";
        private const string SliceExportRangeEndLabelKeyword = "结束";
        private const string SliceExportSaveButtonName = "保存...";
        private const string SliceExportJpegQualityWindowName = "JPEG 质量";
        private const string SliceExportQualityConfirmButtonName = "确定";
        private const string SliceExportWriteProgressText = "正在写入图像";
        private const string SliceExportQtyProgressPrefix = "@@SLICE_QTY@@";
        private const string SystemSaveFileEditName = "文件名:";

        private const int UiPollIntervalMs = UIAutomationHelper.DefaultConditionPollIntervalMs;
        private const int OffscreenProgressBypassMs = 2000;
        private const int ProcessMainWindowTimeoutMs = 10000;

        private readonly ILoggerService<VGAutomationService> _logger;
        private readonly IAngleAlgorithmService _angleAlgorithmService;
        private readonly EventSimulator _simulator;

        public VGAutomationService(
            ILoggerService<VGAutomationService> logger,
            IAngleAlgorithmService angleAlgorithmService)
        {
            _logger = logger;
            _angleAlgorithmService = angleAlgorithmService;
            _simulator = new EventSimulator();
        }

        private static Task<bool> WaitUntilAsync(
            Func<bool> condition,
            int timeoutMs,
            CancellationToken cancellationToken,
            int pollIntervalMs = UiPollIntervalMs) =>
            UIAutomationHelper.WaitUntilAsync(
                condition,
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken,
                pollIntervalMs);

        private static Task<bool> WaitUntilDisappearedAsync(
            Func<AutomationElement?> findElement,
            int timeoutMs,
            CancellationToken cancellationToken,
            int pollIntervalMs = UiPollIntervalMs) =>
            UIAutomationHelper.WaitUntilDisappearedAsync(
                findElement,
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken,
                pollIntervalMs);

        private static bool TryIsFileMenuReady(Process ctViewerProcess) =>
            UIAutomationHelper.FindFileMenuForProcess(ctViewerProcess, FileMenuItemName) != null;

        private static bool IsRenderingWorkspaceReady(AutomationElement mainWindow, CTAutoSlicerSettings settings)
        {
            if (!IsVolumeImportReady(mainWindow, settings))
            {
                return false;
            }

            var renderingPanel = FindRenderingPanel(mainWindow);
            if (renderingPanel == null)
            {
                return false;
            }

            var comboBox = FindRenderingPresetComboBox(renderingPanel);
            return comboBox is { Current.IsEnabled: true };
        }

        private Task WaitForProcessMainWindowAsync(Process process, CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () =>
                {
                    process.Refresh();
                    return process.MainWindowHandle != IntPtr.Zero;
                },
                ProcessMainWindowTimeoutMs,
                cancellationToken);

        private Task<bool> WaitForFileMenuReadyAsync(
            Process ctViewerProcess,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(() => TryIsFileMenuReady(ctViewerProcess), timeoutMs, cancellationToken);

        private Task<bool> WaitForRenderingWorkspaceReadyAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () =>
                {
                    try
                    {
                        return IsRenderingWorkspaceReady(GetFreshMainWindow(ctViewerProcess), settings);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeoutMs,
                cancellationToken);

        private Task<bool> WaitForRenderingWorkspaceReadyAsync(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(() => IsRenderingWorkspaceReady(mainWindow, settings), timeoutMs, cancellationToken);

        private Task<bool> WaitForPresetAppliedAsync(
            AutomationElement comboBox,
            string presetName,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(() => IsPresetApplied(comboBox, presetName), timeoutMs, cancellationToken);

        private Task<bool> WaitForComboDropdownOpenAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                timeoutMs,
                cancellationToken);

        private Task<bool> WaitForViewportQuadrantsReadyAsync(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int minCount,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => UIAutomationHelper.FindViewportQuadrants(
                    mainWindow,
                    settings.ViewportMinWidth,
                    settings.ViewportMinHeight).Count >= minCount,
                timeoutMs,
                cancellationToken);

        private static AutomationElement? FindSavePromptDialog(AutomationElement mainWindow) =>
            UIAutomationHelper.FindWindowByName(mainWindow, SaveDialogWindowName)
            ?? UIAutomationHelper.FindDescendantByName(
                AutomationElement.RootElement,
                ControlType.Window,
                SaveDialogWindowName);

        public async Task RunSimulationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (!settings.IsInputComplete())
            {
                progress?.Report("参数未填写完整");
                _logger.Info("参数未填写完整");
                return;
            }

            progress?.Report("流程开始");
            _logger.Info("流程开始");

            progress?.Report("连接/启动 VG 应用...");
            var ctViewerProcess = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
            await WaitForProcessMainWindowAsync(ctViewerProcess, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report("检查「自动保存恢复」弹窗...");
            await DismissAutoSaveRecoveryDialogAsync(ctViewerProcess, cancellationToken, progress)
                .ConfigureAwait(false);

            progress?.Report("VG 主窗口置前...");
            _logger.Info("VG 主窗口置前");
            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);

            Lock();
            try
            {
                await EnsureVgReadyAsync(ctViewerProcess, settings, cancellationToken, progress).ConfigureAwait(false);
                await DismissAutoSaveRecoveryDialogAsync(ctViewerProcess, cancellationToken, progress)
                    .ConfigureAwait(false);
                await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);

                var vglFiles = GetVglFiles(settings.CTFileFolder);
                if (vglFiles.Count == 0)
                {
                    progress?.Report("未找到 .vgl 文件");
                    _logger.Info("未找到 .vgl 文件");
                    return;
                }

                progress?.Report($"找到 {vglFiles.Count} 个 .vgl 文件");
                _logger.Info($"找到 {vglFiles.Count} 个 .vgl 文件");

                var successCount = 0;
                var failCount = 0;

                for (var index = 0; index < vglFiles.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var vglPath = vglFiles[index];
                    var fileName = Path.GetFileName(vglPath);
                    progress?.Report($"正在导入 {index + 1}/{vglFiles.Count}：{fileName}");
                    _logger.Info($"开始导入 {index + 1}/{vglFiles.Count}：{vglPath}");

                    try
                    {
                        await ImportSingleVglAsync(
                                settings,
                                ctViewerProcess,
                                vglPath,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false);

                        successCount++;
                        await OnImportSucceededAsync(vglPath).ConfigureAwait(false);
                        progress?.Report($"导入完成 {index + 1}/{vglFiles.Count}：{fileName}");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        var reason = GetFullExceptionMessage(ex);
                        progress?.Report($"{ImportFailProgressPrefix}{fileName}|{reason}");
                        _logger.Error($"导入失败：{vglPath}", ex);
                    }
                }

                // 阶段三（厚板）之后尚有对齐、切片等步骤待开发；批次结束暂不 Ctrl+N 清空，
                // 以便在 VG 中保留当前工程状态，便于联调后续功能。全流程完成后恢复。
                progress?.Report("批次结束（暂不清空 VG，保留当前工程以便继续开发后续步骤）");
                _logger.Info("批次结束（暂不清空 VG，保留当前工程以便继续开发后续步骤）");
                // progress?.Report("批次结束，清空 VG");
                // _logger.Info("批次结束，清空 VG");
                // await PrepareNewProjectAsync(
                //         settings,
                //         GetFreshMainWindow(ctViewerProcess),
                //         cancellationToken,
                //         progress)
                //     .ConfigureAwait(false);

                progress?.Report($"批次结束：成功 {successCount} 个，失败 {failCount} 个，共 {vglFiles.Count} 个");
                _logger.Info($"批次结束：成功 {successCount} 个，失败 {failCount} 个，共 {vglFiles.Count} 个");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"{ex.Message}");
                _logger.Error("CT 导入流程异常", ex);
            }
            finally
            {
                try
                {
                    EnsureVgForeground(ctViewerProcess);
                }
                catch (Exception ex)
                {
                    _logger.Debug($"流程结束恢复 VG 焦点失败：{ex.Message}");
                }

                Unlock();
            }
        }

        private async Task ImportSingleVglAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
            var mainWindow = GetFreshMainWindow(ctViewerProcess);
            await PrepareNewProjectAsync(settings, ctViewerProcess, mainWindow, cancellationToken, progress).ConfigureAwait(false);
            mainWindow = GetFreshMainWindow(ctViewerProcess);
            await OpenQuickImportAsync(settings, ctViewerProcess, cancellationToken, progress).ConfigureAwait(false);
            await QuickImportFillAndOpenAsync(settings, ctViewerProcess, vglPath, cancellationToken, progress)
                .ConfigureAwait(false);
            await ConfirmFileSelectionAsync(settings, ctViewerProcess, vglPath, cancellationToken, progress)
                .ConfigureAwait(false);
            await WaitForImportCompleteAsync(settings, ctViewerProcess, cancellationToken, progress)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(settings.RenderingPresetName))
            {
                mainWindow = GetFreshMainWindow(ctViewerProcess);
                await ApplyRenderingPresetAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);
            }

            mainWindow = GetFreshMainWindow(ctViewerProcess);
            await ApplyThickPlateForQuadrantsAsync(
                    settings,
                    ctViewerProcess,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await ExportImageStackSlicesAsync(
                    settings,
                    ctViewerProcess,
                    vglPath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        private async Task EnsureVgReadyAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var waitMs = ResolveVgOpenWaitTime(settings, progress);
            var deadline = DateTime.UtcNow.AddMilliseconds(waitMs);
            var startUtc = DateTime.UtcNow;

            progress?.Report(
                $"等待 VG 启动完成：每秒检测「文件(F)」菜单是否可点击，最大等待 {waitMs}ms");
            _logger.Info($"等待 VG 启动完成，最大等待 {waitMs}ms，轮询间隔 {VgReadyPollIntervalMs}ms");
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureVgForeground(ctViewerProcess);
                await TryDismissAutoSaveRecoveryDialogAsync(cancellationToken, progress)
                    .ConfigureAwait(false);
                var fileMenu = UIAutomationHelper.FindFileMenuForProcess(ctViewerProcess, FileMenuItemName);
                if (fileMenu != null)
                {
                    var elapsedMs = (int)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                    progress?.Report(
                        $"VG 已就绪（「文件(F)」菜单可点击，耗时 {elapsedMs}ms），开始导入流程");
                    _logger.Info($"VG 已就绪，文件菜单可交互，耗时 {elapsedMs}ms");
                    return;
                }

                var waitedMs = (int)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                progress?.Report($"VG 仍在加载中（已等待 {waitedMs}/{waitMs}ms），继续检测...");
                await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"VG 在 {waitMs}ms 内未完成启动（未检测到可点击的「文件(F)」菜单），请增大「VG启动就绪最大等待」后重试");
        }

        private int ResolveVgOpenWaitTime(CTAutoSlicerSettings settings, IProgress<string>? progress)
        {
            if (settings.VGOpenWaitTime <= CTAutoSlicerSettings.LegacyVGOpenWaitTimeThreshold)
            {
                progress?.Report(
                    $"数据库 VG启动就绪最大等待 仍为 {settings.VGOpenWaitTime}ms（旧值过短），已按 {CTAutoSlicerSettings.DefaultVGOpenWaitTime}ms 执行检测");
                _logger.Warn(
                    $"VGOpenWaitTime={settings.VGOpenWaitTime}ms 过短，运行时提升为 {CTAutoSlicerSettings.DefaultVGOpenWaitTime}ms");
                return CTAutoSlicerSettings.DefaultVGOpenWaitTime;
            }

            return settings.VGOpenWaitTime;
        }

        private static AutomationElement GetFreshMainWindow(Process ctViewerProcess)
        {
            ctViewerProcess.Refresh();
            if (ctViewerProcess.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("VG 主窗口句柄无效，请确认软件是否正常运行");
            }

            EnsureVgForeground(ctViewerProcess);
            return UIAutomationHelper.FindMainWindow(ctViewerProcess.MainWindowHandle);
        }

        private static IReadOnlyList<string> GetVglFiles(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Array.Empty<string>();
            }

            return Directory
                .EnumerateFiles(folder, $"*{CTAutoSlicerSettings.VglFileExtension}")
                .OrderBy(path => new FileInfo(path).CreationTimeUtc)
                .ToList();
        }

        private async Task PrepareNewProjectAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            progress?.Report("Ctrl+N 新建/清空工程");
            _logger.Info("Ctrl+N 新建/清空工程");

            await InvokeOnVgUiThreadAsync(ctViewerProcess, SimulateCtrlN).ConfigureAwait(false);

            var saveDialog = await UIAutomationHelper.WaitForElementAsync(
                    () => FindSavePromptDialog(mainWindow),
                    TimeSpan.FromMilliseconds(settings.NewProjectDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (saveDialog != null)
            {
                await HandleSaveDialogAsync(ctViewerProcess, mainWindow, cancellationToken, progress)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.Info("未在超时内检测到保存提示框，继续流程");
            }
        }

        /// <summary>
        /// 在限定时间内轮询「自动保存恢复」弹窗；若出现则点击「跳过」并等待窗口关闭。
        /// </summary>
        private async Task DismissAutoSaveRecoveryDialogAsync(
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            int timeoutMs = AutoSaveRecoveryDismissTimeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureVgForeground(ctViewerProcess);

                if (FindAutoSaveRecoveryDialog() == null)
                {
                    return;
                }

                if (!await TryDismissAutoSaveRecoveryDialogAsync(cancellationToken, progress).ConfigureAwait(false))
                {
                    await Task.Delay(AutoSaveRecoveryScanIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                progress?.Report("检测到「自动保存恢复」弹窗，已点击「跳过」");
                _logger.Info("已自动点击「自动保存恢复」弹窗的「跳过」按钮");
                await WaitUntilDisappearedAsync(
                        FindAutoSaveRecoveryDialog,
                        AutoSaveRecoveryScanIntervalMs * 5,
                        cancellationToken)
                    .ConfigureAwait(false);
                progress?.Report("「自动保存恢复」弹窗已关闭");
                _logger.Info("「自动保存恢复」弹窗已关闭");
                return;
            }

            if (FindAutoSaveRecoveryDialog() != null)
            {
                _logger.Warn($"在 {timeoutMs}ms 内未能关闭「自动保存恢复」弹窗");
                progress?.Report("警告：「自动保存恢复」弹窗仍存在，请人工点击「跳过」后继续");
            }
        }

        private static AutomationElement? FindAutoSaveRecoveryDialog() =>
            UIAutomationHelper.FindWindowByName(
                AutomationElement.RootElement,
                AutoSaveRecoveryDialogWindowName);

        /// <summary>
        /// 若「自动保存恢复」弹窗可见，则点击「跳过」。返回是否执行了跳过操作。
        /// </summary>
        private async Task<bool> TryDismissAutoSaveRecoveryDialogAsync(
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            var recoveryDialog = FindAutoSaveRecoveryDialog();
            if (recoveryDialog == null)
            {
                return false;
            }

            progress?.Report("检测到「自动保存恢复」弹窗，正在点击「跳过」...");
            _logger.Info("检测到「自动保存恢复」弹窗");

            if (UIAutomationHelper.TryInvokeButtonByName(recoveryDialog, AutoSaveRecoverySkipButtonName))
            {
                return true;
            }

            var skipButton = UIAutomationHelper.FindButtonByName(recoveryDialog, AutoSaveRecoverySkipButtonName);
            if (skipButton != null)
            {
                await NativeClickElementAsync(skipButton, cancellationToken).ConfigureAwait(false);
                return true;
            }

            _logger.Warn("找到「自动保存恢复」窗口但未找到可点击的「跳过」按钮");
            return false;
        }

        private async Task HandleSaveDialogAsync(
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var saveDialog = FindSavePromptDialog(mainWindow);

            if (saveDialog == null)
            {
                _logger.Info("未检测到保存提示框，继续流程");
                return;
            }

            progress?.Report("检测到保存提示，确认「否」");
            _logger.Info("检测到保存提示框");

            if (!UIAutomationHelper.TryInvokeButtonByName(saveDialog, SaveDialogNoButtonName))
            {
                await InvokeOnVgUiThreadAsync(ctViewerProcess, SimulateEnter).ConfigureAwait(false);
            }

            await WaitUntilDisappearedAsync(
                    () => FindSavePromptDialog(mainWindow),
                    5000,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task OpenQuickImportAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ExecuteStepWithRetryAsync(
                    "打开快速导入",
                    async attempt =>
                    {
                        var mainWindow = GetFreshMainWindow(ctViewerProcess);
                        if (attempt == 1)
                        {
                            progress?.Report("Ctrl+I 打开快速导入");
                            _logger.Info("Ctrl+I 打开快速导入");
                            await InvokeOnVgUiThreadAsync(ctViewerProcess, SimulateCtrlI).ConfigureAwait(false);
                        }
                        else
                        {
                            progress?.Report($"打开快速导入失败，第 {attempt} 次尝试菜单 Plan B");
                            _logger.Info($"打开快速导入第 {attempt} 次尝试菜单 Plan B");
                            await OpenQuickImportViaMenuPlanBAsync(ctViewerProcess, mainWindow, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        return await UIAutomationHelper.WaitForElementAsync(
                                () => FindQuickImportDialog()
                                    ?? FindQuickImportDialog(GetFreshMainWindow(ctViewerProcess)),
                                TimeSpan.FromMilliseconds(settings.QuickImportDialogWaitTime),
                                cancellationToken,
                                UiPollIntervalMs)
                            .ConfigureAwait(false);
                    },
                    dialog => dialog != null,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        private async Task OpenQuickImportViaMenuPlanBAsync(
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken)
        {
            var fileMenuItem = UIAutomationHelper.FindDescendantByName(mainWindow, ControlType.MenuItem, FileMenuItemName)
                ?? throw new InvalidOperationException("未找到文件菜单");

            if (!UIAutomationHelper.TryExpandMenuItem(fileMenuItem))
            {
                await InvokeOnVgUiThreadAsync(ctViewerProcess, SimulateAltF).ConfigureAwait(false);
                await UIAutomationHelper.WaitForElementAsync(
                        () => UIAutomationHelper.FindDescendantByName(
                            mainWindow,
                            ControlType.MenuItem,
                            QuickImportMenuItemKeyword,
                            nameContains: true),
                        TimeSpan.FromMilliseconds(5000),
                        cancellationToken,
                        UiPollIntervalMs)
                    .ConfigureAwait(false);
            }

            var quickImportMenuItem = UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.MenuItem,
                    QuickImportMenuItemKeyword,
                    nameContains: true)
                ?? throw new InvalidOperationException("未找到快速导入菜单项");

            if (!UIAutomationHelper.TryInvokeButton(quickImportMenuItem))
            {
                throw new InvalidOperationException("无法触发快速导入菜单项");
            }
        }

        private async Task QuickImportFillAndOpenAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var importFilePath = Path.GetFullPath(vglPath);
            var quickImportDialog = await WaitForQuickImportDialogAsync(
                    ctViewerProcess,
                    settings,
                    cancellationToken)
                .ConfigureAwait(false);

            var fileNameEdit = FindQuickImportFileNameEdit(quickImportDialog)
                ?? throw new InvalidOperationException("未找到快速导入「文件名(N):」输入框");

            progress?.Report($"输入 .vgl 文件路径：{importFilePath}");
            _logger.Info($"快速导入输入 .vgl 路径：{importFilePath}");
            await FillQuickImportFileNameAsync(
                    quickImportDialog,
                    fileNameEdit,
                    importFilePath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("Alt+O 打开文件");
            _logger.Info("Alt+O 打开文件");
            await InvokeOnAutomationForegroundAsync(quickImportDialog, SimulateAltO).ConfigureAwait(false);

            var finishButton = await UIAutomationHelper.WaitForElementAsync(
                    () => FindEnabledFinishButton(GetFreshMainWindow(ctViewerProcess)),
                    TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (finishButton != null)
            {
                return;
            }

            var stillOpenDialog = FindQuickImportDialog()
                ?? FindQuickImportDialog(GetFreshMainWindow(ctViewerProcess));
            if (stillOpenDialog != null)
            {
                progress?.Report("Alt+O 未进入文件选择向导，尝试点击「打开(O)」");
                _logger.Info("Alt+O 未进入文件选择向导，尝试点击「打开(O)」");
                var openButton = FindQuickImportOpenButton(stillOpenDialog)
                    ?? throw new InvalidOperationException("快速导入打开失败：未找到「打开(O)」按钮");

                if (!UIAutomationHelper.TryInvokeButton(openButton))
                {
                    await NativeClickElementAsync(openButton, cancellationToken).ConfigureAwait(false);
                }

                finishButton = await UIAutomationHelper.WaitForElementAsync(
                        () => FindEnabledFinishButton(GetFreshMainWindow(ctViewerProcess)),
                        TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                        cancellationToken,
                        UiPollIntervalMs)
                    .ConfigureAwait(false);
            }

            if (finishButton == null)
            {
                throw new InvalidOperationException(
                    $"Alt+O 后未在 {settings.FileSelectionDialogWaitTime}ms 内出现可点击的「{FileSelectionFinishButtonName}」按钮");
            }
        }

        private async Task<AutomationElement> WaitForQuickImportDialogAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken)
        {
            var dialog = await UIAutomationHelper.WaitForElementAsync(
                    () => FindQuickImportDialog() ?? FindQuickImportDialog(GetFreshMainWindow(ctViewerProcess)),
                    TimeSpan.FromMilliseconds(settings.QuickImportDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            return dialog ?? throw new InvalidOperationException("快速导入对话框未打开");
        }

        private async Task FillQuickImportFileNameAsync(
            AutomationElement quickImportDialog,
            AutomationElement fileNameEdit,
            string path,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            Unlock();
            try
            {
                EnsureAutomationForeground(quickImportDialog);
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);

                if (UIAutomationHelper.TrySetEditValueSafe(fileNameEdit, path)
                    && await WaitForInputPathAsync(fileNameEdit, path, 2000, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Info("快速导入路径已通过 ValuePattern 写入");
                    return;
                }

                progress?.Report("ValuePattern 未生效，使用 Alt+N 聚焦后键盘输入");
                _logger.Info("快速导入 ValuePattern 未生效，改用 Alt+N + 键盘输入");
                await InvokeOnUiThreadAsync(SimulateAltN).ConfigureAwait(false);
                await Task.Delay(120, cancellationToken).ConfigureAwait(false);
                await NativeClickElementWhileUnlockedAsync(fileNameEdit, cancellationToken).ConfigureAwait(false);
                await TypeTextIntoFocusedControlAsync(path).ConfigureAwait(false);

                if (!await WaitForInputPathAsync(fileNameEdit, path, 3000, cancellationToken).ConfigureAwait(false))
                {
                    var actual = UIAutomationHelper.TryGetInputValue(fileNameEdit) ?? "(空)";
                    throw new InvalidOperationException(
                        $"快速导入路径写入失败，期望「{path}」，读回「{actual}」");
                }
            }
            finally
            {
                Lock();
            }
        }

        private Task<bool> WaitForInputPathAsync(
            AutomationElement edit,
            string expectedPath,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => InputValueMatchesPath(UIAutomationHelper.TryGetInputValue(edit), expectedPath),
                timeoutMs,
                cancellationToken);

        private static AutomationElement? FindQuickImportFileNameEdit(AutomationElement dialog)
        {
            var edit = UIAutomationHelper.FindEditByName(dialog, QuickImportEditName);
            if (edit != null)
            {
                return edit;
            }

            var comboBox = UIAutomationHelper.FindDescendantByName(dialog, ControlType.ComboBox, QuickImportEditName)
                ?? UIAutomationHelper.FindDescendantByName(
                    dialog,
                    ControlType.ComboBox,
                    "文件名",
                    nameContains: true);
            if (comboBox != null)
            {
                return UIAutomationHelper.FindEditByName(comboBox, QuickImportEditName)
                    ?? UIAutomationHelper.FindFirstDescendantByControlType(comboBox, ControlType.Edit);
            }

            return UIAutomationHelper.FindVisibleFileNameEdit(dialog);
        }

        private static AutomationElement? FindQuickImportOpenButton(AutomationElement dialog) =>
            UIAutomationHelper.FindButtonByName(dialog, QuickImportOpenButtonName)
            ?? UIAutomationHelper.FindButtonByName(dialog, QuickImportOpenButtonNameFallback, exactMatch: false);

        private async Task TypeTextIntoFocusedControlAsync(string value)
        {
            await InvokeOnUiThreadAsync(() =>
            {
                _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                _simulator.SimulateKeyPress(KeyCode.VcA);
                _simulator.SimulateKeyRelease(KeyCode.VcA);
                _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                Thread.Sleep(50);
                _simulator.SimulateTextEntry(value);
            }).ConfigureAwait(false);
        }

        private async Task NativeClickElementWhileUnlockedAsync(
            AutomationElement element,
            CancellationToken cancellationToken)
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException("控件 BoundingRectangle 无效，无法点击");
            }

            var x = (int)(rect.Left + rect.Width / 2);
            var y = (int)(rect.Top + rect.Height / 2);
            var targetWindow = ResolveNativeWindowHandle(element);
            await InvokeOnUiThreadAsync(() =>
            {
                if (targetWindow != IntPtr.Zero)
                {
                    WinAPIHelper.SetForegroundWindow(targetWindow);
                }

                WinAPIHelper.ClickScreen(x, y);
            }).ConfigureAwait(false);
            await Task.Delay(80, cancellationToken).ConfigureAwait(false);
        }

        private static void EnsureAutomationForeground(AutomationElement element)
        {
            var hwnd = ResolveNativeWindowHandle(element);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            WinAPIHelper.ShowWindowAsync(hwnd, 3);
            WinAPIHelper.SetForegroundWindow(hwnd);
        }

        private async Task InvokeOnAutomationForegroundAsync(AutomationElement foregroundScope, Action action)
        {
            Unlock();
            try
            {
                EnsureAutomationForeground(foregroundScope);
                await Task.Delay(80).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(action).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private async Task ConfirmFileSelectionAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ExecuteStepWithRetryAsync(
                    "等待文件选择向导",
                    async _ =>
                    {
                        return await UIAutomationHelper.WaitForElementAsync(
                                () => FindEnabledFinishButton(GetFreshMainWindow(ctViewerProcess)),
                                TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                                cancellationToken,
                                UiPollIntervalMs)
                            .ConfigureAwait(false);
                    },
                    button => button != null,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("文件选择向导已出现，输入路径");
            _logger.Info("文件选择向导已出现");

            var importFilePath = Path.GetFullPath(vglPath);
            var wizardDialog = FindFileSelectionWizardDialog(GetFreshMainWindow(ctViewerProcess))
                ?? throw new InvalidOperationException("未找到文件选择向导对话框");

            var wizardEdit = FindQuickImportFileNameEdit(wizardDialog)
                ?? UIAutomationHelper.FindEditByName(wizardDialog, QuickImportEditName)
                ?? UIAutomationHelper.FindFirstEnabledEdit(wizardDialog, QuickImportEditName)
                ?? throw new InvalidOperationException("未找到文件选择向导输入框");

            progress?.Report($"向导内输入 .vgl 文件路径：{importFilePath}");
            _logger.Info($"文件选择向导输入 .vgl 路径：{importFilePath}");
            await FillQuickImportFileNameAsync(
                    wizardDialog,
                    wizardEdit,
                    importFilePath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await ExecuteStepWithRetryAsync(
                    "完成文件选择",
                    async attempt =>
                    {
                        progress?.Report(attempt == 1 ? "Alt+F 完成文件选择" : $"Alt+F 完成文件选择（第 {attempt} 次重试）");
                        _logger.Info($"Alt+F 完成文件选择，第 {attempt} 次");
                        await InvokeOnAutomationForegroundAsync(wizardDialog, SimulateAltF).ConfigureAwait(false);

                        return await WaitUntilDisappearedAsync(
                                () => FindEnabledFinishButton(GetFreshMainWindow(ctViewerProcess)),
                                settings.FileSelectionDialogWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        private async Task WaitForImportCompleteAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ExecuteStepWithRetryAsync(
                    "等待导入进度框出现",
                    async _ =>
                    {
                        return await UIAutomationHelper.WaitForElementAsync(
                                () =>
                                {
                                    var window = GetFreshMainWindow(ctViewerProcess);
                                    return FindVisibleImportProgressWindow(window);
                                },
                                TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    window => window != null,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("导入体积中......");
            _logger.Info("检测到可见导入进度框");

            var deadline = DateTime.UtcNow.AddMinutes(settings.ImportTimeoutMinutes);
            var offscreenProgressReadySinceUtc = (DateTime?)null;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AutomationElement mainWindow;
                try
                {
                    mainWindow = GetFreshMainWindow(ctViewerProcess);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"刷新 VG 主窗口失败：{GetFullExceptionMessage(ex)}");
                    await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var visibleProgress = FindVisibleImportProgressWindow(mainWindow);
                var volumeReady = IsVolumeImportReady(mainWindow, settings);

                if (visibleProgress == null && volumeReady)
                {
                    progress?.Report("导入体积完成");
                    _logger.Info("导入体积完成：进度框已消失且工作区已就绪");
                    await WaitForRenderingWorkspaceReadyAsync(
                            ctViewerProcess,
                            settings,
                            settings.PostImportWaitTime,
                            cancellationToken)
                        .ConfigureAwait(false);
                    await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
                    return;
                }

                // 可见进度框仍在时，必须等待其消失，不能提前进入预设/厚板（避免 UIA 误报工作区就绪）
                if (visibleProgress != null)
                {
                    offscreenProgressReadySinceUtc = null;
                    progress?.Report("导入体积中......");
                    await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // 仅当进度框 UIA 节点离屏残留、且工作区已就绪时，才允许绕过
                if (volumeReady && FindOffscreenImportProgressWindow(mainWindow) != null)
                {
                    offscreenProgressReadySinceUtc ??= DateTime.UtcNow;
                    var readyMs = (DateTime.UtcNow - offscreenProgressReadySinceUtc.Value).TotalMilliseconds;
                    if (readyMs >= OffscreenProgressBypassMs)
                    {
                        progress?.Report("导入体积完成（工作区已就绪，忽略离屏进度框 UIA 残留）");
                        _logger.Info($"导入体积完成：工作区已就绪 {readyMs:0}ms，忽略离屏进度框 UIA 残留");
                        await WaitForRenderingWorkspaceReadyAsync(
                                ctViewerProcess,
                                settings,
                                settings.PostImportWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);
                        await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                }
                else
                {
                    offscreenProgressReadySinceUtc = null;
                }

                progress?.Report("导入体积中......");
                await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待导入完成超时（{settings.ImportTimeoutMinutes} 分钟）：" +
                "进度框未消失且工作区（渲染面板/四宫格视口）未就绪，请检查 VG 是否卡死或增大「单文件导入超时(分钟)」");
        }

        private async Task ApplyRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var presetName = settings.RenderingPresetName.Trim();
            progress?.Report($"等待渲染工作区就绪后应用预设「{presetName}」（最大 {settings.PresetApplyWaitTime}ms）");
            _logger.Info($"等待渲染工作区就绪，最大 {settings.PresetApplyWaitTime}ms 后应用预设「{presetName}」");
            await WaitForRenderingWorkspaceReadyAsync(
                    ctViewerProcess,
                    settings,
                    settings.PresetApplyWaitTime,
                    cancellationToken)
                .ConfigureAwait(false);

            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);

            var currentMainWindow = mainWindow;
            await ExecuteStepWithRetryAsync(
                    "应用渲染预设",
                    async attempt =>
                    {
                        currentMainWindow = GetFreshMainWindow(ctViewerProcess);
                        EnsureVgForeground(ctViewerProcess);

                        progress?.Report(attempt == 1
                            ? $"打开渲染预设下拉并选择「{presetName}」"
                            : $"应用渲染预设（第 {attempt} 次重试）");
                        _logger.Info(attempt == 1
                            ? $"打开渲染预设下拉并选择「{presetName}」"
                            : $"应用渲染预设第 {attempt} 次重试");

                        var renderingPanel = FindRenderingPanel(currentMainWindow)
                            ?? throw new InvalidOperationException($"未找到「{RenderingPanelWindowName}」面板");

                        var comboBox = FindRenderingPresetComboBox(renderingPanel)
                            ?? throw new InvalidOperationException("未找到渲染预设组合框");

                        if (attempt > 1)
                        {
                            await InvokeOnUiThreadAsync(SimulateEscape).ConfigureAwait(false);
                            await WaitUntilAsync(
                                    () => !UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                                    settings.PresetDropdownWaitTime,
                                    cancellationToken)
                                .ConfigureAwait(false);
                        }

                        var beforeValue = UIAutomationHelper.GetComboBoxDisplayText(comboBox);
                        _logger.Info($"应用前组合框值：「{beforeValue}」");

                        if (IsPresetApplied(comboBox, presetName))
                        {
                            _logger.Info($"ValuePattern 读数已是「{presetName}」，仍打开下拉确认应用");
                        }

                        if (UIAutomationHelper.TrySetComboBoxValue(comboBox, presetName))
                        {
                            if (await WaitForPresetAppliedAsync(
                                        comboBox,
                                        presetName,
                                        settings.PresetDropdownWaitTime,
                                        cancellationToken)
                                    .ConfigureAwait(false))
                            {
                                _logger.Info($"通过 ValuePattern 直接设置预设「{presetName}」成功");
                                return true;
                            }
                        }

                        await OpenRenderingPresetComboBoxAsync(
                                comboBox,
                                settings.PresetDropdownWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (!UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox))
                        {
                            throw new InvalidOperationException("渲染预设下拉未能打开");
                        }

                        var listItem = await UIAutomationHelper.WaitForElementAsync(
                                () => FindPresetListItem(comboBox, renderingPanel, presetName),
                                TimeSpan.FromMilliseconds(settings.PresetDropdownWaitTime),
                                cancellationToken,
                                pollIntervalMs: 200)
                            .ConfigureAwait(false);

                        if (listItem == null)
                        {
                            throw new InvalidOperationException($"未找到预设列表项「{presetName}」");
                        }

                        progress?.Report($"选中预设列表项「{presetName}」");
                        _logger.Info(
                            $"找到预设列表项「{presetName}」，BoundingRect={listItem.Current.BoundingRectangle}");

                        await SelectPresetListItemAsync(
                                listItem,
                                comboBox,
                                presetName,
                                settings.PresetDropdownWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);

                        var afterValue = UIAutomationHelper.GetComboBoxDisplayText(comboBox);
                        _logger.Info($"应用后组合框值：「{afterValue}」");

                        if (!IsPresetApplied(comboBox, presetName))
                        {
                            throw new InvalidOperationException(
                                $"未能选中预设「{presetName}」，应用前「{beforeValue}」应用后「{afterValue}」");
                        }

                        progress?.Report($"渲染预设「{presetName}」已应用");
                        return true;
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report($"渲染预设「{presetName}」已应用");
            _logger.Info($"渲染预设「{presetName}」已应用");
        }

        private static AutomationElement? FindRenderingPanel(AutomationElement mainWindow)
        {
            return UIAutomationHelper.FindWindowByName(mainWindow, RenderingPanelWindowName)
                ?? UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.Window,
                    RenderingPanelWindowName)
                ?? UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.Pane,
                    RenderingPanelWindowName);
        }

        private static AutomationElement? FindRenderingPresetComboBox(AutomationElement renderingPanel)
        {
            return UIAutomationHelper.FindFirstDescendantByControlType(renderingPanel, ControlType.ComboBox);
        }

        private async Task OpenRenderingPresetComboBoxAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox))
            {
                return;
            }

            comboBox.SetFocus();
            if (await WaitForComboDropdownOpenAsync(comboBox, timeoutMs, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (UIAutomationHelper.TryExpandComboBox(comboBox)
                && await WaitForComboDropdownOpenAsync(comboBox, timeoutMs, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await InvokeOnUiThreadAsync(SimulateAltDown).ConfigureAwait(false);
            if (await WaitForComboDropdownOpenAsync(comboBox, timeoutMs, cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            await ClickElementCenterAsync(comboBox).ConfigureAwait(false);
            if (!await WaitForComboDropdownOpenAsync(comboBox, timeoutMs, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("渲染预设下拉在超时内未能打开");
            }
        }

        private async Task SelectPresetListItemAsync(
            AutomationElement listItem,
            AutomationElement comboBox,
            string presetName,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            listItem.SetFocus();
            UIAutomationHelper.TryScrollListItemIntoView(listItem);

            // Select 只会让 Qt 列表高亮到目标项（光标移过去），不等于已应用预设。
            UIAutomationHelper.TrySelectListItem(listItem);
            UIAutomationHelper.TryInvokeElement(listItem);

            _logger.Info(
                $"列表项高亮后组合框值：「{UIAutomationHelper.GetComboBoxDisplayText(comboBox)}」");

            if (!await WaitForPresetAppliedAsync(comboBox, presetName, timeoutMs, cancellationToken)
                    .ConfigureAwait(false))
            {
                _logger.Info($"使用 Win32 鼠标点击确认列表项「{presetName}」");
                await NativeClickElementAsync(listItem, cancellationToken).ConfigureAwait(false);
            }

            if (!await WaitForPresetAppliedAsync(comboBox, presetName, timeoutMs, cancellationToken)
                    .ConfigureAwait(false))
            {
                _logger.Info($"再次 Win32 点击列表项「{presetName}」并回车确认");
                await NativeClickElementAsync(listItem, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
            }

            if (!await WaitForPresetAppliedAsync(comboBox, presetName, timeoutMs, cancellationToken)
                    .ConfigureAwait(false))
            {
                _logger.Info($"尝试键盘输入预设名「{presetName}」并回车");
                await SelectPresetByKeyboardAsync(presetName, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SelectPresetByKeyboardAsync(
            string presetName,
            CancellationToken cancellationToken)
        {
            await InvokeOnUiThreadAsync(() => _simulator.SimulateTextEntry(presetName)).ConfigureAwait(false);
            await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
        }

        private async Task NativeClickElementAsync(
            AutomationElement element,
            CancellationToken cancellationToken)
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException("控件 BoundingRectangle 无效，无法点击");
            }

            var x = (int)(rect.Left + rect.Width / 2);
            var y = (int)(rect.Top + rect.Height / 2);
            var targetWindow = ResolveNativeWindowHandle(element);

            Unlock();
            try
            {
                await WaitUntilAsync(
                        () =>
                        {
                            try
                            {
                                var rect = element.Current.BoundingRectangle;
                                return rect.Width > 0 && rect.Height > 0;
                            }
                            catch
                            {
                                return false;
                            }
                        },
                        500,
                        cancellationToken)
                    .ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    if (targetWindow != IntPtr.Zero)
                    {
                        WinAPIHelper.SetForegroundWindow(targetWindow);
                    }

                    WinAPIHelper.ClickScreen(x, y);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private async Task NativeDoubleClickElementAsync(
            AutomationElement element,
            CancellationToken cancellationToken)
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException("控件 BoundingRectangle 无效，无法双击");
            }

            var x = (int)(rect.Left + rect.Width / 2);
            var y = (int)(rect.Top + rect.Height / 2);
            var targetWindow = ResolveNativeWindowHandle(element);

            Unlock();
            try
            {
                await WaitUntilAsync(
                        () =>
                        {
                            try
                            {
                                var rect = element.Current.BoundingRectangle;
                                return rect.Width > 0 && rect.Height > 0;
                            }
                            catch
                            {
                                return false;
                            }
                        },
                        500,
                        cancellationToken)
                    .ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    if (targetWindow != IntPtr.Zero)
                    {
                        WinAPIHelper.SetForegroundWindow(targetWindow);
                    }

                    WinAPIHelper.DoubleClickScreen(x, y);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private static AutomationElement? FindPresetListItem(
            AutomationElement comboBox,
            AutomationElement renderingPanel,
            string presetName)
        {
            var list = UIAutomationHelper.FindFirstDescendantByControlType(comboBox, ControlType.List);
            if (list != null)
            {
                var itemInList = UIAutomationHelper.FindVisibleDescendantByName(list, ControlType.ListItem, presetName);
                if (itemInList != null)
                {
                    return itemInList;
                }
            }

            var itemInPanel = UIAutomationHelper.FindVisibleDescendantByName(
                renderingPanel,
                ControlType.ListItem,
                presetName);
            if (itemInPanel != null)
            {
                return itemInPanel;
            }

            return UIAutomationHelper.FindVisibleDescendantByName(
                AutomationElement.RootElement,
                ControlType.ListItem,
                presetName);
        }

        private static bool IsPresetListItemSelected(AutomationElement listItem)
        {
            if (listItem.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)
                && pattern is SelectionItemPattern selectionItemPattern)
            {
                return selectionItemPattern.Current.IsSelected;
            }

            return false;
        }

        private static bool IsPresetApplied(AutomationElement comboBox, string presetName)
        {
            if (!UIAutomationHelper.TryGetComboBoxValueText(comboBox, out var currentValue))
            {
                return false;
            }

            return string.Equals(currentValue, presetName, StringComparison.Ordinal);
        }

        private static AutomationElement? FindOffscreenImportProgressWindow(AutomationElement mainWindow)
        {
            try
            {
                foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
                {
                    var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                    foreach (AutomationElement element in mainWindow.FindAll(TreeScope.Descendants, condition))
                    {
                        var name = element.Current.Name ?? string.Empty;
                        if (!name.Equals(ImportProgressWindowName, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (UIAutomationHelper.IsElementInteractable(element))
                        {
                            continue;
                        }

                        return element;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        private async Task ClickElementCenterAsync(AutomationElement element)
        {
            var rect = element.Current.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                throw new InvalidOperationException("控件 BoundingRectangle 无效，无法点击");
            }

            var x = (short)(rect.Left + rect.Width / 2);
            var y = (short)(rect.Top + rect.Height / 2);
            var targetWindow = ResolveNativeWindowHandle(element);
            await InvokeOnUiThreadAsync(() =>
            {
                if (targetWindow != IntPtr.Zero)
                {
                    WinAPIHelper.SetForegroundWindow(targetWindow);
                }

                MouseSimulationHelper.MouseClick(_simulator, MouseButton.Button1, x, y);
            }).ConfigureAwait(false);
        }

        private static IntPtr ResolveNativeWindowHandle(AutomationElement element)
        {
            var current = element;
            while (current != null)
            {
                var hwnd = current.Current.NativeWindowHandle;
                if (hwnd != 0)
                {
                    return new IntPtr(hwnd);
                }

                try
                {
                    current = TreeWalker.RawViewWalker.GetParent(current);
                }
                catch
                {
                    break;
                }
            }

            return IntPtr.Zero;
        }

        private async Task<T> ExecuteStepWithRetryAsync<T>(
            string stepName,
            Func<int, Task<T>> attemptAction,
            Func<T, bool> isSuccess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            int? maxRetriesOverride = null)
        {
            var maxRetries = maxRetriesOverride ?? settings.StepRetryCount;
            Exception? lastException = null;

            for (var attempt = 1; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (attempt > 1)
                {
                    progress?.Report($"{stepName} 未完成，{settings.StepRetryIntervalMs}ms 后进行第 {attempt}/{maxRetries} 次重试...");
                    _logger.Info($"{stepName} 第 {attempt}/{maxRetries} 次重试");
                    await Task.Delay(settings.StepRetryIntervalMs, cancellationToken).ConfigureAwait(false);
                }

                try
                {
                    var result = await attemptAction(attempt).ConfigureAwait(false);
                    if (isSuccess(result))
                    {
                        return result;
                    }

                    _logger.Warn($"{stepName} 第 {attempt} 次未满足完成条件");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.Warn($"{stepName} 第 {attempt} 次异常：{ex.Message}");
                }
            }

            var message = $"{stepName} 在 {maxRetries} 次尝试后仍未完成，自动流程异常";
            throw lastException != null
                ? new InvalidOperationException(message, lastException)
                : new InvalidOperationException(message);
        }

        private static AutomationElement? FindQuickImportDialog(AutomationElement? mainWindow = null)
        {
            try
            {
                if (mainWindow != null)
                {
                    var fromMain = UIAutomationHelper.FindDescendantWindowByNameKeyword(
                            mainWindow,
                            QuickImportDialogNamePrefix,
                            QuickImportOpenButtonNameFallback)
                        ?? UIAutomationHelper.FindWindowByName(
                            mainWindow,
                            QuickImportDialogNamePrefix,
                            nameStartsWith: true);
                    if (fromMain != null)
                    {
                        return fromMain;
                    }
                }

                return UIAutomationHelper.FindDescendantWindowByNameKeyword(
                        AutomationElement.RootElement,
                        QuickImportDialogNamePrefix,
                        QuickImportOpenButtonNameFallback)
                    ?? UIAutomationHelper.FindWindowByName(
                        AutomationElement.RootElement,
                        QuickImportDialogNamePrefix,
                        nameStartsWith: true);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static AutomationElement? FindFileSelectionWizardDialog(AutomationElement mainWindow)
        {
            var finishButton = FindEnabledFinishButton(mainWindow)
                ?? FindEnabledFinishButton(AutomationElement.RootElement);
            if (finishButton == null)
            {
                return null;
            }

            return UIAutomationHelper.FindAncestorDialogOrWindow(finishButton)
                ?? finishButton;
        }

        private static AutomationElement? FindEnabledFinishButton(AutomationElement mainWindow)
        {
            try
            {
                var button = UIAutomationHelper.FindButtonByName(mainWindow, FileSelectionFinishButtonName)
                    ?? UIAutomationHelper.FindButtonByName(
                        AutomationElement.RootElement,
                        FileSelectionFinishButtonName);
                return button is { Current.IsEnabled: true } ? button : null;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static AutomationElement? FindVisibleImportProgressWindow(AutomationElement mainWindow)
        {
            try
            {
                return UIAutomationHelper.FindVisibleDescendantByName(
                           mainWindow,
                           ControlType.Window,
                           ImportProgressWindowName,
                           exactMatch: true)
                       ?? UIAutomationHelper.FindVisibleDescendantByName(
                           mainWindow,
                           ControlType.Pane,
                           ImportProgressWindowName,
                           exactMatch: true)
                       ?? UIAutomationHelper.FindVisibleDescendantByName(
                           AutomationElement.RootElement,
                           ControlType.Window,
                           ImportProgressWindowName,
                           exactMatch: true)
                       ?? UIAutomationHelper.FindVisibleDescendantByName(
                           AutomationElement.RootElement,
                           ControlType.Pane,
                           ImportProgressWindowName,
                           exactMatch: true);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static bool IsVolumeImportReady(AutomationElement mainWindow, CTAutoSlicerSettings settings)
        {
            try
            {
                var renderingPanel = FindVisibleRenderingPanel(mainWindow);
                if (renderingPanel == null)
                {
                    return false;
                }

                var viewports = UIAutomationHelper.FindViewportQuadrants(
                    mainWindow,
                    settings.ViewportMinWidth,
                    settings.ViewportMinHeight);
                return viewports.Count >= 3;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        private static AutomationElement? FindVisibleRenderingPanel(AutomationElement mainWindow)
        {
            return UIAutomationHelper.FindVisibleDescendantByName(
                       mainWindow,
                       ControlType.Window,
                       RenderingPanelWindowName,
                       exactMatch: true)
                   ?? UIAutomationHelper.FindVisibleDescendantByName(
                       mainWindow,
                       ControlType.Pane,
                       RenderingPanelWindowName,
                       exactMatch: true);
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            if (ex is AggregateException aggregateException)
            {
                ex = aggregateException.Flatten().InnerException ?? ex;
            }

            var messages = new List<string>();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    messages.Add(current.Message.Trim());
                }
            }

            return messages.Count > 0 ? string.Join(" -> ", messages) : ex.GetType().Name;
        }

        private async Task SetEditValueWithKeyboardFallbackAsync(
            AutomationElement edit,
            string value,
            CancellationToken cancellationToken = default,
            bool commitWithEnter = false) =>
            await SetEditValueWithKeyboardFallbackAsync(
                    null,
                    edit,
                    value,
                    cancellationToken,
                    commitWithEnter)
                .ConfigureAwait(false);

        private async Task SetEditValueWithKeyboardFallbackAsync(
            Process? ctViewerProcess,
            AutomationElement edit,
            string value,
            CancellationToken cancellationToken = default,
            bool commitWithEnter = false)
        {
            await NativeDoubleClickElementAsync(edit, cancellationToken).ConfigureAwait(false);
            await WaitUntilAsync(
                    () =>
                    {
                        try
                        {
                            return edit.Current.HasKeyboardFocus;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    2000,
                    cancellationToken)
                .ConfigureAwait(false);

            Unlock();
            try
            {
                var targetWindow = ResolveNativeWindowHandle(edit);
                if (targetWindow != IntPtr.Zero)
                {
                    WinAPIHelper.SetForegroundWindow(targetWindow);
                }
                else if (ctViewerProcess != null)
                {
                    EnsureVgForeground(ctViewerProcess);
                }

                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    _simulator.SimulateKeyPress(KeyCode.VcA);
                    _simulator.SimulateKeyRelease(KeyCode.VcA);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                    Thread.Sleep(50);
                    _simulator.SimulateKeyPress(KeyCode.VcHome);
                    _simulator.SimulateKeyRelease(KeyCode.VcHome);
                    _simulator.SimulateKeyPress(KeyCode.VcLeftShift);
                    _simulator.SimulateKeyPress(KeyCode.VcEnd);
                    _simulator.SimulateKeyRelease(KeyCode.VcEnd);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftShift);
                    _simulator.SimulateTextEntry(value);
                    if (commitWithEnter)
                    {
                        _simulator.SimulateKeyPress(KeyCode.VcEnter);
                        _simulator.SimulateKeyRelease(KeyCode.VcEnter);
                    }
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private static bool InputValueMatches(string? actual, string expected)
        {
            if (string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            var normalizedActual = actual.Trim();
            var normalizedExpected = expected.Trim();
            if (normalizedActual.Equals(normalizedExpected, StringComparison.Ordinal))
            {
                return true;
            }

            if (decimal.TryParse(normalizedActual, out var actualNumber)
                && decimal.TryParse(normalizedExpected, out var expectedNumber))
            {
                return actualNumber == expectedNumber;
            }

            return normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal);
        }

        private static bool InputValueMatchesPath(string? actual, string expectedPath)
        {
            if (string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            var normalizedActual = actual.Trim().Trim('"');
            var normalizedExpected = expectedPath.Trim().Trim('"');
            if (normalizedActual.Equals(normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var expectedFileName = Path.GetFileName(normalizedExpected);
            return !string.IsNullOrEmpty(expectedFileName)
                && normalizedActual.EndsWith(expectedFileName, StringComparison.OrdinalIgnoreCase);
        }

        private Task OnImportSucceededAsync(string vglPath)
        {
            _logger.Info($"导入成功（暂不删除文件）：{vglPath}");
            return Task.CompletedTask;
        }

        private async Task ActivateVgWindowAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken)
        {
            ctViewerProcess.Refresh();
            if (ctViewerProcess.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("VG 主窗口句柄无效，无法置前");
            }

            WinAPIHelper.ShowWindowAsync(ctViewerProcess.MainWindowHandle, 3);
            WinAPIHelper.SetForegroundWindow(ctViewerProcess.MainWindowHandle);
            await TryDismissAutoSaveRecoveryDialogAsync(cancellationToken).ConfigureAwait(false);
            await WaitForFileMenuReadyAsync(ctViewerProcess, settings.VGShowFrontWaitTime, cancellationToken)
                .ConfigureAwait(false);
        }

        private void SimulateCtrlN()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
            _simulator.SimulateKeyPress(KeyCode.VcN);
            _simulator.SimulateKeyRelease(KeyCode.VcN);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
        }

        private void SimulateCtrlI()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
            _simulator.SimulateKeyPress(KeyCode.VcI);
            _simulator.SimulateKeyRelease(KeyCode.VcI);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
        }

        private void SimulateAltN()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcN);
            _simulator.SimulateKeyRelease(KeyCode.VcN);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        private void SimulateAltO()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcO);
            _simulator.SimulateKeyRelease(KeyCode.VcO);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        private void SimulateAltF()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcF);
            _simulator.SimulateKeyRelease(KeyCode.VcF);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        /// <summary>菜单级快捷键 Alt+F+T：按住 Alt，依次按 F、T（非两次独立的 Alt 组合）。</summary>
        private void SimulateAltFt()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcF);
            _simulator.SimulateKeyRelease(KeyCode.VcF);
            _simulator.SimulateKeyPress(KeyCode.VcT);
            _simulator.SimulateKeyRelease(KeyCode.VcT);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        private void SimulateAltS()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcS);
            _simulator.SimulateKeyRelease(KeyCode.VcS);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        private void SimulateTab()
        {
            _simulator.SimulateKeyPress(KeyCode.VcTab);
            _simulator.SimulateKeyRelease(KeyCode.VcTab);
        }

        private void SimulateEnter()
        {
            _simulator.SimulateKeyPress(KeyCode.VcEnter);
            _simulator.SimulateKeyRelease(KeyCode.VcEnter);
        }

        private void SimulateEscape()
        {
            _simulator.SimulateKeyPress(KeyCode.VcEscape);
            _simulator.SimulateKeyRelease(KeyCode.VcEscape);
        }

        private void SimulateAltDown()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftAlt);
            _simulator.SimulateKeyPress(KeyCode.VcDown);
            _simulator.SimulateKeyRelease(KeyCode.VcDown);
            _simulator.SimulateKeyRelease(KeyCode.VcLeftAlt);
        }

        public async Task TestThickPlateAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report("厚板测试：三象限依次设置（请确保 VG 已导入体积且已应用 LUT 预设）");
                _logger.Info("厚板三象限测试开始");

                var ctViewerProcess = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);

                await ApplyThickPlateForQuadrantsAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("厚板三象限测试完成");
                _logger.Info("厚板三象限测试完成");
            }
            finally
            {
                Unlock();
            }
        }

        private async Task ApplyThickPlateForQuadrantsAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("开始三象限厚板设置（象限 1 → 2 → 3）");
            _logger.Info("开始三象限厚板设置");

            for (var quadrantIndex = 1; quadrantIndex <= 3; quadrantIndex++)
            {
                await ApplyThickPlateForQuadrantAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        quadrantIndex,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);
            }

            progress?.Report("三象限厚板设置完成");
            _logger.Info("三象限厚板设置完成");
        }

        private async Task ApplyThickPlateForQuadrantAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            int quadrantIndex,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ExecuteStepWithRetryAsync(
                    $"象限{quadrantIndex}厚板设置",
                    async _ =>
                    {
                        mainWindow = GetFreshMainWindow(ctViewerProcess);
                        EnsureVgForeground(ctViewerProcess);
                        LogViewportQuadrantLayout(mainWindow, settings, quadrantIndex);

                        var viewport = GetViewportByQuadrantIndex(mainWindow, settings, quadrantIndex);
                        var viewportRect = viewport.Current.BoundingRectangle;
                        var (clickX, clickY) = GetViewportRightClickPoint(viewportRect, settings, quadrantIndex);
                        var offsetNote = quadrantIndex == 3
                            ? $"（含第三象限上移 {Math.Abs(settings.ViewportQuadrant3ClickOffsetY)}px）"
                            : string.Empty;

                        progress?.Report(
                            $"象限{quadrantIndex} 视口矩形=[{viewportRect.Left:0},{viewportRect.Top:0},{viewportRect.Right:0},{viewportRect.Bottom:0}] 右键点=({clickX},{clickY}){offsetNote}");
                        _logger.Info(
                            $"象限{quadrantIndex} 视口矩形={viewportRect} 右键点=({clickX},{clickY}){offsetNote}");

                        await RightClickAndOpenThickPlateMenuAsync(
                                settings,
                                ctViewerProcess,
                                clickX,
                                clickY,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false);

                        await ConfigureThickPlateDialogAsync(settings, mainWindow, cancellationToken, progress)
                            .ConfigureAwait(false);

                        return true;
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress,
                    settings.ThickPlateStepRetryCount)
                .ConfigureAwait(false);
        }

        private static void EnsureVgForeground(Process ctViewerProcess)
        {
            ctViewerProcess.Refresh();
            if (ctViewerProcess.MainWindowHandle == IntPtr.Zero)
            {
                return;
            }

            WinAPIHelper.ShowWindowAsync(ctViewerProcess.MainWindowHandle, 3);
            WinAPIHelper.SetForegroundWindow(ctViewerProcess.MainWindowHandle);
        }

        private void LogViewportQuadrantLayout(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int targetQuadrantIndex)
        {
            var quadrants = UIAutomationHelper.FindViewportQuadrants(
                mainWindow,
                settings.ViewportMinWidth,
                settings.ViewportMinHeight);

            string LabelForIndex(int index) => index switch
            {
                0 => "Q1左上",
                1 => "Q2右上",
                2 => "Q3左下(Q1下方)",
                3 => "Q4右下",
                _ => $"Q{index + 1}",
            };

            for (var i = 0; i < quadrants.Count; i++)
            {
                var rect = quadrants[i].Current.BoundingRectangle;
                var marker = i + 1 == targetQuadrantIndex ? " ← 本次目标" : string.Empty;
                _logger.Info(
                    $"视口映射 {LabelForIndex(i)}: [{rect.Left:0},{rect.Top:0},{rect.Right:0},{rect.Bottom:0}]{marker}");
            }
        }

        /// <summary>
        /// 视口右键后，按标定偏移一次完成「厚板」菜单点击（跳过 UIA 轮询）。
        /// </summary>
        private async Task RightClickAndOpenThickPlateMenuAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            int rightClickX,
            int rightClickY,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var offsetX = rightClickX + settings.ThickPlateMenuOffsetX;
            var offsetY = rightClickY + settings.ThickPlateMenuOffsetY;
            progress?.Report(
                $"右键 ({rightClickX},{rightClickY}) → 点击「{settings.ThickPlateMenuItemName}」偏移 ({settings.ThickPlateMenuOffsetX},{settings.ThickPlateMenuOffsetY})");
            _logger.Info(
                $"厚板菜单：右键 ({rightClickX},{rightClickY})，偏移点击 ({offsetX},{offsetY})");

            ctViewerProcess.Refresh();
            Unlock();
            try
            {
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                await WaitForViewportQuadrantsReadyAsync(
                        mainWindow,
                        settings,
                        3,
                        settings.ThickPlateStepDelayMs,
                        cancellationToken)
                    .ConfigureAwait(false);

                await InvokeOnUiThreadAsync(() =>
                {
                    if (ctViewerProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        WinAPIHelper.SetForegroundWindow(ctViewerProcess.MainWindowHandle);
                    }

                    MouseSimulationHelper.MouseClick(
                        _simulator,
                        MouseButton.Button2,
                        (short)rightClickX,
                        (short)rightClickY);
                }).ConfigureAwait(false);

                await InvokeOnUiThreadAsync(() =>
                {
                    WinAPIHelper.ClickScreen(offsetX, offsetY);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private static AutomationElement GetViewportByQuadrantIndex(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int quadrantIndex)
        {
            var quadrants = UIAutomationHelper.FindViewportQuadrants(
                mainWindow,
                settings.ViewportMinWidth,
                settings.ViewportMinHeight);

            if (quadrants.Count < quadrantIndex)
            {
                throw new InvalidOperationException(
                    $"未找到足够的视口（需要至少 {quadrantIndex} 个，实际 {quadrants.Count} 个）。请调整 ViewportMinWidth/ViewportMinHeight 或检查 VG 四宫格布局。");
            }

            return quadrants[quadrantIndex - 1];
        }

        private static (int X, int Y) GetViewportRightClickPoint(
            Rect viewportRect,
            CTAutoSlicerSettings settings,
            int quadrantIndex)
        {
            var x = (int)(viewportRect.Left + viewportRect.Width / 2);
            var y = (int)(viewportRect.Top + viewportRect.Height / 2) + settings.ViewportClickOffsetY;
            if (quadrantIndex == 3)
            {
                y += settings.ViewportQuadrant3ClickOffsetY;
            }

            return (x, y);
        }

        private async Task ConfigureThickPlateDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var dialogName = settings.ThickPlateDialogWindowName.Trim();
            var dialog = await UIAutomationHelper.WaitForElementAsync(
                    () => FindThickPlateDialog(mainWindow, dialogName),
                    TimeSpan.FromMilliseconds(settings.ThickPlateDialogWaitTime),
                    cancellationToken,
                    pollIntervalMs: 200)
                .ConfigureAwait(false);

            if (dialog == null)
            {
                throw new InvalidOperationException($"未在 {settings.ThickPlateDialogWaitTime}ms 内出现「{dialogName}」对话框");
            }

            progress?.Report($"已打开「{dialogName}」，勾选「{settings.ThickPlateEnableCheckboxName}」");
            await EnableThickPlateModeAsync(settings, dialog, cancellationToken, progress).ConfigureAwait(false);
            await SelectThickPlateCombinationModeAsync(settings, dialog, cancellationToken, progress)
                .ConfigureAwait(false);
            await ConfirmThickPlateDialogAsync(settings, dialog, cancellationToken, progress).ConfigureAwait(false);

            await UIAutomationHelper.WaitForElementDisappearAsync(
                    () => FindThickPlateDialog(mainWindow, dialogName),
                    TimeSpan.FromMilliseconds(settings.ThickPlateDialogWaitTime),
                    cancellationToken,
                    progress,
                    $"等待「{dialogName}」关闭...")
                .ConfigureAwait(false);

            progress?.Report($"「{dialogName}」已关闭");
        }

        private async Task EnableThickPlateModeAsync(
            CTAutoSlicerSettings settings,
            AutomationElement dialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var checkboxName = settings.ThickPlateEnableCheckboxName.Trim();
            var checkbox = UIAutomationHelper.FindDescendantByName(dialog, ControlType.CheckBox, checkboxName);

            if (checkbox != null && UIAutomationHelper.TryToggleCheckBox(checkbox, targetOn: true))
            {
                if (UIAutomationHelper.IsCheckBoxOn(checkbox))
                {
                    return;
                }
            }

            if (checkbox != null)
            {
                progress?.Report("TogglePattern 未生效，点击复选框中心");
                await NativeClickElementAsync(checkbox, cancellationToken).ConfigureAwait(false);
                if (UIAutomationHelper.IsCheckBoxOn(checkbox))
                {
                    return;
                }
            }

            if (settings.ThickPlateCheckboxFallbackOffsetX != 0
                || settings.ThickPlateCheckboxFallbackOffsetY != 0)
            {
                var rect = dialog.Current.BoundingRectangle;
                var x = (int)rect.Left + settings.ThickPlateCheckboxFallbackOffsetX;
                var y = (int)rect.Top + settings.ThickPlateCheckboxFallbackOffsetY;
                progress?.Report($"使用复选框兜底偏移点击 ({x},{y})");
                await NativeClickScreenAsync(settings, x, y, cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new InvalidOperationException($"未能勾选「{checkboxName}」");
        }

        private async Task SelectThickPlateCombinationModeAsync(
            CTAutoSlicerSettings settings,
            AutomationElement dialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var modeName = settings.ThickPlateCombinationModeName.Trim();
            progress?.Report($"选择组合模式「{modeName}」");

            var comboBox = UIAutomationHelper.FindFirstDescendantByControlType(dialog, ControlType.ComboBox);
            if (comboBox != null)
            {
                UIAutomationHelper.TryExpandComboBox(comboBox);
            }
            else if (settings.ThickPlateComboFallbackOffsetX != 0
                     || settings.ThickPlateComboFallbackOffsetY != 0)
            {
                var rect = dialog.Current.BoundingRectangle;
                var x = (int)rect.Left + settings.ThickPlateComboFallbackOffsetX;
                var y = (int)rect.Top + settings.ThickPlateComboFallbackOffsetY;
                await NativeClickScreenAsync(settings, x, y, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var comboGroup = UIAutomationHelper.FindDescendantByName(
                    dialog,
                    ControlType.Group,
                    settings.ThickPlateEnableCheckboxName);
                if (comboGroup != null)
                {
                    await ClickElementCenterAsync(comboGroup).ConfigureAwait(false);
                }
            }

            var listItem = await UIAutomationHelper.WaitForElementAsync(
                    () => UIAutomationHelper.FindVisibleDescendantByName(dialog, ControlType.ListItem, modeName)
                        ?? UIAutomationHelper.FindVisibleDescendantByName(
                            AutomationElement.RootElement,
                            ControlType.ListItem,
                            modeName),
                    TimeSpan.FromMilliseconds(settings.ThickPlateDropdownWaitTime),
                    cancellationToken,
                    pollIntervalMs: 100)
                .ConfigureAwait(false);

            if (listItem == null)
            {
                throw new InvalidOperationException($"未找到组合模式列表项「{modeName}」");
            }

            await SelectThickPlateListItemAsync(
                    settings,
                    dialog,
                    listItem,
                    modeName,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Qt 下拉：Win32 点击 + 回车提交；不以 UIA 组合框读数作为失败依据（真机读数常滞后或与界面不一致）。
        /// </summary>
        private async Task SelectThickPlateListItemAsync(
            CTAutoSlicerSettings settings,
            AutomationElement dialog,
            AutomationElement listItem,
            string modeName,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            listItem.SetFocus();
            UIAutomationHelper.TryScrollListItemIntoView(listItem);

            UIAutomationHelper.TrySelectListItem(listItem);
            UIAutomationHelper.TryInvokeElement(listItem);

            progress?.Report($"点击并回车确认组合模式「{modeName}」");
            _logger.Info($"厚板组合模式：Win32 点击并回车确认「{modeName}」");
            await NativeClickElementAsync(listItem, cancellationToken).ConfigureAwait(false);
            await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
            await WaitUntilDisappearedAsync(
                    () => UIAutomationHelper.FindVisibleDescendantByName(
                        AutomationElement.RootElement,
                        ControlType.ListItem,
                        modeName),
                    settings.ThickPlateDropdownWaitTime,
                    cancellationToken)
                .ConfigureAwait(false);

            LogThickPlateComboUiaState(dialog, modeName);
            progress?.Report($"已提交组合模式「{modeName}」，继续确认对话框");
        }

        /// <summary>
        /// 仅记录 UIA 诊断信息，不参与成败判定。
        /// </summary>
        private void LogThickPlateComboUiaState(AutomationElement dialog, string modeName)
        {
            var comboBox = UIAutomationHelper.FindFirstDescendantByControlType(dialog, ControlType.ComboBox);
            if (comboBox == null)
            {
                _logger.Info($"厚板组合模式 UIA 诊断：未找到 ComboBox（目标「{modeName}」）");
                return;
            }

            var displayText = UIAutomationHelper.GetComboBoxDisplayText(comboBox);
            var expanded = UIAutomationHelper.IsComboBoxExpanded(comboBox);
            var visibleItem = UIAutomationHelper.FindVisibleDescendantByName(
                AutomationElement.RootElement,
                ControlType.ListItem,
                modeName);
            _logger.Info(
                $"厚板组合模式 UIA 诊断：读数「{displayText}」展开={expanded} " +
                $"可见列表项={(visibleItem != null ? "是" : "否")}（目标「{modeName}」）");
        }

        private async Task ConfirmThickPlateDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement dialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("确认厚板设置对话框");
            var okButton = UIAutomationHelper.FindButtonByName(dialog, ThickPlateOkButtonName);
            if (okButton != null && UIAutomationHelper.TryInvokeButton(okButton))
            {
                return;
            }

            if (okButton != null)
            {
                await NativeClickElementAsync(okButton, cancellationToken).ConfigureAwait(false);
                return;
            }

            await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
        }

        private async Task NativeClickScreenAsync(
            CTAutoSlicerSettings settings,
            int x,
            int y,
            CancellationToken cancellationToken)
        {
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(() => WinAPIHelper.ClickScreen(x, y)).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        private static AutomationElement? FindThickPlateDialog(AutomationElement mainWindow, string dialogName)
        {
            return UIAutomationHelper.FindWindowByName(mainWindow, dialogName)
                ?? UIAutomationHelper.FindWindowByName(AutomationElement.RootElement, dialogName);
        }

        private async Task ExportImageStackSlicesAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (string.IsNullOrWhiteSpace(settings.SliceExportFolder))
            {
                throw new InvalidOperationException("请先配置切片导出目录");
            }

            Directory.CreateDirectory(settings.SliceExportFolder);

            var exportBaseName = SliceExportNamingHelper.BuildExportBaseName(Path.GetFileName(vglPath));
            var quantity = SliceExportNamingHelper.ResolveExportQuantity(
                Path.GetFileName(vglPath),
                settings.SliceExportQuantity,
                settings.SliceExportQuantityQj);

            if (SliceExportNamingHelper.IsGlobalImage(Path.GetFileName(vglPath)))
            {
                progress?.Report($"{SliceExportQtyProgressPrefix}{quantity}");
                _logger.Info($"检测到 QJ 全局图像，导出数量同步为 {quantity}");
            }

            var viewExports = new (string ViewListItemName, string ViewSuffix)[]
            {
                (settings.SliceExportViewFrontName.Trim(), "前"),
                (settings.SliceExportViewRightName.Trim(), "右"),
                (settings.SliceExportViewTopName.Trim(), "上"),
            };

            progress?.Report($"开始三视图切片导出，基础名「{exportBaseName}」，数量 {quantity}");
            _logger.Info($"三视图切片导出开始：{exportBaseName}，数量={quantity}");

            for (var i = 0; i < viewExports.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (viewListItemName, viewSuffix) = viewExports[i];
                var exportSavePath = SliceExportNamingHelper.BuildExportSavePath(
                    settings.SliceExportFolder,
                    exportBaseName,
                    viewSuffix);
                var exportFolder = Path.GetDirectoryName(exportSavePath) ?? settings.SliceExportFolder;
                progress?.Report(
                    $"导出视图 {i + 1}/3：{viewListItemName} → 文件夹「{Path.GetFileName(exportFolder)}」，图片 1~{quantity}");
                _logger.Info(
                    $"导出视图 {i + 1}/3：{viewListItemName}，文件夹={exportFolder}，图片命名 1~{quantity}");

                await ExportSingleViewSliceAsync(
                        settings,
                        ctViewerProcess,
                        viewListItemName,
                        exportSavePath,
                        quantity,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);
            }

            progress?.Report("三视图切片导出完成");
            _logger.Info("三视图切片导出完成");
        }

        private async Task ExportSingleViewSliceAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string viewListItemName,
            string exportSavePathWithoutExt,
            int quantity,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
            await OpenSaveImageStackDialogAsync(settings, ctViewerProcess, cancellationToken, progress)
                .ConfigureAwait(false);

            var mainWindow = GetFreshMainWindow(ctViewerProcess);
            var exportDialog = FindSliceExportDialog(mainWindow)
                ?? throw new InvalidOperationException("未找到「保存影片/图像堆栈」导出对话框");

            await SelectSliceExportViewAsync(
                    settings,
                    exportDialog,
                    viewListItemName,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await FillSliceExportFormAsync(
                    settings,
                    exportDialog,
                    quantity,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await ConfirmSliceSaveAsync(
                    settings,
                    ctViewerProcess,
                    exportDialog,
                    exportSavePathWithoutExt,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await WaitForExportWriteCompleteAsync(
                    ctViewerProcess,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            await WaitForSliceExportDialogClosedAsync(
                    ctViewerProcess,
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task OpenSaveImageStackDialogAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ExecuteStepWithRetryAsync(
                    "打开保存影片/图像堆栈",
                    async attempt =>
                    {
                        progress?.Report(attempt == 1
                            ? "Alt+F+T 打开保存影片/图像堆栈"
                            : $"打开保存影片/图像堆栈（第 {attempt} 次重试）");
                        _logger.Info(attempt == 1
                            ? "Alt+F+T 打开保存影片/图像堆栈"
                            : $"打开保存影片/图像堆栈第 {attempt} 次重试");
                        await InvokeOnUiThreadAsync(SimulateAltFt).ConfigureAwait(false);

                        var mainWindow = GetFreshMainWindow(ctViewerProcess);
                        return await UIAutomationHelper.WaitForElementAsync(
                                () => FindSliceExportDialog(mainWindow),
                                TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                                cancellationToken,
                                UiPollIntervalMs)
                            .ConfigureAwait(false);
                    },
                    dialog => dialog != null,
                    settings,
                    cancellationToken,
                    progress,
                    maxRetriesOverride: settings.StepRetryCount)
                .ConfigureAwait(false);
        }

        private static AutomationElement? FindSliceExportDialog(AutomationElement mainWindow)
        {
            return UIAutomationHelper.FindVisibleWindowByNamePrefix(
                       mainWindow,
                       SliceExportDialogNamePrefix,
                       requireImageKeyword: true)
                   ?? UIAutomationHelper.FindVisibleWindowByNamePrefix(
                       AutomationElement.RootElement,
                       SliceExportDialogNamePrefix,
                       requireImageKeyword: true);
        }

        private async Task SelectSliceExportViewAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            string viewListItemName,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var viewGroup = UIAutomationHelper.FindGroupByName(exportDialog, SliceExport2DViewGroupName)
                ?? exportDialog;
            var comboBox = UIAutomationHelper.FindDescendantByName(viewGroup, ControlType.ComboBox, SliceExport2DViewComboName)
                ?? UIAutomationHelper.FindFirstDescendantByControlType(viewGroup, ControlType.ComboBox)
                ?? throw new InvalidOperationException("未找到「2D 视图」组合框");

            progress?.Report($"选择 2D 视图「{viewListItemName}」");
            _logger.Info($"选择 2D 视图「{viewListItemName}」");

            await OpenRenderingPresetComboBoxAsync(
                    comboBox,
                    settings.PresetDropdownWaitTime,
                    cancellationToken)
                .ConfigureAwait(false);

            var listItem = await UIAutomationHelper.WaitForElementAsync(
                    () => UIAutomationHelper.FindVisibleDescendantByName(
                        comboBox,
                        ControlType.ListItem,
                        viewListItemName,
                        exactMatch: true)
                        ?? UIAutomationHelper.FindVisibleDescendantByName(
                            exportDialog,
                            ControlType.ListItem,
                            viewListItemName,
                            exactMatch: true),
                    TimeSpan.FromMilliseconds(settings.PresetDropdownWaitTime),
                    cancellationToken,
                    pollIntervalMs: 200)
                .ConfigureAwait(false);

            if (listItem == null)
            {
                throw new InvalidOperationException($"未找到 2D 视图列表项「{viewListItemName}」");
            }

            await SelectPresetListItemAsync(
                    listItem,
                    comboBox,
                    viewListItemName,
                    settings.PresetDropdownWaitTime,
                    cancellationToken)
                .ConfigureAwait(false);
            await WaitForPresetAppliedAsync(
                    comboBox,
                    viewListItemName,
                    settings.ThickPlateStepDelayMs,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private async Task FillSliceExportFormAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            int quantity,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var imageSizeEdits = UIAutomationHelper.FindEditsInGroup(exportDialog, SliceExportImageSizeGroupName);
            if (imageSizeEdits.Count == 0)
            {
                throw new InvalidOperationException("未找到「图像大小」宽度输入框");
            }

            var widthValue = settings.SliceExportImageWidth.ToString();
            if (!await SetEditValueWithFallbackAsync(
                    imageSizeEdits[0],
                    widthValue,
                    cancellationToken,
                    commitWithEnter: false).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置图像宽度为 {widthValue}");
            }

            progress?.Report($"已设置图像宽度 {widthValue}");
            _logger.Info($"已设置图像宽度 {widthValue}");

            var quantityEdit = UIAutomationHelper.FindEditAdjacentToLabel(
                exportDialog,
                SliceExportQuantityLabelKeyword,
                SliceExportStackSettingsGroupName);
            if (quantityEdit == null)
            {
                throw new InvalidOperationException(
                    $"未找到「{SliceExportQuantityLabelKeyword}」旁的输入框（Group「{SliceExportStackSettingsGroupName}」内 Text「{SliceExportQuantityLabelKeyword}：」+ Custom 输入框）");
            }

            var quantityValue = quantity.ToString();
            if (!await SetEditValueWithFallbackAsync(
                    quantityEdit,
                    quantityValue,
                    cancellationToken,
                    commitWithEnter: true).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置导出数量为 {quantityValue}");
            }

            progress?.Report($"已设置导出数量 {quantityValue}");
            _logger.Info($"已设置导出数量 {quantityValue}");

            // TODO: 待视觉算法返回切片范围，当前使用配置默认值
            var startEdit = UIAutomationHelper.FindEditAdjacentToLabel(
                exportDialog,
                SliceExportRangeStartLabelKeyword,
                SliceExportSliceRangeGroupName);
            var endEdit = UIAutomationHelper.FindEditAdjacentToLabel(
                exportDialog,
                SliceExportRangeEndLabelKeyword,
                SliceExportSliceRangeGroupName);
            if (startEdit == null || endEdit == null)
            {
                throw new InvalidOperationException(
                    $"未找到「{SliceExportSliceRangeGroupName}」开始/结束输入框（Group 内 Text「开始 [mm]」「结束 [mm]」+ Custom 输入框）");
            }
            var startValue = settings.SliceRangeStartMm.ToString("0.##");
            var endValue = settings.SliceRangeEndMm.ToString("0.##");
            if (!await SetEditValueWithFallbackAsync(
                    startEdit,
                    startValue,
                    cancellationToken,
                    commitWithEnter: true).ConfigureAwait(false)
                || !await SetEditValueWithFallbackAsync(
                    endEdit,
                    endValue,
                    cancellationToken,
                    commitWithEnter: true).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置切片范围 {startValue} ~ {endValue} mm");
            }

            progress?.Report($"已设置切片范围 {startValue} ~ {endValue} mm（TODO: 待视觉算法）");
            _logger.Info($"已设置切片范围 {startValue} ~ {endValue} mm");
        }

        private async Task<bool> SetEditValueWithFallbackAsync(
            AutomationElement edit,
            string value,
            CancellationToken cancellationToken,
            bool commitWithEnter = false)
        {
            var isCustom = edit.Current.ControlType == ControlType.Custom;

            if (!isCustom
                && UIAutomationHelper.TrySetEditValueSafe(edit, value)
                && InputValueMatches(UIAutomationHelper.TryGetInputValue(edit), value))
            {
                return true;
            }

            await SetEditValueWithKeyboardFallbackAsync(edit, value, cancellationToken, commitWithEnter)
                .ConfigureAwait(false);
            if (await WaitUntilAsync(
                        () => InputValueMatches(UIAutomationHelper.TryGetInputValue(edit), value),
                        2000,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return true;
            }

            if (InputValueMatches(UIAutomationHelper.TryGetInputValue(edit), value))
            {
                return true;
            }

            // VG Custom 数值框常无法通过 UIA 读回，完成双击+全选+输入+Enter 后视为已提交
            return isCustom && commitWithEnter;
        }

        private async Task ConfirmSliceSaveAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement exportDialog,
            string exportSavePathWithoutExt,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!UIAutomationHelper.TryInvokeButtonByName(exportDialog, SliceExportSaveButtonName, exactMatch: true))
            {
                throw new InvalidOperationException("未能点击「保存...」按钮");
            }

            progress?.Report("已点击保存，等待系统保存对话框");
            _logger.Info("已点击保存，等待系统保存对话框");

            var fileEdit = await UIAutomationHelper.WaitForElementAsync(
                    () => UIAutomationHelper.FindVisibleFileNameEdit(AutomationElement.RootElement),
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (fileEdit == null)
            {
                throw new InvalidOperationException("未在超时内检测到系统保存对话框");
            }

            await FillSystemSaveDialogAsync(settings, exportSavePathWithoutExt, cancellationToken, progress)
                .ConfigureAwait(false);
            await DismissImageQualityDialogAsync(settings, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task FillSystemSaveDialogAsync(
            CTAutoSlicerSettings settings,
            string fullPathWithoutExtension,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var directory = Path.GetDirectoryName(fullPathWithoutExtension) ?? string.Empty;
            Directory.CreateDirectory(directory);

            await ExecuteStepWithRetryAsync(
                    "填写系统保存对话框",
                    async _ =>
                    {
                        var fileEdit = UIAutomationHelper.FindVisibleFileNameEdit(AutomationElement.RootElement);
                        if (fileEdit == null)
                        {
                            return false;
                        }

                        var saveDialog = UIAutomationHelper.FindAncestorDialogOrWindow(fileEdit);
                        progress?.Report($"保存路径：{fullPathWithoutExtension}");
                        _logger.Info($"保存路径：{fullPathWithoutExtension}");

                        await SetSaveDialogFileNameAsync(
                                fileEdit,
                                fullPathWithoutExtension,
                                settings.SliceExportDialogWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);

                        if (saveDialog != null)
                        {
                            if (!UIAutomationHelper.TryInvokeButtonByName(saveDialog, "保存(S)", exactMatch: false)
                                && !UIAutomationHelper.TryInvokeButtonByName(saveDialog, "保存", exactMatch: false))
                            {
                                await InvokeOnUiThreadAsync(SimulateAltS).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await InvokeOnUiThreadAsync(SimulateAltS).ConfigureAwait(false);
                        }

                        return await WaitUntilDisappearedAsync(
                                () => UIAutomationHelper.FindVisibleFileNameEdit(AutomationElement.RootElement),
                                settings.SliceExportDialogWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress,
                    maxRetriesOverride: settings.StepRetryCount)
                .ConfigureAwait(false);
        }

        private async Task SetSaveDialogFileNameAsync(
            AutomationElement fileEdit,
            string fullPathWithoutExtension,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            await NativeClickElementAsync(fileEdit, cancellationToken).ConfigureAwait(false);
            await WaitUntilAsync(
                    () =>
                    {
                        try
                        {
                            return fileEdit.Current.HasKeyboardFocus;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    timeoutMs,
                    cancellationToken)
                .ConfigureAwait(false);
            await InvokeOnUiThreadAsync(SimulateAltN).ConfigureAwait(false);

            if (!UIAutomationHelper.TrySetEditValueSafe(fileEdit, fullPathWithoutExtension))
            {
                await SetEditValueWithKeyboardFallbackAsync(
                        fileEdit,
                        fullPathWithoutExtension,
                        cancellationToken,
                        commitWithEnter: false)
                    .ConfigureAwait(false);
            }

            var actual = UIAutomationHelper.TryGetInputValue(fileEdit);
            if (!InputValueMatchesPath(actual, fullPathWithoutExtension))
            {
                _logger.Info(
                    $"保存对话框文件名读回「{actual ?? "(空)"}」，与期望「{fullPathWithoutExtension}」不完全一致，继续点击保存");
            }
        }

        private static AutomationElement? FindSystemSaveDialog()
        {
            var fileEdit = UIAutomationHelper.FindVisibleFileNameEdit(AutomationElement.RootElement);
            return fileEdit == null
                ? null
                : UIAutomationHelper.FindAncestorDialogOrWindow(fileEdit);
        }

        private async Task DismissImageQualityDialogAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("等待 JPEG 质量对话框");
            _logger.Info("等待 JPEG 质量对话框");

            var qualityDialog = await UIAutomationHelper.WaitForElementAsync(
                    FindImageQualityDialog,
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken,
                    pollIntervalMs: 300)
                .ConfigureAwait(false);

            if (qualityDialog == null)
            {
                _logger.Warn("未检测到 JPEG 质量对话框，继续流程");
                return;
            }

            await ConfirmJpegQualityDialogAsync(settings, qualityDialog, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task ConfirmJpegQualityDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement qualityDialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var okButton = UIAutomationHelper.FindButtonByName(
                    qualityDialog,
                    SliceExportQualityConfirmButtonName,
                    exactMatch: true)
                ?? UIAutomationHelper.FindButtonByName(
                    qualityDialog,
                    SliceExportQualityConfirmButtonName,
                    exactMatch: false);

            if (okButton != null && UIAutomationHelper.TryInvokeButton(okButton))
            {
                progress?.Report("JPEG 质量：Invoke「确定」");
                _logger.Info("JPEG 质量：Invoke「确定」");
                if (await WaitUntilDisappearedAsync(
                            FindImageQualityDialog,
                            settings.SliceExportDialogWaitTime,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    progress?.Report("JPEG 质量对话框已确认");
                    _logger.Info("JPEG 质量对话框已确认");
                    return;
                }
            }

            if (okButton != null)
            {
                progress?.Report("JPEG 质量：Invoke 无效，Win32 点击「确定」");
                _logger.Info("JPEG 质量：Invoke 无效，Win32 点击「确定」");
                await NativeClickElementAsync(okButton, cancellationToken).ConfigureAwait(false);
                if (await WaitUntilDisappearedAsync(
                            FindImageQualityDialog,
                            settings.SliceExportDialogWaitTime,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    progress?.Report("JPEG 质量对话框已确认");
                    _logger.Info("JPEG 质量对话框已确认");
                    return;
                }
            }

            progress?.Report("JPEG 质量：点击无效，Enter 确认（AccessKey）");
            _logger.Info("JPEG 质量：点击无效，Enter 确认（AccessKey）");
            await FocusAutomationWindowAsync(qualityDialog, cancellationToken).ConfigureAwait(false);
            await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);

            if (!await WaitUntilDisappearedAsync(
                        FindImageQualityDialog,
                        settings.SliceExportDialogWaitTime,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new InvalidOperationException("未能关闭 JPEG 质量对话框（已尝试 Invoke / Win32 点击 / Enter）");
            }

            progress?.Report("JPEG 质量对话框已确认");
            _logger.Info("JPEG 质量对话框已确认");
        }

        private async Task FocusAutomationWindowAsync(
            AutomationElement element,
            CancellationToken cancellationToken)
        {
            var targetWindow = ResolveNativeWindowHandle(element);
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    if (targetWindow != IntPtr.Zero)
                    {
                        WinAPIHelper.SetForegroundWindow(targetWindow);
                    }

                    element.SetFocus();
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            await WaitUntilAsync(
                    () =>
                    {
                        try
                        {
                            return element.Current.HasKeyboardFocus;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    1000,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private static AutomationElement? FindImageQualityDialog()
        {
            return UIAutomationHelper.FindDescendantWindowByNameKeyword(
                    AutomationElement.RootElement,
                    SliceExportJpegQualityWindowName,
                    requireButtonName: SliceExportQualityConfirmButtonName)
                ?? UIAutomationHelper.FindDescendantWindowByNameKeyword(
                    AutomationElement.RootElement,
                    "JPEG",
                    requireButtonName: SliceExportQualityConfirmButtonName);
        }

        private static AutomationElement? FindVisibleExportWriteProgressWindow(AutomationElement mainWindow)
        {
            try
            {
                var progressWindow = FindVisibleImportProgressWindow(mainWindow);
                if (progressWindow == null)
                {
                    return null;
                }

                return UIAutomationHelper.ContainsDescendantText(progressWindow, SliceExportWriteProgressText)
                    ? progressWindow
                    : null;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private async Task WaitForExportWriteCompleteAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            const int pollIntervalMs = 500;
            var progressWindow = await UIAutomationHelper.WaitForElementAsync(
                    () =>
                    {
                        try
                        {
                            return FindVisibleExportWriteProgressWindow(GetFreshMainWindow(ctViewerProcess));
                        }
                        catch
                        {
                            return null;
                        }
                    },
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken,
                    pollIntervalMs)
                .ConfigureAwait(false);

            if (progressWindow == null)
            {
                _logger.Info(
                    $"未在 {settings.SliceExportDialogWaitTime}ms 内检测到「正在写入图像」进度框，视为快速完成");
                return;
            }

            progress?.Report("正在写入图像，等待导出完成...");
            _logger.Info("检测到「正在写入图像」进度框，等待消失");

            var deadline = DateTime.UtcNow.AddMilliseconds(settings.SliceExportWriteTimeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AutomationElement? visibleProgress;
                try
                {
                    visibleProgress = FindVisibleExportWriteProgressWindow(GetFreshMainWindow(ctViewerProcess));
                }
                catch (Exception ex)
                {
                    _logger.Warn($"刷新导出写入进度框失败：{GetFullExceptionMessage(ex)}");
                    await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (visibleProgress == null)
                {
                    progress?.Report("图像写入完成");
                    _logger.Info("导出写入进度框已消失");
                    return;
                }

                progress?.Report("正在写入图像...");
                await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待导出写入完成超时（{settings.SliceExportWriteTimeoutMs} ms）：「请耐心等待.../正在写入图像」进度框未消失");
        }

        private static async Task WaitForSliceExportDialogClosedAsync(
            Process ctViewerProcess,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            await UIAutomationHelper.WaitUntilDisappearedAsync(
                    () =>
                    {
                        try
                        {
                            return FindSliceExportDialog(GetFreshMainWindow(ctViewerProcess));
                        }
                        catch
                        {
                            return null;
                        }
                    },
                    timeout,
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);
        }

        public async Task TestSliceExportAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                if (string.IsNullOrWhiteSpace(settings.SliceExportFolder))
                {
                    throw new InvalidOperationException("请先配置切片导出目录");
                }

                var vglFiles = GetVglFiles(settings.CTFileFolder);
                if (vglFiles.Count == 0)
                {
                    throw new InvalidOperationException("CT 体素文件夹内未找到 .vgl 文件（用于命名规则）");
                }

                var vglPath = vglFiles[0];
                progress?.Report($"切片导出单步测试：请确保 VG 已导入体积、已应用预设并完成厚板设置；命名参考 {Path.GetFileName(vglPath)}");
                _logger.Info($"切片导出单步测试开始，参考文件 {vglPath}");

                var ctViewerProcess = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                EnsureVgForeground(ctViewerProcess);

                await ExportImageStackSlicesAsync(
                        settings,
                        ctViewerProcess,
                        vglPath,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("切片导出单步测试完成");
                _logger.Info("切片导出单步测试完成");
            }
            finally
            {
                Unlock();
            }
        }

        public async Task TestRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                var presetName = settings.RenderingPresetName.Trim();
                if (string.IsNullOrEmpty(presetName))
                {
                    throw new InvalidOperationException("请先填写渲染预设名称");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report("渲染预设单步测试：请确保 VG 中已导入体积");
                _logger.Info($"渲染预设单步测试开始，目标预设「{presetName}」");

                var ctViewerProcess = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                await ApplyRenderingPresetAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("渲染预设单步测试完成");
                _logger.Info("渲染预设单步测试完成");
            }
            finally
            {
                Unlock();
            }
        }

        public async Task<Process> GetCTViewerProcess(CTAutoSlicerSettings settings, CancellationToken cancellationToken = default)
        {
            var processName = Path.GetFileNameWithoutExtension(settings.VGAppPath);
            if (string.IsNullOrWhiteSpace(processName))
            {
                throw new InvalidOperationException("无法从 VG 应用路径解析进程名");
            }

            var processes = Process.GetProcessesByName(processName);
            if (processes.Length > 0)
            {
                return processes[0];
            }

            return Process.Start(new ProcessStartInfo(settings.VGAppPath)
            {
                UseShellExecute = true
            }) ?? throw new InvalidOperationException($"无法启动 VG 应用：{settings.VGAppPath}");
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
