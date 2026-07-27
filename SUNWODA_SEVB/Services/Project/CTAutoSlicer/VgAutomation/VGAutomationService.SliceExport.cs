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
        // 阶段五：切片导出 + TestSliceExport
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

            var widthEdit = imageSizeEdits[0];
            var quantityEdit = UIAutomationHelper.FindEditAdjacentToLabel(
                exportDialog,
                SliceExportQuantityLabelKeyword,
                SliceExportStackSettingsGroupName);
            if (quantityEdit == null)
            {
                throw new InvalidOperationException(
                    $"未找到「{SliceExportQuantityLabelKeyword}」旁的输入框（Group「{SliceExportStackSettingsGroupName}」内 Text「{SliceExportQuantityLabelKeyword}：」+ Custom 输入框）");
            }

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

            var widthValue = settings.SliceExportImageWidth.ToString();
            var quantityValue = quantity.ToString();
            var startValue = rangeStart.ToString("0.##");
            var endValue = rangeEnd.ToString("0.##");

            // 顺序：宽度 → 开始 → 结束 → 切片图数量（数量必须最后填：改其它参数时 VG 会自动改写数量）
            // 禁止 Enter/Tab：输入后点击 UIA 定位的下一目标框失焦提交，避免默认「保存...」与 Tab 乱跳焦点
            if (!await SetSliceExportFieldValueAsync(
                    widthEdit,
                    widthValue,
                    cancellationToken,
                    commitByClicking: startEdit).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置图像宽度为 {widthValue}");
            }

            progress?.Report($"已设置图像宽度 {widthValue}");
            _logger.Info($"已设置图像宽度 {widthValue}");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);

            if (!await SetSliceExportFieldValueAsync(
                    startEdit,
                    startValue,
                    cancellationToken,
                    commitByClicking: endEdit).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"未能设置「{viewSuffix}」视图切片范围开始为 {startValue} mm");
            }

            if (!await SetSliceExportFieldValueAsync(
                    endEdit,
                    endValue,
                    cancellationToken,
                    commitByClicking: quantityEdit).ConfigureAwait(false))
            {
                throw new InvalidOperationException(
                    $"未能设置「{viewSuffix}」视图切片范围结束为 {endValue} mm");
            }

            progress?.Report($"已设置「{viewSuffix}」视图切片范围 {startValue} ~ {endValue} mm");
            _logger.Info($"已设置「{viewSuffix}」视图切片范围 {startValue} ~ {endValue} mm");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);

            // 数量放最后：失焦提交点回宽度框（已填完，不会再改写数量）
            if (!await SetSliceExportFieldValueAsync(
                    quantityEdit,
                    quantityValue,
                    cancellationToken,
                    commitByClicking: widthEdit).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"未能设置导出数量为 {quantityValue}");
            }

            progress?.Report($"已设置导出数量 {quantityValue}（最后写入，避免被其它参数覆盖）");
            _logger.Info($"已设置导出数量 {quantityValue}（最后写入）");
            await DelaySliceExportStepAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 导出对话框专用填值：禁止 Enter/Tab（避免默认「保存...」或焦点乱跳）。
        /// 输入后通过点击 UIA 定位的下一控件失焦提交。
        /// </summary>
        private async Task<bool> SetSliceExportFieldValueAsync(
            AutomationElement edit,
            string value,
            CancellationToken cancellationToken,
            AutomationElement? commitByClicking = null)
        {
            var isCustom = edit.Current.ControlType == ControlType.Custom;

            if (!isCustom
                && UIAutomationHelper.TrySetEditValueSafe(edit, value)
                && InputValueMatches(UIAutomationHelper.TryGetInputValue(edit), value))
            {
                await CommitEditByClickingAsync(commitByClicking, cancellationToken).ConfigureAwait(false);
                return true;
            }

            await SetEditValueWithKeyboardFallbackAsync(
                    edit,
                    value,
                    cancellationToken,
                    commitWithEnter: false)
                .ConfigureAwait(false);
            await CommitEditByClickingAsync(commitByClicking, cancellationToken).ConfigureAwait(false);

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

            // Custom 框常无法 UIA 读回；键盘+点击下一框后视为已提交
            return isCustom;
        }

        /// <summary>点击 UIA 下一目标框，使当前编辑失焦提交（替代 Tab）。</summary>
        private async Task CommitEditByClickingAsync(
            AutomationElement? nextEdit,
            CancellationToken cancellationToken)
        {
            if (nextEdit == null)
            {
                await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
                return;
            }

            await NativeClickElementAsync(nextEdit, cancellationToken).ConfigureAwait(false);
            await Task.Delay(SliceExportFieldCommitDelayMs, cancellationToken).ConfigureAwait(false);
        }

        private static Task DelaySliceExportStepAsync(CTAutoSlicerSettings settings, CancellationToken cancellationToken)
        {
            var delayMs = settings.ThickPlateStepDelayMs;
            return delayMs > 0
                ? Task.Delay(delayMs, cancellationToken)
                : Task.CompletedTask;
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

                        // JPEG 质量框可能在点「保存(S)」后立刻弹出并挡住关闭；
                        // 等待保存框消失时并行处理，避免卡死在 WaitUntilDisappeared。
                        return await WaitSaveDialogClosedWhileDismissingJpegAsync(
                                settings,
                                exportDialog,
                                processId,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false);
                    },
                    success => success,
                    settings,
                    cancellationToken,
                    progress,
                    maxRetriesOverride: settings.StepRetryCount)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 等待「保存图像…」文件名框消失；等待期间若出现「JPEG 质量」则立即点确定。
        /// </summary>
        private async Task<bool> WaitSaveDialogClosedWhileDismissingJpegAsync(
            CTAutoSlicerSettings settings,
            AutomationElement exportDialog,
            int? processId,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var timeoutMs = Math.Max(1000, settings.SliceExportDialogWaitTime);
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            const int pollMs = 200;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AutomationElement? qualityDialog = null;
                try
                {
                    qualityDialog = FindImageQualityDialogScoped(processId, exportDialog);
                }
                catch (ElementNotAvailableException)
                {
                }

                if (qualityDialog != null)
                {
                    progress?.Report("保存确认过程中检测到「JPEG 质量」，点击确定");
                    _logger.Info("保存确认过程中检测到「JPEG 质量」，点击确定");
                    await ConfirmJpegQualityDialogAsync(
                            settings,
                            qualityDialog,
                            () => FindImageQualityDialogScoped(processId, exportDialog),
                            cancellationToken,
                            progress)
                        .ConfigureAwait(false);
                }

                AutomationElement? fileEdit = null;
                try
                {
                    fileEdit = FindVisibleFileNameEditScoped(processId, exportDialog);
                }
                catch (ElementNotAvailableException)
                {
                }

                if (fileEdit == null)
                {
                    return true;
                }

                await Task.Delay(pollMs, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                return FindVisibleFileNameEditScoped(processId, exportDialog) == null;
            }
            catch (ElementNotAvailableException)
            {
                return true;
            }
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

            // 主路径已在「保存(S)」后的等待环中处理 JPEG；此处仅短等晚弹出的兜底，避免无弹窗时白等整段超时。
            var lateAppearWaitMs = Math.Min(1500, Math.Max(500, settings.SliceExportDialogWaitTime));
            var qualityDialog = await UIAutomationHelper.WaitForElementAsync(
                    FindQualityDialog,
                    TimeSpan.FromMilliseconds(lateAppearWaitMs),
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

    }
}
