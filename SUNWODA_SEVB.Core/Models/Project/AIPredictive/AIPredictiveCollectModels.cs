namespace SUNWODA_SEVB.Core.Models.Project.AIPredictive
{
    /// <summary>采集块方式：数组整段连读 / 结构体成员（程序侧可按批 multi-tag）。</summary>
    public enum AIPredictiveCollectMode
    {
        ArrayBatch,
        StructMembers
    }

    /// <summary>采集块（对应 CSV 一行）。</summary>
    public sealed class AIPredictiveCollectBlock
    {
        public string BlockId { get; init; } = "";
        public AIPredictiveCollectMode Mode { get; init; }
        public string SymbolName { get; init; } = "";
        public string Ip { get; init; } = "";
        public int Port { get; init; } = 44818;
        /// <summary>数组行：BOOL/FLOAT；结构体行可为 MIXED（以成员为准）。</summary>
        public string PlatformType { get; init; } = "";
        public int StartIndex { get; init; }
        public int ElementCount { get; init; }
        public IReadOnlyList<AIPredictivePointDef> Points { get; init; } = Array.Empty<AIPredictivePointDef>();
    }

    /// <summary>块内一个逻辑点位。</summary>
    public sealed class AIPredictivePointDef
    {
        public string PointKey { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string PlatformType { get; init; } = "";
        public string CipAddress { get; init; } = "";
        public int? ArrayIndex { get; init; }
        public string Process { get; init; } = "";
    }

    /// <summary>单点最新值（失败为 null）。</summary>
    public sealed class AIPredictivePointValue
    {
        public string PointKey { get; init; } = "";
        public string CipAddress { get; init; } = "";
        public object? Value { get; init; }
        public bool Success { get; init; }
        public string? Error { get; init; }
        public DateTime TimestampUtc { get; init; }
    }

    /// <summary>一轮全量采集统计。</summary>
    public sealed class AIPredictiveRoundStats
    {
        public long ElapsedMs { get; set; }
        public int SuccessCount { get; init; }
        public int FailCount { get; init; }
        public int ArrayBlockCount { get; init; }
        public int StructMemberCount { get; init; }
        public DateTime FinishedUtc { get; init; }
        /// <summary>本轮是否因机台状态允许而实际读取扭矩（含异常宽限内）。</summary>
        public bool TorqueCollected { get; init; }
        /// <summary>本轮读到的 AndonRunStatu；读失败为 null。</summary>
        public short? AndonStatus { get; init; }
        /// <summary>本轮是否处于「离开运行后的 3s 宽限采集」。</summary>
        public bool InAbnormalGrace { get; init; }
    }
}
