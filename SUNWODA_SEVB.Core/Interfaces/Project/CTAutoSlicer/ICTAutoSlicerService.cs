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
        /// 单步测试：按已标定直方图 ROI 截图 → 掩膜优化 → 峰谷半坡 → 拖拽红线。
        /// </summary>
        Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        /// <summary>
        /// 单步测试：阶段四 · 简单 3-2-1 对齐（需已标定 3D ROI）。
        /// 截 3D → DLL 四点 → 打开对齐 → Ctrl+点击四点 → 完成。
        /// </summary>
        Task TestSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null
        );

        void Cancel();
    }
}
