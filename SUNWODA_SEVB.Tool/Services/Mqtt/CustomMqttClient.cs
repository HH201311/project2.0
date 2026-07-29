using HslCommunication;
using HslCommunication.MQTT;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Services.Socket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Mqtt
{
    /// <summary>
    /// 自定义 MQTT 客户端，继承自 HslCommunication 的 MqttClient，添加了默认构造函数以支持依赖注入
    /// </summary>
    public class CustomMqttClient : ICustomMqttClient
    {
        private readonly ILoggerService<CustomMqttClient> _logger;
        private readonly ICustomMqttOptionsLoader _optionsLoader;
        private readonly CustomMqttClientOptions _defaultOptions;

        // 当前生成的配置
        private CustomMqttClientOptions _activeOptions;
        private readonly SynchronizationContext? _synchronizationContext;

        private MqttClient? _mqttClient;
        private readonly object _lockObj = new();
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        // 统计信息
        private long _messagesSent;
        private long _messagesReceived;
        private long _bytesSent;
        private long _bytesReceived;
        private int _reconnectCount;
        private DateTime? _lastConnectedTime;
        private DateTime? _lastDisconnectedTime;

        /// <summary>
        /// 当前使用的 MQTT 连接配置
        /// </summary>
        public CustomMqttClientOptions ActiveOptions => _activeOptions;

        /// <summary>
        /// 当前 MQTT 客户端连接状态
        /// </summary>
        public bool IsConnected => _mqttClient?.IsConnected ?? false;

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string ClientId => _activeOptions?.ClientId ?? "Unknown";

        /// <summary>
        /// 远程IP地址
        /// </summary>
        public string RemoteIP => _mqttClient?.ConnectionOptions?.IpAddress ?? string.Empty;

        /// <summary>
        /// 远程端口
        /// </summary>
        public int RemotePort => _mqttClient?.ConnectionOptions?.Port ?? 0;

        /// <summary>
        /// 统计信息
        /// </summary>
        public CustomMqttStatistics Statistics => new CustomMqttStatistics
        {
            MessagesSent = _messagesSent,
            MessagesReceived = _messagesReceived,
            BytesSent = _bytesSent,
            BytesReceived = _bytesReceived,
            ReconnectCount = _reconnectCount,
            LastConnectedTime = _lastConnectedTime,
            LastDisconnectedTime = _lastDisconnectedTime,
            IsConnected = IsConnected,
            ActiveOptions = _activeOptions
        };

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<CustomMqttConnectionChangedEventArgs>? ConnectionChanged;

        /// <summary>
        /// 消息接收事件
        /// </summary>
        public event EventHandler<MqttApplicationMessage>? MessageReceived;

        /// <summary>
        /// 错误事件
        /// </summary>
        public event EventHandler<CustomMqttErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// 使用自定义的连接选项创建 MQTT 客户端实例
        /// </summary>
        /// <param name="options"></param>
        public CustomMqttClient(ILoggerService<CustomMqttClient> logger,
            ICustomMqttOptionsLoader optionsLoader,
            IOptions<CustomMqttClientOptions> defaultOptions)
        {
            _logger = logger;
            _optionsLoader = optionsLoader;
            _defaultOptions = defaultOptions.Value ?? new CustomMqttClientOptions();
            _activeOptions = _defaultOptions;

            // 捕获当前同步上下文（WPF的Dispatcher）
            _synchronizationContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// 连接 MQTT 服务器
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        public async Task<OperateResult> ConnectAsync(string moduleName)
        {
            return await ConnectAsync(moduleName, string.Empty, 0);
        }

        /// <summary>
        /// 连接 MQTT 服务器
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public async Task<OperateResult> ConnectAsync(string moduleName, string ip, int port)
        {
            if (IsConnected)
            {
                _logger.Warn($"Mqtt 客户端已连接，无需重复连接");
                return OperateResult.CreateFailedResult<string>("Mqtt 客户端已连接，无需重复连接", null);
            }

            await _connectionLock.WaitAsync();
            try
            {
                // 从数据库加载配置
                _activeOptions = await _optionsLoader.LoadMqttClientOptionsAsync(moduleName);
                _activeOptions.ModuleName = moduleName;

                // 清理旧连接
                await DisconnectInternalAsync();
                var options = _activeOptions.GetMqttConnectionOptions();
                if (!string.IsNullOrEmpty(ip)) options.IpAddress = ip;
                if (port > 0 && port < 65536) options.Port = port;

                // 创建新的MQTT客户端
                _mqttClient = new MqttClient(options);
                _mqttClient.UseTimerCheckDropped = true;

                RegisterMqttEvents();

                // 连接服务器
                _logger.Info($"Connecting to MQTT server: {_activeOptions.IpAddress}:{_activeOptions.Port}, ClientId: {ClientId}");
                var result = await _mqttClient.ConnectServerAsync();
                _reconnectCount++;
                if (result.IsSuccess)
                {
                    _lastConnectedTime = DateTime.Now;
                    _logger.Info($"MQTT connected successfully: {ClientId}");
                    // 自动订阅配置的主题
                    await AutoSubscribeTopicsAsync();
                }
                else
                {
                    _lastDisconnectedTime = DateTime.Now;
                    _logger.Error($"MQTT connection failed: {ClientId}, Error: {result.Message}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error connecting MQTT client: {ClientId}", ex);
                RaiseErrorOccurred("Connect", ex);
                return new OperateResult($"Connection error: {ex.Message}");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// 断开MQTT连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _connectionLock.WaitAsync();

            try
            {
                await DisconnectInternalAsync();
                _logger.Info($"MQTT disconnected: {ClientId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disconnecting MQTT client: {ClientId}", ex);
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
            if (_mqttClient == null || !_mqttClient.IsConnected) return;

            try
            {
                // hsl库中ConnectCloseAsync有问题(IsConnecte属性赋值错误！)，改用ConnectClose
                //await _mqttClient.ConnectCloseAsync();
                _mqttClient?.ConnectClose();
                _mqttClient?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error closing MQTT connection ClientId: {ClientId}", ex);
            }
            finally
            {
                _mqttClient = null;
                _lastDisconnectedTime = DateTime.Now;
                RaiseConnectionChanged(false);
            }
        }

        /// <summary>
        /// 订阅主题
        /// </summary>
        public async Task<OperateResult> SubscribeAsync(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return new OperateResult("Topic cannot be null or empty");
            }

            if (_mqttClient?.IsConnected != true)
            {
                return new OperateResult("MQTT client is not connected");
            }

            try
            {
                _logger.Info($"Subscribing to topic: {topic}");
                var result = await _mqttClient.SubscribeMessageAsync(topic);

                if (result.IsSuccess)
                {
                    _logger.Info($"Subscribed to topic successfully: {topic}, ClientId: {ClientId}");
                }
                else
                {
                    _logger.Error($"Failed to subscribe to topic: {topic}, ClientId: {ClientId}, Error: {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error subscribing to topic: {topic}, ClientId: {ClientId}", ex);
                RaiseErrorOccurred("Subscribe", ex);
                return new OperateResult($"Subscribe error: {ex.Message}");
            }
        }

        /// <summary>
        /// 订阅多个主题
        /// </summary>
        public async Task<OperateResult> SubscribeAsync(string[] topics)
        {
            if (topics == null || topics.Length == 0)
            {
                return new OperateResult("Topics cannot be null or empty");
            }

            if (_mqttClient?.IsConnected != true)
            {
                return new OperateResult("MQTT client is not connected");
            }

            try
            {
                _logger.Info($"Subscribing to topics: {string.Join(", ", topics)}");

                var result = await _mqttClient.SubscribeMessageAsync(topics);

                if (result.IsSuccess)
                {
                    _logger.Info($"Subscribed to topics successfully: {string.Join(", ", topics)}, ClientId: {ClientId}");
                }
                else
                {
                    _logger.Error($"Failed to subscribe to topics: {string.Join(", ", topics)}, ClientId: {ClientId}, Error: {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error subscribing to topics: {string.Join(", ", topics)}, ClientId: {ClientId}", ex);
                RaiseErrorOccurred("Subscribe", ex);
                return new OperateResult($"Subscribe error: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消订阅主题
        /// </summary>
        public async Task<OperateResult> UnsubscribeAsync(string topic)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return new OperateResult("Topic cannot be null or empty");
            }

            if (_mqttClient?.IsConnected != true)
            {
                return new OperateResult("MQTT client is not connected");
            }

            try
            {
                _logger.Info($"Unsubscribing from topic: {topic}");

                var result = await _mqttClient.UnSubscribeMessageAsync(topic);

                if (result.IsSuccess)
                {
                    _logger.Info($"Unsubscribed from topic successfully: {topic}, ClientId: {ClientId}");
                }
                else
                {
                    _logger.Error($"Failed to unsubscribe from topic: {topic}, ClientId: {ClientId}, Error: {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error unsubscribing from topic: {topic}, ClientId: {ClientId}", ex);
                RaiseErrorOccurred("Unsubscribe", ex);
                return new OperateResult($"Unsubscribe error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布消息
        /// </summary>
        public async Task<OperateResult> PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false)
        {
            if (string.IsNullOrEmpty(topic))
            {
                return new OperateResult("Topic cannot be null or empty");
            }

            if (_mqttClient?.IsConnected != true)
            {
                return new OperateResult("MQTT client is not connected");
            }

            try
            {
                var message = new MqttApplicationMessage
                {
                    Topic = topic,
                    Payload = Encoding.UTF8.GetBytes(payload ?? string.Empty),
                    QualityOfServiceLevel = qos,
                    Retain = retain
                };

                _logger.Debug($"Publishing message to topic: {topic}, QoS: {qos}, Retain: {retain}");

                var result = await _mqttClient.PublishMessageAsync(message);

                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref _messagesSent);
                    Interlocked.Add(ref _bytesSent, message.Payload?.Length ?? 0);

                    _logger.Debug($"Message published successfully to topic: {topic}");
                }
                else
                {
                    _logger.Error($"Failed to publish message to topic: {topic}, ClientId: {ClientId}, Error: {result.Message}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error publishing message to topic: {topic}, ClientId: {ClientId}", ex);
                RaiseErrorOccurred("Publish", ex);
                return new OperateResult($"Publish error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发布消息（使用MqttApplicationMessage对象）
        /// </summary>
        public async Task<OperateResult> PublishAsync(MqttApplicationMessage message)
        {
            if (message == null)
            {
                return new OperateResult("Message cannot be null");
            }

            if (_mqttClient?.IsConnected != true)
            {
                return new OperateResult("MQTT client is not connected");
            }

            try
            {
                _logger.Debug($"Publishing message to topic: {message.Topic}");

                var result = await _mqttClient.PublishMessageAsync(message);

                if (result.IsSuccess)
                {
                    Interlocked.Increment(ref _messagesSent);
                    Interlocked.Add(ref _bytesSent, message.Payload?.Length ?? 0);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error publishing message to topic: {message.Topic}, ClientId: {ClientId}", ex);
                RaiseErrorOccurred("Publish", ex);
                return new OperateResult($"Publish error: {ex.Message}");
            }
        }

        /// <summary>
        /// 注册MQTT事件
        /// </summary>
        private void RegisterMqttEvents()
        {
            if (_mqttClient == null) return;

            // 连接成功事件
            _mqttClient.OnClientConnected += (client) =>
            {
                ExecuteOnSynchronizationContext(() =>
                {
                    _lastConnectedTime = DateTime.Now;
                    RaiseConnectionChanged(true);
                    _logger.Info($"MQTT client connected event: {ClientId}");
                });
            };

            // 网络异常事件
            _mqttClient.OnNetworkError += (sender, args) =>
            {
                ExecuteOnSynchronizationContext(() =>
                {
                    _lastDisconnectedTime = DateTime.Now;
                    RaiseConnectionChanged(false);
                    RaiseErrorOccurred("NetworkError", new Exception("Network error occurred"));
                    _logger.Warn($"MQTT client network error: {ClientId}");
                });
            };

            // 消息接收事件
            _mqttClient.OnMqttMessageReceived += (client, message) =>
            {
                ExecuteOnSynchronizationContext(() =>
                {
                    Interlocked.Increment(ref _messagesReceived);
                    Interlocked.Add(ref _bytesReceived, message.Payload?.Length ?? 0);

                    _logger.Debug($"MQTT message received: Topic={message.Topic}, ClientId={ClientId}, PayloadLength={message.Payload?.Length ?? 0}");

                    // 处理消息
                    //_messageHandler.HandleMqttMessageAsync(ClientId, message).ConfigureAwait(false);

                    // 触发消息接收事件
                    RaiseMessageReceived(message);
                });
            };
        }

        /// <summary>
        /// 自动订阅配置的主题
        /// </summary>
        private async Task AutoSubscribeTopicsAsync()
        {
            if (_activeOptions.SubscribeTopics == null) return;
            var topics = _activeOptions.SubscribeTopics.Where(o => o.IsSubscribe && !string.IsNullOrEmpty(o.Topic)).Select(o => o.Topic ?? string.Empty).ToArray();
            if (topics == null || topics.Length == 0) return;
            _logger.Info($"Auto-subscribing to topics: {string.Join(", ", topics)}, ClientId: {ClientId}");

            var result = await SubscribeAsync(topics);
            if (!result.IsSuccess)
            {
                _logger.Warn($"Failed to auto-subscribe some topics: {result.Message}");
            }
        }

        /// <summary>
        /// 在同步上下文中执行
        /// </summary>
        private void ExecuteOnSynchronizationContext(Action action)
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

        /// <summary>
        /// 触发连接状态变化事件
        /// </summary>
        private void RaiseConnectionChanged(bool isConnected)
        {
            ConnectionChanged?.Invoke(this, new CustomMqttConnectionChangedEventArgs
            {
                IsConnected = isConnected
            });
        }

        /// <summary>
        /// 触发消息接收事件
        /// </summary>
        private void RaiseMessageReceived(MqttApplicationMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        /// <summary>
        /// 触发错误事件
        /// </summary>
        private void RaiseErrorOccurred(string operation, Exception ex)
        {
            ErrorOccurred?.Invoke(this, new CustomMqttErrorEventArgs
            {
                Operation = operation,
                Exception = ex,
            });
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            _disposed = true;

            await DisconnectAsync();
            _connectionLock?.Dispose();

            _logger.Info($"CustomMqttClient disposed: {ClientId}");
        }
    }
}