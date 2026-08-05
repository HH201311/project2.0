using SUNWODA_SEVB.Core.Models.Socket;
using System.Diagnostics;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// Socket服务端接口
    /// </summary>
    public interface ISocketServer : IAsyncDisposable
    {
        /// <summary>
        /// 获取或设置一个值，该值指示是否启用内置的默认心跳检测与自动回复功能。
        /// 该功能由帮助类提供，通常不需要使用。如需自定义心跳逻辑，可禁用此功能并自行实现。
        /// </summary>
        bool IsBuiltInHeartbeatEnabled { get; set; }

        /// <summary>
        /// 获取或设置一个值，该值指示收到消息后是否启用内置的自动回复功能。
        /// 该功能由帮助类提供，通常不需要使用。如需自定义自动回复逻辑，可禁用此属性并自行实现。
        /// </summary>
        bool IsBuiltInAutoReplyEnabled { get; set; }

        /// <summary>
        /// 获取当前生效的服务器配置。
        /// </summary>
        SocketServerOptions ActiveOptions { get; }

        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 监听端口
        /// </summary>
        int Port { get; }

        /// <summary>
        /// 已连接客户端数量
        /// </summary>
        int ConnectedClientCount { get; }

        /// <summary>
        /// 数据接收事件
        /// </summary>
        event EventHandler<Models.Socket.DataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        event EventHandler<ClientConnectionEventArgs>? ClientConnected;

        /// <summary>
        /// 客户端断开事件
        /// </summary>
        event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;

        /// <summary>
        /// 错误事件
        /// </summary>
        event EventHandler<ServerErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// 启动服务
        /// </summary>
        Task<bool> StartAsync(int port, string moduleName, string? ip,CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止服务
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 发送响应给客户端
        /// </summary>
        Task<bool> SendResponseAsync(string clientEndPoint, SocketResponse response, CancellationToken cancellationToken = default);

        /// <summary>
        /// 向指定客户端发送数据
        /// </summary>
        Task<bool> SendToClientAsync(string clientEndPoint, byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 向指定客户端发送字符串
        /// </summary>
        Task<bool> SendToClientAsync(string clientEndPoint, string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// 广播数据到所有客户端
        /// </summary>
        Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 广播字符串到所有客户端
        /// </summary>
        Task BroadcastAsync(string message, CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开指定客户端
        /// </summary>
        Task DisconnectClientAsync(string clientEndPoint);

        /// <summary>
        /// 获取所有已连接客户端
        /// </summary>
        IReadOnlyList<IClientSession> GetConnectedClients();
    }
}

  
