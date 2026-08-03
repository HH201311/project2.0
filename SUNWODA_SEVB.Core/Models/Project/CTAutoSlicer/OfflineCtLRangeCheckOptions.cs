namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// AI 标注 L1~L5 尺寸范围判断（单位 mm）。
    /// 开启后：线段/数值在范围内为绿、超出为红；名称保持原色。
    /// </summary>
    public sealed class OfflineCtLRangeCheckOptions
    {
        public const int DimensionCount = 5;

        public static OfflineCtLRangeCheckOptions Disabled { get; } = new();

        public bool Enabled { get; init; }

        /// <summary>L1~L5 下限（mm），长度固定为 <see cref="DimensionCount"/>。</summary>
        public double[] MinMm { get; init; } = CreateDefaultMins();

        /// <summary>L1~L5 上限（mm），长度固定为 <see cref="DimensionCount"/>。</summary>
        public double[] MaxMm { get; init; } = CreateDefaultMaxs();

        /// <summary>
        /// 判断是否在范围内。未开启或索引无效时返回 null（中性，不改色）。
        /// </summary>
        public bool? IsInRange(int zeroBasedIndex, double valueMm)
        {
            if (!Enabled
                || zeroBasedIndex < 0
                || zeroBasedIndex >= DimensionCount
                || MinMm is null
                || MaxMm is null
                || zeroBasedIndex >= MinMm.Length
                || zeroBasedIndex >= MaxMm.Length)
            {
                return null;
            }

            var lo = Math.Min(MinMm[zeroBasedIndex], MaxMm[zeroBasedIndex]);
            var hi = Math.Max(MinMm[zeroBasedIndex], MaxMm[zeroBasedIndex]);
            return valueMm >= lo && valueMm <= hi;
        }

        /// <summary>线段颜色：开→绿/红；关→cyan。</summary>
        public string ResolveLineColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => "green",
                false => "red",
                null => "cyan",
            };

        /// <summary>数值颜色：开→绿/红；关→white。</summary>
        public string ResolveValueColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => "green",
                false => "red",
                null => "white",
            };

        /// <summary>旁注数值颜色：开→绿/红；关→magenta（与历史旁注一致）。</summary>
        public string ResolveSideValueColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => "green",
                false => "red",
                null => "magenta",
            };

        private static double[] CreateDefaultMins()
        {
            var a = new double[DimensionCount];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = CTAutoSlicerSettings.DefaultOfflineCtLMinMm;
            }

            return a;
        }

        private static double[] CreateDefaultMaxs()
        {
            var a = new double[DimensionCount];
            for (var i = 0; i < a.Length; i++)
            {
                a[i] = CTAutoSlicerSettings.DefaultOfflineCtLMaxMm;
            }

            return a;
        }
    }
}
