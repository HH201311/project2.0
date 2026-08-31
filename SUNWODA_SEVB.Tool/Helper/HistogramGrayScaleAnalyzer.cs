using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 直方图目标色→纯绿替换、峰谷/半坡检测与调试图输出。
    /// </summary>
    public static class HistogramGrayScaleAnalyzer
    {
        /// <summary>替换后的标记色（纯绿），后续只认此色为波形填充。</summary>
        public static readonly Color MarkerGreen = Color.FromArgb(255, 0, 255, 0);

        private static readonly Regex HexRegex = new(
            @"^#?([0-9A-Fa-f]{6})$",
            RegexOptions.Compiled);

        public static bool TryParseHexColor(string? hex, out Color color)
        {
            color = Color.Empty;
            if (string.IsNullOrWhiteSpace(hex))
            {
                return false;
            }

            var m = HexRegex.Match(hex.Trim());
            if (!m.Success)
            {
                return false;
            }

            var v = m.Groups[1].Value;
            var r = int.Parse(v[..2], NumberStyles.HexNumber);
            var g = int.Parse(v.Substring(2, 2), NumberStyles.HexNumber);
            var b = int.Parse(v.Substring(4, 2), NumberStyles.HexNumber);
            color = Color.FromArgb(255, r, g, b);
            return true;
        }

        public static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        public static HistogramOptimizeResult Optimize(
            Bitmap source,
            Color maskColor,
            int tolerance,
            string? compareSavePath = null,
            string? peaksSavePath = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            tolerance = Math.Clamp(tolerance, 0, 64);

            // 1) 目标色强制替换为纯绿；其余像素保留原图
            using var recolored = ReplaceTargetColorWithGreen(
                source,
                maskColor,
                tolerance,
                out var mask,
                out var rawMaskPixels);

            var width = source.Width;
            var height = source.Height;

            // 2) 去掉「从左到右近全宽」的绿色横带（背景渐变误染）；波形会被暂时切断
            //    去带前记录每列原底部，供下填接回时使用（避免绿柱灌到坐标轴区）
            var colBottoms = CaptureColumnBottoms(mask, width, height);
            RemoveFullWidthGreenBands(mask, width, height, fillRatio: 0.90);
            ClearMaskHolesOnRecolor(recolored, mask);

            // 3) 自上而下：碰到绿色后，向下填到该列原底部，把断层接回成实心波形
            FillDownSolidifyGreen(mask, recolored, width, height, colBottoms);

            var maskPixelCount = CountMask(mask, width, height);
            var heights = BuildColumnHeightsFromBottom(mask, width, height);
            DespikeColumnHeights(heights);
            MedianFilterInPlace(heights, radius: 1);

            var peaks = FindBimodalHalfSlope(heights);

            using var compare = BuildSideBySideCompare(source, recolored);
            if (peaks.Success)
            {
                AnnotatePeaksOnAfterPanel(compare, source.Width, peaks);
            }

            string? savedCompare = null;
            if (!string.IsNullOrWhiteSpace(compareSavePath))
            {
                EnsureParentDir(compareSavePath);
                compare.Save(compareSavePath, ImageFormat.Png);
                savedCompare = compareSavePath;
            }

            string? savedPeaks = null;
            if (!string.IsNullOrWhiteSpace(peaksSavePath))
            {
                using var peaksImg = BuildPeaksDebugImage(source, recolored, heights, peaks);
                EnsureParentDir(peaksSavePath);
                peaksImg.Save(peaksSavePath, ImageFormat.Png);
                savedPeaks = peaksSavePath;
            }

            return new HistogramOptimizeResult(
                heights,
                maskPixelCount,
                rawMaskPixels,
                savedCompare,
                savedPeaks,
                ToHex(maskColor),
                tolerance,
                peaks);
        }

        /// <summary>
        /// 将接近目标色的像素强制替换为纯绿，并生成「仅纯绿」布尔掩膜。
        /// </summary>
        private static Bitmap ReplaceTargetColorWithGreen(
            Bitmap source,
            Color target,
            int tolerance,
            out bool[,] mask,
            out int replacedCount)
        {
            var width = source.Width;
            var height = source.Height;
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            mask = new bool[width, height];
            replacedCount = 0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = source.GetPixel(x, y);
                    if (IsNear(p, target, tolerance))
                    {
                        result.SetPixel(x, y, MarkerGreen);
                        mask[x, y] = true;
                        replacedCount++;
                    }
                    else
                    {
                        result.SetPixel(x, y, p);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 掩膜被清掉的位置，替换图上对应像素置黑，避免仍显示绿色造成错觉。
        /// </summary>
        private static void ClearMaskHolesOnRecolor(Bitmap recolored, bool[,] mask)
        {
            var width = mask.GetLength(0);
            var height = mask.GetLength(1);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        continue;
                    }

                    var p = recolored.GetPixel(x, y);
                    if (p.R == MarkerGreen.R && p.G == MarkerGreen.G && p.B == MarkerGreen.B)
                    {
                        recolored.SetPixel(x, y, Color.Black);
                    }
                }
            }
        }

        /// <summary>
        /// 取高度最高的两个尖峰；左线=两峰间波谷，右线=右侧尖峰，目标线=左线到右线的 3/4 处（靠右约四分之一）。
        /// </summary>
        public static HistogramPeakAnalysis FindBimodalHalfSlope(int[] heights)
        {
            if (heights == null || heights.Length < 16)
            {
                return HistogramPeakAnalysis.Fail("柱高序列过短");
            }

            var n = heights.Length;
            var maxH = heights.Max();
            if (maxH <= 0)
            {
                return HistogramPeakAnalysis.Fail("柱高全为 0");
            }

            // 尖峰阈值；峰距允许较近（双尖峰常紧挨）
            var minPeakH = Math.Max(3, (int)(maxH * 0.18));
            var minSep = Math.Max(4, n / 80);
            var mergeDist = Math.Max(3, minSep / 2);

            // 平台顶也算尖尖：允许左右相等，但至少一侧严格更低
            var candidates = new List<int>();
            for (var i = 1; i < n - 1; i++)
            {
                if (heights[i] < minPeakH)
                {
                    continue;
                }

                var left = heights[i - 1];
                var right = heights[i + 1];
                if (heights[i] >= left
                    && heights[i] >= right
                    && (heights[i] > left || heights[i] > right))
                {
                    candidates.Add(i);
                }
            }

            candidates = MergeNearbyPeaks(candidates, heights, mergeDist);
            if (candidates.Count < 2)
            {
                return HistogramPeakAnalysis.Fail($"显著尖峰不足（候选 {candidates.Count}）");
            }

            // 按高度取最高的两个尖尖（同高时取更尖/更左者已由合并保证）
            if (!TryPickTwoHighestPeaks(candidates, heights, minSep, out var tipA, out var tipB))
            {
                return HistogramPeakAnalysis.Fail("未能选出间距足够的两个最高尖峰");
            }

            var leftTip = Math.Min(tipA, tipB);
            var rightTip = Math.Max(tipA, tipB);

            // 波谷：两尖峰之间（不含端点优先）的最低点
            var valley = leftTip;
            var valleyH = heights[leftTip];
            for (var i = leftTip; i <= rightTip; i++)
            {
                if (heights[i] < valleyH)
                {
                    valleyH = heights[i];
                    valley = i;
                }
            }

            // 若谷落在端点（几乎无凹陷），在开区间再找一次
            if (valley == leftTip || valley == rightTip)
            {
                if (rightTip - leftTip >= 2)
                {
                    valley = leftTip + 1;
                    valleyH = heights[valley];
                    for (var i = leftTip + 1; i < rightTip; i++)
                    {
                        if (heights[i] < valleyH)
                        {
                            valleyH = heights[i];
                            valley = i;
                        }
                    }
                }
            }

            // 左线=波谷，右线=右侧尖峰，目标线=靠右约 1/4（左→右 3/4 处），更贴近波峰以压掉左侧噪点灰阶
            var leftLine = valley;
            var rightLine = rightTip;
            var midLine = leftLine + (int)Math.Round((rightLine - leftLine) * (3.0 / 4.0));

            return new HistogramPeakAnalysis(
                true,
                null,
                leftTip,
                heights[leftTip],
                leftLine,
                heights[leftLine],
                rightLine,
                heights[rightLine],
                midLine);
        }

        /// <summary>
        /// 在候选峰中按高度取最高的两个，且横向间距 ≥ minSep。
        /// </summary>
        private static bool TryPickTwoHighestPeaks(
            List<int> candidates,
            int[] heights,
            int minSep,
            out int peakA,
            out int peakB)
        {
            peakA = -1;
            peakB = -1;
            var ordered = candidates
                .OrderByDescending(i => heights[i])
                .ThenBy(i => i)
                .ToList();

            peakA = ordered[0];
            for (var i = 1; i < ordered.Count; i++)
            {
                var b = ordered[i];
                if (Math.Abs(b - peakA) >= minSep)
                {
                    peakB = b;
                    return true;
                }
            }

            // 最高峰附近被同高簇挡住时，退而求次高对：在高度排序中找第一对间距够的
            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    if (Math.Abs(ordered[j] - ordered[i]) >= minSep)
                    {
                        peakA = ordered[i];
                        peakB = ordered[j];
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<int> MergeNearbyPeaks(List<int> peaks, int[] heights, int mergeDist)
        {
            if (peaks.Count == 0)
            {
                return peaks;
            }

            var sorted = peaks.OrderBy(p => p).ToList();
            var merged = new List<int> { sorted[0] };
            for (var i = 1; i < sorted.Count; i++)
            {
                var prev = merged[^1];
                var cur = sorted[i];
                if (cur - prev <= mergeDist)
                {
                    if (heights[cur] > heights[prev])
                    {
                        merged[^1] = cur;
                    }
                }
                else
                {
                    merged.Add(cur);
                }
            }

            return merged;
        }

        private static bool IsNear(Color a, Color b, int tolerance)
        {
            return Math.Abs(a.R - b.R) <= tolerance
                && Math.Abs(a.G - b.G) <= tolerance
                && Math.Abs(a.B - b.B) <= tolerance;
        }

        private static int CountMask(bool[,] mask, int width, int height)
        {
            var n = 0;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        /// <summary>
        /// 记录每列自顶向下最后一个绿色行（去横带前的波形底边）。
        /// </summary>
        private static int[] CaptureColumnBottoms(bool[,] mask, int width, int height)
        {
            var bottoms = new int[width];
            for (var x = 0; x < width; x++)
            {
                bottoms[x] = -1;
                for (var y = height - 1; y >= 0; y--)
                {
                    if (!mask[x, y])
                    {
                        continue;
                    }

                    bottoms[x] = y;
                    break;
                }
            }

            return bottoms;
        }

        /// <summary>
        /// 删除近全宽绿色横带（背景与填充同色时的误染）。不限厚度——粗横带也删；
        /// 波形主体被切断后由 <see cref="FillDownSolidifyGreen"/> 接回。
        /// </summary>
        private static void RemoveFullWidthGreenBands(
            bool[,] mask,
            int width,
            int height,
            double fillRatio)
        {
            var threshold = (int)(width * fillRatio);
            for (var y = 0; y < height; y++)
            {
                var count = 0;
                for (var x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        count++;
                    }
                }

                if (count < threshold)
                {
                    continue;
                }

                for (var x = 0; x < width; x++)
                {
                    mask[x, y] = false;
                }
            }
        }

        /// <summary>
        /// 按列自上而下扫描：首次碰到绿色后，向下填绿直到该列去带前的底边，
        /// 用于填平横带造成的断层。若无历史底边，则填到图像底（兼容纯尖顶残留）。
        /// </summary>
        private static void FillDownSolidifyGreen(
            bool[,] mask,
            Bitmap recolored,
            int width,
            int height,
            int[] colBottoms)
        {
            // 全局底边兜底：有波形的列底边中位数，避免个别列无底时乱填
            var known = colBottoms.Where(b => b >= 0).OrderBy(b => b).ToList();
            var fallbackBottom = known.Count > 0
                ? known[known.Count / 2]
                : height - 1;

            for (var x = 0; x < width; x++)
            {
                var top = -1;
                for (var y = 0; y < height; y++)
                {
                    if (!mask[x, y])
                    {
                        continue;
                    }

                    top = y;
                    break;
                }

                if (top < 0)
                {
                    continue;
                }

                var end = colBottoms[x] >= top ? colBottoms[x] : Math.Max(top, fallbackBottom);
                end = Math.Min(end, height - 1);
                for (var y = top; y <= end; y++)
                {
                    mask[x, y] = true;
                    recolored.SetPixel(x, y, MarkerGreen);
                }
            }
        }

        /// <summary>
        /// 自底向上连续命中长度；跳过底部空洞，遇间隙停止。
        /// 允许 1px 细缝（LUT 曲线划痕），更大间隙视为峰顶已断。
        /// </summary>
        private static int[] BuildColumnHeightsFromBottom(bool[,] mask, int width, int height)
        {
            var heights = new int[width];
            const int maxGap = 1;
            for (var x = 0; x < width; x++)
            {
                var h = 0;
                var gap = 0;
                var started = false;
                for (var y = height - 1; y >= 0; y--)
                {
                    if (mask[x, y])
                    {
                        started = true;
                        gap = 0;
                        h++;
                        continue;
                    }

                    if (!started)
                    {
                        continue;
                    }

                    gap++;
                    if (gap > maxGap)
                    {
                        break;
                    }

                    // 细缝计入柱高，保持与连续填充一致的顶高
                    h++;
                }

                heights[x] = h;
            }

            return heights;
        }

        private static void DespikeColumnHeights(int[] heights)
        {
            if (heights.Length < 3)
            {
                return;
            }

            var maxH = heights.Max();
            var delta = Math.Max(8, (int)(maxH * 0.08));
            var copy = (int[])heights.Clone();
            for (var x = 1; x < heights.Length - 1; x++)
            {
                var neighborMax = Math.Max(copy[x - 1], copy[x + 1]);
                if (copy[x] > neighborMax + delta)
                {
                    heights[x] = (copy[x - 1] + copy[x + 1]) / 2;
                }
            }
        }

        private static void MedianFilterInPlace(int[] data, int radius)
        {
            if (data.Length == 0 || radius <= 0)
            {
                return;
            }

            var src = (int[])data.Clone();
            var window = new int[radius * 2 + 1];
            for (var i = 0; i < data.Length; i++)
            {
                var n = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    var idx = Math.Clamp(i + k, 0, src.Length - 1);
                    window[n++] = src[idx];
                }

                Array.Sort(window, 0, n);
                data[i] = window[n / 2];
            }
        }

        private static void GaussianSmoothInPlace(int[] data, int radius)
        {
            if (data.Length == 0 || radius <= 0)
            {
                return;
            }

            var src = (int[])data.Clone();
            var sigma = radius / 2.0;
            var kernel = new double[radius * 2 + 1];
            double sumK = 0;
            for (var i = -radius; i <= radius; i++)
            {
                var v = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = v;
                sumK += v;
            }

            for (var i = 0; i < kernel.Length; i++)
            {
                kernel[i] /= sumK;
            }

            for (var i = 0; i < data.Length; i++)
            {
                double acc = 0;
                for (var k = -radius; k <= radius; k++)
                {
                    var idx = Math.Clamp(i + k, 0, src.Length - 1);
                    acc += src[idx] * kernel[k + radius];
                }

                data[i] = (int)Math.Round(acc);
            }
        }

        private static Bitmap BuildSideBySideCompare(Bitmap before, Bitmap after)
        {
            const int gap = 16;
            const int labelH = 36;
            const int pad = 12;
            var w = before.Width;
            var h = before.Height;
            var outW = pad * 2 + w * 2 + gap;
            var outH = pad * 2 + labelH + h;

            var bmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(255, 30, 30, 30));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var titleFont = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            using var titleBrush = new SolidBrush(Color.White);
            g.DrawString("优化前（原截图）", titleFont, titleBrush, pad, pad);
            g.DrawString("优化后（去全宽绿带+下填）", titleFont, titleBrush, pad + w + gap, pad);

            var yImg = pad + labelH;
            g.DrawImage(before, pad, yImg, w, h);
            g.DrawImage(after, pad + w + gap, yImg, w, h);

            using var borderPen = new Pen(Color.FromArgb(255, 80, 80, 80), 1);
            g.DrawRectangle(borderPen, pad, yImg, w - 1, h - 1);
            g.DrawRectangle(borderPen, pad + w + gap, yImg, w - 1, h - 1);

            return bmp;
        }

        private static void AnnotatePeaksOnAfterPanel(Bitmap compare, int panelWidth, HistogramPeakAnalysis peaks)
        {
            const int gap = 16;
            const int labelH = 36;
            const int pad = 12;
            var yImg = pad + labelH;
            var x0 = pad + panelWidth + gap;

            using var g = Graphics.FromImage(compare);
            DrawPeakMarkers(g, x0, yImg, compare.Height - yImg - pad, peaks);
        }

        private static Bitmap BuildPeaksDebugImage(
            Bitmap original,
            Bitmap cleaned,
            int[] heights,
            HistogramPeakAnalysis peaks)
        {
            const int pad = 12;
            const int labelH = 28;
            const int curveH = 120;
            const int gap = 12;
            var w = original.Width;
            var h = original.Height;
            var outW = pad * 2 + w;
            var outH = pad * 2 + labelH + h + gap + curveH + labelH;

            var bmp = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(255, 24, 24, 24));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using var titleFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
            using var smallFont = new Font("Consolas", 9f);
            using var white = new SolidBrush(Color.White);

            g.DrawString("左线=两尖峰间波谷 / 右线=右侧尖峰 / 目标线=靠右约1/4(3/4处)", titleFont, white, pad, pad);
            var yImg = pad + labelH;
            g.DrawImage(cleaned, pad, yImg, w, h);
            DrawPeakMarkers(g, pad, yImg, h, peaks);

            var yCurve = yImg + h + gap;
            g.DrawString(
                peaks.Success
                    ? $"尖峰P1={peaks.LeftPeakX} P2侧={peaks.RightPeakX} | 左线(谷)={peaks.ValleyX} 右线={peaks.RightPeakX} 目标={peaks.TargetX}"
                    : $"峰谷失败：{peaks.FailReason}",
                smallFont,
                white,
                pad,
                yCurve);

            yCurve += labelH;
            DrawHeightCurve(g, pad, yCurve, w, curveH, heights, peaks);
            return bmp;
        }

        private static void DrawPeakMarkers(Graphics g, int x0, int y0, int panelH, HistogramPeakAnalysis peaks)
        {
            if (!peaks.Success)
            {
                return;
            }

            void VLine(int col, Color color, string tag, float width = 2f)
            {
                using var pen = new Pen(color, width);
                g.DrawLine(pen, x0 + col, y0, x0 + col, y0 + panelH - 1);
                using var brush = new SolidBrush(color);
                using var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
                g.DrawString(tag, font, brush, x0 + col + 2, y0 + 4);
            }

            // 两尖峰参考（细线）
            if (peaks.LeftPeakX != peaks.ValleyX)
            {
                VLine(peaks.LeftPeakX, Color.FromArgb(160, 0, 200, 0), "P1", 1f);
            }

            // 左线=波谷，右线=右侧尖峰，目标线=靠右约1/4（业务线，粗）
            VLine(peaks.ValleyX, Color.Yellow, "左");
            VLine(peaks.RightPeakX, Color.Cyan, "右");
            VLine(peaks.TargetX, Color.Magenta, "目标");
        }

        private static void DrawHeightCurve(
            Graphics g,
            int x0,
            int y0,
            int width,
            int height,
            int[] heights,
            HistogramPeakAnalysis peaks)
        {
            using var bg = new SolidBrush(Color.FromArgb(255, 40, 40, 40));
            g.FillRectangle(bg, x0, y0, width, height);
            var maxH = Math.Max(1, heights.Max());
            using var pen = new Pen(Color.LightGray, 1);
            for (var i = 1; i < heights.Length; i++)
            {
                var x1 = x0 + (i - 1) * width / Math.Max(1, heights.Length - 1);
                var x2 = x0 + i * width / Math.Max(1, heights.Length - 1);
                var y1 = y0 + height - 1 - heights[i - 1] * (height - 2) / maxH;
                var y2 = y0 + height - 1 - heights[i] * (height - 2) / maxH;
                g.DrawLine(pen, x1, y1, x2, y2);
            }

            if (peaks.Success)
            {
                void Mark(int col, Color c)
                {
                    var x = x0 + col * width / Math.Max(1, heights.Length - 1);
                    using var p = new Pen(c, 1);
                    g.DrawLine(p, x, y0, x, y0 + height - 1);
                }

                Mark(peaks.LeftPeakX, Color.Lime);
                Mark(peaks.ValleyX, Color.Yellow);
                Mark(peaks.RightPeakX, Color.Cyan);
                Mark(peaks.TargetX, Color.Magenta);
            }
        }

        private static void EnsureParentDir(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        /// <summary>
        /// 水平拖拽路径（含起终点）。
        /// </summary>
        public static List<(short x, short y)> BuildHorizontalDragPath(short startX, short y, short endX)
        {
            var path = new List<(short x, short y)>();
            if (startX == endX)
            {
                path.Add((startX, y));
                path.Add((endX, y));
                return path;
            }

            var step = endX > startX ? (short)1 : (short)-1;
            for (short x = startX; ; x = (short)(x + step))
            {
                path.Add((x, y));
                if (x == endX)
                {
                    break;
                }
            }

            return path;
        }
    }

    public sealed class HistogramOptimizeResult
    {
        public HistogramOptimizeResult(
            int[] columnHeights,
            int maskPixelCount,
            int rawMaskPixelCount,
            string? compareImagePath,
            string? peaksImagePath,
            string maskColorHex,
            int tolerance,
            HistogramPeakAnalysis peaks)
        {
            ColumnHeights = columnHeights;
            MaskPixelCount = maskPixelCount;
            RawMaskPixelCount = rawMaskPixelCount;
            CompareImagePath = compareImagePath;
            PeaksImagePath = peaksImagePath;
            MaskColorHex = maskColorHex;
            Tolerance = tolerance;
            Peaks = peaks;
        }

        public int[] ColumnHeights { get; }
        public int MaskPixelCount { get; }
        public int RawMaskPixelCount { get; }
        public string? CompareImagePath { get; }
        public string? PeaksImagePath { get; }
        public string MaskColorHex { get; }
        public int Tolerance { get; }
        public HistogramPeakAnalysis Peaks { get; }
    }

    public sealed class HistogramPeakAnalysis
    {
        public HistogramPeakAnalysis(
            bool success,
            string? failReason,
            int leftPeakX,
            int leftPeakHeight,
            int valleyX,
            int valleyHeight,
            int rightPeakX,
            int rightPeakHeight,
            int targetX)
        {
            Success = success;
            FailReason = failReason;
            LeftPeakX = leftPeakX;
            LeftPeakHeight = leftPeakHeight;
            ValleyX = valleyX;
            ValleyHeight = valleyHeight;
            RightPeakX = rightPeakX;
            RightPeakHeight = rightPeakHeight;
            TargetX = targetX;
        }

        public static HistogramPeakAnalysis Fail(string reason) =>
            new(false, reason, 0, 0, 0, 0, 0, 0, 0);

        public bool Success { get; }
        public string? FailReason { get; }
        public int LeftPeakX { get; }
        public int LeftPeakHeight { get; }
        /// <summary>左线：两最高尖峰之间的波谷列</summary>
        public int ValleyX { get; }
        public int ValleyHeight { get; }
        /// <summary>右线：两最高尖峰中偏右的尖峰列</summary>
        public int RightPeakX { get; }
        public int RightPeakHeight { get; }
        /// <summary>中线/目标线：ValleyX→RightPeakX 的 3/4 处（靠右约四分之一），红线拖拽目标</summary>
        public int TargetX { get; }
    }
}
