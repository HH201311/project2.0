namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 工艺阈值参考线（P0 来自工艺表，不读 PLC 限值点）。
    /// </summary>
    public class BoxGluingThresholdConfig
    {
        public string Group { get; set; } = string.Empty;

        public int MeterFrom { get; set; }

        public int MeterTo { get; set; }

        public double? Min { get; set; }

        public double? Max { get; set; }
    }
}
