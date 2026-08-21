using SharpHook.Native;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public partial class VGAutomationService
    {
        /// <summary>
        /// 临时单测：3D 找点前视角准备 — 绝对坐标点击切换视图 → 等待 → 沿标定箭头拖拽。
        /// 尚未接入主流程；用于验证极耳遮挡场景下的视角切换效果。
        /// </summary>
        public async Task TestPre321ViewPrepAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            PrepareInputBlockForRun();
            try
            {
                if (!settings.HasPre321ViewPrepPrerequisites)
                {
                    throw new InvalidOperationException(
                        "尚未标定「3D找点前视角准备」的切换点击与拖拽箭头，请先在「视觉对齐」页完成标定");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report(
                    "3D找点前视角准备测试：请确保 VG 已导入体积且第四象限 3D 可见");
                _logger.Info(
                    $"3D找点前视角准备测试：click=({settings.Pre321ViewClickX},{settings.Pre321ViewClickY}) " +
                    $"drag ({settings.Pre321ViewDragStartX},{settings.Pre321ViewDragStartY})→" +
                    $"({settings.Pre321ViewDragEndX},{settings.Pre321ViewDragEndY})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                EnsureVgForeground(ctViewerProcess);
                _ = GetFreshMainWindow(ctViewerProcess);

                await RunPre321ViewPrepAsync(settings, cancellationToken, progress).ConfigureAwait(false);

                progress?.Report("3D找点前视角准备测试完成");
                _logger.Info("3D找点前视角准备测试完成");
            }
            finally
            {
                ReleaseInputBlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 点击切换视图坐标 → 等待 → 沿标定箭头直线拖拽。
        /// </summary>
        private async Task RunPre321ViewPrepAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ClickPre321ViewSwitchAsync(settings, cancellationToken, progress).ConfigureAwait(false);

            var waitMs = Math.Max(0, settings.Pre321ViewClickWaitMs);
            if (waitMs > 0)
            {
                progress?.Report($"等待 {waitMs}ms 后开始拖拽…");
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }

            await DragPre321ViewArrowAsync(settings, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task ClickPre321ViewSwitchAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasPre321ViewClick)
            {
                throw new InvalidOperationException("切换视图点击坐标尚未标定");
            }

            progress?.Report(
                $"点击切换视图坐标 ({settings.Pre321ViewClickX},{settings.Pre321ViewClickY})…");
            _logger.Info(
                $"3D找点前视角：绝对点击 ({settings.Pre321ViewClickX},{settings.Pre321ViewClickY})");

            await NativeClickScreenAsync(
                    settings,
                    settings.Pre321ViewClickX,
                    settings.Pre321ViewClickY,
                    cancellationToken)
                .ConfigureAwait(false);

            progress?.Report("切换视图点击已完成");
        }

        private async Task DragPre321ViewArrowAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasPre321ViewDrag)
            {
                throw new InvalidOperationException("找点前拖拽箭头尚未标定或起点终点重合");
            }

            var path = MouseSimulationHelper.BuildLinearDragPath(
                settings.Pre321ViewDragStartX,
                settings.Pre321ViewDragStartY,
                settings.Pre321ViewDragEndX,
                settings.Pre321ViewDragEndY);

            progress?.Report(
                $"沿箭头拖拽：({settings.Pre321ViewDragStartX},{settings.Pre321ViewDragStartY}) → " +
                $"({settings.Pre321ViewDragEndX},{settings.Pre321ViewDragEndY})，路径点 {path.Count}");
            _logger.Info(
                $"3D找点前视角拖拽：({settings.Pre321ViewDragStartX},{settings.Pre321ViewDragStartY})→" +
                $"({settings.Pre321ViewDragEndX},{settings.Pre321ViewDragEndY})");

            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                var stepDelay = Math.Max(1, settings.Pre321ViewDragStepDelayMs);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelay);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("找点前箭头拖拽完成");
        }
    }
}
