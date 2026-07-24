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
        // 阶段四：简单 3-2-1 对齐
        /// <summary>
        /// 阶段四：截 3D → 本地剪影找点 → 打开对齐 →
        /// Ctrl+三点定平面 → 偏移 Edit 置 0 → 「上」视图两点定向量 → Ctrl+原点 → 完成。
        /// 注意：向量两点必须在原点之前，否则「上」标定区域画面会变。
        /// </summary>
        private async Task RunSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasVision3dRoi)
            {
                throw new InvalidOperationException("尚未标定 3D 视图 ROI，无法执行简单 3-2-1 对齐");
            }

            if (!settings.HasVisionTopViewRoi)
            {
                throw new InvalidOperationException(
                    "尚未标定「上」视图 ROI，无法执行向量两点（请先在「阶段四视觉对齐」中标定「上」视图区域）");
            }

            progress?.Report("截取第四象限 3D 视图 ROI…");
            var capturePath = CaptureVision3dRoiToFile(settings);
            progress?.Report($"3D 截图已保存：{capturePath}");
            _logger.Info($"3D ROI 截图：{capturePath}");

            progress?.Report("本地剪影算法找点（Vision3dSectionPointsDetector，不调用 DLL）…");
            var captureDir = Path.GetDirectoryName(capturePath)!;
            var detectResult = Vision3dSectionPointsDetector.Detect(capturePath, debugDir: captureDir);
            var relativePoints = detectResult.Points;
            if (relativePoints.Length < 4)
            {
                throw new InvalidOperationException(
                    $"本地找点返回点数不足：期望 4，实际 {relativePoints.Length}");
            }

            progress?.Report($"本地找点：{detectResult.Summary}");
            _logger.Info($"本地找点：{detectResult.Summary}");

            // 默认顺序：左上、右上、右下（上盖三点）+ 原点；原点须在向量两点之后点击
            var planeScreenPoints = new (int X, int Y, string Label)[3];
            var planeAnnotatePoints = new (int X, int Y, string Label)[3];
            var planeLabels = new[] { "左上", "右上", "右下" };
            for (var i = 0; i < 3; i++)
            {
                var (sx, sy) = CTAutoSlicerSettings.ToScreenPoint(
                    settings.Vision3dRoiX,
                    settings.Vision3dRoiY,
                    relativePoints[i].X,
                    relativePoints[i].Y);
                var label = planeLabels[i];
                planeScreenPoints[i] = (sx, sy, label);
                planeAnnotatePoints[i] = (relativePoints[i].X, relativePoints[i].Y, label);
                progress?.Report(
                    $"{label}：相对=({relativePoints[i].X},{relativePoints[i].Y}) → 屏幕=({sx},{sy})");
                _logger.Info(
                    $"{label}：相对=({relativePoints[i].X},{relativePoints[i].Y}) → 屏幕=({sx},{sy})");
            }

            var (originSx, originSy) = CTAutoSlicerSettings.ToScreenPoint(
                settings.Vision3dRoiX,
                settings.Vision3dRoiY,
                relativePoints[3].X,
                relativePoints[3].Y);
            progress?.Report(
                $"原点：相对=({relativePoints[3].X},{relativePoints[3].Y}) → 屏幕=({originSx},{originSy})（稍后点击）");
            _logger.Info(
                $"原点：相对=({relativePoints[3].X},{relativePoints[3].Y}) → 屏幕=({originSx},{originSy})");

            try
            {
                var annotatedPath = Path.Combine(
                    captureDir,
                    Path.GetFileNameWithoutExtension(capturePath) + "_click_points.png");
                var annotateAll = new (int X, int Y, string Label)[4];
                for (var i = 0; i < 3; i++)
                {
                    annotateAll[i] = planeAnnotatePoints[i];
                }

                annotateAll[3] = (relativePoints[3].X, relativePoints[3].Y, "原点");
                ScreenCaptureHelper.SaveAnnotatedPointsImage(capturePath, annotatedPath, annotateAll);
                progress?.Report($"四点点击标注图已保存：{annotatedPath}（另有检测器 _points/_mask）");
                _logger.Info($"3D 四点点击标注图：{annotatedPath}");
            }
            catch (Exception annotateEx)
            {
                progress?.Report($"四点标注图保存失败（继续流程）：{annotateEx.Message}");
                _logger.Warn($"3D 四点标注图保存失败：{annotateEx.Message}");
            }

            progress?.Report("打开「简单 3-2-1 对齐」…");
            await OpenSimple321AlignmentAsync(
                    settings,
                    ctViewerProcess,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            // —— 步骤1：三点定平面（左上→右上→右下）——
            progress?.Report("步骤1/3：Ctrl+左键依次点击上盖三点（左上→右上→右下）…");
            foreach (var point in planeScreenPoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Ctrl+点击 {point.Label} @({point.X},{point.Y})");
                await CtrlClickScreenAsync(point.X, point.Y, cancellationToken).ConfigureAwait(false);
                var interval = Math.Max(100, settings.VisionClickIntervalMs);
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            // —— 步骤1.5：左侧无名偏移 Edit 置 0 + Enter（否则步骤2「上」视图非预期）——
            await ResetSimple321PlaneOffsetEditAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            // —— 步骤2：「上」视图右侧外缘两点（必须在原点之前）——
            // 三点+置0后短等，让 VG 刷新「上」标定区高亮后再截图
            await Task.Delay(Math.Max(200, settings.VisionClickIntervalMs), cancellationToken)
                .ConfigureAwait(false);
            progress?.Report("步骤2/3：「上」视图向量两点（须先于原点，否则标定区会变）…");
            await ClickTopViewVectorPointsAsync(settings, cancellationToken, progress)
                .ConfigureAwait(false);

            // —— 步骤3：3D 原点 ——
            progress?.Report($"步骤3/3：Ctrl+点击原点 @({originSx},{originSy})…");
            await CtrlClickScreenAsync(originSx, originSy, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(100, settings.VisionClickIntervalMs), cancellationToken)
                .ConfigureAwait(false);

            progress?.Report("点击「完成(F)」…");
            await ClickSimple321FinishAsync(settings, mainWindow, cancellationToken, progress)
                .ConfigureAwait(false);

            await TryDismissAlignedContinueWarningAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("简单 3-2-1 对齐流程完成（3 平面点 + 偏移置0 + 2 向量点 + 1 原点）");
        }

        /// <summary>
        /// 三点定平面后：点击左侧无名偏移 Edit → 输入 0 → Enter。
        /// 优先 UIA 定位（空 Name / 值含 mm，靠近标定点）；失败则绝对坐标点击 + 键盘。
        /// </summary>
        private async Task ResetSimple321PlaneOffsetEditAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasSimple321OffsetEdit)
            {
                throw new InvalidOperationException(
                    "尚未标定三点后偏移 Edit，请先在「阶段四视觉对齐」中标定「偏移置0」编辑框");
            }

            progress?.Report(
                $"步骤1.5：偏移 Edit 置 0 @({settings.Simple321OffsetEditX},{settings.Simple321OffsetEditY})…");
            _logger.Info(
                $"简单 3-2-1：偏移 Edit 置 0 ({settings.Simple321OffsetEditX},{settings.Simple321OffsetEditY})");

            var edit = FindSimple321OffsetEdit(mainWindow, settings);
            Unlock();
            try
            {
                if (edit != null)
                {
                    var rect = edit.Current.BoundingRectangle;
                    progress?.Report($"已 UIA 定位偏移 Edit Rect={rect}，点击并写入 0");
                    _logger.Info($"偏移 Edit UIA Rect={rect}");
                    await NativeClickElementWhileUnlockedAsync(edit, cancellationToken).ConfigureAwait(false);
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);

                    if (!UIAutomationHelper.TrySetEditValueSafe(edit, "0"))
                    {
                        await TypeTextIntoFocusedControlAsync("0").ConfigureAwait(false);
                    }
                }
                else
                {
                    progress?.Report("UIA 未找到偏移 Edit，使用标定绝对坐标点击后键盘输入 0");
                    _logger.Warn("偏移 Edit UIA 未命中，回退绝对坐标");
                    await InvokeOnUiThreadAsync(() =>
                            WinAPIHelper.ClickScreen(
                                settings.Simple321OffsetEditX,
                                settings.Simple321OffsetEditY))
                        .ConfigureAwait(false);
                    await Task.Delay(80, cancellationToken).ConfigureAwait(false);
                    await TypeTextIntoFocusedControlAsync("0").ConfigureAwait(false);
                }

                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                await InvokeOnUiThreadAsync(SimulateEnter).ConfigureAwait(false);
                await Task.Delay(Math.Max(150, settings.VisionClickIntervalMs), cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            progress?.Report("偏移 Edit 已置 0 并回车");
            _logger.Info("简单 3-2-1：偏移 Edit 置 0 + Enter 完成");
        }

        /// <summary>
        /// 在主窗左侧寻找无名 Edit（常显示「x.xx mm」）；优先与标定点中心距最近。
        /// </summary>
        private static AutomationElement? FindSimple321OffsetEdit(
            AutomationElement mainWindow,
            CTAutoSlicerSettings settings)
        {
            System.Windows.Rect mainRect;
            try
            {
                mainRect = mainWindow.Current.BoundingRectangle;
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            if (mainRect.Width <= 0 || mainRect.Height <= 0)
            {
                return null;
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

            AutomationElementCollection elements;
            try
            {
                elements = mainWindow.FindAll(TreeScope.Descendants, condition);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            AutomationElement? nearest = null;
            var nearestDist = double.MaxValue;
            const double maxCenterDistancePx = 40;

            foreach (AutomationElement element in elements)
            {
                try
                {
                    if (!element.Current.IsEnabled || !element.Current.IsKeyboardFocusable)
                    {
                        continue;
                    }

                    var name = element.Current.Name ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    var r = element.Current.BoundingRectangle;
                    // Insights 约 95×19；过滤过大/过小
                    if (r.Width is < 40 or > 220 || r.Height is < 12 or > 40)
                    {
                        continue;
                    }

                    // 偏主窗左侧
                    var cx = r.X + r.Width / 2;
                    var cy = r.Y + r.Height / 2;
                    if (cx > mainRect.Left + mainRect.Width * 0.35)
                    {
                        continue;
                    }

                    var value = UIAutomationHelper.TryGetInputValue(element) ?? string.Empty;
                    var looksLikeMm = value.Contains("mm", StringComparison.OrdinalIgnoreCase)
                        || value.Contains('.', StringComparison.Ordinal)
                        || string.IsNullOrWhiteSpace(value);

                    if (!looksLikeMm)
                    {
                        continue;
                    }

                    var dx = cx - settings.Simple321OffsetEditX;
                    var dy = cy - settings.Simple321OffsetEditY;
                    var dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = element;
                    }
                }
                catch (ElementNotAvailableException)
                {
                }
            }

            if (nearest != null && nearestDist <= maxCenterDistancePx)
            {
                return nearest;
            }

            return null;
        }

        /// <summary>
        /// 简单 3-2-1 步骤2：截取「上」视图 → 本地右缘两点 → 标注落盘 → Ctrl+从上到下点击。
        /// </summary>
        private async Task ClickTopViewVectorPointsAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report("截取第二象限「上」视图 ROI…");
            var capturePath = CaptureVisionTopViewRoiToFile(settings);
            progress?.Report($"「上」视图截图已保存：{capturePath}");
            _logger.Info($"「上」视图 ROI 截图：{capturePath}");

            progress?.Report("本地算法找右侧外缘向量两点（VisionTopViewVectorPointsDetector）…");
            var captureDir = Path.GetDirectoryName(capturePath)!;
            var detectResult = VisionTopViewVectorPointsDetector.Detect(capturePath, debugDir: captureDir);
            var relativePoints = detectResult.Points;
            if (relativePoints.Length < 2)
            {
                throw new InvalidOperationException(
                    $"向量找点返回点数不足：期望 2，实际 {relativePoints.Length}");
            }

            // 强制从上到下
            var top = relativePoints[0];
            var bottom = relativePoints[1];
            if (bottom.Y < top.Y)
            {
                (top, bottom) = (bottom, top);
            }

            progress?.Report($"向量找点：{detectResult.Summary}");
            _logger.Info($"向量找点：{detectResult.Summary}");

            var screenPoints = new (int X, int Y, string Label)[2];
            var annotatePoints = new (int X, int Y, string Label)[2];
            var labels = new[] { "向量上", "向量下" };
            var rel = new[] { top, bottom };
            for (var i = 0; i < 2; i++)
            {
                var (sx, sy) = CTAutoSlicerSettings.ToScreenPoint(
                    settings.VisionTopViewRoiX,
                    settings.VisionTopViewRoiY,
                    rel[i].X,
                    rel[i].Y);
                screenPoints[i] = (sx, sy, labels[i]);
                annotatePoints[i] = (rel[i].X, rel[i].Y, labels[i]);
                progress?.Report(
                    $"{labels[i]}：相对=({rel[i].X},{rel[i].Y}) → 屏幕=({sx},{sy})");
                _logger.Info(
                    $"{labels[i]}：相对=({rel[i].X},{rel[i].Y}) → 屏幕=({sx},{sy})");
            }

            try
            {
                var annotatedPath = Path.Combine(
                    captureDir,
                    Path.GetFileNameWithoutExtension(capturePath) + "_click_points.png");
                ScreenCaptureHelper.SaveAnnotatedPointsImage(capturePath, annotatedPath, annotatePoints);
                progress?.Report($"向量两点标注图已保存：{annotatedPath}");
                _logger.Info($"「上」视图向量标注图：{annotatedPath}");
            }
            catch (Exception annotateEx)
            {
                progress?.Report($"向量标注图保存失败（继续流程）：{annotateEx.Message}");
                _logger.Warn($"「上」视图向量标注图保存失败：{annotateEx.Message}");
            }

            progress?.Report("Ctrl+左键从上到下点击向量两点…");
            foreach (var point in screenPoints)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Ctrl+点击 {point.Label} @({point.X},{point.Y})");
                await CtrlClickScreenAsync(point.X, point.Y, cancellationToken).ConfigureAwait(false);
                var interval = Math.Max(100, settings.VisionClickIntervalMs);
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
        }

        private string CaptureVision3dRoiToFile(CTAutoSlicerSettings settings)
        {
            var dayDir = settings.EnsureVisionCaptureDayDir();
            var cleanup = settings.CleanupExpiredVisionCaptures();
            if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
            {
                _logger.Info(
                    $"视觉截图清理：删除目录 {cleanup.DeletedDirs}、文件 {cleanup.DeletedFiles}" +
                    $"（保留 {settings.VisionCaptureKeepDays} 天）");
            }

            var filePath = Path.Combine(dayDir, $"vision_3d_{DateTime.Now:HHmmss_fff}.png");
            ScreenCaptureHelper.CaptureRegionAndSave(
                filePath,
                settings.Vision3dRoiX,
                settings.Vision3dRoiY,
                settings.Vision3dRoiWidth,
                settings.Vision3dRoiHeight);
            return filePath;
        }

        private string CaptureVisionTopViewRoiToFile(CTAutoSlicerSettings settings)
        {
            var dayDir = settings.EnsureVision2dVectorCaptureDayDir();
            var cleanup = settings.CleanupExpiredVision2dVectorCaptures();
            if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
            {
                _logger.Info(
                    $"向量截图清理：删除目录 {cleanup.DeletedDirs}、文件 {cleanup.DeletedFiles}" +
                    $"（保留 {settings.Vision2dVectorCaptureKeepDays} 天）");
            }

            var filePath = Path.Combine(dayDir, $"vision_top_{DateTime.Now:HHmmss_fff}.png");
            ScreenCaptureHelper.CaptureRegionAndSave(
                filePath,
                settings.VisionTopViewRoiX,
                settings.VisionTopViewRoiY,
                settings.VisionTopViewRoiWidth,
                settings.VisionTopViewRoiHeight);
            return filePath;
        }

        /// <summary>
        /// 按标定绝对坐标点击测量工具栏「简单对齐」下拉 → 选菜单项 → 等待对话框。
        /// 下拉控件为 Qt，UIA 常无法识别，故第一步必须用屏幕坐标点击。
        /// </summary>
        private async Task OpenSimple321AlignmentAsync(
            CTAutoSlicerSettings settings,
            Process ctViewerProcess,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.HasSimple321ToolbarClick)
            {
                throw new InvalidOperationException(
                    "尚未标定「简单对齐」下拉点击位置，请先在「阶段四视觉对齐」中标定下拉框");
            }

            var clickX = settings.Simple321ToolbarClickX;
            var clickY = settings.Simple321ToolbarClickY;
            progress?.Report($"按标定坐标点击「简单对齐」下拉 @({clickX},{clickY})");
            _logger.Info($"简单 3-2-1：绝对定位点击下拉 ({clickX},{clickY})");
            await NativeClickScreenAsync(settings, clickX, clickY, cancellationToken).ConfigureAwait(false);
            await Task.Delay(Math.Max(100, settings.VisionClickIntervalMs), cancellationToken).ConfigureAwait(false);

            var menuItem = await UIAutomationHelper.WaitForElementAsync(
                    () => FindSimple321MenuItem(mainWindow, ctViewerProcess),
                    TimeSpan.FromMilliseconds(settings.VisionAlignmentDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (menuItem == null)
            {
                throw new InvalidOperationException(
                    $"未在 {settings.VisionAlignmentDialogWaitTime}ms 内出现菜单项「{Simple321MenuItemName}」" +
                    "（请确认下拉标定坐标准确，且点击后菜单已展开）");
            }

            progress?.Report($"选择菜单「{menuItem.Current.Name}」");
            if (!UIAutomationHelper.TryInvokeElement(menuItem))
            {
                await NativeClickElementAsync(menuItem, cancellationToken).ConfigureAwait(false);
            }

            var dialog = await UIAutomationHelper.WaitForElementAsync(
                    () => FindWindowByNameScoped(mainWindow, Simple321DialogNamePrefix, nameStartsWith: true),
                    TimeSpan.FromMilliseconds(settings.VisionAlignmentDialogWaitTime),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (dialog == null)
            {
                throw new InvalidOperationException(
                    $"未在 {settings.VisionAlignmentDialogWaitTime}ms 内出现「{Simple321DialogNamePrefix}...」对话框");
            }

            progress?.Report("已打开「简单 3-2-1 对齐」对话框");
            _logger.Info("简单 3-2-1 对齐对话框已打开");

            // 标点前先重置，清掉上次残留对齐状态，避免异常
            await ClickSimple321ResetAsync(dialog, cancellationToken, progress).ConfigureAwait(false);
        }

        /// <summary>
        /// 在「简单 3-2-1 对齐」对话框内点击「重置(R)」（Insights AccessKey Alt+R）。
        /// </summary>
        private async Task ClickSimple321ResetAsync(
            AutomationElement alignmentDialog,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            progress?.Report($"点击「{Simple321ResetButtonName}」…");
            _logger.Info($"简单 3-2-1：点击「{Simple321ResetButtonName}」后再标点");

            var resetButton = UIAutomationHelper.FindButtonByName(alignmentDialog, Simple321ResetButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    alignmentDialog,
                    ControlType.Button,
                    "重置",
                    nameContains: true)
                ?? throw new InvalidOperationException(
                    $"未找到按钮「{Simple321ResetButtonName}」，请确认「简单 3-2-1 对齐」对话框已完全加载");

            if (!UIAutomationHelper.TryInvokeButton(resetButton))
            {
                await NativeClickElementAsync(resetButton, cancellationToken).ConfigureAwait(false);
            }

            await Task.Delay(Math.Max(150, UiPollIntervalMs), cancellationToken).ConfigureAwait(false);
            progress?.Report($"「{Simple321ResetButtonName}」已点击，开始标点");
        }

        private static AutomationElement? FindSimple321MenuItem(
            AutomationElement mainWindow,
            Process ctViewerProcess)
        {
            var inMain = UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.MenuItem,
                    Simple321MenuItemName)
                ?? UIAutomationHelper.FindDescendantByName(
                    mainWindow,
                    ControlType.MenuItem,
                    Simple321MenuItemNameContains,
                    nameContains: true);
            if (inMain != null)
            {
                return inMain;
            }

            try
            {
                var processId = ctViewerProcess.Id;
                foreach (var topWindow in EnumerateSameProcessTopLevelWindows(processId))
                {
                    var item = UIAutomationHelper.FindDescendantByName(
                            topWindow,
                            ControlType.MenuItem,
                            Simple321MenuItemName)
                        ?? UIAutomationHelper.FindDescendantByName(
                            topWindow,
                            ControlType.MenuItem,
                            Simple321MenuItemNameContains,
                            nameContains: true);
                    if (item != null)
                    {
                        return item;
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }

            return null;
        }

        private async Task ClickSimple321FinishAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var dialog = FindWindowByNameScoped(mainWindow, Simple321DialogNamePrefix, nameStartsWith: true)
                ?? throw new InvalidOperationException("未找到「简单 3-2-1 对齐」对话框，无法点击完成");

            var finishButton = UIAutomationHelper.FindButtonByName(dialog, Simple321FinishButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    dialog,
                    ControlType.Button,
                    "完成",
                    nameContains: true);

            if (finishButton == null)
            {
                throw new InvalidOperationException($"未找到按钮「{Simple321FinishButtonName}」");
            }

            if (!UIAutomationHelper.TryInvokeButton(finishButton))
            {
                await NativeClickElementAsync(finishButton, cancellationToken).ConfigureAwait(false);
            }

            // 「完成」后常立刻弹出「VGSTUDIO MAX 警告」；若不先点「是」，对齐对话框不会关闭，会卡在消失等待。
            await WaitSimple321DialogClosedHandlingWarningAsync(
                    settings,
                    mainWindow,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);

            progress?.Report("「完成」已点击，对齐对话框已关闭");
        }

        /// <summary>
        /// 等待「简单 3-2-1 对齐」关闭；期间若出现「VGSTUDIO MAX 警告」则点「是(Y)」。
        /// </summary>
        private async Task WaitSimple321DialogClosedHandlingWarningAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var timeout = TimeSpan.FromMilliseconds(Math.Max(settings.VisionAlignmentDialogWaitTime, 8000));
            var deadline = DateTime.UtcNow + timeout;
            var warningHandled = false;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var warning = FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName);
                if (warning != null)
                {
                    progress?.Report(
                        $"检测到「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
                    _logger.Info($"简单 3-2-1：对齐完成确认警告，点击「{VgAlignedWarningYesButtonName}」");
                    await ClickAlignedWarningYesAsync(warning, cancellationToken).ConfigureAwait(false);
                    warningHandled = true;
                    await Task.Delay(UiPollIntervalMs, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var alignmentDialog = FindWindowByNameScoped(
                    mainWindow,
                    Simple321DialogNamePrefix,
                    nameStartsWith: true);
                if (alignmentDialog == null)
                {
                    if (!warningHandled)
                    {
                        // 对齐窗已关、警告未出现：再短等一下，避免漏掉晚弹出的警告
                        var lateWarning = await UIAutomationHelper.WaitForElementAsync(
                                () => FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName),
                                TimeSpan.FromMilliseconds(800),
                                cancellationToken,
                                UiPollIntervalMs)
                            .ConfigureAwait(false);
                        if (lateWarning != null)
                        {
                            progress?.Report(
                                $"对齐窗关闭后出现「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
                            await ClickAlignedWarningYesAsync(lateWarning, cancellationToken)
                                .ConfigureAwait(false);
                        }
                    }

                    return;
                }

                progress?.Report("等待「简单 3-2-1 对齐」对话框关闭（若有警告将先点「是」）...");
                await Task.Delay(UiPollIntervalMs, cancellationToken).ConfigureAwait(false);
            }

            throw new TimeoutException(
                $"等待「简单 3-2-1 对齐」关闭超时（{timeout.TotalMinutes:0.#} 分钟）。" +
                $"若一直停在「{VgAlignedWarningDialogName}」，请确认「{VgAlignedWarningYesButtonName}」可被识别。");
        }

        private async Task ClickAlignedWarningYesAsync(
            AutomationElement warningDialog,
            CancellationToken cancellationToken)
        {
            var yesButton = UIAutomationHelper.FindButtonByName(warningDialog, VgAlignedWarningYesButtonName)
                ?? UIAutomationHelper.FindDescendantByName(
                    warningDialog,
                    ControlType.Button,
                    "是",
                    nameContains: true)
                ?? throw new InvalidOperationException(
                    $"已出现「{VgAlignedWarningDialogName}」，但未找到按钮「{VgAlignedWarningYesButtonName}」");

            if (!UIAutomationHelper.TryInvokeButton(yesButton))
            {
                await NativeClickElementAsync(yesButton, cancellationToken).ConfigureAwait(false);
            }

            // 给模态框一点关闭时间；外层循环会继续轮询
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 「完成」后可选弹窗兜底（对齐窗已关、警告稍晚出现时）。
        /// </summary>
        private async Task TryDismissAlignedContinueWarningAsync(
            CTAutoSlicerSettings settings,
            AutomationElement mainWindow,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            var waitMs = Math.Clamp(settings.VisionAlignmentDialogWaitTime, 800, 2000);
            progress?.Report($"再次确认是否仍有「{VgAlignedWarningDialogName}」（最多 {waitMs}ms）…");

            var warning = await UIAutomationHelper.WaitForElementAsync(
                    () => FindWindowByNameScoped(mainWindow, VgAlignedWarningDialogName),
                    TimeSpan.FromMilliseconds(waitMs),
                    cancellationToken,
                    UiPollIntervalMs)
                .ConfigureAwait(false);

            if (warning == null)
            {
                progress?.Report($"无残留「{VgAlignedWarningDialogName}」");
                return;
            }

            progress?.Report($"仍有「{VgAlignedWarningDialogName}」，点击「{VgAlignedWarningYesButtonName}」…");
            await ClickAlignedWarningYesAsync(warning, cancellationToken).ConfigureAwait(false);
            progress?.Report($"已点击「{VgAlignedWarningYesButtonName}」");
            _logger.Info($"简单 3-2-1：兜底关闭警告窗「{VgAlignedWarningDialogName}」");
        }

        /// <summary>按住 Ctrl 左键点击屏幕坐标（3-2-1 选点）。</summary>
        private async Task CtrlClickScreenAsync(int x, int y, CancellationToken cancellationToken)
        {
            Unlock();
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    WinAPIHelper.SetCursorPos(x, y);
                    Thread.Sleep(60);
                    _simulator.SimulateKeyPress(KeyCode.VcLeftControl);
                    Thread.Sleep(40);
                    WinAPIHelper.LeftClickAt(x, y);
                    Thread.Sleep(40);
                    _simulator.SimulateKeyRelease(KeyCode.VcLeftControl);
                }).ConfigureAwait(false);
            }
            finally
            {
                Lock();
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 主流程/单测共用：截图 → 峰谷半坡 → 拖红线。未标定 ROI 时跳过（不中断导入）。
        /// </summary>
    }
}
