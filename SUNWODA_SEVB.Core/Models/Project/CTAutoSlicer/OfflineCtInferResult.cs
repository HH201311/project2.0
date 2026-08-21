namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 单次推理结构化结果（坐标已从 HTuple 提取为可序列化数据）。
    /// </summary>
    public sealed class OfflineCtInferResult
    {
        /// <summary>增强图或原图标识。</summary>
        public string ModeTag { get; init; } = string.Empty;

        /// <summary>结果图落盘路径（成功写盘后填充）。</summary>
        public string? ResultImagePath { get; set; }

        /// <summary>接口 RunInfo；空表示成功。</summary>
        public string RunInfo { get; init; } = string.Empty;

        /// <summary>是否检测成功（RunInfo 为空）。</summary>
        public bool IsSuccess => string.IsNullOrEmpty(RunInfo);

        /// <summary>每个电芯的 row 坐标（L1~L5）。</summary>
        public IReadOnlyList<double[]> RegionRows { get; init; } = Array.Empty<double[]>();

        /// <summary>每个电芯的 col 坐标（L1~L5）。</summary>
        public IReadOnlyList<double[]> RegionCols { get; init; } = Array.Empty<double[]>();

        /// <summary>每个电芯的尺寸数据。</summary>
        public IReadOnlyList<double[]> PrintfValues { get; init; } = Array.Empty<double[]>();
    }
}
