namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// AI 标注 L1~L5 尺寸范围判断（单位 mm）。
    /// 开启后：线段与「名称+数值」同色——范围内深绿、超出深红。
    /// </summary>
    public sealed class OfflineCtLRangeCheckOptions
    {
        public const int DimensionCount = 5;

        /// <summary>合格深绿（Halcon #RRGGBB，避免亮绿刺眼）。</summary>
        public const string ColorPass = "#008B00";

        /// <summary>超差深红。</summary>
        public const string ColorFail = "#B22222";

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

        /// <summary>线段颜色：开→深绿/深红；关→cyan。</summary>
        public string ResolveLineColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => ColorPass,
                false => ColorFail,
                null => "cyan",
            };

        /// <summary>底部汇总数值颜色：开→深绿/深红；关→white。</summary>
        public string ResolveValueColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => ColorPass,
                false => ColorFail,
                null => "white",
            };

        /// <summary>旁注数值颜色：开→深绿/深红；关→与名称同用白色（不再单独品红）。</summary>
        public string ResolveSideValueColor(int zeroBasedIndex, double valueMm) =>
            IsInRange(zeroBasedIndex, valueMm) switch
            {
                true => ColorPass,
                false => ColorFail,
                null => "white",
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
