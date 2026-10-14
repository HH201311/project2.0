namespace SUNWODA_SEVB.Core.Models.Project.AIPredictive
{
    /// <summary>本机可改的 CIP 终点与厂家（多机台各一份；端口固定 44818）。</summary>
    public sealed class AIPredictivePlcEndpoint
    {
        /// <summary>厂家：先导 / 赢合（决定采集块与状态标签）。</summary>
        public string Vendor { get; set; } = AIPredictiveVendorNames.XianDao;

        public string Ip { get; set; } = "10.147.72.10";

        /// <summary>固定 CIP 端口；保存时强制为 44818。</summary>
        public int Port { get; set; } = 44818;
    }

    /// <summary>厂家显示名与点位配置常量。</summary>
    public static class AIPredictiveVendorNames
    {
        public const string XianDao = "先导";
        public const string YingHe = "赢合";
        public const int FixedCipPort = 44818;

        public static readonly string[] All = { XianDao, YingHe };

        public static string Normalize(string? vendor)
        {
            if (string.IsNullOrWhiteSpace(vendor))
                return XianDao;
            var v = vendor.Trim();
            if (string.Equals(v, YingHe, StringComparison.OrdinalIgnoreCase)
                || v.Contains("赢合", StringComparison.Ordinal))
                return YingHe;
            return XianDao;
        }

        /// <summary>机台安灯状态 CIP 标签。</summary>
        public static string AndonTag(string? vendor) =>
            Normalize(vendor) == YingHe ? "Andon_RunStatu[0]" : "AndonRunStatu";

        /// <summary>输出目录 AIPredictive/ 下相对文件名（仅加载存在的）。</summary>
        public static string[] CollectCsvFileNames(string? vendor)
        {
            if (Normalize(vendor) == YingHe)
            {
                return new[]
                {
                    "赢合_采集块_伺服.csv",
                };
            }

            return new[]
            {
                "AIPredictive采集块_伺服.csv",
                "AIPredictive采集块_气缸.csv",
            };
        }

        public static string DefaultIp(string? vendor) =>
            Normalize(vendor) == YingHe ? "10.147.72.15" : "10.147.72.10";
    }
}
