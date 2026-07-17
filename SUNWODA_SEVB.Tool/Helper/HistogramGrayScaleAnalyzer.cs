using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 直方图绝对色掩膜优化：去红/蓝/文字与峰顶同色针，生成前后对比图。
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
            string? compareSavePath = null)
        {
            ArgumentNullException.ThrowIfNull(source);
            tolerance = Math.Clamp(tolerance, 0, 64);

            var width = source.Width;
            var height = source.Height;
            var mask = new bool[width, height];
            var maskPixelCount = 0;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var p = source.GetPixel(x, y);
                    if (IsNear(p, maskColor, tolerance))
                    {
                        mask[x, y] = true;
                        maskPixelCount++;
                    }
                }
            }

            var heights = BuildColumnHeightsByMaskCount(mask, width, height);
            DespikeColumnHeights(heights);
            MedianFilterInPlace(heights, radius: 2);

            // 对比图右侧：直接显示掩膜像素（保留直方图色，去掉红/蓝/文字/渐变）
            using var cleaned = RenderMaskImage(source, mask, maskColor);
            using var compare = BuildSideBySideCompare(source, cleaned);

            string? savedPath = null;
            if (!string.IsNullOrWhiteSpace(compareSavePath))
            {
                var dir = Path.GetDirectoryName(compareSavePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                compare.Save(compareSavePath, ImageFormat.Png);
                savedPath = compareSavePath;
            }

            return new HistogramOptimizeResult(
                heights,
                maskPixelCount,
                savedPath,
                ToHex(maskColor),
                tolerance);
        }

        private static bool IsNear(Color a, Color b, int tolerance)
        {
            return Math.Abs(a.R - b.R) <= tolerance
                && Math.Abs(a.G - b.G) <= tolerance
                && Math.Abs(a.B - b.B) <= tolerance;
        }

        /// <summary>
        /// 每列掩膜像素计数作为柱高；峰顶同色竖针会使该列略增高，由去针处理。
        /// </summary>
        private static int[] BuildColumnHeightsByMaskCount(bool[,] mask, int width, int height)
        {
            var heights = new int[width];
            for (var x = 0; x < width; x++)
            {
                var h = 0;
                for (var y = 0; y < height; y++)
                {
                    if (mask[x, y])
                    {
                        h++;
                    }
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

        /// <summary>
        /// 优化后可视化：黑底上仅保留掩膜命中像素（用掩膜色填充，便于核对去杂线效果）。
        /// </summary>
        private static Bitmap RenderMaskImage(Bitmap source, bool[,] mask, Color fill)
        {
            var width = source.Width;
            var height = source.Height;
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
    }

    public sealed class HistogramOptimizeResult
    {
        public HistogramOptimizeResult(
            int[] columnHeights,
            int maskPixelCount,
            string? compareImagePath,
            string maskColorHex,
            int tolerance)
        {
            ColumnHeights = columnHeights;
            MaskPixelCount = maskPixelCount;
            CompareImagePath = compareImagePath;
            MaskColorHex = maskColorHex;
            Tolerance = tolerance;
        }

        public int[] ColumnHeights { get; }
        public int MaskPixelCount { get; }
        public string? CompareImagePath { get; }
        public string MaskColorHex { get; }
        public int Tolerance { get; }
    }
}
