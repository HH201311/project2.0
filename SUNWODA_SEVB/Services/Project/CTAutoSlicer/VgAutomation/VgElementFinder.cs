using System.Windows.Automation;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer.VgAutomation
{
    /// <summary>
    /// VG UIA 通用查找（仅 CTAutoSlicer 内部使用，不改动公共 Helper）。
    /// 禁止 Root Descendants；优先主窗 → 同进程顶层 → 必要时桌面顶层。
    /// </summary>
    internal static class VgElementFinder
    {
        /// <summary>
        /// 在指定范围内按 Name 前缀查找可见 Window/Pane（不做全桌面 Descendants）。
        /// </summary>
        internal static AutomationElement? FindNamedWindowAmong(
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
        internal static AutomationElement? FindNamedWindowOnDesktopByProcess(int processId, string namePrefix)
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
        internal static IEnumerable<AutomationElement> EnumerateSameProcessTopLevelWindows(int processId)
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
        internal static IEnumerable<AutomationElement> EnumerateDesktopTopLevelWindows()
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
        internal static AutomationElement? FindWindowByNameScoped(
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
        internal static AutomationElement? FindVisibleListItemScoped(
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
        internal static AutomationElement? FindVisibleFileNameEditScoped(
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
        internal static AutomationElement? FindVisibleWindowByNamePrefixScoped(
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
        internal static AutomationElement? FindDescendantWindowByNameKeywordScoped(
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

    }
}