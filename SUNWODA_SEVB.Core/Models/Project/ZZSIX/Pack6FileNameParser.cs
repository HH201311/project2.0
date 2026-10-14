using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>解析现场文件名：{SN}-{相机}-{张序}-{OK|NG}_{时间戳}.jpg</summary>
    public static class Pack6FileNameParser
    {
        private static readonly Regex FilePattern = new(
            @"^(?<sn>.+)-(?<cam>[^-]+)-(?<seq>\d+)-(?:OK|NG)_(?<ts>\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2}-\d+)\.(?:jpg|jpeg|bmp|png)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed record ParseResult(string ProductSn, string CameraName, int ShotIndex);

        public static bool TryParseFileName(string? fileName, out ParseResult result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            var name = Path.GetFileName(fileName);
            var m = FilePattern.Match(name);
            if (!m.Success)
                return false;
            result = new ParseResult(
                m.Groups["sn"].Value,
                m.Groups["cam"].Value,
                int.Parse(m.Groups["seq"].Value));
            return true;
        }

        /// <summary>优先父目录名（日期下的 SN 夹），否则文件名解析。</summary>
        public static string? ResolveFolderSn(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            try
            {
                var dir = Path.GetFileName(Path.GetDirectoryName(fullPath));
                if (!string.IsNullOrWhiteSpace(dir) &&
                    dir.Length >= 10 &&
                    !dir.Equals("1", StringComparison.Ordinal) &&
                    !dir.All(char.IsDigit)) // 排除纯日期夹误用
                {
                    // 日期夹 yyyyMMdd 为 8 位数字；SN 夹通常含字母
                    if (!(dir.Length == 8 && dir.All(char.IsDigit)))
                        return dir;
                }
            }
            catch
            {
                // ignore
            }

            return TryParseFileName(fullPath, out var parsed) ? parsed.ProductSn : null;
        }

        /// <summary>从完整路径向上找日期夹名（yyyyMMdd 或 yyyy-MM-dd）。</summary>
        public static string? ResolveDateFolderName(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                for (var i = 0; i < 6 && !string.IsNullOrWhiteSpace(dir); i++)
                {
                    var name = Path.GetFileName(dir);
                    if (IsDateFolderName(name))
                        return name;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        public static bool IsDateFolderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;
            if (name.Length == 8 && name.All(char.IsDigit))
                return true;
            return name.Length == 10 &&
                   name[4] == '-' && name[7] == '-' &&
                   char.IsDigit(name[0]) && char.IsDigit(name[1]) && char.IsDigit(name[2]) &&
                   char.IsDigit(name[3]) && char.IsDigit(name[5]) && char.IsDigit(name[6]) &&
                   char.IsDigit(name[8]) && char.IsDigit(name[9]);
        }

        /// <summary>同一天的两种日期夹写法（现场可能是 20260917 或 2026-09-17）。</summary>
        public static IReadOnlyList<string> ExpandDateFolderAliases(string? dateFolder)
        {
            var list = new List<string>();
            void Add(string? s)
            {
                if (string.IsNullOrWhiteSpace(s))
                    return;
                if (!list.Contains(s, StringComparer.OrdinalIgnoreCase))
                    list.Add(s);
            }

            Add(dateFolder);
            if (!string.IsNullOrWhiteSpace(dateFolder) &&
                dateFolder.Length == 8 && dateFolder.All(char.IsDigit) &&
                DateTime.TryParseExact(dateFolder, "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var d1))
                Add(d1.ToString("yyyy-MM-dd"));
            if (!string.IsNullOrWhiteSpace(dateFolder) &&
                DateTime.TryParseExact(dateFolder, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var d2))
                Add(d2.ToString("yyyyMMdd"));

            var today = DateTime.Now;
            Add(today.ToString("yyyyMMdd"));
            Add(today.ToString("yyyy-MM-dd"));
            return list;
        }
    }
}
