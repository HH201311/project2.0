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
            Lock();
            try
            {
                var presetName = settings.RenderingPresetName.Trim();
                if (string.IsNullOrEmpty(presetName))
                {
                    throw new InvalidOperationException("请先填写渲染预设名称");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report("渲染预设单步测试：请确保 VG 中已导入体积");
                _logger.Info($"渲染预设单步测试开始，目标预设「{presetName}」");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                var mainWindow = GetFreshMainWindow(ctViewerProcess);
                await ApplyRenderingPresetAsync(
                        settings,
                        ctViewerProcess,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("渲染预设单步测试完成");
                _logger.Info("渲染预设单步测试完成");
            }
            finally
            {
                Unlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 单步测试：截图 → 掩膜优化 → 峰谷半坡 → 拖拽红线到位。
        /// </summary>
        public async Task TestHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
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

                progress?.Report("直方图灰度单步测试：请确保 VG 中已显示直方图");
                _logger.Info(
                    $"直方图灰度单步测试开始，ROI=({settings.HistogramRoiX},{settings.HistogramRoiY}," +
                    $"{settings.HistogramRoiWidth}x{settings.HistogramRoiHeight})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                await ApplyHistogramGrayScaleAdjustAsync(
                        settings,
                        ctViewerProcess,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("直方图灰度调整单步测试完成（截图+峰谷+拖拽）");
                _logger.Info("直方图灰度单步测试完成");
            }
            finally
            {
                Unlock();
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
            Lock();
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
                    ? "视觉 DLL（CTAutoCutImg）"
                    : "上位机本地（OpenCV）";
                progress?.Report(
                    $"简单 3-2-1 对齐测试（找点={sourceLabel}）：请确保 VG 已导入体积且第四象限 3D 可见");
                _logger.Info(
                    $"简单 3-2-1 对齐测试开始（Simple321PointSource={settings.Simple321PointSource}），" +
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
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("简单 3-2-1 对齐测试完成");
                _logger.Info("简单 3-2-1 对齐测试完成");
            }
            finally
            {
                Unlock();
                _activeVgProcess = null;
            }
        }
    }
}
