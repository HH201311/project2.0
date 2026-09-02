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
        // 阶段二：渲染 LUT 预设（直方图调整见 HistogramAndPostAlign）
        private async Task ApplyRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string? namingSourcePath = null)
        {
            var polarity = settings.ResolveRenderingPresetPolarityLabel(namingSourcePath);
            var presetName = settings.ResolveRenderingPresetName(namingSourcePath);
            if (string.IsNullOrWhiteSpace(presetName))
            {
                progress?.Report($"跳过渲染预设：{polarity}预设名称为空");
                _logger.Info($"跳过渲染预设：{polarity}预设名称为空（源={Path.GetFileName(namingSourcePath ?? "")}）");
                return;
            }

            progress?.Report(
                $"等待渲染工作区就绪后应用{polarity}预设「{presetName}」" +
                $"（文件={Path.GetFileName(namingSourcePath ?? "")}，最大 {settings.PresetApplyWaitTime}ms）");
            _logger.Info(
                $"等待渲染工作区就绪，最大 {settings.PresetApplyWaitTime}ms 后应用{polarity}预设「{presetName}」" +
                $"（源={namingSourcePath}）");
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

    }
}
