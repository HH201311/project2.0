using SUNWODA_SEVB.Core.Models.Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// Socket 客户端接口
    /// </summary>
    public interface ISocketClient : IAsyncDisposable
    {
        bool IsConnected { get; }
        string ClientId { get; }
        string RemoteEndPoint { get; }
        SocketClientOptions ActiveOptions { get; }

        /// <summary>
        /// 连接成功事件
        /// </summary>
        event EventHandler? Connected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        event EventHandler? Disconnected;

        /// <summary>
        /// 消息接收事件
        /// </summary>
        event EventHandler<ClientDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<ClientErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// 连接到服务器，成功返回 true，失败返回 false
        /// </summary>
        Task<bool> ConnectAsync(string moduleName, CancellationToken cancellationToken = default);
        Task<bool> ConnectAsync(string ip, int port, string moduleName, CancellationToken cancellationToken = default);

        Task DisconnectAsync();
        Task SendAsync(byte[] data, CancellationToken cancellationToken = default);
        Task SendAsync(string message, CancellationToken cancellationToken = default);
        Task SendAsync(string message, Encoding encoding, CancellationToken cancellationToken = default);
    }
}
