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
        // 进程池调度 / 后处理编排入口 / 主流程对齐与捕捉包装
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
                                    progress,
                                    namingSourcePath: readySlot.QueueEntryPath)
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
                            _logger.Error($"后处理失败：{readySlot.FilePath}", ex);
                            await AwaitManualInterventionThenArchiveFailedAsync(
                                    settings,
                                    readySlot.Process,
                                    readySlot.QueueEntryPath,
                                    fileName,
                                    reason,
                                    cancellationToken,
                                    progress)
                                .ConfigureAwait(false);
                        }
                        finally
                        {
                            KillVgProcess(readySlot.Process, $"后处理结束释放：{fileName}");
                            slots.Remove(readySlot);
                            _activeVgProcess = null;
                        }

                        continue;
                    }

                    // 处理导入失败槽：人工干预 → 归档 → 杀进程
                    var failedSlot = slots.Find(s => s.State == VgSlotState.Failed);
                    if (failedSlot != null)
                    {
                        var fileName = Path.GetFileName(failedSlot.FilePath);
                        var reason = failedSlot.Error == null
                            ? "导入等待失败/超时"
                            : GetFullExceptionMessage(failedSlot.Error);
                        failCount++;
                        progress?.Report($"{ImportFailProgressPrefix}{fileName}|{reason}");
                        _logger.Error($"导入失败：{failedSlot.FilePath} — {reason}");
                        await AwaitManualInterventionThenArchiveFailedAsync(
                                settings,
                                failedSlot.Process,
                                failedSlot.QueueEntryPath,
                                fileName,
                                reason,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false);
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
                            _logger.Error($"启动/开始导入失败：{vglPath}", ex);
                            await AwaitManualInterventionThenArchiveFailedAsync(
                                    settings,
                                    started,
                                    volumeItem.QueueEntryPath,
                                    fileName,
                                    reason,
                                    cancellationToken,
                                    progress)
                                .ConfigureAwait(false);
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
                    await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
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
                _manualInterventionGate.Reset();
                throw;
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? ex.GetType().Name
                    : ex.Message;
                progress?.Report($"CT 导入流程异常：{message}");
                _logger.Error("CT 导入流程异常", ex);
                try
                {
                    await AwaitManualInterventionThenArchiveFailedAsync(
                            settings,
                            null,
                            string.Empty,
                            "(批次异常)",
                            message,
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception interveneEx)
                {
                    _logger.Error("批次异常后的人工干预等待失败（已吞掉，防止闪退）", interveneEx);
                }
            }
            finally
            {
                _manualInterventionGate.Reset();
                progress?.Report(ManualInterveneEndProgressPrefix);

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
        /// 单文件失败：暂停自动化 → 醒目提示人工处理 → 等「放入异常文件夹并跳过」或 10 分钟超时 →
        /// 将排队条目移入「运行失败的文件」→ 由调用方 Kill VG 并继续下一文件。
        /// </summary>
        private async Task AwaitManualInterventionThenArchiveFailedAsync(
            CTAutoSlicerSettings settings,
            Process? ctViewerProcess,
            string queueEntryPath,
            string fileName,
            string reason,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            try
            {
                // 解除键鼠封锁，便于人工操作；不要置前 VG，应把自动化软件弹到最前
                Unlock();
                _pauseGate.Pause();

                // 先开门闩，再通知 UI，避免操作员快速点击时 Signal 落空
                _manualInterventionGate.Begin();

                var timeout = CTAutoSlicerSettings.DefaultManualInterventionTimeout;
                progress?.Report(
                    $"{ManualInterveneProgressPrefix}{fileName}|{reason}");
                progress?.Report(
                    $"已暂停等待人工处理：{fileName}（限时 {timeout.TotalMinutes:0} 分钟，超时将关闭 VG 并继续下一文件）");
                _logger.Warn(
                    $"等待人工干预：文件={fileName}，超时={timeout.TotalMinutes}分钟，原因={reason}");

                // 给 UI 线程一点时间处理 Progress（置前本软件 + 显示面板）
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);

                var waitResult = await _manualInterventionGate
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false);

                if (waitResult == ManualInterventionWaitResult.TimedOut)
                {
                    progress?.Report(
                        $"人工处理超时（{timeout.TotalMinutes:0} 分钟）：将关闭 VG，归档失败文件并继续下一文件 — {fileName}");
                    _logger.Warn($"人工干预超时：{fileName}");
                }
                else
                {
                    progress?.Report($"已确认放入异常文件夹并跳过，归档失败文件并继续 — {fileName}");
                    _logger.Info($"人工干预已确认（放入异常文件夹并跳过）：{fileName}");
                }
            }
            finally
            {
                progress?.Report(ManualInterveneEndProgressPrefix);
                _pauseGate.Reset();
                try
                {
                    Lock();
                }
                catch (Exception lockEx)
                {
                    _logger.Warn($"人工干预结束后重新 Lock 失败（忽略）：{lockEx.Message}");
                }

                if (!string.IsNullOrWhiteSpace(queueEntryPath)
                    && !string.IsNullOrWhiteSpace(settings.CTFileFolder))
                {
                    MoveFailedQueueEntryToFailedFolder(settings.CTFileFolder, queueEntryPath, progress);
                }
            }
        }

        /// <summary>
        /// 将失败的排队条目（.vgl/.vgi/.lnk）移入体素目录下「运行失败的文件」，避免下次重复导入。
        /// </summary>
        private void MoveFailedQueueEntryToFailedFolder(
            string ctFileFolder,
            string queueEntryPath,
            IProgress<string>? progress)
        {
            try
            {
                if (!File.Exists(queueEntryPath))
                {
                    _logger.Warn($"失败归档跳过：排队条目不存在 {queueEntryPath}");
                    return;
                }

                var folderName = CTAutoSlicerSettings.FailedFilesSubFolderName;
                var failedDir = Path.Combine(ctFileFolder, folderName);
                Directory.CreateDirectory(failedDir);

                var fileName = Path.GetFileName(queueEntryPath);
                var destPath = Path.Combine(failedDir, fileName);
                if (File.Exists(destPath))
                {
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    destPath = Path.Combine(
                        failedDir,
                        $"{name}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");
                }

                File.Move(queueEntryPath, destPath);
                progress?.Report($"已移入「{folderName}」：{Path.GetFileName(destPath)}");
                _logger.Info($"失败文件已归档：{queueEntryPath} → {destPath}");
            }
            catch (Exception ex)
            {
                progress?.Report($"移入「{CTAutoSlicerSettings.FailedFilesSubFolderName}」失败：{ex.Message}");
                _logger.Error($"失败文件归档异常：{queueEntryPath}", ex);
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
        /// 导入完成后的切图主流程（需焦点，全局串行）：
        /// LUT 预设 → 直方图 → 厚板 → 简单 3-2-1 → 对齐后捕捉拖拽 → 三视图切片导出 → 上视图交接。
        /// 阶段七 AI 标注已拆至独立「AI 标注模式」，本流程不再调用 NJCT。
        /// 独占期间暂停其他槽位的导入 UIA 轮询，禁止切换进程焦点。
        /// </summary>
        /// <param name="namingSourcePath">
        /// 导出命名来源（快捷方式请传 .lnk 排队条目路径，取前 6 字符作主文件夹）。
        /// </param>
        private async Task RunPostImportPipelineAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            string vglPath,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string? namingSourcePath = null)
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
                        progress,
                        softFailOnError: true,
                        namingSourcePath: namingSourcePath ?? vglPath)
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
                        progress,
                        namingSourcePath: namingSourcePath ?? vglPath)
                    .ConfigureAwait(false);

                await TryRunPostAlignCaptureDragInMainFlowAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                var (exportBaseName, quantity) = await ExportImageStackSlicesAsync(
                        settings,
                        ctViewerProcess,
                        vglPath,
                        cancellationToken,
                        progress,
                        namingSourcePath: namingSourcePath)
                    .ConfigureAwait(false);

                // 切图模式终点：复制上视图送检图到交接目录；阶段七已拆到 AI 标注模式独立执行
                await MirrorTopViewHandoffAsync(
                        settings,
                        exportBaseName,
                        quantity,
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
            IProgress<string>? progress,
            string? namingSourcePath = null)
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

            var productTag = SliceExportNamingHelper.BuildCaptureProductTag(namingSourcePath);
            var algoLabel = settings.UseVisionDllForSimple321
                ? "视觉 DLL 算法（CTAutoCutImg.dll）"
                : "上位机本地算法（OpenCV：Vision3d / VisionTopView）";
            progress?.Report(
                $"开始阶段四视觉对齐（简单 3-2-1）；【找点算法】当前调用：{algoLabel}；产品={productTag}");
            _logger.Info(
                $"主流程执行阶段四：简单 3-2-1 对齐；【找点算法】当前调用={algoLabel}" +
                $"（Simple321PointSource={settings.Simple321PointSource}；产品标签={productTag}）");

            try
            {
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                await RunSimple321AlignmentAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress,
                        namingSourcePath)
                    .ConfigureAwait(false);

                progress?.Report("阶段四视觉对齐完成，继续后续步骤");
                _logger.Info("阶段四视觉对齐完成");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 尽量关掉对话框后上抛，由进程池进入人工干预 → 归档 → 下一文件
                var reason = GetFullExceptionMessage(ex);
                progress?.Report($"阶段四视觉对齐失败：{reason}");
                _logger.Error("阶段四视觉对齐失败", ex);
                try
                {
                    await TryCloseSimple321DialogBestEffortAsync(
                            ctViewerProcess,
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false);
                }
                catch (Exception closeEx)
                {
                    _logger.Warn($"关闭 3-2-1 对话框失败（忽略）：{closeEx.Message}");
                }

                throw;
            }
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

            try
            {
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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 上抛给进程池：人工干预 → 归档失败文件
                var reason = GetFullExceptionMessage(ex);
                progress?.Report($"对齐后捕捉拖拽失败：{reason}");
                _logger.Error("对齐后捕捉拖拽失败", ex);
                throw;
            }
        }

        /// <summary>
        /// 3-2-1 中途失败时尽力关闭对话框（Esc / 取消按钮），避免挡住后续导出。
        /// </summary>
        private async Task TryCloseSimple321DialogBestEffortAsync(
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            try
            {
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                var dialog = FindWindowByNameScoped(
                    mainWindow,
                    Simple321DialogNamePrefix,
                    nameStartsWith: true);
                if (dialog == null)
                {
                    return;
                }

                progress?.Report("阶段四失败后尝试关闭「简单 3-2-1 对齐」对话框…");
                var cancelButton =
                    UIAutomationHelper.FindButtonByName(dialog, "取消")
                    ?? UIAutomationHelper.FindDescendantByName(
                        dialog,
                        ControlType.Button,
                        "取消",
                        nameContains: true);

                Unlock();
                try
                {
                    if (cancelButton != null)
                    {
                        if (!UIAutomationHelper.TryInvokeButton(cancelButton))
                        {
                            await NativeClickElementWhileUnlockedAsync(cancelButton, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await InvokeOnUiThreadAsync(SimulateEscape).ConfigureAwait(false);
                    }
                }
                finally
                {
                    Lock();
                }

                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                if (FindWindowByNameScoped(mainWindow, Simple321DialogNamePrefix, nameStartsWith: true) != null)
                {
                    Unlock();
                    try
                    {
                        await InvokeOnUiThreadAsync(SimulateEscape).ConfigureAwait(false);
                    }
                    finally
                    {
                        Lock();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn($"关闭「简单 3-2-1 对齐」对话框失败（忽略）：{ex.Message}");
            }
        }

        /// <summary>
        /// 第一步：等待 VG 打开。
        /// 判定已打开：轮询到「文件(F)」菜单，或队列中的中断弹窗（如「自动保存恢复」）。
        /// 延迟弹出的中断窗不在此死等，由后续步骤异常时 Drain 队列处理。
        /// </summary>
    }
}
