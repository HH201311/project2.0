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

        public async Task RunSimulationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("切片流程正在运行中");
            }

            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await _vgAutomationService
                    .RunSimulationAsync(settings, _runCts.Token, progress)
                    .ConfigureAwait(false);
            }
            finally
            {
                _runCts.Dispose();
                _runCts = null;
            }
        }

        public Task TestRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("切片流程正在运行中，请稍后再试");
            }

            return _vgAutomationService.TestRenderingPresetAsync(settings, cancellationToken, progress);
        }

        public Task TestThickPlateAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("切片流程正在运行中，请稍后再试");
            }

            return _vgAutomationService.TestThickPlateAsync(settings, cancellationToken, progress);
        }

        public Task TestSliceExportAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException("切片流程正在运行中，请稍后再试");
            }

            return _vgAutomationService.TestSliceExportAsync(settings, cancellationToken, progress);
        }

        public void Cancel()
        {
            _runCts?.Cancel();
        }
    }
}
