namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 直方图 ROI 截图、红线检测、掩膜优化与峰谷分析结果（屏幕坐标为物理像素）。
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
            int dragScreenY,
            string? compareImagePath = null,
            string? peaksImagePath = null,
            int maskPixelCount = 0,
            bool peaksSuccess = false,
            string? peaksFailReason = null,
            int leftPeakX = 0,
            int valleyX = 0,
            int rightPeakX = 0,
            int targetX = 0,
            int? targetScreenX = null)
        {
            FilePath = filePath;
            RoiX = roiX;
            RoiY = roiY;
            RoiWidth = roiWidth;
            RoiHeight = roiHeight;
            RedLineColumn = redLineColumn;
            RedLineScreenX = redLineScreenX;
            DragScreenY = dragScreenY;
            CompareImagePath = compareImagePath;
            PeaksImagePath = peaksImagePath;
            MaskPixelCount = maskPixelCount;
            PeaksSuccess = peaksSuccess;
            PeaksFailReason = peaksFailReason;
            LeftPeakX = leftPeakX;
            ValleyX = valleyX;
            RightPeakX = rightPeakX;
            TargetX = targetX;
            TargetScreenX = targetScreenX;
        }

        public string FilePath { get; }
        public int RoiX { get; }
        public int RoiY { get; }
        public int RoiWidth { get; }
        public int RoiHeight { get; }
        public int? RedLineColumn { get; }
        public int? RedLineScreenX { get; }
        public int DragScreenY { get; }
        public string? CompareImagePath { get; }
        public string? PeaksImagePath { get; }
        public int MaskPixelCount { get; }
        public bool PeaksSuccess { get; }
        public string? PeaksFailReason { get; }
        public int LeftPeakX { get; }
        public int ValleyX { get; }
        public int RightPeakX { get; }
        public int TargetX { get; }
        public int? TargetScreenX { get; }
    }
}
