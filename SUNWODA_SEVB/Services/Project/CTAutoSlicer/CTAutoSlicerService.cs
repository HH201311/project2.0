using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public class CTAutoSlicerService : ICTAutoSlicerService
    {
        private readonly VGAutomationService _vgAutomationService;
        private CancellationTokenSource? _runCts;

        public CTAutoSlicerService(VGAutomationService vgAutomationService)
        {
            _vgAutomationService = vgAutomationService;
        }

        public bool IsRunning => _runCts != null;

        public bool IsPaused => _vgAutomationService.IsPaused;

        public bool IsAwaitingManualIntervention => _vgAutomationService.IsAwaitingManualIntervention;

        public event EventHandler? PauseStateChanged
        {
            add => _vgAutomationService.PauseStateChanged += value;
            remove => _vgAutomationService.PauseStateChanged -= value;
        }

        public event EventHandler? ManualInterventionStateChanged
        {
            add => _vgAutomationService.ManualInterventionStateChanged += value;
            remove => _vgAutomationService.ManualInterventionStateChanged -= value;
        }

        public bool TogglePause()
        {
            if (!IsRunning)
            {
                return false;
            }

            return _vgAutomationService.TogglePause();
        }

        public void ConfirmManualIntervention() => _vgAutomationService.ConfirmManualIntervention();

        public Task RunSimulationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.RunSimulationAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中");

        public Task RunAiAnnotationBatchAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.RunAiAnnotationBatchAsync(settings, token, prog),
                cancellationToken,
                progress,
                "AI 标注流程正在运行中");

        public Task TestRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestRenderingPresetAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestThickPlateAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestThickPlateAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestSliceExportAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestSliceExportAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestHistogramGrayScaleAdjustAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestSimple321AlignmentAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestPostAlignCaptureDragAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestPostAlignCaptureDragAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestPre321ViewPrepAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestPre321ViewPrepAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task TestOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null,
            string? explicitImagePath = null,
            string? explicitOutputFolder = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.TestOfflineCtVisionAnnotationAsync(
                    settings,
                    token,
                    prog,
                    explicitImagePath,
                    explicitOutputFolder),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        public Task RunOfflineCtDllBatchTestAsync(
            CTAutoSlicerSettings settings,
            string inputFolder,
            string outputFolder,
            int dllBlackHeight,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.RunOfflineCtDllBatchTestAsync(
                    settings,
                    inputFolder,
                    outputFolder,
                    dllBlackHeight,
                    token,
                    prog),
                cancellationToken,
                progress,
                "切片流程正在运行中，请稍后再试");

        private async Task ExecuteExclusiveAsync(
            Func<CancellationToken, IProgress<string>?, Task> action,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string busyMessage)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(busyMessage);
            }

            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                _vgAutomationService.ClearPause();
                _vgAutomationService.ClearManualIntervention();
                await action(_runCts.Token, progress).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                progress?.Report("流程已取消");
                throw;
            }
            catch (Exception ex)
            {
                // 大捕获：防止未处理异常冲出服务层导致 UI/async void 闪退
                // 反射 TargetInvocationException 只含英文套话，须展开 InnerException
                var message = FormatExceptionChain(ex);
                progress?.Report($"切片服务捕获未处理异常（已拦截，防止闪退）：{message}");
                System.Diagnostics.Trace.TraceError($"[CTAutoSlicerService] {ex}");
            }
            finally
            {
                _vgAutomationService.ClearPause();
                _vgAutomationService.ClearManualIntervention();
                _runCts.Dispose();
                _runCts = null;
            }
        }

        public void Cancel()
        {
            _vgAutomationService.ClearPause();
            _vgAutomationService.ClearManualIntervention();
            _runCts?.Cancel();
        }

        /// <summary>展开异常链（含反射 TargetInvocationException 的 InnerException）。</summary>
        private static string FormatExceptionChain(Exception ex)
        {
            var parts = new List<string>();
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message))
                {
                    parts.Add(current.Message.Trim());
                }
            }

            return parts.Count > 0 ? string.Join(" → ", parts) : ex.GetType().Name;
        }
    }
}
