using HslCommunication;
using HslCommunication.MQTT;
using iText.Forms.Xfdf;
using Masuit.Tools;
using Microsoft.Extensions.Hosting;
using MySqlX.XDevAPI;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB;
using SUNWODA_SEVB.Core.Enumerations.Project.CCTB.FormEnums;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.FormModels;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Services.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Data.Repositories;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SUNWODA_SEVB.Core.Interfaces.Project.CCTB.ICommunicationService;

namespace SUNWODA_SEVB.Services.Project.CCTB
{
    /// <summary>
    /// 通信服务
    /// </summary>
    public class CommunicationService : ICommunicationService
    {
        private readonly ILoggerService<CommunicationService> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;
        private readonly IPLCConfigRepository _plcConfigRepository;
        private readonly IPLCService _plcService;
        private readonly ISocketServerManager _socketServerManager;
        private readonly ISocketClientFactory _socketClientFactory;
        private readonly ICustomMqttClientFactory _mqttClientFactory;

        private bool isInitialized;
        /// <summary>
        /// 是否初始化完成
        /// </summary>
        public bool IsInitialized => isInitialized;

        // Socket 服务端
        private ISocketServer? wmSocketServer, dhSocketServer, ccdSocketServer;
        public ISocketServer? WMSocketServer => wmSocketServer;
        public ISocketServer? DHSocketServer => dhSocketServer;
        public ISocketServer? CCDSocketServer => ccdSocketServer;

        // Socket 客户端
        private ISocketClient? wmSocketClient, dhASocketClient, dhBSocketClient;
        private ISocketClient? opmmPLCSocketClient, opmmCCDSocketClient, opmmWeightSocketClient;
        public ISocketClient? WMSocketClient => wmSocketClient;
        public ISocketClient? DHASocketClient => dhASocketClient;
        public ISocketClient? DHBSocketClient => dhBSocketClient;
        public ISocketClient? OPMMPLCSocketClient => opmmPLCSocketClient;
        public ISocketClient? OPMMCCDSocketClient => opmmCCDSocketClient;
        public ISocketClient? OPMMWeightSocketClient => opmmWeightSocketClient;

        // MQTT 客户端
        private ICustomMqttClient? dhAMqttClient, dhBMqttClient;
        public ICustomMqttClient? DHAMqttClient => dhAMqttClient;
        public ICustomMqttClient? DHBMqttClient => dhBMqttClient;

        // 事件
        public event EventHandler<DeviceStatusEventArgs>? DeviceStatusChanged;

        // 配置缓存
        private CommunicationSettingModel? settings;

        public CommunicationSettingModel? Settings => settings;

        public CommunicationService(
            ILoggerService<CommunicationService> logger,
            IProjectSettingRepository projectSettingRepository,
            IPLCConfigRepository plcConfigRepository,
            IPLCService plcService,
            ISocketServerManager socketServerManager,
            ISocketClientFactory socketClientFactory,
            ICustomMqttClientFactory mqttClientFactory)
        {
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
            _plcConfigRepository = plcConfigRepository;
            _plcService = plcService;
            _socketServerManager = socketServerManager;
            _socketClientFactory = socketClientFactory;
            _mqttClientFactory = mqttClientFactory;
        }

        #region 启动/停止所有服务
        /// <summary>
        /// 启动所有设备
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAllAsync(CancellationToken cancellationToken = default)
        {
            if (IsInitialized) return;
            try
            {
                _logger.Info("开始初始化所有通讯服务...");
                settings = LoadSettings();
                await InitializeSocketServersAsync();
                await InitializeSocketClientsAsync();
                await InitializeMqttClientAsync();
                _logger.Info("所有通讯服务初始化完成");
            }
            finally { isInitialized = true; }
        }

        /// <summary>
        /// 停止所有设备
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StopAllAsync(CancellationToken cancellationToken = default)
        {
            _logger.Info("正在停止所有通讯服务...");

            // 停止服务端
            await StopSocketServerAsync(wmSocketServer, WMSocketServerDeviceName);
            await StopSocketServerAsync(dhSocketServer, DHSocketServerDeviceName);
            await StopSocketServerAsync(ccdSocketServer, CCDSocketServerDeviceName);
            wmSocketServer = dhSocketServer = ccdSocketServer = null;

            // 停止客户端
            await StopSocketClientAsync(wmSocketClient, WMSocketClientDeviceName);
            await StopSocketClientAsync(dhASocketClient, DHASocketClientDeviceName);
            await StopSocketClientAsync(dhBSocketClient, DHBSocketClientDeviceName);
            wmSocketClient = dhASocketClient = dhBSocketClient = null;

            await StopSocketClientAsync(opmmPLCSocketClient, OPMMPLCSocketClientDeviceName);
            await StopSocketClientAsync(opmmCCDSocketClient, OPMMCCDSocketClientDeviceName);
            await StopSocketClientAsync(opmmWeightSocketClient, OPMMWeightSocketClientDeviceName);
            opmmPLCSocketClient = opmmCCDSocketClient = opmmWeightSocketClient = null;

            // 停止 MQTT
            await StopMqttClientAsync(dhAMqttClient, DHAMqttClientDeviceName);
            await StopMqttClientAsync(dhBMqttClient, DHBMqttClientDeviceName);
            dhAMqttClient = dhAMqttClient = null;

            _logger.Info("所有通讯服务已停止");
        }

