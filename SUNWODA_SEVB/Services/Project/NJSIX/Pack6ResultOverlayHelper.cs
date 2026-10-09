using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// 在原图上绘制 OCR 区域框与码文。
    /// 叠图仅写系统临时目录供界面预览与拷贝到「结果图片」；不再维护 LocalPathPre/标注预览。
    /// </summary>
    public static class Pack6ResultOverlayHelper
    {
        /// <summary>历史目录名（已废弃，不再创建）。</summary>
        public const string FolderName = "标注预览";
        public const string AlgorithmErrorBanner = "视觉检测异常";

        private static readonly string StagingRoot =
            Path.Combine(Path.GetTempPath(), "NJSIX_Pack6Overlay");

        /// <summary>叠图暂存根（临时目录，非结果根下的预览夹）。</summary>
        public static string GetOverlayRoot(Pack6Config config)
        {
            _ = config;
            return StagingRoot;
        }

        public static void EnsureAndPrune(Pack6Config config)
        {
            _ = config;
            try
            {
                Directory.CreateDirectory(StagingRoot);
                // 临时叠图只保留当天，避免占盘；正式存档在「结果图片」
                var cutoff = DateTime.Now.Date;
                foreach (var dir in Directory.EnumerateDirectories(StagingRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (name.Length == 8 &&
                        DateTime.TryParseExact(name, "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out var day) &&
                        day < cutoff)
                    {
                        try { Directory.Delete(dir, recursive: true); }
                        catch { /* 忽略占用 */ }
                    }
                }
            }
            catch
            {
                // 裁剪失败不影响检测
            }
        }

        public static string? TryWrite(
            Pack6Config config,
            Pack6IngressItem item,
            string sourceImagePath,
            IReadOnlyList<Pack6OverlayBox> boxes,
            bool algorithmError,
            string? detailMessage = null,
            string? ngBanner = null,
            bool? isOk = null)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) ||
                sourceImagePath.StartsWith("sim://", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(sourceImagePath))
                return null;

            try
            {
                var dayDir = Path.Combine(GetOverlayRoot(config), DateTime.Now.ToString("yyyyMMdd"));
                Directory.CreateDirectory(dayDir);
                var baseName = Path.GetFileNameWithoutExtension(sourceImagePath);
                if (string.IsNullOrWhiteSpace(baseName))
                    baseName = $"{item.CameraName}_{item.ShotIndex}_{DateTime.Now:HHmmssfff}";
                var dest = Path.Combine(dayDir, baseName + "_ann.jpg");

                using var fs = new FileStream(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var src = Image.FromStream(fs);
                using var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.DrawImage(src, 0, 0, src.Width, src.Height);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                    var penW = Math.Max(2, Math.Min(src.Width, src.Height) / 400);
                    var fontPx = Math.Max(14f, Math.Min(src.Width, src.Height) / 50f);
                    using var font = new Font("Microsoft YaHei UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);

                    // 只画色框，不在 ROI 内叠字，避免挡住原码无法比对
                    foreach (var box in boxes)
                    {
                        if (box.Width <= 1 || box.Height <= 1)
                            continue;
                        var color = Color.FromArgb(box.Argb);
                        using var pen = new Pen(color, penW);
                        g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
                    }

                    // 绿二维码上方 / 黄序列码右侧 / 青产品码与铭牌下方
                    DrawLabelsByPlacement(g, src.Width, src.Height, font, boxes);

                    // 左上角仅 OK / NG 及 NG 详情
                    if (!algorithmError)
                    {
                        var ok = isOk ?? string.IsNullOrWhiteSpace(ngBanner);
                        if (!ok || !string.IsNullOrWhiteSpace(ngBanner))
                            DrawTopLeftStatus(g, src.Width, src.Height, fontPx, ok: false, ngBanner);
                        else
                            DrawTopLeftStatus(g, src.Width, src.Height, fontPx, ok: true, ngDetail: null);
                    }

                    if (algorithmError)
                    {
                        var banner = AlgorithmErrorBanner;
                        if (!string.IsNullOrWhiteSpace(detailMessage) &&
                            !detailMessage.Contains(AlgorithmErrorBanner, StringComparison.Ordinal))
                        {
                            var shortDetail = detailMessage.Trim();
                            if (shortDetail.Length > 80)
                                shortDetail = shortDetail[..80] + "…";
                            banner = $"{AlgorithmErrorBanner}\n{shortDetail}";
                        }

                        using var bannerFont = new Font("Microsoft YaHei UI", fontPx * 1.4f, FontStyle.Bold, GraphicsUnit.Pixel);
                        var sz = g.MeasureString(banner, bannerFont);
                        var bx = Math.Max(8f, (src.Width - sz.Width) / 2f);
                        var by = Math.Max(8f, src.Height * 0.08f);
                        using var bannerBg = new SolidBrush(Color.FromArgb(200, 120, 0, 0));
                        g.FillRectangle(bannerBg, bx - 8, by - 6, sz.Width + 16, sz.Height + 12);
                        using var bannerBrush = new SolidBrush(Color.Yellow);
                        g.DrawString(banner, bannerFont, bannerBrush, bx, by);
                    }
                }

                var jpeg = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(e => e.FormatID == ImageFormat.Jpeg.Guid);
                if (jpeg is null)
                {
                    bmp.Save(dest, ImageFormat.Jpeg);
                }
                else
                {
                    using var ep = new EncoderParameters(1);
                    ep.Param[0] = new EncoderParameter(Encoder.Quality, 85L);
                    bmp.Save(dest, jpeg, ep);
                }

                return dest;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 绿框二维码：上方；黄框序列码：右侧（放不下改左侧）；青框/铭牌产品码：下方。
        /// </summary>
        private static void DrawLabelsByPlacement(
            Graphics g,
            int imgW,
            int imgH,
            Font font,
            IReadOnlyList<Pack6OverlayBox> boxes)
        {
            const float gap = 6f;
            const float margin = 4f;

            foreach (var box in boxes)
            {
                if (string.IsNullOrWhiteSpace(box.Label) || box.Width <= 1 || box.Height <= 1)
                    continue;

                var text = box.Label.Trim();
                var color = Color.FromArgb(box.Argb);
                var maxTextW = Math.Max(40f, imgW * 0.5f);
                var sz = g.MeasureString(text, font, (int)maxTextW);
                var panelW = sz.Width;
                var panelH = sz.Height;

                float px;
                float py;
                switch (box.LabelPlacement)
                {
                    case Pack6OverlayLabelPlacement.Above:
                        px = Math.Clamp(box.X, margin, Math.Max(margin, imgW - margin - panelW));
                        py = box.Y - gap - panelH;
                        if (py < margin)
                            py = margin;
                        break;

                    case Pack6OverlayLabelPlacement.Right:
                        px = box.X + box.Width + gap;
                        py = Math.Clamp(box.Y, margin, Math.Max(margin, imgH - margin - panelH));
                        if (px + panelW > imgW - margin)
                            px = Math.Max(margin, box.X - gap - panelW);
                        break;

                    default: // Below
                        px = Math.Clamp(box.X, margin, Math.Max(margin, imgW - margin - panelW));
                        py = Math.Min(imgH - margin - panelH, box.Y + box.Height + gap);
                        if (py < margin)
                            py = margin;
                        break;
                }

                // 同图微调：版本号下移、铭牌紫字上移等
                if (Math.Abs(box.LabelOffsetY) > 0.01f)
                    py = Math.Clamp(py + box.LabelOffsetY, margin, Math.Max(margin, imgH - margin - panelH));

                using var outline = new SolidBrush(Color.FromArgb(220, 0, 0, 0));
                using var brush = new SolidBrush(color);
                for (var ox = -1; ox <= 1; ox++)
                {
                    for (var oy = -1; oy <= 1; oy++)
                    {
                        if (ox == 0 && oy == 0)
                            continue;
                        g.DrawString(text, font, outline, px + ox, py + oy);
                    }
                }
                g.DrawString(text, font, brush, px, py);
            }
        }

        /// <summary>左上角仅画 OK / NG（及 NG 详情），不含识别码文。</summary>
        private static void DrawTopLeftStatus(
            Graphics g,
            int imgW,
            int imgH,
            float baseFontPx,
            bool ok,
            string? ngDetail)
        {
            string banner;
            Color bgColor;
            Color textColor;
            if (ok)
            {
                banner = "OK";
                bgColor = Color.FromArgb(200, 0, 110, 0);
                textColor = Color.Lime;
            }
            else
            {
                var detail = string.IsNullOrWhiteSpace(ngDetail) ? string.Empty : ngDetail.Trim();
                banner = string.IsNullOrEmpty(detail) ? "NG" : $"NG\n{detail}";
                bgColor = Color.FromArgb(210, 160, 0, 0);
                textColor = Color.Yellow;
            }

            var fontPx = Math.Max(16f, baseFontPx * 1.15f);
            using var bannerFont = new Font("Microsoft YaHei UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
            var sz = g.MeasureString(banner, bannerFont, imgW - 24);
            var bx = 10f;
            var by = 10f;
            var pad = 8f;
            var h = sz.Height + pad;
            using (var bannerBg = new SolidBrush(bgColor))
            {
                g.FillRectangle(bannerBg, bx - 2, by - 2,
                    Math.Min(sz.Width + pad * 2, imgW - 16), h);
            }

            using var bannerBrush = new SolidBrush(textColor);
            g.DrawString(banner, bannerFont, bannerBrush,
                new RectangleF(bx + 4, by + 2, imgW - 28, imgH * 0.45f));
        }
    }
}
