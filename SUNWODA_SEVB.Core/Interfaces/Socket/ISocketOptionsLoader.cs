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
        Task<SocketServerOptions> LoadOptionsAsync(string moduleName);
    }
}
