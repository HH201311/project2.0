namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    public class BoxGluingSeriesPoints
    {
        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public int Meter { get; set; }

        public string Side { get; set; } = "A";

        public double[] Xs { get; set; } = Array.Empty<double>();

        public double[] Ys { get; set; } = Array.Empty<double>();
    }

    /// <summary>
    /// 供 UI 一次性拷贝的曲线快照，避免采集线程与绘图线程争用。
    /// </summary>
    public class BoxGluingChartSnapshot
    {
        public IReadOnlyList<BoxGluingSeriesPoints> Series { get; set; } =
            Array.Empty<BoxGluingSeriesPoints>();

        public IReadOnlyList<BoxGluingThresholdConfig> Thresholds { get; set; } =
            Array.Empty<BoxGluingThresholdConfig>();
    }
}
