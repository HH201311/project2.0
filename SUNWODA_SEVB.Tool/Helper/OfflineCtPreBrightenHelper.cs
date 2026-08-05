using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 方案 B：调用 NJCT 前保证竖图（宽&gt;高则 CW90°）→ 内容上移（可选）→ 按 ROI 亮度条件提亮。
    /// 主流程默认写入临时文件并在推理后删除；产品目录只保留成功结果图。
    /// </summary>
    public static class OfflineCtPreBrightenHelper
    {
        /// <summary>历史：曾用于落盘排查的子目录名；主流程已不再创建。</summary>
        public const string InputSubFolderName = "input";

        /// <summary>历史文件名主干；现优先使用源图文件名。</summary>
        public const string FinalInputFileNameWithoutExt = "input_for_njct";
        public const string BrightenReportFileName = "prebrighten_report.txt";
        public const string AlgorithmCropFileNameWithoutExt = "njct_algo_crop_1844x1500";

        /// <summary>默认：均值低于此值则提亮（临界偏暗，高于诊断硬伤 100）。</summary>
        public const double DefaultMeanTrigger = 120.0;

        /// <summary>默认：P50 低于此值则提亮。</summary>
        public const double DefaultP50Trigger = 110.0;

        /// <summary>提亮后目标 ROI 均值（分区提亮强度依据）。</summary>
        public const double DefaultTargetMean = 132.0;

        /// <summary>提亮后希望 P95 不超过此值（报告参考）。</summary>
        public const double DefaultMaxOutputP95 = 235.0;

        public const double MinGain = 1.0;

        /// <summary>旧线性增益上限（已弃用路径）。</summary>
        public const double MaxGain = 1.5;

        /// <summary>分区暗部抬升强度上限（约等于 PS Shadows 量）。</summary>
        public const double MaxToneLift = 0.55;

        public sealed class BrightnessStats
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public int RoiWidth { get; init; }
            public int RoiHeight { get; init; }
            public double Mean { get; init; }
            public double P50 { get; init; }
            public double P5 { get; init; }
            public double P95 { get; init; }
        }

        public sealed class PrepareResult
        {
            /// <summary>送入 NJCT 的最终图路径。</summary>
            public string FinalImagePath { get; init; } = string.Empty;

            public bool BrightenApplied { get; init; }
            /// <summary>分区提亮强度（旧字段名 GainUsed；现为 toneLiftAmount）。</summary>
            public double GainUsed { get; init; } = 1.0;
            public BrightnessStats Before { get; init; } = new();
            public BrightnessStats? After { get; init; }
            public string ReportPath { get; init; } = string.Empty;
            public string Summary { get; init; } = string.Empty;

            /// <summary>为 true 时 <see cref="FinalImagePath"/> 为临时文件，推理结束后应由调用方删除。</summary>
            public bool IsTemporary { get; init; }

            /// <summary>本次因横图（宽&gt;高）执行了顺时针 90°。</summary>
            public bool RotatedCw90 { get; init; }

            /// <summary>实际应用的内容上移像素（钳制后；0 表示未上移）。</summary>
            public int ContentShiftUpPx { get; init; }
        }

        /// <summary>
        /// 统计左上角 NJCT 裁剪区（1844×1500，不足则取整图）的均值与分位数。
        /// </summary>
        public static BrightnessStats MeasureRoi(string imagePath)
        {
            using var bmp = LoadBitmapUnlocked(imagePath);
            return MeasureRoi(bmp);
        }

        public static BrightnessStats MeasureRoi(Bitmap bmp)
        {
            var roiW = Math.Min(OfflineCtImageDiagnostics.RequiredRoiWidth, bmp.Width);
            var roiH = Math.Min(OfflineCtImageDiagnostics.RequiredRoiHeight, bmp.Height);
            var step = Math.Max(1, Math.Min(roiW, roiH) / 100);
            var samples = new List<byte>(roiW * roiH / (step * step) + 8);
            long sum = 0;

            var data = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            try
            {
                var stride = data.Stride;
                var bytes = new byte[Math.Abs(stride) * bmp.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                for (var y = 0; y < roiH; y += step)
                {
                    var row = y * stride;
                    for (var x = 0; x < roiW; x += step)
                    {
                        var i = row + x * 3;
                        // Format24bppRgb 内存序为 B,G,R
                        var g = (byte)((bytes[i] + bytes[i + 1] + bytes[i + 2]) / 3);
                        samples.Add(g);
                        sum += g;
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            if (samples.Count == 0)
            {
                return new BrightnessStats
                {
                    Width = bmp.Width,
                    Height = bmp.Height,
                    RoiWidth = roiW,
                    RoiHeight = roiH,
                };
            }

            samples.Sort();
            double Pct(double p) => samples[(int)Math.Floor((samples.Count - 1) * p)];

            return new BrightnessStats
            {
                Width = bmp.Width,
                Height = bmp.Height,
                RoiWidth = roiW,
                RoiHeight = roiH,
                Mean = sum / (double)samples.Count,
                P5 = Pct(0.05),
                P50 = Pct(0.5),
                P95 = Pct(0.95),
            };
        }

        public static bool NeedsBrighten(
            BrightnessStats stats,
            double meanTrigger = DefaultMeanTrigger,
            double p50Trigger = DefaultP50Trigger) =>
            stats.Mean < meanTrigger || stats.P50 < p50Trigger;

        /// <summary>
        /// 保证竖图（宽&gt;高则 CW90°）→ 内容上移（可选，底部黑填）→ 计算亮度 →（必要时）提亮 → 送检图。
        /// <paramref name="persistFinalInput"/> 为 true 时落盘到 <c>{outputFolder}/input/</c>（供视觉 DLL），
        /// 并写报告；为 false 时写临时文件（调用方负责删除）。
        /// </summary>
        /// <param name="sourceImagePath">原始上视图（或已竖图的交接图）</param>
        /// <param name="outputFolder">产品标注目录</param>
        /// <param name="contentShiftUpPx">内容上移像素；画布不变，底部黑边；≤0 不上移</param>
        /// <param name="persistFinalInput">是否落盘到 input/ 供 DLL 与排查</param>
        public static PrepareResult PrepareFinalInputImage(
            string sourceImagePath,
            string outputFolder,
            bool enableBrighten,
            double meanTrigger = DefaultMeanTrigger,
            double p50Trigger = DefaultP50Trigger,
            double targetMean = DefaultTargetMean,
            bool persistFinalInput = true,
            int contentShiftUpPx = 0)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("预提亮输入图不存在", sourceImagePath);
            }

            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".jpg";
            }

            ext = ext.ToLowerInvariant();

            var working = LoadBitmapUnlocked(sourceImagePath);
            try
            {
                // 1) 竖图约定：仅当宽>高时 CW90
                var rotatedCw90 = OfflineCtAnnotationHelper.EnsurePortraitByClockwise90IfLandscape(working);
                var rotateTag = rotatedCw90
                    ? "已CW90(横→竖)；"
                    : $"已竖图无需转({working.Width}x{working.Height})；";

                // 2) 内容上移：适配 DLL 偏上裁切（主体抬到上方，底部黑边被裁）
                var shiftApplied = 0;
                var shiftTag = string.Empty;
                if (contentShiftUpPx > 0)
                {
                    var shifted = ShiftContentUpFillBlackBottom(working, contentShiftUpPx, out shiftApplied);
                    if (!ReferenceEquals(shifted, working))
                    {
                        working.Dispose();
                        working = shifted;
                    }

                    shiftTag = shiftApplied > 0
                        ? $"内容上移{shiftApplied}px(底黑填)；"
                        : string.Empty;
                }

                var before = MeasureRoi(working);
                var need = enableBrighten && NeedsBrighten(before, meanTrigger, p50Trigger);
                var prefixTag = rotateTag + shiftTag;

                string finalPath;
                string reportPath;
                bool isTemporary;
                if (persistFinalInput)
                {
                    var inputDir = Path.Combine(outputFolder, InputSubFolderName);
                    Directory.CreateDirectory(inputDir);
                    var fileName = Path.GetFileName(sourceImagePath);
                    if (string.IsNullOrWhiteSpace(fileName))
                    {
                        fileName = FinalInputFileNameWithoutExt + ext;
                    }

                    finalPath = Path.Combine(inputDir, fileName);
                    reportPath = Path.Combine(inputDir, BrightenReportFileName);
                    isTemporary = false;
                }
                else
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), "OfflineCtInput");
                    Directory.CreateDirectory(tempDir);
                    finalPath = Path.Combine(
                        tempDir,
                        $"{FinalInputFileNameWithoutExt}_{Guid.NewGuid():N}{ext}");
                    reportPath = string.Empty;
                    isTemporary = true;
                }

                var format = ResolveImageFormat(finalPath);
                double toneLift = 0.0;
                BrightnessStats? after = null;

                if (!need)
                {
                    working.Save(finalPath, format);
                    var summary = enableBrighten
                        ? $"{prefixTag}亮度合格，未提亮：ROI均值={before.Mean:F1}，P50={before.P50:F0}（触发线 均值<{meanTrigger:F0} 或 P50<{p50Trigger:F0}）→ input/{Path.GetFileName(finalPath)}"
                        : $"{prefixTag}预提亮关闭：ROI均值={before.Mean:F1}，P50={before.P50:F0} → input/{Path.GetFileName(finalPath)}";
                    if (!persistFinalInput)
                    {
                        summary = enableBrighten
                            ? $"{prefixTag}亮度合格，未提亮：ROI均值={before.Mean:F1}，P50={before.P50:F0}（临时推理图）"
                            : $"{prefixTag}预提亮关闭：ROI均值={before.Mean:F1}，P50={before.P50:F0}（临时推理图）";
                    }

                    if (persistFinalInput)
                    {
                        WriteReport(
                            reportPath,
                            summary,
                            before,
                            null,
                            toneLift,
                            brightenApplied: false,
                            rotateCw90: rotatedCw90,
                            contentShiftUpPx: shiftApplied);
                    }

                    return new PrepareResult
                    {
                        FinalImagePath = finalPath,
                        BrightenApplied = false,
                        GainUsed = 0.0,
                        Before = before,
                        After = before,
                        ReportPath = reportPath,
                        Summary = summary,
                        IsTemporary = isTemporary,
                        RotatedCw90 = rotatedCw90,
                        ContentShiftUpPx = shiftApplied,
                    };
                }

                toneLift = ComputeToneLiftAmount(before.Mean, targetMean);
                using (var bright = ApplyToneZoneBrighten(working, toneLift))
                {
                    bright.Save(finalPath, format);
                    after = MeasureRoi(bright);
                }

                var outName = persistFinalInput
                    ? $"input/{Path.GetFileName(finalPath)}"
                    : Path.GetFileName(finalPath);
                var summaryBright =
                    $"{prefixTag}分区提亮(暗部/中间调抬升，高光白场保护)：强度={toneLift:F3}；"
                    + $"前 均值={before.Mean:F1}/P50={before.P50:F0}/P95={before.P95:F0}"
                    + $" → 后 均值={after!.Mean:F1}/P50={after.P50:F0}/P95={after.P95:F0} → {outName}";
                if (persistFinalInput)
                {
                    WriteReport(
                        reportPath,
                        summaryBright,
                        before,
                        after,
                        toneLift,
                        brightenApplied: true,
                        rotateCw90: rotatedCw90,
                        contentShiftUpPx: shiftApplied);
                }

                return new PrepareResult
                {
                    FinalImagePath = finalPath,
                    BrightenApplied = true,
                    GainUsed = toneLift,
                    Before = before,
                    After = after,
                    ReportPath = reportPath,
                    Summary = summaryBright,
                    IsTemporary = isTemporary,
                    RotatedCw90 = rotatedCw90,
                    ContentShiftUpPx = shiftApplied,
                };
            }
            finally
            {
                working.Dispose();
            }
        }

        /// <summary>
        /// 内容整体上移 <paramref name="shiftUpPx"/> 像素：画布尺寸不变，顶部溢出裁掉，底部黑边填充。
        /// </summary>
        /// <returns>新位图（调用方负责 Dispose）；若无需上移则返回原引用。</returns>
        public static Bitmap ShiftContentUpFillBlackBottom(
            Bitmap source,
            int shiftUpPx,
            out int appliedShiftPx)
        {
            ArgumentNullException.ThrowIfNull(source);
            appliedShiftPx = 0;
            if (shiftUpPx <= 0)
            {
                return source;
            }

            var h = source.Height;
            var w = source.Width;
            if (h <= 0 || w <= 0)
            {
                return source;
            }

            appliedShiftPx = Math.Min(shiftUpPx, h);
            var dest = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(dest))
            {
                g.Clear(Color.Black);
                // 目标点 (0, -applied)：内容上移，底部露出黑底
                g.DrawImageUnscaled(source, 0, -appliedShiftPx);
            }

            return dest;
        }

        /// <summary>
        /// 兼容旧签名：忽略 <paramref name="rotateCw90"/>（现按宽高自动保证竖图）。
        /// </summary>
        [Obsolete("请使用不带 rotateCw90 的重载；现按宽>高自动 CW90")]
        public static PrepareResult PrepareFinalInputImage(
            string sourceImagePath,
            string outputFolder,
            bool enableBrighten,
            bool rotateCw90,
            double meanTrigger = DefaultMeanTrigger,
            double p50Trigger = DefaultP50Trigger,
            double targetMean = DefaultTargetMean,
            bool persistFinalInput = true) =>
            PrepareFinalInputImage(
                sourceImagePath,
                outputFolder,
                enableBrighten,
                meanTrigger,
                p50Trigger,
                targetMean,
                persistFinalInput,
                contentShiftUpPx: 0);

        /// <summary>
        /// 由目标均值差估算「暗部抬升强度」(0~MaxToneLift)。
        /// 不再使用全图像素 × 线性增益（易过曝）。
        /// </summary>
        public static double ComputeToneLiftAmount(double currentMean, double targetMean)
        {
            var need = Math.Max(0.0, targetMean - currentMean) / 255.0;
            // 暗图需要更强抬升；映射到约 0~0.55
            var amount = need * 1.35;
            if (amount < 0.05 && need > 0)
            {
                amount = 0.05;
            }

            return Math.Clamp(amount, 0.0, MaxToneLift);
        }

        /// <summary>
        /// 类 PS 分区：阴影/暗部强抬、中间调轻抬、高光少动、白色保护（甚至微压）。
        /// 按亮度权重混合，避免全局线性增益把高光打到 255。
        /// </summary>
        public static Bitmap ApplyToneZoneBrighten(Bitmap source, double toneLiftAmount)
        {
            var amount = Math.Clamp(toneLiftAmount, 0.0, MaxToneLift);
            var src24 = source.PixelFormat == PixelFormat.Format24bppRgb
                ? source
                : source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format24bppRgb);
            var disposeSrc24 = !ReferenceEquals(src24, source);

            try
            {
                var dst = new Bitmap(src24.Width, src24.Height, PixelFormat.Format24bppRgb);
                var rect = new Rectangle(0, 0, src24.Width, src24.Height);
                var srcData = src24.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var dstData = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var srcStride = srcData.Stride;
                    var dstStride = dstData.Stride;
                    var height = src24.Height;
                    var width = src24.Width;
                    var srcBytes = new byte[Math.Abs(srcStride) * height];
                    var dstBytes = new byte[Math.Abs(dstStride) * height];
                    Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length);

                    // 分区强度相对系数（类似 PS：Shadows / Midtones / Highlights / Whites）
                    var shadowBoost = amount;           // 阴影/暗部
                    var midBoost = amount * 0.45;       // 中间调
                    var highlightBoost = amount * 0.08; // 高光几乎不抬
                    var whiteCompress = amount * 0.12;  // 白场略压，防过曝

                    for (var y = 0; y < height; y++)
                    {
                        var sRow = y * srcStride;
                        var dRow = y * dstStride;
                        for (var x = 0; x < width; x++)
                        {
                            var si = sRow + x * 3;
                            var di = dRow + x * 3;
                            var b = srcBytes[si] / 255.0;
                            var g = srcBytes[si + 1] / 255.0;
                            var r = srcBytes[si + 2] / 255.0;
                            var lum = (b + g + r) / 3.0;

                            // 平滑分区权重（0~1）
                            var wShadow = 1.0 - SmoothStep(0.05, 0.40, lum);      // 暗部/阴影
                            var wMid = 1.0 - Math.Abs(lum - 0.45) / 0.35;          // 中间调峰
                            if (wMid < 0)
                            {
                                wMid = 0;
                            }

                            var wHighlight = SmoothStep(0.55, 0.85, lum);           // 高光
                            var wWhite = SmoothStep(0.82, 0.97, lum);               // 白色

                            // 抬升量：暗部空间大 (1-lum)，白场反向压缩
                            var delta =
                                shadowBoost * wShadow * (1.0 - lum)
                                + midBoost * wMid * (1.0 - lum) * 0.65
                                + highlightBoost * wHighlight * (1.0 - lum) * 0.25
                                - whiteCompress * wWhite * lum;

                            var lum2 = Clamp01(lum + delta);
                            double scale;
                            if (lum <= 1e-6)
                            {
                                // 纯黑：直接抬一点亮度
                                scale = 0;
                                var lift = Clamp01(delta);
                                dstBytes[di] = ClampToByte(lift * 255.0);
                                dstBytes[di + 1] = dstBytes[di];
                                dstBytes[di + 2] = dstBytes[di];
                                continue;
                            }

                            scale = lum2 / lum;
                            dstBytes[di] = ClampToByte(srcBytes[si] * scale);
                            dstBytes[di + 1] = ClampToByte(srcBytes[si + 1] * scale);
                            dstBytes[di + 2] = ClampToByte(srcBytes[si + 2] * scale);
                        }
                    }

                    Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length);
                }
                finally
                {
                    src24.UnlockBits(srcData);
                    dst.UnlockBits(dstData);
                }

                return dst;
            }
            finally
            {
                if (disposeSrc24)
                {
                    src24.Dispose();
                }
            }
        }

        /// <summary>旧线性增益接口保留（调试/兼容）；正式路径请用分区提亮。</summary>
        [Obsolete("易过曝，请使用 ApplyToneZoneBrighten")]
        public static double ComputeGain(
            double currentMean,
            double currentP95,
            double targetMean,
            double maxOutputP95 = DefaultMaxOutputP95)
        {
            if (currentMean <= 1e-6)
            {
                return MaxGain;
            }

            var gainMean = targetMean / currentMean;
            var gainP95 = currentP95 > 1e-6 ? maxOutputP95 / currentP95 : MaxGain;
            var gain = Math.Min(gainMean, gainP95);
            return Math.Clamp(gain, MinGain, MaxGain);
        }

        [Obsolete("易过曝，请使用 ApplyToneZoneBrighten")]
        public static double ComputeGain(double currentMean, double targetMean) =>
            ComputeGain(currentMean, currentP95: 255, targetMean, maxOutputP95: DefaultMaxOutputP95);

        /// <summary>
        /// 模拟 NJCT <c>Rect(0,0,1844,1500)</c> 裁剪并落盘（检测失败排查用）。
        /// </summary>
        public static string SaveNjCtAlgorithmCrop(string sourceImagePath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                throw new FileNotFoundException("裁剪源图不存在", sourceImagePath);
            }

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using var src = LoadBitmapUnlocked(sourceImagePath);
            var roiW = Math.Min(OfflineCtImageDiagnostics.RequiredRoiWidth, src.Width);
            var roiH = Math.Min(OfflineCtImageDiagnostics.RequiredRoiHeight, src.Height);
            if (roiW <= 0 || roiH <= 0)
            {
                throw new InvalidOperationException($"图像尺寸过小，无法裁剪：{src.Width}×{src.Height}");
            }

            using var crop = src.Clone(new Rectangle(0, 0, roiW, roiH), PixelFormat.Format24bppRgb);
            crop.Save(outputPath, ResolveImageFormat(outputPath));
            return outputPath;
        }

        public static string BuildAlgorithmCropPath(string? resultImagePathOrFolder, string sourceImagePath)
        {
            string folder;
            if (!string.IsNullOrWhiteSpace(resultImagePathOrFolder))
            {
                folder = Directory.Exists(resultImagePathOrFolder)
                    ? resultImagePathOrFolder
                    : (Path.GetDirectoryName(resultImagePathOrFolder) ?? string.Empty);
            }
            else
            {
                folder = Path.GetDirectoryName(sourceImagePath) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.GetDirectoryName(sourceImagePath) ?? ".";
            }

            var ext = Path.GetExtension(sourceImagePath);
            if (string.IsNullOrWhiteSpace(ext))
            {
                ext = ".jpg";
            }

            return Path.Combine(folder, AlgorithmCropFileNameWithoutExt + ext.ToLowerInvariant());
        }

        [Obsolete("易过曝，请使用 ApplyToneZoneBrighten")]
        public static Bitmap ApplyGain(Bitmap source, double gain)
        {
            var src24 = source.PixelFormat == PixelFormat.Format24bppRgb
                ? source
                : source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format24bppRgb);
            var disposeSrc24 = !ReferenceEquals(src24, source);

            try
            {
                var dst = new Bitmap(src24.Width, src24.Height, PixelFormat.Format24bppRgb);
                var rect = new Rectangle(0, 0, src24.Width, src24.Height);
                var srcData = src24.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var dstData = dst.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                try
                {
                    var srcStride = srcData.Stride;
                    var dstStride = dstData.Stride;
                    var height = src24.Height;
                    var width = src24.Width;
                    var srcBytes = new byte[Math.Abs(srcStride) * height];
                    var dstBytes = new byte[Math.Abs(dstStride) * height];
                    Marshal.Copy(srcData.Scan0, srcBytes, 0, srcBytes.Length);

                    for (var y = 0; y < height; y++)
                    {
                        var sRow = y * srcStride;
                        var dRow = y * dstStride;
                        for (var x = 0; x < width; x++)
                        {
                            var si = sRow + x * 3;
                            var di = dRow + x * 3;
                            dstBytes[di] = ClampToByte(srcBytes[si] * gain);
                            dstBytes[di + 1] = ClampToByte(srcBytes[si + 1] * gain);
                            dstBytes[di + 2] = ClampToByte(srcBytes[si + 2] * gain);
                        }
                    }

                    Marshal.Copy(dstBytes, 0, dstData.Scan0, dstBytes.Length);
                }
                finally
                {
                    src24.UnlockBits(srcData);
                    dst.UnlockBits(dstData);
                }

                return dst;
            }
            finally
            {
                if (disposeSrc24)
                {
                    src24.Dispose();
                }
            }
        }

        private static double SmoothStep(double edge0, double edge1, double x)
        {
            if (edge1 <= edge0)
            {
                return x >= edge1 ? 1.0 : 0.0;
            }

            var t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3.0 - 2.0 * t);
        }

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private static byte ClampToByte(double v)
        {
            if (v <= 0)
            {
                return 0;
            }

            if (v >= 255)
            {
                return 255;
            }

            return (byte)(v + 0.5);
        }

        private static void WriteReport(
            string reportPath,
            string summary,
            BrightnessStats before,
            BrightnessStats? after,
            double gain,
            bool brightenApplied,
            bool rotateCw90 = false,
            int contentShiftUpPx = 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Offline CT 预提亮报告（方案 B）");
            sb.AppendLine("generatedAt=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("summary=" + summary);
            sb.AppendLine("rotateCw90=" + rotateCw90);
            sb.AppendLine("contentShiftUpPx=" + contentShiftUpPx);
            sb.AppendLine("mode=toneZones(shadows/midtones/highlights/whites)");
            sb.AppendLine("toneLiftAmount=" + gain.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine("brightenApplied=" + brightenApplied);
            sb.AppendLine(
                $"before: size={before.Width}x{before.Height} roi={before.RoiWidth}x{before.RoiHeight} "
                + $"mean={before.Mean:F2} P5={before.P5:F0} P50={before.P50:F0} P95={before.P95:F0}");
            if (after != null)
            {
                sb.AppendLine(
                    $"after: size={after.Width}x{after.Height} roi={after.RoiWidth}x{after.RoiHeight} "
                    + $"mean={after.Mean:F2} P5={after.P5:F0} P50={after.P50:F0} P95={after.P95:F0}");
            }

            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var temp = new Bitmap(fs);
            // 转 24bpp，避免索引色/32bpp 导致 LockBits 不一致
            if (temp.PixelFormat == PixelFormat.Format24bppRgb)
            {
                return new Bitmap(temp);
            }

            var clone = new Bitmap(temp.Width, temp.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(clone))
            {
                g.DrawImage(temp, 0, 0, temp.Width, temp.Height);
            }

            return clone;
        }

        private static ImageFormat ResolveImageFormat(string path)
        {
            var ext = Path.GetExtension(path);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }

            if (ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Bmp;
            }

            if (ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Tiff;
            }

            return ImageFormat.Jpeg;
        }
    }
}
