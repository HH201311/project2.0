using System.Drawing;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 直方图 ROI 内红色阈值竖线检测（供拖拽起点定位）。
    /// </summary>
    public static class HistogramRedLineDetector
    {
        /// <summary>
        /// 在直方图截图中查找偏红竖线列。
        /// </summary>
        /// <returns>
        /// 相对 ROI 左缘的列索引；未找到返回 null。
        /// </returns>
        public static int? FindRedLineColumn(
            Bitmap bitmap,
            byte minRed = 160,
            byte maxGreen = 120,
            byte maxBlue = 120,
            double minColumnRatio = 0.08)
        {
            if (bitmap.Width < 2 || bitmap.Height < 2)
            {
                return null;
            }

            var scores = new int[bitmap.Width];
            var minHits = Math.Max(3, (int)(bitmap.Height * minColumnRatio));

            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (c.R >= minRed && c.G <= maxGreen && c.B <= maxBlue && c.R > c.G + 40 && c.R > c.B + 40)
                    {
                        scores[x]++;
                    }
                }
            }

            var bestX = -1;
            var bestScore = 0;
            for (var x = 0; x < scores.Length; x++)
            {
                if (scores[x] > bestScore)
                {
                    bestScore = scores[x];
                    bestX = x;
                }
            }

            if (bestX < 0 || bestScore < minHits)
            {
                return null;
            }

            return bestX;
        }
    }
}
