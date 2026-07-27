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
                throw new InvalidOperationException("µÄºΣ╗╢ BoundingRectangle µùáµòê∩╝îµùáµ│òτé╣σç╗");
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
                throw new InvalidOperationException("µÄºΣ╗╢ BoundingRectangle µùáµòê∩╝îµùáµ│òσÅîσç╗");
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
                // σ┐╜τòÑ∩╝îσÉÄτ╗¡Σ╗àσ£¿σ╖▓Σ╝áσàÑµá╣σåàµƒÑµë╛
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
                throw new InvalidOperationException("µÄºΣ╗╢ BoundingRectangle µùáµòê∩╝îµùáµ│òτé╣σç╗");
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
                    progress?.Report($"{stepName} µ£¬σ«îµêÉ∩╝î{settings.StepRetryIntervalMs}ms σÉÄΦ┐¢Φíîτ¼¼ {attempt}/{maxRetries} µ¼íΘçìΦ»ò...");
                    _logger.Info($"{stepName} τ¼¼ {attempt}/{maxRetries} µ¼íΘçìΦ»ò");
                    await Task.Delay(settings.StepRetryIntervalMs, cancellationToken).ConfigureAwait(false);
                }

                // µ¡ÑΘ¬ñσ╝Çσºïσëìσàêµ╕àΣ╕Çµ¼íΘÿƒσêù∩╝îΘü┐σàìσ╖▓τƒÑΣ╕¡µû¡τ¬ùµîíΣ╜Åµ£¼µ¡Ñ
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

                    _logger.Warn($"{stepName} τ¼¼ {attempt} µ¼íµ£¬µ╗íΦ╢│σ«îµêÉµ¥íΣ╗╢");

                    // µ£¬Φ╛╛µáçµù╢∩╝ÜΦïÑµÿ»Θÿƒσêùσ╝╣τ¬ùσ»╝Φç┤∩╝îσñäτÉåσÉÄΦ┐¢σàÑΣ╕ïΣ╕ÇΦ╜«ΘçìΦ»ò
                    if (_activeVgProcess != null
                        && await TryDrainInterruptDialogQueueAsync(
                                _activeVgProcess,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false))
                    {
                        progress?.Report($"{stepName} µ£¬σ«îµêÉ∩╝îσ╖▓σñäτÉåΣ╕¡µû¡σ╝╣τ¬ù∩╝îσ░åΘçìΦ»òΦ»Ñµ¡ÑΘ¬ñ");
                        _logger.Info($"{stepName} µ£¬σ«îµêÉΣ╕öσæ╜Σ╕¡Σ╕¡µû¡σ╝╣τ¬ùΘÿƒσêù∩╝îσçåσñçΘçìΦ»ò");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger.Warn($"{stepName} τ¼¼ {attempt} µ¼íσ╝éσ╕╕∩╝Ü{ex.Message}");

                    // σ╝éσ╕╕σÉÄ∩╝ÜΣ╝ÿσàêµúÇµƒÑµÿ»σÉªΣ╕║ΘÿƒσêùΣ╕¡τÜäΣ╕¡µû¡σ╝╣τ¬ù∩╝îσñäτÉåσÉÄσåìΘçìΦ»òµ£¼µ¡ÑΘ¬ñ
                    if (_activeVgProcess != null
                        && await TryDrainInterruptDialogQueueAsync(
                                _activeVgProcess,
                                cancellationToken,
                                progress)
                            .ConfigureAwait(false))
                    {
                        progress?.Report($"{stepName} σ╝éσ╕╕σÉÄσ╖▓σñäτÉåΣ╕¡µû¡σ╝╣τ¬ù∩╝îσ░åΘçìΦ»òΦ»Ñµ¡ÑΘ¬ñ");
                        _logger.Info($"{stepName} σ╝éσ╕╕σÉÄσæ╜Σ╕¡Σ╕¡µû¡σ╝╣τ¬ùΘÿƒσêù∩╝îσçåσñçΘçìΦ»òπÇéσÄƒσ╝éσ╕╕∩╝Ü{ex.Message}");
                        lastException = null;
                    }
                }
            }

            var message = $"{stepName} σ£¿ {maxRetries} µ¼íσ░¥Φ»òσÉÄΣ╗ìµ£¬σ«îµêÉ∩╝îΦç¬σè¿µ╡üτ¿ïσ╝éσ╕╕";
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
                    // 1) Σ╕╗τ¬ùσÅúτ¢┤µÄÑσ¡Éτ¬ùσÅú∩╝êµáçσçåπÇîµëôσ╝ÇµûçΣ╗╢πÇìσ»╣Φ»¥µíå∩╝îµ»½τºÆτ║º∩╝ë
                    var amongChildren = FindNamedWindowAmong(
                        mainWindow,
                        TreeScope.Children,
                        QuickImportDialogNamePrefix);
                    if (amongChildren != null)
                    {
                        return amongChildren;
                    }

                    // 2) Σ╕╗τ¬ùσÅúσ¡Éµáæ∩╝êΣ╗ìΣ╕ìµë½µíîΘ¥ó∩╝ë
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

                    // 3) σÉîΦ┐¢τ¿ïΘí╢σ▒éτ¬ùσÅú∩╝êTreeScope.Children∩╝îΘü┐σàì Root Descendants σà¿µíîΘ¥óµë½µÅÅ∩╝ë
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

                // µùáΣ╕╗τ¬ùσÅúµù╢∩╝ÜΣ╗àµë½µíîΘ¥óΘí╢σ▒éτ¬ùσÅú∩╝êChildren∩╝ë∩╝îτªüµ¡ó Root.FindAll(Descendants)
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
                // µ£Çτ¿│∩╝Üσåàµûç TextπÇîµ¡úσ£¿σ»╝σàÑΣ╜ôτº»πÇì∩╝êΣ╕Ä Accessibility Insights Σ╕ÇΦç┤∩╝ë
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

        /// <summary>µÿ»σÉªσ╖▓σç║τÄ░Σ╜ôτº»σ»╝σàÑΦ┐¢σ║ª∩╝êΣ║ñµëï/σÉÄσÅ░τ¡ëσ╛àσà▒τö¿∩╝ëπÇé</summary>
        private static bool IsVolumeImportProgressVisible(AutomationElement mainWindow) =>
            FindVisibleImportProgressWindow(mainWindow) != null;

        /// <summary>
        /// µƒÑµë╛σÅ»ΦºüτÜäπÇîµ¡úσ£¿σ»╝σàÑΣ╜ôτº»πÇìµûçµ£¼∩╝êInsights∩╝ÜControlType.Text∩╝ëπÇé
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
            // Σ╝ÿσàêτ▓╛τí« Name µ¥íΣ╗╢∩╝êInsights∩╝ÜName=µ¡úσ£¿σ»╝σàÑΣ╜ôτº»∩╝ë∩╝îΘü┐σàìσà¿µáæµë½ Text
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

                    // Φ┐¢σ║ªτ¬ùσ»╣ IsOffscreen/IsEnabled σü╢σÅæΣ╕ìσÅ»Θ¥á∩╝¢µ£ëσ░║σ»╕µêûσÉ½σ»╝σàÑµûçµíêσì│Φ«ñ
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

        /// <summary>Φ┐¢σ║ªτ▒╗ UI σÅ»Φºüσêñσ«Ü∩╝ÜΣ╕ìσ╝║σê╢ IsEnabled∩╝êΘâ¿σêå Qt Φ┐¢σ║ªτ¬ù Enabled Σ╕ìτ¿│σ«Ü∩╝ëπÇé</summary>
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

        /// <summary>µêÉσèƒσÉÄσêáΘÖñΣ╜ôτ┤áµûçΣ╗╢σñ╣σåàτÜäµÄÆΘÿƒµ¥íτ¢«∩╝êτ£ƒσ«₧ .vgl/.vgi µêûσ┐½µì╖µû╣σ╝Å .lnk∩╝ëπÇé</summary>
        private Task OnImportSucceededAsync(string queueEntryPath)
        {
            try
            {
                if (File.Exists(queueEntryPath))
                {
                    File.Delete(queueEntryPath);
                    _logger.Info($"σñäτÉåµêÉσèƒ∩╝îσ╖▓σêáΘÖñµÄÆΘÿƒµ¥íτ¢«∩╝Ü{queueEntryPath}");
                }
                else
                {
                    _logger.Warn($"σñäτÉåµêÉσèƒ∩╝îΣ╜åµÄÆΘÿƒµ¥íτ¢«σ╖▓Σ╕ìσ¡ÿσ£¿∩╝Ü{queueEntryPath}");
                }
            }
            catch (Exception ex)
            {
                // µ╡üτ¿ïσ╖▓µêÉσèƒ∩╝¢σêáΘÖñσñ▒Φ┤ÑσÅ¬Φ«░Φ¡ªσæè∩╝îΘü┐σàìΦ»»σêñµò┤σìòσñ▒Φ┤Ñ
                _logger.Warn($"σñäτÉåµêÉσèƒΣ╜åσêáΘÖñµÄÆΘÿƒµ¥íτ¢«σñ▒Φ┤Ñ∩╝êΦ»╖µëïσè¿µ╕àτÉå∩╝ë∩╝Ü{queueEntryPath} ΓÇö {ex.Message}");
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
                throw new InvalidOperationException("VG Σ╕╗τ¬ùσÅúσÅÑµƒäµùáµòê∩╝îµùáµ│òτ╜«σëì");
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
        /// σ┐½ΘÇƒσ»╝σàÑσ┐½µì╖Θö« Ctrl+F+I∩╝ÜµîëΣ╜Å Ctrl∩╝îΣ╛¥µ¼íµîë FπÇüI∩╝êΣ╕Äτ£ƒµ£║µáçσ«ÜΣ╕ÇΦç┤∩╝ëπÇé
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

        /// <summary>ΦÅ£σìòτ║ºσ┐½µì╖Θö« Alt+F+T∩╝ÜµîëΣ╜Å Alt∩╝îΣ╛¥µ¼íµîë FπÇüT∩╝êΘ¥₧Σ╕ñµ¼íτï¼τ½ïτÜä Alt τ╗äσÉê∩╝ëπÇé</summary>
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
