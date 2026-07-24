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
        // 阶段三：三象限厚板
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

    }
}
