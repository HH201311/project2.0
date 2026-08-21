using System.Globalization;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// CT 尺寸汇总 CSV：产品尺寸（AI）与手动测距；UTF-8 BOM，按产品名 upsert，便于追加与 Excel 打开。
    /// </summary>
    public static class OfflineCtCsvExportHelper
    {
        public const string ProductDimensionFileName = "CT产品尺寸.csv";
        public const string ManualMeasureFileName = "CT手动测距.csv";

        public static readonly string[] ProductDimensionHeaders =
        [
            "产品名", "标注时间", "像素精度_um_per_px", "范围启用",
            "L1下限_mm", "L1上限_mm", "L2下限_mm", "L2上限_mm", "L3下限_mm", "L3上限_mm",
            "L4下限_mm", "L4上限_mm", "L5下限_mm", "L5上限_mm",
            "电芯1_L1_mm", "电芯1_L2_mm", "电芯1_L3_mm", "电芯1_L4_mm", "电芯1_L5_mm",
            "电芯1_L1判定", "电芯1_L2判定", "电芯1_L3判定", "电芯1_L4判定", "电芯1_L5判定",
            "电芯2_L1_mm", "电芯2_L2_mm", "电芯2_L3_mm", "电芯2_L4_mm", "电芯2_L5_mm",
            "电芯2_L1判定", "电芯2_L2判定", "电芯2_L3判定", "电芯2_L4判定", "电芯2_L5判定",
            "产品判定", "结果图路径",
        ];

        public static readonly string[] ManualMeasureHeaders =
        [
            "产品名", "测距名", "长度_mm", "长度_px", "X1", "Y1", "X2", "Y2", "导出时间",
        ];

        public static string BuildProductDimensionCsvPath(string folder) =>
            Path.Combine(NormalizeDir(folder), ProductDimensionFileName);

        public static string BuildManualMeasureCsvPath(string folder) =>
            Path.Combine(NormalizeDir(folder), ManualMeasureFileName);

        /// <summary>从相对路径解析产品名：优先第一级目录名，否则文件名去扩展。</summary>
        public static string ResolveProductName(string? relativeOrFilePath)
        {
            if (string.IsNullOrWhiteSpace(relativeOrFilePath))
            {
                return "unknown";
            }

            var normalized = relativeOrFilePath.Replace('\\', '/').Trim('/');
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts[0];
            }

            return Path.GetFileNameWithoutExtension(parts[0]);
        }

        public static OfflineCtProductDimensionCsvRow BuildProductRow(
            string productName,
            double pixelSizeUm,
            OfflineCtLRangeCheckOptions rangeCheck,
            IReadOnlyList<double[]>? printfValuesPx,
            string? resultImagePath,
            DateTime? generatedAt = null)
        {
            var um = pixelSizeUm > 0 ? pixelSizeUm : 1.0;
            var cell1 = ExtractCellMm(printfValuesPx, 0, um);
            var cell2 = ExtractCellMm(printfValuesPx, 1, um);
            var judge1 = BuildJudges(cell1, rangeCheck);
            var judge2 = BuildJudges(cell2, rangeCheck);
            var productJudge = "—";
            if (rangeCheck.Enabled)
            {
                var anyNg = judge1.Concat(judge2).Any(j => j == "NG");
                var anyOk = judge1.Concat(judge2).Any(j => j == "OK");
                productJudge = anyNg ? "NG" : anyOk ? "OK" : "—";
            }

            return new OfflineCtProductDimensionCsvRow
            {
                ProductName = productName,
                GeneratedAt = (generatedAt ?? DateTime.Now).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                PixelSizeUm = um,
                RangeEnabled = rangeCheck.Enabled ? "1" : "0",
                MinMm = Pad5(rangeCheck.MinMm),
                MaxMm = Pad5(rangeCheck.MaxMm),
                Cell1Mm = cell1,
                Cell1Judge = judge1,
                Cell2Mm = cell2,
                Cell2Judge = judge2,
                ProductJudge = productJudge,
                ResultImagePath = resultImagePath ?? string.Empty,
            };
        }

        public static void UpsertProductDimensionRow(string csvFolderOrPath, OfflineCtProductDimensionCsvRow row)
        {
            var path = ResolveCsvPath(csvFolderOrPath, ProductDimensionFileName);
            var rows = ReadDataRows(path, ProductDimensionHeaders.Length);
            var key = row.ProductName?.Trim() ?? string.Empty;
            var replaced = false;
            for (var i = 0; i < rows.Count; i++)
            {
                if (string.Equals(GetField(rows[i], 0), key, StringComparison.OrdinalIgnoreCase))
                {
                    rows[i] = row.ToFields();
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                rows.Add(row.ToFields());
            }

            WriteAll(path, ProductDimensionHeaders, rows);
        }

        /// <summary>
        /// 替换指定产品的手动测距行后写回；其它产品行保留。
        /// </summary>
        public static void ReplaceManualMeasures(
            string csvFolderOrPath,
            IReadOnlyCollection<string> productNames,
            IReadOnlyList<string[]> newRowsForThoseProducts)
        {
            var path = ResolveCsvPath(csvFolderOrPath, ManualMeasureFileName);
            var existing = ReadDataRows(path, ManualMeasureHeaders.Length);
            var nameSet = new HashSet<string>(
                productNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()),
                StringComparer.OrdinalIgnoreCase);

            var kept = existing
                .Where(r => !nameSet.Contains(GetField(r, 0)))
                .ToList();
            kept.AddRange(newRowsForThoseProducts);
            WriteAll(path, ManualMeasureHeaders, kept);
        }

        public static string[] BuildManualMeasureFields(
            string productName,
            ManualMeasureItem m,
            string exportedAt) =>
            [
                productName,
                m.Label ?? string.Empty,
                m.LengthMm.ToString("F4", CultureInfo.InvariantCulture),
                m.PixelLength.ToString("F2", CultureInfo.InvariantCulture),
                m.X1.ToString("F2", CultureInfo.InvariantCulture),
                m.Y1.ToString("F2", CultureInfo.InvariantCulture),
                m.X2.ToString("F2", CultureInfo.InvariantCulture),
                m.Y2.ToString("F2", CultureInfo.InvariantCulture),
                exportedAt,
            ];

        /// <summary>从手动测距 CSV 按产品名加载（续标）。</summary>
        public static List<ManualMeasureItem> TryLoadManualMeasures(string csvFolderOrPath, string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return [];
            }

            var all = LoadAllManualMeasuresByProduct(csvFolderOrPath);
            return all.TryGetValue(productName.Trim(), out var list)
                ? CloneMeasures(list)
                : [];
        }

        /// <summary>
        /// 一次读取手动测距 CSV，按产品名建索引（大批量打开标注窗时避免每图重读整表）。
        /// </summary>
        public static Dictionary<string, List<ManualMeasureItem>> LoadAllManualMeasuresByProduct(
            string csvFolderOrPath)
        {
            var map = new Dictionary<string, List<ManualMeasureItem>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(csvFolderOrPath))
            {
                return map;
            }

            string path;
            try
            {
                path = ResolveCsvPath(csvFolderOrPath, ManualMeasureFileName);
            }
            catch
            {
                return map;
            }

            if (!File.Exists(path))
            {
                return map;
            }

            var rows = ReadDataRows(path, ManualMeasureHeaders.Length);
            foreach (var r in rows)
            {
                var product = GetField(r, 0).Trim();
                if (string.IsNullOrEmpty(product))
                {
                    continue;
                }

                if (!TryParseMeasureRow(r, out var item))
                {
                    continue;
                }

                if (!map.TryGetValue(product, out var list))
                {
                    list = [];
                    map[product] = list;
                }

                list.Add(item);
            }

            return map;
        }

        /// <summary>合并多份 CSV 索引；后者同名产品不覆盖已有（主目录优先）。</summary>
        public static void MergeManualMeasureLookup(
            Dictionary<string, List<ManualMeasureItem>> primary,
            IReadOnlyDictionary<string, List<ManualMeasureItem>> secondary)
        {
            foreach (var kv in secondary)
            {
                if (primary.ContainsKey(kv.Key) && primary[kv.Key].Count > 0)
                {
                    continue;
                }

                primary[kv.Key] = CloneMeasures(kv.Value);
            }
        }

        private static bool TryParseMeasureRow(string[] r, out ManualMeasureItem item)
        {
            item = null!;
            if (!TryParseDouble(GetField(r, 4), out var x1)
                || !TryParseDouble(GetField(r, 5), out var y1)
                || !TryParseDouble(GetField(r, 6), out var x2)
                || !TryParseDouble(GetField(r, 7), out var y2))
            {
                return false;
            }

            TryParseDouble(GetField(r, 2), out var mm);
            TryParseDouble(GetField(r, 3), out var px);
            item = new ManualMeasureItem
            {
                Label = GetField(r, 1),
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                LengthMm = mm,
                PixelLength = px,
                LengthUm = mm * 1000.0,
            };
            return true;
        }

        private static List<ManualMeasureItem> CloneMeasures(IReadOnlyList<ManualMeasureItem> source)
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

        private static double[] ExtractCellMm(IReadOnlyList<double[]>? printfValuesPx, int cellIndex, double umPerPx)
        {
            var mm = new double[OfflineCtLRangeCheckOptions.DimensionCount];
            for (var i = 0; i < mm.Length; i++)
            {
                mm[i] = double.NaN;
            }

            if (printfValuesPx is null || cellIndex < 0 || cellIndex >= printfValuesPx.Count)
            {
                return mm;
            }

            var src = printfValuesPx[cellIndex];
            if (src is null)
            {
                return mm;
            }

            for (var i = 0; i < mm.Length && i < src.Length; i++)
            {
                mm[i] = src[i] * umPerPx / 1000.0;
            }

            return mm;
        }

        private static string[] BuildJudges(double[] cellMm, OfflineCtLRangeCheckOptions rangeCheck)
        {
            var judges = new string[OfflineCtLRangeCheckOptions.DimensionCount];
            for (var i = 0; i < judges.Length; i++)
            {
                if (double.IsNaN(cellMm[i]))
                {
                    judges[i] = "—";
                    continue;
                }

                judges[i] = rangeCheck.IsInRange(i, cellMm[i]) switch
                {
                    true => "OK",
                    false => "NG",
                    null => "—",
                };
            }

            return judges;
        }

        private static double[] Pad5(double[]? src)
        {
            var a = new double[OfflineCtLRangeCheckOptions.DimensionCount];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = src is not null && i < src.Length ? src[i] : 0;
            }

            return a;
        }

        private static string ResolveCsvPath(string folderOrPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(folderOrPath))
            {
                throw new ArgumentException("CSV 目录不能为空", nameof(folderOrPath));
            }

            var trimmed = folderOrPath.Trim();
            if (trimmed.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(trimmed);
            }

            return Path.Combine(NormalizeDir(trimmed), fileName);
        }

        private static string NormalizeDir(string dir) =>
            Path.GetFullPath(dir.Trim())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static List<string[]> ReadDataRows(string path, int expectedMinCols)
        {
            if (!File.Exists(path))
            {
                return [];
            }

            try
            {
                var lines = File.ReadAllLines(path, Encoding.UTF8);
                var rows = new List<string[]>();
                var start = 0;
                if (lines.Length > 0 && LooksLikeHeader(lines[0]))
                {
                    start = 1;
                }

                for (var i = start; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        continue;
                    }

                    var fields = ParseCsvLine(lines[i]);
                    if (fields.Length < 1)
                    {
                        continue;
                    }

                    if (fields.Length < expectedMinCols)
                    {
                        var padded = new string[expectedMinCols];
                        Array.Copy(fields, padded, fields.Length);
                        for (var k = fields.Length; k < padded.Length; k++)
                        {
                            padded[k] = string.Empty;
                        }

                        fields = padded;
                    }

                    rows.Add(fields);
                }

                return rows;
            }
            catch (IOException ex)
            {
                throw new IOException(
                    $"无法读取 CSV（可能正被 Excel 占用），请关闭后重试：{path}\n{ex.Message}",
                    ex);
            }
        }

        private static bool LooksLikeHeader(string line) =>
            line.Contains("产品名", StringComparison.Ordinal)
            || line.Contains("Product", StringComparison.OrdinalIgnoreCase);

        private static void WriteAll(string path, string[] headers, IReadOnlyList<string[]> rows)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var tmp = path + ".tmp";
            try
            {
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
                {
                    writer.WriteLine(ToCsvLine(headers));
                    foreach (var row in rows)
                    {
                        writer.WriteLine(ToCsvLine(row));
                    }
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, destinationBackupFileName: null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                    catch (IOException)
                    {
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch (IOException ex)
            {
                try
                {
                    if (File.Exists(tmp))
                    {
                        File.Delete(tmp);
                    }
                }
                catch
                {
                    // ignore
                }

                throw new IOException(
                    $"无法写入 CSV（可能正被 Excel 占用），请关闭后重试：{path}\n{ex.Message}",
                    ex);
            }
        }

        private static string GetField(string[] row, int index) =>
            index >= 0 && index < row.Length ? row[index]?.Trim() ?? string.Empty : string.Empty;

        private static bool TryParseDouble(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

        private static string ToCsvLine(IReadOnlyList<string> fields)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(EscapeCsv(fields[i] ?? string.Empty));
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"')
                || value.Contains(',')
                || value.Contains('\r')
                || value.Contains('\n'))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        private static string[] ParseCsvLine(string line)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            list.Add(sb.ToString());
            return list.ToArray();
        }
    }

    /// <summary>CT产品尺寸.csv 一行。</summary>
    public sealed class OfflineCtProductDimensionCsvRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string GeneratedAt { get; set; } = string.Empty;
        public double PixelSizeUm { get; set; }
        public string RangeEnabled { get; set; } = "0";
        public double[] MinMm { get; set; } = new double[5];
        public double[] MaxMm { get; set; } = new double[5];
        public double[] Cell1Mm { get; set; } = new double[5];
        public string[] Cell1Judge { get; set; } = ["—", "—", "—", "—", "—"];
        public double[] Cell2Mm { get; set; } = new double[5];
        public string[] Cell2Judge { get; set; } = ["—", "—", "—", "—", "—"];
        public string ProductJudge { get; set; } = "—";
        public string ResultImagePath { get; set; } = string.Empty;

        public string[] ToFields()
        {
            string F(double v) =>
                double.IsNaN(v) ? string.Empty : v.ToString("F4", CultureInfo.InvariantCulture);

            string[] Five(double[] a) =>
                [F(a[0]), F(a[1]), F(a[2]), F(a[3]), F(a[4])];

            // 表头为 L1下限,L1上限,L2下限,L2上限…
            var rangePairs = new string[10];
            for (var i = 0; i < 5; i++)
            {
                rangePairs[i * 2] = F(MinMm[i]);
                rangePairs[i * 2 + 1] = F(MaxMm[i]);
            }

            return
            [
                ProductName,
                GeneratedAt,
                PixelSizeUm.ToString("G", CultureInfo.InvariantCulture),
                RangeEnabled,
                ..rangePairs,
                ..Five(Cell1Mm),
                ..Cell1Judge,
                ..Five(Cell2Mm),
                ..Cell2Judge,
                ProductJudge,
                ResultImagePath,
            ];
        }
    }
}
