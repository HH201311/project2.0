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
    /// </summary>
    public static class ManualMeasureExportHelper
    {
        public const string OutputFolderName = "output";

        private static readonly string[] ImageExtensions =
            [".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"];

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static IReadOnlyList<string> EnumerateImagesRecursive(string rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            {
                return [];
            }

            var rootFull = Path.GetFullPath(rootFolder);
            var outputFull = Path.GetFullPath(Path.Combine(rootFull, OutputFolderName));

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

                    // 跳过 output 目录内已生成的叠加图，避免二次打开时再扫入
                    var full = Path.GetFullPath(p);
                    return !full.StartsWith(outputFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(full, outputFull, StringComparison.OrdinalIgnoreCase);
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

        public static string BuildOutputDir(string rootFolder) =>
            Path.Combine(rootFolder, OutputFolderName);

        /// <summary>单图 JSON 落盘路径：output/{相对目录}/{文件名去扩展}.json</summary>
        public static string BuildImageJsonPath(string rootFolder, string relativePath)
        {
            var relNoExt = Path.ChangeExtension(relativePath.Replace('/', Path.DirectorySeparatorChar), null)
                           ?? relativePath;
            return Path.Combine(BuildOutputDir(rootFolder), relNoExt + ".json");
        }

        /// <summary>叠加图：output/{相对目录}/{文件名去扩展}_annotated.png</summary>
        public static string BuildAnnotatedImagePath(string rootFolder, string relativePath)
        {
            var relNoExt = Path.ChangeExtension(relativePath.Replace('/', Path.DirectorySeparatorChar), null)
                           ?? relativePath;
            return Path.Combine(BuildOutputDir(rootFolder), relNoExt + "_annotated.png");
        }

        public static string BuildIndexJsonPath(string rootFolder) =>
            Path.Combine(BuildOutputDir(rootFolder), "index.json");

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

        /// <summary>若存在单图 JSON 则加载测量列表；否则返回空列表。</summary>
        public static List<ManualMeasureItem> TryLoadImageMeasures(string rootFolder, string relativePath)
        {
            var jsonPath = BuildImageJsonPath(rootFolder, relativePath);
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

        public static void ExportSession(
            string rootFolder,
            double pixelSizeUm,
            IReadOnlyList<ManualMeasureImageRecord> images,
            int bottomBlackHeightPx = 0)
        {
            if (string.IsNullOrWhiteSpace(rootFolder))
            {
                throw new ArgumentException("根目录不能为空", nameof(rootFolder));
            }

            var outputDir = BuildOutputDir(rootFolder);
            Directory.CreateDirectory(outputDir);

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

                WriteImageJson(rootFolder, img);
                if (img.Measures.Count > 0 && File.Exists(img.AbsolutePath))
                {
                    RenderAnnotatedImage(
                        img.AbsolutePath,
                        BuildAnnotatedImagePath(rootFolder, img.RelativePath),
                        img.Measures,
                        bottomBlackHeightPx);
                }
            }

            var indexPath = BuildIndexJsonPath(rootFolder);
            File.WriteAllText(
                indexPath,
                JsonSerializer.Serialize(export, JsonOptions),
                Encoding.UTF8);
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

        private static void WriteImageJson(string rootFolder, ManualMeasureImageRecord record)
        {
            var path = BuildImageJsonPath(rootFolder, record.RelativePath);
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

        private static PointF PlaceLabelAtLeaderEnd(PointF mid, PointF labelPos, float textW, float textH)
        {
            const float pad = 4f;
            var ox = labelPos.X - mid.X;
            var oy = labelPos.Y - mid.Y;
            var olen = MathF.Sqrt(ox * ox + oy * oy);
            if (olen < 1e-3f)
            {
                return new PointF(labelPos.X - textW / 2f, labelPos.Y - textH - pad);
            }

            var ux = ox / olen;
            var uy = oy / olen;
            var cx = labelPos.X + ux * (textW * 0.5f + pad);
            var cy = labelPos.Y + uy * (textH * 0.5f + pad);
            return new PointF(cx - textW / 2f, cy - textH / 2f);
        }

        private static void DrawCross(Graphics g, Pen pen, PointF c, float r)
        {
            g.DrawLine(pen, c.X - r, c.Y, c.X + r, c.Y);
            g.DrawLine(pen, c.X, c.Y - r, c.X, c.Y + r);
        }
    }
}
