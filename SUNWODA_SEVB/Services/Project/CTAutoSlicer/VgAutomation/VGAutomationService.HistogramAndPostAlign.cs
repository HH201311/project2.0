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
        // 直方图灰度拖拽 + 对齐后捕捉拖拽
        private async Task ApplyHistogramGrayScaleAdjustAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            bool softFailOnError = false,
            string? namingSourcePath = null)
        {
            if (!settings.HasHistogramRoi)
            {
                progress?.Report("跳过直方图灰度调整：尚未标定 ROI（请先「标定直方图区域」）");
                _logger.Info("跳过直方图灰度调整：尚未标定 ROI");
                return;
            }

            var productTag = SliceExportNamingHelper.BuildCaptureProductTag(namingSourcePath);
            progress?.Report($"开始直方图灰度调整（截图→峰谷→拖红线→LUT中点对齐→按Y扩展；产品={productTag}）");
            _logger.Info(
                $"直方图灰度调整：产品={productTag}，ROI=({settings.HistogramRoiX},{settings.HistogramRoiY}," +
                $"{settings.HistogramRoiWidth}x{settings.HistogramRoiHeight})");

            try
            {
                await ActivateVgWindowAsync(ctViewerProcess, settings, cancellationToken).ConfigureAwait(false);
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);

                var capture = await CaptureHistogramRoiAsync(
                        settings,
                        cancellationToken,
                        progress,
                        namingSourcePath)
                    .ConfigureAwait(false);

                progress?.Report($"截图已保存：{capture.FilePath}");
                if (!string.IsNullOrWhiteSpace(capture.CompareImagePath))
                {
                    progress?.Report($"优化对比图：{capture.CompareImagePath}（掩膜像素 {capture.MaskPixelCount}）");
                }

                if (!string.IsNullOrWhiteSpace(capture.PeaksImagePath))
                {
                    progress?.Report($"峰谷调试图：{capture.PeaksImagePath}");
                }

                progress?.Report(
                    $"红线初始位置：相对列={capture.RedLineColumn?.ToString() ?? "未检测到"}，" +
                    $"屏幕X={capture.RedLineScreenX?.ToString() ?? "-"}，拖拽Y={capture.DragScreenY}");

                if (!capture.PeaksSuccess)
                {
                    throw new InvalidOperationException(
                        $"峰谷分析失败：{capture.PeaksFailReason ?? "未知原因"}");
                }

                progress?.Report(
                    $"峰谷结果：左线(谷)={capture.ValleyX}, 右线={capture.RightPeakX}, 中线={capture.TargetX} " +
                    $"（两尖峰参考 P1={capture.LeftPeakX}；屏幕中线X={capture.TargetScreenX}）");
                _logger.Info(
                    $"峰谷：P1={capture.LeftPeakX} 左线={capture.ValleyX} 右线={capture.RightPeakX} 中线={capture.TargetX}, " +
                    $"red={capture.RedLineColumn}, compare={capture.CompareImagePath}, peaks={capture.PeaksImagePath}");

                await DragRedLineToTargetAsync(capture, cancellationToken, progress).ConfigureAwait(false);

                await DragLutSegmentMidpointToRedLineAsync(
                        settings,
                        capture,
                        cancellationToken,
                        progress,
                        requireCalibrated: !softFailOnError,
                        namingSourcePath: namingSourcePath)
                    .ConfigureAwait(false);

                await ExpandLutSegmentByPeakValleyDistanceAsync(
                        settings,
                        capture,
                        cancellationToken,
                        progress,
                        requireCalibrated: !softFailOnError,
                        namingSourcePath: namingSourcePath)
                    .ConfigureAwait(false);

                progress?.Report("直方图灰度调整完成");
                _logger.Info("直方图灰度调整完成");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (softFailOnError)
            {
                progress?.Report($"直方图灰度调整失败（跳过，继续后续步骤）：{GetFullExceptionMessage(ex)}");
                _logger.Error("直方图灰度调整失败（主流程跳过）", ex);
            }
        }

        /// <summary>
        /// 将红线从当前位置拖到半坡目标列。
        /// </summary>
        private async Task DragRedLineToTargetAsync(
            HistogramCaptureResult capture,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (capture.TargetScreenX is not int targetX)
            {
                throw new InvalidOperationException("半坡目标屏幕坐标无效");
            }

            var startX = capture.RedLineScreenX
                ?? (capture.RoiX + Math.Max(1, capture.RoiWidth / 4));
            var y = (short)Math.Clamp(capture.DragScreenY, short.MinValue, short.MaxValue);
            var sx = (short)Math.Clamp(startX, short.MinValue, short.MaxValue);
            var ex = (short)Math.Clamp(targetX, short.MinValue, short.MaxValue);

            if (Math.Abs(ex - sx) < 2)
            {
                progress?.Report($"红线已接近目标（ΔX={ex - sx}），跳过拖拽");
                _logger.Info($"红线拖拽跳过：start={sx} end={ex}");
                return;
            }

            var path = HistogramGrayScaleAnalyzer.BuildHorizontalDragPath(sx, y, ex);
            progress?.Report($"拖拽红线：({sx},{y}) → ({ex},{y})，路径点 {path.Count}");
            _logger.Info($"拖拽红线：({sx},{y}) → ({ex},{y})");

            // BlockInput 下 SharpHook 仍可模拟；短暂 Unlock 提高部分环境跟手性
            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelayMs: 4);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("红线拖拽完成");
        }

        /// <summary>
        /// 将当前电极 LUT 预设关键段两端点连线中点水平拖到红线目标列，使关键斜段始终落在峰值附近。
        /// 正/负极端点分别标定；按文件名 -Z/-F 选用；未标定时主流程跳过，单测可要求必须标定。
        /// </summary>
        private async Task DragLutSegmentMidpointToRedLineAsync(
            CTAutoSlicerSettings settings,
            HistogramCaptureResult capture,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            bool requireCalibrated,
            string? namingSourcePath = null)
        {
            var polarity = settings.ResolveRenderingPresetPolarityLabel(namingSourcePath);
            if (!settings.TryResolveLutSegmentPoints(
                    namingSourcePath,
                    out var point1X,
                    out var point1Y,
                    out var point2X,
                    out var point2Y))
            {
                var msg =
                    $"跳过 LUT 关键段中点对齐：尚未标定{polarity}两端点" +
                    "（请先在「直方图标定」中分别标定正极/负极 LUT 关键段两点）";
                if (requireCalibrated)
                {
                    throw new InvalidOperationException(
                        $"尚未标定{polarity} LUT 关键段两端点，请先在「直方图标定」中标定对应电极的两点");
                }

                progress?.Report(msg);
                _logger.Info(msg);
                return;
            }

            if (capture.TargetScreenX is not int targetX)
            {
                throw new InvalidOperationException("半坡目标屏幕坐标无效，无法对齐 LUT 中点");
            }

            var midX = (point1X + point2X) / 2;
            var midY = (point1Y + point2Y) / 2;
            var sx = (short)Math.Clamp(midX, short.MinValue, short.MaxValue);
            var ex = (short)Math.Clamp(targetX, short.MinValue, short.MaxValue);
            var y = (short)Math.Clamp(midY, short.MinValue, short.MaxValue);

            progress?.Report(
                $"LUT 关键段（{polarity}）：P1=({point1X},{point1Y}) P2=({point2X},{point2Y}) " +
                $"中点=({midX},{midY}) → 红线目标X={targetX}");
            _logger.Info(
                $"LUT 关键段中点对齐（{polarity}）：P1=({point1X},{point1Y}) P2=({point2X},{point2Y}) " +
                $"mid=({midX},{midY}) targetX={targetX} 源={namingSourcePath}");

            if (Math.Abs(ex - sx) < 2)
            {
                progress?.Report($"LUT 中点已接近红线（ΔX={ex - sx}），跳过拖拽");
                _logger.Info($"LUT 中点拖拽跳过：start={sx} end={ex}");
                return;
            }

            var path = HistogramGrayScaleAnalyzer.BuildHorizontalDragPath(sx, y, ex);
            progress?.Report($"拖拽 LUT 中点：({sx},{y}) → ({ex},{y})，路径点 {path.Count}");
            _logger.Info($"拖拽 LUT 中点：({sx},{y}) → ({ex},{y})");

            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelayMs: 4);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report($"LUT 关键段中点对齐完成（{polarity}）");
        }

        /// <summary>
        /// 2d：中点已对齐红线后，按 Y=TargetX−ValleyX 非对称扩展关键段两端。
        /// 左端点再向左移动 LeftFactor×Y，右端点再向右移动 RightFactor×Y；坐标钳制在直方图 ROI 内。
        /// 端点屏幕位置由「标定坐标 + 中点平移量」推算（不重新截图识别）。
        /// </summary>
        private async Task ExpandLutSegmentByPeakValleyDistanceAsync(
            CTAutoSlicerSettings settings,
            HistogramCaptureResult capture,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            bool requireCalibrated,
            string? namingSourcePath = null)
        {
            var polarity = settings.ResolveRenderingPresetPolarityLabel(namingSourcePath);
            if (!settings.TryResolveLutSegmentPoints(
                    namingSourcePath,
                    out var point1X,
                    out var point1Y,
                    out var point2X,
                    out var point2Y))
            {
                var msg =
                    $"跳过 LUT 按 Y 扩展：尚未标定{polarity}两端点" +
                    "（请先在「直方图标定」中分别标定正极/负极 LUT 关键段两点）";
                if (requireCalibrated)
                {
                    throw new InvalidOperationException(
                        $"尚未标定{polarity} LUT 关键段两端点，请先在「直方图标定」中标定对应电极的两点");
                }

                progress?.Report(msg);
                _logger.Info(msg);
                return;
            }

            if (capture.TargetScreenX is not int targetX)
            {
                throw new InvalidOperationException("半坡目标屏幕坐标无效，无法按 Y 扩展 LUT 端点");
            }

            // Y：红线目标列相对左侧波谷的水平距离（ROI 列距 ≡ 屏幕像素）
            var yPx = Math.Max(0, capture.TargetX - capture.ValleyX);
            if (yPx < 2)
            {
                progress?.Report($"LUT 按 Y 扩展跳过：Y={yPx}（TargetX−ValleyX）过小");
                _logger.Info($"LUT 按 Y 扩展跳过：Y={yPx}");
                return;
            }

            var leftFactor = Math.Max(0, settings.LutExpandLeftYFactor);
            var rightFactor = Math.Max(0, settings.LutExpandRightYFactor);
            if (leftFactor <= 0 && rightFactor <= 0)
            {
                progress?.Report("LUT 按 Y 扩展跳过：左右扩展因子均为 0");
                _logger.Info("LUT 按 Y 扩展跳过：因子均为 0");
                return;
            }

            // 与 2c 一致：中点对齐后两端 = 标定坐标 + (Target − mid)
            var midX = (point1X + point2X) / 2;
            var deltaX = targetX - midX;

            int leftX;
            int leftY;
            int rightX;
            int rightY;
            if (point1X <= point2X)
            {
                leftX = point1X + deltaX;
                leftY = point1Y;
                rightX = point2X + deltaX;
                rightY = point2Y;
            }
            else
            {
                leftX = point2X + deltaX;
                leftY = point2Y;
                rightX = point1X + deltaX;
                rightY = point1Y;
            }

            var leftShift = (int)Math.Round(leftFactor * yPx);
            var rightShift = (int)Math.Round(rightFactor * yPx);
            var leftEndX = leftX - leftShift;
            var rightEndX = rightX + rightShift;

            // 钳制在直方图 ROI 左右边界内（留 1px 边距）
            var roiMinX = capture.RoiX + 1;
            var roiMaxX = capture.RoiX + Math.Max(2, capture.RoiWidth) - 2;
            leftEndX = Math.Clamp(leftEndX, roiMinX, roiMaxX);
            rightEndX = Math.Clamp(rightEndX, roiMinX, roiMaxX);

            if (leftEndX >= rightEndX)
            {
                progress?.Report(
                    $"LUT 按 Y 扩展跳过：钳制后左右端点交叉或重合（左={leftEndX}, 右={rightEndX}）");
                _logger.Warn(
                    $"LUT 按 Y 扩展跳过：钳制后交叉 left={leftEndX} right={rightEndX} " +
                    $"Y={yPx} L×{leftFactor} R×{rightFactor}");
                return;
            }

            progress?.Report(
                $"LUT 按 Y 扩展（{polarity}）：Y={yPx}=Target({capture.TargetX})−Valley({capture.ValleyX})，" +
                $"左×{leftFactor}→Δ{leftShift}px，右×{rightFactor}→Δ{rightShift}px；" +
                $"左 ({leftX},{leftY})→({leftEndX},{leftY})，右 ({rightX},{rightY})→({rightEndX},{rightY})");
            _logger.Info(
                $"LUT 按 Y 扩展（{polarity}）：Y={yPx} LFactor={leftFactor} RFactor={rightFactor} " +
                $"left {leftX}→{leftEndX} right {rightX}→{rightEndX} ROI=[{roiMinX},{roiMaxX}]");

            await DragLutEndpointHorizontallyAsync(
                    leftX,
                    leftY,
                    leftEndX,
                    cancellationToken,
                    progress,
                    "左下")
                .ConfigureAwait(false);

            await DragLutEndpointHorizontallyAsync(
                    rightX,
                    rightY,
                    rightEndX,
                    cancellationToken,
                    progress,
                    "右上")
                .ConfigureAwait(false);

            var midAfter = (leftEndX + rightEndX) / 2;
            progress?.Report(
                $"LUT 按 Y 扩展完成（{polarity}）：终态中点X={midAfter}（红线目标={targetX}，" +
                $"因左右非对称扩展中点可偏离约 {(rightShift - leftShift) / 2.0:F1}px）");
        }

        /// <summary>
        /// 将单个 LUT 控制点水平拖到目标 X（保持 Y 不变）。
        /// </summary>
        private async Task DragLutEndpointHorizontallyAsync(
            int startX,
            int startY,
            int endX,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string pointLabel)
        {
            var sx = (short)Math.Clamp(startX, short.MinValue, short.MaxValue);
            var ex = (short)Math.Clamp(endX, short.MinValue, short.MaxValue);
            var y = (short)Math.Clamp(startY, short.MinValue, short.MaxValue);

            if (Math.Abs(ex - sx) < 2)
            {
                progress?.Report($"LUT {pointLabel}端点已接近目标（ΔX={ex - sx}），跳过拖拽");
                return;
            }

            var path = HistogramGrayScaleAnalyzer.BuildHorizontalDragPath(sx, y, ex);
            progress?.Report($"拖拽 LUT {pointLabel}端点：({sx},{y}) → ({ex},{y})，路径点 {path.Count}");
            _logger.Info($"拖拽 LUT {pointLabel}端点：({sx},{y}) → ({ex},{y})");

            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(() =>
                {
                    MouseSimulationHelper.MouseDragPressMoveRelease(
                        _simulator,
                        MouseButton.Button1,
                        path,
                        stepDelayMs: 4);
                }).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }
        }

        /// <summary>
        /// 单步测试：对齐后捕捉无名按钮（UIA 索引/Invoke，失败则绝对坐标）→ 按箭头标定拖拽。
        /// </summary>
        public async Task TestPostAlignCaptureDragAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            PrepareInputBlockForRun();
            try
            {
                if (!settings.HasPostAlignCaptureDragPrerequisites)
                {
                    throw new InvalidOperationException(
                        "尚未标定「对齐后捕捉」按钮坐标与拖拽箭头，请先在对应 Expander 中完成标定");
                }

                if (string.IsNullOrWhiteSpace(settings.VGAppPath))
                {
                    throw new InvalidOperationException("请先配置 VG 应用路径");
                }

                progress?.Report(
                    "对齐后捕捉拖拽测试：请确保 VG 已打开且 3-2-1 已完成（或处于可执行拖拽的视图状态）");
                _logger.Info(
                    $"对齐后捕捉拖拽测试：按钮=({settings.PostAlignCaptureButtonX},{settings.PostAlignCaptureButtonY}) " +
                    $"index={settings.PostAlignCaptureButtonIndex} " +
                    $"drag ({settings.PostAlignDragStartX},{settings.PostAlignDragStartY})→" +
                    $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})");

                var (ctViewerProcess, _) = await GetCTViewerProcess(settings, cancellationToken).ConfigureAwait(false);
                _activeVgProcess = ctViewerProcess;
                EnsureVgForeground(ctViewerProcess);
                var mainWindow = GetFreshMainWindow(ctViewerProcess);

                await RunPostAlignCaptureDragAsync(
                        settings,
                        mainWindow,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);

                progress?.Report("对齐后捕捉拖拽测试完成");
                _logger.Info("对齐后捕捉拖拽测试完成");
            }
            finally
            {
                ReleaseInputBlock();
                _activeVgProcess = null;
            }
        }

        /// <summary>
        /// 点击捕捉按钮 → 等待 → 沿标定箭头直线拖拽。
        /// </summary>
        private async Task RunPostAlignCaptureDragAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            await ClickPostAlignCaptureButtonAsync(settings, mainWindow, cancellationToken, progress)
                .ConfigureAwait(false);

            var waitMs = Math.Max(0, settings.PostAlignClickWaitMs);
            if (waitMs > 0)
            {
                progress?.Report($"等待 {waitMs}ms 后开始拖拽…");
                await Task.Delay(waitMs, cancellationToken).ConfigureAwait(false);
            }

            await DragPostAlignArrowAsync(settings, cancellationToken, progress).ConfigureAwait(false);
        }

        private async Task ClickPostAlignCaptureButtonAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var button = FindPostAlignCaptureButton(mainWindow, settings);
            if (button != null)
            {
                var name = button.Current.Name ?? string.Empty;
                var rect = button.Current.BoundingRectangle;
                progress?.Report(
                    $"方案一：定位无名按钮（Name=「{name}」）Rect={rect}，尝试 Invoke…");
                _logger.Info($"对齐后捕捉：UIA 按钮 Rect={rect}");

                if (UIAutomationHelper.TryInvokeElement(button))
                {
                    progress?.Report("捕捉按钮已通过 InvokePattern 触发");
                    _logger.Info("对齐后捕捉：Invoke 成功");
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                    return;
                }

                progress?.Report("Invoke 失败，Win32 点击按钮中心");
                await NativeClickElementAsync(button, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!settings.HasPostAlignCaptureButton)
            {
                throw new InvalidOperationException(
                    "未在 VG 主窗内定位到无名捕捉按钮，且尚未标定绝对坐标");
            }

            progress?.Report(
                $"UIA 未找到按钮，使用标定绝对坐标 ({settings.PostAlignCaptureButtonX},{settings.PostAlignCaptureButtonY})");
            _logger.Warn("对齐后捕捉：UIA 未命中，回退绝对坐标");
            await NativeClickScreenAsync(
                    settings,
                    settings.PostAlignCaptureButtonX,
                    settings.PostAlignCaptureButtonY,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 在主窗子树中收集视口底部工具栏无名小按钮，按 Left 排序；优先与标定坐标最近的矩形，否则按索引。
        /// </summary>
        private static AutomationElement? FindPostAlignCaptureButton(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings)
        {
            var candidates = CollectEmptyNameViewportToolbarButtons(mainWindow);
            if (candidates.Count == 0)
            {
                return null;
            }

            candidates.Sort((a, b) =>
            {
                var ra = a.Current.BoundingRectangle;
                var rb = b.Current.BoundingRectangle;
                var cmp = ra.Left.CompareTo(rb.Left);
                return cmp != 0 ? cmp : ra.Top.CompareTo(rb.Top);
            });

            if (settings.HasPostAlignCaptureButton)
            {
                const double maxCenterDistancePx = 28;
                AutomationElement? nearest = null;
                var nearestDist = double.MaxValue;
                foreach (var btn in candidates)
                {
                    var r = btn.Current.BoundingRectangle;
                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    var dx = cx - settings.PostAlignCaptureButtonX;
                    var dy = cy - settings.PostAlignCaptureButtonY;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = btn;
                    }
                }

                if (nearest != null && nearestDist <= maxCenterDistancePx)
                {
                    return nearest;
                }
            }

            var index = Math.Clamp(settings.PostAlignCaptureButtonIndex, 0, candidates.Count - 1);
            return candidates[index];
        }

        private static List<AutomationElement> CollectEmptyNameViewportToolbarButtons(AutomationElement mainWindow)
        {
            var result = new List<AutomationElement>();
            System.Windows.Rect mainRect;
            try
            {
                mainRect = mainWindow.Current.BoundingRectangle;
            }
            catch (ElementNotAvailableException)
            {
                return result;
            }

            if (mainRect.Width <= 0 || mainRect.Height <= 0)
            {
                return result;
            }

            var minCenterY = mainRect.Top + mainRect.Height * 0.65;
            var minCenterX = mainRect.Left + mainRect.Width * 0.35;

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

            AutomationElementCollection elements;
            try
            {
                elements = mainWindow.FindAll(TreeScope.Descendants, condition);
            }
            catch (ElementNotAvailableException)
            {
                return result;
            }

            foreach (AutomationElement element in elements)
            {
                try
                {
                    var name = element.Current.Name ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (!element.Current.IsEnabled)
                    {
                        continue;
                    }

                    var r = element.Current.BoundingRectangle;
                    if (r.Width is < 8 or > 72 || r.Height is < 8 or > 72)
                    {
                        continue;
                    }

                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    if (cy < minCenterY || cx < minCenterX)
                    {
                        continue;
                    }

                    result.Add(element);
                }
                catch (ElementNotAvailableException)
                {
                    // 忽略瞬时失效节点
                }
            }

            return result;
        }

        private async Task DragPostAlignArrowAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasPostAlignDrag)
            {
                throw new InvalidOperationException("拖拽箭头尚未标定或起点终点重合");
            }

            var path = MouseSimulationHelper.BuildLinearDragPath(
                settings.PostAlignDragStartX,
                settings.PostAlignDragStartY,
                settings.PostAlignDragEndX,
                settings.PostAlignDragEndY);

            progress?.Report(
                $"沿箭头拖拽：({settings.PostAlignDragStartX},{settings.PostAlignDragStartY}) → " +
                $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})，路径点 {path.Count}");
            _logger.Info(
                $"对齐后箭头拖拽：({settings.PostAlignDragStartX},{settings.PostAlignDragStartY})→" +
                $"({settings.PostAlignDragEndX},{settings.PostAlignDragEndY})");

            Unlock();
            try
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                var stepDelay = Math.Max(1, settings.PostAlignDragStepDelayMs);
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

            progress?.Report("箭头拖拽完成");
        }

        /// <summary>
        /// 按已标定屏幕坐标截取直方图 ROI，保存原图/对比图/峰谷图，并检测红线与半坡目标。
        /// </summary>
    }
}
