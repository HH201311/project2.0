using System.IO;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public partial class VGAutomationService
    {
        /// <summary>
        /// 主流程：切片导出后，对「上」视图中间图做 AI 标注（软失败）。
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
        /// 对指定导出名的「上」视图中间图执行推理并落盘；也可直接对单张图测试。
        /// </summary>
        public async Task RunOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            string? vglPath,
            string? exportBaseName,
            int? quantity,
            CancellationToken cancellationToken,
            IProgress<string>? progress,
            string? explicitImagePath = null)
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

                annotationFolder = Path.Combine(
                    Path.GetDirectoryName(imagePath) ?? settings.SliceExportFolder,
                    OfflineCtAnnotationHelper.AnnotationFolderName);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(settings.SliceExportFolder))
                {
                    throw new InvalidOperationException("请先配置切片导出目录");
                }

                var resolvedBaseName = exportBaseName;
                var resolvedQuantity = quantity ?? 0;
                if (string.IsNullOrWhiteSpace(resolvedBaseName))
                {
                    if (string.IsNullOrWhiteSpace(vglPath))
                    {
                        throw new InvalidOperationException("缺少导出名或 vgl 路径，无法定位「上」视图文件夹");
                    }

                    resolvedBaseName = SliceExportNamingHelper.BuildExportBaseName(Path.GetFileName(vglPath));
                    resolvedQuantity = SliceExportNamingHelper.ResolveExportQuantity(
                        Path.GetFileName(vglPath),
                        settings.SliceExportQuantity,
                        settings.SliceExportQuantityQj);
                }

                var topFolder = OfflineCtAnnotationHelper.BuildTopViewFolder(
                    settings.SliceExportFolder,
                    resolvedBaseName);
                imagePath = OfflineCtAnnotationHelper.TryPickMiddleTopViewImage(topFolder, resolvedQuantity)
                    ?? throw new FileNotFoundException(
                        $"未在「上」视图目录找到中间图（期望序号 {Math.Max(1, resolvedQuantity / 2)}）：{topFolder}",
                        topFolder);

                annotationFolder = OfflineCtAnnotationHelper.BuildAnnotationFolder(
                    settings.SliceExportFolder,
                    resolvedBaseName);

                progress?.Report(
                    $"AI 标注选用「上」视图中间图：{Path.GetFileName(imagePath)}（数量基准 {resolvedQuantity}）");
                _logger.Info($"AI 标注输入图：{imagePath}，输出目录：{annotationFolder}");
            }

            Directory.CreateDirectory(annotationFolder!);

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
                progress?.Report("AI 标注：调用 ModelAction（增强图）…");
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
                progress?.Report("AI 标注：调用 ModelAction_origin（原图）…");
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
                OfflineCtAnnotationHelper.WriteAnnotationJson(jsonPath, imagePath, results);
                progress?.Report($"已写入 JSON：{jsonPath}");
            }

            if (settings.OfflineCtExportCsv)
            {
                var csvPath = Path.Combine(annotationFolder!, "annotation.csv");
                OfflineCtAnnotationHelper.WriteAnnotationCsv(csvPath, imagePath, results);
                progress?.Report($"已写入 CSV：{csvPath}");
            }

            progress?.Report($"AI 图像标注完成 → {annotationFolder}");
            _logger.Info($"AI 图像标注完成：{annotationFolder}，结果数={results.Count}");
        }

        /// <summary>
        /// 单步测试：选手动图或按切片目录规则取「上」视图中间图。
        /// </summary>
        public async Task TestOfflineCtVisionAnnotationAsync(
            CTAutoSlicerSettings settings,
            CancellationToken cancellationToken,
            IProgress<string>? progress = null,
            string? explicitImagePath = null)
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
                        explicitImagePath)
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
