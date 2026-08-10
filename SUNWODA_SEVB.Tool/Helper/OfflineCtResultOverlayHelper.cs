using System.Globalization;
using System.IO;
using HalconDotNet;
using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 将 NJCT 四参数叠到 ResultImg：AllPointRegion + L1~L5 线段 + AllPrintfValue；
    /// 底部按 OfflineCtBlackHeight <b>覆盖</b>打黑（全宽，与二次手动标注一致，不扩展画布），
    /// 遮罩四栏：L1~L5范围 / 电芯1 / 电芯2 / 手动预留；
    /// AI 汇总：名称与数值同色；开启范围→合格深绿/超差深红，未启用→白。
    /// </summary>
    public static class OfflineCtResultOverlayHelper
    {
        /// <summary>数值字号相对名称的比例（略小，下方留出下划线高度）。</summary>
        private const double ValueFontScale = 0.78;

        /// <param name="bottomInfoBlackHeight">
        /// 底部信息遮罩高度（OfflineCtBlackHeight，图像像素）。
        /// 在 ResultImg <b>底部全宽覆盖</b>黑色矩形后写字；0 则不打黑。
        /// </param>
        /// <param name="rangeCheck">L1~L5 mm 范围判断；null/未开启时线段保持青色、名称与数值保持白。</param>
        public static string WriteAnnotatedResultImage(
            HObject resultImg,
            IReadOnlyList<HObject>? pointRegions,
            IReadOnlyList<double[]> regionRows,
            IReadOnlyList<double[]> regionCols,
            IReadOnlyList<double[]> printfValues,
            string resultImagePath,
            double pixelSizeUm = 1.0,
            int bottomInfoBlackHeight = 0,
            OfflineCtLRangeCheckOptions? rangeCheck = null)
        {
            ArgumentNullException.ThrowIfNull(resultImg);
            if (string.IsNullOrWhiteSpace(resultImagePath))
            {
                throw new ArgumentException("结果图路径不能为空", nameof(resultImagePath));
            }

            rangeCheck ??= OfflineCtLRangeCheckOptions.Disabled;
            var umPerPx = pixelSizeUm > 0 ? pixelSizeUm : 1.0;

            HOperatorSet.GetImageSize(resultImg, out HTuple width, out HTuple height);
            var imgWidth = width.I;
            var imgHeight = height.I;
            // 与 ManualMeasureExportHelper 一致：clamp 到图像高度，在底部覆盖，不追加画布
            var bandH = Math.Clamp(bottomInfoBlackHeight, 0, imgHeight);
            var bandTop = imgHeight - bandH;

            HWindow? window = null;
            string? tempFile = null;
            try
            {
                window = new HWindow();
                var windowWidth = Math.Max(imgWidth, 100);
                var windowHeight = Math.Max(imgHeight, 100);
                window.OpenWindow(0, 0, windowWidth, windowHeight, new HTuple(0), "invisible", "");
                window.SetPart(0, 0, imgHeight - 1, imgWidth - 1);
                window.DispObj(resultImg);

                // ① 抓点 Region
                if (pointRegions != null)
                {
                    window.SetDraw("margin");
                    window.SetLineWidth(2);
                    foreach (var region in pointRegions)
                    {
                        if (region is null || !region.IsInitialized())
                        {
                            continue;
                        }

                        window.SetColor("green");
                        window.DispObj(region);
                    }
                }

                // ② L1~L5 竖线段 + 图上旁注（画在打黑之前，遮罩区内的旁注会被盖住，属预期）
                // 旁注：名称与数值同色（开范围→深绿/深红，未开→白）
                var labelFont = Math.Clamp(imgHeight / 55, 36, 72);
                ApplyWindowFont(window, labelFont);
                var cellCount = Math.Max(
                    regionRows?.Count ?? 0,
                    Math.Max(regionCols?.Count ?? 0, printfValues?.Count ?? 0));
                for (var cell = 0; cell < cellCount; cell++)
                {
                    var rows = regionRows != null && cell < regionRows.Count
                        ? regionRows[cell]
                        : Array.Empty<double>();
                    var cols = regionCols != null && cell < regionCols.Count
                        ? regionCols[cell]
                        : Array.Empty<double>();
                    var vals = printfValues != null && cell < printfValues.Count
                        ? printfValues[cell]
                        : Array.Empty<double>();

                    var pairCount = Math.Min(rows.Length, cols.Length) / 2;
                    for (var k = 0; k < pairCount; k++)
                    {
                        var r1 = rows[2 * k];
                        var c1 = cols[2 * k];
                        var r2 = rows[2 * k + 1];
                        var c2 = cols[2 * k + 1];

                        var mm = k < vals.Length ? vals[k] * umPerPx / 1000.0 : double.NaN;
                        var lineColor = double.IsNaN(mm)
                            ? "cyan"
                            : rangeCheck.ResolveLineColor(k, mm);

                        window.SetColor(lineColor);
                        window.SetLineWidth(2);
                        window.DispLine(r1, c1, r2, c2);

                        window.SetColor(lineColor);
                        window.DispCross(r1, c1, 12, 0);
                        window.DispCross(r2, c2, 12, 0);

                        // 旁注仅画在遮罩上方，避免被打黑盖掉
                        var midRow = (r1 + r2) / 2.0;
                        if (bandH > 0 && midRow >= bandTop)
                        {
                            continue;
                        }

                        var textCol = Math.Max(c1, c2) + 18;
                        if (textCol > imgWidth - 80)
                        {
                            textCol = Math.Min(c1, c2) - 120;
                        }

                        var sideColor = double.IsNaN(mm)
                            ? "white"
                            : rangeCheck.ResolveSideValueColor(k, mm);
                        DrawNameAndValue(
                            window,
                            midRow,
                            textCol,
                            $"L{k + 1}:",
                            double.IsNaN(mm)
                                ? string.Empty
                                : FormatPhysicalLength(vals[k], umPerPx),
                            nameColor: sideColor,
                            valueColor: sideColor,
                            fontSize: labelFont,
                            coordSystem: "image");
                    }
                }

                // ③ 底部覆盖打黑 + 汇总文字（仅 mm；数值按范围绿/红）
                if (bandH > 0)
                {
                    window.SetColor("black");
                    window.SetDraw("fill");
                    window.DispRectangle1(
                        (double)bandTop,
                        0.0,
                        imgHeight - 1.0,
                        imgWidth - 1.0);

                    DrawBottomInfoBand(
                        window,
                        imgWidth,
                        bandTop,
                        bandH,
                        printfValues,
                        umPerPx,
                        rangeCheck);
                }

                // ④ 落盘（画布尺寸与 ResultImg 相同）
                var dir = Path.GetDirectoryName(resultImagePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var ext = Path.GetExtension(resultImagePath);
                var isPng = ext.Equals(".png", StringComparison.OrdinalIgnoreCase);
                var finalPath = isPng
                    ? Path.ChangeExtension(resultImagePath, ".png")
                    : Path.ChangeExtension(resultImagePath, ".jpg");

                tempFile = Path.Combine(Path.GetTempPath(), $"njct_overlay_{Guid.NewGuid():N}.tif");
                window.DumpWindow("tiff", tempFile);

                HOperatorSet.ReadImage(out HObject dumped, tempFile);
                try
                {
                    var writeArg = Path.Combine(
                        Path.GetDirectoryName(finalPath) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(finalPath));
                    HOperatorSet.WriteImage(dumped, isPng ? "png" : "jpeg 90", 0, writeArg);
                }
                finally
                {
                    dumped.Dispose();
                }

                // 出图即记住汇总（内存），二次标注左栏立刻可显示，不落中间文件
                var summary = OfflineCtAnnotationHelper.FormatAiDimensionSummaryText(
                    printfValues,
                    umPerPx);
                OfflineCtAnnotationHelper.RememberAiDimensionSummary(finalPath, summary);

                return finalPath;
            }
            finally
            {
                try { window?.CloseWindow(); } catch { /* ignore */ }
                try { window?.Dispose(); } catch { /* ignore */ }
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { /* ignore */ }
                }

                width.Dispose();
                height.Dispose();
            }
        }

        /// <summary>
        /// 底部遮罩四栏：①L1~L5范围 ②电芯1 ③电芯2 ④右 1/4 留空给二次标注手动数据。
        /// 字号与标注页同步（OfflineCtAnnotationHelper.ComputeBottomBandFontSizeImagePx）。
        /// AI 尺寸：名称与数值同色；开启范围→深绿/深红；未开启为白色。
        /// </summary>
        private static void DrawBottomInfoBand(
            HWindow window,
            int imgWidth,
            int bandTop,
            int bandH,
            IReadOnlyList<double[]>? printfValues,
            double umPerPx,
            OfflineCtLRangeCheckOptions rangeCheck)
        {
            var fontSize = OfflineCtAnnotationHelper.ComputeBottomBandFontSizeImagePx(bandH, imgWidth);
            ApplyWindowFont(window, fontSize);

            var lineHeight = fontSize * 1.22;
            var startRow = bandTop + Math.Max(12.0, fontSize * 0.25);
            var bandBottom = bandTop + bandH - 8.0;
            // 四等分：范围 / 电芯1 / 电芯2 / 手动预留
            var colW = imgWidth / 4.0;
            var pad = Math.Max(12.0, imgWidth * 0.008);
            var col0 = pad;
            var col1 = colW + pad;
            var col2 = colW * 2.0 + pad;
            var colMax0 = colW - pad;
            var colMax1 = colW * 2.0 - pad;
            var colMax2 = colW * 3.0 - pad;

            window.DispText(
                "AI尺寸汇总",
                "window",
                startRow,
                col0,
                "orange",
                new HTuple("box"),
                new HTuple("false"));

            // ① L1~L5 配置范围
            DrawRangeColumn(
                window,
                rangeCheck,
                col0,
                colMax0,
                startRow + lineHeight,
                bandBottom,
                lineHeight,
                fontSize);

            var cellCount = printfValues?.Count ?? 0;
            if (cellCount == 0)
            {
                window.DispText(
                    "（无尺寸数据）",
                    "window",
                    startRow + lineHeight,
                    col1,
                    "white",
                    new HTuple("box"),
                    new HTuple("false"));
                return;
            }

            // ② 电芯1
            DrawCellColumn(
                window,
                printfValues!,
                cellIndex: 0,
                col: col1,
                colMax: colMax1,
                startRow + lineHeight,
                bandBottom,
                lineHeight,
                umPerPx,
                fontSize,
                rangeCheck);

            // ③ 电芯2
            if (cellCount >= 2)
            {
                DrawCellColumn(
                    window,
                    printfValues!,
                    cellIndex: 1,
                    col: col2,
                    colMax: colMax2,
                    startRow + lineHeight,
                    bandBottom,
                    lineHeight,
                    umPerPx,
                    fontSize,
                    rangeCheck);
            }

            // ④ 右栏留给二次手动标注，出图时不写
        }

        /// <summary>
        /// 第一栏：显示 L1~L5 配置范围（mm）。未启用时提示「未启用」。
        /// </summary>
        private static void DrawRangeColumn(
            HWindow window,
            OfflineCtLRangeCheckOptions rangeCheck,
            double col,
            double colMax,
            double startY,
            double bandBottom,
            double lineHeight,
            int fontSize)
        {
            var y = startY;
            window.DispText(
                "L1~L5范围",
                "window",
                y,
                col,
                "yellow",
                new HTuple("box"),
                new HTuple("false"));
            y += lineHeight;

            if (!rangeCheck.Enabled)
            {
                if (y + lineHeight <= bandBottom)
                {
                    window.DispText(
                        "（未启用）",
                        "window",
                        y,
                        col,
                        "white",
                        new HTuple("box"),
                        new HTuple("false"));
                }

                return;
            }

            for (var k = 0; k < OfflineCtLRangeCheckOptions.DimensionCount; k++)
            {
                if (y + lineHeight > bandBottom - lineHeight * 0.5)
                {
                    break;
                }

                var lo = k < rangeCheck.MinMm.Length ? rangeCheck.MinMm[k] : 0.0;
                var hi = k < rangeCheck.MaxMm.Length ? rangeCheck.MaxMm[k] : 0.0;
                if (lo > hi)
                {
                    (lo, hi) = (hi, lo);
                }

                var name = $"L{k + 1}:";
                var value = string.Create(
                    CultureInfo.InvariantCulture,
                    $" {lo:F4}~{hi:F4}mm");
                // 范围本身用白色；绿/红留给尺寸汇总数值
                DrawNameAndValue(
                    window,
                    y,
                    col,
                    name,
                    value,
                    nameColor: "white",
                    valueColor: "white",
                    fontSize: fontSize,
                    coordSystem: "window");
                y += lineHeight;
            }

            _ = colMax; // 预留裁切宽度，当前由 Halcon 自然截断
        }

        private static void DrawCellColumn(
            HWindow window,
            IReadOnlyList<double[]> printfValues,
            int cellIndex,
            double col,
            double colMax,
            double startY,
            double bandBottom,
            double lineHeight,
            double umPerPx,
            int fontSize,
            OfflineCtLRangeCheckOptions rangeCheck)
        {
            if (cellIndex < 0 || cellIndex >= printfValues.Count)
            {
                return;
            }

            var y = startY;
            window.DispText(
                $"电芯{cellIndex + 1}",
                "window",
                y,
                col,
                "yellow",
                new HTuple("box"),
                new HTuple("false"));
            y += lineHeight;

            var vals = printfValues[cellIndex] ?? Array.Empty<double>();
            for (var k = 0; k < vals.Length; k++)
            {
                if (y + lineHeight > bandBottom - lineHeight * 0.5)
                {
                    break;
                }

                var px = vals[k];
                var mm = px * umPerPx / 1000.0;
                // 仅物理长度；名称与数值同色（开范围→深绿/深红，未开→白）
                var name = $"L{k + 1}:";
                var value = " " + FormatPhysicalLength(px, umPerPx);
                var color = rangeCheck.ResolveValueColor(k, mm);

                DrawNameAndValue(
                    window,
                    y,
                    col,
                    name,
                    value,
                    nameColor: color,
                    valueColor: color,
                    fontSize: fontSize,
                    coordSystem: "window");
                y += lineHeight;
            }

            _ = colMax;
        }

        /// <summary>
        /// 将像素距离换算为物理长度文案（统一 mm），不出现 px / μm。
        /// </summary>
        private static string FormatPhysicalLength(double lengthPx, double umPerPx)
        {
            var um = lengthPx * (umPerPx > 0 ? umPerPx : 1.0);
            var mm = um / 1000.0;
            return string.Create(CultureInfo.InvariantCulture, $"{mm:F4}mm");
        }

        /// <summary>
        /// 名称与数值分两次 DispText：名称用全字号；数值略小并带下划线，
        /// 使「数值高度 + 下划线」与名称字高大致平齐；名称与数值默认同色。
        /// </summary>
        private static void DrawNameAndValue(
            HWindow window,
            double row,
            double col,
            string name,
            string value,
            string nameColor,
            string valueColor,
            int fontSize,
            string coordSystem)
        {
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(value))
            {
                return;
            }

            if (!string.IsNullOrEmpty(name))
            {
                ApplyWindowFont(window, fontSize);
                window.DispText(
                    name,
                    coordSystem,
                    row,
                    col,
                    nameColor,
                    new HTuple("box"),
                    new HTuple("false"));
            }

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var valueFont = Math.Max(12, (int)Math.Round(fontSize * ValueFontScale));
            // 下划线占用名称多出的高度，使「数值+下划线」与 L1 字高平齐
            var underlineGap = Math.Max(2.0, fontSize - valueFont);
            var underlineThickness = Math.Max(2.0, fontSize * 0.06);

            // 按字号估算「L1:」宽度，数值紧随其后
            var nameWidth = EstimateTextWidthPx(name, fontSize);
            var valueCol = col + nameWidth;
            ApplyWindowFont(window, valueFont);
            window.DispText(
                value,
                coordSystem,
                row,
                valueCol,
                valueColor,
                new HTuple("box"),
                new HTuple("false"));

            var valueWidth = EstimateTextWidthPx(value.TrimStart(), valueFont);
            // 前导空格也占宽，下划线从实际数字起笔
            var leadSpaces = 0;
            while (leadSpaces < value.Length && value[leadSpaces] == ' ')
            {
                leadSpaces++;
            }

            var underlineCol0 = valueCol + EstimateTextWidthPx(value[..leadSpaces], valueFont);
            var underlineCol1 = underlineCol0 + Math.Max(valueWidth, valueFont * 2.0);
            var underlineRow = row + valueFont + underlineGap * 0.35;
            window.SetColor(valueColor);
            window.SetLineWidth(underlineThickness);
            window.DispLine(underlineRow, underlineCol0, underlineRow, underlineCol1);

            // 恢复名称字号，避免后续同行绘制受影响
            ApplyWindowFont(window, fontSize);
        }

        private static double EstimateTextWidthPx(string text, int fontSize)
        {
            if (string.IsNullOrEmpty(text) || fontSize <= 0)
            {
                return 0;
            }

            // Times New Roman 等宽近似：字母数字约 0.55×字号
            return text.Length * fontSize * 0.55;
        }

        /// <summary>优先 Times New Roman（与 CTNCLX 相同），失败再回退。</summary>
        private static void ApplyWindowFont(HWindow window, int fontSize)
        {
            var candidates = new[]
            {
                $"Times New Roman-{fontSize}",
                $"Arial-{fontSize}",
                $"Courier New-{fontSize}",
                $"-*-courier-bold-r-*-*-{fontSize}-*-*-*-*-*-*-*",
            };
            foreach (var font in candidates)
            {
                try
                {
                    window.SetFont(font);
                    return;
                }
                catch
                {
                    // try next
                }
            }
        }
    }
}
