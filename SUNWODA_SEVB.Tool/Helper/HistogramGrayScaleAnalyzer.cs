using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 直方图绝对色掩膜优化、峰谷/半坡检测与调试图输出。
    /// </summary>
    public static class HistogramGrayScaleAnalyzer
    {
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

            var width = source.Width;
            var height = source.Height;
            var mask = new bool[width, height];
            var rawMaskPixels = 0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = source.GetPixel(x, y);
                    if (IsNear(p, maskColor, tolerance))
                    {
                        mask[x, y] = true;
                        rawMaskPixels++;
                    }
                }
            }

            // 去掉渐变等亮度带造成的「近全宽横条」，否则柱高被抬平、峰谷难辨
            RemoveNearFullWidthBands(mask, width, height, fillRatio: 0.72);

            var maskPixelCount = CountMask(mask, width, height);
            var heights = BuildColumnHeightsFromBottom(mask, width, height);
            DespikeColumnHeights(heights);
            MedianFilterInPlace(heights, radius: 2);
            GaussianSmoothInPlace(heights, radius: 3);

            var peaks = FindBimodalHalfSlope(heights);

            using var cleaned = RenderMaskImage(mask, maskColor);
            using var compare = BuildSideBySideCompare(source, cleaned);
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
                using var peaksImg = BuildPeaksDebugImage(source, cleaned, heights, peaks);
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
        /// 在平滑柱高曲线上找左峰、右峰、中间波谷，半坡目标 = (谷+右峰)/2。
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

            var minPeakH = Math.Max(3, (int)(maxH * 0.12));
            var minSep = Math.Max(8, n / 12);
            var candidates = new List<int>();

            for (var i = 2; i < n - 2; i++)
            {
                if (heights[i] < minPeakH)
                {
                    continue;
                }

                if (heights[i] >= heights[i - 1]
                    && heights[i] >= heights[i + 1]
                    && heights[i] >= heights[i - 2]
                    && heights[i] >= heights[i + 2])
                {
                    candidates.Add(i);
                }
            }

            // 合并过近的局部峰，保留更高者
            candidates = MergeNearbyPeaks(candidates, heights, minSep / 2);
            if (candidates.Count < 2)
            {
                // 兜底：全局最高 + 其右侧次高显著峰
                var left = Array.IndexOf(heights, maxH);
                var right = -1;
                var rightBest = 0;
                for (var i = left + minSep; i < n - 2; i++)
                {
                    if (heights[i] < minPeakH)
                    {
                        continue;
                    }

                    if (heights[i] >= heights[i - 1]
                        && heights[i] >= heights[i + 1]
                        && heights[i] > rightBest)
                    {
                        rightBest = heights[i];
                        right = i;
                    }
                }

                if (right < 0)
                {
                    return HistogramPeakAnalysis.Fail($"显著峰不足（候选 {candidates.Count}）");
                }

                candidates = [left, right];
            }

            // 按 X 排序后取「左侧主峰」与「右侧主峰」：在左半/右半各取最高，若都在一侧则取最高与次高且间距够
            candidates.Sort();
            int leftPeak;
            int rightPeak;
            PickLeftAndRightPeaks(candidates, heights, minSep, out leftPeak, out rightPeak);

            if (rightPeak <= leftPeak)
            {
                return HistogramPeakAnalysis.Fail("未能区分左右峰");
            }

            var valley = leftPeak;
            var valleyH = heights[leftPeak];
            for (var i = leftPeak; i <= rightPeak; i++)
            {
                if (heights[i] < valleyH)
                {
                    valleyH = heights[i];
                    valley = i;
                }
            }

            var target = (valley + rightPeak) / 2;
            return new HistogramPeakAnalysis(
                true,
                null,
                leftPeak,
                heights[leftPeak],
                valley,
                valleyH,
                rightPeak,
                heights[rightPeak],
                target);
        }

        private static void PickLeftAndRightPeaks(
            List<int> candidates,
            int[] heights,
            int minSep,
            out int leftPeak,
            out int rightPeak)
        {
            // 在所有候选对中选：间距够大，且两峰高度之和最大
            leftPeak = candidates[0];
            rightPeak = candidates[^1];
            var bestScore = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                for (var j = i + 1; j < candidates.Count; j++)
                {
                    var a = candidates[i];
                    var b = candidates[j];
                    if (b - a < minSep)
                    {
                        continue;
                    }

                    var score = heights[a] + heights[b];
                    // 略微偏好「左峰更高」的双峰（CT 常见背景峰更高）
                    if (heights[a] >= heights[b] * 0.85)
                    {
                        score += heights[a] / 10;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        leftPeak = a;
                        rightPeak = b;
                    }
                }
            }
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
        /// 删除横向填充比例过高的行（渐变等亮度带），避免抬平柱高。
        /// </summary>
        private static void RemoveNearFullWidthBands(bool[,] mask, int width, int height, double fillRatio)
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
        /// 自底向上连续命中长度；跳过底部空洞，遇间隙停止（削弱峰顶同色竖针影响需配合去针）。
        /// </summary>
        private static int[] BuildColumnHeightsFromBottom(bool[,] mask, int width, int height)
        {
            var heights = new int[width];
            for (var x = 0; x < width; x++)
            {
                var h = 0;
                for (var y = height - 1; y >= 0; y--)
                {
                    if (!mask[x, y])
                    {
                        if (h > 0)
                        {
                            break;
                        }

                        continue;
                    }

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

        private static Bitmap RenderMaskImage(bool[,] mask, Color fill)
        {
            var width = mask.GetLength(0);
            var height = mask.GetLength(1);
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Black);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (mask[x, y])
                    {
                        bmp.SetPixel(x, y, fill);
                    }
                }
            }

            return bmp;
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
            g.DrawString("优化后（掩膜去杂线）", titleFont, titleBrush, pad + w + gap, pad);

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

            g.DrawString("峰谷/半坡标注（掩膜图）", titleFont, white, pad, pad);
            var yImg = pad + labelH;
            g.DrawImage(cleaned, pad, yImg, w, h);
            DrawPeakMarkers(g, pad, yImg, h, peaks);

            var yCurve = yImg + h + gap;
            g.DrawString(
                peaks.Success
                    ? $"L={peaks.LeftPeakX} V={peaks.ValleyX} R={peaks.RightPeakX} T*={peaks.TargetX}  (谷+右峰)/2"
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

            void VLine(int col, Color color, string tag)
            {
                using var pen = new Pen(color, 2);
                g.DrawLine(pen, x0 + col, y0, x0 + col, y0 + panelH - 1);
                using var brush = new SolidBrush(color);
                using var font = new Font("Consolas", 9f, FontStyle.Bold);
                g.DrawString(tag, font, brush, x0 + col + 2, y0 + 4);
            }

            VLine(peaks.LeftPeakX, Color.Lime, "L");
            VLine(peaks.ValleyX, Color.Yellow, "V");
            VLine(peaks.RightPeakX, Color.Cyan, "R");
            VLine(peaks.TargetX, Color.Magenta, "T*");
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
        public int ValleyX { get; }
        public int ValleyHeight { get; }
        public int RightPeakX { get; }
        public int RightPeakHeight { get; }
        /// <summary>半坡目标列：(ValleyX + RightPeakX) / 2</summary>
        public int TargetX { get; }
    }
}
