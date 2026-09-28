namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>结果/原图归档用的缺陷目录：大类 + 可选细分（对齐枣庄 GetNewFilePath）。</summary>
    public sealed class Pack6DefectFolder
    {
        public required string Category { get; init; }
        public string? SubDefect { get; init; }
    }

    /// <summary>把 Defects 文案映射为归档子目录（仅大类，无下级细分）。</summary>
    public static class Pack6DefectFolderMapper
    {
        /// <summary>DLL/Halcon 等视觉算法异常专用归档目录（优先于三码等业务 NG）。</summary>
        public const string VisionAlgorithmCategory = "视觉算法异常";
        /// <summary>历史别名（叠字横幅等仍可能出现）。</summary>
        public const string VisionDetectCategory = "视觉检测异常";
        /// <summary>码检（前1/长左2白标签/前3铭牌）业务 NG 归档目录。</summary>
        public const string ThreeCodeCategory = "三码校验";

        public static IReadOnlyList<Pack6DefectFolder> Map(IReadOnlyList<string>? defects)
        {
            if (defects is null || defects.Count == 0)
                return [];

            var list = new List<Pack6DefectFolder>();
            foreach (var raw in defects)
            {
                var text = (raw ?? string.Empty).Trim();
                if (text.Length == 0)
                    continue;

                var (cat, sub) = Classify(text);
                cat = CleanSegment(cat);
                // 视觉算法异常 / 三码校验：不再建下级细分目录
                if (string.Equals(cat, VisionAlgorithmCategory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cat, VisionDetectCategory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cat, ThreeCodeCategory, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cat, "三码校验异常", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(cat, VisionDetectCategory, StringComparison.OrdinalIgnoreCase))
                        cat = VisionAlgorithmCategory;
                    sub = null;
                }
                else
                    sub = string.IsNullOrWhiteSpace(sub) ? null : CleanSegment(sub);

                if (string.IsNullOrWhiteSpace(cat))
                    continue;

                if (list.Any(x =>
                        string.Equals(x.Category, cat, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.SubDefect ?? string.Empty, sub ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                    continue;

                list.Add(new Pack6DefectFolder { Category = cat, SubDefect = sub });
            }

            return list;
        }

        /// <summary>视觉 DLL 返回的异常文案（含横幅或历史 Halcon 特征）。</summary>
        public static bool IsVisionDllReportedError(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;
            return text.Contains(VisionAlgorithmCategory, StringComparison.OrdinalIgnoreCase)
                   || text.Contains(VisionDetectCategory, StringComparison.OrdinalIgnoreCase)
                   || text.Contains("DLL脚本", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("AlgorithmError", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("HALCON", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("#3111", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("empty region", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("crop_domain", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("HOperator", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("OCR失败", StringComparison.OrdinalIgnoreCase);
        }

        private static (string Category, string? Sub) Classify(string text)
        {
            // 视觉算法异常优先，不进三码目录
            if (IsVisionDllReportedError(text))
                return (VisionAlgorithmCategory, null);

            // 码检业务 NG →「三码校验」
            if (text.Contains("三码", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("二维码", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("前十位", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("长度不符", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("编码", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("零部件", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("白标签", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("铭牌", StringComparison.OrdinalIgnoreCase) ||
                (text.Contains("文件夹", StringComparison.OrdinalIgnoreCase) &&
                 text.Contains("不一致", StringComparison.OrdinalIgnoreCase)))
                return (ThreeCodeCategory, null);

            return ("其它异常", null);
        }

        public static string CleanSegment(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "未知";
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var chars = name.Trim().Select(c => invalid.Contains(c) || c is '/' or '\\' ? '_' : c).ToArray();
            var cleaned = new string(chars).Trim(' ', '.');
            return string.IsNullOrWhiteSpace(cleaned) ? "未知" : cleaned;
        }
    }
}
