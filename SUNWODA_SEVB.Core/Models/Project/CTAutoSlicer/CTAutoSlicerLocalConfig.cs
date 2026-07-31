namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// CTAutoSlicer 本机配置（程序目录 JSON，不落库）。
    /// 用于机台角色等不宜进共享 DB 的项。
    /// </summary>
    public sealed class CTAutoSlicerLocalConfig
    {
        /// <summary>流水线模式：切图 / AI 标注。</summary>
        public PipelineRunMode PipelineRunMode { get; set; } = PipelineRunMode.SliceOnly;
    }
}
