namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT AI 标注：结果图输出模式。
    /// </summary>
    public enum OfflineCtResultImageMode
    {
        /// <summary>仅增强图（ModelAction）</summary>
        Enhance = 0,

        /// <summary>仅原图（ModelAction_origin）</summary>
        Origin = 1,

        /// <summary>增强图 + 原图都输出</summary>
        Both = 2,
    }
}
