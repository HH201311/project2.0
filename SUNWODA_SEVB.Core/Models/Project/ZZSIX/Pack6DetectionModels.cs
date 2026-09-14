namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>收图入队项。</summary>
    public sealed class Pack6IngressItem
    {
        public required string FilePath { get; init; }
        public required string QueueName { get; init; }
        public string? ProductCode { get; init; }
        public DateTime EnqueuedAt { get; init; } = DateTime.Now;
    }

    /// <summary>单帧检测结果。</summary>
    public sealed class Pack6FrameResult
    {
        public required string FilePath { get; init; }
        public required string QueueName { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public bool IsOk { get; init; }
        public IReadOnlyList<string> Defects { get; init; } = Array.Empty<string>();
        public string StationName { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; } = DateTime.Now;
        public bool FromSimulation { get; init; }
    }

    /// <summary>Pack 级汇总结果（对齐旧 DetectionResultModel）。</summary>
    public sealed class Pack6ProductResult
    {
        public required string ProductCode { get; init; }
        public bool IsOk { get; init; }
        public bool TimedOut { get; init; }
        public IReadOnlyList<string> Defects { get; init; } = Array.Empty<string>();
        public DateTime CompletedAt { get; init; } = DateTime.Now;
    }
}
