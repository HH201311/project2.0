using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.YWSIX
{
    /// <summary>
    /// 已处理原图标记（本地索引，不改 Y/Z 原图）。
    /// 用于掉线/重启后补扫未处理文件，避免漏检与重复检。
    /// </summary>
    public interface IYWSIXPack6ProcessedStore
    {
        /// <summary>打开/切换索引目录（通常为 LocalPathPre/state）。</summary>
        void Open(Pack6Config config);

        bool IsProcessed(string fullPath);

        void MarkProcessed(string fullPath);

        void Close();
    }
}
