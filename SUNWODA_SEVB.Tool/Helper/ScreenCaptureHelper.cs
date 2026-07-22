using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SUNWODA_SEVB.Tool.Helper
{
    public static class ScreenCaptureHelper
    {
        public static Bitmap CaptureRegion(Rectangle region)
        {
            var bmp = new Bitmap(region.Width, region.Height);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(region.Location, System.Drawing.Point.Empty, region.Size);
            return bmp;
        }

        public static void CaptureRegionAndSave(string filePath, int x, int y, int width, int height)
        {
            using var bmp = CaptureRegion(new Rectangle(x, y, width, height));
            bmp.Save(filePath, ImageFormat.Png);
        }

        /// <summary>
        /// 在已有截图上标注像素点并另存（坐标相对图像左上角）。
        /// </summary>
        public static void SaveAnnotatedPointsImage(
            string sourceImagePath,
            string destImagePath,
            IReadOnlyList<(int X, int Y, string Label)> points)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("标注源截图不存在", sourceImagePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destImagePath)!);

            using var source = new Bitmap(sourceImagePath);
            using var canvas = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.DrawImage(source, 0, 0, source.Width, source.Height);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using var font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
                var palette = new[]
                {
                    Color.FromArgb(255, 220, 20, 60),   // 上盖1 红
                    Color.FromArgb(255, 30, 144, 255),  // 上盖2 蓝
                    Color.FromArgb(255, 50, 205, 50),   // 上盖3 绿
                    Color.FromArgb(255, 255, 165, 0),   // 原点 橙
                };

                for (var i = 0; i < points.Count; i++)
                {
                    var (x, y, label) = points[i];
                    var color = palette[i % palette.Length];
                    using var brush = new SolidBrush(color);
                    using var pen = new Pen(color, 2f);

                    const int r = 7;
                    g.DrawEllipse(pen, x - r, y - r, r * 2, r * 2);
                    g.FillEllipse(brush, x - 2, y - 2, 4, 4);

                    var text = string.IsNullOrWhiteSpace(label) ? $"P{i + 1}" : label;
                    var textPos = new PointF(x + 10, y - 18);
                    using var textBg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
                    var size = g.MeasureString(text, font);
                    g.FillRectangle(textBg, textPos.X - 2, textPos.Y - 1, size.Width + 4, size.Height + 2);
                    g.DrawString(text, font, brush, textPos);
                }
            }

            canvas.Save(destImagePath, ImageFormat.Png);
        }
    }
}
