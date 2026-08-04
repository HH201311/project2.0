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
    /// 二次手动标注：递归枚举图片、续标加载、导出 index/单图 JSON 与叠加图。
    /// 默认导出目录名为 <see cref="DefaultExportFolderName"/>（result）；可在标注界面改选任意绝对路径。
    /// </summary>
    public static class ManualMeasureExportHelper
    {
        /// <summary>默认导出子目录名（相对打开的根目录）。</summary>
        public const string DefaultExportFolderName = "result";

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

        /// <summary>默认导出目录：{rootFolder}/result</summary>
        public static string ResolveDefaultExportDir(string rootFolder) =>
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

                    // 跳过导出目录 / 预处理 input 内已生成图，避免二次打开时再扫入
                    var full = Path.GetFullPath(p);
                    if (skipDirs.Any(skip =>
                        full.StartsWith(skip + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(full, skip, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }

                    // 任意层级 …/input/…（各产品下的预处理送检图）
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

        /// <summary>单图 JSON：{exportDir}/{相对目录}/{文件名去扩展}.json</summary>
        public static string BuildImageJsonPath(string exportDir, string relativePath)
        {
            var relNoExt = Path.ChangeExtension(relativePath.Replace('/', Path.DirectorySeparatorChar), null)
                           ?? relativePath;
            return Path.Combine(NormalizeExportDir(exportDir), relNoExt + ".json");
        }

        /// <summary>叠加图：{exportDir}/{相对目录}/{文件名去扩展}_annotated.png</summary>
        public static string BuildAnnotatedImagePath(string exportDir, string relativePath)
        {
            var relNoExt = Path.ChangeExtension(relativePath.Replace('/', Path.DirectorySeparatorChar), null)
                           ?? relativePath;
            return Path.Combine(NormalizeExportDir(exportDir), relNoExt + "_annotated.png");
        }

        public static string BuildIndexJsonPath(string exportDir) =>
            Path.Combine(NormalizeExportDir(exportDir), "index.json");

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
        /// 若存在单图 JSON 则加载测量列表；否则返回空列表。
        /// <paramref name="legacyExportDir"/> 非空且主目录无数据时，回退读取历史 output。
        /// </summary>
        public static List<ManualMeasureItem> TryLoadImageMeasures(
            string exportDir,
            string relativePath,
            string? legacyExportDir = null)
        {
            var loaded = TryLoadImageMeasuresFromDir(exportDir, relativePath);
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

            return TryLoadImageMeasuresFromDir(legacyExportDir, relativePath);
        }

        public static void ExportSession(
            string rootFolder,
            double pixelSizeUm,
            IReadOnlyList<ManualMeasureImageRecord> images,
            int bottomBlackHeightPx = 0,
            string? exportDir = null)
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

            var export = new ManualMeasureSessionExport
            {
                RootFolder = Path.GetFullPath(rootFolder),
                PixelSizeUm = pixelSizeUm,
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Images = images.Select(CloneRecord).ToList(),
            };

            foreach (var img in export.Images)
            {
                EnsureLabels(img.Measures);
                foreach (var m in img.Measures)
                {
                    RecalcLengths(m, pixelSizeUm);
                }

                WriteImageJson(outDir, img);
                if (img.Measures.Count > 0 && File.Exists(img.AbsolutePath))
                {
                    RenderAnnotatedImage(
                        img.AbsolutePath,
                        BuildAnnotatedImagePath(outDir, img.RelativePath),
                        img.Measures,
                        bottomBlackHeightPx);
                }
            }

            var indexPath = BuildIndexJsonPath(outDir);
            File.WriteAllText(
                indexPath,
                JsonSerializer.Serialize(export, JsonOptions),
                Encoding.UTF8);
        }

        private static List<ManualMeasureItem> TryLoadImageMeasuresFromDir(
            string exportDir,
            string relativePath)
        {
            var jsonPath = BuildImageJsonPath(exportDir, relativePath);
            if (!File.Exists(jsonPath))
            {
                return [];
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
                    // ignore invalid path
                }
            }

            AddIfValid(ResolveDefaultExportDir(rootFull));
            AddIfValid(ResolveLegacyExportDir(rootFull));
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

        private static ManualMeasureImageRecord CloneRecord(ManualMeasureImageRecord src) =>
            new()
            {
                RelativePath = src.RelativePath,
                AbsolutePath = src.AbsolutePath,
                Measures = src.Measures.Select(m => new ManualMeasureItem
                {
                    Id = m.Id,
                    Label = m.Label,
                    X1 = m.X1,
                    Y1 = m.Y1,
                    X2 = m.X2,
                    Y2 = m.Y2,
                    PixelLength = m.PixelLength,
                    LengthUm = m.LengthUm,
                    LengthMm = m.LengthMm,
                    LabelX = m.LabelX,
                    LabelY = m.LabelY,
                }).ToList(),
            };

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

        private static void WriteImageJson(string exportDir, ManualMeasureImageRecord record)
        {
            var path = BuildImageJsonPath(exportDir, record.RelativePath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions), Encoding.UTF8);
        }

        /// <summary>
        /// 在原图上绘制测距线；上方标签仅 mm；底部按 blackHeight 打黑并汇总测距信息。
        /// </summary>
        public static void RenderAnnotatedImage(
            string sourceImagePath,
            string outputPath,
            IReadOnlyList<ManualMeasureItem> measures,
            int bottomBlackHeightPx = 0)
        {
            const float leaderLength = 42f;

            using var src = Image.FromFile(sourceImagePath);
            using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Black);
                g.DrawImage(src, 0, 0, src.Width, src.Height);

                using var pen = new Pen(Color.FromArgb(255, 0, 200, 255), 2f);
                using var leaderPen = new Pen(Color.FromArgb(220, 180, 220, 255), 1.5f)
                {
                    DashStyle = DashStyle.Dash,
                };
                using var font = new Font("Consolas", 14f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var summaryFont = new Font("Consolas", 16f, FontStyle.Bold, GraphicsUnit.Pixel);
                using var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 0));
                using var summaryBrush = new SolidBrush(Color.FromArgb(240, 255, 255, 255));
                using var bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));

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
                        (_, labelCenter) = GetPerpendicularLeader(p1, p2, leaderLength);
                    }

                    g.DrawLine(leaderPen, mid, labelCenter);

                    // 上方标签：仅毫米；中心落在 labelCenter
                    var text = FormatMeasureLabelMm(m);
                    var size = g.MeasureString(text, font);
                    var textOrigin = new PointF(labelCenter.X - size.Width / 2f, labelCenter.Y - size.Height / 2f);
                    var rect = new RectangleF(textOrigin.X - 3f, textOrigin.Y - 1f, size.Width + 6f, size.Height + 2f);
                    g.FillRectangle(bgBrush, rect);
                    g.DrawString(text, font, brush, textOrigin);

                    DrawCross(g, pen, p1, 6f);
                    DrawCross(g, pen, p2, 6f);
                }

                // 底部遮罩 + 汇总（上位机打黑，与 OfflineCtBlackHeight 一致）
                var bandH = Math.Clamp(bottomBlackHeightPx, 0, src.Height);
                if (bandH > 0)
                {
                    var bandY = src.Height - bandH;
                    g.FillRectangle(Brushes.Black, 0, bandY, src.Width, bandH);

                    var lines = measures
                        .Select(FormatMeasureLabelMm)
                        .ToList();
                    if (lines.Count == 0)
                    {
                        lines.Add("（暂无测距）");
                    }

                    var lineH = summaryFont.GetHeight(g) + 4f;
                    var startY = bandY + 8f;
                    for (var i = 0; i < lines.Count; i++)
                    {
                        var y = startY + i * lineH;
                        if (y + lineH > src.Height - 4)
                        {
                            break;
                        }

                        g.DrawString(lines[i], summaryFont, summaryBrush, 12f, y);
                    }
                }
            }

            var outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir))
            {
                Directory.CreateDirectory(outDir);
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
