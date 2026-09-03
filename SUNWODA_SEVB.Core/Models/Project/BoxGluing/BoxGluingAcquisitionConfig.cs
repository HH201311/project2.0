namespace SUNWODA_SEVB.Core.Models.Project.BoxGluing
{
    /// <summary>
    /// 口琴管涂胶采集运行配置。
    /// </summary>
    public class BoxGluingAcquisitionConfig
    {
        public const string VmClassName = "VM_BoxGluingMonitoringPage";

        public int SampleIntervalMs { get; set; } = 80;

        public int TriggerPollIntervalMs { get; set; } = 50;

        public string CsvRootPath { get; set; } = @"D:\BoxGluingData";

        public int ChartWindowPoints { get; set; } = 3000;

        /// <summary>
        /// true：不读 PLC，按勾选工位生成模拟量，便于无设备联调。
        /// </summary>
        public bool UseSimulation { get; set; }

        public List<BoxGluingChannelConfig> Channels { get; set; } = new();

        public List<BoxGluingTriggerConfig> Triggers { get; set; } = new();

        public List<BoxGluingThresholdConfig> Thresholds { get; set; } = new();

        public IEnumerable<BoxGluingChannelConfig> EnabledChannels =>
            Channels.Where(c => c.Enabled);

        public IEnumerable<BoxGluingChannelConfig> ChannelsOfStation(int station) =>
            EnabledChannels.Where(c => c.Meter == station);
    }
}
