using SUNWODA_SEVB.Core.Models.Socket;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// Socket配置加载器接口
    /// </summary>
    public interface ISocketOptionsLoader
    {
        /// <summary>
        /// 从数据库加载配置
        /// </summary>
        Task<SocketServerOptions> LoadServerOptionsAsync(string moduleName);

        /// <summary>
        /// 从数据库加载配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        Task<SocketClientOptions> LoadClientOptionsAsync(string moduleName);

        /// <summary>
        /// 从数据库加载配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        Task<ProtocolOptions> LoadProtocolOptionsAsync(string moduleName);

        /// <summary>
        /// 更新配置到数据库
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        Task<bool> UpdateServerOptionsAsync(string moduleName, SocketServerOptions options);

        /// <summary>
        /// 更新配置到数据库
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        Task<bool> UpdateClientOptionsAsync(string moduleName, SocketClientOptions options);

        /// <summary>
        /// 更新配置到数据库
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        Task<bool> UpdateProtocolOptionsAsync(string moduleName, ProtocolOptions options);
    }
}
