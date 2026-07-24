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
        /// <summary>启动阶段轮询「自动保存恢复」/「文件(F)」的间隔</summary>
        private const int VgStartupPollIntervalMs = 500;
        /// <summary>导入等步骤中的通用轮询间隔</summary>
        private const int VgReadyPollIntervalMs = 1000;
        private const string QuickImportDialogNamePrefix = "快速导入";
        private const string QuickImportEditName = "文件名(N):";
        private const string QuickImportOpenButtonName = "打开(O)";
        private const string QuickImportOpenButtonNameFallback = "打开";
        private const string FileMenuItemName = "文件(F)";
        private const string FileSelectionFinishButtonName = "完成(F)";
        private const string ImportProgressWindowName = "请耐心等待...";
        /// <summary>导入进度框内文（Insights：Text Name=正在导入体积），比仅认窗口标题更稳。</summary>
        private const string ImportProgressStatusText = "正在导入体积";
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

        private const string Simple321MenuItemName = "简单 3-2-1 对齐(S)...";
        private const string Simple321MenuItemNameContains = "简单 3-2-1 对齐";
        private const string Simple321DialogNamePrefix = "简单 3-2-1 对齐";
        private const string Simple321FinishButtonName = "完成(F)";
        /// <summary>标点前先重置，避免残留对齐状态（Insights：Name = 重置(R)，AccessKey Alt+R）</summary>
        private const string Simple321ResetButtonName = "重置(R)";
        /// <summary>对齐完成后可能弹出的确认窗（Insights：Name = VGSTUDIO MAX 警告）</summary>
        private const string VgAlignedWarningDialogName = "VGSTUDIO MAX 警告";
        /// <summary>警告窗「是」按钮（Insights：Name = 是(Y)，AccessKey Alt+Y）</summary>
        private const string VgAlignedWarningYesButtonName = "是(Y)";

        private const int UiPollIntervalMs = UIAutomationHelper.DefaultConditionPollIntervalMs;
        private const int OffscreenProgressBypassMs = 2000;
        private const int ProcessMainWindowTimeoutMs = 10000;
        /// <summary>Qt 预设下拉：每种打开方式的最大等待，失败立即试下一方式（避免每步耗满 PresetDropdownWaitTime）。</summary>
        private const int PresetDropdownOpenStepWaitMs = 800;
        private const int PresetListItemPollIntervalMs = 100;
        /// <summary>Win32 点击列表项后等待下拉关闭的上限。</summary>
        private const int PresetDropdownCloseWaitMs = 1500;
        /// <summary>切片导出：单字段 Enter 提交后等待 VG/Qt 消化读数（Custom 框 UIA 常滞后）。</summary>
        private const int SliceExportFieldCommitDelayMs = 300;
        /// <summary>进程池调度：无 UI 动作时的空闲轮询间隔。</summary>
        private const int PoolIdlePollIntervalMs = 1000;

        /// <summary>
        /// 后处理独占计数：&gt;0 时后台导入等待暂停 UIA，避免抢焦点打断 LUT/厚板/3-2-1/导出。
        /// </summary>
        private int _postPipelineExclusiveCount;

        private readonly ILoggerService<VGAutomationService> _logger;
        private readonly IAngleAlgorithmService _angleAlgorithmService;
        private readonly ICutImgAlgorithmService _cutImgAlgorithmService;
        private readonly EventSimulator _simulator;
        /// <summary>
        /// 可能于任意时刻弹出的中断窗口队列。步骤异常/失败时先 Drain，处理后重试该步骤。
        /// 后续可继续 Register 其它弹窗（保存提示等）。
        /// </summary>
        private readonly List<InterruptDialogHandler> _interruptDialogQueue = new();
        private Process? _activeVgProcess;

        /// <summary>进程池槽状态。</summary>
        private enum VgSlotState
        {
            Importing,
            ImportDone,
            Failed
        }

        /// <summary>
        /// 体素文件夹排队条目：ImportPath 传给 VG；QueueEntryPath 为文件夹内条目（真实文件或 .lnk），成功后删除。
        /// </summary>
        private sealed class VolumeQueueItem
        {
            public required string ImportPath { get; init; }
            public required string QueueEntryPath { get; init; }
        }

        /// <summary>单个 VG 进程槽：绑定文件路径与导入等待任务。</summary>
        private sealed class VgProcessSlot
        {
            public required Process Process { get; init; }
            /// <summary>传给 VG 的实际体积路径（快捷方式已解析为目标 .vgl/.vgi）。</summary>
            public required string FilePath { get; init; }
            /// <summary>体素文件夹内排队条目路径；成功后删除此项（快捷方式时勿删源文件）。</summary>
            public required string QueueEntryPath { get; init; }
            public VgSlotState State { get; set; } = VgSlotState.Importing;
            public Task? WaitTask { get; set; }
            public Exception? Error { get; set; }
            public DateTime ImportStartedUtc { get; set; } = DateTime.UtcNow;
        }

        public VGAutomationService(
            ILoggerService<VGAutomationService> logger,
            IAngleAlgorithmService angleAlgorithmService,
            ICutImgAlgorithmService cutImgAlgorithmService)
        {
            _logger = logger;
            _angleAlgorithmService = angleAlgorithmService;
            _cutImgAlgorithmService = cutImgAlgorithmService;
            _simulator = new EventSimulator();
            RegisterDefaultInterruptDialogs();
        }

        /// <summary>中断弹窗处理器：查找 + 关闭动作。</summary>
        private sealed class InterruptDialogHandler
        {
            public required string Name { get; init; }
            public required Func<Process, AutomationElement?> Find { get; init; }
            public required Func<Process, AutomationElement, CancellationToken, IProgress<string>?, Task<bool>> HandleAsync { get; init; }
        }

        private void RegisterDefaultInterruptDialogs()
        {
            _interruptDialogQueue.Add(new InterruptDialogHandler
            {
                Name = AutoSaveRecoveryDialogWindowName,
                Find = FindAutoSaveRecoveryDialog,
                HandleAsync = HandleAutoSaveRecoveryDialogAsync
            });
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
                        // 静默取窗，避免后台轮询抢焦点
                        var window = TryGetMainWindowElement(ctViewerProcess);
                        return window != null && IsRenderingWorkspaceReady(window, settings);
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

        private Task<bool> WaitForComboDropdownOpenAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                timeoutMs,
                cancellationToken,
                PresetListItemPollIntervalMs);

        private Task<bool> WaitForComboDropdownClosedAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => !UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                timeoutMs,
                cancellationToken,
                PresetListItemPollIntervalMs);

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
            FindWindowByNameScoped(mainWindow, SaveDialogWindowName);

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

            var volumeItems = GetVolumeQueueItems(settings.CTFileFolder);
            if (volumeItems.Count == 0)
            {
                progress?.Report("未找到 .vgl/.vgi 文件（含「.vgi - 快捷方式」）");
                _logger.Info("未找到 .vgl/.vgi 文件（含快捷方式）");
                return;
            }

            var maxSlots = settings.GetClampedMaxVgProcesses();
            var pending = new Queue<VolumeQueueItem>(volumeItems);
            var slots = new List<VgProcessSlot>();
            var successCount = 0;
            var failCount = 0;
            var totalCount = volumeItems.Count;

            progress?.Report("流程开始（VG 进程池模式）");
            _logger.Info($"流程开始：文件 {totalCount} 个，最大 VG 进程数 {maxSlots}");
            progress?.Report($"找到 {totalCount} 个体素文件（含快捷方式），最大并发 VG={maxSlots}");

            Lock();
            try
            {
                while (pending.Count > 0 || slots.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    HarvestFinishedImportWaits(slots, progress);

                    cancellationToken.ThrowIfCancellationRequested();

                    // 优先处理已导入完成的槽（后处理串行，尽快腾出内存）
                    var readySlot = slots.Find(s => s.State == VgSlotState.ImportDone);
                    if (readySlot != null)
                    {
                        var fileName = Path.GetFileName(readySlot.FilePath);
                        progress?.Report($"开始后处理：{fileName}（PID={readySlot.Process.Id}）");
                        _logger.Info($"开始后处理：{readySlot.FilePath}，PID={readySlot.Process.Id}");

                        try
                        {
                            await RunPostImportPipelineAsync(
                                    settings,
                                    readySlot.Process,
                                    readySlot.FilePath,
                                    cancellationToken,
                                    progress)
                                .ConfigureAwait(false);

                            await OnImportSucceededAsync(readySlot.QueueEntryPath).ConfigureAwait(false);
                            successCount++;
                            progress?.Report($"处理成功并已删除排队条目：{Path.GetFileName(readySlot.QueueEntryPath)}");
                            _logger.Info($"处理成功：导入={readySlot.FilePath}，已删排队条目={readySlot.QueueEntryPath}");
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
                            _logger.Error($"后处理失败（保留文件）：{readySlot.FilePath}", ex);
                        }
                        finally
                        {
                            KillVgProcess(readySlot.Process, $"后处理结束释放：{fileName}");
                            slots.Remove(readySlot);
                            _activeVgProcess = null;
                        }

                        continue;
                    }

                    // 处理导入失败槽：杀进程，保留文件
                    var failedSlot = slots.Find(s => s.State == VgSlotState.Failed);
                    if (failedSlot != null)
                    {
                        var fileName = Path.GetFileName(failedSlot.FilePath);
                        var reason = failedSlot.Error == null
                            ? "导入等待失败"
                            : GetFullExceptionMessage(failedSlot.Error);
                        failCount++;
                        progress?.Report($"{ImportFailProgressPrefix}{fileName}|{reason}");
                        _logger.Error($"导入失败（保留文件，杀进程）：{failedSlot.FilePath} — {reason}");
                        KillVgProcess(failedSlot.Process, $"导入失败释放：{fileName}");
                        slots.Remove(failedSlot);
                        continue;
                    }

                    // 槽位未满且仍有待处理文件 → 启动新 VG 并进入导入（需独占键鼠）
                    if (slots.Count < maxSlots && pending.Count > 0)
                    {
                        var volumeItem = pending.Dequeue();
                        var vglPath = volumeItem.ImportPath;
                        var fileName = Path.GetFileName(vglPath);
                        var slotIndex = successCount + failCount + slots.Count + 1;
                        progress?.Report(
                            $"启动 VG 导入 {slotIndex}/{totalCount}：{fileName}（槽位 {slots.Count + 1}/{maxSlots}）");
                        _logger.Info(
                            $"启动 VG 导入：{vglPath}（排队条目={volumeItem.QueueEntryPath}），当前槽 {slots.Count}/{maxSlots}，剩余排队 {pending.Count}");

                        Process? started = null;
                        try
                        {
                            started = await LaunchFreshVgAndStartImportAsync(
                                    settings,
                                    vglPath,
                                    cancellationToken,
                                    progress)
                                .ConfigureAwait(false);

                            var slot = new VgProcessSlot
                            {
                                Process = started,
                                FilePath = vglPath,
                                QueueEntryPath = volumeItem.QueueEntryPath,
                                State = VgSlotState.Importing,
                                ImportStartedUtc = DateTime.UtcNow
                            };
                            slots.Add(slot);
                            // 先宣告槽位占用并放行主循环填下一 VG，再挂后台等待（避免后台 UIA 抢先占日志/延迟）
                            progress?.Report(
                                $"已进入导入等待：{fileName}（PID={started.Id}，活动槽 {slots.Count}/{maxSlots}）");
                            slot.WaitTask = WaitImportInBackgroundAsync(
                                settings,
                                slot,
                                cancellationToken,
                                progress);
                        }
                        catch (OperationCanceledException)
                        {
                            KillVgProcess(started, "取消时清理启动中的 VG");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            var reason = GetFullExceptionMessage(ex);
                            progress?.Report($"{ImportFailProgressPrefix}{fileName}|{reason}");
                            _logger.Error($"启动/开始导入失败（保留文件）：{vglPath}", ex);
                            KillVgProcess(started, $"启动导入失败：{fileName}");
                            _activeVgProcess = null;
                        }

                        continue;
                    }

                    // 仅有 Importing 槽：等待任一导入完成（静默 UIA，不置顶）
                    var activeWaits = slots
                        .Where(s => s.State == VgSlotState.Importing && s.WaitTask != null)
                        .Select(s => s.WaitTask!)
                        .ToArray();
                    if (activeWaits.Length == 0)
                    {
                        break;
                    }

                    progress?.Report(
                        $"导入等待中……活动槽 {slots.Count}/{maxSlots}，排队 {pending.Count}");
                    await Task.WhenAny(
                            Task.WhenAny(activeWaits),
                            Task.Delay(PoolIdlePollIntervalMs, cancellationToken))
                        .ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                progress?.Report($"批次结束：成功 {successCount} 个，失败 {failCount} 个，共 {totalCount} 个");
                _logger.Info($"批次结束：成功 {successCount} 个，失败 {failCount} 个，共 {totalCount} 个");
            }
            catch (OperationCanceledException)
            {
                progress?.Report("流程已取消，正在清理 VG 进程池…");
                _logger.Warn("流程取消，清理全部 VG 槽位");
                throw;
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? ex.GetType().Name
                    : ex.Message;
                progress?.Report($"CT 导入流程异常：{message}");
                _logger.Error("CT 导入流程异常", ex);
            }
            finally
            {
                foreach (var slot in slots.ToList())
                {
                    KillVgProcess(slot.Process, $"批次结束清理：{Path.GetFileName(slot.FilePath)}");
                }

                slots.Clear();
                Unlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 冷启动全新 VG（不附着已有进程）→ 就绪 → 执行导入操作直至文件选择确认。
        /// 导入体积等待由后台任务按 PID 静默轮询。
        /// </summary>
        private async Task<Process> LaunchFreshVgAndStartImportAsync(
            CTAutoSlicerSettings settings,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("启动新的 VG 进程…");
            var process = StartFreshVgProcess(settings);
            _activeVgProcess = process;

            try
            {
                await WaitForProcessMainWindowAsync(process, cancellationToken).ConfigureAwait(false);
                await WaitForVgOpenAsync(process, settings, cancellationToken, progress)
                    .ConfigureAwait(false);
                await ActivateVgWindowAsync(process, settings, cancellationToken).ConfigureAwait(false);
                await StartImportOnlyAsync(settings, process, vglPath, cancellationToken, progress)
                    .ConfigureAwait(false);
                return process;
            }
            catch
            {
                _activeVgProcess = null;
                throw;
            }
        }

        /// <summary>始终 Process.Start 新实例，禁止附着已有 VG。</summary>
        private Process StartFreshVgProcess(CTAutoSlicerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.VGAppPath) || !File.Exists(settings.VGAppPath))
            {
                throw new InvalidOperationException($"VG 应用路径无效：{settings.VGAppPath}");
            }

            var started = Process.Start(new ProcessStartInfo(settings.VGAppPath)
            {
                UseShellExecute = true
            }) ?? throw new InvalidOperationException($"无法启动 VG 应用：{settings.VGAppPath}");

            _logger.Info($"已启动新 VG 进程 PID={started.Id}：{settings.VGAppPath}");
            return started;
        }

        /// <summary>
        /// 仅执行导入操作链（需焦点）：Drain → 置前 → Ctrl+N → 快速导入 → 文件选择确认。
        /// 不含体积导入等待与后处理。
        /// </summary>
        private async Task StartImportOnlyAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            _activeVgProcess = ctViewerProcess;
            await TryDrainInterruptDialogQueueAsync(ctViewerProcess, cancellationToken, progress)
                .ConfigureAwait(false);
            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
            var mainWindow = GetFreshMainWindow(ctViewerProcess);
            await PrepareNewProjectAsync(settings, ctViewerProcess, mainWindow, cancellationToken, progress)
                .ConfigureAwait(false);
            mainWindow = GetFreshMainWindow(ctViewerProcess);
            var quickImportDialog = await OpenQuickImportAsync(settings, ctViewerProcess, cancellationToken, progress)
                .ConfigureAwait(false);
            await QuickImportFillAndOpenAsync(
                    settings,
                    ctViewerProcess,
                    quickImportDialog,
                    vglPath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
            await ConfirmFileSelectionAsync(settings, ctViewerProcess, vglPath, cancellationToken, progress)
                .ConfigureAwait(false);
        }

        /// <summary>后台静默等待导入完成（不置顶）；完成后标记槽位状态。</summary>
        private async Task WaitImportInBackgroundAsync(
            CTAutoSlicerSettings settings,
            VgProcessSlot slot,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            // 立刻让出，确保主循环能先打出「已进入导入等待」并启动下一 VG
            await Task.Yield();

            var fileName = Path.GetFileName(slot.FilePath);
            try
            {
                await WaitForImportCompleteAsync(
                        settings,
                        slot.Process,
                        cancellationToken,
                        progress,
                        activateWhenDone: false)
                    .ConfigureAwait(false);
                slot.State = VgSlotState.ImportDone;
                progress?.Report($"导入体积完成，等待后处理：{fileName}（PID={slot.Process.Id}）");
                _logger.Info($"导入体积完成：{slot.FilePath}，PID={slot.Process.Id}");
            }
            catch (OperationCanceledException)
            {
                // 取消不记业务失败；主循环 finally 统一 Kill 槽位
                throw;
            }
            catch (Exception ex)
            {
                slot.State = VgSlotState.Failed;
                slot.Error = ex;
                _logger.Error($"导入等待失败：{slot.FilePath}", ex);
            }
        }

        /// <summary>收割已结束的导入等待任务状态（主循环调用）。</summary>
        private static void HarvestFinishedImportWaits(
            List<VgProcessSlot> slots,
            IProgress<string>? progress)
        {
            foreach (var slot in slots)
            {
                if (slot.State != VgSlotState.Importing || slot.WaitTask is not { IsCompleted: true })
                {
                    continue;
                }

                // WaitImportInBackgroundAsync 已写入 State/Error；此处仅处理未捕获的故障
                if (slot.WaitTask.IsFaulted && slot.State == VgSlotState.Importing)
                {
                    slot.State = VgSlotState.Failed;
                    slot.Error = slot.WaitTask.Exception?.GetBaseException()
                        ?? new InvalidOperationException("导入等待任务异常结束");
                    progress?.Report(
                        $"导入等待异常：{Path.GetFileName(slot.FilePath)}");
                }
            }
        }

        /// <summary>
        /// 导入完成后的主流程（需焦点，全局串行）：
        /// LUT 预设 → 直方图 → 厚板 → 简单 3-2-1 → 对齐后捕捉拖拽 → 三视图切片导出。
        /// 独占期间暂停其他槽位的导入 UIA 轮询，禁止切换进程焦点。
        /// </summary>
        private async Task RunPostImportPipelineAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            EnterPostPipelineExclusive(ctViewerProcess);
            try
            {
                _activeVgProcess = ctViewerProcess;
                await TryDrainInterruptDialogQueueAsync(ctViewerProcess, cancellationToken, progress)
                    .ConfigureAwait(false);
                await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);

                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                if (!string.IsNullOrWhiteSpace(settings.RenderingPresetName))
                {
                    await ApplyRenderingPresetAsync(
                            settings,
                            ctViewerProcess,
                            mainWindow,
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false);
                }

                await ApplyHistogramGrayScaleAdjustAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("直方图灰度步骤已结束，继续厚板设置");
                _logger.Info("直方图灰度步骤已结束，继续厚板设置");

                mainWindow = GetFreshMainWindow(ctViewerProcess);
                await ApplyThickPlateForQuadrantsAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                await TryRunVisionAlignmentInMainFlowAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                await TryRunPostAlignCaptureDragInMainFlowAsync(
                        settings,
                        ctViewerProcess,
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
            finally
            {
                ExitPostPipelineExclusive();
            }
        }

        /// <summary>单文件全流程（启动导入 + 等待 + 后处理）。供兼容/单测路径复用。</summary>
        private async Task ImportSingleVglAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await StartImportOnlyAsync(settings, ctViewerProcess, vglPath, cancellationToken, progress)
                .ConfigureAwait(false);
            await WaitForImportCompleteAsync(
                    settings,
                    ctViewerProcess,
                    cancellationToken,
                    progress,
                    activateWhenDone: true)
                .ConfigureAwait(false);
            await RunPostImportPipelineAsync(
                    settings,
                    ctViewerProcess,
                    vglPath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        /// <summary>强制结束 VG 进程树；失败仅记日志。</summary>
        private void KillVgProcess(Process? process, string reason)
        {
            if (process == null)
            {
                return;
            }

            try
            {
                process.Refresh();
                if (!process.HasExited)
                {
                    _logger.Info($"Kill VG PID={process.Id}：{reason}");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Kill VG 失败（{reason}）：{ex.Message}");
            }
            finally
            {
                try
                {
                    process.Dispose();
                }
                catch
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// 主流程阶段四：厚板之后、导出之前执行简单 3-2-1。
        /// 开关关闭或未标定齐全时跳过（不阻塞导出）。
        /// </summary>
        private async Task TryRunVisionAlignmentInMainFlowAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.EnableVisionAlignmentSteps)
            {
                progress?.Report("跳过阶段四视觉对齐：EnableVisionAlignmentSteps=false");
                _logger.Info("跳过阶段四视觉对齐：EnableVisionAlignmentSteps=false");
                return;
            }

            if (!settings.HasSimple321Prerequisites)
            {
                var missing = new List<string>();
                if (!settings.HasVision3dRoi)
                {
                    missing.Add("3D ROI");
                }

                if (!settings.HasVisionTopViewRoi)
                {
                    missing.Add("「上」视图 ROI");
                }

                if (!settings.HasSimple321ToolbarClick)
                {
                    missing.Add("简单对齐下拉点击位置");
                }

                if (!settings.HasSimple321OffsetEdit)
                {
                    missing.Add("三点后偏移Edit（置0）");
                }

                var reason = string.Join("、", missing);
                progress?.Report($"跳过阶段四视觉对齐：尚未标定 {reason}");
                _logger.Warn($"跳过阶段四视觉对齐：尚未标定 {reason}");
                return;
            }

            progress?.Report("开始阶段四视觉对齐（简单 3-2-1）…");
            _logger.Info("主流程执行阶段四：简单 3-2-1 对齐");

            EnsureVgForeground(ctViewerProcess);
            var mainWindow = GetFreshMainWindow(ctViewerProcess);
            await RunSimple321AlignmentAsync(
                    settings,
                    ctViewerProcess,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("阶段四视觉对齐完成，继续后续步骤");
            _logger.Info("阶段四视觉对齐完成");
        }

        /// <summary>
        /// 3-2-1 完成后、切片导出前：点击视口底部无名按钮 → 按箭头标定拖拽。
        /// </summary>
        private async Task TryRunPostAlignCaptureDragInMainFlowAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.EnablePostAlignCaptureDragSteps)
            {
                progress?.Report("跳过对齐后捕捉拖拽：EnablePostAlignCaptureDragSteps=false");
                _logger.Info("跳过对齐后捕捉拖拽：EnablePostAlignCaptureDragSteps=false");
                return;
            }

            if (!settings.HasPostAlignCaptureDragPrerequisites)
            {
                var missing = new List<string>();
                if (!settings.HasPostAlignCaptureButton)
                {
                    missing.Add("捕捉按钮坐标");
                }

                if (!settings.HasPostAlignDrag)
                {
                    missing.Add("拖拽箭头");
                }

                var reason = string.Join("、", missing);
                progress?.Report($"跳过对齐后捕捉拖拽：尚未标定 {reason}");
                _logger.Warn($"跳过对齐后捕捉拖拽：尚未标定 {reason}");
                return;
            }

            progress?.Report("开始对齐后捕捉按钮与拖拽…");
            _logger.Info("主流程：对齐后捕捉按钮 + 箭头拖拽");

            EnsureVgForeground(ctViewerProcess);
            var mainWindow = GetFreshMainWindow(ctViewerProcess);
            await RunPostAlignCaptureDragAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("对齐后捕捉拖拽完成，继续切片导出");
            _logger.Info("对齐后捕捉拖拽完成");
        }

        /// <summary>
        /// 第一步：等待 VG 打开。
        /// 判定已打开：轮询到「文件(F)」菜单，或队列中的中断弹窗（如「自动保存恢复」）。
        /// 延迟弹出的中断窗不在此死等，由后续步骤异常时 Drain 队列处理。
        /// </summary>
        private async Task WaitForVgOpenAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var waitMs = ResolveVgOpenWaitTime(settings, progress);
            var deadline = DateTime.UtcNow.AddMilliseconds(waitMs);
            var startUtc = DateTime.UtcNow;
            DateTime? lastStartupProgressUtc = null;

            progress?.Report(
                $"等待 VG 打开：轮询中断弹窗队列或「文件(F)」菜单，最大等待 {waitMs}ms");
            _logger.Info($"等待 VG 打开，最大等待 {waitMs}ms，轮询间隔 {VgStartupPollIntervalMs}ms");

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureVgForeground(ctViewerProcess);

                // 启动过程中若已出现队列弹窗，先处理（不等后续步骤异常）
                if (await TryDrainInterruptDialogQueueAsync(
                            ctViewerProcess,
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false))
                {
                    await Task.Delay(VgStartupPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (TryIsFileMenuReady(ctViewerProcess))
                {
                    var elapsedMs = (int)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                    progress?.Report($"VG 已打开（检测到「文件(F)」菜单，耗时 {elapsedMs}ms）");
                    _logger.Info($"VG 已打开，文件菜单可交互，耗时 {elapsedMs}ms");
                    return;
                }

                var waitedMs = (int)(DateTime.UtcNow - startUtc).TotalMilliseconds;
                var shouldReportStartup =
                    lastStartupProgressUtc == null
                    || (DateTime.UtcNow - lastStartupProgressUtc.Value).TotalMilliseconds >= 2000;
                if (shouldReportStartup)
                {
                    lastStartupProgressUtc = DateTime.UtcNow;
                    progress?.Report($"VG 仍在启动中（已等待 {waitedMs}/{waitMs}ms）...");
                }

                await Task.Delay(VgStartupPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"VG 在 {waitMs}ms 内未打开（未检测到中断弹窗，也未检测到「文件(F)」菜单），请增大「VG启动就绪最大等待」后重试");
        }

        /// <summary>
        /// 扫描中断弹窗队列：若存在则依次执行对应关闭步骤。
        /// 返回 true 表示本轮至少成功处理了一个弹窗。
        /// </summary>
        private async Task<bool> TryDrainInterruptDialogQueueAsync(
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var handledAny = false;

            foreach (var handler in _interruptDialogQueue)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AutomationElement? dialog;
                try
                {
                    dialog = handler.Find(ctViewerProcess);
                }
                catch (Exception ex)
                {
                    _logger.Debug($"查找中断弹窗「{handler.Name}」失败：{ex.Message}");
                    continue;
                }

                if (dialog == null)
                {
                    continue;
                }

                progress?.Report($"检测到中断弹窗「{handler.Name}」，执行队列步骤...");
                _logger.Info($"中断弹窗队列命中：「{handler.Name}」");

                try
                {
                    var handled = await handler
                        .HandleAsync(ctViewerProcess, dialog, cancellationToken, progress)
                        .ConfigureAwait(false);
                    if (handled)
                    {
                        handledAny = true;
                        progress?.Report($"中断弹窗「{handler.Name}」已处理");
                        _logger.Info($"中断弹窗「{handler.Name}」处理完成");
                    }
                    else
                    {
                        progress?.Report($"中断弹窗「{handler.Name}」处理未成功，稍后可能重试");
                        _logger.Warn($"中断弹窗「{handler.Name}」Handle 返回 false");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"处理中断弹窗「{handler.Name}」异常：{ex.Message}");
                    progress?.Report($"处理中断弹窗「{handler.Name}」异常：{ex.Message}");
                }
            }

            return handledAny;
        }

        /// <summary>队列步骤：关闭「自动保存恢复」——点击「跳过」并等待窗口消失。</summary>
        private async Task<bool> HandleAutoSaveRecoveryDialogAsync(
            Process ctViewerProcess,
            AutomationElement recoveryDialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("正在点击「自动保存恢复」→「跳过」...");
            if (!await TryClickAutoSaveSkipButtonAsync(recoveryDialog, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            progress?.Report("已点击「跳过」");
            await WaitUntilAsync(
                    () => FindAutoSaveRecoveryDialog(ctViewerProcess) == null,
                    10000,
                    cancellationToken,
                    VgStartupPollIntervalMs)
                .ConfigureAwait(false);

            return FindAutoSaveRecoveryDialog(ctViewerProcess) == null;
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

        /// <summary>
        /// 按 Accessibility Insights 树查找「自动保存恢复」：
        /// 优先在 VG 主窗口子级查找（图一位于主窗下），再查本进程其它顶级窗口。
        /// </summary>
        private static AutomationElement? FindAutoSaveRecoveryDialog(Process ctViewerProcess)
        {
            try
            {
                ctViewerProcess.Refresh();
                if (ctViewerProcess.MainWindowHandle != IntPtr.Zero)
                {
                    var mainWindow = AutomationElement.FromHandle(ctViewerProcess.MainWindowHandle);
                    var underMain = FindNamedWindowAmongChildren(mainWindow, AutoSaveRecoveryDialogWindowName);
                    if (underMain != null)
                    {
                        return underMain;
                    }
                }

                var windowCondition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ControlType.Window);
                var topWindows = AutomationElement.RootElement.FindAll(TreeScope.Children, windowCondition);
                foreach (AutomationElement topWindow in topWindows)
                {
                    try
                    {
                        if (topWindow.Current.ProcessId != ctViewerProcess.Id)
                        {
                            continue;
                        }

                        var name = topWindow.Current.Name ?? string.Empty;
                        if (name == AutoSaveRecoveryDialogWindowName)
                        {
                            return topWindow;
                        }

                        var nested = FindNamedWindowAmongChildren(topWindow, AutoSaveRecoveryDialogWindowName);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // 窗口正在关闭
                    }
                    catch (InvalidOperationException)
                    {
                        // UIA 临时不可用
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            return null;
        }

        private static AutomationElement? FindNamedWindowAmongChildren(AutomationElement parent, string windowName)
        {
            try
            {
                var windowCondition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ControlType.Window);
                var children = parent.FindAll(TreeScope.Children, windowCondition);
                foreach (AutomationElement child in children)
                {
                    try
                    {
                        if ((child.Current.Name ?? string.Empty) == windowName)
                        {
                            return child;
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // 子窗口瞬时失效
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 点击「跳过」（图二：Button Name=跳过，具备 InvokePattern；失败则 Win32 点击）。
        /// 点击前短暂解除 BlockInput，避免仿真鼠标被挡住。
        /// </summary>
        private async Task<bool> TryClickAutoSaveSkipButtonAsync(
            AutomationElement recoveryDialog,
            CancellationToken cancellationToken)
        {
            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);

                if (UIAutomationHelper.TryInvokeButtonByName(recoveryDialog, AutoSaveRecoverySkipButtonName))
                {
                    return true;
                }

                var skipButton = UIAutomationHelper.FindButtonByName(
                    recoveryDialog,
                    AutoSaveRecoverySkipButtonName);
                if (skipButton == null)
                {
                    return false;
                }

                if (UIAutomationHelper.TryInvokeButton(skipButton))
                {
                    return true;
                }

                var rect = skipButton.Current.BoundingRectangle;
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    return false;
                }

                await InvokeOnUiThreadAsync(() =>
                        WinAPIHelper.ClickScreen(
                            (int)(rect.Left + rect.Width / 2),
                            (int)(rect.Top + rect.Height / 2)))
                    .ConfigureAwait(false);
                return true;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            finally
            {
                Lock();
            }
        }

        private static AutomationElement GetFreshMainWindow(Process ctViewerProcess)
        {
            ctViewerProcess.Refresh();
            if (ctViewerProcess.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("VG 主窗口句柄无效，请确认软件是否正常运行");
            }

            // 禁止在此处 Maximize/SetForeground：后台导入轮询也会走本方法，会抢焦点打断后处理。
            // 需要置前时请显式调用 ActivateVgWindowAsync / EnsureVgForeground。
            return UIAutomationHelper.FindMainWindow(ctViewerProcess.MainWindowHandle);
        }

        /// <summary>
        /// 仅刷新主窗口句柄，不最大化、不抢焦点（用于后台导入静默轮询）。
        /// </summary>
        private static AutomationElement? TryGetMainWindowElement(Process ctViewerProcess)
        {
            try
            {
                ctViewerProcess.Refresh();
                if (ctViewerProcess.MainWindowHandle == IntPtr.Zero)
                {
                    return null;
                }

                return UIAutomationHelper.FindMainWindow(ctViewerProcess.MainWindowHandle);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private void EnterPostPipelineExclusive(Process ctViewerProcess)
        {
            Interlocked.Increment(ref _postPipelineExclusiveCount);
            _logger.Info(
                $"进入后处理独占：PID={ctViewerProcess.Id}（暂停其他槽位导入 UIA，禁止切换焦点）");
        }

        private void ExitPostPipelineExclusive()
        {
            Interlocked.Decrement(ref _postPipelineExclusiveCount);
            _logger.Info("退出后处理独占（恢复其他槽位导入静默等待）");
        }

        private bool IsPostPipelineExclusive =>
            Volatile.Read(ref _postPipelineExclusiveCount) > 0;

        /// <summary>后处理进行中时，后台导入等待在此休眠，不做 UIA、不打「导入体积中」。</summary>
        private async Task WaitWhilePostPipelineExclusiveAsync(CancellationToken cancellationToken)
        {
            var logged = false;
            while (IsPostPipelineExclusive)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!logged)
                {
                    _logger.Info("后处理独占中，本槽导入等待暂停 UIA 轮询…");
                    logged = true;
                }

                await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 枚举目录内可导入体素条目（.vgl / .vgi，以及「xxx.vgi - 快捷方式.lnk」等指向体素的快捷方式），按创建时间升序。
        /// 快捷方式会解析为目标路径供 VG 导入，成功后只删除文件夹内的 .lnk，不删除源体积文件。
        /// </summary>
        private IReadOnlyList<VolumeQueueItem> GetVolumeQueueItems(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return Array.Empty<VolumeQueueItem>();
            }

            var items = new List<VolumeQueueItem>();
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                var extension = Path.GetExtension(path);
                if (CTAutoSlicerSettings.IsImportVolumeExtension(extension))
                {
                    items.Add(new VolumeQueueItem
                    {
                        ImportPath = path,
                        QueueEntryPath = path
                    });
                    continue;
                }

                var fileName = Path.GetFileName(path);
                var isLnk = fileName.EndsWith(
                    CTAutoSlicerSettings.ShortcutFileExtension,
                    StringComparison.OrdinalIgnoreCase);
                if (!isLnk)
                {
                    if (CTAutoSlicerSettings.LooksLikeBrokenVolumeShortcutName(path))
                    {
                        _logger.Warn(
                            $"检测到疑似体素快捷方式，但缺少 .lnk 后缀，已跳过（请右键源 .vgi「创建快捷方式」）：{fileName}");
                    }

                    continue;
                }

                if (!ShellLinkHelper.TryGetShortcutTarget(path, out var targetPath))
                {
                    _logger.Warn($"体素快捷方式无法解析目标，已跳过：{path}");
                    continue;
                }

                if (!CTAutoSlicerSettings.IsImportVolumeExtension(Path.GetExtension(targetPath)))
                {
                    _logger.Warn(
                        $"快捷方式目标不是 .vgl/.vgi，已跳过：{fileName} → {targetPath}");
                    continue;
                }

                if (!File.Exists(targetPath))
                {
                    _logger.Warn($"体素快捷方式目标不存在，已跳过：{path} → {targetPath}");
                    continue;
                }

                items.Add(new VolumeQueueItem
                {
                    ImportPath = targetPath,
                    QueueEntryPath = path
                });
                _logger.Info($"识别体素快捷方式：{fileName} → {targetPath}");
            }

            return items
                .OrderBy(item => new FileInfo(item.QueueEntryPath).CreationTimeUtc)
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

        /// <summary>
        /// 图一：Ctrl+F+I 打开「快速导入...」，一旦检测到弹窗立即返回（不再二次等待）。
        /// </summary>
        private async Task<AutomationElement> OpenQuickImportAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var foundDialog = await ExecuteStepWithRetryAsync(
                    "打开快速导入",
                    async attempt =>
                    {
                        progress?.Report(
                            attempt == 1
                                ? "Ctrl+F+I 打开快速导入"
                                : $"Ctrl+F+I 打开快速导入（第 {attempt} 次重试）");
                        _logger.Info($"Ctrl+F+I 打开快速导入，第 {attempt} 次");
                        await InvokeOnVgUiThreadAsync(ctViewerProcess, SimulateCtrlFi).ConfigureAwait(false);

                        // 图一：弹窗一出现即返回。仅在 VG 窗口范围内查找，检测时不最大化、不抢焦点。
                        return await UIAutomationHelper.WaitForElementAsync(
                                () =>
                                {
                                    var mainWindow = TryGetMainWindowElement(ctViewerProcess);
                                    return mainWindow == null
                                        ? null
                                        : FindQuickImportDialog(mainWindow);
                                },
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

            progress?.Report("已检测到「快速导入...」，立即输入路径");
            _logger.Info("已检测到「快速导入...」对话框（图一），立即进入输入");
            return foundDialog
                ?? throw new InvalidOperationException("快速导入对话框未打开（图一）");
        }

        /// <summary>
        /// 图二 + 图三：对已出现的「快速导入」对话框，立即点击「文件名(N):」输入路径并点「打开(O)」。
        /// 图四由 ConfirmFileSelectionAsync 处理。
        /// </summary>
        private async Task QuickImportFillAndOpenAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement quickImportDialog,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var importFilePath = Path.GetFullPath(vglPath);

            // 图二：对话框已在上一步检出，直接找输入框并写入（不再二次等待弹窗）
            var fileNameEdit = await UIAutomationHelper.WaitForElementAsync(
                    () => FindQuickImportFileNameEdit(quickImportDialog),
                    TimeSpan.FromMilliseconds(Math.Min(settings.QuickImportDialogWaitTime, 3000)),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("未找到快速导入「文件名(N):」输入框（图二）");

            progress?.Report($"点击「文件名(N):」并输入路径：{importFilePath}");
            _logger.Info($"快速导入（图二）立即写入路径：{importFilePath}");
            await FillQuickImportFileNameByClickAsync(
                    quickImportDialog,
                    fileNameEdit,
                    importFilePath,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            // 图三：点击「打开(O)」按钮
            var openButton = FindQuickImportOpenButton(quickImportDialog)
                ?? throw new InvalidOperationException("未找到快速导入「打开(O)」按钮（图三）");

            progress?.Report("点击「打开(O)」按钮");
            _logger.Info("快速导入（图三）点击「打开(O)」");
            await ClickQuickImportOpenButtonAsync(quickImportDialog, openButton, cancellationToken)
                .ConfigureAwait(false);

            // 等待「快速导入」对话框关闭，交由图四逻辑继续
            var dialogClosed = await WaitUntilDisappearedAsync(
                    () =>
                    {
                        var mw = TryGetMainWindowElement(ctViewerProcess);
                        return mw == null ? null : FindQuickImportDialog(mw);
                    },
                    settings.FileSelectionDialogWaitTime,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!dialogClosed)
            {
                throw new InvalidOperationException(
                    $"点击「打开(O)」后，「快速导入」对话框在 {settings.FileSelectionDialogWaitTime}ms 内未关闭");
            }

            progress?.Report("「快速导入」对话框已关闭，等待文件选择向导");
            _logger.Info("「快速导入」对话框已关闭，准备检查图四文件选择向导");
        }

        /// <summary>
        /// 图二：先点击「文件名(N):」，再写入路径（ValuePattern 失败则键盘输入）。
        /// 仅置前对话框，不最大化。
        /// </summary>
        private async Task FillQuickImportFileNameByClickAsync(
            AutomationElement quickImportDialog,
            AutomationElement fileNameEdit,
            string path,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            Unlock();
            try
            {
                BringAutomationElementToFront(quickImportDialog);

                // 按标定流程：先点击图二输入框获得焦点
                await NativeClickElementWhileUnlockedAsync(fileNameEdit, cancellationToken).ConfigureAwait(false);

                if (UIAutomationHelper.TrySetEditValueSafe(fileNameEdit, path)
                    && await WaitForInputPathAsync(fileNameEdit, path, 2000, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Info("快速导入路径已通过 ValuePattern 写入");
                    return;
                }

                progress?.Report("ValuePattern 未生效，改用键盘输入路径");
                _logger.Info("快速导入 ValuePattern 未生效，改用键盘输入");
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

        /// <summary>图三：Invoke「打开(O)」，失败则 Win32 点击。</summary>
        private async Task ClickQuickImportOpenButtonAsync(
            AutomationElement quickImportDialog,
            AutomationElement openButton,
            CancellationToken cancellationToken)
        {
            BringAutomationElementToFront(quickImportDialog);

            if (UIAutomationHelper.TryInvokeButton(openButton))
            {
                _logger.Info("「打开(O)」已通过 InvokePattern 触发");
                return;
            }

            _logger.Info("Invoke「打开(O)」失败，改用 Win32 点击");
            await NativeClickElementAsync(openButton, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 文件选择向导内写入路径。禁止 Alt+N（向导中 Alt+N =「下一步(N)」，会点错按钮）。
        /// </summary>
        private async Task FillQuickImportFileNameAsync(
            AutomationElement dialog,
            AutomationElement fileNameEdit,
            string path,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            Unlock();
            try
            {
                BringAutomationElementToFront(dialog);

                // 先点击输入框获得焦点（勿用 Alt+N：文件选择向导里 Alt+N 是「下一步」）
                await NativeClickElementWhileUnlockedAsync(fileNameEdit, cancellationToken).ConfigureAwait(false);

                if (UIAutomationHelper.TrySetEditValueSafe(fileNameEdit, path)
                    && await WaitForInputPathAsync(fileNameEdit, path, 2000, cancellationToken).ConfigureAwait(false))
                {
                    _logger.Info("向导路径已通过 ValuePattern 写入");
                    return;
                }

                progress?.Report("ValuePattern 未生效，点击输入框后键盘输入路径");
                _logger.Info("向导 ValuePattern 未生效，改用点击 + 键盘输入（不用 Alt+N）");
                await NativeClickElementWhileUnlockedAsync(fileNameEdit, cancellationToken).ConfigureAwait(false);
                await TypeTextIntoFocusedControlAsync(path).ConfigureAwait(false);

                if (!await WaitForInputPathAsync(fileNameEdit, path, 3000, cancellationToken).ConfigureAwait(false))
                {
                    var actual = UIAutomationHelper.TryGetInputValue(fileNameEdit) ?? "(空)";
                    throw new InvalidOperationException(
                        $"路径写入失败，期望「{path}」，读回「{actual}」");
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

        /// <summary>
        /// 将控件所在窗口置前。仅 SetForeground，不最大化（对话框输入无需放大）。
        /// </summary>
        private static void BringAutomationElementToFront(AutomationElement element)
        {
            var hwnd = ResolveNativeWindowHandle(element);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            WinAPIHelper.SetForegroundWindow(hwnd);
        }

        /// <summary>
        /// 兼容旧调用：置前即可。历史实现曾 ShowWindow(3) 最大化，已改为不放大。
        /// </summary>
        private static void EnsureAutomationForeground(AutomationElement element) =>
            BringAutomationElementToFront(element);

        private async Task InvokeOnAutomationForegroundAsync(AutomationElement foregroundScope, Action action)
        {
            Unlock();
            try
            {
                BringAutomationElementToFront(foregroundScope);
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

            var wizardEdit = FindFileSelectionWizardPathEdit(wizardDialog)
                ?? throw new InvalidOperationException(
                    "未找到文件选择向导路径输入框（须为「文件名」相关 Edit，排除尺寸/mm 类控件）");

            progress?.Report($"向导内输入导入文件路径：{importFilePath}");
            _logger.Info(
                $"文件选择向导输入路径：{importFilePath}；输入框 Name=「{wizardEdit.Current.Name}」");
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
                        progress?.Report(
                            attempt == 1
                                ? $"点击「{FileSelectionFinishButtonName}」完成文件选择"
                                : $"点击「{FileSelectionFinishButtonName}」（第 {attempt} 次重试）");
                        _logger.Info($"点击「{FileSelectionFinishButtonName}」完成文件选择，第 {attempt} 次");

                        // 直接定位并点击「完成(F)」元素；禁止 Alt+F / Alt+N（易与「下一步(N)」混淆）
                        await ClickFileSelectionFinishButtonAsync(ctViewerProcess, cancellationToken, progress)
                            .ConfigureAwait(false);

                        // 进程池关键路径：向导「完成」按钮 UIA 残留很慢，但「请耐心等待」已出现时
                        // 导入实际已开始。任一条件满足即放行，避免空等十余秒才开下一 VG。
                        return await WaitForFileSelectionHandoffAsync(
                                ctViewerProcess,
                                settings.FileSelectionDialogWaitTime,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false);
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 点击「完成(F)」后的交接手势：出现「正在导入体积」进度 <b>或</b> 向导消失，即视为可进入后台等待并填下一槽。
        /// </summary>
        private async Task<bool> WaitForFileSelectionHandoffAsync(
            Process ctViewerProcess,
            int timeoutMs,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var handoff = await WaitUntilAsync(
                    () =>
                    {
                        try
                        {
                            var mainWindow = GetFreshMainWindow(ctViewerProcess);
                            // 优先：Insights 标定的「正在导入体积」弹窗内文
                            if (IsVolumeImportProgressVisible(mainWindow))
                            {
                                return true;
                            }

                            // 向导/完成按钮已不可用，视为文件选择结束
                            return FindEnabledFinishButton(mainWindow) == null;
                        }
                        catch (ElementNotAvailableException)
                        {
                            // 窗口瞬断时偏向「已交手」，避免卡死
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    timeoutMs,
                    cancellationToken,
                    pollIntervalMs: 100)
                .ConfigureAwait(false);

            if (!handoff)
            {
                return false;
            }

            try
            {
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                if (IsVolumeImportProgressVisible(mainWindow))
                {
                    progress?.Report(
                        $"文件选择已交手：已检测到「{ImportProgressStatusText}」（PID={ctViewerProcess.Id}），可启动下一 VG");
                    _logger.Info(
                        $"文件选择交手：检测到「{ImportProgressStatusText}」，PID={ctViewerProcess.Id}");
                }
                else
                {
                    progress?.Report(
                        $"文件选择已交手：向导已关闭（PID={ctViewerProcess.Id}）");
                    _logger.Info($"文件选择交手：向导/完成按钮已消失，PID={ctViewerProcess.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"文件选择交手后诊断日志失败：{ex.Message}");
            }

            return true;
        }

        /// <summary>
        /// 在文件选择向导内查找路径输入框：优先「文件名」；排除当前值为 mm/尺寸类的 Edit。
        /// </summary>
        private static AutomationElement? FindFileSelectionWizardPathEdit(AutomationElement wizardDialog)
        {
            var byName = FindQuickImportFileNameEdit(wizardDialog)
                ?? UIAutomationHelper.FindEditByName(wizardDialog, QuickImportEditName)
                ?? UIAutomationHelper.FindVisibleFileNameEdit(wizardDialog);
            if (byName != null && !LooksLikeMeasurementEdit(byName))
            {
                return byName;
            }

            // 回退：在向导内找可编辑、非 mm、且 Name/值像路径或文件名的 Edit
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var elements = wizardDialog.FindAll(TreeScope.Descendants, condition);
            AutomationElement? fallback = null;
            foreach (AutomationElement element in elements)
            {
                try
                {
                    if (!element.Current.IsEnabled || LooksLikeMeasurementEdit(element))
                    {
                        continue;
                    }

                    var name = element.Current.Name ?? string.Empty;
                    if (name.Contains("文件名", StringComparison.Ordinal)
                        || name.Contains("路径", StringComparison.Ordinal)
                        || name.Contains("File", StringComparison.OrdinalIgnoreCase))
                    {
                        return element;
                    }

                    fallback ??= element;
                }
                catch (ElementNotAvailableException)
                {
                    // continue
                }
            }

            return fallback;
        }

        private static bool LooksLikeMeasurementEdit(AutomationElement edit)
        {
            try
            {
                var name = edit.Current.Name ?? string.Empty;
                if (name.Contains("mm", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("开始", StringComparison.Ordinal)
                    || name.Contains("结束", StringComparison.Ordinal)
                    || name.Contains("宽度", StringComparison.Ordinal)
                    || name.Contains("高度", StringComparison.Ordinal))
                {
                    return true;
                }

                var value = UIAutomationHelper.TryGetInputValue(edit) ?? string.Empty;
                if (value.Contains("mm", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 纯数字+小数且很短，多半是尺寸/测量框（如「0.00」），不是路径
                if (value.Length > 0
                    && value.Length <= 12
                    && double.TryParse(value.Trim(), out _))
                {
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// 按 Name 精确定位「完成(F)」并 Invoke / Win32 点击；绝不点「下一步(N) >」。
        /// </summary>
        private async Task ClickFileSelectionFinishButtonAsync(
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var finishButton = FindEnabledFinishButton(GetFreshMainWindow(ctViewerProcess))
                ?? throw new InvalidOperationException(
                    $"未找到可用按钮「{FileSelectionFinishButtonName}」");

            var name = finishButton.Current.Name ?? string.Empty;
            if (!name.Contains("完成", StringComparison.Ordinal)
                || name.Contains("下一步", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"定位到的按钮不是「完成」：实际 Name=「{name}」");
            }

            progress?.Report($"已定位按钮「{name}」，直接点击元素（不用 Alt+F）");
            _logger.Info(
                $"文件选择向导点击完成按钮：Name=「{name}」，" +
                $"Rect={finishButton.Current.BoundingRectangle}");

            BringAutomationElementToFront(finishButton);

            if (UIAutomationHelper.TryInvokeButton(finishButton))
            {
                _logger.Info("「完成(F)」已通过 InvokePattern 触发");
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                return;
            }

            _logger.Info("Invoke「完成(F)」失败，改用 Win32 点击按钮中心");
            await NativeClickElementAsync(finishButton, cancellationToken).ConfigureAwait(false);
        }

        private async Task WaitForImportCompleteAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            bool activateWhenDone = true)
        {
            // 进程池静默等待：不走 ExecuteStepWithRetry / Drain；禁止 GetFreshMainWindow 抢焦点
            if (!activateWhenDone)
            {
                await WaitWhilePostPipelineExclusiveAsync(cancellationToken).ConfigureAwait(false);

                var appeared = await UIAutomationHelper.WaitForElementAsync(
                        () =>
                        {
                            try
                            {
                                if (IsPostPipelineExclusive)
                                {
                                    return null;
                                }

                                var window = TryGetMainWindowElement(ctViewerProcess);
                                return window != null && IsVolumeImportProgressVisible(window)
                                    ? window
                                    : null;
                            }
                            catch
                            {
                                return null;
                            }
                        },
                        TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (appeared == null)
                {
                    // 可能因后处理独占导致超时；独占结束后再给一次短窗口
                    await WaitWhilePostPipelineExclusiveAsync(cancellationToken).ConfigureAwait(false);
                    appeared = await UIAutomationHelper.WaitForElementAsync(
                            () =>
                            {
                                try
                                {
                                    var window = TryGetMainWindowElement(ctViewerProcess);
                                    return window != null && IsVolumeImportProgressVisible(window)
                                        ? window
                                        : null;
                                }
                                catch
                                {
                                    return null;
                                }
                            },
                            TimeSpan.FromMilliseconds(settings.FileSelectionDialogWaitTime),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                if (appeared == null)
                {
                    throw new TimeoutException(
                        $"等待导入进度框出现超时（{settings.FileSelectionDialogWaitTime}ms），PID={ctViewerProcess.Id}");
                }
            }
            else
            {
                await ExecuteStepWithRetryAsync(
                        "等待导入进度框出现",
                        async _ =>
                        {
                            return await UIAutomationHelper.WaitForElementAsync(
                                    () =>
                                    {
                                        var window = GetFreshMainWindow(ctViewerProcess);
                                        return IsVolumeImportProgressVisible(window) ? window : null;
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
            }

            if (activateWhenDone || !IsPostPipelineExclusive)
            {
                progress?.Report($"导入体积中......（PID={ctViewerProcess.Id}）");
            }

            _logger.Info($"检测到可见导入进度框，PID={ctViewerProcess.Id}");

            var deadline = DateTime.UtcNow.AddMinutes(settings.ImportTimeoutMinutes);
            var offscreenProgressReadySinceUtc = (DateTime?)null;
            var lastProgressLogUtc = DateTime.MinValue;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!activateWhenDone)
                {
                    await WaitWhilePostPipelineExclusiveAsync(cancellationToken).ConfigureAwait(false);
                }

                AutomationElement? mainWindow;
                try
                {
                    // 静默等待：绝不 SetForeground；主流程等待：可用 GetFreshMainWindow（也不再内置置前）
                    mainWindow = activateWhenDone
                        ? GetFreshMainWindow(ctViewerProcess)
                        : TryGetMainWindowElement(ctViewerProcess);
                    if (mainWindow == null)
                    {
                        await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
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
                    progress?.Report($"导入体积完成（PID={ctViewerProcess.Id}）");
                    _logger.Info($"导入体积完成：进度框已消失且工作区已就绪，PID={ctViewerProcess.Id}");
                    await WaitForRenderingWorkspaceReadyAsync(
                            ctViewerProcess,
                            settings,
                            settings.PostImportWaitTime,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (activateWhenDone)
                    {
                        await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken)
                            .ConfigureAwait(false);
                    }

                    return;
                }

                if (visibleProgress != null)
                {
                    offscreenProgressReadySinceUtc = null;
                    // 静默等待降频日志，避免后处理期间刷屏误导为「切进程」
                    if (activateWhenDone
                        || (DateTime.UtcNow - lastProgressLogUtc).TotalSeconds >= 15)
                    {
                        lastProgressLogUtc = DateTime.UtcNow;
                        progress?.Report($"导入体积中......（PID={ctViewerProcess.Id}）");
                    }

                    await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (volumeReady && FindOffscreenImportProgressWindow(mainWindow) != null)
                {
                    offscreenProgressReadySinceUtc ??= DateTime.UtcNow;
                    var readyMs = (DateTime.UtcNow - offscreenProgressReadySinceUtc.Value).TotalMilliseconds;
                    if (readyMs >= OffscreenProgressBypassMs)
                    {
                        progress?.Report(
                            $"导入体积完成（工作区已就绪，忽略离屏进度框 UIA 残留，PID={ctViewerProcess.Id}）");
                        _logger.Info(
                            $"导入体积完成：工作区已就绪 {readyMs:0}ms，忽略离屏进度框 UIA 残留，PID={ctViewerProcess.Id}");
                        await WaitForRenderingWorkspaceReadyAsync(
                                ctViewerProcess,
                                settings,
                                settings.PostImportWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (activateWhenDone)
                        {
                            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken)
                                .ConfigureAwait(false);
                        }

                        return;
                    }
                }
                else
                {
                    offscreenProgressReadySinceUtc = null;
                }

                if (activateWhenDone
                    || (DateTime.UtcNow - lastProgressLogUtc).TotalSeconds >= 15)
                {
                    lastProgressLogUtc = DateTime.UtcNow;
                    progress?.Report($"导入体积中......（PID={ctViewerProcess.Id}）");
                }

                await Task.Delay(VgReadyPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待导入完成超时（{settings.ImportTimeoutMinutes} 分钟）：" +
                $"进度框未消失且工作区未就绪，PID={ctViewerProcess.Id}");
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
                            _logger.Info($"组合框 ValuePattern 已是「{presetName}」，跳过打开下拉");
                            progress?.Report($"渲染预设「{presetName}」已应用（无需重复选择）");
                            return true;
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
                                pollIntervalMs: PresetListItemPollIntervalMs)
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

                        if (IsPresetApplied(comboBox, presetName)
                            || !UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox))
                        {
                            LogPresetComboUiaState(comboBox, presetName, "应用完成");
                            progress?.Report($"渲染预设「{presetName}」已应用");
                            return true;
                        }

                        var afterValue = UIAutomationHelper.GetComboBoxDisplayText(comboBox);
                        throw new InvalidOperationException(
                            $"未能应用预设「{presetName}」，应用前「{beforeValue}」应用后「{afterValue}」，下拉仍展开");
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
            int totalTimeoutMs,
            CancellationToken cancellationToken)
        {
            if (UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox))
            {
                return;
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(totalTimeoutMs);
            comboBox.SetFocus();

            async Task<bool> TryOpenStepAsync(string stepName, Func<Task> action)
            {
                var remainingMs = (int)Math.Max(200, (deadline - DateTime.UtcNow).TotalMilliseconds);
                if (remainingMs <= 0)
                {
                    return false;
                }

                var stepWaitMs = Math.Min(PresetDropdownOpenStepWaitMs, remainingMs);
                await action().ConfigureAwait(false);
                if (await WaitForComboDropdownOpenAsync(comboBox, stepWaitMs, cancellationToken)
                        .ConfigureAwait(false))
                {
                    _logger.Info($"渲染预设下拉已通过{stepName}打开");
                    return true;
                }

                _logger.Info($"渲染预设下拉：{stepName} 在 {stepWaitMs}ms 内未检测到展开，尝试下一方式");
                return false;
            }

            if (await TryOpenStepAsync(
                    "Expand",
                    () =>
                    {
                        UIAutomationHelper.TryExpandComboBox(comboBox);
                        return Task.CompletedTask;
                    })
                .ConfigureAwait(false))
            {
                return;
            }

            if (await TryOpenStepAsync(
                    "Alt+Down",
                    () => InvokeOnUiThreadAsync(SimulateAltDown))
                .ConfigureAwait(false))
            {
                return;
            }

            if (await TryOpenStepAsync(
                    "点击组合框",
                    () => ClickElementCenterAsync(comboBox))
                .ConfigureAwait(false))
            {
                return;
            }

            throw new InvalidOperationException("渲染预设下拉在超时内未能打开");
        }

        /// <summary>
        /// Qt 预设下拉：Win32 点击确认；可选回车提交。
        /// 导出对话框内禁止回车（「保存...」常为默认按钮，Enter 会误触系统另存为）。
        /// </summary>
        private async Task SelectPresetListItemAsync(
            AutomationElement listItem,
            AutomationElement comboBox,
            string presetName,
            int timeoutMs,
            CancellationToken cancellationToken,
            bool confirmWithEnter = true)
        {
            listItem.SetFocus();
            UIAutomationHelper.TryScrollListItemIntoView(listItem);
            UIAutomationHelper.TrySelectListItem(listItem);

            var closeWaitMs = Math.Min(PresetDropdownCloseWaitMs, timeoutMs);

            async Task<bool> ConfirmByClickAsync(string stage)
            {
                if (confirmWithEnter)
                {
                    _logger.Info($"{stage}：Win32 点击并回车确认列表项「{presetName}」");
                }
                else
                {
                    _logger.Info($"{stage}：Win32 点击确认列表项「{presetName}」（导出对话框禁用 Enter）");
                }

                await NativeClickElementAsync(listItem, cancellationToken).ConfigureAwait(false);
                if (confirmWithEnter)
                {
                    await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
                }

                if (await WaitForComboDropdownClosedAsync(comboBox, closeWaitMs, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return true;
                }

                return IsPresetApplied(comboBox, presetName);
            }

            if (await ConfirmByClickAsync("首次").ConfigureAwait(false))
            {
                LogPresetComboUiaState(comboBox, presetName, "点击确认后");
                return;
            }

            if (await ConfirmByClickAsync("再次").ConfigureAwait(false))
            {
                LogPresetComboUiaState(comboBox, presetName, "二次点击后");
                return;
            }

            if (!confirmWithEnter)
            {
                LogPresetComboUiaState(comboBox, presetName, "仅点击后（未回车）");
                return;
            }

            _logger.Info($"下拉仍开，尝试键盘输入预设名「{presetName}」并回车");
            await SelectPresetByKeyboardAsync(presetName, cancellationToken).ConfigureAwait(false);
            await WaitForComboDropdownClosedAsync(comboBox, closeWaitMs, cancellationToken)
                .ConfigureAwait(false);
            LogPresetComboUiaState(comboBox, presetName, "键盘输入后");
        }

        private void LogPresetComboUiaState(AutomationElement comboBox, string presetName, string stage)
        {
            var displayText = UIAutomationHelper.GetComboBoxDisplayText(comboBox);
            var uiaApplied = IsPresetApplied(comboBox, presetName);
            var expanded = UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox);
            _logger.Info(
                $"渲染预设 UIA 诊断（{stage}）：读数「{displayText}」展开={expanded} " +
                $"ValuePattern匹配={uiaApplied}（目标「{presetName}」）");
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
            int? processId = null;
            try
            {
                processId = renderingPanel.Current.ProcessId;
            }
            catch (ElementNotAvailableException)
            {
                // 忽略，后续仅在已传入根内查找
            }

            return FindVisibleListItemScoped(
                presetName,
                processId,
                comboBox,
                renderingPanel);
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

                // 步骤开始前先清一次队列，避免已知中断窗挡住本步
                if (_activeVgProcess != null)
                {
                    await TryDrainInterruptDialogQueueAsync(
                            _activeVgProcess,
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false);
                }

                try
                {
                    var result = await attemptAction(attempt).ConfigureAwait(false);
                    if (isSuccess(result))
                    {
                        return result;
                    }

                    _logger.Warn($"{stepName} 第 {attempt} 次未满足完成条件");

                    // 未达标时：若是队列弹窗导致，处理后进入下一轮重试
                    if (_activeVgProcess != null
                        && await TryDrainInterruptDialogQueueAsync(
                                _activeVgProcess,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false))
                    {
                        progress?.Report($"{stepName} 未完成，已处理中断弹窗，将重试该步骤");
                        _logger.Info($"{stepName} 未完成且命中中断弹窗队列，准备重试");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.Warn($"{stepName} 第 {attempt} 次异常：{ex.Message}");

                    // 异常后：优先检查是否为队列中的中断弹窗，处理后再重试本步骤
                    if (_activeVgProcess != null
                        && await TryDrainInterruptDialogQueueAsync(
                                _activeVgProcess,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false))
                    {
                        progress?.Report($"{stepName} 异常后已处理中断弹窗，将重试该步骤");
                        _logger.Info($"{stepName} 异常后命中中断弹窗队列，准备重试。原异常：{ex.Message}");
                        lastException = null;
                    }
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
                    // 1) 主窗口直接子窗口（标准「打开文件」对话框，毫秒级）
                    var amongChildren = FindNamedWindowAmong(
                        mainWindow,
                        TreeScope.Children,
                        QuickImportDialogNamePrefix);
                    if (amongChildren != null)
                    {
                        return amongChildren;
                    }

                    // 2) 主窗口子树（仍不扫桌面）
                    var amongDescendants = FindNamedWindowAmong(
                            mainWindow,
                            TreeScope.Descendants,
                            QuickImportDialogNamePrefix)
                        ?? UIAutomationHelper.FindWindowByName(
                            mainWindow,
                            QuickImportDialogNamePrefix,
                            nameStartsWith: true);
                    if (amongDescendants != null)
                    {
                        return amongDescendants;
                    }

                    // 3) 同进程顶层窗口（TreeScope.Children，避免 Root Descendants 全桌面扫描）
                    var processId = mainWindow.Current.ProcessId;
                    if (processId > 0)
                    {
                        var sameProcessTopLevel = FindNamedWindowOnDesktopByProcess(
                            processId,
                            QuickImportDialogNamePrefix);
                        if (sameProcessTopLevel != null)
                        {
                            return sameProcessTopLevel;
                        }
                    }
                }

                // 无主窗口时：仅扫桌面顶层窗口（Children），禁止 Root.FindAll(Descendants)
                return FindNamedWindowAmong(
                    AutomationElement.RootElement,
                    TreeScope.Children,
                    QuickImportDialogNamePrefix);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        /// <summary>
        /// 在指定范围内按 Name 前缀查找可见 Window/Pane（不做全桌面 Descendants）。
        /// </summary>
        private static AutomationElement? FindNamedWindowAmong(
            AutomationElement root,
            TreeScope scope,
            string namePrefix)
        {
            foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                var elements = root.FindAll(scope, condition);
                foreach (AutomationElement element in elements)
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!name.StartsWith(namePrefix, StringComparison.Ordinal)
                        && !name.Contains(namePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 按进程 ID 在桌面顶层窗口中查找（仅 Children，不会遍历整棵 UIA 树）。
        /// </summary>
        private static AutomationElement? FindNamedWindowOnDesktopByProcess(int processId, string namePrefix)
        {
            foreach (AutomationElement element in EnumerateSameProcessTopLevelWindows(processId))
            {
                var name = element.Current.Name ?? string.Empty;
                if (!name.StartsWith(namePrefix, StringComparison.Ordinal)
                    && !name.Contains(namePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var rect = element.Current.BoundingRectangle;
                if (rect.Width > 0 && rect.Height > 0)
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// 枚举同进程桌面顶层 Window（Root.Children，禁止 Descendants）。
        /// </summary>
        private static IEnumerable<AutomationElement> EnumerateSameProcessTopLevelWindows(int processId)
        {
            if (processId <= 0)
            {
                yield break;
            }

            AutomationElementCollection? elements;
            try
            {
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                    new PropertyCondition(AutomationElement.ProcessIdProperty, processId));
                elements = AutomationElement.RootElement.FindAll(TreeScope.Children, condition);
            }
            catch (ElementNotAvailableException)
            {
                yield break;
            }

            foreach (AutomationElement element in elements)
            {
                yield return element;
            }
        }

        /// <summary>
        /// 枚举桌面顶层 Window（仅 Children）。用于系统「另存为」等可能不在 VG 进程内的对话框。
        /// </summary>
        private static IEnumerable<AutomationElement> EnumerateDesktopTopLevelWindows()
        {
            AutomationElementCollection? elements;
            try
            {
                var condition = new PropertyCondition(
                    AutomationElement.ControlTypeProperty,
                    ControlType.Window);
                elements = AutomationElement.RootElement.FindAll(TreeScope.Children, condition);
            }
            catch (ElementNotAvailableException)
            {
                yield break;
            }

            foreach (AutomationElement element in elements)
            {
                yield return element;
            }
        }

        /// <summary>
        /// 主窗子树 → 同进程顶层窗口：按 Name 找 Window/Pane（禁止 Root Descendants）。
        /// </summary>
        private static AutomationElement? FindWindowByNameScoped(
            AutomationElement mainWindow,
            string windowName,
            bool nameStartsWith = false)
        {
            try
            {
                var inMain = UIAutomationHelper.FindWindowByName(mainWindow, windowName, nameStartsWith);
                if (inMain != null)
                {
                    return inMain;
                }

                var processId = mainWindow.Current.ProcessId;
                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId))
                {
                    var name = topWindow.Current.Name ?? string.Empty;
                    var matched = nameStartsWith
                        ? name.StartsWith(windowName, StringComparison.Ordinal)
                        : name.Equals(windowName, StringComparison.Ordinal)
                          || name.Contains(windowName, StringComparison.Ordinal);
                    if (!matched)
                    {
                        continue;
                    }

                    if (UIAutomationHelper.IsElementInteractable(topWindow)
                        || (topWindow.Current.BoundingRectangle is { Width: > 0, Height: > 0 }))
                    {
                        return topWindow;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 在优先根 → 同进程顶层窗口子树中查找可见 ListItem（禁止 Root Descendants）。
        /// Qt 下拉常挂在 ComboBox/面板或同进程浮动窗下。
        /// </summary>
        private static AutomationElement? FindVisibleListItemScoped(
            string itemName,
            int? processId,
            params AutomationElement?[] preferRoots)
        {
            foreach (var root in preferRoots)
            {
                if (root == null)
                {
                    continue;
                }

                try
                {
                    // ComboBox 内先找 List 再找项，范围更窄
                    var list = UIAutomationHelper.FindFirstDescendantByControlType(root, ControlType.List);
                    if (list != null)
                    {
                        var inList = UIAutomationHelper.FindVisibleDescendantByName(
                            list,
                            ControlType.ListItem,
                            itemName);
                        if (inList != null)
                        {
                            return inList;
                        }
                    }

                    var inRoot = UIAutomationHelper.FindVisibleDescendantByName(
                        root,
                        ControlType.ListItem,
                        itemName);
                    if (inRoot != null)
                    {
                        return inRoot;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续下一根
                }
            }

            if (processId is not > 0)
            {
                return null;
            }

            foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId.Value))
            {
                try
                {
                    var item = UIAutomationHelper.FindVisibleDescendantByName(
                        topWindow,
                        ControlType.ListItem,
                        itemName);
                    if (item != null)
                    {
                        return item;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            return null;
        }

        /// <summary>
        /// 优先根 → 「保存图像」窗 → 同进程顶层 → 桌面顶层：查找「文件名」Edit（禁止 Root Descendants）。
        /// VG2022：系统保存框嵌在「保存影片/图像序列」下，标题为「保存图像...」。
        /// </summary>
        private static AutomationElement? FindVisibleFileNameEditScoped(
            int? processId,
            params AutomationElement?[] preferRoots)
        {
            foreach (var root in preferRoots)
            {
                if (root == null)
                {
                    continue;
                }

                try
                {
                    var edit = UIAutomationHelper.FindVisibleFileNameEdit(root);
                    if (edit != null)
                    {
                        return edit;
                    }

                    // 显式先找嵌套「保存图像...」，再在其子树找文件名（FindAll 跨嵌套窗偶发漏检）
                    var saveImageDialog = UIAutomationHelper.FindVisibleSaveImageDialog(root);
                    if (saveImageDialog != null)
                    {
                        edit = UIAutomationHelper.FindVisibleFileNameEdit(saveImageDialog);
                        if (edit != null)
                        {
                            return edit;
                        }
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            if (processId is > 0)
            {
                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId.Value))
                {
                    try
                    {
                        var edit = UIAutomationHelper.FindVisibleFileNameEdit(topWindow);
                        if (edit != null)
                        {
                            return edit;
                        }

                        var saveImageDialog = UIAutomationHelper.FindVisibleSaveImageDialog(topWindow);
                        if (saveImageDialog != null)
                        {
                            edit = UIAutomationHelper.FindVisibleFileNameEdit(saveImageDialog);
                            if (edit != null)
                            {
                                return edit;
                            }
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // 继续
                    }
                }
            }

            // 系统另存为可能不在 VG 进程，或作为桌面顶层「保存图像...」出现
            foreach (var topWindow in EnumerateDesktopTopLevelWindows())
            {
                try
                {
                    var name = topWindow.Current.Name ?? string.Empty;
                    var isSaveImageTop =
                        name.Contains("保存图像", StringComparison.Ordinal)
                        || name.Contains("另存为", StringComparison.Ordinal);

                    // 同进程顶层已扫过；但标题为「保存图像」的仍再扫一次（防止嵌套/归属差异漏检）
                    if (processId is > 0
                        && topWindow.Current.ProcessId == processId.Value
                        && !isSaveImageTop)
                    {
                        continue;
                    }

                    var edit = UIAutomationHelper.FindVisibleFileNameEdit(topWindow);
                    if (edit != null)
                    {
                        return edit;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            return null;
        }

        /// <summary>
        /// 主窗 → 同进程顶层：按 Name 前缀找可见导出对话框（禁止 Root Descendants）。
        /// </summary>
        private static AutomationElement? FindVisibleWindowByNamePrefixScoped(
            AutomationElement mainWindow,
            string namePrefix,
            bool requireImageKeyword = false)
        {
            try
            {
                var inMain = UIAutomationHelper.FindVisibleWindowByNamePrefix(
                    mainWindow,
                    namePrefix,
                    requireImageKeyword);
                if (inMain != null)
                {
                    return inMain;
                }

                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(mainWindow.Current.ProcessId))
                {
                    var name = topWindow.Current.Name ?? string.Empty;
                    if (!name.StartsWith(namePrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (requireImageKeyword
                        && !name.Contains("图像", StringComparison.Ordinal)
                        && !name.Contains("影片", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (UIAutomationHelper.IsElementInteractable(topWindow))
                    {
                        return topWindow;
                    }

                    // 顶层窗本身是对话框外壳时，再在其子树找一次
                    var nested = UIAutomationHelper.FindVisibleWindowByNamePrefix(
                        topWindow,
                        namePrefix,
                        requireImageKeyword);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// 在优先根 / 同进程顶层中按关键字找嵌套对话框（禁止 Root Descendants）。
        /// </summary>
        private static AutomationElement? FindDescendantWindowByNameKeywordScoped(
            string nameKeyword,
            string? requireButtonName,
            int? processId,
            params AutomationElement?[] preferRoots)
        {
            foreach (var root in preferRoots)
            {
                if (root == null)
                {
                    continue;
                }

                try
                {
                    var found = UIAutomationHelper.FindDescendantWindowByNameKeyword(
                        root,
                        nameKeyword,
                        requireButtonName);
                    if (found != null)
                    {
                        return found;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            if (processId is not > 0)
            {
                return null;
            }

            foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId.Value))
            {
                try
                {
                    var name = topWindow.Current.Name ?? string.Empty;
                    if (name.Contains(nameKeyword, StringComparison.Ordinal)
                        && UIAutomationHelper.IsElementInteractable(topWindow)
                        && (string.IsNullOrWhiteSpace(requireButtonName)
                            || UIAutomationHelper.FindButtonByName(topWindow, requireButtonName) != null))
                    {
                        return topWindow;
                    }

                    var nested = UIAutomationHelper.FindDescendantWindowByNameKeyword(
                        topWindow,
                        nameKeyword,
                        requireButtonName);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            return null;
        }

        private static AutomationElement? FindFileSelectionWizardDialog(AutomationElement mainWindow)
        {
            var finishButton = FindEnabledFinishButton(mainWindow);
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
                var button = UIAutomationHelper.FindButtonByName(mainWindow, FileSelectionFinishButtonName);
                if (button is { Current.IsEnabled: true })
                {
                    return button;
                }

                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(mainWindow.Current.ProcessId))
                {
                    button = UIAutomationHelper.FindButtonByName(topWindow, FileSelectionFinishButtonName);
                    if (button is { Current.IsEnabled: true })
                    {
                        return button;
                    }
                }

                return null;
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
                // 最稳：内文 Text「正在导入体积」（与 Accessibility Insights 一致）
                var statusText = FindVisibleImportStatusText(mainWindow);
                if (statusText != null)
                {
                    return UIAutomationHelper.FindAncestorDialogOrWindow(statusText) ?? statusText;
                }

                var inMain = FindProgressWindowByTitle(mainWindow);
                if (inMain != null)
                {
                    return inMain;
                }

                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(mainWindow.Current.ProcessId))
                {
                    try
                    {
                        var name = topWindow.Current.Name ?? string.Empty;
                        if (name.Equals(ImportProgressWindowName, StringComparison.Ordinal)
                            && IsProgressUiVisible(topWindow))
                        {
                            return topWindow;
                        }

                        var nested = FindProgressWindowByTitle(topWindow);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // continue
                    }
                }

                return null;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        /// <summary>是否已出现体积导入进度（交手/后台等待共用）。</summary>
        private static bool IsVolumeImportProgressVisible(AutomationElement mainWindow) =>
            FindVisibleImportProgressWindow(mainWindow) != null;

        /// <summary>
        /// 查找可见的「正在导入体积」文本（Insights：ControlType.Text）。
        /// </summary>
        private static AutomationElement? FindVisibleImportStatusText(AutomationElement mainWindow)
        {
            try
            {
                var inMain = FindVisibleTextByKeyword(mainWindow, ImportProgressStatusText);
                if (inMain != null)
                {
                    return inMain;
                }

                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(mainWindow.Current.ProcessId))
                {
                    var text = FindVisibleTextByKeyword(topWindow, ImportProgressStatusText);
                    if (text != null)
                    {
                        return text;
                    }
                }

                return null;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
        }

        private static AutomationElement? FindVisibleTextByKeyword(AutomationElement root, string keyword)
        {
            // 优先精确 Name 条件（Insights：Name=正在导入体积），避免全树扫 Text
            try
            {
                var exact = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.NameProperty, keyword));
                var exactHit = root.FindFirst(TreeScope.Descendants, exact);
                if (exactHit != null && IsProgressUiVisible(exactHit))
                {
                    return exactHit;
                }
            }
            catch (ElementNotAvailableException)
            {
                // fall through
            }

            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
            {
                try
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!name.Contains(keyword, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (IsProgressUiVisible(element))
                    {
                        return element;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // continue
                }
            }

            return null;
        }

        private static AutomationElement? FindProgressWindowByTitle(AutomationElement root)
        {
            return FindNamedProgressSurface(root, ControlType.Window)
                   ?? FindNamedProgressSurface(root, ControlType.Pane);
        }

        private static AutomationElement? FindNamedProgressSurface(AutomationElement root, ControlType controlType)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
            {
                try
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!name.Equals(ImportProgressWindowName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // 进度窗对 IsOffscreen/IsEnabled 偶发不可靠；有尺寸或含导入文案即认
                    if (IsProgressUiVisible(element)
                        || UIAutomationHelper.ContainsDescendantText(element, ImportProgressStatusText))
                    {
                        return element;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // continue
                }
            }

            return null;
        }

        /// <summary>进度类 UI 可见判定：不强制 IsEnabled（部分 Qt 进度窗 Enabled 不稳定）。</summary>
        private static bool IsProgressUiVisible(AutomationElement element)
        {
            try
            {
                if (element.Current.IsOffscreen)
                {
                    return false;
                }

                var rect = element.Current.BoundingRectangle;
                return rect.Width > 0 && rect.Height > 0;
            }
            catch (ElementNotAvailableException)
            {
                return false;
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

        /// <summary>成功后删除体素文件夹内的排队条目（真实 .vgl/.vgi 或快捷方式 .lnk）。</summary>
        private Task OnImportSucceededAsync(string queueEntryPath)
        {
            try
            {
                if (File.Exists(queueEntryPath))
                {
                    File.Delete(queueEntryPath);
                    _logger.Info($"处理成功，已删除排队条目：{queueEntryPath}");
                }
                else
                {
                    _logger.Warn($"处理成功，但排队条目已不存在：{queueEntryPath}");
                }
            }
            catch (Exception ex)
            {
                // 流程已成功；删除失败只记警告，避免误判整单失败
                _logger.Warn($"处理成功但删除排队条目失败（请手动清理）：{queueEntryPath} — {ex.Message}");
            }

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

        /// <summary>
        /// 快速导入快捷键 Ctrl+F+I：按住 Ctrl，依次按 F、I（与真机标定一致）。
        /// </summary>
        private void SimulateCtrlFi()
        {
            _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
            _simulator.SimulateKeyPress(KeyCode.VcF);
            _simulator.SimulateKeyRelease(KeyCode.VcF);
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

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
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
                _activeVgProcess = null;
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
                    () =>
                    {
                        int? processId = null;
                        try
                        {
                            processId = dialog.Current.ProcessId;
                        }
                        catch (ElementNotAvailableException)
                        {
                        }

                        return FindVisibleListItemScoped(modeName, processId, dialog);
                    },
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

            int? processId = null;
            try
            {
                processId = dialog.Current.ProcessId;
            }
            catch (ElementNotAvailableException)
            {
            }

            await WaitUntilDisappearedAsync(
                    () => FindVisibleListItemScoped(modeName, processId, dialog),
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
            int? processId = null;
            try
            {
                processId = dialog.Current.ProcessId;
            }
            catch (ElementNotAvailableException)
            {
            }

            var visibleItem = FindVisibleListItemScoped(modeName, processId, dialog);
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
            return FindWindowByNameScoped(mainWindow, dialogName);
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

            var vglFileName = Path.GetFileName(vglPath);
            var exportBaseName = SliceExportNamingHelper.BuildExportBaseName(
                vglFileName,
                now: null,
                out var usedDefaultName);
            if (usedDefaultName)
            {
                progress?.Report(
                    $"导入文件名不符合命名规则，已使用默认导出名「{exportBaseName}」（原文件：{vglFileName}）");
                _logger.Info(
                    $"切片导出命名回退默认：原文件「{vglFileName}」→「{exportBaseName}」");
            }

            var quantity = SliceExportNamingHelper.ResolveExportQuantity(
                vglFileName,
                settings.SliceExportQuantity,
                settings.SliceExportQuantityQj);

            if (SliceExportNamingHelper.IsGlobalImage(vglFileName))
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

            progress?.Report($"开始三视图切片导出，主文件夹「{exportBaseName}」，数量 {quantity}");
            _logger.Info($"三视图切片导出开始：主文件夹={exportBaseName}，数量={quantity}");

            for (var i = 0; i < viewExports.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (viewListItemName, viewSuffix) = viewExports[i];
                var exportSavePath = SliceExportNamingHelper.BuildExportSavePath(
                    settings.SliceExportFolder,
                    exportBaseName,
                    viewSuffix);
                var exportFolder = Path.GetDirectoryName(exportSavePath) ?? settings.SliceExportFolder;
                var relativeFolder = Path.Combine(
                    SliceExportNamingHelper.BuildExportFileRootFolderName(exportBaseName),
                    Path.GetFileName(exportFolder));
                progress?.Report(
                    $"导出视图 {i + 1}/3：{viewListItemName} → 「{relativeFolder}」，图片 1~{quantity}");
                _logger.Info(
                    $"导出视图 {i + 1}/3：{viewListItemName}，文件夹={exportFolder}，图片命名 1~{quantity}");

                await ExportSingleViewSliceAsync(
                        settings,
                        ctViewerProcess,
                        viewListItemName,
                        viewSuffix,
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
            string viewSuffix,
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
                    viewSuffix,
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
            return FindVisibleWindowByNamePrefixScoped(
                mainWindow,
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
                    cancellationToken,
                    confirmWithEnter: false)
                .ConfigureAwait(false);

            if (!IsPresetApplied(comboBox, viewListItemName)
                && UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox))
            {
                throw new InvalidOperationException($"未能选择 2D 视图「{viewListItemName}」");
            }
        }

        private async Task FillSliceExportFormAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            string viewSuffix,
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
            // 导出对话框「保存...」常为默认按钮：禁止 Enter，改用 Tab 失焦提交
            if (!await SetSliceExportFieldValueAsync(
                    imageSizeEdits[0],
                    widthValue,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置图像宽度为 {widthValue}");
            }

            progress?.Report($"已设置图像宽度 {widthValue}");
            _logger.Info($"已设置图像宽度 {widthValue}");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);

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
            if (!await SetSliceExportFieldValueAsync(
                    quantityEdit,
                    quantityValue,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置导出数量为 {quantityValue}");
            }

            progress?.Report($"已设置导出数量 {quantityValue}");
            _logger.Info($"已设置导出数量 {quantityValue}");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);

            // 切片范围：按视角分别取 UI 配置（前 / 右 / 上）
            var (rangeStart, rangeEnd) = settings.GetSliceRangeForView(viewSuffix);
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
            var startValue = rangeStart.ToString("0.##");
            var endValue = rangeEnd.ToString("0.##");
            if (!await SetSliceExportFieldValueAsync(
                    startEdit,
                    startValue,
                    cancellationToken).ConfigureAwait(false)
                || !await SetSliceExportFieldValueAsync(
                    endEdit,
                    endValue,
                    cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"未能设置「{viewSuffix}」视图切片范围 {startValue} ~ {endValue} mm");
            }

            progress?.Report($"已设置「{viewSuffix}」视图切片范围 {startValue} ~ {endValue} mm");
            _logger.Info($"已设置「{viewSuffix}」视图切片范围 {startValue} ~ {endValue} mm");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 导出对话框专用填值：禁止 Enter（避免触发默认「保存...」），输入后用 Tab 失焦提交。
        /// </summary>
        private async Task<bool> SetSliceExportFieldValueAsync(
            AutomationElement edit,
            string value,
            CancellationToken cancellationToken)
        {
            var isCustom = edit.Current.ControlType == ControlType.Custom;

            if (!isCustom
                && UIAutomationHelper.TrySetEditValueSafe(edit, value)
                && InputValueMatches(UIAutomationHelper.TryGetInputValue(edit), value))
            {
                await CommitEditWithTabAsync(edit, cancellationToken).ConfigureAwait(false);
                return true;
            }

            await SetEditValueWithKeyboardFallbackAsync(
                    edit,
                    value,
                    cancellationToken,
                    commitWithEnter: false)
                .ConfigureAwait(false);
            await CommitEditWithTabAsync(edit, cancellationToken).ConfigureAwait(false);

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

            // Custom 框常无法 UIA 读回；键盘+Tab 后视为已提交
            return isCustom;
        }

        private static Task DelaySliceExportStepAsync(CTAutoSlicerSettings settings, CancellationToken cancellationToken)
        {
            var delayMs = settings.ThickPlateStepDelayMs;
            return delayMs > 0
                ? Task.Delay(delayMs, cancellationToken)
                : Task.CompletedTask;
        }

        private async Task CommitEditWithTabAsync(
            AutomationElement edit,
            CancellationToken cancellationToken)
        {
            try
            {
                edit.SetFocus();
            }
            catch (ElementNotAvailableException)
            {
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(SimulateTab).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
        }

        private async Task CommitEditWithEnterAsync(
            AutomationElement edit,
            CancellationToken cancellationToken)
        {
            try
            {
                edit.SetFocus();
            }
            catch (ElementNotAvailableException)
            {
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    _simulator.SimulateKeyPress(KeyCode.VcEnter);
                    _simulator.SimulateKeyRelease(KeyCode.VcEnter);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
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
                if (commitWithEnter)
                {
                    await CommitEditWithEnterAsync(edit, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }

            await SetEditValueWithKeyboardFallbackAsync(edit, value, cancellationToken, commitWithEnter)
                .ConfigureAwait(false);
            if (commitWithEnter)
            {
                await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
            }

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

            // VG Custom 数值框常无法通过 UIA 读回；键盘+Enter 后再等一拍，避免未输完就点「保存...」
            if (isCustom && commitWithEnter)
            {
                await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
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

            progress?.Report("已点击保存，等待系统保存对话框（保存图像...）");
            _logger.Info("已点击保存，等待系统保存对话框（保存图像...）");

            int? processId = null;
            try
            {
                processId = exportDialog.Current.ProcessId;
            }
            catch (ElementNotAvailableException)
            {
            }

            // 「保存图像...」嵌在「保存影片/图像序列」下；文件名控件可能是 ComboBox/空 Name Edit
            var fileEdit = await UIAutomationHelper.WaitForElementAsync(
                    () => FindVisibleFileNameEditScoped(processId, exportDialog),
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (fileEdit == null)
            {
                AutomationElement? saveImageDialog = null;
                try
                {
                    saveImageDialog = UIAutomationHelper.FindVisibleSaveImageDialog(exportDialog);
                    if (saveImageDialog == null && processId is > 0)
                    {
                        foreach (var top in EnumerateSameProcessTopLevelWindows(processId.Value))
                        {
                            saveImageDialog = UIAutomationHelper.FindVisibleSaveImageDialog(top);
                            if (saveImageDialog != null)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (ElementNotAvailableException)
                {
                }

                var hint = saveImageDialog == null
                    ? "未找到标题含「保存图像」的对话框"
                    : $"已见到「{saveImageDialog.Current.Name}」但未找到「文件名」输入框";
                throw new InvalidOperationException($"未在超时内检测到系统保存对话框（{hint}）");
            }

            try
            {
                var host = UIAutomationHelper.FindAncestorDialogOrWindow(fileEdit);
                _logger.Info(
                    host != null
                        ? $"已定位系统保存对话框「{host.Current.Name}」，开始填写路径"
                        : "已定位文件名输入框，开始填写路径");
            }
            catch (ElementNotAvailableException)
            {
                _logger.Info("已定位文件名输入框，开始填写路径");
            }

            await FillSystemSaveDialogAsync(
                    settings,
                    exportDialog,
                    processId,
                    exportSavePathWithoutExt,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
            await DismissImageQualityDialogAsync(
                    settings,
                    exportDialog,
                    processId,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        private async Task FillSystemSaveDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            int? processId,
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
                        var fileEdit = FindVisibleFileNameEditScoped(processId, exportDialog);
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
                                () => FindVisibleFileNameEditScoped(processId, exportDialog),
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

        private static AutomationElement? FindImageQualityDialogScoped(
            int? processId,
            params AutomationElement?[] preferRoots)
        {
            // Insights：嵌套在「保存影片/图像堆栈」下，窗口 Name=「JPEG 质量」
            // 先认窗体，再在 Confirm 时找含「确定」的按钮（避免 require 精确按钮名导致漏检）
            return FindJpegQualityWindowCandidate(processId, preferRoots);
        }

        private static AutomationElement? FindJpegQualityWindowCandidate(
            int? processId,
            params AutomationElement?[] preferRoots)
        {
            foreach (var root in preferRoots)
            {
                if (root == null)
                {
                    continue;
                }

                try
                {
                    var found = FindJpegQualityWindowInRoot(root);
                    if (found != null)
                    {
                        return found;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // continue
                }
            }

            if (processId is > 0)
            {
                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId.Value))
                {
                    try
                    {
                        var name = topWindow.Current.Name ?? string.Empty;
                        if (IsJpegQualityWindowName(name) && IsProgressUiVisible(topWindow))
                        {
                            return topWindow;
                        }

                        var nested = FindJpegQualityWindowInRoot(topWindow);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // continue
                    }
                }
            }

            return null;
        }

        private static AutomationElement? FindJpegQualityWindowInRoot(AutomationElement root)
        {
            foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, condition))
                {
                    try
                    {
                        var name = element.Current.Name ?? string.Empty;
                        if (!IsJpegQualityWindowName(name))
                        {
                            continue;
                        }

                        if (IsProgressUiVisible(element))
                        {
                            return element;
                        }
                    }
                    catch (ElementNotAvailableException)
                    {
                        // continue
                    }
                }
            }

            return null;
        }

        private static bool IsJpegQualityWindowName(string name) =>
            name.Contains(SliceExportJpegQualityWindowName, StringComparison.Ordinal)
            || (name.Contains("JPEG", StringComparison.OrdinalIgnoreCase)
                && name.Contains("质量", StringComparison.Ordinal));

        /// <summary>在质量对话框内查找名称含「确定」的按钮（Insights：按钮 Name=确定）。</summary>
        private static AutomationElement? FindJpegQualityConfirmButton(AutomationElement qualityDialog)
        {
            var byName = UIAutomationHelper.FindButtonByName(
                    qualityDialog,
                    SliceExportQualityConfirmButtonName,
                    exactMatch: true)
                ?? UIAutomationHelper.FindButtonByName(
                    qualityDialog,
                    SliceExportQualityConfirmButtonName,
                    exactMatch: false);
            if (byName != null)
            {
                return byName;
            }

            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            foreach (AutomationElement button in qualityDialog.FindAll(TreeScope.Descendants, condition))
            {
                try
                {
                    var name = button.Current.Name ?? string.Empty;
                    if (!name.Contains(SliceExportQualityConfirmButtonName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (button.Current.IsEnabled && IsProgressUiVisible(button))
                    {
                        return button;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // continue
                }
            }

            return null;
        }

        private async Task DismissImageQualityDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            int? processId,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("检查是否出现「JPEG 质量」对话框（有则点确定）");
            _logger.Info("检查「JPEG 质量」对话框（可选）");

            AutomationElement? FindQualityDialog() =>
                FindImageQualityDialogScoped(processId, exportDialog);

            var qualityDialog = await UIAutomationHelper.WaitForElementAsync(
                    FindQualityDialog,
                    TimeSpan.FromMilliseconds(settings.SliceExportDialogWaitTime),
                    cancellationToken,
                    pollIntervalMs: 200)
                .ConfigureAwait(false);

            if (qualityDialog == null)
            {
                _logger.Info("未出现「JPEG 质量」对话框，继续流程");
                return;
            }

            progress?.Report("已检测到「JPEG 质量」，点击含「确定」的按钮");
            _logger.Info("已检测到「JPEG 质量」对话框，准备点击确定");

            await ConfirmJpegQualityDialogAsync(
                    settings,
                    qualityDialog,
                    FindQualityDialog,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }

        private async Task ConfirmJpegQualityDialogAsync(
            CTAutoSlicerSettings settings,
            AutomationElement qualityDialog,
            Func<AutomationElement?> findQualityDialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var okButton = FindJpegQualityConfirmButton(qualityDialog);
            if (okButton == null)
            {
                _logger.Warn("「JPEG 质量」内未找到含「确定」的按钮，尝试 Enter");
            }
            else
            {
                var buttonName = okButton.Current.Name ?? SliceExportQualityConfirmButtonName;
                if (UIAutomationHelper.TryInvokeButton(okButton))
                {
                    progress?.Report($"JPEG 质量：Invoke「{buttonName}」");
                    _logger.Info($"JPEG 质量：Invoke「{buttonName}」");
                    if (await WaitUntilDisappearedAsync(
                                findQualityDialog,
                                settings.SliceExportDialogWaitTime,
                                cancellationToken)
                            .ConfigureAwait(false))
                    {
                        progress?.Report("JPEG 质量对话框已确认");
                        _logger.Info("JPEG 质量对话框已确认");
                        return;
                    }
                }

                progress?.Report($"JPEG 质量：Invoke 无效，Win32 点击「{buttonName}」");
                _logger.Info($"JPEG 质量：Invoke 无效，Win32 点击「{buttonName}」");
                await NativeClickElementAsync(okButton, cancellationToken).ConfigureAwait(false);
                if (await WaitUntilDisappearedAsync(
                            findQualityDialog,
                            settings.SliceExportDialogWaitTime,
                            cancellationToken)
                        .ConfigureAwait(false))
                {
                    progress?.Report("JPEG 质量对话框已确认");
                    _logger.Info("JPEG 质量对话框已确认");
                    return;
                }
            }

            progress?.Report("JPEG 质量：点击无效，Enter 确认");
            _logger.Info("JPEG 质量：点击无效，Enter 确认");
            await FocusAutomationWindowAsync(qualityDialog, cancellationToken).ConfigureAwait(false);
            await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);

            if (!await WaitUntilDisappearedAsync(
                        findQualityDialog,
                        settings.SliceExportDialogWaitTime,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    "未能关闭「JPEG 质量」对话框（已尝试 Invoke / Win32 点击含「确定」按钮 / Enter）");
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
            // 质量框偶发晚于「保存图像」关闭才弹出：写入等待前再兜底一次
            await TryDismissLateJpegQualityDialogAsync(
                    settings,
                    ctViewerProcess,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            const int pollIntervalMs = 500;
            var progressWindow = await UIAutomationHelper.WaitForElementAsync(
                    () =>
                    {
                        try
                        {
                            var main = TryGetMainWindowElement(ctViewerProcess);
                            return main == null ? null : FindVisibleExportWriteProgressWindow(main);
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
                // 可能卡在质量框导致写入进度未出现：再试一次质量确认
                await TryDismissLateJpegQualityDialogAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);
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

                await TryDismissLateJpegQualityDialogAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                AutomationElement? visibleProgress;
                try
                {
                    var main = TryGetMainWindowElement(ctViewerProcess);
                    visibleProgress = main == null ? null : FindVisibleExportWriteProgressWindow(main);
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

                await Task.Delay(pollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待导出写入完成超时（{settings.SliceExportWriteTimeoutMs} ms）：「请耐心等待.../正在写入图像」进度框未消失");
        }

        /// <summary>
        /// 若当前已出现「JPEG 质量」则立即点含「确定」的按钮；未出现则立刻返回（不空等）。
        /// </summary>
        private async Task TryDismissLateJpegQualityDialogAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            int? processId = null;
            try
            {
                processId = ctViewerProcess.Id;
            }
            catch
            {
                return;
            }

            var qualityDialog = FindImageQualityDialogScoped(processId);
            if (qualityDialog == null)
            {
                return;
            }

            progress?.Report("检测到晚弹出的「JPEG 质量」，点击含「确定」的按钮");
            _logger.Info("检测到晚弹出的「JPEG 质量」对话框，点击确定");
            await ConfirmJpegQualityDialogAsync(
                    settings,
                    qualityDialog,
                    () => FindImageQualityDialogScoped(processId),
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
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

                var volumeItems = GetVolumeQueueItems(settings.CTFileFolder);
                if (volumeItems.Count == 0)
                {
                    throw new InvalidOperationException(
                        "CT 体素文件夹内未找到 .vgl/.vgi 文件或体素快捷方式（用于命名规则）");
                }

                var vglPath = volumeItems[0].ImportPath;
                progress?.Report($"切片导出单步测试：请确保 VG 已导入体积、已应用预设并完成厚板设置；命名参考 {Path.GetFileName(vglPath)}");
                _logger.Info($"切片导出单步测试开始，参考文件 {vglPath}");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
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
                _activeVgProcess = null;
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

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
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
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 单步测试：截图 → 掩膜优化 → 峰谷半坡 → 拖拽红线到位。
        /// </summary>
        public async Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (!settings.HasHistogramRoi)
                {
                    throw new InvalidOperationException("尚未标定直方图区域，请先点击「截图标定」框选 ROI");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report("直方图灰度单步测试：请确保 VG 中已显示直方图");
                _logger.Info(
                    $"直方图灰度单步测试开始，ROI=({settings.HistogramRoiX},{settings.HistogramRoiY}," +
                    $"{settings.HistogramRoiWidth}x{settings.HistogramRoiHeight})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                await ApplyHistogramGrayScaleAdjustAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("直方图灰度调整单步测试完成（截图+峰谷+拖拽）");
                _logger.Info("直方图灰度单步测试完成");
            }
            finally
            {
                Unlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 单步测试：3D 截图 → 本地剪影四角找点 → 打开「简单 3-2-1 对齐」→ Ctrl+点击四点 → 完成。
        /// 暂不使用 CTAutoCutImg.dll，便于验证 OpenCV 找点。
        /// </summary>
        public async Task TestSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (!settings.HasVision3dRoi)
                {
                    throw new InvalidOperationException("尚未标定 3D 视图区域，请先在「阶段四视觉对齐」中标定 3D 区域");
                }

                if (!settings.HasVisionTopViewRoi)
                {
                    throw new InvalidOperationException(
                        "尚未标定「上」视图区域，请先在「阶段四视觉对齐」中标定「上」视图（向量两点需要）");
                }

                if (!settings.HasSimple321ToolbarClick)
                {
                    throw new InvalidOperationException(
                        "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框");
                }

                if (!settings.HasSimple321OffsetEdit)
                {
                    throw new InvalidOperationException(
                        "尚未标定三点后偏移 Edit 点击位置，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report("简单 3-2-1 对齐测试（本地剪影找点，不调用视觉 DLL）：请确保 VG 已导入体积且第四象限 3D 可见");
                _logger.Info(
                    $"简单 3-2-1 对齐测试开始（本地 Vision3dSectionPointsDetector），3D ROI=({settings.Vision3dRoiX},{settings.Vision3dRoiY}," +
                    $"{settings.Vision3dRoiWidth}x{settings.Vision3dRoiHeight})，" +
                    $"下拉点击=({settings.Simple321ToolbarClickX},{settings.Simple321ToolbarClickY})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);

                await RunSimple321AlignmentAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("简单 3-2-1 对齐测试完成");
                _logger.Info("简单 3-2-1 对齐测试完成");
            }
            finally
            {
                Unlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 阶段四：截 3D → 本地剪影找点 → 打开对齐 →
        /// Ctrl+三点定平面 → 偏移 Edit 置 0 → 「上」视图两点定向量 → Ctrl+原点 → 完成。
        /// 注意：向量两点必须在原点之前，否则「上」标定区域画面会变。
        /// </summary>
        private async Task RunSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasVision3dRoi)
            {
                throw new InvalidOperationException("尚未标定 3D 视图 ROI，无法执行简单 3-2-1 对齐");
            }

            if (!settings.HasVisionTopViewRoi)
            {
                throw new InvalidOperationException(
                    "尚未标定「上」视图 ROI，无法执行向量两点（请先在「阶段四视觉对齐」中标定「上」视图区域）");
            }

            progress?.Report("截取第四象限 3D 视图 ROI…");
            var capturePath = CaptureVision3dRoiToFile(settings);
            progress?.Report($"3D 截图已保存：{capturePath}");
            _logger.Info($"3D ROI 截图：{capturePath}");

            progress?.Report("本地剪影算法找点（Vision3dSectionPointsDetector，不调用 DLL）…");
            var captureDir = Path.GetDirectoryName(capturePath)!;
            var detectResult = Vision3dSectionPointsDetector.Detect(capturePath, debugDir: captureDir);
            var relativePoints = detectResult.Points;
            if (relativePoints.Length < 4)
            {
                throw new InvalidOperationException(
                    $"本地找点返回点数不足：期望 4，实际 {relativePoints.Length}");
            }

            progress?.Report($"本地找点：{detectResult.Summary}");
            _logger.Info($"本地找点：{detectResult.Summary}");

            // 默认顺序：左上、右上、右下（上盖三点）+ 原点；原点须在向量两点之后点击
            var planeScreenPoints = new (int X, int Y, string Label)[3];
            var planeAnnotatePoints = new (int X, int Y, string Label)[3];
            var planeLabels = new[] { "左上", "右上", "右下" };
            for (var i = 0; i < 3; i++)
            {
                var (sx, sy) = CTAutoSlicerSettings.ToScreenPoint(
                    settings.Vision3dRoiX,
                    settings.Vision3dRoiY,
                    relativePoints[i].X,
                    relativePoints[i].Y);
                var label = planeLabels[i];
                planeScreenPoints[i] = (sx, sy, label);
                planeAnnotatePoints[i] = (relativePoints[i].X, relativePoints[i].Y, label);
                progress?.Report(
                    $"{label}：相对=({relativePoints[i].X},{relativePoints[i].Y}) → 屏幕=({sx},{sy})");
                _logger.Info(
                    $"{label}：相对=({relativePoints[i].X},{relativePoints[i].Y}) → 屏幕=({sx},{sy})");
            }

            var (originSx, originSy) = CTAutoSlicerSettings.ToScreenPoint(
                settings.Vision3dRoiX,
                settings.Vision3dRoiY,
                relativePoints[3].X,
                relativePoints[3].Y);
            progress?.Report(
                $"原点：相对=({relativePoints[3].X},{relativePoints[3].Y}) → 屏幕=({originSx},{originSy})（稍后点击）");
            _logger.Info(
                $"原点：相对=({relativePoints[3].X},{relativePoints[3].Y}) → 屏幕=({originSx},{originSy})");

            try
            {
                var annotatedPath = Path.Combine(
                    captureDir,
                    Path.GetFileNameWithoutExtension(capturePath) + "_click_points.png");
                var annotateAll = new (int X, int Y, string Label)[4];
                for (var i = 0; i < 3; i++)
                {
                    annotateAll[i] = planeAnnotatePoints[i];
                }

                annotateAll[3] = (relativePoints[3].X, relativePoints[3].Y, "原点");
                ScreenCaptureHelper.SaveAnnotatedPointsImage(capturePath, annotatedPath, annotateAll);
                progress?.Report($"四点点击标注图已保存：{annotatedPath}（另有检测器 _points/_mask）");
                _logger.Info($"3D 四点点击标注图：{annotatedPath}");
            }
            catch (Exception annotateEx)
            {
                progress?.Report($"四点标注图保存失败（继续流程）：{annotateEx.Message}");
                _logger.Warn($"3D 四点标注图保存失败：{annotateEx.Message}");
            }

            progress?.Report("打开「简单 3-2-1 对齐」…");
            await OpenSimple321AlignmentAsync(
                    settings,
                    ctViewerProcess,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            // —— 步骤1：三点定平面（左上→右上→右下）——
            progress?.Report("步骤1/3：Ctrl+左键依次点击上盖三点（左上→右上→右下）…");
            foreach (var point in planeScreenPoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Ctrl+点击 {point.Label} @({point.X},{point.Y})");
                await CtrlClickScreenAsync(point.X, point.Y, cancellationToken).ConfigureAwait(false);
                var interval = Math.Max(100, settings.VisionClickIntervalMs);
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            // —— 步骤1.5：左侧无名偏移 Edit 置 0 + Enter（否则步骤2「上」视图非预期）——
            await ResetSimple321PlaneOffsetEditAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            // —— 步骤2：「上」视图右侧外缘两点（必须在原点之前）——
            // 三点+置0后短等，让 VG 刷新「上」标定区高亮后再截图
            await Task.Delay(Math.Max(200, settings.VisionClickIntervalMs), cancellationToken)
                .ConfigureAwait(false);
            progress?.Report("步骤2/3：「上」视图向量两点（须先于原点，否则标定区会变）…");
            await ClickTopViewVectorPointsAsync(settings, cancellationToken, progress)
                .ConfigureAwait(false);

            // —— 步骤3：3D 原点 ——
            progress?.Report($"步骤3/3：Ctrl+点击原点 @({originSx},{originSy})…");
            await CtrlClickScreenAsync(originSx, originSy, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(100, settings.VisionClickIntervalMs), cancellationToken)
                .ConfigureAwait(false);

            progress?.Report("点击「完成(F)」…");
            await ClickSimple321FinishAsync(settings, mainWindow, cancellationToken, progress)
                .ConfigureAwait(false);

            await TryDismissAlignedContinueWarningAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("简单 3-2-1 对齐流程完成（3 平面点 + 偏移置0 + 2 向量点 + 1 原点）");
        }

        /// <summary>
        /// 三点定平面后：点击左侧无名偏移 Edit → 输入 0 → Enter。
        /// 优先 UIA 定位（空 Name / 值含 mm，靠近标定点）；失败则绝对坐标点击 + 键盘。
        /// </summary>
        private async Task ResetSimple321PlaneOffsetEditAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasSimple321OffsetEdit)
            {
                throw new InvalidOperationException(
                    "尚未标定三点后偏移 Edit，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框");
            }

            progress?.Report(
                $"步骤1.5：偏移 Edit 置 0 @({settings.Simple321OffsetEditX},{settings.Simple321OffsetEditY})…");
            _logger.Info(
                $"简单 3-2-1：偏移 Edit 置 0 ({settings.Simple321OffsetEditX},{settings.Simple321OffsetEditY})");

            var edit = FindSimple321OffsetEdit(mainWindow, settings);
            Unlock();
            try
            {
                if (edit != null)
                {
                    var rect = edit.Current.BoundingRectangle;
                    progress?.Report($"已 UIA 定位偏移 Edit Rect={rect}，点击并写入 0");
                    _logger.Info($"偏移 Edit UIA Rect={rect}");
                    await NativeClickElementWhileUnlockedAsync(edit, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);

                    if (!UIAutomationHelper.TrySetEditValueSafe(edit, "0"))
                    {
                        await TypeTextIntoFocusedControlAsync("0").ConfigureAwait(false);
                    }
                }
                else
                {
                    progress?.Report("UIA 未找到偏移 Edit，使用标定绝对坐标点击后键盘输入 0");
                    _logger.Warn("偏移 Edit UIA 未命中，回退绝对坐标");
                    await InvokeOnUiThreadAsync(() =>
                            WinAPIHelper.ClickScreen(
                                settings.Simple321OffsetEditX,
                                settings.Simple321OffsetEditY))
                        .ConfigureAwait(false);
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                    await TypeTextIntoFocusedControlAsync("0").ConfigureAwait(false);
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
                await Task.Delay(Math.Max(150, settings.VisionClickIntervalMs), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("偏移 Edit 已置 0 并回车");
            _logger.Info("简单 3-2-1：偏移 Edit 置 0 + Enter 完成");
        }

        /// <summary>
        /// 在主窗左侧寻找无名 Edit（常显示「x.xx mm」）；优先与标定点中心距最近。
        /// </summary>
        private static AutomationElement? FindSimple321OffsetEdit(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings)
        {
            System.Windows.Rect mainRect;
            try
            {
                mainRect = mainWindow.Current.BoundingRectangle;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            if (mainRect.Width <= 0 || mainRect.Height <= 0)
            {
                return null;
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

            AutomationElementCollection elements;
            try
            {
                elements = mainWindow.FindAll(TreeScope.Descendants, condition);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            AutomationElement? nearest = null;
            var nearestDist = double.MaxValue;
            const double maxCenterDistancePx = 40;

            foreach (AutomationElement element in elements)
            {
                try
                {
                    if (!element.Current.IsEnabled || !element.Current.IsKeyboardFocusable)
                    {
                        continue;
                    }

                    var name = element.Current.Name ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var r = element.Current.BoundingRectangle;
                    // Insights 约 95×19；过滤过大/过小
                    if (r.Width is < 40 or > 220 || r.Height is < 12 or > 40)
                    {
                        continue;
                    }

                    // 偏主窗左侧
                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    if (cx > mainRect.Left + mainRect.Width * 0.35)
                    {
                        continue;
                    }

                    var value = UIAutomationHelper.TryGetInputValue(element) ?? string.Empty;
                    var looksLikeMm = value.Contains("mm", StringComparison.OrdinalIgnoreCase)
                        || value.Contains('.', StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(value);

                    if (!looksLikeMm)
                    {
                        continue;
                    }

                    var dx = cx - settings.Simple321OffsetEditX;
                    var dy = cy - settings.Simple321OffsetEditY;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = element;
                    }
                }
                catch (ElementNotAvailableException)
                {
                }
            }

            if (nearest != null && nearestDist <= maxCenterDistancePx)
            {
                return nearest;
            }

            return null;
        }

        /// <summary>
        /// 简单 3-2-1 步骤2：截取「上」视图 → 本地右缘两点 → 标注落盘 → Ctrl+从上到下点击。
        /// </summary>
        private async Task ClickTopViewVectorPointsAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("截取第二象限「上」视图 ROI…");
            var capturePath = CaptureVisionTopViewRoiToFile(settings);
            progress?.Report($"「上」视图截图已保存：{capturePath}");
            _logger.Info($"「上」视图 ROI 截图：{capturePath}");

            progress?.Report("本地算法找右侧外缘向量两点（VisionTopViewVectorPointsDetector）…");
            var captureDir = Path.GetDirectoryName(capturePath)!;
            var detectResult = VisionTopViewVectorPointsDetector.Detect(capturePath, debugDir: captureDir);
            var relativePoints = detectResult.Points;
            if (relativePoints.Length < 2)
            {
                throw new InvalidOperationException(
                    $"向量找点返回点数不足：期望 2，实际 {relativePoints.Length}");
            }

            // 强制从上到下
            var top = relativePoints[0];
            var bottom = relativePoints[1];
            if (bottom.Y < top.Y)
            {
                (top, bottom) = (bottom, top);
            }

            progress?.Report($"向量找点：{detectResult.Summary}");
            _logger.Info($"向量找点：{detectResult.Summary}");

            var screenPoints = new (int X, int Y, string Label)[2];
            var annotatePoints = new (int X, int Y, string Label)[2];
            var labels = new[] { "向量上", "向量下" };
            var rel = new[] { top, bottom };
            for (var i = 0; i < 2; i++)
            {
                var (sx, sy) = CTAutoSlicerSettings.ToScreenPoint(
                    settings.VisionTopViewRoiX,
                    settings.VisionTopViewRoiY,
                    rel[i].X,
                    rel[i].Y);
                screenPoints[i] = (sx, sy, labels[i]);
                annotatePoints[i] = (rel[i].X, rel[i].Y, labels[i]);
                progress?.Report(
                    $"{labels[i]}：相对=({rel[i].X},{rel[i].Y}) → 屏幕=({sx},{sy})");
                _logger.Info(
                    $"{labels[i]}：相对=({rel[i].X},{rel[i].Y}) → 屏幕=({sx},{sy})");
            }

            try
            {
                var annotatedPath = Path.Combine(
                    captureDir,
                    Path.GetFileNameWithoutExtension(capturePath) + "_click_points.png");
                ScreenCaptureHelper.SaveAnnotatedPointsImage(capturePath, annotatedPath, annotatePoints);
                progress?.Report($"向量两点标注图已保存：{annotatedPath}");
                _logger.Info($"「上」视图向量标注图：{annotatedPath}");
            }
            catch (Exception annotateEx)
            {
                progress?.Report($"向量标注图保存失败（继续流程）：{annotateEx.Message}");
                _logger.Warn($"「上」视图向量标注图保存失败：{annotateEx.Message}");
            }

            progress?.Report("Ctrl+左键从上到下点击向量两点…");
            foreach (var point in screenPoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Ctrl+点击 {point.Label} @({point.X},{point.Y})");
                await CtrlClickScreenAsync(point.X, point.Y, cancellationToken).ConfigureAwait(false);
                var interval = Math.Max(100, settings.VisionClickIntervalMs);
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }

        private string CaptureVision3dRoiToFile(CTAutoSlicerSettings settings)
        {
            var dayDir = settings.EnsureVisionCaptureDayDir();
            var cleanup = settings.CleanupExpiredVisionCaptures();
            if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
            {
                _logger.Info(
                    $"视觉截图清理：删除目录 {cleanup.DeletedDirs}、文件 {cleanup.DeletedFiles}" +
                    $"（保留 {settings.VisionCaptureKeepDays} 天）");
            }

            var filePath = Path.Combine(dayDir, $"vision_3d_{DateTime.Now:HHmmss_fff}.png");
            ScreenCaptureHelper.CaptureRegionAndSave(
                filePath,
                settings.Vision3dRoiX,
                settings.Vision3dRoiY,
                settings.Vision3dRoiWidth,
                settings.Vision3dRoiHeight);
            return filePath;
        }

        private string CaptureVisionTopViewRoiToFile(CTAutoSlicerSettings settings)
        {
            var dayDir = settings.EnsureVision2dVectorCaptureDayDir();
            var cleanup = settings.CleanupExpiredVision2dVectorCaptures();
            if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
            {
                _logger.Info(
                    $"向量截图清理：删除目录 {cleanup.DeletedDirs}、文件 {cleanup.DeletedFiles}" +
                    $"（保留 {settings.Vision2dVectorCaptureKeepDays} 天）");
            }

            var filePath = Path.Combine(dayDir, $"vision_top_{DateTime.Now:HHmmss_fff}.png");
            ScreenCaptureHelper.CaptureRegionAndSave(
                filePath,
                settings.VisionTopViewRoiX,
                settings.VisionTopViewRoiY,
                settings.VisionTopViewRoiWidth,
                settings.VisionTopViewRoiHeight);
            return filePath;
        }

        /// <summary>
        /// 按标定绝对坐标点击测量工具栏「简单对齐」下拉 → 选菜单项 → 等待对话框。
        /// 下拉控件为 Qt，UIA 常无法识别，故第一步必须用屏幕坐标点击。
        /// </summary>
        private async Task OpenSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasSimple321ToolbarClick)
            {
                throw new InvalidOperationException(
                    "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框");
            }

            var clickX = settings.Simple321ToolbarClickX;
            var clickY = settings.Simple321ToolbarClickY;
            progress?.Report($"按标定坐标点击「简单对齐」下拉 @({clickX},{clickY})");
            _logger.Info($"简单 3-2-1：绝对定位点击下拉 ({clickX},{clickY})");
            await NativeClickScreenAsync(settings, clickX, clickY, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(100, settings.VisionClickIntervalMs), cancellationToken).ConfigureAwait(false);

            var menuItem = await UIAutomationHelper.WaitForElementAsync(
                    () => FindSimple321MenuItem(mainWindow, ctViewerProcess),
                    TimeSpan.FromMilliseconds(settings.VisionAlignmentDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (menuItem == null)
            {
                throw new InvalidOperationException(
                    $"未在 {settings.VisionAlignmentDialogWaitTime}ms 内出现菜单项「{Simple321MenuItemName}」" +
                    "（请确认下拉标定坐标准确，且点击后菜单已展开）");
            }

            progress?.Report($"选择菜单「{menuItem.Current.Name}」");
            if (!UIAutomationHelper.TryInvokeElement(menuItem))
            {
                await NativeClickElementAsync(menuItem, cancellationToken).ConfigureAwait(false);
            }

            var dialog = await UIAutomationHelper.WaitForElementAsync(
                    () => FindWindowByNameScoped(mainWindow, Simple321DialogNamePrefix, nameStartsWith: true),
                    TimeSpan.FromMilliseconds(settings.VisionAlignmentDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (dialog == null)
            {
                throw new InvalidOperationException(
                    $"未在 {settings.VisionAlignmentDialogWaitTime}ms 内出现「{Simple321DialogNamePrefix}...」对话框");
            }

            progress?.Report("已打开「简单 3-2-1 对齐」对话框");
            _logger.Info("简单 3-2-1 对齐对话框已打开");

            // 标点前先重置，清掉上次残留对齐状态，避免异常
            await ClickSimple321ResetAsync(dialog, cancellationToken, progress).ConfigureAwait(false);
        }

        /// <summary>
        /// 在「简单 3-2-1 对齐」对话框内点击「重置(R)」（Insights AccessKey Alt+R）。
        /// </summary>
        private async Task ClickSimple321ResetAsync(
            AutomationElement alignmentDialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report($"点击「{Simple321ResetButtonName}」…");
            _logger.Info($"简单 3-2-1：点击「{Simple321ResetButtonName}」后再标点");

            var resetButton = UIAutomationHelper.FindButtonByName(alignmentDialog, Simple321ResetButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    alignmentDialog,
                    ControlType.Button,
                    "重置",
                    nameContains: true)
                ?? throw new InvalidOperationException(
                    $"未找到按钮「{Simple321ResetButtonName}」，请确认「简单 3-2-1 对齐」对话框已完全加载");

            if (!UIAutomationHelper.TryInvokeButton(resetButton))
            {
                await NativeClickElementAsync(resetButton, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Math.Max(150, UiPollIntervalMs), cancellationToken).ConfigureAwait(false);
            progress?.Report($"「{Simple321ResetButtonName}」已点击，开始标点");
        }

        private static AutomationElement? FindSimple321MenuItem(
            AutomationElement mainWindow,
            Process ctViewerProcess)
        {
            var inMain = UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.MenuItem,
                    Simple321MenuItemName)
                ?? UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.MenuItem,
                    Simple321MenuItemNameContains,
                    nameContains: true);
            if (inMain != null)
            {
                return inMain;
            }

            try
            {
                var processId = ctViewerProcess.Id;
                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId))
                {
                    var item = UIAutomationHelper.FindDescendantByName(
                            topWindow,
                            ControlType.MenuItem,
                            Simple321MenuItemName)
                        ?? UIAutomationHelper.FindDescendantByName(
                            topWindow,
                            ControlType.MenuItem,
                            Simple321MenuItemNameContains,
                            nameContains: true);
                    if (item != null)
                    {
                        return item;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        private async Task ClickSimple321FinishAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var dialog = FindWindowByNameScoped(mainWindow, Simple321DialogNamePrefix, nameStartsWith: true)
                ?? throw new InvalidOperationException("未找到「简单 3-2-1 对齐」对话框，无法点击完成");

            var finishButton = UIAutomationHelper.FindButtonByName(dialog, Simple321FinishButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    dialog,
                    ControlType.Button,
                    "完成",
                    nameContains: true);

            if (finishButton == null)
            {
                throw new InvalidOperationException($"未找到按钮「{Simple321FinishButtonName}」");
            }

            if (!UIAutomationHelper.TryInvokeButton(finishButton))
            {
                await NativeClickElementAsync(finishButton, cancellationToken).ConfigureAwait(false);
            }

            // 「完成」后常立刻弹出「VGSTUDIO MAX 警告」；若不先点「是」，对齐对话框不会关闭，会卡在消失等待。
            await WaitSimple321DialogClosedHandlingWarningAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("「完成」已点击，对齐对话框已关闭");
        }

        /// <summary>
        /// 等待「简单 3-2-1 对齐」关闭；期间若出现「VGSTUDIO MAX 警告」则点「是(Y)」。
        /// </summary>
        private async Task WaitSimple321DialogClosedHandlingWarningAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var timeout = TimeSpan.FromMilliseconds(Math.Max(settings.VisionAlignmentDialogWaitTime, 8000));
            var deadline = DateTime.UtcNow + timeout;
            var warningHandled = false;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var warning = FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName);
                if (warning != null)
                {
                    progress?.Report(
                        $"检测到「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
                    _logger.Info($"简单 3-2-1：对齐完成确认警告，点击「{VgAlignedWarningYesButtonName}」");
                    await ClickAlignedWarningYesAsync(warning, cancellationToken).ConfigureAwait(false);
                    warningHandled = true;
                    await Task.Delay(UiPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var alignmentDialog = FindWindowByNameScoped(
                    mainWindow,
                    Simple321DialogNamePrefix,
                    nameStartsWith: true);
                if (alignmentDialog == null)
                {
                    if (!warningHandled)
                    {
                        // 对齐窗已关、警告未出现：再短等一下，避免漏掉晚弹出的警告
                        var lateWarning = await UIAutomationHelper.WaitForElementAsync(
                                () => FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName),
                                TimeSpan.FromMilliseconds(800),
                                cancellationToken,
                                UiPollIntervalMs)
                            .ConfigureAwait(false);
                        if (lateWarning != null)
                        {
                            progress?.Report(
                                $"对齐窗关闭后出现「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
                            await ClickAlignedWarningYesAsync(lateWarning, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }

                    return;
                }

                progress?.Report("等待「简单 3-2-1 对齐」对话框关闭（若有警告将先点「是」）...");
                await Task.Delay(UiPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待「简单 3-2-1 对齐」关闭超时（{timeout.TotalMinutes:0.#} 分钟）。" +
                $"若一直停在「{VgAlignedWarningDialogName}」，请确认「{VgAlignedWarningYesButtonName}」可被识别。");
        }

        private async Task ClickAlignedWarningYesAsync(
            AutomationElement warningDialog,
            CancellationToken cancellationToken)
        {
            var yesButton = UIAutomationHelper.FindButtonByName(warningDialog, VgAlignedWarningYesButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    warningDialog,
                    ControlType.Button,
                    "是",
                    nameContains: true)
                ?? throw new InvalidOperationException(
                    $"已出现「{VgAlignedWarningDialogName}」，但未找到按钮「{VgAlignedWarningYesButtonName}」");

            if (!UIAutomationHelper.TryInvokeButton(yesButton))
            {
                await NativeClickElementAsync(yesButton, cancellationToken).ConfigureAwait(false);
            }

            // 给模态框一点关闭时间；外层循环会继续轮询
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 「完成」后可选弹窗兜底（对齐窗已关、警告稍晚出现时）。
        /// </summary>
        private async Task TryDismissAlignedContinueWarningAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var waitMs = Math.Clamp(settings.VisionAlignmentDialogWaitTime, 800, 2000);
            progress?.Report($"再次确认是否仍有「{VgAlignedWarningDialogName}」（最多 {waitMs}ms）…");

            var warning = await UIAutomationHelper.WaitForElementAsync(
                    () => FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName),
                    TimeSpan.FromMilliseconds(waitMs),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (warning == null)
            {
                progress?.Report($"无残留「{VgAlignedWarningDialogName}」");
                return;
            }

            progress?.Report($"仍有「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
            await ClickAlignedWarningYesAsync(warning, cancellationToken).ConfigureAwait(false);
            progress?.Report($"已点击「{VgAlignedWarningYesButtonName}」");
            _logger.Info($"简单 3-2-1：兜底关闭警告窗「{VgAlignedWarningDialogName}」");
        }

        /// <summary>按住 Ctrl 左键点击屏幕坐标（3-2-1 选点）。</summary>
        private async Task CtrlClickScreenAsync(int x, int y, CancellationToken cancellationToken)
        {
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    WinAPIHelper.SetCursorPos(x, y);
                    Thread.Sleep(60);
                    _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    Thread.Sleep(40);
                    WinAPIHelper.LeftClickAt(x, y);
                    Thread.Sleep(40);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 主流程/单测共用：截图 → 峰谷半坡 → 拖红线。未标定 ROI 时跳过（不中断导入）。
        /// </summary>
        private async Task ApplyHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasHistogramRoi)
            {
                progress?.Report("跳过直方图灰度调整：尚未标定 ROI（请先「截图标定」）");
                _logger.Info("跳过直方图灰度调整：尚未标定 ROI");
                return;
            }

            progress?.Report("开始直方图灰度调整（截图→峰谷→拖红线）");
            _logger.Info(
                $"直方图灰度调整：ROI=({settings.HistogramRoiX},{settings.HistogramRoiY}," +
                $"{settings.HistogramRoiWidth}x{settings.HistogramRoiHeight})");

            await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            var capture = await CaptureHistogramRoiAsync(settings, cancellationToken, progress)
                .ConfigureAwait(false);

            progress?.Report($"截图已保存：{capture.FilePath}");
            if (!string.IsNullOrWhiteSpace(capture.CompareImagePath))
            {
                progress?.Report($"优化对比图：{capture.CompareImagePath}（掩膜像素 {capture.MaskPixelCount}）");
            }

            if (!string.IsNullOrWhiteSpace(capture.PeaksImagePath))
            {
                progress?.Report($"峰谷调试图：{capture.PeaksImagePath}");
            }

            progress?.Report(
                $"红线初始位置：相对列={capture.RedLineColumn?.ToString() ?? "未检测到"}，" +
                $"屏幕X={capture.RedLineScreenX?.ToString() ?? "-"}，拖拽Y={capture.DragScreenY}");

            if (!capture.PeaksSuccess)
            {
                throw new InvalidOperationException(
                    $"峰谷分析失败：{capture.PeaksFailReason ?? "未知原因"}");
            }

            progress?.Report(
                $"峰谷结果：左线(谷)={capture.ValleyX}, 右线={capture.RightPeakX}, 中线={capture.TargetX} " +
                $"（两尖峰参考 P1={capture.LeftPeakX}；屏幕中线X={capture.TargetScreenX}）");
            _logger.Info(
                $"峰谷：P1={capture.LeftPeakX} 左线={capture.ValleyX} 右线={capture.RightPeakX} 中线={capture.TargetX}, " +
                $"red={capture.RedLineColumn}, compare={capture.CompareImagePath}, peaks={capture.PeaksImagePath}");

            await DragRedLineToTargetAsync(capture, cancellationToken, progress).ConfigureAwait(false);
            progress?.Report("直方图灰度调整完成");
            _logger.Info("直方图灰度调整完成");
        }

        /// <summary>
        /// 将红线从当前位置拖到半坡目标列。
        /// </summary>
        private async Task DragRedLineToTargetAsync(
            HistogramCaptureResult capture,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (capture.TargetScreenX is not int targetX)
            {
                throw new InvalidOperationException("半坡目标屏幕坐标无效");
            }

            var startX = capture.RedLineScreenX
                ?? (capture.RoiX + Math.Max(1, capture.RoiWidth / 4));
            var y = (short)Math.Clamp(capture.DragScreenY, short.MinValue, short.MaxValue);
            var sx = (short)Math.Clamp(startX, short.MinValue, short.MaxValue);
            var ex = (short)Math.Clamp(targetX, short.MinValue, short.MaxValue);

            if (Math.Abs(ex - sx) < 2)
            {
                progress?.Report($"红线已接近目标（ΔX={ex - sx}），跳过拖拽");
                _logger.Info($"红线拖拽跳过：start={sx} end={ex}");
                return;
            }

            var path = HistogramGrayScaleAnalyzer.BuildHorizontalDragPath(sx, y, ex);
            progress?.Report($"拖拽红线：({sx},{y}) → ({ex},{y})，路径点 {path.Count}");
            _logger.Info($"拖拽红线：({sx},{y}) → ({ex},{y})");

            // BlockInput 下 SharpHook 仍可模拟；短暂 Unlock 提高部分环境跟手性
            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelayMs: 4);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("红线拖拽完成");
        }

        /// <summary>
        /// 单步测试：对齐后捕捉无名按钮（UIA 索引/Invoke，失败则绝对坐标）→ 按箭头标定拖拽。
        /// </summary>
        public async Task TestPostAlignCaptureDragAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (!settings.HasPostAlignCaptureDragPrerequisites)
                {
                    throw new InvalidOperationException(
                        "尚未标定「对齐后捕捉」按钮坐标与拖拽箭头，请先在对应 Expander 中完成标定");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report(
                    "对齐后捕捉拖拽测试：请确保 VG 已打开且 3-2-1 已完成（或处于可执行拖拽的视图状态）");
                _logger.Info(
                    $"对齐后捕捉拖拽测试：按钮=({settings.PostAlignCaptureButtonX},{settings.PostAlignCaptureButtonY}) " +
                    $"index={settings.PostAlignCaptureButtonIndex} " +
                    $"drag ({settings.PostAlignDragStartX},{settings.PostAlignDragStartY})→" +
                    $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);

                await RunPostAlignCaptureDragAsync(
                        settings,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("对齐后捕捉拖拽测试完成");
                _logger.Info("对齐后捕捉拖拽测试完成");
            }
            finally
            {
                Unlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 点击捕捉按钮 → 等待 → 沿标定箭头直线拖拽。
        /// </summary>
        private async Task RunPostAlignCaptureDragAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ClickPostAlignCaptureButtonAsync(settings, mainWindow, cancellationToken, progress)
                .ConfigureAwait(false);

            var waitMs = Math.Max(0, settings.PostAlignClickWaitMs);
            if (waitMs > 0)
            {
                progress?.Report($"等待 {waitMs}ms 后开始拖拽…");
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }

            await DragPostAlignArrowAsync(settings, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task ClickPostAlignCaptureButtonAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var button = FindPostAlignCaptureButton(mainWindow, settings);
            if (button != null)
            {
                var name = button.Current.Name ?? string.Empty;
                var rect = button.Current.BoundingRectangle;
                progress?.Report(
                    $"方案一：定位无名按钮（Name=「{name}」）Rect={rect}，尝试 Invoke…");
                _logger.Info($"对齐后捕捉：UIA 按钮 Rect={rect}");

                if (UIAutomationHelper.TryInvokeElement(button))
                {
                    progress?.Report("捕捉按钮已通过 InvokePattern 触发");
                    _logger.Info("对齐后捕捉：Invoke 成功");
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                    return;
                }

                progress?.Report("Invoke 失败，Win32 点击按钮中心");
                await NativeClickElementAsync(button, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!settings.HasPostAlignCaptureButton)
            {
                throw new InvalidOperationException(
                    "未在 VG 主窗内定位到无名捕捉按钮，且尚未标定绝对坐标");
            }

            progress?.Report(
                $"UIA 未找到按钮，使用标定绝对坐标 ({settings.PostAlignCaptureButtonX},{settings.PostAlignCaptureButtonY})");
            _logger.Warn("对齐后捕捉：UIA 未命中，回退绝对坐标");
            await NativeClickScreenAsync(
                    settings,
                    settings.PostAlignCaptureButtonX,
                    settings.PostAlignCaptureButtonY,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 在主窗子树中收集视口底部工具栏无名小按钮，按 Left 排序；优先与标定坐标最近的矩形，否则按索引。
        /// </summary>
        private static AutomationElement? FindPostAlignCaptureButton(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings)
        {
            var candidates = CollectEmptyNameViewportToolbarButtons(mainWindow);
            if (candidates.Count == 0)
            {
                return null;
            }

            candidates.Sort((a, b) =>
            {
                var ra = a.Current.BoundingRectangle;
                var rb = b.Current.BoundingRectangle;
                var cmp = ra.Left.CompareTo(rb.Left);
                return cmp != 0 ? cmp : ra.Top.CompareTo(rb.Top);
            });

            if (settings.HasPostAlignCaptureButton)
            {
                const double maxCenterDistancePx = 28;
                AutomationElement? nearest = null;
                var nearestDist = double.MaxValue;
                foreach (var btn in candidates)
                {
                    var r = btn.Current.BoundingRectangle;
                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    var dx = cx - settings.PostAlignCaptureButtonX;
                    var dy = cy - settings.PostAlignCaptureButtonY;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = btn;
                    }
                }

                if (nearest != null && nearestDist <= maxCenterDistancePx)
                {
                    return nearest;
                }
            }

            var index = Math.Clamp(settings.PostAlignCaptureButtonIndex, 0, candidates.Count - 1);
            return candidates[index];
        }

        private static List<AutomationElement> CollectEmptyNameViewportToolbarButtons(AutomationElement mainWindow)
        {
            var result = new List<AutomationElement>();
            System.Windows.Rect mainRect;
            try
            {
                mainRect = mainWindow.Current.BoundingRectangle;
            }
            catch (ElementNotAvailableException)
            {
                return result;
            }

            if (mainRect.Width <= 0 || mainRect.Height <= 0)
            {
                return result;
            }

            var minCenterY = mainRect.Top + mainRect.Height * 0.65;
            var minCenterX = mainRect.Left + mainRect.Width * 0.35;

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

            AutomationElementCollection elements;
            try
            {
                elements = mainWindow.FindAll(TreeScope.Descendants, condition);
            }
            catch (ElementNotAvailableException)
            {
                return result;
            }

            foreach (AutomationElement element in elements)
            {
                try
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!element.Current.IsEnabled)
                    {
                        continue;
                    }

                    var r = element.Current.BoundingRectangle;
                    if (r.Width is < 8 or > 72 || r.Height is < 8 or > 72)
                    {
                        continue;
                    }

                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    if (cy < minCenterY || cx < minCenterX)
                    {
                        continue;
                    }

                    result.Add(element);
                }
                catch (ElementNotAvailableException)
                {
                    // 忽略瞬时失效节点
                }
            }

            return result;
        }

        private async Task DragPostAlignArrowAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasPostAlignDrag)
            {
                throw new InvalidOperationException("拖拽箭头尚未标定或起点终点重合");
            }

            var path = MouseSimulationHelper.BuildLinearDragPath(
                settings.PostAlignDragStartX,
                settings.PostAlignDragStartY,
                settings.PostAlignDragEndX,
                settings.PostAlignDragEndY);

            progress?.Report(
                $"沿箭头拖拽：({settings.PostAlignDragStartX},{settings.PostAlignDragStartY}) → " +
                $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})，路径点 {path.Count}");
            _logger.Info(
                $"对齐后箭头拖拽：({settings.PostAlignDragStartX},{settings.PostAlignDragStartY})→" +
                $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})");

            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                var stepDelay = Math.Max(1, settings.PostAlignDragStepDelayMs);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelay);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("箭头拖拽完成");
        }

        /// <summary>
        /// 按已标定屏幕坐标截取直方图 ROI，保存原图/对比图/峰谷图，并检测红线与半坡目标。
        /// </summary>
        public Task<HistogramCaptureResult> CaptureHistogramRoiAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
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

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var filePath = Path.Combine(dir, $"histogram_{stamp}.png");
            var comparePath = Path.Combine(dir, $"histogram_compare_{stamp}.png");
            var peaksPath = Path.Combine(dir, $"histogram_peaks_{stamp}.png");

            progress?.Report($"截取直方图 ROI ({roiX},{roiY},{roiW}x{roiH}) → {dir}");
            _logger.Info($"截取直方图 ROI → {filePath}");

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
