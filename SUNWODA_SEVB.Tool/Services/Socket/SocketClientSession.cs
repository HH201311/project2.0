using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// 客户端会话实现
    /// </summary>
    public sealed class SocketClientSession : IAsyncDisposable
    {
        private readonly System.Net.Sockets.Socket _socket;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private bool _disposed;
        private volatile bool _isConnected;

        /// <summary>
        /// 会话唯一标识
        /// </summary>
        public string Id { get; }

        // 添加客户端ID属性
        public string ClientId { get; set; } = string.Empty;
        /// <summary>
        /// 客户端端点地址
        /// </summary>
        public string EndPoint { get; }

        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectedTime { get; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        public DateTime LastActivityTime { get; private set; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _isConnected && !_disposed && _socket.Connected;

        /// <summary>
        /// 自定义标签数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// 接收的字节数
        /// </summary>
        public long ReceivedBytes { get; private set; }

        /// <summary>
        /// 发送的字节数
        /// </summary>
        public long SentBytes { get; private set; }

        public SocketClientSession(System.Net.Sockets.Socket socket)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            Id = Guid.NewGuid().ToString("N");
            EndPoint = socket.RemoteEndPoint?.ToString() ?? "Unknown";
            ConnectedTime = DateTime.Now;
            LastActivityTime = DateTime.Now;
            _isConnected = true;
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
                return 0;

            try
            {
                var bytesRead = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                if (bytesRead > 0)
                {
                    LastActivityTime = DateTime.Now;
                    ReceivedBytes += bytesRead;
                }
                else
                {
                    _isConnected = false;
                }
                return bytesRead;
            }
            catch (SocketException)
            {
                _isConnected = false;
                throw;
            }
            catch (ObjectDisposedException)
            {
                _isConnected = false;
                throw;
            }
        }

        /// <summary>
        /// 发送字节数据
        /// </summary>
        public async Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || data == null || data.Length == 0)
                return false;

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsConnected)
                    return false;

                var totalSent = 0;
                while (totalSent < data.Length)
                {
                    var sent = await _socket.SendAsync(
                        new ArraySegment<byte>(data, totalSent, data.Length - totalSent),
                        SocketFlags.None,
                        cancellationToken);

                    if (sent == 0)
                    {
                        _isConnected = false;
                        return false;
                    }

                    totalSent += sent;
                }

                LastActivityTime = DateTime.Now;
                SentBytes += data.Length;
                return true;
            }
            catch (SocketException)
            {
                _isConnected = false;
                return false;
            }
            catch (ObjectDisposedException)
            {
                _isConnected = false;
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// 发送字符串数据
        /// </summary>
        public Task<bool> SendAsync(string message, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            encoding ??= Encoding.UTF8;
            var data = encoding.GetBytes(message);
            return SendAsync(data, cancellationToken);
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            _isConnected = false;
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                }
            }
            catch { }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isConnected = false;

            Close();

            try
            {
                _socket.Close();
                _socket.Dispose();
            }
            catch { }

            _sendLock.Dispose();

            await ValueTask.CompletedTask;
        }
    }
}
