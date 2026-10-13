using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
{
    /// <summary>
    /// 义乌文件名 / 路径解析。
    /// 文件：OK-{相机名}-{张序}-{时间戳}-{SN}.jpg
    /// 路径：...\{日期}\{SN}\{相机名}\原始图像\{文件}
    /// </summary>
    public static class Pack6FileNameParser
    {
        private static readonly Regex OkPattern = new(
            @"^OK-(?<cam>.+?)-(?<shot>\d+)-(?<ts>\d+)-(?<sn>.+)\.(?:jpg|jpeg|bmp|png)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed record ParseResult(string ProductSn, string CameraName, int ShotIndex);

        /// <summary>按完整路径解析（推荐）。</summary>
        public static bool TryParseIngress(string? fullPath, string? diskRoleIgnored, out ParseResult result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 只收 OK 原图
            if (!fileName.StartsWith("OK-", StringComparison.OrdinalIgnoreCase))
                return false;

            var m = OkPattern.Match(fileName);
            if (!m.Success)
                return false;

            var camFromName = m.Groups["cam"].Value.Trim();
            if (!Pack6CameraCatalog.TryGetByCameraName(camFromName, out var def))
                return false;

            var shot = int.Parse(m.Groups["shot"].Value);
            if (shot < 1)
                return false;

            var snFromName = m.Groups["sn"].Value.Trim();
            var folderSn = ResolveFolderSn(fullPath);
            var sn = !string.IsNullOrWhiteSpace(folderSn) ? folderSn! : snFromName;
            if (string.IsNullOrWhiteSpace(sn))
                return false;

            // 目录名与文件名中的相机名交叉校验（若能取到目录）
            var camFromFolder = ResolveCameraFolderName(fullPath);
            if (!string.IsNullOrWhiteSpace(camFromFolder) &&
                !string.Equals(camFromFolder, def.CameraName, StringComparison.Ordinal))
                return false;

            result = new ParseResult(sn, def.CameraName, shot);
            return true;
        }

        /// <summary>仅按文件名解析。</summary>
        public static bool TryParseFileName(string? fileName, out ParseResult result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(fileName))
                return false;
            var m = OkPattern.Match(Path.GetFileName(fileName));
            if (!m.Success)
                return false;
            var cam = m.Groups["cam"].Value.Trim();
            if (!Pack6CameraCatalog.TryGetByCameraName(cam, out var def))
                return false;
            var shot = int.Parse(m.Groups["shot"].Value);
            if (shot < 1)
                return false;
            result = new ParseResult(m.Groups["sn"].Value.Trim(), def.CameraName, shot);
            return true;
        }

        /// <summary>
        /// 路径形如 ...\{SN}\{相机}\原始图像\file → 取 SN。
        /// </summary>
        public static string? ResolveFolderSn(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            try
            {
                // file → 原始图像 → 相机 → SN
                var dir = Path.GetDirectoryName(fullPath); // 原始图像
                dir = Path.GetDirectoryName(dir);          // 相机
                dir = Path.GetDirectoryName(dir);          // SN
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name) &&
                    name.Length >= 10 &&
                    !IsDateFolderName(name) &&
                    !Pack6CameraCatalog.TryGetByCameraName(name, out _) &&
                    !string.Equals(name, "原始图像", StringComparison.Ordinal) &&
                    !string.Equals(name, "SaveImage", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>路径 ...\{相机}\原始图像\file → 相机名。</summary>
        public static string? ResolveCameraFolderName(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            try
            {
                var dir = Path.GetDirectoryName(fullPath); // 原始图像
                var parent = Path.GetDirectoryName(dir);   // 相机
                var name = Path.GetFileName(parent);
                if (Pack6CameraCatalog.TryGetByCameraName(name, out var def))
                    return def.CameraName;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>从完整路径向上找日期夹名（yyyyMMdd 或 yyyy-MM-dd）。</summary>
        public static string? ResolveDateFolderName(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            try
            {
                var dir = Path.GetDirectoryName(fullPath);
                for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(dir); i++)
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

        /// <summary>同一天的两种日期夹写法。</summary>
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
            if (string.IsNullOrWhiteSpace(dateFolder))
                return list;

            if (dateFolder.Length == 8 && dateFolder.All(char.IsDigit))
            {
                Add($"{dateFolder[..4]}-{dateFolder.Substring(4, 2)}-{dateFolder.Substring(6, 2)}");
            }
            else if (dateFolder.Length == 10 && dateFolder[4] == '-' && dateFolder[7] == '-')
            {
                Add(dateFolder.Replace("-", "", StringComparison.Ordinal));
            }

            return list;
        }
    }
}
