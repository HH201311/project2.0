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
        // 单测入口：LUT / 直方图 / 简单3-2-1
        public async Task TestRenderingPresetAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            PrepareInputBlockForRun();
            try
            {
                var namingSource = TryResolveNamingSourceFromCtFolder(settings);
                var polarity = settings.ResolveRenderingPresetPolarityLabel(namingSource);
                var presetName = settings.ResolveRenderingPresetName(namingSource);
                if (string.IsNullOrEmpty(presetName))
                {
                    throw new InvalidOperationException(
                        $"请先填写{polarity}预设名称（直方图标定 → 渲染预设）");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report(
                    $"渲染预设单步测试：请确保 VG 中已导入体积；" +
                    $"按文件名选{polarity}「{presetName}」（参考={Path.GetFileName(namingSource ?? "")}）");
                _logger.Info(
                    $"渲染预设单步测试开始，{polarity}目标「{presetName}」，命名源={namingSource}");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                await ApplyRenderingPresetAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress,
                        namingSourcePath: namingSource)
                    .ConfigureAwait(false);

                progress?.Report("渲染预设单步测试完成");
                _logger.Info("渲染预设单步测试完成");
            }
            finally
            {
                ReleaseInputBlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 单步测试：截图 → 掩膜优化 → 峰谷半坡 → 拖拽红线到位 → LUT 关键段中点对齐红线。
        /// </summary>
        public async Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            PrepareInputBlockForRun();
            try
            {
                if (!settings.HasHistogramRoi)
                {
                    throw new InvalidOperationException("尚未标定直方图区域，请先点击「标定直方图区域」框选 ROI");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                var namingSource = TryResolveNamingSourceFromCtFolder(settings);
                var polarity = settings.ResolveRenderingPresetPolarityLabel(namingSource);
                if (!settings.HasLutSegmentPointsFor(namingSource))
                {
                    throw new InvalidOperationException(
                        $"尚未标定{polarity} LUT 关键段两端点，请先在「直方图标定」中标定对应电极的两点");
                }

                settings.TryResolveLutSegmentPoints(
                    namingSource,
                    out var p1x,
                    out var p1y,
                    out var p2x,
                    out var p2y);

                progress?.Report(
                    $"直方图灰度单步测试：请确保 VG 中已显示直方图且已应用{polarity} LUT 预设");
                _logger.Info(
                    $"直方图灰度单步测试开始，ROI=({settings.HistogramRoiX},{settings.HistogramRoiY}," +
                    $"{settings.HistogramRoiWidth}x{settings.HistogramRoiHeight}) " +
                    $"LUT{polarity}=({p1x},{p1y})-({p2x},{p2y})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                var productTag = SliceExportNamingHelper.BuildCaptureProductTag(namingSource);
                if (!string.IsNullOrWhiteSpace(namingSource))
                {
                    progress?.Report($"直方图单测命名参考：{Path.GetFileName(namingSource)} → 产品标签={productTag} → {polarity}");
                }
                else
                {
                    progress?.Report($"直方图单测未找到体素文件，截图产品标签={productTag}，按{polarity}标点");
                }

                await ApplyHistogramGrayScaleAdjustAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress,
                        namingSourcePath: namingSource)
                    .ConfigureAwait(false);

                progress?.Report("直方图灰度调整单步测试完成（截图+峰谷+拖红线+LUT中点对齐+按Y扩展）");
                _logger.Info("直方图灰度单步测试完成");
            }
            finally
            {
                ReleaseInputBlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 单步测试：3D 截图 → 按配置选本地/视觉 DLL 找点 → 打开「简单 3-2-1 对齐」→ Ctrl+点击 → 完成。
        /// </summary>
        public async Task TestSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            PrepareInputBlockForRun();
            try
            {
                if (!settings.HasVision3dRoi)
                {
                    throw new InvalidOperationException("尚未标定 3D 视图区域，请先在「阶段四视觉对齐」中标定 3D 区域");
                }

                if (!settings.HasVisionTopViewRoi)
                {
                    throw new InvalidOperationException(
                        "尚未标定「上」视图区域，请先在「阶段四视觉对齐」中标定「上」视图（向量两点需要）");
                }

                if (!settings.HasSimple321ToolbarClick)
                {
                    throw new InvalidOperationException(
                        "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框");
                }

                if (!settings.HasSimple321OffsetEdit)
                {
                    throw new InvalidOperationException(
                        "尚未标定三点后偏移 Edit 点击位置，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                var sourceLabel = settings.UseVisionDllForSimple321
                    ? "视觉 DLL 算法（CTAutoCutImg.dll）"
                    : "上位机本地算法（OpenCV：Vision3d / VisionTopView）";
                var namingSource = TryResolveNamingSourceFromCtFolder(settings);
                var productTag = SliceExportNamingHelper.BuildCaptureProductTag(namingSource);
                progress?.Report(
                    $"【找点算法】当前调用：{sourceLabel}；产品标签={productTag}；请确保 VG 已导入体积且第四象限 3D 可见");
                _logger.Info(
                    $"【找点算法】简单 3-2-1 对齐测试开始：当前调用={sourceLabel}" +
                    $"（Simple321PointSource={settings.Simple321PointSource}；产品标签={productTag}），" +
                    $"3D ROI=({settings.Vision3dRoiX},{settings.Vision3dRoiY}," +
                    $"{settings.Vision3dRoiWidth}x{settings.Vision3dRoiHeight})，" +
                    $"下拉点击=({settings.Simple321ToolbarClickX},{settings.Simple321ToolbarClickY})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);

                await RunSimple321AlignmentAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress,
                        namingSource)
                    .ConfigureAwait(false);

                progress?.Report("简单 3-2-1 对齐测试完成");
                _logger.Info("简单 3-2-1 对齐测试完成");
            }
            finally
            {
                ReleaseInputBlock();
                _activeVgProcess = null;
            }
        }
    }
}
