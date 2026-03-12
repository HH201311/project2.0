using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    public sealed class SocketServer : ISocketServer
    {
        private System.Net.Sockets.Socket? _listenerSocket;
        private CancellationTokenSource? _serverCts;
        private readonly ConcurrentDictionary<string, SocketClientSession> _clients = new();
        private readonly object _lockObj = new();
        private bool _disposed;

        private readonly ILoggerService<SocketServer> _logger;
        private readonly IMessageHandler _messageHandler;
        private readonly ISocketOptionsLoader _optionsLoader;
        private readonly SocketServerOptions _defaultOptions;
        private readonly SynchronizationContext? _synchronizationContext;     

        // 当前生效的配置
        private SocketServerOptions _activeOptions;
        private IFrameParser? _frameParser;

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public int ConnectedClientCount => _clients.Count(c => c.Value.IsConnected);

        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        public event EventHandler<ClientConnectionEventArgs>? ClientConnected;
        public event EventHandler<ClientConnectionEventArgs>? ClientDisconnected;
        public event EventHandler<ServerErrorEventArgs>? ErrorOccurred;

        // 构造函数注入 MessageHandler
        public SocketServer(ILoggerService<SocketServer> logger, IMessageHandler messageHandler, ISocketOptionsLoader optionsLoader,
            IOptions<SocketServerOptions> defaultOptions)
        {
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _optionsLoader = optionsLoader ?? throw new ArgumentNullException(nameof(optionsLoader));
            _defaultOptions = defaultOptions?.Value ?? new SocketServerOptions();
            _activeOptions = _defaultOptions;

            // 捕获当前同步上下文（WPF的Dispatcher）
            _synchronizationContext = SynchronizationContext.Current;
        }

        public async Task<bool> StartAsync(int port, string moduleName, string? ip,CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                _logger.Info("服务已在运行中");
                return false;
            }
            try
            {
                // 从数据库加载配置
                _activeOptions = await _optionsLoader.LoadOptionsAsync(moduleName);
                _frameParser = new FrameParser(_activeOptions.Protocol);
                // 如果传入端口为0，使用配置中的端口
                var actualPort = port > 0 ? port : _activeOptions.DefaultPort;
                lock (_lockObj)
                {
                    
                    _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    _listenerSocket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    _listenerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                    var listenAddress = !string.IsNullOrWhiteSpace(ip) && IPAddress.TryParse(ip, out var parsedIp) ? parsedIp: IPAddress.Any;
                    var endPoint = new IPEndPoint(listenAddress, actualPort);
                    _listenerSocket.Bind(endPoint);
                    _listenerSocket.Listen(_activeOptions.Backlog);

                    Port = actualPort;
                    IsRunning = true;
                }

                _logger.Info($"Socket服务已启动，监听端口: {Port}", true);
                // 启动监听任务
                _ = AcceptClientsLoopAsync(_serverCts.Token);

                return true;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                _logger.Error($"端口 {port} 已被占用", ex,true);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"启动服务失败: {ex.Message}", ex,true);
                await CleanupAsync();
                return false;
            }
        }

        public async Task StopAsync()
        {
            if (!IsRunning) return;

            lock (_lockObj)
            {
                IsRunning = false;
            }
            _logger.Info("正在停止Socket服务...", true);
            await CleanupAsync();
            _logger.Info("Socket服务已停止", true);
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private async Task CleanupAsync()
        {
            try
            {
                _serverCts?.Cancel();
            }
            catch { }

            // 关闭所有客户端连接
            var disconnectTasks = _clients.Values.Select(async session =>
            {
                try
                {
                    await session.DisposeAsync();
                }
                catch { }
            });
            await Task.WhenAll(disconnectTasks);
            _clients.Clear();

            // 关闭监听Socket
            try
            {
                _listenerSocket?.Close();
                _listenerSocket?.Dispose();
            }
            catch { }

            _listenerSocket = null;
            _serverCts?.Dispose();
            _serverCts = null;
        }

        public async Task<bool> SendToClientAsync(string clientEndPoint, byte[] data, CancellationToken cancellationToken = default)
        {
            var session = FindClientByEndPoint(clientEndPoint);
            if (session == null)
            {
                _logger.Error($"客户端 {clientEndPoint} 不存在",true);
                return false;
            }

            return await session.SendAsync(data, cancellationToken);
        }

        public Task<bool> SendToClientAsync(string clientEndPoint, string message, CancellationToken cancellationToken = default)
        {
            return SendToClientAsync(clientEndPoint, Encoding.UTF8.GetBytes(message), cancellationToken);
        }

        public async Task BroadcastAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            var tasks = _clients.Values
                .Where(c => c.IsConnected)
                .Select(c => c.SendAsync(data, cancellationToken));

            await Task.WhenAll(tasks);
        }
        public Task BroadcastAsync(string message, CancellationToken cancellationToken = default)
        {
            return BroadcastAsync(Encoding.UTF8.GetBytes(message), cancellationToken);
        }

        public async Task DisconnectClientAsync(string clientEndPoint)
        {
            var session = FindClientByEndPoint(clientEndPoint);
            if (session != null)
            {
                await RemoveClientAsync(session);
            }
        }

        public IReadOnlyList<IClientSession> GetConnectedClients()
        {
            return (IReadOnlyList<IClientSession>)_clients.Values.Where(c => c.IsConnected).ToList();

        }

        public async Task<bool> SendResponseAsync(string clientEndPoint, SocketResponse response, CancellationToken cancellationToken = default)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            // 确保有时间戳
            if (!response.Timestamp.HasValue)
                response.Timestamp = DateTime.Now;

            var json = response.ToJson();
            return await SendToClientAsync(clientEndPoint, json, cancellationToken);
        }


        /// <summary>
        /// 根据EndPoint查找客户端
        /// </summary>
        private SocketClientSession? FindClientByEndPoint(string endPoint)
        {
            return _clients.Values.FirstOrDefault(c => c.EndPoint == endPoint);
        }
        /// <summary>
        /// 移除客户端
        /// </summary>
        private async Task RemoveClientAsync(SocketClientSession session)
        {
            if (_clients.TryRemove(session.Id, out _))
            {
                _logger.Info($"客户端已断开连接: {session.EndPoint}", true);
                RaiseClientDisconnected(session);
                await session.DisposeAsync();
            }
        }

        private async Task AcceptClientsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && IsRunning )
            {
                try
                {
                    if (ConnectedClientCount >= _activeOptions.MaxClients)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }
                    var clientSocket = await _listenerSocket!.AcceptAsync(cancellationToken);
                    var session = new SocketClientSession(clientSocket);

                    if (_clients.TryAdd(session.Id, session))
                    {
                        _logger.Info($"客户端已连接: {session.EndPoint}", true);
                        RaiseClientConnected(session);
                        _ = HandleClientAsync(session, cancellationToken);
                    }
                    else
                    {
                        await session.DisposeAsync();
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                    {
                        _logger.Error( $"接受客户端连接时发生错误", ex,true);
                        RaiseErrorOccurred(ex, "接受客户端连接时发生错误");
                    }
                }
            }
        }

        private async Task HandleClientAsync(SocketClientSession session, CancellationToken cancellationToken)
        {
            var buffer = new byte[_activeOptions.ReceiveBufferSize];
            var memoryStream = new MemoryStream();
            try
            {
                using var timeoutCts = _activeOptions.ReceiveTimeout.HasValue
                 ? new CancellationTokenSource()
                 : null;

                while (!cancellationToken.IsCancellationRequested && session.IsConnected)
                {
                    if (_activeOptions.ReceiveTimeout.HasValue)
                    {
                        timeoutCts?.CancelAfter(_activeOptions.ReceiveTimeout.Value);
                    }

                    using var linkedCts = timeoutCts != null
                        ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                        : null;

                    var token = linkedCts?.Token ?? cancellationToken;

                    int bytesRead;
                    try
                    {
                        bytesRead = await session.ReceiveAsync(buffer, token);
                    }
                    catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true)
                    {
                        timeoutCts.TryReset();
                        continue;
                    }
                    if (bytesRead == 0) break;

                    await ProcessReceivedData(session, buffer, bytesRead, memoryStream, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException)
            {
                // 如果是Socket或IO异常，且服务器正在运行，才记录错误
                if (IsRunning)
                {
                    _logger.Error($"客户端 {session.EndPoint} 通信异常: {ex.Message}");
                    RaiseErrorOccurred(ex, $"客户端 {session.EndPoint} 通信异常", session.EndPoint);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"客户端 {session.EndPoint} 通信异常: {ex.Message}");
                RaiseErrorOccurred(ex, $"客户端 {session.EndPoint} 通信异常", session.EndPoint);
            }
            finally
            {
                memoryStream.Dispose();
                await RemoveClientAsync(session);
            }
        }

        private static bool IsHeartbeatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var trimmed = message.Trim();

            return string.Equals(trimmed, "Send", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(trimmed, "HEARTBEAT", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(trimmed, "PING", StringComparison.OrdinalIgnoreCase);
        }

        private async Task SendHeartbeatResponseAsync(SocketClientSession session, CancellationToken cancellationToken)
        {
            try
            {
                await session.SendAsync(Encoding.UTF8.GetBytes("OK"), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.Warn($"发送心跳响应失败: {ex.Message}",true);
            }
        }

        private async Task ProcessReceivedData(SocketClientSession session,byte[] buffer,int bytesRead,MemoryStream memoryStream,CancellationToken cancellationToken)
        {
            _logger.Info($"接收到 {bytesRead} 字节数据",true);
            ////var receivedHex = BitConverter.ToString(buffer, 0, bytesRead);
            //var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            //_logger.Info($"接收到 {bytesRead} 字节: TEXT=[{receivedText}]", true);

            memoryStream.Write(buffer, 0, bytesRead);

            var data = memoryStream.ToArray();

            int offset = 0;
            while (offset < data.Length)
            {
                var result = _frameParser!.TryParseFrame(data, offset, data.Length - offset);

                if (!result.Success)
                    break;

                // 处理完整帧
                if (result.FrameData != null)
                {
                    await ProcessCompleteMessage(session, result.FrameData, cancellationToken);
                }

                offset += result.ConsumedBytes;
            }


            // 保存未处理完的数据
            memoryStream.SetLength(0);
            if (offset < data.Length)
            {
                memoryStream.Write(data, offset, data.Length - offset);
            }
        }

        private async Task ProcessCompleteMessage(
           SocketClientSession session,
           byte[] messageBytes,
           CancellationToken cancellationToken)
        {
            var message = Encoding.UTF8.GetString(messageBytes, 0, messageBytes.Length).Trim();

            if (IsHeartbeatMessage(message))
            {
                await SendHeartbeatResponseAsync(session, cancellationToken);
                return;
            }

            // 消息传递给业务层处理
            RaiseDataReceived(session, messageBytes);

            try
            {
                var response = await _messageHandler.HandleMessageAsync(message, session.EndPoint);

                if (!await SendResponseAsync(session.EndPoint, response, cancellationToken))
                {
                    _logger.Warn($"向客户端 {session.EndPoint} 发送响应失败", true);                 
                }
            }
            catch (Exception ex)
            {
                _logger.Error("处理消息失败",ex);
                RaiseErrorOccurred(ex, "处理消息失败", session.EndPoint);
                var errorResponse = SocketResponse.Error($"消息处理失败: {ex.Message}");
                await SendResponseAsync(session.EndPoint, errorResponse, cancellationToken);
            }
        }



        private void RaiseDataReceived(SocketClientSession session, byte[] data)
        {
            var args = new DataReceivedEventArgs
            {
                ClientEndPoint = session.EndPoint,
                ClientId = session.Id,
                Data = data,
                ReceivedTime = DateTime.Now
            };

            InvokeOnSyncContext(() => DataReceived?.Invoke(this, args));
        }

        private void RaiseClientConnected(SocketClientSession session)
        {
            var args = new ClientConnectionEventArgs
            {
                ClientId = session.Id,
                EndPoint = session.EndPoint,
                ConnectionTime = session.ConnectedTime,
                IsConnected = true
            };

            InvokeOnSyncContext(() => ClientConnected?.Invoke(this, args));
        }

        private void RaiseClientDisconnected(SocketClientSession session)
        {
            var args = new ClientConnectionEventArgs
            {
                ClientId = session.Id,
                EndPoint = session.EndPoint,
                ConnectionTime = session.ConnectedTime,
                IsConnected = false
            };

            InvokeOnSyncContext(() => ClientDisconnected?.Invoke(this, args));
        }

        /// <summary>
        /// 在同步上下文（UI线程）上执行
        /// </summary>
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

        private void RaiseErrorOccurred(Exception exception, string message, string? clientEndPoint = null)
        {
            var args = new ServerErrorEventArgs
            {
                Exception = exception,
                Message = message,
                ClientEndPoint = clientEndPoint
            };

            InvokeOnSyncContext(() => ErrorOccurred?.Invoke(this, args));
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            await StopAsync();
        }


    }
}
