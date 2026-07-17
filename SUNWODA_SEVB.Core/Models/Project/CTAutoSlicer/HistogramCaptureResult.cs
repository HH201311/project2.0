namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 直方图 ROI 截图与红线检测结果（屏幕坐标为物理像素）。
    /// </summary>
    public sealed class HistogramCaptureResult
    {
        public HistogramCaptureResult(
            string filePath,
            int roiX,
            int roiY,
            int roiWidth,
            int roiHeight,
            int? redLineColumn,
            int? redLineScreenX,
            int dragScreenY)
        {
            FilePath = filePath;
            RoiX = roiX;
            RoiY = roiY;
            RoiWidth = roiWidth;
            RoiHeight = roiHeight;
            RedLineColumn = redLineColumn;
            RedLineScreenX = redLineScreenX;
            DragScreenY = dragScreenY;
        }

        public string FilePath { get; }
        public int RoiX { get; }
        public int RoiY { get; }
        public int RoiWidth { get; }
        public int RoiHeight { get; }
        /// <summary>相对 ROI 左缘的红线列；未检测到为 null</summary>
        public int? RedLineColumn { get; }
        /// <summary>红线屏幕绝对 X</summary>
        public int? RedLineScreenX { get; }
        /// <summary>建议拖拽使用的屏幕 Y（ROI 垂直中线）</summary>
        public int DragScreenY { get; }
    }
}
