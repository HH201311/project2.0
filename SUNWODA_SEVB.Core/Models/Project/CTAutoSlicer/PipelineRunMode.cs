namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// CT 自动化流水线运行模式：切图与 AI 标注拆分为两台电脑独立执行。
    /// </summary>
    public enum PipelineRunMode
    {
        /// <summary>仅切图：导入→对齐→三视图导出→复制上视图送检图到交接目录，不跑 NJCT。</summary>
        SliceOnly = 0,

        /// <summary>仅 AI 标注：扫描交接目录批量调用 NJCT，不启 VG。</summary>
        AiAnnotateOnly = 1,
    }
}
