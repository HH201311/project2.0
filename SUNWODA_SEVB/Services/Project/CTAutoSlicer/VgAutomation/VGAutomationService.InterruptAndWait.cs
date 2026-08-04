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
        // VG 启动等待 / 中断弹窗队列 / 后处理独占 / 文件队列
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

        /// <summary>
        /// 单测用：从 CT 体素文件夹取第一个排队条目路径，供截图文件名产品前缀（与导出名规则一致）。
        /// </summary>
        private string? TryResolveNamingSourceFromCtFolder(CTAutoSlicerSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.CTFileFolder))
            {
                return null;
            }

            var items = GetVolumeQueueItems(settings.CTFileFolder);
            return items.Count > 0 ? items[0].QueueEntryPath : null;
        }
    }
}