        /// <summary>
        /// 根据模块名停止指定的通讯服务
        /// </summary>
        private async Task StopByModuleNameAsync(string deviceName)
        {
            _logger.Info($"正在停止模块 [{deviceName}]...");

            try
            {
                // Socket 服务端
                if (deviceName == WMSocketServerDeviceName) await StopSocketServerAsync(wmSocketServer, deviceName);
                else if (deviceName == DHSocketServerDeviceName) await StopSocketServerAsync(dhSocketServer, deviceName);
                else if (deviceName == CCDSocketServerDeviceName) await StopSocketServerAsync(ccdSocketServer, deviceName);
                // Socket 客户端
                else if (deviceName == WMSocketClientDeviceName) await StopSocketClientAsync(wmSocketClient, deviceName);
                else if (deviceName == DHASocketClientDeviceName) await StopSocketClientAsync(dhASocketClient, deviceName);
                else if (deviceName == DHBSocketClientDeviceName) await StopSocketClientAsync(dhBSocketClient, deviceName);
                else if (deviceName == OPMMPLCSocketClientDeviceName) await StopSocketClientAsync(opmmPLCSocketClient, deviceName);
                else if (deviceName == OPMMCCDSocketClientDeviceName) await StopSocketClientAsync(opmmCCDSocketClient, deviceName);
                else if (deviceName == OPMMWeightSocketClientDeviceName) await StopSocketClientAsync(opmmWeightSocketClient, deviceName);
                // MQTT 客户端
                else if (deviceName == DHAMqttClientDeviceName) await StopMqttClientAsync(dhAMqttClient, deviceName);
                else if (deviceName == DHBMqttClientDeviceName) await StopMqttClientAsync(dhBMqttClient, deviceName);
                // 不存在
                else _logger.Warn($"未知模块 [{deviceName}]，无法停止");
            }
            catch (Exception ex)
            {
                _logger.Error($"停止模块 [{deviceName}] 失败", ex);
                throw;
            }
        }
        #endregion

        #region Socket 服务端
        /// <summary>
        /// 初始化 Socket 服务器
        /// </summary>
        /// <returns></returns>
        public async Task InitializeSocketServersAsync()
        {
            try
            {
                if (settings == null) throw new Exception("通讯参数初始化配置为空！");

                // 测重仪服务端模式
                if (settings.WMCommunicationMode == ECommunicationMode.服务端Socket && settings.WMSocketServerEnabled)
                {
                    var port = settings.WMSocketServerPort;
                    var server = await StartSocketServerAsync(WMSocketServerDeviceName, port);
                    SetModuleField(WMSocketServerDeviceName, server);
                }

                // 模头服务端
                if (settings.DHCommunicationMode == ECommunicationMode.客户端Socket && settings.DHSocketServerEnabled)
                {
                    var port = settings.DHSocketServerPort;
                    var server = await StartSocketServerAsync(DHSocketServerDeviceName, port);
                    SetModuleField(DHSocketServerDeviceName, server);
                }

                // CCD 服务端
                if (settings.CCDSocketServerEnabled)
                {
                    var port = settings.CCDSocketServerPort;
                    var server = await StartSocketServerAsync(CCDSocketServerDeviceName, port);
                    SetModuleField(CCDSocketServerDeviceName, server);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("服务器启动异常！", ex);
            }
        }

        /// <summary>
        /// 启动 Socket 服务器
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private async Task<ISocketServer?> StartSocketServerAsync(string deviceName, int port)
        {
            if (string.IsNullOrEmpty(deviceName)) throw new ArgumentNullException(nameof(deviceName));
            if (port <= 0 || port > 65536) throw new ArgumentOutOfRangeException(nameof(port));

            var initializeMessage = !IsInitialized ? "初始化" : string.Empty;
            var moduleName = ModuleToConfigGroupMap[deviceName];
            _logger.Info($"正在{initializeMessage}启动 {deviceName} 服务端，端口：{port}...");
            try
            {
                var result = await _socketServerManager.StartAsync(moduleName, port);
                if (!result)
                {
                    _logger.Error($"Socket 服务端{initializeMessage}启动失败[{deviceName} Port:{port}]");
                    HandyControl.Controls.Growl.WarningGlobal($"Socket 服务端{initializeMessage}启动失败[{deviceName} Port:{port}]");
                    return null;
                }

                var server = _socketServerManager.GetSocketServerByPort(port);
                if (server == null || !server.IsRunning)
                {
                    _logger.Error($"Socket 服务端{initializeMessage}启动后状态异常[{deviceName} Port:{port}]");
                    HandyControl.Controls.Growl.ErrorGlobal($"Socket 服务端{initializeMessage}启动后状态异常[{deviceName} Port:{port}]");
                    await _socketServerManager.StopPortAsync(port).ConfigureAwait(false);
                    return null;
                }

                // 注册事件
                RegisterSocketServerHandler(server, deviceName);

                _logger.Info($"Socket 服务端{initializeMessage}启动成功 [{deviceName}] 端口: {port}");
                HandyControl.Controls.Growl.SuccessGlobal($"Socket 服务{initializeMessage}启动成功 [{deviceName}] 端口: {port}");
                return server;
            }
            catch (Exception ex)
            {
                _logger.Error($"Socket 服务端{initializeMessage}启动异常 [{deviceName} Port:{port}]", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"Socket 服务端{initializeMessage}启动异常 [{deviceName} Port:{port}] Message:{ex.Message}");
                try { await _socketServerManager.StopPortAsync(port); } catch { }
                return null;
            }
        }

        /// <summary>
        /// 停止 Socket 服务器
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        private async Task StopSocketServerAsync(ISocketServer? server, string deviceName)
        {
            if (server == null) return;
            var port = server.Port;
            _logger.Info($"正在停止 Socket 服务端 [{deviceName}] 端口: {port}...");
            try
            {
                server.DataReceived -= WMSocketServer_OnDataReceived;
                server.DataReceived -= DHSocketServer_OnDataReceived;
                server.DataReceived -= CCDSocketServer_OnDataReceived;
                server.ErrorOccurred -= SocketServer_OnErrorOccurred;
                server.ClientConnected -= SocketServer_OnClientConnected;
                server.ClientDisconnected -= SocketServer_OnClientDisconnected;
                await _socketServerManager.StopPortAsync(port);
                SetModuleField(deviceName, (ISocketServer?)null);

                _logger.Info($"Socket 服务端已停止 [{deviceName}] 端口: {port}");
                HandyControl.Controls.Growl.WarningGlobal($"Socket 服务端停止成功 [{deviceName}] 端口: {port}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Socket 服务端停止异常 [{deviceName}] 端口: {port}", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"Socket 服务端停止异常 [{deviceName}] 端口: {port} 异常：[{ex.Message}]");
            }
        }

        /// <summary>
        /// 注册 Socket 服务端事件
        /// </summary>
        /// <param name="server"></param>
        /// <param name="deviceName"></param>
        private void RegisterSocketServerHandler(ISocketServer? server, string deviceName)
        {
            if (server == null || string.IsNullOrEmpty(deviceName)) return;

            server.ErrorOccurred -= SocketServer_OnErrorOccurred;
            server.ErrorOccurred += SocketServer_OnErrorOccurred;
            server.ClientConnected -= SocketServer_OnClientConnected;
            server.ClientConnected += SocketServer_OnClientConnected;
            server.ClientDisconnected -= SocketServer_OnClientDisconnected;
            server.ClientDisconnected += SocketServer_OnClientDisconnected;

            if (deviceName == WMSocketServerDeviceName)
            {
                server.DataReceived -= WMSocketServer_OnDataReceived;
                server.DataReceived += WMSocketServer_OnDataReceived;
                return;
            }
            if (deviceName == DHSocketServerDeviceName)
            {
                server.DataReceived -= DHSocketServer_OnDataReceived;
                server.DataReceived += DHSocketServer_OnDataReceived;
                return;
            }
            if (deviceName == CCDSocketServerDeviceName)
            {
                server.DataReceived -= CCDSocketServer_OnDataReceived;
                server.DataReceived += CCDSocketServer_OnDataReceived;
                return;
            }
        }

        private void WMSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据：[{message}]");
        }

