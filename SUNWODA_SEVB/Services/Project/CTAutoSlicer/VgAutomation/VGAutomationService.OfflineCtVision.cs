using System.IO;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public partial class VGAutomationService
    {
        /// <summary>
        /// AI 标注模式批处理：扫描交接目录一层图像，逐张推理并落盘到标注结果根目录。
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

                var ok = 0;
                var fail = 0;
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

                progress?.Report($"AI 标注批处理完成：成功 {ok}，失败 {fail}，合计 {images.Count}");
                _logger.Info($"AI 标注批处理完成：ok={ok}, fail={fail}, total={images.Count}");
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

                var resolvedBaseName = exportBaseName;
                if (string.IsNullOrWhiteSpace(resolvedBaseName))
                {
                    if (string.IsNullOrWhiteSpace(vglPath))
                    {
                        throw new InvalidOperationException("缺少导出名或 vgl 路径，无法定位「上」视图文件夹");
                    }

                    resolvedBaseName = SliceExportNamingHelper.BuildExportBaseName(Path.GetFileName(vglPath));
                }

                var sliceIndex = settings.ResolveOfflineCtTopViewSliceIndex();
                var expectedName = $"{sliceIndex}.jpg";

                var topFolder = OfflineCtAnnotationHelper.BuildTopViewFolder(
                    settings.SliceExportFolder,
                    resolvedBaseName);
                imagePath = OfflineCtAnnotationHelper.TryPickTopViewImageByIndex(topFolder, sliceIndex)
                    ?? throw new FileNotFoundException(
                        $"未在「上」视图目录按文件名找到送检图（期望 {expectedName} 等）：{topFolder}",
                        topFolder);

                annotationFolder = OfflineCtAnnotationHelper.BuildAnnotationFolder(
                    settings.SliceExportFolder,
                    resolvedBaseName);

                progress?.Report(
                    $"AI 标注按文件名选用：{Path.GetFileName(imagePath)}"
                    + $"（配置={settings.ResolveOfflineCtTopViewSliceIndex()} → {expectedName}，"
                    + $"像素精度 {settings.ResolveOfflineCtPixelSizeUm():G} μm/px）");
                _logger.Info($"AI 标注输入图：{imagePath}，输出目录：{annotationFolder}，fileNameIndex={sliceIndex}");
            }

            Directory.CreateDirectory(annotationFolder!);
            progress?.Report($"AI 标注输入图（原始上视图）：{imagePath}");
            progress?.Report($"AI 标注输出目录：{annotationFolder}");
            _logger.Info($"AI 标注输入图：{imagePath}，输出目录：{annotationFolder}");

            var originalImagePath = imagePath;

            // 方案 B：内存中可选 CW90 → 算亮度 → 仅落盘最终效果图 input_for_njct → 再推理（不再落盘 input_rotated_cw90）
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(
                settings.OfflineCtRotateTopViewCw90
                    ? "AI 标注：内存顺时针旋转 90° + 计算 ROI 亮度，生成最终推理图…"
                    : "AI 标注：计算 ROI 亮度并准备最终推理图（未旋转）…");
            var preBrighten = await Task.Run(
                    () => OfflineCtPreBrightenHelper.PrepareFinalInputImage(
                        originalImagePath,
                        annotationFolder!,
                        settings.EnableOfflineCtPreBrighten,
                        settings.OfflineCtRotateTopViewCw90,
                        settings.OfflineCtPreBrightenMeanTrigger,
                        settings.OfflineCtPreBrightenP50Trigger,
                        settings.OfflineCtPreBrightenTargetMean),
                    cancellationToken)
                .ConfigureAwait(false);
            imagePath = preBrighten.FinalImagePath;
            progress?.Report(preBrighten.Summary);
            _logger.Info($"AI 标注预处理：{preBrighten.Summary}；报告={preBrighten.ReportPath}");

            var modelPath = settings.ResolveOfflineCtModelPath();
            progress?.Report($"AI 标注初始化模型：{modelPath}");
            await Task.Run(
                    () => _offlineCtVisionService.Initialize(modelPath, settings.OfflineCtIsRunCuda),
                    cancellationToken)
                .ConfigureAwait(false);

            var results = new List<OfflineCtInferResult>();
            var mode = settings.OfflineCtResultImageMode;
            var blackHeight = settings.OfflineCtBlackHeight;

            if (mode is OfflineCtResultImageMode.Enhance or OfflineCtResultImageMode.Both)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var enhancePath = Path.Combine(annotationFolder!, "result_enhance.jpg");
                progress?.Report($"AI 标注：调用 ModelAction（增强图），输入={Path.GetFileName(imagePath)}…");
                var enhanceResult = await Task.Run(
                        () => _offlineCtVisionService.InferEnhance(imagePath, blackHeight, enhancePath),
                        cancellationToken)
                    .ConfigureAwait(false);
                results.Add(enhanceResult);
                progress?.Report(
                    enhanceResult.IsSuccess
                        ? $"增强图完成：{enhanceResult.ResultImagePath}"
                        : $"增强图完成但有 RunInfo：{enhanceResult.RunInfo}");
            }

            if (mode is OfflineCtResultImageMode.Origin or OfflineCtResultImageMode.Both)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var originPath = Path.Combine(annotationFolder!, "result_origin.jpg");
                progress?.Report($"AI 标注：调用 ModelAction_origin（原图），输入={Path.GetFileName(imagePath)}…");
                var originResult = await Task.Run(
                        () => _offlineCtVisionService.InferOrigin(imagePath, blackHeight, originPath),
                        cancellationToken)
                    .ConfigureAwait(false);
                results.Add(originResult);
                progress?.Report(
                    originResult.IsSuccess
                        ? $"原图完成：{originResult.ResultImagePath}"
                        : $"原图完成但有 RunInfo：{originResult.RunInfo}");
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

            progress?.Report($"AI 图像标注完成 → {annotationFolder}");
            _logger.Info($"AI 图像标注完成：{annotationFolder}，结果数={results.Count}");
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
                        vglPath = volumeItems[0].ImportPath;
                        exportBaseName = SliceExportNamingHelper.BuildExportBaseName(Path.GetFileName(vglPath));
                        quantity = SliceExportNamingHelper.ResolveExportQuantity(
                            Path.GetFileName(vglPath),
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
    }
}
