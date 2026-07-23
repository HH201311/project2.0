using System.Diagnostics;
using System.Windows.Automation;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class UIAutomationHelper
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

        public static AutomationElement FindMainWindow(IntPtr mainWindowHandle)
        {
            return AutomationElement.FromHandle(mainWindowHandle);
        }

        public static AutomationElement FindMainWindow(Condition condition)
        {
            return AutomationElement.RootElement.FindFirst(TreeScope.Children, condition);
        }

        public static AutomationElement? FindChildernElement(AutomationElement parentElement, Condition condition)
        {
            return parentElement.FindFirst(TreeScope.Descendants, condition);
        }

        public static AutomationElement? FindDescendantByName(
            AutomationElement root,
            ControlType controlType,
            string name,
            bool nameStartsWith = false,
            bool nameContains = false)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                string elementName;
                try
                {
                    elementName = element.Current.Name ?? string.Empty;
                }
                catch (ElementNotAvailableException)
                {
                    continue;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (nameStartsWith && elementName.StartsWith(name, StringComparison.Ordinal))
                {
                    return element;
                }

                if (nameContains && elementName.Contains(name, StringComparison.Ordinal))
                {
                    return element;
                }

                if (!nameStartsWith && !nameContains && elementName == name)
                {
                    return element;
                }
            }

            return null;
        }

        public static AutomationElement? FindWindowByName(
            AutomationElement root,
            string windowName,
            bool nameStartsWith = false)
        {
            var window = FindDescendantByName(root, ControlType.Window, windowName, nameStartsWith);
            if (window != null)
            {
                return window;
            }

            return FindDescendantByName(root, ControlType.Pane, windowName, nameStartsWith);
        }

        public static AutomationElement? FindEditByName(AutomationElement root, string editName)
        {
            return FindDescendantByName(root, ControlType.Edit, editName)
                ?? FindEditByNameFlexible(root, editName);
        }

        /// <summary>
        /// 按 Name 查找 Edit，忽略末尾冒号并支持包含匹配（如「文件名:」「文件名(N):」）。
        /// </summary>
        public static AutomationElement? FindEditByNameFlexible(AutomationElement root, string editNameKeyword)
        {
            var normalizedKeyword = NormalizeLabelText(editNameKeyword);
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                if (!element.Current.IsEnabled)
                {
                    continue;
                }

                var normalizedName = NormalizeLabelText(element.Current.Name ?? string.Empty);
                if (normalizedName.Equals(normalizedKeyword, StringComparison.Ordinal)
                    || normalizedName.Contains(normalizedKeyword, StringComparison.Ordinal)
                    || normalizedKeyword.Contains(normalizedName, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            return null;
        }

        /// <summary>
        /// 在桌面 UIA 树中查找可见的「文件名」输入框（嵌套保存对话框内）。
        /// Windows 通用「保存图像...」里「文件名」常为 ComboBox，内层 Edit 的 Name 可能为空。
        /// </summary>
        public static AutomationElement? FindVisibleFileNameEdit(AutomationElement searchRoot)
        {
            // 1) Edit：Name 含「文件名」
            var byEditName = FindVisibleNamedFileNameControl(searchRoot, ControlType.Edit);
            if (byEditName != null)
            {
                return byEditName;
            }

            // 2) ComboBox：Name 含「文件名」→ 优先返回内层可写 Edit，否则返回 ComboBox 本身
            var fileNameCombo = FindVisibleNamedFileNameControl(searchRoot, ControlType.ComboBox);
            if (fileNameCombo != null)
            {
                var nestedEdit = FindFirstDescendantEdit(fileNameCombo);
                return nestedEdit ?? fileNameCombo;
            }

            // 3) 先定位「保存图像」窗，再在其子树找「文件名」Edit/Combo（应对 FindAll 跨嵌套窗不稳定）
            var saveImageDialog = FindVisibleSaveImageDialog(searchRoot);
            if (saveImageDialog != null && !ReferenceEquals(saveImageDialog, searchRoot))
            {
                return FindVisibleFileNameEdit(saveImageDialog);
            }

            // 4) 「保存图像」窗内：任意可见可写 Edit（通用对话框通常只有文件名一处主输入）
            if (saveImageDialog != null)
            {
                var anyEdit = FindFirstDescendantEdit(saveImageDialog);
                if (anyEdit != null)
                {
                    return anyEdit;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找标题含「保存图像」的可见对话框（VG：保存影片/图像序列 → 保存图像...）。
        /// 先 FindAll，失败再用 ControlViewWalker 深搜（部分嵌套 Win32 对话框 Descendants 会漏）。
        /// </summary>
        public static AutomationElement? FindVisibleSaveImageDialog(AutomationElement searchRoot)
        {
            foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                AutomationElementCollection elements;
                try
                {
                    elements = searchRoot.FindAll(TreeScope.Descendants, condition);
                }
                catch (ElementNotAvailableException)
                {
                    elements = null!;
                }

                if (elements != null)
                {
                    foreach (AutomationElement element in elements)
                    {
                        if (IsSaveImageDialogElement(element))
                        {
                            return element;
                        }
                    }
                }
            }

            if (IsSaveImageDialogElement(searchRoot))
            {
                return searchRoot;
            }

            return FindSaveImageDialogByWalker(searchRoot, maxDepth: 12);
        }

        private static bool IsSaveImageDialogElement(AutomationElement? element)
        {
            if (element == null)
            {
                return false;
            }

            try
            {
                var name = element.Current.Name ?? string.Empty;
                if (!name.Contains("保存图像", StringComparison.Ordinal))
                {
                    return false;
                }

                return IsElementInteractable(element)
                    || element.Current.BoundingRectangle is { Width: > 0, Height: > 0 };
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        private static AutomationElement? FindSaveImageDialogByWalker(
            AutomationElement root,
            int maxDepth)
        {
            var walker = TreeWalker.ControlViewWalker;
            return FindSaveImageDialogByWalkerCore(walker, root, depth: 0, maxDepth);
        }

        private static AutomationElement? FindSaveImageDialogByWalkerCore(
            TreeWalker walker,
            AutomationElement parent,
            int depth,
            int maxDepth)
        {
            if (depth > maxDepth)
            {
                return null;
            }

            AutomationElement? child;
            try
            {
                child = walker.GetFirstChild(parent);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            while (child != null)
            {
                if (IsSaveImageDialogElement(child))
                {
                    return child;
                }

                var nested = FindSaveImageDialogByWalkerCore(walker, child, depth + 1, maxDepth);
                if (nested != null)
                {
                    return nested;
                }

                try
                {
                    child = walker.GetNextSibling(child);
                }
                catch (ElementNotAvailableException)
                {
                    break;
                }
            }

            return null;
        }

        private static AutomationElement? FindVisibleNamedFileNameControl(
            AutomationElement searchRoot,
            ControlType controlType)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            AutomationElementCollection elements;
            try
            {
                elements = searchRoot.FindAll(TreeScope.Descendants, condition);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            foreach (AutomationElement element in elements)
            {
                try
                {
                    if (!element.Current.IsEnabled)
                    {
                        continue;
                    }

                    var normalizedName = NormalizeLabelText(element.Current.Name ?? string.Empty);
                    if (!normalizedName.Contains("文件名", StringComparison.Ordinal)
                        && !normalizedName.Contains("File name", StringComparison.OrdinalIgnoreCase)
                        && !normalizedName.Contains("FileName", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    return element;
                }
                catch (ElementNotAvailableException)
                {
                    // 继续
                }
            }

            return null;
        }

        public static AutomationElement? FindAncestorDialogOrWindow(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            AutomationElement? fallback = null;
            var current = element;
            while (current != null && current != AutomationElement.RootElement)
            {
                var controlType = current.Current.ControlType;
                var name = current.Current.Name ?? string.Empty;
                if (name.Contains("保存图像", StringComparison.Ordinal)
                    || name.Contains("另存为", StringComparison.Ordinal))
                {
                    return current;
                }

                if (controlType == ControlType.Window || controlType == ControlType.Pane)
                {
                    fallback = current;
                }

                current = walker.GetParent(current);
            }

            return fallback;
        }

        public static string? TryGetInputValue(AutomationElement element)
        {
            try
            {
                if (TryGetValuePatternValue(element, out var value))
                {
                    return value;
                }

                var nestedEdit = FindFirstDescendantEdit(element);
                if (nestedEdit != null
                    && !ReferenceEquals(nestedEdit, element)
                    && TryGetValuePatternValue(nestedEdit, out value))
                {
                    return value;
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        private static bool TryGetValuePatternValue(AutomationElement element, out string value)
        {
            value = string.Empty;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern valuePattern)
            {
                value = valuePattern.Current.Value ?? string.Empty;
                return true;
            }

            return false;
        }

        public static AutomationElement? FindButtonByName(AutomationElement root, string buttonName, bool exactMatch = true)
        {
            return exactMatch
                ? FindDescendantByName(root, ControlType.Button, buttonName)
                : FindDescendantByName(root, ControlType.Button, buttonName, nameContains: true);
        }

        public static AutomationElement? FindFirstEnabledEdit(AutomationElement root, string? excludeName = null)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                if (!element.Current.IsEnabled)
                {
                    continue;
                }

                var name = element.Current.Name ?? string.Empty;
                if (!string.IsNullOrEmpty(excludeName) && name == excludeName)
                {
                    continue;
                }

                return element;
            }

            return null;
        }

        public static bool TrySetEditValue(AutomationElement edit, string value)
        {
            if (TrySetValuePattern(edit, value))
            {
                return true;
            }

            var nestedEdit = FindFirstDescendantEdit(edit);
            if (nestedEdit != null && !ReferenceEquals(nestedEdit, edit))
            {
                return TrySetValuePattern(nestedEdit, value);
            }

            return false;
        }

        private static bool TrySetValuePattern(AutomationElement element, string value)
        {
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern valuePattern)
            {
                if (!valuePattern.Current.IsReadOnly)
                {
                    valuePattern.SetValue(value);
                    return true;
                }
            }

            return false;
        }

        private static AutomationElement? FindFirstDescendantEdit(AutomationElement root)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                if (IsInputCandidate(element))
                {
                    return element;
                }
            }

            return null;
        }

        public static bool TryInvokeElement(AutomationElement element)
        {
            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern) && pattern is InvokePattern invokePattern)
            {
                invokePattern.Invoke();
                return true;
            }

            return false;
        }

        public static bool TryInvokeButton(AutomationElement button)
        {
            return TryInvokeElement(button);
        }

        public static bool TryExpandComboBox(AutomationElement comboBox)
        {
            if (comboBox.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)
                && pattern is ExpandCollapsePattern expandCollapsePattern)
            {
                if (expandCollapsePattern.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
                {
                    expandCollapsePattern.Expand();
                }

                return true;
            }

            return false;
        }

        public static bool IsComboBoxExpanded(AutomationElement comboBox)
        {
            if (comboBox.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)
                && pattern is ExpandCollapsePattern expandCollapsePattern)
            {
                return expandCollapsePattern.Current.ExpandCollapseState == ExpandCollapseState.Expanded;
            }

            return false;
        }

        public static bool IsElementInteractable(AutomationElement? element)
        {
            if (element == null)
            {
                return false;
            }

            var rect = element.Current.BoundingRectangle;
            return element.Current.IsEnabled
                && !element.Current.IsOffscreen
                && rect.Width > 0
                && rect.Height > 0;
        }

        public static bool TrySelectListItem(AutomationElement listItem)
        {
            if (listItem.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern)
                && selectionPattern is SelectionItemPattern selectionItemPattern)
            {
                selectionItemPattern.Select();
                return true;
            }

            return TryInvokeElement(listItem);
        }

        public static bool ContainsDescendantText(AutomationElement root, string textKeyword)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                var name = element.Current.Name ?? string.Empty;
                if (name.Contains(textKeyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static AutomationElement? FindVisibleDescendantByName(
            AutomationElement root,
            ControlType controlType,
            string name,
            bool exactMatch = true)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                var elementName = element.Current.Name ?? string.Empty;
                var nameMatched = exactMatch
                    ? elementName.Equals(name, StringComparison.Ordinal)
                    : elementName.Contains(name, StringComparison.Ordinal);
                if (!nameMatched)
                {
                    continue;
                }

                if (IsElementInteractable(element))
                {
                    return element;
                }
            }

            return null;
        }

        public static bool TrySetComboBoxValue(AutomationElement comboBox, string value)
        {
            if (comboBox.TryGetCurrentPattern(ValuePattern.Pattern, out var comboValuePattern)
                && comboValuePattern is ValuePattern valuePattern
                && !valuePattern.Current.IsReadOnly)
            {
                valuePattern.SetValue(value);
                return true;
            }

            var edit = FindFirstDescendantByControlType(comboBox, ControlType.Edit);
            if (edit != null && TrySetEditValue(edit, value))
            {
                return true;
            }

            return false;
        }

        public static string GetComboBoxDisplayText(AutomationElement comboBox)
        {
            if (TryGetComboBoxValueText(comboBox, out var value))
            {
                return value;
            }

            var edit = FindFirstDescendantByControlType(comboBox, ControlType.Edit);
            if (edit != null)
            {
                var editName = edit.Current.Name;
                if (!string.IsNullOrWhiteSpace(editName))
                {
                    return editName.Trim();
                }
            }

            return (comboBox.Current.Name ?? string.Empty).Trim();
        }

        /// <summary>
        /// 仅读取 ComboBox 可编辑值（ValuePattern），不含 Name 兜底，用于判定预设是否已真正应用。
        /// </summary>
        public static bool TryGetComboBoxValueText(AutomationElement comboBox, out string value)
        {
            value = string.Empty;
            if (comboBox.TryGetCurrentPattern(ValuePattern.Pattern, out var comboValuePattern)
                && comboValuePattern is ValuePattern valuePattern)
            {
                var patternValue = valuePattern.Current.Value;
                if (!string.IsNullOrWhiteSpace(patternValue))
                {
                    value = patternValue.Trim();
                    return true;
                }
            }

            var edit = FindFirstDescendantByControlType(comboBox, ControlType.Edit);
            if (edit != null
                && edit.TryGetCurrentPattern(ValuePattern.Pattern, out var editValuePattern)
                && editValuePattern is ValuePattern editValue
                && !string.IsNullOrWhiteSpace(editValue.Current.Value))
            {
                value = editValue.Current.Value.Trim();
                return true;
            }

            return false;
        }

        public static bool TryScrollListItemIntoView(AutomationElement listItem)
        {
            if (listItem.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var pattern)
                && pattern is ScrollItemPattern scrollItemPattern)
            {
                scrollItemPattern.ScrollIntoView();
                return true;
            }

            return false;
        }

        public static bool IsRenderingPresetDropdownOpen(AutomationElement comboBox)
        {
            if (IsComboBoxExpanded(comboBox))
            {
                return true;
            }

            var list = FindFirstDescendantByControlType(comboBox, ControlType.List);
            return list != null && IsElementInteractable(list);
        }

        public static AutomationElement? FindFirstDescendantByControlType(
            AutomationElement root,
            ControlType controlType)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            return root.FindFirst(TreeScope.Descendants, condition);
        }

        public static bool TryInvokeButtonByName(AutomationElement root, string buttonName, bool exactMatch = true)
        {
            var button = FindButtonByName(root, buttonName, exactMatch);
            return button != null && TryInvokeButton(button);
        }

        public static bool TryExpandMenuItem(AutomationElement menuItem)
        {
            if (menuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern)
                && pattern is ExpandCollapsePattern expandCollapsePattern)
            {
                if (expandCollapsePattern.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
                {
                    expandCollapsePattern.Expand();
                }

                return true;
            }

            return false;
        }

        public static bool IsMenuItemInteractive(AutomationElement? menuItem)
        {
            if (menuItem == null)
            {
                return false;
            }

            try
            {
                return menuItem.Current.IsEnabled
                    && !menuItem.Current.IsOffscreen
                    && menuItem.Current.BoundingRectangle.Width > 0
                    && menuItem.Current.BoundingRectangle.Height > 0;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        public static AutomationElement? FindFileMenuForProcess(Process process, string menuItemName)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                try
                {
                    var mainWindow = AutomationElement.FromHandle(process.MainWindowHandle);
                    var menuItem = FindDescendantByName(mainWindow, ControlType.MenuItem, menuItemName);
                    if (IsMenuItemInteractive(menuItem))
                    {
                        return menuItem;
                    }
                }
                catch
                {
                    // 主窗口 Automation 尚未就绪
                }
            }

            var windowCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
            var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, windowCondition);
            foreach (AutomationElement window in windows)
            {
                try
                {
                    if (window.Current.ProcessId != process.Id)
                    {
                        continue;
                    }

                    var menuItem = FindDescendantByName(window, ControlType.MenuItem, menuItemName);
                    if (IsMenuItemInteractive(menuItem))
                    {
                        return menuItem;
                    }
                }
                catch (ElementNotAvailableException)
                {
                    // 窗口正在关闭或 UIA 树抖动
                }
                catch (InvalidOperationException)
                {
                    // UIA 临时不可用
                }
            }

            return null;
        }

        public static async Task<bool> WaitForProcessMainWindowAsync(
            Process process,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                process.Refresh();
                if (process.MainWindowHandle != IntPtr.Zero && !process.HasExited)
                {
                    try
                    {
                        var window = AutomationElement.FromHandle(process.MainWindowHandle);
                        if (window != null && !string.IsNullOrEmpty(window.Current.Name))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // 主窗口尚未完全就绪，继续轮询
                    }
                }

                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        public const int DefaultConditionPollIntervalMs = 100;

        /// <summary>
        /// 轮询直到条件成立或超时。配置的超时时间为最大等待上限，条件满足后立即返回。
        /// </summary>
        public static async Task<bool> WaitUntilAsync(
            Func<bool> condition,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            int pollIntervalMs = DefaultConditionPollIntervalMs)
        {
            var deadline = DateTime.UtcNow + timeout;
            var pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, pollIntervalMs));
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (condition())
                {
                    return true;
                }

                await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// 轮询直到元素消失或超时。返回 true 表示已消失，false 表示超时仍存在。
        /// </summary>
        public static async Task<bool> WaitUntilDisappearedAsync(
            Func<AutomationElement?> findElement,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            int pollIntervalMs = DefaultConditionPollIntervalMs)
        {
            var deadline = DateTime.UtcNow + timeout;
            var pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, pollIntervalMs));
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (findElement() == null)
                {
                    return true;
                }

                await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(false);
            }

            return findElement() == null;
        }

        public static async Task<AutomationElement?> WaitForElementAsync(
            Func<AutomationElement?> findElement,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            int pollIntervalMs = 1000)
        {
            var deadline = DateTime.UtcNow + timeout;
            var pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, pollIntervalMs));
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = findElement();
                if (element != null)
                {
                    return element;
                }

                await Task.Delay(pollDelay, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        public static async Task WaitForElementDisappearAsync(
            Func<AutomationElement?> findElement,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null,
            string? waitingMessage = null)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = findElement();
                if (element == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(waitingMessage))
                {
                    progress?.Report(waitingMessage);
                }

                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException($"等待元素消失超时（{timeout.TotalMinutes:0.#} 分钟）");
        }

        /// <summary>
        /// 枚举四宫格视口：按视口中心点划分为 [Q1左上, Q2右上, Q3左下, Q4右下]。
        /// Q3 位于 Q1 正下方（同列），而非 Q2 右侧。
        /// </summary>
        public static IReadOnlyList<AutomationElement> FindViewportQuadrants(
            AutomationElement mainWindow,
            int minWidth,
            int minHeight)
        {
            var mainRect = mainWindow.Current.BoundingRectangle;
            if (mainRect.Width <= 0 || mainRect.Height <= 0)
            {
                return Array.Empty<AutomationElement>();
            }

            var minArea = minWidth * (double)minHeight;
            var mainArea = mainRect.Width * mainRect.Height;
            var candidates = new List<(AutomationElement Element, System.Windows.Rect Rect, double Area)>();

            foreach (var controlType in new[] { ControlType.Group, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                var elements = mainWindow.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement element in elements)
                {
                    if (!IsElementInteractable(element))
                    {
                        continue;
                    }

                    var rect = element.Current.BoundingRectangle;
                    if (rect.Width < minWidth || rect.Height < minHeight)
                    {
                        continue;
                    }

                    var area = rect.Width * rect.Height;
                    if (area < minArea || area > mainArea * 0.55)
                    {
                        continue;
                    }

                    if (rect.Left < mainRect.Left - 1
                        || rect.Top < mainRect.Top - 1
                        || rect.Right > mainRect.Right + 1
                        || rect.Bottom > mainRect.Bottom + 1)
                    {
                        continue;
                    }

                    candidates.Add((element, rect, area));
                }
            }

            var filtered = RemoveOverlappingViewportCandidates(candidates);
            if (filtered.Count == 0)
            {
                return Array.Empty<AutomationElement>();
            }

            var viewportCandidates = filtered.Count >= 4
                ? filtered.OrderByDescending(c => c.Area).Take(4).ToList()
                : filtered.OrderByDescending(c => c.Area).ToList();

            if (viewportCandidates.Count < 4)
            {
                viewportCandidates.Sort((a, b) =>
                {
                    var topCompare = a.Rect.Top.CompareTo(b.Rect.Top);
                    return topCompare != 0 ? topCompare : a.Rect.Left.CompareTo(b.Rect.Left);
                });
                return viewportCandidates.Select(c => c.Element).ToList();
            }

            var centers = viewportCandidates
                .Select(c => (
                    c.Element,
                    c.Rect,
                    CenterX: c.Rect.Left + c.Rect.Width / 2,
                    CenterY: c.Rect.Top + c.Rect.Height / 2))
                .ToList();

            var minCenterX = centers.Min(c => c.CenterX);
            var maxCenterX = centers.Max(c => c.CenterX);
            var minCenterY = centers.Min(c => c.CenterY);
            var maxCenterY = centers.Max(c => c.CenterY);
            var midX = (minCenterX + maxCenterX) / 2;
            var midY = (minCenterY + maxCenterY) / 2;

            AutomationElement? q1 = null;
            AutomationElement? q2 = null;
            AutomationElement? q3 = null;
            AutomationElement? q4 = null;

            foreach (var (element, _, centerX, centerY) in centers)
            {
                var isTop = centerY < midY;
                var isLeft = centerX < midX;
                if (isTop && isLeft)
                {
                    q1 = element;
                }
                else if (isTop && !isLeft)
                {
                    q2 = element;
                }
                else if (!isTop && isLeft)
                {
                    q3 = element;
                }
                else
                {
                    q4 = element;
                }
            }

            var ordered = new List<AutomationElement>();
            if (q1 != null)
            {
                ordered.Add(q1);
            }

            if (q2 != null)
            {
                ordered.Add(q2);
            }

            if (q3 != null)
            {
                ordered.Add(q3);
            }

            if (q4 != null)
            {
                ordered.Add(q4);
            }

            return ordered;
        }

        private static List<(AutomationElement Element, System.Windows.Rect Rect, double Area)> RemoveOverlappingViewportCandidates(
            List<(AutomationElement Element, System.Windows.Rect Rect, double Area)> candidates)
        {
            var sorted = candidates.OrderByDescending(c => c.Area).ToList();
            var kept = new List<(AutomationElement Element, System.Windows.Rect Rect, double Area)>();

            foreach (var candidate in sorted)
            {
                var dominated = kept.Any(existing => GetRectOverlapRatio(existing.Rect, candidate.Rect) > 0.85);
                if (!dominated)
                {
                    kept.Add(candidate);
                }
            }

            return kept;
        }

        private static double GetRectOverlapRatio(System.Windows.Rect a, System.Windows.Rect b)
        {
            var overlapLeft = Math.Max(a.Left, b.Left);
            var overlapTop = Math.Max(a.Top, b.Top);
            var overlapRight = Math.Min(a.Right, b.Right);
            var overlapBottom = Math.Min(a.Bottom, b.Bottom);
            if (overlapRight <= overlapLeft || overlapBottom <= overlapTop)
            {
                return 0;
            }

            var overlapArea = (overlapRight - overlapLeft) * (overlapBottom - overlapTop);
            var smallerArea = Math.Min(a.Width * a.Height, b.Width * b.Height);
            return smallerArea <= 0 ? 0 : overlapArea / smallerArea;
        }

        public static bool TryToggleCheckBox(AutomationElement? checkBox, bool targetOn)
        {
            if (checkBox == null)
            {
                return false;
            }

            if (!checkBox.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern)
                || pattern is not TogglePattern togglePattern)
            {
                return false;
            }

            var current = togglePattern.Current.ToggleState;
            var isOn = current == ToggleState.On;
            if (isOn == targetOn)
            {
                return true;
            }

            togglePattern.Toggle();
            return true;
        }

        public static bool IsCheckBoxOn(AutomationElement? checkBox)
        {
            if (checkBox == null)
            {
                return false;
            }

            if (checkBox.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern)
                && pattern is TogglePattern togglePattern)
            {
                return togglePattern.Current.ToggleState == ToggleState.On;
            }

            return false;
        }

        public static AutomationElement? TryFindContextMenuItem(AutomationElement searchRoot, string menuItemName)
        {
            foreach (var controlType in new[] { ControlType.MenuItem, ControlType.ListItem })
            {
                var item = FindVisibleDescendantByName(searchRoot, controlType, menuItemName);
                if (item != null)
                {
                    return item;
                }
            }

            var menu = FindDescendantByName(searchRoot, ControlType.Menu, menuItemName, nameContains: true);
            if (menu != null)
            {
                return menu;
            }

            return FindVisibleDescendantByName(searchRoot, ControlType.MenuItem, menuItemName, exactMatch: false);
        }

        public static async Task<AutomationElement?> WaitForContextMenuItemAsync(
            Func<AutomationElement?> findMenuItem,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            int pollIntervalMs = 100)
        {
            return await WaitForElementAsync(findMenuItem, timeout, cancellationToken, pollIntervalMs)
                .ConfigureAwait(false);
        }

        public static AutomationElement? FindGroupByName(AutomationElement root, string groupName)
        {
            return FindDescendantByName(root, ControlType.Group, groupName)
                ?? FindDescendantByName(root, ControlType.Pane, groupName)
                ?? FindDescendantByName(root, ControlType.Custom, groupName)
                ?? FindDescendantByName(root, ControlType.Group, groupName, nameContains: true)
                ?? FindDescendantByName(root, ControlType.Pane, groupName, nameContains: true)
                ?? FindDescendantByName(root, ControlType.Custom, groupName, nameContains: true);
        }

        /// <summary>
        /// 在指定容器内，按 Text 标签名（忽略末尾全角/半角冒号）查找相邻输入框（Edit / Custom 等）。
        /// </summary>
        /// <param name="containerLabelKeyword">
        /// 可选：先按该 Text 标签定位父容器，再在其中查找目标标签，避免「开始」等短词误匹配。
        /// </param>
        public static AutomationElement? FindEditAdjacentToLabel(
            AutomationElement root,
            string labelKeyword,
            string? groupName = null,
            string? containerLabelKeyword = null)
        {
            var searchRoot = ResolveSearchRoot(root, groupName, containerLabelKeyword);

            foreach (var label in FindTextsMatchingKeyword(searchRoot, labelKeyword))
            {
                var input = FindInputNearLabel(searchRoot, label);
                if (input != null)
                {
                    return input;
                }
            }

            if (searchRoot != root)
            {
                foreach (var label in FindTextsMatchingKeyword(root, labelKeyword))
                {
                    var input = FindInputNearLabel(root, label);
                    if (input != null)
                    {
                        return input;
                    }
                }
            }

            return null;
        }

        private static AutomationElement ResolveSearchRoot(
            AutomationElement root,
            string? groupName,
            string? containerLabelKeyword)
        {
            if (!string.IsNullOrWhiteSpace(containerLabelKeyword))
            {
                foreach (var containerLabel in FindTextsMatchingKeyword(root, containerLabelKeyword))
                {
                    var container = TreeWalker.ControlViewWalker.GetParent(containerLabel);
                    if (container != null)
                    {
                        return container;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(groupName))
            {
                return root;
            }

            return FindGroupByName(root, groupName) ?? root;
        }

        private static IReadOnlyList<AutomationElement> FindTextsMatchingKeyword(
            AutomationElement root,
            string labelKeyword)
        {
            var results = new List<AutomationElement>();
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                var name = element.Current.Name ?? string.Empty;
                if (TextNameMatchesKeyword(name, labelKeyword))
                {
                    results.Add(element);
                }
            }

            // 部分控件将静态标签暴露为 Custom
            var customCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom);
            var customElements = root.FindAll(TreeScope.Descendants, customCondition);
            foreach (AutomationElement element in customElements)
            {
                var name = element.Current.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (TextNameMatchesKeyword(name, labelKeyword))
                {
                    results.Add(element);
                }
            }

            return results;
        }

        private static bool TextNameMatchesKeyword(string elementName, string labelKeyword)
        {
            var normalizedName = NormalizeLabelText(elementName);
            var normalizedKeyword = NormalizeLabelText(labelKeyword);
            return normalizedName.Contains(normalizedKeyword, StringComparison.Ordinal)
                || normalizedKeyword.Contains(normalizedName, StringComparison.Ordinal);
        }

        private static string NormalizeLabelText(string text)
        {
            return text.Trim().TrimEnd('：', ':', ' ', '\t').Trim();
        }

        private static AutomationElement? FindInputNearLabel(
            AutomationElement searchRoot,
            AutomationElement label)
        {
            var input = FindInputOnSameRowAsLabel(searchRoot, label);
            if (input != null)
            {
                return input;
            }

            return FindFollowingSiblingInput(label);
        }

        private static AutomationElement? FindFirstInputInSubtree(AutomationElement root)
        {
            foreach (AutomationElement input in FindAllInputCandidates(root))
            {
                return input;
            }

            return null;
        }

        private static AutomationElement? FindFollowingSiblingInput(AutomationElement label)
        {
            var walker = TreeWalker.ControlViewWalker;
            var sibling = walker.GetNextSibling(label);
            while (sibling != null)
            {
                if (IsInputCandidate(sibling))
                {
                    return sibling;
                }

                var nestedInput = FindFirstInputInSubtree(sibling);
                if (nestedInput != null)
                {
                    return nestedInput;
                }

                sibling = walker.GetNextSibling(sibling);
            }

            return null;
        }

        private static bool IsInputCandidate(AutomationElement element)
        {
            var rect = element.Current.BoundingRectangle;
            if (!element.Current.IsEnabled || rect.Width <= 0 || rect.Height <= 0)
            {
                return false;
            }

            var controlType = element.Current.ControlType;
            if (controlType == ControlType.Edit)
            {
                return true;
            }

            if (controlType == ControlType.Custom || controlType == ControlType.Spinner)
            {
                // VG 2022 数值框多为 Custom，名称常与 Group 相同（如「堆栈设置」），未必暴露 ValuePattern
                return true;
            }

            return false;
        }

        private static IEnumerable<AutomationElement> FindAllInputCandidates(AutomationElement root)
        {
            var condition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner));
            var elements = root.FindAll(TreeScope.Descendants, condition);
            foreach (AutomationElement element in elements)
            {
                if (IsInputCandidate(element))
                {
                    yield return element;
                }
            }
        }

        private static AutomationElement? FindInputOnSameRowAsLabel(
            AutomationElement searchRoot,
            AutomationElement label)
        {
            var labelRect = label.Current.BoundingRectangle;
            if (labelRect.Width <= 0 || labelRect.Height <= 0)
            {
                return null;
            }

            const double rowTolerance = 16.0;
            AutomationElement? bestInput = null;
            var bestDistance = double.MaxValue;

            foreach (AutomationElement input in FindAllInputCandidates(searchRoot))
            {
                var inputRect = input.Current.BoundingRectangle;
                if (inputRect.Width <= 0 || inputRect.Height <= 0)
                {
                    continue;
                }

                var verticalDelta = Math.Abs((inputRect.Top + inputRect.Bottom) / 2
                    - (labelRect.Top + labelRect.Bottom) / 2);
                if (verticalDelta > rowTolerance)
                {
                    continue;
                }

                var horizontalDistance = inputRect.Left - labelRect.Right;
                if (horizontalDistance < -24)
                {
                    continue;
                }

                var distance = Math.Abs(horizontalDistance) + verticalDelta;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestInput = input;
                }
            }

            return bestInput;
        }

        public static IReadOnlyList<AutomationElement> FindEditsInGroup(AutomationElement root, string groupName)
        {
            var group = FindGroupByName(root, groupName);
            if (group == null)
            {
                return Array.Empty<AutomationElement>();
            }

            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
            var elements = group.FindAll(TreeScope.Descendants, condition);
            var edits = new List<AutomationElement>();
            foreach (AutomationElement element in elements)
            {
                if (IsElementInteractable(element))
                {
                    edits.Add(element);
                }
            }

            edits.Sort((a, b) =>
            {
                var ra = a.Current.BoundingRectangle;
                var rb = b.Current.BoundingRectangle;
                var topCompare = ra.Top.CompareTo(rb.Top);
                return topCompare != 0 ? topCompare : ra.Left.CompareTo(rb.Left);
            });

            return edits;
        }

        public static bool TrySetEditValueSafe(AutomationElement? edit, string value)
        {
            if (edit == null)
            {
                return false;
            }

            try
            {
                return TrySetEditValue(edit, value);
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        /// <summary>
        /// 在 UIA 子树中按窗口/窗格 Name 关键字查找（支持嵌套在 VG 主窗口内的对话框）。
        /// </summary>
        public static AutomationElement? FindDescendantWindowByNameKeyword(
            AutomationElement root,
            string nameKeyword,
            string? requireButtonName = null)
        {
            foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                var elements = root.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement element in elements)
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!name.Contains(nameKeyword, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var rect = element.Current.BoundingRectangle;
                    if (!element.Current.IsEnabled || rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(requireButtonName)
                        && FindButtonByName(element, requireButtonName) == null)
                    {
                        continue;
                    }

                    return element;
                }
            }

            return null;
        }

        public static AutomationElement? FindVisibleWindowByNamePrefix(
            AutomationElement root,
            string namePrefix,
            bool requireImageKeyword = false)
        {
            foreach (var controlType in new[] { ControlType.Window, ControlType.Pane })
            {
                var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
                var elements = root.FindAll(TreeScope.Descendants, condition);
                foreach (AutomationElement element in elements)
                {
                    var name = element.Current.Name ?? string.Empty;
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

                    if (IsElementInteractable(element))
                    {
                        return element;
                    }
                }
            }

            return null;
        }

        public static IReadOnlyList<AutomationElement> FindAllDescendantsByControlType(
            AutomationElement root,
            ControlType controlType)
        {
            var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
            var elements = root.FindAll(TreeScope.Descendants, condition);
            var result = new List<AutomationElement>();
            foreach (AutomationElement element in elements)
            {
                result.Add(element);
            }

            return result;
        }
    }
}
