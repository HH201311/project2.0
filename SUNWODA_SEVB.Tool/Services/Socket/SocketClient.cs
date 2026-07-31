using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// Socket TCP 客户端实现
    /// </summary>
    /// <summary>
    /// Socket 客户端，支持依赖注入、帧解析等功能
    /// </summary>
    public class SocketClient : ISocketClient
    {
        private readonly ILoggerService<SocketClient> _logger;
        private readonly ISocketOptionsLoader _optionsLoader;
        private readonly SocketClientOptions _defaultOptions;

        // 当前生效的配置
        private SocketClientOptions _activeOptions;
        private readonly SynchronizationContext? _synchronizationContext;

        private System.Net.Sockets.Socket? _socket;
        private readonly object _lockObj = new();
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _receiveCts;
        private IFrameParser? _frameParser;
        private MemoryStream? _receiveBuffer;
        private bool _disposed;

        /// <summary>
        /// 当前使用的 Socket 连接配置
        /// </summary>
        public SocketClientOptions ActiveOptions => _activeOptions;

        /// <summary>
        /// 当前 Socket 客户端连接状态
        /// </summary>
        public bool IsConnected => _socket?.Connected ?? false;

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId => _activeOptions?.ModuleName ?? "Unknown";

        /// <summary>
        /// 远程地址
        /// </summary>
        public string RemoteEndPoint => _socket?.RemoteEndPoint?.ToString() ?? string.Empty;

        /// <summary>
        /// 连接成功事件
        /// </summary>
        public event EventHandler? Connected;

        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event EventHandler? Disconnected;

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event EventHandler<ClientDataReceivedEventArgs>? DataReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event EventHandler<ClientErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// 创建 Socket 客户端实例
        /// </summary>
        public SocketClient(
            ILoggerService<SocketClient> logger,
            ISocketOptionsLoader optionsLoader,
            IOptions<SocketClientOptions> defaultOptions)
        {
            _logger = logger;
            _optionsLoader = optionsLoader;
            _defaultOptions = defaultOptions.Value ?? new SocketClientOptions();
            _activeOptions = _defaultOptions;

            // 捕获当前同步上下文（WPF的Dispatcher）
            _synchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// 连接到 Socket 服务器，成功返回 true，失败返回 false
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(string moduleName, CancellationToken cancellationToken = default)
        {
            return await ConnectAsync(string.Empty, 0, moduleName, cancellationToken);
        }

        /// <summary>
        /// 连接到 Socket 服务器，成功返回 true，失败返回 false
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="moduleName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<bool> ConnectAsync(string ip, int port, string moduleName, CancellationToken cancellationToken = default)
        {
            if (IsConnected)
            {
                _logger.Warn($"Socket 客户端已连接，无需重复连接");
                return true;
            }

            await _connectionLock.WaitAsync();
            try
            {
                // 从数据库加载配置
                _activeOptions = await _optionsLoader.LoadClientOptionsAsync(moduleName);
                _activeOptions.ModuleName = moduleName;

                // 清理旧连接
                await DisconnectInternalAsync();

                // 初始化帧解析器
                _frameParser = new FrameParser(_activeOptions.Protocol);

                // 创建Socket
                _socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // 设置Socket选项
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (_activeOptions.ReceiveBufferSize > 0) _socket.ReceiveBufferSize = _activeOptions.ReceiveBufferSize;
                _socket.SendTimeout = _activeOptions.SendTimeout;
                _socket.ReceiveTimeout = _activeOptions.ReceiveTimeout;

                var activeIp = string.IsNullOrWhiteSpace(ip) ? _activeOptions.DefaultIP : ip;
                var activePort = port > 0 && port < 65536 ? port : _activeOptions.DefaultPort;

                // 解析服务器地址
                if (!IPAddress.TryParse(activeIp, out var ipAddress))
                {
                    var hostEntry = await Dns.GetHostEntryAsync(activeIp);
                    ipAddress = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                    if (ipAddress == null)
                    {
                        _logger.Error($"无法解析主机名: {activeIp}");
                        return false;
                    }
                }

                var endPoint = new IPEndPoint(ipAddress, activePort);

                _logger.Info($"Connecting to Socket server: {activeIp}:{activePort}, ClientId: {ClientId}");

                using var connectCts = new CancellationTokenSource(_activeOptions.ConnectTimeout);
                try
                {
                    await _socket.ConnectAsync(endPoint, connectCts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.Error($"Socket connection timeout: {ClientId}");
                    await CleanupFailedConnectionAsync();
                    return false;
                }
                catch (SocketException ex)
                {
                    _logger.Error($"Socket connection failed: {ClientId}, Error: {ex.Message}");
                    await CleanupFailedConnectionAsync();
                    return false;
                }

                // 初始化接收缓冲区
                _receiveBuffer = new MemoryStream();

                _logger.Info($"Socket connected successfully: {ClientId} -> {RemoteEndPoint}");

                // 触发连接成功事件
                RaiseConnected();

                // 启动接收任务
                _receiveCts = new CancellationTokenSource();
                _ = ReceiveLoopAsync(_receiveCts.Token);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error connecting Socket client: {ClientId}", ex);
                RaiseErrorOccurred("Connect", ex);
                await CleanupFailedConnectionAsync();
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// 连接失败时清理资源
        /// </summary>
        private async Task CleanupFailedConnectionAsync()
        {
            try
            {
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _receiveCts = null;
            }
            catch { }

            try
            {
                _receiveBuffer?.Dispose();
                _receiveBuffer = null;
            }
            catch { }

            try
            {
                _socket?.Dispose();
            }
            catch { }
            finally
            {
                _socket = null;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 断开 Socket 连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();

            try
            {
                await DisconnectInternalAsync();
                RaiseDisconnected();
                _logger.Info($"Socket disconnected: {ClientId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disconnecting Socket client: {ClientId}", ex);
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// 内部断开连接方法
        /// </summary>
        private async Task DisconnectInternalAsync()
        {
            try
            {
                _receiveCts?.Cancel();
                _receiveCts?.Dispose();
                _receiveCts = null;
            }
            catch { }

            try
            {
                _receiveBuffer?.Dispose();
                _receiveBuffer = null;
            }
            catch { }

            try
            {
                if (_socket != null)
                {
                    try
                    {
                        _socket.Shutdown(SocketShutdown.Both);
                    }
                    catch { }

                    _socket.Close();
                    _socket.Dispose();
                }
            }
            catch { }
            finally
            {
                _socket = null;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 接收数据循环
        /// </summary>
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[_activeOptions.ReceiveBufferSize];

            while (!cancellationToken.IsCancellationRequested && IsConnected)
            {
                try
                {
                    var bytesRead = await _socket!.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None, cancellationToken);
                    if (bytesRead == 0)
                    {
                        _logger.Warn($"Socket server closed connection: {ClientId}");
                        break;
                    }

                    await ProcessReceivedDataAsync(buffer, bytesRead, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // 主动取消，正常退出
                    _logger.Debug($"接收循环取消: {ClientId}");
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    // Socket 超时（由 ReceiveTimeout 触发），继续等待
                    _logger.Debug($"接收超时，继续等待: {ClientId}");
                    continue;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // 连接被对方重置
                    _logger.Warn($"连接被重置: {ClientId}");
                    break;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
                {
                    // 被信号中断，可继续
                    _logger.Debug($"接收被中断: {ClientId}");
                    continue;
                }
                catch (SocketException ex)
                {
                    _logger.Error($"Socket 接收错误: {ClientId}, ErrorCode={ex.SocketErrorCode}", ex);
                    RaiseErrorOccurred("Receive", ex);
                    break;
                }
                catch (ObjectDisposedException)
                {
                    _logger.Debug($"Socket 已释放: {ClientId}");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"接收数据异常: {ClientId}", ex);
                    RaiseErrorOccurred("Receive", ex);
                    break;
                }
            }

            // 连接断开
            if (IsConnected)
            {
                await DisconnectInternalAsync();
                RaiseDisconnected();
            }
        }

        /// <summary>
        /// 处理接收到的数据
        /// </summary>
        private async Task ProcessReceivedDataAsync(byte[] buffer, int bytesRead, CancellationToken cancellationToken)
        {
            _receiveBuffer!.Write(buffer, 0, bytesRead);

            var data = _receiveBuffer.ToArray();
            int offset = 0;

            while (offset < data.Length)
            {
                var result = _frameParser!.TryParseFrame(data, offset, data.Length - offset);

                if (!result.Success) break;

                if (result.FrameData != null)
                {
                    _logger.Debug($"Socket message received: ClientId={ClientId}, Length={result.FrameData.Length}");
                    RaiseMessageReceived(result.FrameData);
                }

                offset += result.ConsumedBytes;
            }

            // 保存未处理完的数据
            _receiveBuffer.SetLength(0);
            if (offset < data.Length)
            {
                _receiveBuffer.Write(data, offset, data.Length - offset);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送字节数组
        /// </summary>
        public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty");
            }

            if (!IsConnected || _socket == null)
            {
                throw new InvalidOperationException("Socket client is not connected");
            }

            try
            {
                _logger.Debug($"Sending message: ClientId={ClientId}, Length={data.Length}");

                using var sendCts = new CancellationTokenSource(_activeOptions.SendTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sendCts.Token);
                await _socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None, linkedCts.Token);

                _logger.Debug($"Message sent successfully: ClientId={ClientId}, Length={data.Length}");
            }
            catch (OperationCanceledException)
            {
                _logger.Error($"Send timeout: ClientId={ClientId}");
                throw new TimeoutException($"Send timeout: {_activeOptions.SendTimeout}ms");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error sending message: ClientId={ClientId}", ex);
                RaiseErrorOccurred("Send", ex);
                throw;
            }
        }

        /// <summary>
        /// 发送字符串消息
        /// </summary>
        public Task SendAsync(string message, CancellationToken cancellationToken = default)
        {
            return SendAsync(Encoding.UTF8.GetBytes(message), cancellationToken);
        }

        /// <summary>
        /// 发送字符串消息（使用指定编码）
        /// </summary>
        public Task SendAsync(string message, Encoding encoding, CancellationToken cancellationToken = default)
        {
            return SendAsync(encoding.GetBytes(message), cancellationToken);
        }

        #region 事件触发

        private void InvokeOnSyncContext(Action action)
        {
            if (_synchronizationContext != null)
            {
                _synchronizationContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }

        private void RaiseConnected()
        {
            InvokeOnSyncContext(() => Connected?.Invoke(this, EventArgs.Empty));
        }

        private void RaiseDisconnected()
        {
            InvokeOnSyncContext(() => Disconnected?.Invoke(this, EventArgs.Empty));
        }

        private void RaiseMessageReceived(byte[] data)
        {
            var args = new ClientDataReceivedEventArgs
            {
                RemoteEndPoint = RemoteEndPoint,
                Data = data,
                ReceivedTime = DateTime.Now
            };

            InvokeOnSyncContext(() => DataReceived?.Invoke(this, args));
        }

        private void RaiseErrorOccurred(string message, Exception ex)
        {
            var args = new ClientErrorEventArgs
            {
                RemoteEndPoint = RemoteEndPoint,
                Message = message,
                Exception = ex
            };

            InvokeOnSyncContext(() => ErrorOccurred?.Invoke(this, args));
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;

            await DisconnectInternalAsync();
            _connectionLock?.Dispose();

            _logger.Info($"SocketClient disposed: {ClientId}");
        }
    }
}
