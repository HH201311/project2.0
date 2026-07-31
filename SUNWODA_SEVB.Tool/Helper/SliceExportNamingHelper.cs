using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Tool.Helper
{
    /// <summary>
    /// 切片导出文件名解析与生成（基于 .vgl 导入命名规则）。
    /// </summary>
    public static class SliceExportNamingHelper
    {
        /// <summary>命名不合规时的默认型号前缀。</summary>
        public const string DefaultModelCode = "0000";

        // 示例：0657-FD 2026-7-12 15-35-16.vgl / 0987-QJ 2026-7-13 8-10-19.vgl
        private static readonly Regex VglNamePattern = new(
            @"^(?<model>\d{4})-(?<type>FD|QJ)\s+\d{4}-\d{1,2}-\d{1,2}\s+\d{1,2}-\d{1,2}-\d{1,2}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>文件名是否含 QJ（全局图像，不区分大小写）。</summary>
        public static bool IsGlobalImage(string vglFileName)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(vglFileName);
            return nameWithoutExt.Contains("QJ", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 保留「型号-类型」前缀，日期段替换为当前时间（用作导出文件夹名）。
        /// 不符合规则时回退默认名（0000-FD/QJ + 当前时间），不抛异常，避免卡住流程。
        /// </summary>
        public static string BuildExportBaseName(string vglFileName, DateTime? now = null) =>
            BuildExportBaseName(vglFileName, now, out _);

        /// <summary>
        /// 同 <see cref="BuildExportBaseName(string, DateTime?)"/>；
        /// <paramref name="usedDefaultName"/> 为 true 表示已回退默认命名。
        /// </summary>
        public static string BuildExportBaseName(
            string vglFileName,
            DateTime? now,
            out bool usedDefaultName)
        {
            usedDefaultName = false;
            var timestamp = FormatExportTimestamp(now ?? DateTime.Now);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(vglFileName ?? string.Empty);

            if (string.IsNullOrWhiteSpace(nameWithoutExt))
            {
                usedDefaultName = true;
                return $"{DefaultModelCode}-FD {timestamp}";
            }

            var match = VglNamePattern.Match(nameWithoutExt.Trim());
            if (!match.Success)
            {
                usedDefaultName = true;
                var type = IsGlobalImage(vglFileName ?? string.Empty) ? "QJ" : "FD";
                return $"{DefaultModelCode}-{type} {timestamp}";
            }

            var model = match.Groups["model"].Value;
            var matchedType = match.Groups["type"].Value.ToUpperInvariant();
            return $"{model}-{matchedType} {timestamp}";
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

        /// <summary>生成单个视图的导出文件夹名（不含根目录与主文件夹），如「0657-FD 2026-7-13 10-58-00 前」。</summary>
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
        /// 例如 …/0657-FD 2026-7-13 10-58-00/0657-FD 2026-7-13 10-58-00 前/0
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

        /// <summary>根据是否 QJ 解析导出数量。</summary>
        public static int ResolveExportQuantity(string vglFileName, int defaultQuantity, int qjQuantity)
        {
            return IsGlobalImage(vglFileName) ? qjQuantity : defaultQuantity;
        }

        /// <summary>格式化为与样例一致的时间串：yyyy-M-d H-mm-ss</summary>
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
    }

}
