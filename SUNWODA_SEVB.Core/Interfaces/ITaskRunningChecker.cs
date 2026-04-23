

namespace SUNWODA_SEVB.Core.Interfaces
{
    /// <summary>
    /// 用于检查 ViewModel 是否有运行中的任务
    /// </summary>
    public interface ITaskRunningChecker
    {
        /// <summary>
        /// 检查是否有运行中的任务
        /// </summary>
        Task<bool> HasRunningTasksAsync();
    }

    /// <summary>
    /// 支持清理的 ViewModel
    /// </summary>
    public interface ICleanupable
    {
        /// <summary>
        /// 执行清理工作
        /// </summary>
        Task CleanupAsync();
    }
}
