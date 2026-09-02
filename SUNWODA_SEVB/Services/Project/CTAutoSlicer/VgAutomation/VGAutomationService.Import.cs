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
        // 阶段一：新建工程 / 快速导入 / 等待导入完成
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

    }
}
