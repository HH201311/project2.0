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
        private const string ImportFailProgressPrefix = "@@IMPORT_FAIL@@";
        /// <summary>请求 UI 弹出人工干预面板：@@MANUAL_INTERVENE@@文件名|原因</summary>
        private const string ManualInterveneProgressPrefix = "@@MANUAL_INTERVENE@@";
        /// <summary>关闭人工干预面板。</summary>
        private const string ManualInterveneEndProgressPrefix = "@@MANUAL_INTERVENE_END@@";
        private const string SaveDialogWindowName = "VGSTUDIO MAX 问题";
        private const string SaveDialogNoButtonName = "否(N)";
        private const string AutoSaveRecoveryDialogWindowName = "自动保存恢复";
        private const string AutoSaveRecoverySkipButtonName = "跳过";
        /// <summary>启动阶段轮询「自动保存恢复」/「文件(F)」的间隔</summary>
        private const int VgStartupPollIntervalMs = 500;
        /// <summary>导入等步骤中的通用轮询间隔</summary>
        private const int VgReadyPollIntervalMs = 1000;
        private const string QuickImportDialogNamePrefix = "快速导入";
        private const string QuickImportEditName = "文件名(N):";
        private const string QuickImportOpenButtonName = "打开(O)";
        private const string QuickImportOpenButtonNameFallback = "打开";
        private const string FileMenuItemName = "文件(F)";
        private const string FileSelectionFinishButtonName = "完成(F)";
        private const string ImportProgressWindowName = "请耐心等待...";
        /// <summary>导入进度框内文（Insights：Text Name=正在导入体积），比仅认窗口标题更稳。</summary>
        private const string ImportProgressStatusText = "正在导入体积";
        private const string RenderingPanelWindowName = "渲染";
        private const string ThickPlateOkButtonName = "确定";
        private const string SliceExportDialogNamePrefix = "保存";
        private const string SliceExport2DViewGroupName = "2D 视图";
        private const string SliceExport2DViewComboName = "2D 视图";
        private const string SliceExportImageSizeGroupName = "图像大小";
        private const string SliceExportStackSettingsGroupName = "堆栈设置";
        private const string SliceExportSliceRangeGroupName = "切片图范围";
        private const string SliceExportQuantityLabelKeyword = "切片图数量";
        private const string SliceExportRangeStartLabelKeyword = "开始";
        private const string SliceExportRangeEndLabelKeyword = "结束";
        private const string SliceExportSaveButtonName = "保存...";
        private const string SliceExportJpegQualityWindowName = "JPEG 质量";
        private const string SliceExportQualityConfirmButtonName = "确定";
        private const string SliceExportWriteProgressText = "正在写入图像";
        private const string SliceExportQtyProgressPrefix = "@@SLICE_QTY@@";
        private const string SystemSaveFileEditName = "文件名:";

        private const string Simple321MenuItemName = "简单 3-2-1 对齐(S)...";
        private const string Simple321MenuItemNameContains = "简单 3-2-1 对齐";
        private const string Simple321DialogNamePrefix = "简单 3-2-1 对齐";
        private const string Simple321FinishButtonName = "完成(F)";
        /// <summary>标点前先重置，避免残留对齐状态（Insights：Name = 重置(R)，AccessKey Alt+R）</summary>
        private const string Simple321ResetButtonName = "重置(R)";
        /// <summary>对齐完成后可能弹出的确认窗（Insights：Name = VGSTUDIO MAX 警告）</summary>
        private const string VgAlignedWarningDialogName = "VGSTUDIO MAX 警告";
        /// <summary>警告窗「是」按钮（Insights：Name = 是(Y)，AccessKey Alt+Y）</summary>
        private const string VgAlignedWarningYesButtonName = "是(Y)";

        private const int UiPollIntervalMs = UIAutomationHelper.DefaultConditionPollIntervalMs;
        private const int OffscreenProgressBypassMs = 2000;
        private const int ProcessMainWindowTimeoutMs = 10000;
        /// <summary>Qt 预设下拉：每种打开方式的最大等待，失败立即试下一方式（避免每步耗满 PresetDropdownWaitTime）。</summary>
        private const int PresetDropdownOpenStepWaitMs = 800;
        private const int PresetListItemPollIntervalMs = 100;
        /// <summary>Win32 点击列表项后等待下拉关闭的上限。</summary>
        private const int PresetDropdownCloseWaitMs = 1500;
        /// <summary>切片导出：单字段 Enter 提交后等待 VG/Qt 消化读数（Custom 框 UIA 常滞后）。</summary>
        private const int SliceExportFieldCommitDelayMs = 300;
        /// <summary>进程池调度：无 UI 动作时的空闲轮询间隔。</summary>
        private const int PoolIdlePollIntervalMs = 1000;

        /// <summary>
        /// 后处理独占计数：&gt;0 时后台导入等待暂停 UIA，避免抢焦点打断 LUT/厚板/3-2-1/导出。
        /// </summary>
        private int _postPipelineExclusiveCount;

        private readonly ILoggerService<VGAutomationService> _logger;
        private readonly IAngleAlgorithmService _angleAlgorithmService;
        private readonly ICutImgAlgorithmService _cutImgAlgorithmService;
        private readonly ICTOfflineVisionService _offlineCtVisionService;
        private readonly EventSimulator _simulator;
        private readonly AutomationPauseGate _pauseGate = new();
        private readonly ManualInterventionGate _manualInterventionGate = new();
        /// <summary>
        /// 可能于任意时刻弹出的中断窗口队列。步骤异常/失败时先 Drain，处理后重试该步骤。
        /// 后续可继续 Register 其它弹窗（保存提示等）。
        /// </summary>
        private readonly List<InterruptDialogHandler> _interruptDialogQueue = new();
        private Process? _activeVgProcess;

        /// <summary>进程池槽状态。</summary>
        private enum VgSlotState
        {
            Importing,
            ImportDone,
            Failed
        }

        /// <summary>
        /// 体素文件夹排队条目：ImportPath 传给 VG；QueueEntryPath 为文件夹内条目（真实文件或 .lnk），成功后删除。
        /// </summary>
        private sealed class VolumeQueueItem
        {
            public required string ImportPath { get; init; }
            public required string QueueEntryPath { get; init; }
        }

        /// <summary>单个 VG 进程槽：绑定文件路径与导入等待任务。</summary>
        private sealed class VgProcessSlot
        {
            public required Process Process { get; init; }
            /// <summary>传给 VG 的实际体积路径（快捷方式已解析为目标 .vgl/.vgi）。</summary>
            public required string FilePath { get; init; }
            /// <summary>体素文件夹内排队条目路径；成功后删除此项（快捷方式时勿删源文件）。</summary>
            public required string QueueEntryPath { get; init; }
            public VgSlotState State { get; set; } = VgSlotState.Importing;
            public Task? WaitTask { get; set; }
            public Exception? Error { get; set; }
            public DateTime ImportStartedUtc { get; set; } = DateTime.UtcNow;
        }

        public VGAutomationService(
            ILoggerService<VGAutomationService> logger,
            IAngleAlgorithmService angleAlgorithmService,
            ICutImgAlgorithmService cutImgAlgorithmService,
            ICTOfflineVisionService offlineCtVisionService)
        {
            _logger = logger;
            _angleAlgorithmService = angleAlgorithmService;
            _cutImgAlgorithmService = cutImgAlgorithmService;
            _offlineCtVisionService = offlineCtVisionService;
            _simulator = new EventSimulator();
            RegisterDefaultInterruptDialogs();
            UIAutomationHelper.CooperativePauseWaitAsync = _pauseGate.WaitIfPausedAsync;
        }

        public bool IsPaused => _pauseGate.IsPaused;

        public bool IsAwaitingManualIntervention => _manualInterventionGate.IsAwaiting;

        public event EventHandler? PauseStateChanged
        {
            add => _pauseGate.PauseStateChanged += value;
            remove => _pauseGate.PauseStateChanged -= value;
        }

        public event EventHandler? ManualInterventionStateChanged
        {
            add => _manualInterventionGate.StateChanged += value;
            remove => _manualInterventionGate.StateChanged -= value;
        }

        /// <summary>切换暂停/继续。暂停时解除 BlockInput，便于手动检查 VG。</summary>
        public bool TogglePause()
        {
            var paused = _pauseGate.Toggle();
            if (paused)
            {
                Unlock();
                _logger.Info("流程已暂停（Alt+P 可继续）");
            }
            else
            {
                _logger.Info("流程已继续");
            }

            return paused;
        }

        public void ClearPause() => _pauseGate.Reset();

        /// <summary>操作员点击「处理完成」后调用，放行人工干预等待。</summary>
        public void ConfirmManualIntervention()
        {
            if (!_manualInterventionGate.IsAwaiting)
            {
                return;
            }

            _logger.Info("操作员确认人工干预「处理完成」");
            _manualInterventionGate.SignalCompleted();
        }

        /// <summary>取消/结束流程时清出暂停与人工干预门闩。</summary>
        public void ClearManualIntervention() => _manualInterventionGate.Reset();

        private Task WaitIfPausedAsync(CancellationToken cancellationToken) =>
            _pauseGate.WaitIfPausedAsync(cancellationToken);

        /// <summary>中断弹窗处理器：查找 + 关闭动作。</summary>
        private sealed class InterruptDialogHandler
        {
            public required string Name { get; init; }
            public required Func<Process, AutomationElement?> Find { get; init; }
            public required Func<Process, AutomationElement, CancellationToken, IProgress<string>?, Task<bool>> HandleAsync { get; init; }
        }

        private void RegisterDefaultInterruptDialogs()
        {
            _interruptDialogQueue.Add(new InterruptDialogHandler
            {
                Name = AutoSaveRecoveryDialogWindowName,
                Find = FindAutoSaveRecoveryDialog,
                HandleAsync = HandleAutoSaveRecoveryDialogAsync
            });
        }

        private static Task<bool> WaitUntilAsync(
            Func<bool> condition,
            int timeoutMs,
            CancellationToken cancellationToken,
            int pollIntervalMs = UiPollIntervalMs) =>
            UIAutomationHelper.WaitUntilAsync(
                condition,
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken,
                pollIntervalMs);

        private static Task<bool> WaitUntilDisappearedAsync(
            Func<AutomationElement?> findElement,
            int timeoutMs,
            CancellationToken cancellationToken,
            int pollIntervalMs = UiPollIntervalMs) =>
            UIAutomationHelper.WaitUntilDisappearedAsync(
                findElement,
                TimeSpan.FromMilliseconds(timeoutMs),
                cancellationToken,
                pollIntervalMs);

        private static bool TryIsFileMenuReady(Process ctViewerProcess) =>
            UIAutomationHelper.FindFileMenuForProcess(ctViewerProcess, FileMenuItemName) != null;

        private static bool IsRenderingWorkspaceReady(AutomationElement mainWindow, CTAutoSlicerSettings settings)
        {
            if (!IsVolumeImportReady(mainWindow, settings))
            {
                return false;
            }

            var renderingPanel = FindRenderingPanel(mainWindow);
            if (renderingPanel == null)
            {
                return false;
            }

            var comboBox = FindRenderingPresetComboBox(renderingPanel);
            return comboBox is { Current.IsEnabled: true };
        }

        private Task WaitForProcessMainWindowAsync(Process process, CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () =>
                {
                    process.Refresh();
                    return process.MainWindowHandle != IntPtr.Zero;
                },
                ProcessMainWindowTimeoutMs,
                cancellationToken);

        private Task<bool> WaitForFileMenuReadyAsync(
            Process ctViewerProcess,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(() => TryIsFileMenuReady(ctViewerProcess), timeoutMs, cancellationToken);

        private Task<bool> WaitForRenderingWorkspaceReadyAsync(
            Process ctViewerProcess,
            CTAutoSlicerSettings settings,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () =>
                {
                    try
                    {
                        // 静默取窗，避免后台轮询抢焦点
                        var window = TryGetMainWindowElement(ctViewerProcess);
                        return window != null && IsRenderingWorkspaceReady(window, settings);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeoutMs,
                cancellationToken);

        private Task<bool> WaitForRenderingWorkspaceReadyAsync(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(() => IsRenderingWorkspaceReady(mainWindow, settings), timeoutMs, cancellationToken);

        private Task<bool> WaitForComboDropdownOpenAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                timeoutMs,
                cancellationToken,
                PresetListItemPollIntervalMs);

        private Task<bool> WaitForComboDropdownClosedAsync(
            AutomationElement comboBox,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => !UIAutomationHelper.IsRenderingPresetDropdownOpen(comboBox),
                timeoutMs,
                cancellationToken,
                PresetListItemPollIntervalMs);

        private Task<bool> WaitForViewportQuadrantsReadyAsync(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings,
            int minCount,
            int timeoutMs,
            CancellationToken cancellationToken) =>
            WaitUntilAsync(
                () => UIAutomationHelper.FindViewportQuadrants(
                    mainWindow,
                    settings.ViewportMinWidth,
                    settings.ViewportMinHeight).Count >= minCount,
                timeoutMs,
                cancellationToken);

        private static AutomationElement? FindSavePromptDialog(AutomationElement mainWindow) =>
            FindWindowByNameScoped(mainWindow, SaveDialogWindowName);

    }
}
