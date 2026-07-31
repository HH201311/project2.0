using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 惠州离线 CT AI 标注：上视图中间图选取与 JSON/CSV 落盘。
    /// </summary>
    public static class OfflineCtAnnotationHelper
    {
        public const string AnnotationFolderName = "AI标注";

        /// <summary>已废弃中间落盘名；最终推理图请用 OfflineCtPreBrightenHelper.FinalInputFileNameWithoutExt。</summary>
        [Obsolete("不再单独落盘旋转中间图，请使用 PrepareFinalInputImage(rotateCw90: true)")]
        public const string RotatedInputFileNameWithoutExt = "input_rotated_cw90";

        private static readonly Regex NumericFileName = new(
            @"^(\d+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

        /// <summary>
        /// 「上」视图文件夹：{exportRoot}/{exportBaseName}/{exportBaseName} 上
        /// </summary>
        public static string BuildTopViewFolder(string sliceExportFolder, string exportBaseName) =>
            Path.Combine(
                sliceExportFolder,
                SliceExportNamingHelper.BuildExportFileRootFolderName(exportBaseName),
                SliceExportNamingHelper.BuildExportFolderName(exportBaseName, "上"));

        /// <summary>
        /// AI 标注输出目录（与三视图文件夹同级）：{exportRoot}/{exportBaseName}/AI标注
        /// </summary>
        public static string BuildAnnotationFolder(string sliceExportFolder, string exportBaseName) =>
            Path.Combine(
                sliceExportFolder,
                SliceExportNamingHelper.BuildExportFileRootFolderName(exportBaseName),
                AnnotationFolderName);

        /// <summary>
        /// 交接文件名：{exportBaseName}_{index:D3}{ext}，避免批次重名覆盖。
        /// </summary>
        public static string BuildHandoffFileName(string exportBaseName, int sliceIndex, string sourceImagePath)
        {
            var safeBase = string.IsNullOrWhiteSpace(exportBaseName)
                ? "export"
                : Path.GetFileName(exportBaseName.Trim());
            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext)
                || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".jpg";
            }

            return $"{safeBase}_{Math.Max(1, sliceIndex):D3}{ext.ToLowerInvariant()}";
        }

        /// <summary>
        /// 将上视图送检图复制到交接目录，返回目标路径。
        /// </summary>
        public static string CopyTopViewImageToHandoffFolder(
            string sourceImagePath,
            string handoffFolder,
            string exportBaseName,
            int sliceIndex)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("待交接的上视图送检图不存在", sourceImagePath);
            }

            if (string.IsNullOrWhiteSpace(handoffFolder))
            {
                throw new InvalidOperationException("请先配置上视图交接目录（TopViewHandoffFolder）");
            }

            Directory.CreateDirectory(handoffFolder);
            var destPath = Path.Combine(
                handoffFolder,
                BuildHandoffFileName(exportBaseName, sliceIndex, sourceImagePath));
            File.Copy(sourceImagePath, destPath, overwrite: true);
            return destPath;
        }

        /// <summary>
        /// 枚举交接目录一层内的图像文件（不递归，避免扫到结果子目录）。
        /// </summary>
        public static IReadOnlyList<string> EnumerateHandoffImages(string handoffFolder)
        {
            if (string.IsNullOrWhiteSpace(handoffFolder) || !Directory.Exists(handoffFolder))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateFiles(handoffFolder)
                .Where(p => ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// AI 批处理输出目录：{resultRoot}/{文件名去扩展}/AI标注
        /// </summary>
        public static string BuildBatchAnnotationFolder(string resultRoot, string sourceImagePath)
        {
            var stem = Path.GetFileNameWithoutExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(stem))
            {
                stem = "annotation";
            }

            return Path.Combine(resultRoot, stem, AnnotationFolderName);
        }

        /// <summary>
        /// 将上视图顺时针旋转 90° 后保存到 outputPath（原图不变）。主流程已改为内存旋转+input_for_njct，一般不再调用。
        /// </summary>
        public static string SaveClockwiseRotated90(string sourceImagePath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("待旋转的上视图不存在", sourceImagePath);
            }

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 先读入内存再释放文件锁，避免 Image.FromFile 长期占用源文件
            using var source = LoadBitmapUnlocked(sourceImagePath);
            source.RotateFlip(RotateFlipType.Rotate90FlipNone);

            var format = ResolveImageFormat(outputPath);
            source.Save(outputPath, format);
            return outputPath;
        }

        /// <summary>
        /// 已废弃：勿再生成 input_rotated_cw90；请用 OfflineCtPreBrightenHelper.PrepareFinalInputImage(..., rotateCw90: true)。
        /// </summary>
        [Obsolete("不再落盘 input_rotated_cw90，请使用 OfflineCtPreBrightenHelper.PrepareFinalInputImage")]
        public static string CreateRotatedInputForAnnotation(string sourceImagePath, string annotationFolder)
        {
            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext) || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                ext = ".jpg";
            }

            var outputPath = Path.Combine(annotationFolder, RotatedInputFileNameWithoutExt + ext.ToLowerInvariant());
            return SaveClockwiseRotated90(sourceImagePath, outputPath);
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var temp = new Bitmap(fs);
            return new Bitmap(temp);
        }

        private static ImageFormat ResolveImageFormat(string path)
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }

            if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Bmp;
            }

            if (ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Tiff;
            }

            return ImageFormat.Jpeg;
        }

        /// <summary>
        /// 按文件名数字序号选取「上」视图切片图（1-based）。
        /// 例如 sliceIndex=50 → 优先取 50.jpg；缺失时取排序后最接近的一张。
        /// </summary>
        /// <param name="topViewFolder">「上」视图导出目录</param>
        /// <param name="sliceIndex">目标序号（建议 1～100；内部会钳制到 ≥1）</param>
        public static string? TryPickTopViewImageByIndex(string topViewFolder, int sliceIndex)
        {
            if (string.IsNullOrWhiteSpace(topViewFolder) || !Directory.Exists(topViewFolder))
            {
                return null;
            }

            var files = Directory.EnumerateFiles(topViewFolder)
                .Where(p => ImageExtensions.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                .Select(p => (Path: p, Index: TryParseLeadingNumber(Path.GetFileNameWithoutExtension(p))))
                .Where(x => x.Index > 0)
                .OrderBy(x => x.Index)
                .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                return null;
            }

            var targetIndex = Math.Max(1, sliceIndex);
            var hit = files.FirstOrDefault(f => f.Index == targetIndex);
            if (hit.Path != null)
            {
                return hit.Path;
            }

            // 目标序号文件缺失时，取排序后序号最接近的一张
            var closest = files
                .OrderBy(f => Math.Abs(f.Index - targetIndex))
                .ThenBy(f => f.Index)
                .First();
            return closest.Path;
        }

        /// <summary>
        /// 兼容旧调用：取中间图（quantity/2）。新代码请用 <see cref="TryPickTopViewImageByIndex"/>。
        /// </summary>
        [Obsolete("请使用 TryPickTopViewImageByIndex，并由 OfflineCtTopViewSliceIndex 配置目标序号")]
        public static string? TryPickMiddleTopViewImage(string topViewFolder, int quantity)
        {
            var n = quantity > 0 ? quantity : 0;
            var targetIndex = n > 0 ? Math.Max(1, n / 2) : CTAutoSlicerSettings.DefaultOfflineCtTopViewSliceIndex;
            return TryPickTopViewImageByIndex(topViewFolder, targetIndex);
        }

        public static void WriteAnnotationJson(
            string outputPath,
            string sourceImagePath,
            IReadOnlyList<OfflineCtInferResult> results,
            double? pixelSizeUm = null)
        {
            var payload = new
            {
                sourceImage = sourceImagePath,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                pixelSizeUm = pixelSizeUm,
                pixelSizeUnit = "μm/px",
                unitConversion = "1 mm = 1000 μm; L_μm = N_px × pixelSizeUm; L_mm = N_px × pixelSizeUm / 1000",
                results = results.Select(r => new
                {
                    mode = r.ModeTag,
                    success = r.IsSuccess,
                    runInfo = r.RunInfo,
                    resultImage = r.ResultImagePath,
                    cells = Enumerable.Range(0, Math.Max(r.RegionRows.Count, Math.Max(r.RegionCols.Count, r.PrintfValues.Count)))
                        .Select(i => new
                        {
                            cellIndex = i + 1,
                            rows = i < r.RegionRows.Count ? r.RegionRows[i] : Array.Empty<double>(),
                            cols = i < r.RegionCols.Count ? r.RegionCols[i] : Array.Empty<double>(),
                            printfValues = i < r.PrintfValues.Count ? r.PrintfValues[i] : Array.Empty<double>(),
                        })
                        .ToList(),
                }).ToList(),
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            File.WriteAllText(outputPath, json, Encoding.UTF8);
        }

        public static void WriteAnnotationCsv(
            string outputPath,
            string sourceImagePath,
            IReadOnlyList<OfflineCtInferResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Mode,Success,RunInfo,ResultImage,SourceImage,CellIndex,PointIndex,Row,Col,PrintfValue");
            foreach (var r in results)
            {
                var cellCount = Math.Max(r.RegionRows.Count, Math.Max(r.RegionCols.Count, r.PrintfValues.Count));
                if (cellCount == 0)
                {
                    sb.AppendLine(string.Join(',',
                        Csv(r.ModeTag),
                        r.IsSuccess ? "1" : "0",
                        Csv(r.RunInfo),
                        Csv(r.ResultImagePath ?? string.Empty),
                        Csv(sourceImagePath),
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty));
                    continue;
                }

                for (var c = 0; c < cellCount; c++)
                {
                    var rows = c < r.RegionRows.Count ? r.RegionRows[c] : Array.Empty<double>();
                    var cols = c < r.RegionCols.Count ? r.RegionCols[c] : Array.Empty<double>();
                    var vals = c < r.PrintfValues.Count ? r.PrintfValues[c] : Array.Empty<double>();
                    var pointCount = Math.Max(rows.Length, Math.Max(cols.Length, vals.Length));
                    if (pointCount == 0)
                    {
                        sb.AppendLine(string.Join(',',
                            Csv(r.ModeTag),
                            r.IsSuccess ? "1" : "0",
                            Csv(r.RunInfo),
                            Csv(r.ResultImagePath ?? string.Empty),
                            Csv(sourceImagePath),
                            (c + 1).ToString(CultureInfo.InvariantCulture),
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty));
                        continue;
                    }

                    for (var p = 0; p < pointCount; p++)
                    {
                        sb.AppendLine(string.Join(',',
                            Csv(r.ModeTag),
                            r.IsSuccess ? "1" : "0",
                            Csv(r.RunInfo),
                            Csv(r.ResultImagePath ?? string.Empty),
                            Csv(sourceImagePath),
                            (c + 1).ToString(CultureInfo.InvariantCulture),
                            (p + 1).ToString(CultureInfo.InvariantCulture),
                            p < rows.Length ? rows[p].ToString(CultureInfo.InvariantCulture) : string.Empty,
                            p < cols.Length ? cols[p].ToString(CultureInfo.InvariantCulture) : string.Empty,
                            p < vals.Length ? vals[p].ToString(CultureInfo.InvariantCulture) : string.Empty));
                    }
                }
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        private static int TryParseLeadingNumber(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return -1;
            }

            var m = NumericFileName.Match(name.Trim());
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : -1;
        }

        private static string Csv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
