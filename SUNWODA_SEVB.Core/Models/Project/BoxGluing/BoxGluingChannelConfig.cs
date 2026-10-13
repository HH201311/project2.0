namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 过程量通道配置。
    /// </summary>
    public class BoxGluingChannelConfig
    {
        public string Label { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        /// <summary>Torque / Pressure / MeteringPressure / Level</summary>
        public string Group { get; set; } = string.Empty;

        public int Meter { get; set; }

        public string Side { get; set; } = "A";

        public bool Enabled { get; set; } = true;
    }
}
