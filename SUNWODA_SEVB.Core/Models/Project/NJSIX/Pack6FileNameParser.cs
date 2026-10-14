using System.IO;
using System.Text.RegularExpressions;

namespace SUNWODA_SEVB.Core.Models.Project.NJSIX
{
    /// <summary>
    /// 南京文件名解析。
    /// 底/周：*_Step{张序}_Cam{相机}_OK{时间}.jpg（忽略 Window）；
    /// 上盖：{SN}_Step{张序}_{时间}.jpg（无 Cam；无 Step 的杂图忽略）。
    /// Cam = 第几相机；Step = 该相机第几张。
    /// </summary>
    public static class Pack6FileNameParser
    {
        private static readonly Regex SideBottomOkPattern = new(
            @"^(?<prefix>.+)_Step(?<step>\d+)_Cam(?<cam>\d+)_OK(?<ts>\d+)\.(?:jpg|jpeg|bmp|png)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TopStepPattern = new(
            @"^(?<sn>.+)_Step(?<step>\d+)_(?<ts>\d+)\.(?:jpg|jpeg|bmp|png)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public sealed record ParseResult(string ProductSn, string CameraName, int ShotIndex);

        /// <summary>仅按文件名解析（不含盘符角色时无法映射底/周相机）。</summary>
        public static bool TryParseFileName(string? fileName, out ParseResult result)
        {
            result = null!;
            return false;
        }

        /// <summary>
        /// 按监视盘角色解析。diskRole：Top / Bottom / Side。
        /// </summary>
        public static bool TryParseIngress(string? fullPath, string diskRole, out ParseResult result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            // 只收 OK；Window / 其它后缀一律忽略
            if (fileName.Contains("_Window", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains("Window_", StringComparison.OrdinalIgnoreCase))
                return false;

            var folderSn = ResolveFolderSn(fullPath) ?? string.Empty;
            var role = (diskRole ?? string.Empty).Trim();

            if (string.Equals(role, "Top", StringComparison.OrdinalIgnoreCase))
            {
                var m = TopStepPattern.Match(fileName);
                if (!m.Success)
                    return false;
                var sn = folderSn;
                if (string.IsNullOrWhiteSpace(sn))
                    sn = m.Groups["sn"].Value;
                var step = int.Parse(m.Groups["step"].Value);
                if (step < 1)
                    return false;
                result = new ParseResult(sn, "上盖相机", step);
                return true;
            }

            if (string.Equals(role, "Bottom", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "Side", StringComparison.OrdinalIgnoreCase))
            {
                var m = SideBottomOkPattern.Match(fileName);
                if (!m.Success)
                    return false;
                var step = int.Parse(m.Groups["step"].Value);
                var cam = int.Parse(m.Groups["cam"].Value);
                if (step < 1 || cam < 1)
                    return false;
                var disk = string.Equals(role, "Bottom", StringComparison.OrdinalIgnoreCase) ? "Bottom" : "Side";
                if (!Pack6CameraCatalog.TryMapLocalCam(disk, cam, out var def))
                    return false;
                result = new ParseResult(folderSn, def.CameraName, step);
                return true;
            }

            return false;
        }

        /// <summary>优先父目录名（日期下的 SN 夹），否则空。</summary>
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
                    !dir.All(char.IsDigit))
                {
                    if (!(dir.Length == 8 && dir.All(char.IsDigit)))
                        return dir;
                }
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
