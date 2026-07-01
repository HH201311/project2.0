using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// Socket 服务管理器：统一管理多个端口的监听服务
    /// </summary>
    public interface ISocketServerManager : IAsyncDisposable
    {
        /// <summary>
        /// 当前所有监听端口
        /// </summary>
        IReadOnlyList<int> ListeningPorts { get; }

        /// <summary>
        /// 所有端口的总连接数
        /// </summary>
        int TotalConnectedClientCount { get; }

        /// <summary>
        /// 启动指定端口服务
        /// </summary>
        /// <param name="port"></param>
        /// <param name="moduleName"></param>
        /// <param name="ip"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> StartAsync(string moduleName, int port = 0, string? ip = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止指定端口服务
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        Task<bool> StopPortAsync(int port);

        /// <summary>
        /// 停止所有端口
        /// </summary>
        Task StopAllAsync();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        ISocketServer? GetSocketServerByPort(int port);
    }
}
