using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// MES配置提供者接口
    /// </summary>
    public interface IMesConfigurationProvider
    {
        /// <summary>
        /// 从数据库获取MES配置
        /// </summary>
        Task<MesApiConfiguration?> GetConfigurationAsync();

        /// <summary>
        /// 重新加载配置
        /// </summary>
        Task<bool> ReloadConfigurationAsync();

        /// <summary>
        /// 获取当前配置
        /// </summary>
        MesApiConfiguration? CurrentConfiguration { get; }        
    }
}
