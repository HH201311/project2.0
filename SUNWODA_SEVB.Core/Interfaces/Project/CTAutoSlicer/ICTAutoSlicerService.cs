using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer
{
    public interface ICTAutoSlicerService
    {
        bool IsRunning { get; }

        Task RunSimulationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        /// <summary>
        /// 单步测试：在 VG 已导入体积的前提下，仅应用渲染预设（不执行导入）。
        /// </summary>
        Task TestRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        /// <summary>
        /// 单步测试：在 VG 已导入体积且已应用预设的前提下，对三象限依次执行厚板设置。
        /// </summary>
        Task TestThickPlateAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        /// <summary>
        /// 单步测试：在 VG 已导入体积且已应用预设并完成厚板的前提下，执行三视图切片导出。
        /// </summary>
        Task TestSliceExportAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        /// <summary>
        /// 单步测试：按已标定直方图 ROI 截图 → 定位红线 →（后续）峰谷分析与拖拽。
        /// </summary>
        Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        void Cancel();
    }
}
