using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.SocketHelper
{
    /// <summary>
    /// 客户端
    /// </summary>
    public class SocketClient : IDisposable
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

        public event EventHandler<string>? OnConnected;
        public event EventHandler<string>? OnDisconnected;
        public event EventHandler<byte[]>? OnMessageReceived;
        public event EventHandler<Exception>? OnError;

        private Socket? _client;
        private bool _isConnected = false;
        private bool _disposed = false;
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverIp"></param>
        /// <param name="serverPort"></param>
        public SocketClient(string serverIp, int serverPort, ILoggerService logger)
        {
            _logger = logger;
            ServerIp = serverIp;
            ServerPort = serverPort;
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        /// <returns></returns>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                lock (this)
                {
                    if (_isConnected) return true;
                    _isConnected = true;
                }
                _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await _client.ConnectAsync(new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort));
                var msg = $"已连接到 {ServerIp}:{ServerPort}";
                _logger.Info(msg);
                _cancellationTokenSource = new CancellationTokenSource();
                _ = ReceiveLoopAsync(_cancellationTokenSource.Token);
                OnConnected?.Invoke(this, msg);
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.Error($"错误:{ex.Message}");
                OnError?.Invoke(this, ex);
                return false;
            }
        }

        /// <summary>
        /// 重新连接
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ReConnectAsync()
        {
            try
            {
                _client = _client ?? new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await _client.ConnectAsync(new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort));
                _logger.Info("重连成功！");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"重连异常:{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 锻炼
        /// </summary>
        public void Disconnect()
        {
            lock (this)
            {
                if (!_isConnected) return;
                _isConnected = false;
                _cancellationTokenSource?.Cancel();
                _client?.Close();
                _client?.Dispose();
                _client = null;
                OnDisconnected?.Invoke(this, "已断开连接");
            }
        }

        /// <summary>
        /// 循环接收消息
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || !_client.Connected)
                    {
                        if (!_isConnected) break;
                        _logger.Info("服务器已断开连接，正在尝试重连！");
                        var re = await ReConnectAsync();
                        continue;
                    }

                    var received = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    if (received == 0)
                    {
                        if (!_isConnected) break;
                        _logger.Info("服务器已断开连接，正在尝试重连！");
                        var re = await ReConnectAsync();
                        continue;
                    }

                    var bytes = new byte[received];
                    Array.Copy(buffer, 0, bytes, 0, received);
                    OnMessageReceived?.Invoke(this, bytes);
                }
                catch (Exception ex)
                {
                    _logger.Error($"异常: {ex.Message}");
                    OnError?.Invoke(this, ex);
                }
            }
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<int> SendAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            return await SendAsync(buffer);
        }

        /// <summary>
        /// 异步发送
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<int> SendAsync(byte[] message)
        {
            if (_disposed || !_isConnected) return 0;

            try
            {
                if (_client == null || !_client.Connected) return 0;
                return await _client.SendAsync(new ArraySegment<byte>(message), SocketFlags.None);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                return 0;
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }
}
