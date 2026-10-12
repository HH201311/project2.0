using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    /// <summary>对齐枣庄：原始图片 + 结果图片归档，含 NG 缺陷子目录与磁盘清理。</summary>
    public interface INJSIXPack6ImageArchiveService
    {
        /// <summary>确保图像数据根目录存在。</summary>
        void EnsureRoots(Pack6Config config);

        /// <summary>
        /// 归档单帧：拷贝原图到「原始图片」，结果图到「结果图片」；
        /// NG 时按缺陷大类/细分建子目录（可多份）。
        /// </summary>
        void ArchiveFrame(Pack6Config config, Pack6FrameResult frame);

        /// <summary>
        /// 磁盘占用超过阈值时，删除「图像数据」下最早日期目录；
        /// 优先删满保留年限以外的日期，仍超限再删最早（保吞吐）。
        /// </summary>
        void PurgeIfNeeded(Pack6Config config);
    }
}
