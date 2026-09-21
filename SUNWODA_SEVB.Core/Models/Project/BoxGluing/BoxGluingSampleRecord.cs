namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 一次采样记录（时间 + 启用通道数值）。
    /// </summary>
    public class BoxGluingSampleRecord
    {
        public DateTime Timestamp { get; set; }

        /// <summary>本拍处于采集中的工位，如 1 或 1|3。</summary>
        public string TriggerStations { get; set; } = string.Empty;

        public IReadOnlyList<string> ChannelLabels { get; set; } = Array.Empty<string>();

        public IReadOnlyList<double?> Values { get; set; } = Array.Empty<double?>();

        public double ElapsedSeconds { get; set; }

        /// <summary>各工位相对本段出胶起点的秒数，三路独立。</summary>
        public Dictionary<int, double> ElapsedByStation { get; set; } = new();
    }
}
