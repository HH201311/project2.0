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

        public Task RunSimulationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null) =>
            ExecuteExclusiveAsync(
                (token, prog) => _vgAutomationService.RunSimulationAsync(settings, token, prog),
                cancellationToken,
                progress,
                "切片流程正在运行中");

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
                await action(_runCts.Token, progress).ConfigureAwait(false);
            }
            finally
            {
                _runCts.Dispose();
                _runCts = null;
            }
        }

        public void Cancel()
        {
            _runCts?.Cancel();
        }
    }
}