        private void DHSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据：[{message}]");
        }

        private void CCDSocketServer_OnDataReceived(object? sender, DataReceivedEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据：[{message}]");
        }

        private void SocketServer_OnClientDisconnected(object? sender, ClientConnectionEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.WarningGlobal($"[{deviceName}] 客户端断开：[{e.EndPoint}]");
        }

        private void SocketServer_OnClientConnected(object? sender, ClientConnectionEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 客户端连接：[{e.EndPoint}]");
        }

        private void SocketServer_OnErrorOccurred(object? sender, ServerErrorEventArgs e)
        {
            if (sender is not ISocketServer server) return;
            var deviceName = server.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.ErrorGlobal($"[{deviceName}] 错误：[{e.Message}]");
        }
        #endregion

        #region Socket 客户端
        /// <summary>
        /// 初始化 Socket 客户端
        /// </summary>
        /// <returns></returns>
        public async Task InitializeSocketClientsAsync()
        {
            try
            {
                if (settings == null) return;

                if (settings.WMCommunicationMode == ECommunicationMode.客户端Socket && settings.WMSocketClientEnabled)
                {
                    var ip = settings.WMSocketClientIP;
                    var port = settings.WMSocketClientPort;
                    var client = await StartSocketClientAsync(WMSocketClientDeviceName, ip, port);
                    SetModuleField(WMSocketClientDeviceName, client);
                }
                if (settings.DHCommunicationMode == ECommunicationMode.客户端Socket && settings.DHASocketClientEnabled)
                {
                    var ip = settings.DHASocketClientIP;
                    var port = settings.DHASocketClientPort;
                    var client = await StartSocketClientAsync(DHASocketClientDeviceName, ip, port);
                    SetModuleField(DHASocketClientDeviceName, client);
                }
                if (settings.DHCommunicationMode == ECommunicationMode.客户端Socket && settings.DHBSocketClientEnabled)
                {
                    var ip = settings.DHBSocketClientIP;
                    var port = settings.DHBSocketClientPort;
                    var client = await StartSocketClientAsync(DHBSocketClientDeviceName, ip, port);
                    SetModuleField(DHBSocketClientDeviceName, client);
                }
                if (settings.OPMMPLCSocketClientEnabled)
                {
                    var ip = settings.OPMMSocketClientIP;
                    var port = settings.OPMMPLCSocketClientPort;
                    var client = await StartSocketClientAsync(OPMMPLCSocketClientDeviceName, ip, port);
                    SetModuleField(OPMMPLCSocketClientDeviceName, client);
                }
                if (settings.OPMMCCDSocketClientEnabled)
                {
                    var ip = settings.OPMMSocketClientIP;
                    var port = settings.OPMMCCDSocketClientPort;
                    var client = await StartSocketClientAsync(OPMMCCDSocketClientDeviceName, ip, port);
                    SetModuleField(OPMMCCDSocketClientDeviceName, client);
                }
                if (settings.OPMMWeightSocketClientEnabled)
                {
                    var ip = settings.OPMMSocketClientIP;
                    var port = settings.OPMMWeightSocketClientPort;
                    var client = await StartSocketClientAsync(OPMMWeightSocketClientDeviceName, ip, port);
                    SetModuleField(OPMMWeightSocketClientDeviceName, client);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Socket 客户端初始化异常", ex);
            }
        }

        /// <summary>
        /// 启动 Socket 客户端
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private async Task<ISocketClient?> StartSocketClientAsync(string deviceName, string? ip, int port)
        {
            if (string.IsNullOrEmpty(deviceName)) throw new ArgumentNullException(nameof(deviceName));
            if (string.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port <= 0 || port > 65536) throw new ArgumentOutOfRangeException(nameof(port));

            var initializeMessage = !IsInitialized ? "初始化" : string.Empty;
            var moduleName = ModuleToConfigGroupMap[deviceName];
            var remoteEndPoint = $"{ip}:{port}";
            _logger.Info($"正在{initializeMessage}连接 Socket 客户端 [{deviceName}] 远程地址：{remoteEndPoint}...");
            try
            {
                var client = _socketClientFactory.CreateClient();

                // 注册事件
                RegisterSocketClientHandler(client, deviceName);

                var result = await client.ConnectAsync(ip, port, moduleName);
                if (!result || !client.IsConnected)
                {
                    _logger.Error($"Socket 客户端{initializeMessage}连接失败[{deviceName}] 远程地址：{remoteEndPoint}");
                    HandyControl.Controls.Growl.WarningGlobal($"Socket 客户端{initializeMessage}连接失败[{deviceName}] 远程地址：{remoteEndPoint}");
                    await client.DisposeAsync().ConfigureAwait(false);
                    return null;
                }

                _logger.Info($"Socket 客户端{initializeMessage}连接成功 [{deviceName}] 远程地址：{remoteEndPoint}");
                HandyControl.Controls.Growl.SuccessGlobal($"Socket 客户端{initializeMessage}连接成功 [{deviceName}] 远程地址：{remoteEndPoint}");
                return client;
            }
            catch (Exception ex)
            {
                _logger.Error($"Socket 客户端{initializeMessage}连接异常 [{deviceName}] 远程地址：{remoteEndPoint}", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"Socket 客户端{initializeMessage}连接异常 [{deviceName}] 远程地址：{remoteEndPoint} Message:{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 停止 Socket 客户端
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task StopSocketClientAsync(ISocketClient? client, string deviceName)
        {
            if (client == null) return;
            var remoteEndPoint = client.RemoteEndPoint;
            _logger.Info($"正在停止 Socket 客户端 [{deviceName}] 远程地址：{remoteEndPoint}...");
            try
            {
                client.DataReceived -= WMSocketClient_OnDataReceived;
                client.DataReceived -= DHASocketClient_OnDataReceived;
                client.DataReceived -= DHBSocketClient_OnDataReceived;
                client.Connected -= SocketClient_OnConnected;
                client.Disconnected -= SocketClient_OnDisconnected;
                client.ErrorOccurred -= SocketClient_OnErrorOccurred;
                await client.DisconnectAsync();
                await client.DisposeAsync();
                SetModuleField(deviceName, (ISocketClient?)null);

                _logger.Info($"Socket 客户端已停止 [{deviceName}] 远程地址：{remoteEndPoint}");
                HandyControl.Controls.Growl.WarningGlobal($"Socket 客户端已停止 [{deviceName}] 远程地址：{remoteEndPoint}");
            }
            catch (Exception ex)
            {
                _logger.Error("Socket 客户端停止异常", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"Socket 客户端停止异常 [{deviceName}] 远程地址：{remoteEndPoint} 异常：[{ex.Message}]");
            }
        }

        /// <summary>
        /// 注册 Socket 客户端事件
        /// </summary>
        /// <param name="client"></param>
        /// <param name="deviceName"></param>
        private void RegisterSocketClientHandler(ISocketClient? client, string deviceName)
        {
            if (client == null || string.IsNullOrEmpty(deviceName)) return;

            client.Connected -= SocketClient_OnConnected;
            client.Connected += SocketClient_OnConnected;
            client.Disconnected -= SocketClient_OnDisconnected;
            client.Disconnected += SocketClient_OnDisconnected;
            client.ErrorOccurred -= SocketClient_OnErrorOccurred;
            client.ErrorOccurred += SocketClient_OnErrorOccurred;

            if (deviceName == WMSocketClientDeviceName)
            {
                client.DataReceived -= WMSocketClient_OnDataReceived;
                client.DataReceived += WMSocketClient_OnDataReceived;
            }
            else if (deviceName == DHASocketClientDeviceName)
            {
                client.DataReceived -= DHASocketClient_OnDataReceived;
                client.DataReceived += DHASocketClient_OnDataReceived;
            }
            else if (deviceName == DHBSocketClientDeviceName)
            {
                client.DataReceived -= DHBSocketClient_OnDataReceived;
                client.DataReceived += DHBSocketClient_OnDataReceived;
            }
            else if (deviceName == OPMMPLCSocketClientDeviceName)
            {
                client.DataReceived -= OPMMPLCSocketClient_OnDataReceived;
                client.DataReceived += OPMMPLCSocketClient_OnDataReceived;
            }
            else if (deviceName == OPMMCCDSocketClientDeviceName)
            {
                client.DataReceived -= OPMMCCDSocketClient_OnDataReceived;
                client.DataReceived += OPMMCCDSocketClient_OnDataReceived;
            }
            else if (deviceName == OPMMWeightSocketClientDeviceName)
            {
                client.DataReceived -= OPMMWeightSocketClient_OnDataReceived;
                client.DataReceived += OPMMWeightSocketClient_OnDataReceived;
            }
        }

        private void WMSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void DHASocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void DHBSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void OPMMPLCSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void OPMMCCDSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void OPMMWeightSocketClient_OnDataReceived(object? sender, ClientDataReceivedEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var message = Encoding.UTF8.GetString(e.Data);
            _logger.Info($"[{deviceName}] 收到数据: {message}");
            // TODO: 解析数据并处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到数据 [{message}]");
        }

        private void SocketClient_OnConnected(object? sender, EventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions.ModuleName ?? "Unknow";
            var initializeMessage = !IsInitialized ? "初始化" : string.Empty;
            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 客户端{initializeMessage}连接成功: {client.RemoteEndPoint}");
        }

        private void SocketClient_OnDisconnected(object? sender, EventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.WarningGlobal($"[{deviceName}] 客户端已断开连接: {client.RemoteEndPoint}");
        }

        private void SocketClient_OnErrorOccurred(object? sender, ClientErrorEventArgs e)
        {
            if (!(sender is ISocketClient client)) return;
            var deviceName = client.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.ErrorGlobal($"[{deviceName}] 错误：[{e.Message}]");
            if (e.Exception == null) _logger.Error($"[{deviceName}] 客户端错误: {e.Message}");
            else _logger.Error($"[{deviceName}] 客户端错误: {e.Message}", e.Exception);
        }
        #endregion

        #region MQTT 客户端
        /// <summary>
        /// 初始化 Mqtt 客户端
        /// </summary>
        /// <returns></returns>
        public async Task InitializeMqttClientAsync()
        {
            try
            {
                if (settings == null) return;
                if (settings.DHCommunicationMode != ECommunicationMode.客户端Mqtt) return;

                if (settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHAMqttClientEnabled)
                {
                    var ip = settings.DHAMqttClientIP;
                    var port = settings.DHAMqttClientPort;
                    var client = await StartMqttClientAsync(DHAMqttClientDeviceName, ip, port);
                    SetModuleField(DHAMqttClientDeviceName, client);
                }

                if (settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHBMqttClientEnabled)
                {
                    var ip = settings.DHBMqttClientIP;
                    var port = settings.DHBMqttClientPort;
                    var client = await StartMqttClientAsync(DHBMqttClientDeviceName, ip, port);
                    SetModuleField(DHBMqttClientDeviceName, client);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Mqtt 客户端初始化异常", ex);
            }
        }

        /// <summary>
        /// 启动 Mqtt 客户端
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private async Task<ICustomMqttClient?> StartMqttClientAsync(string deviceName, string? ip, int port)
        {
            if (string.IsNullOrEmpty(deviceName)) throw new ArgumentNullException(nameof(deviceName));
            if (string.IsNullOrEmpty(ip)) throw new ArgumentNullException(nameof(ip));
            if (port <= 0 || port > 65536) throw new ArgumentOutOfRangeException(nameof(port));

            var initializeMessage = !IsInitialized ? "初始化" : string.Empty;
            var moduleName = ModuleToConfigGroupMap[deviceName];
            var rempteEndPoint = $"{ip}:{port}";
            _logger.Info($"正在{initializeMessage}连接 MQTT 客户端 [{deviceName}] 远程地址：{rempteEndPoint}...");
            try
            {
                var client = _mqttClientFactory.CreateClient();

                // 注册事件
                RegisterMqttClientHandler(client, deviceName);

                var result = await client.ConnectAsync(moduleName, ip, port);
                if (!result.IsSuccess)
                {
                    _logger.Error($"Mqtt 客户端{initializeMessage}连接失败[{deviceName} ClientId:{client.ClientId}] 远程地址：{rempteEndPoint}");
                    HandyControl.Controls.Growl.WarningGlobal($"Mqtt 客户端{initializeMessage}连接失败[{deviceName}] 远程地址：{rempteEndPoint}");
                    await client.DisconnectAsync().ConfigureAwait(false);
                    await client.DisposeAsync().ConfigureAwait(false);
                    return null;
                }

                _logger.Info($"Mqtt 客户端{initializeMessage}连接成功 [{deviceName} ClientId:{client.ClientId}] 远程地址：{rempteEndPoint}");
                HandyControl.Controls.Growl.SuccessGlobal($"Mqtt 客户端{initializeMessage}连接成功 [{deviceName}] 远程地址：{rempteEndPoint}");
                return client;
            }
            catch (Exception ex)
            {
                _logger.Error($"Mqtt 客户端{initializeMessage}连接异常 [{deviceName}] 远程地址：{rempteEndPoint}", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"Mqtt 客户端{initializeMessage}连接异常 [{deviceName}] 远程地址：{rempteEndPoint} Message:{ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 停止 Mqtt 客户端
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task StopMqttClientAsync(ICustomMqttClient? client, string deviceName)
        {
            if (client == null) return;
            var remoteEndPoint = $"{client.RemoteIP}:{client.RemotePort}";
            _logger.Info($"正在断开 MQTT 客户端 [{deviceName}] 远程地址：{remoteEndPoint}...");
            try
            {
                client.MessageReceived -= DHAMqttClient_OnMessageReceived;
                client.MessageReceived -= DHBMqttClient_OnMessageReceived;
                client.ConnectionChanged -= MqttClient_OnConnectionChanged;
                client.ErrorOccurred -= MqttClient_OnErrorOccurred;
                await client.DisconnectAsync();
                await client.DisconnectAsync();
                SetModuleField(deviceName, (ICustomMqttClient?)null);

                _logger.Info($"MQTT 客户端已停止 [{deviceName}] 远程地址：{remoteEndPoint}");
                HandyControl.Controls.Growl.WarningGlobal($"MQTT 客户端已停止 [{deviceName}] 远程地址：{remoteEndPoint}");
            }
            catch (Exception ex)
            {
                _logger.Error($"MQTT 客户端停止异常 [{deviceName}] 远程地址：{remoteEndPoint}", ex);
                HandyControl.Controls.Growl.ErrorGlobal($"MQTT 客户端停止异常 [{deviceName}] 远程地址：{remoteEndPoint} 异常：[{ex.Message}]");
            }
        }

        /// <summary>
        /// 注册 Mqtt 客户端事件
        /// </summary>
        /// <param name="client"></param>
        /// <param name="deviceName"></param>
        private void RegisterMqttClientHandler(ICustomMqttClient? client, string deviceName)
        {
            if (client == null || string.IsNullOrEmpty(deviceName)) return;

            client.ConnectionChanged -= MqttClient_OnConnectionChanged;
            client.ConnectionChanged += MqttClient_OnConnectionChanged;
            client.ErrorOccurred -= MqttClient_OnErrorOccurred;
            client.ErrorOccurred += MqttClient_OnErrorOccurred;

            if (deviceName == DHAMqttClientDeviceName)
            {
                client.MessageReceived -= DHAMqttClient_OnMessageReceived;
                client.MessageReceived += DHAMqttClient_OnMessageReceived;
                return;
            }

            if (deviceName == DHBMqttClientDeviceName)
            {
                client.MessageReceived -= DHBMqttClient_OnMessageReceived;
                client.MessageReceived += DHBMqttClient_OnMessageReceived;
                return;
            }
        }

        private void DHAMqttClient_OnMessageReceived(object? sender, MqttApplicationMessage message)
        {
            if (!(sender is ICustomMqttClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var payload = Encoding.UTF8.GetString(message.Payload);
            _logger.Info($"[{deviceName}] 收到消息 [{message.Topic}]: {payload}");
            // TODO: 根据 Topic 分发处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到消息 [{message.Topic}]: {payload}");
        }

        private void DHBMqttClient_OnMessageReceived(object? sender, MqttApplicationMessage message)
        {
            if (!(sender is ICustomMqttClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            var payload = Encoding.UTF8.GetString(message.Payload);
            _logger.Info($"[{deviceName}] 收到消息 [{message.Topic}]: {payload}");
            // TODO: 根据 Topic 分发处理

            HandyControl.Controls.Growl.InfoGlobal($"[{deviceName}] 收到消息 [{message.Topic}]: {payload}");
        }

        private void MqttClient_OnConnectionChanged(object? sender, CustomMqttConnectionChangedEventArgs e)
        {
            if (!(sender is ICustomMqttClient client)) return;
            var deviceName = client.ActiveOptions?.ModuleName ?? "Unknow";
            if (e.IsConnected) HandyControl.Controls.Growl.SuccessGlobal($"[{deviceName}] 客户端连接成功");
            else HandyControl.Controls.Growl.WarningGlobal($"[{deviceName}] 客户端断开连接");
        }

        private void MqttClient_OnErrorOccurred(object? sender, CustomMqttErrorEventArgs e)
        {
            if (!(sender is ICustomMqttClient client)) return;
            var deviceName = client.ActiveOptions.ModuleName ?? "Unknow";
            HandyControl.Controls.Growl.ErrorGlobal($"[{deviceName}] 错误：{e.Operation}-[{e.Exception?.Message}]");
            _logger.Error($"[{deviceName}] 发生错误，Operation= {e.Operation}", e.Exception ?? new Exception("未知错误"));
        }
        #endregion

        #region 状态监控与自动重连
        private readonly ConcurrentQueue<string> _pendingRestartModules = new();

        /// <summary>
        /// 外部通知指定模块的配置已变更，需要重启
        /// </summary>
        /// <param name="moduleNames">需要重启的模块名称</param>
        public void NotifyModulesChanged(params string[] moduleNames)
        {
            if (moduleNames == null || moduleNames.Length == 0) return;

            foreach (var deviceName in moduleNames)
            {
                _pendingRestartModules.Enqueue(deviceName);
            }
        }

        /// <summary>
        /// 触发 设备状态变化
        /// </summary>
        /// <param name="args"></param>
        private void RaiseDeviceStatusChanged(DeviceStatusEventArgs args) => DeviceStatusChanged?.Invoke(this, args);

        /// <summary>
        /// 重连检测
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CheckAndReconnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var newSettings = LoadSettings();

                // HashSet去重
                var moduleNames = new HashSet<string>(newSettings.GetChangedModules(settings));
                if (moduleNames.Count > 0)
                {
                    var mns = new List<string>(moduleNames);
                    while (_pendingRestartModules.Count > 0)
                    {
                        if (!_pendingRestartModules.TryDequeue(out var deviceName)) continue;
                        if (!moduleNames.Contains(deviceName)) mns.Add(deviceName);
                    }
                    foreach (var deviceName in moduleNames) await StopByModuleNameAsync(deviceName);
                }
                settings = newSettings;
                if (settings == null) throw new Exception("通讯参数加载配置为空！");

                // Socket 服务端状态检查
                var t1 = CheckSocketServerAsync(wmSocketServer, WMSocketServerDeviceName, settings.WMSocketServerPort, settings.WMCommunicationMode == ECommunicationMode.服务端Socket && settings.WMSocketServerEnabled);
                var t2 = CheckSocketServerAsync(dhSocketServer, DHSocketServerDeviceName, settings.DHSocketServerPort, settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHSocketServerEnabled);
                var t3 = CheckSocketServerAsync(ccdSocketServer, CCDSocketServerDeviceName, settings.CCDSocketServerPort, settings.CCDSocketServerEnabled);

                // Socket 客户端状态检查与重连
                var t4 = CheckSocketClientAsync(wmSocketClient, WMSocketClientDeviceName, settings.WMSocketClientIP, settings.WMSocketClientPort, settings.WMCommunicationMode == ECommunicationMode.客户端Socket && settings.WMSocketClientEnabled);
                var t5 = CheckSocketClientAsync(dhASocketClient, DHASocketClientDeviceName, settings.DHASocketClientIP, settings.DHASocketClientPort, settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHASocketClientEnabled);
                var t6 = CheckSocketClientAsync(dhBSocketClient, DHBSocketClientDeviceName, settings.DHBSocketClientIP, settings.DHBSocketClientPort, settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHBSocketClientEnabled);

                // Mqtt 客户端状态检查与重连
                var t7 = CheckMqttClientAsync(dhAMqttClient, DHAMqttClientDeviceName, settings.DHAMqttClientIP, settings.DHAMqttClientPort, settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHAMqttClientEnabled);
                var t8 = CheckMqttClientAsync(dhBMqttClient, DHBMqttClientDeviceName, settings.DHBMqttClientIP, settings.DHBMqttClientPort, settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHBMqttClientEnabled);

                // OPMM 客户端状态检查与重连
                var t9 = CheckSocketClientAsync(opmmPLCSocketClient, OPMMPLCSocketClientDeviceName, settings.OPMMSocketClientIP, settings.OPMMPLCSocketClientPort, settings.OPMMPLCSocketClientEnabled);
                var t10 = CheckSocketClientAsync(opmmCCDSocketClient, OPMMCCDSocketClientDeviceName, settings.OPMMSocketClientIP, settings.OPMMCCDSocketClientPort, settings.OPMMCCDSocketClientEnabled);
                var t11 = CheckSocketClientAsync(opmmWeightSocketClient, OPMMWeightSocketClientDeviceName, settings.OPMMSocketClientIP, settings.OPMMWeightSocketClientPort, settings.OPMMWeightSocketClientEnabled);

                // PLC 设备状态检查
                var t12 = CheckPLCAsync(CMPLCDeviceName);

                await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12);
            }
            catch (Exception ex)
            {
                _logger.Error("通讯状态检查异常", ex);
            }
        }

        /// <summary>
        /// 更新所有设备状态
        /// </summary>
        public async Task RefreshDeviceStatusAsync(CancellationToken cancellationToken = default)
        {
            var args = new DeviceStatusEventArgs();
            args.DeviceStatus = GetDeviceStatus(CMPLCDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(WMSocketServerDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(DHSocketServerDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(CCDSocketServerDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(WMSocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(DHASocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(DHBSocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(DHAMqttClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(DHBMqttClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(OPMMPLCSocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(OPMMCCDSocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
            args.DeviceStatus = GetDeviceStatus(OPMMWeightSocketClientDeviceName);
            RaiseDeviceStatusChanged(args);
        }

        private async Task CheckSocketServerAsync(ISocketServer? server, string deviceName, int port, bool shouldRun)
        {
            if (!shouldRun)
            {
                if (server != null) await StopSocketServerAsync(server, deviceName);
                return;
            }
            if (server != null && server.IsRunning) return;

            _logger.Info($"[{deviceName}] 服务端未运行 端口：{port}，正在重启...");
            var newServer = await StartSocketServerAsync(deviceName, port);
            SetModuleField(deviceName, newServer);
        }

        private async Task CheckSocketClientAsync(ISocketClient? client, string deviceName, string? ip, int port, bool shouldConnect)
        {
            if (!shouldConnect)
            {
                if (client != null) await StopSocketClientAsync(client, deviceName);
                return;
            }

            if (client != null && client.IsConnected) return;

            _logger.Info($"[{deviceName}] 客户端未连接，远程地址：{ip}:{port}，正在重新连接...");
            var newClient = await StartSocketClientAsync(deviceName, ip, port);
            SetModuleField(deviceName, newClient);
        }

        private async Task CheckMqttClientAsync(ICustomMqttClient? client, string deviceName, string? ip, int port, bool shouldConnect)
        {
            if (!shouldConnect)
            {
                if (client != null) await StopMqttClientAsync(client, deviceName);
                return;
            }

            if (client != null && client.IsConnected) return;

            _logger.Warn($"[{deviceName}] Mqtt 客户端未连接，远程地址：{ip}:{port}，正在重新连接...");
            var newClient = await StartMqttClientAsync(deviceName, ip, port);
            SetModuleField(deviceName, newClient);
        }

        private async Task CheckPLCAsync(string deviceName)
        {

        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 加载通讯配置参数
        /// </summary>
        /// <returns></returns>
        private CommunicationSettingModel LoadSettings()
        {
            var newSettings = _projectSettingRepository.LoadCommunicationSettingProjectParameters();
            if (newSettings == null) throw new Exception("通讯参数加载配置为空！");
            var model = _plcConfigRepository.GetList(o => o.Name == CCTBCore.PLCName).FirstOrDefault();
            if (model != null)
            {
                newSettings.CMPLCIP = model.IP;
                newSettings.CMPLCPort = model.Port;
                newSettings.CMBrandSpecificationProtocal = model.BrandSpecificationProtocal;
                newSettings.CMCycleReadTime = model.CycleReadTime;
                newSettings.CMCycleWriteTime = model.CycleWriteTime;
                newSettings.CMIsEnable = model.IsEnable;
            }

            return newSettings;
        }

        /// <summary>
        /// 根据模块名设置 Socket 服务端实例
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="server"></param>
        private void SetModuleField(string deviceName, ISocketServer? server)
        {
            switch (deviceName)
            {
                case WMSocketServerDeviceName: wmSocketServer = server; break;
                case DHSocketServerDeviceName: dhSocketServer = server; break;
                case CCDSocketServerDeviceName: ccdSocketServer = server; break;
                default: _logger.Error($"[{deviceName}] 未知模块，无法设置 Socket 服务端实例"); break;
            }
        }

        /// <summary>
        /// 根据模块名设置 Socket 客户端实例
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="client"></param>
        private void SetModuleField(string deviceName, ISocketClient? client)
        {
            switch (deviceName)
            {
                case WMSocketClientDeviceName: wmSocketClient = client; break;
                case DHASocketClientDeviceName: dhASocketClient = client; break;
                case DHBSocketClientDeviceName: dhBSocketClient = client; break;
                case OPMMPLCSocketClientDeviceName: opmmPLCSocketClient = client; break;
                case OPMMCCDSocketClientDeviceName: opmmCCDSocketClient = client; break;
                case OPMMWeightSocketClientDeviceName: opmmWeightSocketClient = client; break;
                default: _logger.Error($"[{deviceName}] 未知模块，无法设置 Socket 客户端实例"); break;
            }
        }

        /// <summary>
        /// 根据模块名设置 Mqtt 客户端实例
        /// </summary>
        /// <param name="deviceName"></param>
        /// <param name="client"></param>
        private void SetModuleField(string deviceName, ICustomMqttClient? client)
        {
            switch (deviceName)
            {
                case DHAMqttClientDeviceName: dhAMqttClient = client; break;
                case DHBMqttClientDeviceName: dhBMqttClient = client; break;
                default: _logger.Error($"[{deviceName}] 未知模块，无法设置 MQTT 客户端实例"); break;
            }
        }

        /// <summary>
        /// 获取设备状态信息
        /// </summary>
        /// <param name="deviceName"></param>
        /// <returns></returns>
        private DeviceStatusModel? GetDeviceStatus(string deviceName)
        {
            var deviceStatus = new DeviceStatusModel();
            deviceStatus.DeviceName = deviceName;
            if (settings == null)
            {
                deviceStatus.DeviceStatus = EDeviceStatus.未启用;
                deviceStatus.AcceptDeviceCount = 0;
                return deviceStatus;
            }
            if (deviceName == WMSocketServerDeviceName)
            {
                if (settings.WMCommunicationMode == ECommunicationMode.服务端Socket && settings.WMSocketServerEnabled)
                {
                    deviceStatus.DeviceStatus = (wmSocketServer?.IsRunning ?? false) ? EDeviceStatus.已开启 : EDeviceStatus.未开启;
                    deviceStatus.AcceptDeviceCount = wmSocketServer?.ConnectedClientCount ?? 0;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == DHSocketServerDeviceName)
            {
                if (settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHSocketServerEnabled)
                {
                    deviceStatus.DeviceStatus = (dhSocketServer?.IsRunning ?? false) ? EDeviceStatus.已开启 : EDeviceStatus.未开启;
                    deviceStatus.AcceptDeviceCount = dhSocketServer?.ConnectedClientCount ?? 0;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == CCDSocketServerDeviceName)
            {
                if (settings.CCDSocketServerEnabled)
                {
                    deviceStatus.DeviceStatus = (ccdSocketServer?.IsRunning ?? false) ? EDeviceStatus.已开启 : EDeviceStatus.未开启;
                    deviceStatus.AcceptDeviceCount = ccdSocketServer?.ConnectedClientCount ?? 0;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == WMSocketClientDeviceName)
            {
                if (settings.WMCommunicationMode == ECommunicationMode.客户端Socket && settings.WMSocketClientEnabled)
                {
                    deviceStatus.DeviceStatus = (wmSocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == DHASocketClientDeviceName)
            {
                if (settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHASocketClientEnabled)
                {
                    deviceStatus.DeviceStatus = (dhASocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == DHBSocketClientDeviceName)
            {
                if (settings.DHCommunicationMode == ECommunicationMode.服务端Socket && settings.DHBSocketClientEnabled)
                {
                    deviceStatus.DeviceStatus = (dhBSocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == DHAMqttClientDeviceName)
            {
                if (settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHAMqttClientEnabled)
                {
                    deviceStatus.DeviceStatus = (dhAMqttClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == DHBMqttClientDeviceName)
            {
                if (settings.DHCommunicationMode == ECommunicationMode.客户端Mqtt && settings.DHBMqttClientEnabled)
                {
                    deviceStatus.DeviceStatus = (dhBMqttClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
                }
                else deviceStatus.DeviceStatus = EDeviceStatus.未启用;
            }
            else if (deviceName == OPMMPLCSocketClientDeviceName)
            {
                deviceStatus.DeviceStatus = (opmmPLCSocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
            }
            else if (deviceName == OPMMCCDSocketClientDeviceName)
            {
                deviceStatus.DeviceStatus = (opmmCCDSocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
            }
            else if (deviceName == OPMMWeightSocketClientDeviceName)
            {
                deviceStatus.DeviceStatus = (opmmWeightSocketClient?.IsConnected ?? false) ? EDeviceStatus.已连接 : EDeviceStatus.未连接;
            }
            else if (deviceName == CMPLCDeviceName)
            {
                if (!settings.CMIsEnable) deviceStatus.DeviceStatus = EDeviceStatus.未启用;
                else
                {
                    var isGet = _plcService.ConnectionStatus.TryGetValue(CCTBCore.PLCName, out ConnectInfo? value);
                    deviceStatus.DeviceStatus = (!isGet || value == null || !value.Status) ? EDeviceStatus.未连接 : EDeviceStatus.已连接;
                }
            }

            return deviceStatus;
        }
        #endregion
    }
}
