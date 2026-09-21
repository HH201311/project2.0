namespace SUNWODA_SEVB.Core.Models.Project.ZZSIX
{
    /// <summary>收图入队项。</summary>
    public sealed class Pack6IngressItem
    {
        public required string FilePath { get; init; }
        public required string QueueName { get; init; }
        public string? ProductCode { get; init; }
        public string CameraName { get; init; } = string.Empty;
        public int ShotIndex { get; init; }
        public DateTime EnqueuedAt { get; init; } = DateTime.Now;
    }

    /// <summary>预览图上的识别框（像素坐标，相对原图）。</summary>
    public sealed class Pack6OverlayBox
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public string Label { get; init; } = string.Empty;
        /// <summary>ARGB，默认亮绿。</summary>
        public int Argb { get; init; } = unchecked((int)0xFF00FF66);
    }

    /// <summary>单帧检测结果。</summary>
    public sealed class Pack6FrameResult
    {
        public required string FilePath { get; init; }
        public required string QueueName { get; init; }
        public string ProductCode { get; init; } = string.Empty;
        public string CameraName { get; init; } = string.Empty;
        public int ShotIndex { get; init; }
        public bool IsOk { get; init; }
        public IReadOnlyList<string> Defects { get; init; } = Array.Empty<string>();
        public string StationName { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; } = DateTime.Now;
        public bool FromSimulation { get; init; }
        public Pack6OcrMode OcrMode { get; init; } = Pack6OcrMode.None;
        public string QrData { get; init; } = string.Empty;
        public string PartSN { get; init; } = string.Empty;
        public string ProductSN { get; init; } = string.Empty;
        public string FolderSn { get; init; } = string.Empty;
        public string EngineMessage { get; init; } = string.Empty;
        /// <summary>叠框标注图路径；空则预览用 <see cref="FilePath"/>。</summary>
        public string PreviewImagePath { get; init; } = string.Empty;
        /// <summary>视觉检测异常（DLL/Halcon 脚本/许可/抛错等），预览显示「视觉检测异常」。</summary>
        public bool IsVisionAlgorithmError { get; init; }
        /// <summary>规则/三码 NG 时叠在预览图左上角的文案。</summary>
        public string NgBanner { get; init; } = string.Empty;
        public IReadOnlyList<Pack6OverlayBox> OverlayBoxes { get; init; } = Array.Empty<Pack6OverlayBox>();
    }

    /// <summary>Pack 级汇总结果。</summary>
    public sealed class Pack6ProductResult
    {
        public required string ProductCode { get; init; }
        public bool IsOk { get; init; }
        public bool TimedOut { get; init; }
        public IReadOnlyList<string> Defects { get; init; } = Array.Empty<string>();
        public DateTime CompletedAt { get; init; } = DateTime.Now;
    }
}
