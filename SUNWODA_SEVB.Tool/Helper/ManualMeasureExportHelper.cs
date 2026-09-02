using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 二次手动标注：递归枚举图片、从 CSV 续标、导出扁平叠加图与 CSV（无 json/index）。
    /// 默认导出目录名为 <see cref="DefaultExportFolderName"/>（result）。
    /// </summary>
    public static class ManualMeasureExportHelper
    {
        /// <summary>默认导出子目录名（相对打开的根目录；仅作枚举跳过兼容）。</summary>
        public const string DefaultExportFolderName = "result";

        /// <summary>二次手动标注导出目录内置默认（UNC，本机可改并持久化）。</summary>
        public const string DefaultManualExportFolder = @"\\192.168.1.120\3Ddeliever\result";

        /// <summary>历史导出子目录名；枚举跳过与续标回退时仍识别。</summary>
        public const string LegacyExportFolderName = "output";

        [Obsolete("请使用 DefaultExportFolderName（result）")]
        public const string OutputFolderName = DefaultExportFolderName;

        private static readonly string[] ImageExtensions =
            [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>默认导出目录：优先内置 UNC；兼容旧调用仍保留 Root 相对 result 解析。</summary>
        public static string ResolveDefaultExportDir(string rootFolder) =>
            DefaultManualExportFolder;

        /// <summary>相对根目录的 result 子目录（枚举时跳过用）。</summary>
        public static string ResolveRootResultDir(string rootFolder) =>
            Path.Combine(rootFolder, DefaultExportFolderName);

        /// <summary>历史导出目录：{rootFolder}/output（仅续标兼容）</summary>
        public static string ResolveLegacyExportDir(string rootFolder) =>
            Path.Combine(rootFolder, LegacyExportFolderName);

        public static IReadOnlyList<string> EnumerateImagesRecursive(
            string rootFolder,
            string? exportDirToSkip = null)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                return [];
            }

            var rootFull = Path.GetFullPath(rootFolder);
            var skipDirs = BuildSkipDirs(rootFull, exportDirToSkip);

            return Directory
                .EnumerateFiles(rootFull, "*.*", SearchOption.AllDirectories)
                .Where(p =>
                {
                    var ext = Path.GetExtension(p);
                    if (string.IsNullOrEmpty(ext)
                        || !ImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var full = Path.GetFullPath(p);
                    if (skipDirs.Any(skip =>
                        full.StartsWith(skip + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, skip, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }

                    try
                    {
                        var relative = Path.GetRelativePath(rootFull, full);
                        var segments = relative.Split(
                            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                            StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Any(s =>
                            string.Equals(
                                s,
                                OfflineCtPreBrightenHelper.InputSubFolderName,
                                StringComparison.OrdinalIgnoreCase)))
                        {
                            return false;
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    return true;
                })
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string ToRelativePath(string rootFolder, string absolutePath)
        {
            var root = Path.GetFullPath(rootFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(absolutePath);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(full);
            }

            return full[(root.Length + 1)..].Replace('\\', '/');
        }

        /// <summary>兼容旧调用：等同 <see cref="ResolveDefaultExportDir"/>。</summary>
        public static string BuildOutputDir(string rootFolder) =>
            ResolveDefaultExportDir(rootFolder);

        /// <summary>
        /// 叠加图扁平输出：{exportDir}/{产品名}.png（无子目录、无 _annotated 后缀）。
        /// </summary>
        public static string BuildAnnotatedImagePath(string exportDir, string relativePath)
        {
            var product = OfflineCtCsvExportHelper.ResolveProductName(relativePath);
            var safe = SanitizeFileName(product);
            return Path.Combine(NormalizeExportDir(exportDir), safe + ".png");
        }

        public static ManualMeasureItem CreateMeasure(
            double x1,
            double y1,
            double x2,
            double y2,
            double pixelSizeUm,
            string? label = null)
        {
            var nPx = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            var s = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            return new ManualMeasureItem
            {
                Label = label ?? string.Empty,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                PixelLength = nPx,
                LengthUm = nPx * s,
                LengthMm = nPx * s / 1000.0,
            };
        }

        public static void RecalcLengths(ManualMeasureItem item, double pixelSizeUm)
        {
            var s = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            var dx = item.X2 - item.X1;
            var dy = item.Y2 - item.Y1;
            item.PixelLength = Math.Sqrt(dx * dx + dy * dy);
            item.LengthUm = item.PixelLength * s;
            item.LengthMm = item.LengthUm / 1000.0;
        }

        /// <summary>
        /// 一次加载主导出目录 + 历史 output 的手动测距 CSV 索引（主目录优先）。
        /// </summary>
        public static Dictionary<string, List<ManualMeasureItem>> BuildManualMeasureLookup(
            string exportDir,
            string? legacyExportDir = null)
        {
            var map = OfflineCtCsvExportHelper.LoadAllManualMeasuresByProduct(exportDir);
            if (string.IsNullOrWhiteSpace(legacyExportDir))
            {
                return map;
            }

            try
            {
                var primary = Path.GetFullPath(NormalizeExportDir(exportDir));
                var legacy = Path.GetFullPath(NormalizeExportDir(legacyExportDir));
                if (string.Equals(primary, legacy, StringComparison.OrdinalIgnoreCase))
                {
                    return map;
                }
            }
            catch
            {
                return map;
            }

            var secondary = OfflineCtCsvExportHelper.LoadAllManualMeasuresByProduct(legacyExportDir);
            OfflineCtCsvExportHelper.MergeManualMeasureLookup(map, secondary);
            return map;
        }

        /// <summary>
        /// 续标：优先用已建好的 CSV 索引，再回退历史单图 JSON（避免每图重读整表）。
        /// </summary>
        public static List<ManualMeasureItem> TryLoadImageMeasures(
            string exportDir,
            string relativePath,
            string? legacyExportDir = null,
            IReadOnlyDictionary<string, List<ManualMeasureItem>>? csvLookup = null)
        {
            var product = OfflineCtCsvExportHelper.ResolveProductName(relativePath);
            if (csvLookup is not null)
            {
                if (csvLookup.TryGetValue(product, out var indexed) && indexed.Count > 0)
                {
                    return CloneMeasureList(indexed);
                }
            }
            else
            {
                var fromCsv = OfflineCtCsvExportHelper.TryLoadManualMeasures(exportDir, product);
                if (fromCsv.Count > 0)
                {
                    return fromCsv;
                }
            }

            var loaded = TryLoadLegacyImageJson(exportDir, relativePath);
            if (loaded.Count > 0)
            {
                return loaded;
            }

            if (string.IsNullOrWhiteSpace(legacyExportDir))
            {
                return [];
            }

            try
            {
                var primary = Path.GetFullPath(NormalizeExportDir(exportDir));
                var legacy = Path.GetFullPath(NormalizeExportDir(legacyExportDir));
                if (string.Equals(primary, legacy, StringComparison.OrdinalIgnoreCase))
                {
                    return [];
                }
            }
            catch
            {
                return [];
            }

            if (csvLookup is null)
            {
                var fromLegacyCsv = OfflineCtCsvExportHelper.TryLoadManualMeasures(legacyExportDir, product);
                if (fromLegacyCsv.Count > 0)
                {
                    return fromLegacyCsv;
                }
            }

            return TryLoadLegacyImageJson(legacyExportDir, relativePath);
        }

        private static List<ManualMeasureItem> CloneMeasureList(IReadOnlyList<ManualMeasureItem> source)
        {
            var list = new List<ManualMeasureItem>(source.Count);
            foreach (var m in source)
            {
                list.Add(new ManualMeasureItem
                {
                    Label = m.Label,
                    X1 = m.X1,
                    Y1 = m.Y1,
                    X2 = m.X2,
                    Y2 = m.Y2,
                    LengthMm = m.LengthMm,
                    PixelLength = m.PixelLength,
                    LengthUm = m.LengthUm,
                    LabelX = m.LabelX,
                    LabelY = m.LabelY,
                });
            }

            return list;
        }

        /// <summary>
        /// 导出：CSV +（可选）叠加图。界面导出请将 <paramref name="renderImages"/> 置 false，
        /// 由窗口把当前标注画布截屏保存，保证与预览一致。
        /// </summary>
        public static void ExportSession(
            string rootFolder,
            double pixelSizeUm,
            IReadOnlyList<ManualMeasureImageRecord> images,
            int bottomBlackHeightPx = 0,
            string? exportDir = null,
            OfflineCtLRangeCheckOptions? rangeCheck = null,
            bool renderImages = true)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                throw new ArgumentException("根目录不能为空", nameof(rootFolder));
            }

            var outDir = NormalizeExportDir(
                string.IsNullOrWhiteSpace(exportDir)
                    ? ResolveDefaultExportDir(rootFolder)
                    : exportDir);
            Directory.CreateDirectory(outDir);

            var exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var range = rangeCheck ?? OfflineCtLRangeCheckOptions.Disabled;
            var productNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var manualRows = new List<string[]>();

            foreach (var img in images)
            {
                EnsureLabels(img.Measures);
                foreach (var m in img.Measures)
                {
                    RecalcLengths(m, pixelSizeUm);
                }

                var product = OfflineCtCsvExportHelper.ResolveProductName(img.RelativePath);
                productNames.Add(product);

                if (File.Exists(img.AbsolutePath) && renderImages)
                {
                    RenderAnnotatedImage(
                        img.AbsolutePath,
                        BuildAnnotatedImagePath(outDir, img.RelativePath),
                        img.Measures,
                        bottomBlackHeightPx);
                }

                foreach (var m in img.Measures)
                {
                    manualRows.Add(
                        OfflineCtCsvExportHelper.BuildManualMeasureFields(product, m, exportedAt));
                }
            }

            OfflineCtCsvExportHelper.ReplaceManualMeasures(outDir, productNames, manualRows);
            EnsureProductRowsExist(outDir, productNames, pixelSizeUm, range, exportedAt);
            TryMergeProductDimensionsFromRoot(rootFolder, outDir);
        }

        private static void EnsureProductRowsExist(
            string outDir,
            IEnumerable<string> productNames,
            double pixelSizeUm,
            OfflineCtLRangeCheckOptions range,
            string exportedAt)
        {
            var path = OfflineCtCsvExportHelper.BuildProductDimensionCsvPath(outDir);
            foreach (var name in productNames)
            {
                if (string.IsNullOrWhiteSpace(name) || ProductDimensionContains(path, name))
                {
                    continue;
                }

                var row = OfflineCtCsvExportHelper.BuildProductRow(
                    name,
                    pixelSizeUm,
                    range,
                    printfValuesPx: null,
                    resultImagePath: string.Empty,
                    generatedAt: DateTime.TryParse(
                        exportedAt,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt)
                        ? dt
                        : DateTime.Now);
                OfflineCtCsvExportHelper.UpsertProductDimensionRow(outDir, row);
            }
        }

        private static bool ProductDimensionContains(string csvPath, string productName)
        {
            if (!File.Exists(csvPath))
            {
                return false;
            }

            try
            {
                foreach (var line in File.ReadLines(csvPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.Contains("产品名", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var comma = line.IndexOf(',');
                    var name = comma > 0 ? line[..comma].Trim().Trim('"') : line.Trim().Trim('"');
                    if (string.Equals(name, productName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static void TryMergeProductDimensionsFromRoot(string rootFolder, string outDir)
        {
            try
            {
                var rootCsv = OfflineCtCsvExportHelper.BuildProductDimensionCsvPath(rootFolder);
                var outCsv = OfflineCtCsvExportHelper.BuildProductDimensionCsvPath(outDir);
                if (!File.Exists(rootCsv))
                {
                    return;
                }

                if (string.Equals(
                        Path.GetFullPath(rootCsv),
                        Path.GetFullPath(outCsv),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var rootLines = File.ReadAllLines(rootCsv, Encoding.UTF8);
                if (rootLines.Length <= 1)
                {
                    return;
                }

                for (var i = 1; i < rootLines.Length; i++)
                {
                    var line = rootLines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var comma = line.IndexOf(',');
                    var name = comma > 0 ? line[..comma].Trim().Trim('"') : line.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(name) || ProductDimensionContains(outCsv, name))
                    {
                        continue;
                    }

                    AppendRawProductLine(outCsv, rootLines[0], line);
                }
            }
            catch
            {
                // 合并失败不影响主导出
            }
        }

        private static void AppendRawProductLine(string outCsv, string header, string dataLine)
        {
            if (!File.Exists(outCsv))
            {
                File.WriteAllText(
                    outCsv,
                    header + Environment.NewLine + dataLine + Environment.NewLine,
                    new UTF8Encoding(true));
                return;
            }

            File.AppendAllText(outCsv, dataLine + Environment.NewLine, new UTF8Encoding(true));
        }

        private static List<ManualMeasureItem> TryLoadLegacyImageJson(string exportDir, string relativePath)
        {
            var relNoExt = Path.ChangeExtension(relativePath.Replace('/', Path.DirectorySeparatorChar), null)
                           ?? relativePath;
            var jsonPath = Path.Combine(NormalizeExportDir(exportDir), relNoExt + ".json");
            if (!File.Exists(jsonPath))
            {
                var flat = Path.Combine(
                    NormalizeExportDir(exportDir),
                    OfflineCtCsvExportHelper.ResolveProductName(relativePath) + ".json");
                if (!File.Exists(flat))
                {
                    return [];
                }

                jsonPath = flat;
            }

            try
            {
                var json = File.ReadAllText(jsonPath, Encoding.UTF8);
                var record = JsonSerializer.Deserialize<ManualMeasureImageRecord>(json, JsonOptions);
                return record?.Measures ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static List<string> BuildSkipDirs(string rootFull, string? exportDirToSkip)
        {
            var list = new List<string>();
            void AddIfValid(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                try
                {
                    var full = Path.GetFullPath(path)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!list.Any(x => string.Equals(x, full, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(full);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            AddIfValid(ResolveRootResultDir(rootFull));
            AddIfValid(ResolveLegacyExportDir(rootFull));
            AddIfValid(DefaultManualExportFolder);
            AddIfValid(Path.Combine(rootFull, OfflineCtPreBrightenHelper.InputSubFolderName));
            AddIfValid(exportDirToSkip);
            return list;
        }

        private static string NormalizeExportDir(string exportDir)
        {
            if (string.IsNullOrWhiteSpace(exportDir))
            {
                throw new ArgumentException("导出目录不能为空", nameof(exportDir));
            }

            return Path.GetFullPath(exportDir.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            var s = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(s) ? "unnamed" : s;
        }

        private static void EnsureLabels(IList<ManualMeasureItem> measures)
        {
            for (var i = 0; i < measures.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(measures[i].Label))
                {
                    measures[i].Label = $"M{i + 1}";
                }
            }
        }

        /// <summary>
        /// 在结果图上绘制测距线，布局与二次手动标注预览一致：
        /// 测距线/引出/黄标签；左侧 AI 已烧录汇总保留，仅右侧 1/4 打黑写「手动标注」。
        /// </summary>
        public static void RenderAnnotatedImage(
            string sourceImagePath,
            string outputPath,
            IReadOnlyList<ManualMeasureItem> measures,
            int bottomBlackHeightPx = 0)
        {
            // 与界面 LabelLeaderLength=42（zoom≈1 时图像像素）对齐
            const float leaderLengthImg = 42f;

            using var src = Image.FromFile(sourceImagePath);
            using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.Black);
                g.DrawImage(src, 0, 0, src.Width, src.Height);

                // 与界面一致：青色测距线 + 浅青虚线引出 + 黄字黑底标签
                using var pen = new Pen(Color.FromArgb(255, 0, 220, 255), 2f);
                using var leaderPen = new Pen(Color.FromArgb(255, 180, 220, 255), 1.2f)
                {
                    DashStyle = DashStyle.Dash,
                    DashPattern = [4f, 3f],
                };
                var labelFontSize = Math.Clamp(src.Width / 160f, 12f, 28f);
                using var font = new Font("Consolas", labelFontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.Yellow);
                using var bgBrush = new SolidBrush(Color.FromArgb(180, 0, 0, 0));

                foreach (var m in measures)
                {
                    var p1 = new PointF((float)m.X1, (float)m.Y1);
                    var p2 = new PointF((float)m.X2, (float)m.Y2);
                    g.DrawLine(pen, p1, p2);

                    var mid = new PointF((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f);
                    PointF labelCenter;
                    if (m.HasCustomLabelPosition)
                    {
                        labelCenter = new PointF((float)m.LabelX!.Value, (float)m.LabelY!.Value);
                    }
                    else
                    {
                        (_, labelCenter) = GetPerpendicularLeader(p1, p2, leaderLengthImg);
                    }

                    g.DrawLine(leaderPen, mid, labelCenter);

                    var text = FormatMeasureLabelMm(m);
                    var size = g.MeasureString(text, font);
                    var textOrigin = new PointF(labelCenter.X - size.Width / 2f, labelCenter.Y - size.Height / 2f);
                    var rect = new RectangleF(textOrigin.X - 3f, textOrigin.Y - 1f, size.Width + 6f, size.Height + 2f);
                    g.FillRectangle(bgBrush, rect);
                    g.DrawString(text, font, brush, textOrigin);

                    DrawCross(g, pen, p1, 6f);
                    DrawCross(g, pen, p2, 6f);
                }

                // 导出：左侧 AI 汇总保留；右 1/4 打黑写「手动标注」
                // 与 AI 出图同一 OfflineCtBlackHeight（原图路径自动 ÷2），不做底栏检测
                var bandH = OfflineCtAnnotationHelper.ResolveConfiguredBottomBlackHeightForPath(
                    sourceImagePath,
                    bottomBlackHeightPx,
                    src.Height);
                bandH = CTAutoSlicerSettings.ClampBottomBlackHeight(bandH, src.Height);
                if (bandH > 0)
                {
                    var bandY = src.Height - bandH;
                    var rightX = src.Width * 3f / 4f;
                    var rightW = src.Width - rightX;

                    // 只盖右侧，不遮左侧 AI 汇总
                    g.FillRectangle(Brushes.Black, rightX, bandY, rightW, bandH);

                    using var dividerPen = new Pen(Color.FromArgb(255, 0x66, 0x66, 0x66), 1f);
                    g.DrawLine(dividerPen, rightX, bandY + 4f, rightX, src.Height - 4f);

                    var fontPx = OfflineCtAnnotationHelper.ComputeBottomBandFontSizeImagePx(bandH, src.Width);
                    using var summaryFont = new Font("Consolas", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
                    using var titleBrush = new SolidBrush(Color.Orange);
                    using var summaryBrush = new SolidBrush(Color.White);

                    var lines = new List<string> { "手动标注" };
                    if (measures.Count > 0)
                    {
                        lines.AddRange(measures.Select(FormatMeasureLabelMm));
                    }
                    else
                    {
                        lines.Add("（标注后显示于此）");
                    }

                    var topPad = (float)Math.Max(12.0, fontPx * 0.25);
                    var lineH = summaryFont.GetHeight(g) * 1.22f;
                    var startY = bandY + topPad;
                    var textX = rightX + 10f;
                    var maxTextW = Math.Max(40f, rightW - 20f);

                    for (var i = 0; i < lines.Count; i++)
                    {
                        var y = startY + i * lineH;
                        if (y + lineH > src.Height - 4)
                        {
                            break;
                        }

                        var lineBrush = i == 0 ? titleBrush : summaryBrush;
                        var text = lines[i];
                        g.DrawString(
                            text,
                            summaryFont,
                            lineBrush,
                            new RectangleF(textX, y, maxTextW, lineH),
                            new StringFormat
                            {
                                Trimming = StringTrimming.EllipsisCharacter,
                                FormatFlags = StringFormatFlags.NoWrap,
                            });
                    }
                }
            }

            var outParent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outParent))
            {
                Directory.CreateDirectory(outParent);
            }

            bmp.Save(outputPath, ImageFormat.Png);
        }

        /// <summary>图上 / 底部统一格式：名称 + 毫米。</summary>
        public static string FormatMeasureLabelMm(ManualMeasureItem m) =>
            $"{m.Label}: {m.LengthMm:F4} mm";

        private static (PointF Mid, PointF LabelPos) GetPerpendicularLeader(PointF p1, PointF p2, float length)
        {
            var mid = new PointF((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f);
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var len = MathF.Sqrt(dx * dx + dy * dy);
            float nx;
            float ny;
            if (len < 1e-3f)
            {
                nx = 0f;
                ny = -1f;
            }
            else
            {
                nx = -dy / len;
                ny = dx / len;
                if (ny > 0f || (MathF.Abs(ny) < 1e-6f && nx < 0f))
                {
                    nx = -nx;
                    ny = -ny;
                }
            }

            return (mid, new PointF(mid.X + nx * length, mid.Y + ny * length));
        }

        private static void DrawCross(Graphics g, Pen pen, PointF c, float r)
        {
            g.DrawLine(pen, c.X - r, c.Y, c.X + r, c.Y);
            g.DrawLine(pen, c.X, c.Y - r, c.X, c.Y + r);
        }
    }
}
