using HslCommunication.MQTT;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Services.Project.CCTB;
using SUNWODA_SEVB.Core.Models.Socket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CCTB
{
    /// <summary>
    /// 通信服务接口，提供Socket和MQTT客户端的访问
    /// </summary>
    public interface ICommunicationService
    {
        public const string WMSocketServerDeviceName = "WMSocketServer";
        public const string DHSocketServerDeviceName = "DHSocketServer";
        public const string CCDSocketServerDeviceName = "CCDSocketServer";
        public const string WMSocketClientDeviceName = "WMSocketClient";
        public const string DHASocketClientDeviceName = "DHASocketClient";
        public const string DHBSocketClientDeviceName = "DHBSocketClient";
        public const string OPMMPLCSocketClientDeviceName = "OPMMPLCSocketClient";
        public const string OPMMCCDSocketClientDeviceName = "OPMMCCDSocketClient";
        public const string OPMMWeightSocketClientDeviceName = "OPMMWeightSocketClient";
        public const string DHAMqttClientDeviceName = "DHAMqttClient";
        public const string DHBMqttClientDeviceName = "DHBMqttClient";
        public const string OPMMSocketClientDeviceNames = "OPMMSocketClient";
        public const string CMPLCDeviceName = "CMPLC";

        /// <summary>
        /// 模块名称与数据库配置组名称的映射
        /// Key: 模块设备名称（DeviceName）
        /// Value: 数据库配置项的分组名称（BelongToVM）
        /// </summary>
        public static readonly Dictionary<string, string> ModuleToConfigGroupMap = new()
        {
            {WMSocketServerDeviceName, "WMSocketServer" },
            {DHSocketServerDeviceName, "DHSocketServer" },
            {CCDSocketServerDeviceName, "CCDSocketServer" },
            {WMSocketClientDeviceName, "WMSocketClient" },
            {DHASocketClientDeviceName, "DHASocketClient" },
            {DHBSocketClientDeviceName, "DHBSocketClient" },
            {DHAMqttClientDeviceName, "DHAMqttClient" },
            {DHBMqttClientDeviceName, "DHBMqttClient" },
            {OPMMPLCSocketClientDeviceName, "OPMMSocketClient" },
            {OPMMCCDSocketClientDeviceName, "OPMMSocketClient" },
            {OPMMWeightSocketClientDeviceName, "OPMMSocketClient" },
            {OPMMSocketClientDeviceNames, "OPMMSocketClient" },
        };

        // Socket 服务端
        ISocketServer? WMSocketServer { get; }
        ISocketServer? DHSocketServer { get; }
        ISocketServer? CCDSocketServer { get; }

        // Socket 客户端
        ISocketClient? WMSocketClient { get; }
        ISocketClient? DHASocketClient { get; }
        ISocketClient? DHBSocketClient { get; }
        ISocketClient? OPMMPLCSocketClient { get; }
        ISocketClient? OPMMCCDSocketClient { get; }
        ISocketClient? OPMMWeightSocketClient { get; }

        // Mqtt 客户端
        ICustomMqttClient? DHAMqttClient { get; }
        ICustomMqttClient? DHBMqttClient { get; }

        /// <summary>
        /// 设备状态变化时触发
        /// </summary>
        event EventHandler<DeviceStatusEventArgs> DeviceStatusChanged;
        event EventHandler<DataReceivedEventArgs> WMSocketSererDataReceived;
        event EventHandler<DataReceivedEventArgs> DHSocketSererDataReceived;
        event EventHandler<DataReceivedEventArgs> CCDSocketSererDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> WMSocketClientDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> DHASocketClientDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> DHBSocketClientDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> OPMMPLCSocketClientDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> OPMMCCDSocketClientDataReceived;
        event EventHandler<ClientDataReceivedEventArgs> OPMMWeightSocketClientDataReceived;
        event EventHandler<MqttApplicationMessage> DHAMqttMessageReceived;
        event EventHandler<MqttApplicationMessage> DHBMqttMessageReceived;

        /// <summary>
        /// 配置信息
        /// </summary>
        CommunicationSettingModel? Settings { get; }

        /// <summary>
        /// 外部通知指定模块的配置已变更，需要重启
        /// </summary>
        /// <param name="moduleNames"></param>
        void NotifyModulesChanged(params string[] moduleNames);

        /// <summary>
        /// 检查所有通讯连接并执行重连
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CheckAndReconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查所有设备通讯状态
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RefreshDeviceStatusAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 启动所有通讯服务
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StartAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止所有通讯服务
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StopAllAsync(CancellationToken cancellationToken = default);
    }
}
