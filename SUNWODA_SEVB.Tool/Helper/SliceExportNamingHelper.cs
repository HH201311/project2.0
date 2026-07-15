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
        /// </summary>
        public static string BuildExportBaseName(string vglFileName, DateTime? now = null)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(vglFileName);
            if (string.IsNullOrWhiteSpace(nameWithoutExt))
            {
                throw new InvalidOperationException($"无法解析 .vgl 文件名：{vglFileName}");
            }

            var match = VglNamePattern.Match(nameWithoutExt);
            if (!match.Success)
            {
                throw new InvalidOperationException(
                    $"导入文件名不符合命名规则（期望如 0657-FD 2026-7-12 15-35-16.vgl）：{vglFileName}");
            }

            var model = match.Groups["model"].Value;
            var type = match.Groups["type"].Value.ToUpperInvariant();
            var timestamp = FormatExportTimestamp(now ?? DateTime.Now);
            return $"{model}-{type} {timestamp}";
        }

        /// <summary>图像堆栈导出时保存对话框中的基础文件名（VG 将依次导出为 1、2、3…）。</summary>
        public const string ImageStackBaseFileName = "1";

        /// <summary>生成单个视图的导出文件夹名（不含根目录），如「0657-FD 2026-7-13 10-58-00 前」。</summary>
        public static string BuildExportFolderName(string exportBaseName, string viewSuffix)
        {
            if (string.IsNullOrWhiteSpace(viewSuffix))
            {
                throw new ArgumentException("视图后缀不能为空", nameof(viewSuffix));
            }

            return $"{exportBaseName.Trim()} {viewSuffix.Trim()}";
        }

        /// <summary>
        /// 保存对话框完整路径：{导出根目录}/{文件夹名}/1（不含扩展名；文件夹内图片为 1、2、3…）。
        /// </summary>
        public static string BuildExportSavePath(string exportRootFolder, string exportBaseName, string viewSuffix)
        {
            var folderPath = Path.Combine(
                exportRootFolder,
                BuildExportFolderName(exportBaseName, viewSuffix));
            return Path.Combine(folderPath, ImageStackBaseFileName);
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
