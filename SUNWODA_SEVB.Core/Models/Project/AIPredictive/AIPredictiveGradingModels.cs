namespace SUNWODA_SEVB.Core.Models.Project.AIPredictive
{
    /// <summary>分级预警调度配置（对应 AIPredictive/GradingAlert/grading_config.json）。</summary>
    public sealed class AIPredictiveGradingConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Python 可执行文件（默认 PATH 中的 python）。</summary>
        public string PythonExe { get; set; } = "python";

        public string DeviceName { get; set; } = "先导3号机";

        /// <summary>多久跑一轮推理（分钟）。默认 120=每 2 小时。</summary>
        public int IntervalMinutes { get; set; } = 120;

        /// <summary>每轮喂给算法的滚动数据窗口（小时），默认 2h。</summary>
        public double LookbackHours { get; set; } = 2;

        public int TimeoutSeconds { get; set; } = 180;

        /// <summary>弹窗等级（默认仅一级/二级）。</summary>
        public List<string> AlertLevels { get; set; } = new() { "一级", "二级" };
    }

    /// <summary>单条分级预警（一级/二级）。</summary>
    public sealed class AIPredictiveGradingAlertItem
    {
        public int Rank { get; set; }
        public string Axis { get; set; } = "";
        public string Mechanism { get; set; } = "";
        public string Station { get; set; } = "";
        public string Level { get; set; } = "";
        public double RiskScore { get; set; }
        public string EvidenceType { get; set; } = "";
        public string Advice { get; set; } = "";

        /// <summary>展示键：轴|等级（历史兼容，已不再用于抑制）。</summary>
        public string SuppressKey => $"{Axis}|{Level}";
    }

    /// <summary>一轮推理结果。</summary>
    public sealed class AIPredictiveGradingRunResult
    {
        public bool Skipped { get; set; }
        public string? SkipReason { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public IReadOnlyList<AIPredictiveGradingAlertItem> Alerts { get; set; }
            = Array.Empty<AIPredictiveGradingAlertItem>();
    }
}
