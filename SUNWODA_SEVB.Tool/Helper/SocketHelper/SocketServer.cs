using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.SocketHelper
{
    /// <summary>
    /// 服务端
    /// </summary>
    public class SocketServer : IDisposable
    {
        private ILoggerService _logger;
        /// <summary>
        /// 服务端IP
        /// </summary>
        public string ServerIp { get; private set; }
        /// <summary>
        /// 监听端口
        /// </summary>
        public int ServerPort { get; private set; }

        public event EventHandler<ClientEventArgs<string>>? OnClientConnected;
        public event EventHandler<ClientEventArgs<string>>? OnClientDisconnected;
        public event EventHandler<ClientEventArgs<byte[]>>? OnMessageReceived;
        public event EventHandler<Exception>? OnError;

        private Socket? _server;
        private bool _isRunning = false;
        private bool _disposed = false;
        private List<Socket> _connectedClients = new List<Socket>();
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverIp"></param>
        /// <param name="serverPort"></param>
        public SocketServer(string serverIp, int serverPort, ILoggerService logger)
        {
            _logger = logger;
            ServerIp = serverIp;
            ServerPort = serverPort;
        }

        /// <summary>
        /// 开启
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            try
            {
                lock (this)
                {
                    if (_isRunning) return;
                    _isRunning = true;
                }

                _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _server.Bind(new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort));
                _server.Listen(10); // 允许最大 10 个连接队列
                _cancellationTokenSource = new CancellationTokenSource();
                _logger.Info($"服务端已启动，监听地址: {ServerIp}:{ServerPort}");
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _server.AcceptAsync();
                        var msg = $"新客户端连接: {client.RemoteEndPoint}";
                        _logger.Info(msg);
                        _connectedClients.Add(client);
                        OnClientConnected?.Invoke(this, new ClientEventArgs<string>(client, msg));
                        // 启动任务处理该客户端
                        _ = HandleClientReceiveAsync(client, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"错误: {ex.Message}");
                        OnError?.Invoke(this, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.Error($"错误: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task HandleClientReceiveAsync(Socket client, CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[4096];
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var received = await client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    if (received == 0) break;
                    var bytes = new byte[received];
                    Array.Copy(buffer, 0, bytes, 0, received);
                    OnMessageReceived?.Invoke(this, new ClientEventArgs<byte[]>(client, bytes));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"客户端处理异常: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
            finally
            {
                // 从连接列表中移除
                DisconnectedClient(client);
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<int> SendToClientAsync(Socket client, string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            return await SendToClientAsync(client, buffer);
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<int> SendToClientAsync(Socket client, byte[] message)
        {
            if (!_connectedClients.Contains(client)) return 0;
            try
            {
                return await client.SendAsync(new ArraySegment<byte>(message), SocketFlags.None);
            }
            catch (Exception ex)
            {
                var msg = $"发送失败给客户端 {client.RemoteEndPoint}: {ex.Message}";
                _logger.Error(msg);
                // 移除断开的客户端
                DisconnectedClient(client);
                return 0;
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendToAllAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message + "\n"); // 确保消息格式兼容
            var disconnectedClients = new List<Socket>();
            var clients = new List<Socket>(_connectedClients);
            foreach (var client in clients)
            {
                var send = await SendToClientAsync(client, message);
            }
        }

        /// <summary>
        /// 溢出客户端
        /// </summary>
        /// <param name="client"></param>
        private void DisconnectedClient(Socket client)
        {
            lock (_connectedClients)
            {
                if (_connectedClients.Contains(client))
                {
                    _connectedClients.Remove(client);
                    var remoteEndPoint = client.RemoteEndPoint;
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client.Dispose();
                    var msg = $"客户端已断开连接:{remoteEndPoint}";
                    _logger.Info(msg);
                    OnClientDisconnected?.Invoke(this, new ClientEventArgs<string>(client, msg));
                }
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _cancellationTokenSource?.Cancel();
                _server?.Close();
                _server?.Dispose();
                _server = null;
                _logger.Info("服务端已停止");
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
