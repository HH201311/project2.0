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
        /// 取中间图：按文件名数字排序后取第 quantity/2 张（1-based；100→50）。
        /// quantity≤0 时用目录内文件数 / 2。
        /// </summary>
        public static string? TryPickMiddleTopViewImage(string topViewFolder, int quantity)
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

            var n = quantity > 0 ? quantity : files.Count;
            var targetIndex = Math.Max(1, n / 2);
            var hit = files.FirstOrDefault(f => f.Index == targetIndex);
            if (hit.Path != null)
            {
                return hit.Path;
            }

            // 目标序号文件缺失时，取排序后最接近中间的一张
            var midPos = Math.Clamp(targetIndex - 1, 0, files.Count - 1);
            return files[midPos].Path;
        }

        public static void WriteAnnotationJson(
            string outputPath,
            string sourceImagePath,
            IReadOnlyList<OfflineCtInferResult> results)
        {
            var payload = new
            {
                sourceImage = sourceImagePath,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
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
