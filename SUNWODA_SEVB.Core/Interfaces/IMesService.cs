
namespace SUNWODA_SEVB.Core.Interfaces
{
    /// <summary>
    /// MES服务基础接口
    /// </summary>
    public interface IMesService
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        string ServiceName { get; }

        ///// <summary>
        ///// 服务版本
        ///// </summary>
        //string GetVersion();

        /// <summary>
        /// 服务是否已启用
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 初始化服务
        /// </summary>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 重置服务
        /// </summary>
        Task ResetAsync();
        /// <summary>
        /// 动态更新MES启用状态
        /// </summary>
        Task<bool> UpdateEnabledStatusAsync();
    }
}
