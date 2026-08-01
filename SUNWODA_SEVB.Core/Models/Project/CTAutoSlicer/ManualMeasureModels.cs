namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>单条两点测距（坐标为图像像素系）。</summary>
    public sealed class ManualMeasureItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>显示标签，默认 M1、M2…</summary>
        public string Label { get; set; } = string.Empty;

        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        /// <summary>像素长度 N_px = hypot(Δx, Δy)。</summary>
        public double PixelLength { get; set; }

        /// <summary>物理长度（μm）= N_px × pixelSizeUm。</summary>
        public double LengthUm { get; set; }

        /// <summary>物理长度（mm）= LengthUm / 1000。</summary>
        public double LengthMm { get; set; }

        /// <summary>
        /// 标签中心 X（图像像素）。null 表示使用默认垂直引出位置。
        /// </summary>
        public double? LabelX { get; set; }

        /// <summary>
        /// 标签中心 Y（图像像素）。null 表示使用默认垂直引出位置。
        /// </summary>
        public double? LabelY { get; set; }

        /// <summary>是否已手动拖拽过标签位置。</summary>
        public bool HasCustomLabelPosition => LabelX.HasValue && LabelY.HasValue;
    }

    /// <summary>单张图片的手动测距记录。</summary>
    public sealed class ManualMeasureImageRecord
    {
        /// <summary>相对打开根目录的路径（含文件名）。</summary>
        public string RelativePath { get; set; } = string.Empty;

        public string AbsolutePath { get; set; } = string.Empty;

        public bool IsAnnotated => Measures is { Count: > 0 };

        public List<ManualMeasureItem> Measures { get; set; } = [];
    }

    /// <summary>一次会话导出汇总（index.json）。</summary>
    public sealed class ManualMeasureSessionExport
    {
        public string RootFolder { get; set; } = string.Empty;

        public double PixelSizeUm { get; set; }

        public string PixelSizeUnit { get; set; } = "μm/px";

        public string UnitConversion { get; set; } =
            "1 mm = 1000 μm; L_μm = N_px × pixelSizeUm; L_mm = N_px × pixelSizeUm / 1000";

        public string GeneratedAt { get; set; } = string.Empty;

        public List<ManualMeasureImageRecord> Images { get; set; } = [];
    }
}
