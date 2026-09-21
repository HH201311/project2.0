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
    }
}
