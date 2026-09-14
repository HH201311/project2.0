namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>
    /// PACK 六面检运行配置（路径/开关来自旧 UDV 键名；PLC 地址不臆造，仅保留键名占位）。
    /// </summary>
    public class Pack6Config
    {
        public const string VmClassName = "VM_ZZSIXPage";

        /// <summary>结果与日志根目录（对应旧 LocalPathPre）。</summary>
        public string LocalPathPre { get; set; } = @"D:\ZZSIX_Pack6";

        public string TopCamPath { get; set; } = @"D:\Pack6Images\Top";
        public string BottomCamPath { get; set; } = @"D:\Pack6Images\Bottom";
        public string SideCamPath { get; set; } = @"D:\Pack6Images\Side";

        public string TopCamFilter { get; set; } = "*.jpg;*.bmp;*.png";
        public string BottomCamFilter { get; set; } = "*.jpg;*.bmp;*.png";
        public string SideCamFilter { get; set; } = "*.jpg;*.bmp;*.png";

        /// <summary>无真实 ZZPack 引擎时走模拟检测。</summary>
        public bool UseSimulation { get; set; } = true;

        /// <summary>Pack 未齐套超时（分钟），对齐旧逻辑约 10 分钟。</summary>
        public int PackTimeoutMinutes { get; set; } = 10;

        /// <summary>旧 UDV：PLC_HeartBeat / AlarmSignal —— 仅键名，地址来自现场配置。</summary>
        public string PlcHeartBeatKey { get; set; } = "PLC_HeartBeat";
        public string AlarmSignalKey { get; set; } = "AlarmSignal";

        public string CellType { get; set; } = "X04C";
        public string ProductionLine { get; set; } = "P01";
        public string Workshop { get; set; } = "ZZ";
    }
}
