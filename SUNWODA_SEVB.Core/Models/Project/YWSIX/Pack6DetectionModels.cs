namespace SUNWODA_SEVB.Core.Models.Project.YWSIX
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

    /// <summary>识别码文相对检测框的放置位置。</summary>
    public enum Pack6OverlayLabelPlacement
    {
        /// <summary>框下方（产品码 / 铭牌）。</summary>
        Below = 0,
        /// <summary>框上方（二维码绿框）。</summary>
        Above = 1,
        /// <summary>框右侧（序列码黄框）；右侧放不下改左侧。</summary>
        Right = 2
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
        /// <summary>码文放置：绿上方 / 黄右侧 / 青下方 / 铭牌右侧。</summary>
        public Pack6OverlayLabelPlacement LabelPlacement { get; init; } = Pack6OverlayLabelPlacement.Below;

        /// <summary>相对默认放置的额外纵向像素（正=下，负=上），用于同图微调避让。</summary>
        public float LabelOffsetY { get; init; }
    }

    /// <summary>预览图上的点标记（Halcon：Row=Y，Col=X）。</summary>
    public sealed class Pack6OverlayPoint
    {
        public float Col { get; init; }
        public float Row { get; init; }
        public string Label { get; init; } = string.Empty;
        public int Argb { get; init; } = unchecked((int)0xFFFF6600);
    }

    /// <summary>预览图上的线段（连接 PIN 中心等）。</summary>
    public sealed class Pack6OverlayLine
    {
        public float Col1 { get; init; }
        public float Row1 { get; init; }
        public float Col2 { get; init; }
        public float Row2 { get; init; }
        public int Argb { get; init; } = unchecked((int)0xFFFFFF00);
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
        /// <summary>同图双检时的铭牌产品编码；单独铭牌帧亦写入 ProductSN。</summary>
        public string NameplateProductSN { get; init; } = string.Empty;
        /// <summary>白标签数模版本号（DLL version，如 M00）。</summary>
        public string Version { get; init; } = string.Empty;
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
        /// <summary>本包是否做过三码 OCR（任一路）。统计「三码校验」时用。</summary>
        public bool ThreeCodeChecked { get; init; }
        /// <summary>三码一套齐全且通过（同图双检 + 单独白标签均 OK）。整包只计一次。</summary>
        public bool ThreeCodeOk { get; init; }
    }
}
