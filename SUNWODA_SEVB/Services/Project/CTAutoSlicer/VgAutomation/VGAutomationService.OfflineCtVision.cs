using System.IO;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public partial class VGAutomationService
    {
        /// <summary>
        /// AI 标注模式批处理：扫描交接目录图像，逐张推理并落盘到标注结果根目录；
        /// 成功后按原结构备份到「标注结果根目录」上级的「交接备份」，并删除交接源（备份保留 30 天）。
        /// </summary>
        public async Task RunAiAnnotationBatchAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            Lock();
            try
            {
                if (string.IsNullOrWhiteSpace(settings.TopViewHandoffFolder)
                    || !Directory.Exists(settings.TopViewHandoffFolder))
                {
                    throw new InvalidOperationException("请先配置有效的上视图交接目录（TopViewHandoffFolder）");
                }

                if (string.IsNullOrWhiteSpace(settings.SliceExportFolder))
                {
                    throw new InvalidOperationException("请先配置标注结果根目录（复用 SliceExportFolder）");
                }

                var images = OfflineCtAnnotationHelper.EnumerateHandoffImages(settings.TopViewHandoffFolder);
                if (images.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"交接目录内未找到可标注图像（jpg/png 等）：{settings.TopViewHandoffFolder}");
                }

                Directory.CreateDirectory(settings.SliceExportFolder);
                progress?.Report($"AI 标注批处理开始：共 {images.Count} 张 → {settings.TopViewHandoffFolder}");
                _logger.Info($"AI 标注批处理开始：count={images.Count}，handoff={settings.TopViewHandoffFolder}");

                // 批处理开始时清理超过一个月的交接备份（锚点=标注结果根目录）
                try
                {
                    var backupRoot = OfflineCtAnnotationHelper.BuildHandoffBackupRoot(
                        settings.SliceExportFolder);
                    var cleanup = OfflineCtAnnotationHelper.CleanupExpiredHandoffBackups(
                        settings.SliceExportFolder);
                    if (cleanup.DeletedDirs > 0 || cleanup.DeletedFiles > 0)
                    {
                        progress?.Report(
                            $"交接备份清理：删除过期目录 {cleanup.DeletedDirs}、文件 {cleanup.DeletedFiles}"
                            + $"（保留 {OfflineCtAnnotationHelper.DefaultHandoffBackupKeepDays} 天，根={backupRoot}）");
                        _logger.Info(
                            $"交接备份清理：dirs={cleanup.DeletedDirs}, files={cleanup.DeletedFiles}，root={backupRoot}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"交接备份过期清理跳过：{ex.Message}");
                }

                var ok = 0;
                var fail = 0;
                var backupFail = 0;
                for (var i = 0; i < images.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var imagePath = images[i];
                    var outputFolder = OfflineCtAnnotationHelper.BuildBatchAnnotationFolder(
                        settings.SliceExportFolder,
                        imagePath);
                    progress?.Report($"[{i + 1}/{images.Count}] 标注：{Path.GetFileName(imagePath)}");
                    try
                    {
                        await RunOfflineCtVisionAnnotationAsync(
                                settings,
                                vglPath: null,
                                exportBaseName: null,
                                quantity: null,
                                cancellationToken,
                                progress,
                                explicitImagePath: imagePath,
                                explicitOutputFolder: outputFolder)
                            .ConfigureAwait(false);
                        ok++;

                        // 标注成功：备份到「标注结果根目录」上级/交接备份，再删交接源
                        try
                        {
                            var backupPath = OfflineCtAnnotationHelper.BackupAndRemoveHandoffImage(
                                settings.TopViewHandoffFolder,
                                imagePath,
                                settings.SliceExportFolder);
                            progress?.Report(
                                $"[{i + 1}/{images.Count}] 已备份并移出交接：{Path.GetFileName(imagePath)} → {backupPath}");
                            _logger.Info($"交接备份并删除：src={imagePath} → {backupPath}");
                        }
                        catch (Exception backupEx)
                        {
                            backupFail++;
                            progress?.Report(
                                $"[{i + 1}/{images.Count}] 标注成功，但交接备份/删除失败（源仍保留）：{backupEx.Message}");
                            _logger.Error($"交接备份/删除失败：{imagePath}", backupEx);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        progress?.Report($"[{i + 1}/{images.Count}] 失败（继续下一张）：{ex.Message}");
                        _logger.Error($"AI 批处理单张失败：{imagePath}", ex);
                    }
                }

                progress?.Report(
                    $"AI 标注批处理完成：成功 {ok}，失败 {fail}，备份失败 {backupFail}，合计 {images.Count}");
                _logger.Info(
                    $"AI 标注批处理完成：ok={ok}, fail={fail}, backupFail={backupFail}, total={images.Count}");
            }
            finally
            {
                Unlock();
            }
        }

        /// <summary>
        /// 兼容保留：历史主流程软失败入口。现行切图链路已改为交接目录，不再调用。
        /// </summary>
        private async Task TryRunOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            string vglPath,
            string? exportBaseName,
            int? quantity,
            CancellationToken cancellationToken,
            IProgress<string>? progress)
        {
            if (!settings.EnableOfflineCtVisionAnnotation)
            {
                progress?.Report("跳过：AI 图像标注（开关已关闭）");
                _logger.Info("跳过 AI 图像标注：EnableOfflineCtVisionAnnotation=false");
                return;
            }

            try
            {
                await RunOfflineCtVisionAnnotationAsync(
                        settings,
                        vglPath,
                        exportBaseName,
                        quantity,
                        cancellationToken,
                        progress)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                progress?.Report($"AI 图像标注失败（已跳过，继续后续）：{ex.Message}");
                _logger.Error("AI 图像标注软失败", ex);
            }
        }

        /// <summary>
        /// 对指定导出名的「上」视图（按 OfflineCtTopViewSliceIndex）执行推理并落盘；也可直接对单张图测试。
        /// </summary>
        /// <param name="explicitOutputFolder">
        /// 仅在指定 <paramref name="explicitImagePath"/> 时生效：输出目录；空则用输入图所在目录。
        /// </param>
        public async Task RunOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            string? vglPath,
            string? exportBaseName,
            int? quantity,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string? explicitImagePath = null,
            string? explicitOutputFolder = null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string imagePath;
            string? annotationFolder = null;
            string? resolvedExportBaseName = exportBaseName;

            if (!string.IsNullOrWhiteSpace(explicitImagePath))
            {
                imagePath = explicitImagePath.Trim();
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException("指定的 AI 标注输入图不存在", imagePath);
                }

                // 独立标注：优先用指定输出目录；空则落到输入图所在文件夹
                if (!string.IsNullOrWhiteSpace(explicitOutputFolder))
                {
                    annotationFolder = explicitOutputFolder.Trim();
                }
                else
                {
                    annotationFolder = Path.GetDirectoryName(imagePath)
                        ?? throw new InvalidOperationException("无法解析输入图所在目录作为默认输出目录");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(settings.SliceExportFolder))
                {
                    throw new InvalidOperationException("请先配置切片导出目录");
                }

                if (string.IsNullOrWhiteSpace(resolvedExportBaseName))
                {
                    if (string.IsNullOrWhiteSpace(vglPath))
                    {
                        throw new InvalidOperationException("缺少导出名或 vgl 路径，无法定位「上」视图文件夹");
                    }

                    // 兼容旧单机路径：仅有导入路径时仍按前 6 字符规则生成导出名
                    resolvedExportBaseName = SliceExportNamingHelper.BuildExportBaseName(
                        Path.GetFileName(vglPath));
                }

                var sliceIndex = settings.ResolveOfflineCtTopViewSliceIndex();
                var expectedStem = SliceExportNamingHelper.BuildImageStackFileStem(sliceIndex);
                var expectedName = $"{expectedStem}.jpg";

                var topFolder = OfflineCtAnnotationHelper.BuildTopViewFolder(
                    settings.SliceExportFolder,
                    resolvedExportBaseName);
                imagePath = OfflineCtAnnotationHelper.TryPickTopViewImageByIndex(topFolder, sliceIndex)
                    ?? throw new FileNotFoundException(
                        $"未在「上」视图目录按文件名找到送检图（期望第 {sliceIndex} 张 → {expectedName} 等）：{topFolder}",
                        topFolder);

                annotationFolder = OfflineCtAnnotationHelper.BuildAnnotationFolder(
                    settings.SliceExportFolder,
                    resolvedExportBaseName);

                progress?.Report(
                    $"AI 标注选用第 {sliceIndex} 张：{Path.GetFileName(imagePath)}"
                    + $"（配置={sliceIndex} → {expectedName}，"
                    + $"像素精度 {settings.ResolveOfflineCtPixelSizeUm():G} μm/px）");
                _logger.Info($"AI 标注输入图：{imagePath}，输出目录：{annotationFolder}，sliceIndex={sliceIndex}，stem={expectedStem}");
            }

            Directory.CreateDirectory(annotationFolder!);
            progress?.Report($"AI 标注输入图（原始上视图）：{imagePath}");
            progress?.Report($"AI 标注输出目录：{annotationFolder}");
            _logger.Info($"AI 标注输入图：{imagePath}，输出目录：{annotationFolder}");

            var originalImagePath = imagePath;

            // 方案 B：竖图校验 → 内容上移 → 条件提亮；送检图仅用临时文件，推理后删除，不落盘 input/
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("AI 标注：校验竖图 → 内容上移(可选) → 预处理送检（临时文件）…");
            var preBrighten = await Task.Run(
                    () => OfflineCtPreBrightenHelper.PrepareFinalInputImage(
                        originalImagePath,
                        annotationFolder!,
                        settings.EnableOfflineCtPreBrighten,
                        settings.OfflineCtPreBrightenMeanTrigger,
                        settings.OfflineCtPreBrightenP50Trigger,
                        settings.OfflineCtPreBrightenTargetMean,
                        // 不落盘 input/：临时文件推理后删除，产品目录只保留成功结果图
                        persistFinalInput: false,
                        contentShiftUpPx: settings.ResolveOfflineCtContentShiftUpPx()),
                    cancellationToken)
                .ConfigureAwait(false);
            imagePath = preBrighten.FinalImagePath;
            progress?.Report(preBrighten.Summary);
            _logger.Info(
                $"AI 标注预处理：{preBrighten.Summary}"
                + (string.IsNullOrEmpty(preBrighten.ReportPath) ? string.Empty : $"；报告={preBrighten.ReportPath}"));

            try
            {
                var modelPath = settings.ResolveOfflineCtModelPath();
                progress?.Report($"AI 标注初始化模型：{modelPath}");
                await Task.Run(
                        () => _offlineCtVisionService.Initialize(modelPath, settings.OfflineCtIsRunCuda),
                        cancellationToken)
                    .ConfigureAwait(false);

                var results = new List<OfflineCtInferResult>();
                var mode = settings.OfflineCtResultImageMode;
                // NJCT 打黑固定传 0；底部信息遮罩由上位机按 OfflineCtBlackHeight
                // 在 ResultImg 底部覆盖打黑并写入 L1~L5（与二次手动标注同一配置项）。
                // 增强图用全高；原图默认减半。
                const int dllBlackHeight = 0;
                var pixelSizeUm = settings.ResolveOfflineCtPixelSizeUm();
                var bottomInfoBlackHeightEnhance = Math.Max(0, settings.OfflineCtBlackHeight);
                var bottomInfoBlackHeightOrigin =
                    CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(bottomInfoBlackHeightEnhance);
                var rangeCheck = settings.BuildOfflineCtLRangeCheckOptions();
                var bothModes = mode == OfflineCtResultImageMode.Both;
                // 结果图文件名与导入产品文件夹名一致（…/{导出名}/{导出名}.jpg），不用 result_enhance
                var resultBaseName = OfflineCtAnnotationHelper.ResolveAnnotationResultBaseName(
                    annotationFolder!,
                    originalImagePath,
                    resolvedExportBaseName);

                if (mode is OfflineCtResultImageMode.Enhance or OfflineCtResultImageMode.Both)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var enhancePath = OfflineCtAnnotationHelper.BuildAnnotationResultImagePath(
                        annotationFolder!,
                        resultBaseName,
                        "enhance",
                        bothModes);
                    progress?.Report(
                        $"AI 标注：调用 ModelAction（增强图+抓点/尺寸+底部遮罩 {bottomInfoBlackHeightEnhance}px），输入={Path.GetFileName(imagePath)}…");
                    var enhanceResult = await Task.Run(
                            () => _offlineCtVisionService.InferEnhance(
                                imagePath,
                                dllBlackHeight,
                                enhancePath,
                                pixelSizeUm,
                                bottomInfoBlackHeightEnhance,
                                rangeCheck),
                            cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(enhanceResult);
                    progress?.Report(
                        enhanceResult.IsSuccess
                            ? $"增强标注图完成：{enhanceResult.ResultImagePath}"
                            : $"增强图完成但有 RunInfo：{enhanceResult.RunInfo}");
                    // 落盘后立刻推送预览路径，供 UI 逐张刷新（不必等整批结束）
                    if (!string.IsNullOrWhiteSpace(enhanceResult.ResultImagePath)
                        && File.Exists(enhanceResult.ResultImagePath))
                    {
                        progress?.Report($"@@AI_RESULT_PREVIEW@@{enhanceResult.ResultImagePath}");
                    }
                }

                if (mode is OfflineCtResultImageMode.Origin or OfflineCtResultImageMode.Both)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var originPath = OfflineCtAnnotationHelper.BuildAnnotationResultImagePath(
                        annotationFolder!,
                        resultBaseName,
                        "origin",
                        bothModes);
                    progress?.Report(
                        $"AI 标注：调用 ModelAction_origin（原图+抓点/尺寸+底部遮罩 {bottomInfoBlackHeightOrigin}px=增强一半），输入={Path.GetFileName(imagePath)}…");
                    var originResult = await Task.Run(
                            () => _offlineCtVisionService.InferOrigin(
                                imagePath,
                                dllBlackHeight,
                                originPath,
                                pixelSizeUm,
                                bottomInfoBlackHeightOrigin,
                                rangeCheck),
                            cancellationToken)
                        .ConfigureAwait(false);
                    results.Add(originResult);
                    progress?.Report(
                        originResult.IsSuccess
                            ? $"原图标注完成：{originResult.ResultImagePath}"
                            : $"原图完成但有 RunInfo：{originResult.RunInfo}");
                    // Both 模式：增强图已推送过；仅 Origin 模式时用原图推送预览
                    if (mode == OfflineCtResultImageMode.Origin
                        && !string.IsNullOrWhiteSpace(originResult.ResultImagePath)
                        && File.Exists(originResult.ResultImagePath))
                    {
                        progress?.Report($"@@AI_RESULT_PREVIEW@@{originResult.ResultImagePath}");
                    }
                }

                if (settings.OfflineCtExportJson)
                {
                    var jsonPath = Path.Combine(annotationFolder!, "annotation.json");
                    OfflineCtAnnotationHelper.WriteAnnotationJson(
                        jsonPath,
                        originalImagePath,
                        results,
                        settings.ResolveOfflineCtPixelSizeUm());
                    progress?.Report($"已写入 JSON：{jsonPath}");
                }

                if (settings.OfflineCtExportCsv)
                {
                    var csvPath = Path.Combine(annotationFolder!, "annotation.csv");
                    OfflineCtAnnotationHelper.WriteAnnotationCsv(csvPath, originalImagePath, results);
                    progress?.Report($"已写入 CSV：{csvPath}");
                }

                // 汇总表：结果根目录 CT产品尺寸.csv（按产品名 upsert，便于追加）
                try
                {
                    var csvRoot = !string.IsNullOrWhiteSpace(settings.SliceExportFolder)
                        ? settings.SliceExportFolder.Trim()
                        : Path.GetDirectoryName(annotationFolder!) ?? annotationFolder!;
                    var successResult = results.FirstOrDefault(r => r.IsSuccess)
                                        ?? results.FirstOrDefault();
                    if (successResult is not null)
                    {
                        var row = OfflineCtCsvExportHelper.BuildProductRow(
                            resultBaseName,
                            pixelSizeUm,
                            rangeCheck,
                            successResult.PrintfValues,
                            successResult.ResultImagePath);
                        OfflineCtCsvExportHelper.UpsertProductDimensionRow(csvRoot, row);
                        progress?.Report(
                            $"已更新尺寸汇总：{OfflineCtCsvExportHelper.BuildProductDimensionCsvPath(csvRoot)}");
                    }
                }
                catch (Exception csvEx)
                {
                    progress?.Report($"尺寸汇总 CSV 写入失败：{csvEx.Message}");
                    _logger.Warn($"CT产品尺寸.csv 写入失败：{csvEx.Message}");
                }

                progress?.Report($"AI 图像标注完成 → {annotationFolder}");
                _logger.Info($"AI 图像标注完成：{annotationFolder}，结果数={results.Count}");
            }
            finally
            {
                if (preBrighten.IsTemporary
                    && !string.IsNullOrWhiteSpace(preBrighten.FinalImagePath)
                    && !string.Equals(
                        preBrighten.FinalImagePath,
                        originalImagePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (File.Exists(preBrighten.FinalImagePath))
                        {
                            File.Delete(preBrighten.FinalImagePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"删除临时推理输入失败：{preBrighten.FinalImagePath}，{ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 单步测试：选手动图或按切片目录规则取「上」视图配置序号图。
        /// </summary>
        public async Task TestOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null,
            string? explicitImagePath = null,
            string? explicitOutputFolder = null)
        {
            Lock();
            try
            {
                progress?.Report("开始 AI 图像标注单步测试");
                _logger.Info("AI 图像标注单步测试开始");

                string? vglPath = null;
                string? exportBaseName = null;
                int? quantity = null;

                if (string.IsNullOrWhiteSpace(explicitImagePath)
                    && !string.IsNullOrWhiteSpace(settings.CTFileFolder))
                {
                    var volumeItems = GetVolumeQueueItems(settings.CTFileFolder);
                    if (volumeItems.Count > 0)
                    {
                        var volumeItem = volumeItems[0];
                        vglPath = volumeItem.ImportPath;
                        // 导出名取排队条目显示名前 6 字符（快捷方式与真实文件一致）
                        var namingFileName = Path.GetFileName(volumeItem.QueueEntryPath);
                        exportBaseName = SliceExportNamingHelper.BuildExportBaseName(namingFileName);
                        quantity = SliceExportNamingHelper.ResolveExportQuantity(
                            namingFileName,
                            settings.SliceExportQuantity,
                            settings.SliceExportQuantityQj);
                    }
                }

                await RunOfflineCtVisionAnnotationAsync(
                        settings,
                        vglPath,
                        exportBaseName,
                        quantity,
                        cancellationToken,
                        progress,
                        explicitImagePath,
                        explicitOutputFolder)
                    .ConfigureAwait(false);

                progress?.Report("AI 图像标注单步测试完成");
                _logger.Info("AI 图像标注单步测试完成");
            }
            finally
            {
                Unlock();
            }
        }

        /// <summary>
        /// NJCT DLL 直调批量测试：不走预处理/交接/上位机打黑叠加；
        /// <paramref name="dllBlackHeight"/> 直接传给 ModelAction 的 BlackHeight。
        /// </summary>
        public async Task RunOfflineCtDllBatchTestAsync(
            CTAutoSlicerSettings settings,
            string inputFolder,
            string outputFolder,
            int dllBlackHeight,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
            {
                throw new DirectoryNotFoundException($"DLL 直调输入目录不存在：{inputFolder}");
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("DLL 直调输出目录不能为空", nameof(outputFolder));
            }

            var inFull = Path.GetFullPath(inputFolder.Trim());
            var outFull = Path.GetFullPath(outputFolder.Trim());
            Directory.CreateDirectory(outFull);

            var images = Directory
                .EnumerateFiles(inFull, "*.*", SearchOption.AllDirectories)
                .Where(p =>
                {
                    var ext = Path.GetExtension(p);
                    return !string.IsNullOrEmpty(ext)
                           && (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase));
                })
                .Where(p =>
                {
                    var full = Path.GetFullPath(p);
                    return !full.StartsWith(
                        outFull + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(full, outFull, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (images.Count == 0)
            {
                throw new InvalidOperationException($"输入目录下未找到图片：{inFull}");
            }

            var blackHeight = Math.Max(0, dllBlackHeight);
            var mode = settings.OfflineCtResultImageMode;
            var modelPath = settings.ResolveOfflineCtModelPath();

            Lock();
            try
            {
                progress?.Report(
                    $"NJCT DLL 直调批量开始：共 {images.Count} 张，BlackHeight={blackHeight}，模式={mode}");
                _logger.Info(
                    $"NJCT DLL 直调批量：input={inFull}，output={outFull}，black={blackHeight}，count={images.Count}");

                await Task.Run(
                        () => _offlineCtVisionService.Initialize(modelPath, settings.OfflineCtIsRunCuda),
                        cancellationToken)
                    .ConfigureAwait(false);

                var ok = 0;
                var fail = 0;
                for (var i = 0; i < images.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var src = images[i];
                    var stem = Path.GetFileNameWithoutExtension(src);
                    progress?.Report($"[{i + 1}/{images.Count}] DLL 推理：{Path.GetFileName(src)}");

                    try
                    {
                        if (mode is OfflineCtResultImageMode.Enhance or OfflineCtResultImageMode.Both)
                        {
                            var outPath = Path.Combine(
                                outFull,
                                mode == OfflineCtResultImageMode.Both
                                    ? $"{stem}_enhance.jpg"
                                    : $"{stem}.jpg");
                            var r = await Task.Run(
                                    () => _offlineCtVisionService.InferEnhance(
                                        src,
                                        blackHeight,
                                        outPath,
                                        pixelSizeUm: settings.ResolveOfflineCtPixelSizeUm(),
                                        bottomInfoBlackHeight: 0,
                                        rangeCheck: null,
                                        applyHostOverlay: false),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            if (r.IsSuccess)
                            {
                                ok++;
                                progress?.Report($"  增强 OK → {r.ResultImagePath}");
                            }
                            else
                            {
                                fail++;
                                progress?.Report($"  增强失败：{r.RunInfo}");
                            }
                        }

                        if (mode is OfflineCtResultImageMode.Origin or OfflineCtResultImageMode.Both)
                        {
                            var originDllBlack =
                                CTAutoSlicerSettings.ResolveOriginBottomBlackHeight(blackHeight);
                            var outPath = Path.Combine(
                                outFull,
                                mode == OfflineCtResultImageMode.Both
                                    ? $"{stem}_origin.jpg"
                                    : $"{stem}.jpg");
                            var r = await Task.Run(
                                    () => _offlineCtVisionService.InferOrigin(
                                        src,
                                        originDllBlack,
                                        outPath,
                                        pixelSizeUm: settings.ResolveOfflineCtPixelSizeUm(),
                                        bottomInfoBlackHeight: 0,
                                        rangeCheck: null,
                                        applyHostOverlay: false),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            if (r.IsSuccess)
                            {
                                ok++;
                                progress?.Report(
                                    $"  原图 OK（DLL BlackHeight={originDllBlack}=增强一半）→ {r.ResultImagePath}");
                            }
                            else
                            {
                                fail++;
                                progress?.Report($"  原图失败：{r.RunInfo}");
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        fail++;
                        progress?.Report($"  异常：{ex.Message}");
                        _logger.Warn($"DLL 直调失败：{src}，{ex.Message}");
                    }
                }

                progress?.Report($"NJCT DLL 直调批量结束：成功 {ok}，失败 {fail}，输出={outFull}");
                _logger.Info($"NJCT DLL 直调批量结束：ok={ok}，fail={fail}");
            }
            finally
            {
                Unlock();
            }
        }
    }
}
