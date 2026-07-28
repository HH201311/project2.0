using System.Drawing;
using System.IO;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// NJCT 返回「当前图像暂不支持检测」时的自动诊断（给出具体原因，不做多选猜测）。
    /// </summary>
    public static class OfflineCtImageDiagnostics
    {
        /// <summary>NJCT 硬编码裁剪宽。</summary>
        public const int RequiredRoiWidth = 1844;

        /// <summary>NJCT 硬编码裁剪高。</summary>
        public const int RequiredRoiHeight = 1500;

        /// <summary>
        /// ROI 平均灰度低于此值判定为过暗（对照：失败图≈69，成功图≈146）。
        /// </summary>
        public const double TooDarkRoiMeanThreshold = 100.0;

        /// <summary>成功样本 ROI 平均灰度参考下限（日志提示用）。</summary>
        public const double TypicalOkRoiMeanLow = 130.0;

        /// <summary>成功样本 ROI 平均灰度参考上限（日志提示用）。</summary>
        public const double TypicalOkRoiMeanHigh = 160.0;

        public sealed class Diagnosis
        {
            /// <summary>唯一主因（面向日志/RunInfo）。</summary>
            public string PrimaryReason { get; init; } = string.Empty;

            /// <summary>附加检查摘要（已排除项/次要信息）。</summary>
            public string Details { get; init; } = string.Empty;

            public int Width { get; init; }
            public int Height { get; init; }
            public double FullMeanGray { get; init; }
            public double RoiMeanGray { get; init; }
            public bool SizeOk { get; init; }
            public bool TooDark { get; init; }
        }

        /// <summary>
        /// 针对 NJCT 兜底文案，自动检测并返回「具体原因」。
        /// </summary>
        public static string BuildUnsupportedRunInfo(string imagePath, string? originalImagePath = null)
        {
            var d = Diagnose(imagePath, originalImagePath);
            var sb = new StringBuilder();
            sb.Append("当前图像暂不支持检测。具体原因：");
            sb.Append(d.PrimaryReason);
            if (!string.IsNullOrWhiteSpace(d.Details))
            {
                sb.Append(" | 检测摘要：");
                sb.Append(d.Details);
            }

            return sb.ToString();
        }

        public static Diagnosis Diagnose(string imagePath, string? originalImagePath = null)
        {
            var notes = new List<string>();
            string? primary = null;

            // —— 路径 ——
            if (!string.IsNullOrWhiteSpace(originalImagePath)
                && !string.Equals(originalImagePath, imagePath, StringComparison.OrdinalIgnoreCase)
                && !IsAsciiPath(originalImagePath))
            {
                notes.Add("原路径含非 ASCII，已转存临时 ASCII 路径后再推理（路径问题已规避）");
            }
            else if (!IsAsciiPath(imagePath))
            {
                primary ??= $"输入图路径含非 ASCII，OpenCV ImRead 会失败：{imagePath}";
            }

            // —— D: ——
            var dDriveIssue = ProbeDDriveIssue();
            if (dDriveIssue != null)
            {
                primary ??= dDriveIssue;
            }
            else
            {
                notes.Add("D: 可写且空间足够");
            }

            // —— Halcon 许可 ——
            var licenseIssue = ProbeHalconLicenseIssue();
            if (licenseIssue != null)
            {
                primary ??= licenseIssue;
            }
            else
            {
                notes.Add("Halcon 许可文件已找到");
            }

            // —— 图像尺寸 / 亮度 ——
            int width = 0, height = 0;
            double fullMean = 0, roiMean = 0;
            var sizeOk = false;
            var tooDark = false;
            string? imageIssue = null;

            if (!File.Exists(imagePath))
            {
                imageIssue = $"输入图不存在：{imagePath}";
            }
            else
            {
                try
                {
                    using var bmp = LoadBitmapUnlocked(imagePath);
                    width = bmp.Width;
                    height = bmp.Height;
                    sizeOk = width >= RequiredRoiWidth && height >= RequiredRoiHeight;
                    (fullMean, roiMean) = SampleMeanGray(bmp);
                    tooDark = sizeOk && roiMean < TooDarkRoiMeanThreshold;

                    if (!sizeOk)
                    {
                        imageIssue =
                            $"图像尺寸不足：当前 {width}×{height}，NJCT 需从左上角裁剪 {RequiredRoiWidth}×{RequiredRoiHeight}，宽高均须 ≥ 该值";
                    }
                    else if (tooDark)
                    {
                        imageIssue =
                            $"输入图过暗：算法裁剪区 {RequiredRoiWidth}×{RequiredRoiHeight} 平均灰度={roiMean:F1}"
                            + $"（成功样本约 {TypicalOkRoiMeanLow:F0}~{TypicalOkRoiMeanHigh:F0}；阈值<{TooDarkRoiMeanThreshold:F0}）"
                            + "。请在 VG 提高直方图/LUT 亮度后再导出上视图，不要调打黑高度";
                    }
                    else
                    {
                        notes.Add(
                            $"尺寸 {width}×{height}（满足裁剪）；全图灰度={fullMean:F1}，ROI灰度={roiMean:F1}（亮度正常）");
                    }
                }
                catch (Exception ex)
                {
                    imageIssue = $"无法读取输入图做自检：{ex.Message}（路径={imagePath}）";
                }
            }

            if (imageIssue != null)
            {
                // 尺寸/过暗等图片硬伤优先给出（同环境成功过时最有用）
                if (primary != null)
                {
                    notes.Insert(0, "环境另检：" + primary);
                }

                primary = imageIssue;
            }

            primary ??=
                $"构图或算法内部处理失败（尺寸/亮度/环境检查未发现硬伤：{width}×{height}，ROI灰度={roiMean:F1}）。"
                + "常见为电芯在画面中占比异常、朝向未旋转、或 VG 叠加层干扰；请对照成功样本构图";

            return new Diagnosis
            {
                PrimaryReason = primary,
                Details = string.Join("；", notes),
                Width = width,
                Height = height,
                FullMeanGray = fullMean,
                RoiMeanGray = roiMean,
                SizeOk = sizeOk,
                TooDark = tooDark,
            };
        }

        private static string? ProbeDDriveIssue()
        {
            const long minFreeBytes = 50L * 1024 * 1024;
            try
            {
                var drive = new DriveInfo("D");
                if (!drive.IsReady)
                {
                    return "D: 盘不可用（NJCT 硬编码写 D:\\TEST.png）";
                }

                if (drive.AvailableFreeSpace < minFreeBytes)
                {
                    return $"D: 盘空间不足（仅剩 {drive.AvailableFreeSpace / 1024.0 / 1024.0:F1} MB，"
                        + "NJCT 需写 D:\\TEST.png，建议至少 50 MB）";
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"无法检查 D: 盘：{ex.Message}";
            }
        }

        private static string? ProbeHalconLicenseIssue()
        {
            var existing = Environment.GetEnvironmentVariable("HALCON_LICENSE_FILE");
            if (!string.IsNullOrWhiteSpace(existing) && File.Exists(existing))
            {
                return null;
            }

            var dllsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dlls");
            var candidates = new[]
            {
                existing,
                Path.Combine(dllsDir, "license.dat"),
                Path.Combine(dllsDir, "license", "license.dat"),
                Path.Combine(dllsDir, "license", "license_22.11.dat"),
            };

            if (candidates.Any(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p!)))
            {
                return null;
            }

            return $"未找到 Halcon 许可文件（已查 HALCON_LICENSE_FILE 与 {dllsDir}\\license*.dat）";
        }

        private static Bitmap LoadBitmapUnlocked(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var temp = new Bitmap(fs);
            return new Bitmap(temp);
        }

        private static (double FullMean, double RoiMean) SampleMeanGray(Bitmap bmp)
        {
            var w = bmp.Width;
            var h = bmp.Height;
            var step = Math.Max(1, Math.Min(w, h) / 100);

            long fullSum = 0;
            var fullN = 0;
            for (var y = 0; y < h; y += step)
            {
                for (var x = 0; x < w; x += step)
                {
                    fullSum += Gray(bmp.GetPixel(x, y));
                    fullN++;
                }
            }

            var roiW = Math.Min(RequiredRoiWidth, w);
            var roiH = Math.Min(RequiredRoiHeight, h);
            long roiSum = 0;
            var roiN = 0;
            for (var y = 0; y < roiH; y += step)
            {
                for (var x = 0; x < roiW; x += step)
                {
                    roiSum += Gray(bmp.GetPixel(x, y));
                    roiN++;
                }
            }

            var fullMean = fullN > 0 ? fullSum / (double)fullN : 0;
            var roiMean = roiN > 0 ? roiSum / (double)roiN : 0;
            return (fullMean, roiMean);
        }

        private static int Gray(Color c) => (c.R + c.G + c.B) / 3;

        private static bool IsAsciiPath(string path)
        {
            foreach (var ch in path)
            {
                if (ch > 127)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
