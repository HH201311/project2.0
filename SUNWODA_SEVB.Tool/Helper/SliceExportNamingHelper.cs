using System.Globalization;
using System.IO;
using System.Text;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 切片导出文件名解析与生成（基于导入体积/快捷方式显示名）。
    /// 导出名固定取文件名主干前 <see cref="ExportBaseNameLength"/> 个字符，
    /// 例如「1161-F 2026-7-25 2-28-56.vgi - 快捷方式」→「1161-F」。
    /// </summary>
    public static class SliceExportNamingHelper
    {
        /// <summary>命名不合规时的默认型号前缀（4 位数字）。</summary>
        public const string DefaultModelCode = "0000";

        /// <summary>导出主文件夹 / 结果图名使用的前缀长度。</summary>
        public const int ExportBaseNameLength = 6;

        /// <summary>文件名是否含 QJ（全局图像，不区分大小写）。</summary>
        public static bool IsGlobalImage(string vglFileName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(vglFileName ?? string.Empty);
            return nameWithoutExt.Contains("QJ", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 导出名：导入文件名主干的前 6 个字符（用作主文件夹、AI 结果图名等）。
        /// 不符合规则时回退默认名（0000-F / 0000-Q），不抛异常，避免卡住流程。
        /// </summary>
        public static string BuildExportBaseName(string vglFileName, DateTime? now = null) =>
            BuildExportBaseName(vglFileName, now, out _);

        /// <summary>
        /// 同 <see cref="BuildExportBaseName(string, DateTime?)"/>；
        /// <paramref name="usedDefaultName"/> 为 true 表示已回退默认命名。
        /// <paramref name="now"/> 保留兼容，当前规则不再拼时间戳。
        /// </summary>
        public static string BuildExportBaseName(
            string vglFileName,
            DateTime? now,
            out bool usedDefaultName)
        {
            _ = now;
            usedDefaultName = false;
            var stem = NormalizeImportFileStem(vglFileName);

            if (string.IsNullOrWhiteSpace(stem))
            {
                usedDefaultName = true;
                return BuildDefaultExportBaseName(vglFileName);
            }

            var prefix = stem.Length <= ExportBaseNameLength
                ? stem
                : stem[..ExportBaseNameLength];
            prefix = SanitizeFileName(prefix).Trim();

            if (string.IsNullOrWhiteSpace(prefix))
            {
                usedDefaultName = true;
                return BuildDefaultExportBaseName(vglFileName);
            }

            return prefix;
        }

        /// <summary>
        /// 从导入路径/文件名提取主干，供取前缀使用。
        /// 支持真实 .vgl/.vgi，以及「xxx.vgi - 快捷方式.lnk」。
        /// </summary>
        public static string NormalizeImportFileStem(string? fileNameOrPath)
        {
            var name = Path.GetFileName(fileNameOrPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            // 「a.vgi - 快捷方式.lnk」→ 去 .lnk
            if (name.EndsWith(
                    CTAutoSlicerSettingsShortcutExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^CTAutoSlicerSettingsShortcutExtension.Length].TrimEnd();
            }

            // 「a.vgi - 快捷方式」→ 去「 - 快捷方式」/「 - Shortcut」
            name = StripShortcutCaptionSuffix(name);

            // 「a.vgi」/「a.vgl」或其它扩展 → 去扩展名
            name = Path.GetFileNameWithoutExtension(name);

            return name.Trim();
        }

        /// <summary>
        /// 图像堆栈导出时保存对话框中的基础文件名（前缀）。
        /// VG 以此为前缀递增导出；选第 50 张时实际文件名为 <c>050.jpg</c>（前缀 + 序号）。
        /// </summary>
        public const string ImageStackBaseFileName = "0";

        /// <summary>
        /// 由切片序号生成图像文件主干名：前缀 + 序号。
        /// 例如前缀 <c>0</c>、序号 50 → <c>050</c>（对应 050.jpg）。
        /// </summary>
        public static string BuildImageStackFileStem(int sliceIndex)
        {
            var index = Math.Max(0, sliceIndex);
            return ImageStackBaseFileName + index.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>生成单个视图的导出文件夹名（不含根目录与主文件夹），如「1161-F 前」。</summary>
        public static string BuildExportFolderName(string exportBaseName, string viewSuffix)
        {
            if (string.IsNullOrWhiteSpace(viewSuffix))
            {
                throw new ArgumentException("视图后缀不能为空", nameof(viewSuffix));
            }

            return $"{exportBaseName.Trim()} {viewSuffix.Trim()}";
        }

        /// <summary>
        /// 单个体素文件的主导出目录名（三视图共用一层），即 <paramref name="exportBaseName"/>。
        /// </summary>
        public static string BuildExportFileRootFolderName(string exportBaseName)
        {
            if (string.IsNullOrWhiteSpace(exportBaseName))
            {
                throw new ArgumentException("导出名不能为空", nameof(exportBaseName));
            }

            return exportBaseName.Trim();
        }

        /// <summary>
        /// 保存对话框完整路径：
        /// {导出根目录}/{主文件夹}/{视图文件夹}/0
        /// 例如 …/1161-F/1161-F 前/0
        /// （不含扩展名；前缀为 ImageStackBaseFileName，堆栈图为 01、02…050…）。
        /// </summary>
        public static string BuildExportSavePath(string exportRootFolder, string exportBaseName, string viewSuffix)
        {
            var fileRootFolder = Path.Combine(
                exportRootFolder,
                BuildExportFileRootFolderName(exportBaseName));
            var viewFolderPath = Path.Combine(
                fileRootFolder,
                BuildExportFolderName(exportBaseName, viewSuffix));
            return Path.Combine(viewFolderPath, ImageStackBaseFileName);
        }

        /// <summary>
        /// 调试截图文件名中的产品标签：与导出主文件夹相同规则（导入文件名主干前 6 字符）。
        /// 空输入时返回 <c>unknown</c>，便于与无导入上下文的单测截图区分。
        /// </summary>
        public static string BuildCaptureProductTag(string? namingSourcePathOrFileName)
        {
            if (string.IsNullOrWhiteSpace(namingSourcePathOrFileName))
            {
                return "unknown";
            }

            var tag = BuildExportBaseName(namingSourcePathOrFileName);
            return string.IsNullOrWhiteSpace(tag) ? "unknown" : tag;
        }

        /// <summary>根据是否 QJ 解析导出数量。</summary>
        public static int ResolveExportQuantity(string vglFileName, int defaultQuantity, int qjQuantity)
        {
            return IsGlobalImage(vglFileName) ? qjQuantity : defaultQuantity;
        }

        /// <summary>格式化为与样例一致的时间串：yyyy-M-d H-mm-ss（保留供其它调用）。</summary>
        public static string FormatExportTimestamp(DateTime dateTime)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}-{1}-{2} {3}-{4}-{5}",
                dateTime.Year,
                dateTime.Month,
                dateTime.Day,
                dateTime.Hour,
                dateTime.Minute,
                dateTime.Second);
        }

        private const string CTAutoSlicerSettingsShortcutExtension = ".lnk";

        private static string BuildDefaultExportBaseName(string? vglFileName)
        {
            // 与前 6 字符规则对齐：0000-F / 0000-Q
            var typeLetter = IsGlobalImage(vglFileName ?? string.Empty) ? "Q" : "F";
            return $"{DefaultModelCode}-{typeLetter}";
        }

        private static string StripShortcutCaptionSuffix(string name)
        {
            const string cn = " - 快捷方式";
            const string en = " - Shortcut";
            if (name.EndsWith(cn, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^cn.Length].TrimEnd();
            }

            if (name.EndsWith(en, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^en.Length].TrimEnd();
            }

            return name;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
            {
                if (Array.IndexOf(invalid, ch) >= 0)
                {
                    continue;
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}
