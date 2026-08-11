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
        // UIA 点击/重试/查找 / Edit 赋值 / 窗口激活 / 键盘模拟
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
                await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);

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

        /// <summary>转发至 <see cref="VgAutomation.VgElementFinder"/>（通用查找已外置，仅本模块使用）。</summary>
        private static AutomationElement? FindNamedWindowAmong(
            AutomationElement root,
            TreeScope scope,
            string namePrefix) =>
            VgAutomation.VgElementFinder.FindNamedWindowAmong(root, scope, namePrefix);

        private static AutomationElement? FindNamedWindowOnDesktopByProcess(int processId, string namePrefix) =>
            VgAutomation.VgElementFinder.FindNamedWindowOnDesktopByProcess(processId, namePrefix);

        private static IEnumerable<AutomationElement> EnumerateSameProcessTopLevelWindows(int processId) =>
            VgAutomation.VgElementFinder.EnumerateSameProcessTopLevelWindows(processId);

        private static IEnumerable<AutomationElement> EnumerateDesktopTopLevelWindows() =>
            VgAutomation.VgElementFinder.EnumerateDesktopTopLevelWindows();

        private static AutomationElement? FindWindowByNameScoped(
            AutomationElement mainWindow,
            string windowName,
            bool nameStartsWith = false) =>
            VgAutomation.VgElementFinder.FindWindowByNameScoped(mainWindow, windowName, nameStartsWith);

        private static AutomationElement? FindVisibleListItemScoped(
            string itemName,
            int? processId,
            params AutomationElement?[] preferRoots) =>
            VgAutomation.VgElementFinder.FindVisibleListItemScoped(itemName, processId, preferRoots);

        private static AutomationElement? FindVisibleFileNameEditScoped(
            int? processId,
            params AutomationElement?[] preferRoots) =>
            VgAutomation.VgElementFinder.FindVisibleFileNameEditScoped(processId, preferRoots);

        private static AutomationElement? FindVisibleWindowByNamePrefixScoped(
            AutomationElement mainWindow,
            string namePrefix,
            bool requireImageKeyword = false) =>
            VgAutomation.VgElementFinder.FindVisibleWindowByNamePrefixScoped(mainWindow, namePrefix, requireImageKeyword);

        private static AutomationElement? FindDescendantWindowByNameKeywordScoped(
            string nameKeyword,
            string? requireButtonName,
            int? processId,
            params AutomationElement?[] preferRoots) =>
            VgAutomation.VgElementFinder.FindDescendantWindowByNameKeywordScoped(
                nameKeyword, requireButtonName, processId, preferRoots);

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

    }
}
