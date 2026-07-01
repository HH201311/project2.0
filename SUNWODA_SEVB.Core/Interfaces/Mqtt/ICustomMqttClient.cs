using HslCommunication;
using HslCommunication.MQTT;
using SUNWODA_SEVB.Core.Models.Mqtt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Mqtt
{
    public interface ICustomMqttClient : IAsyncDisposable
    {
        CustomMqttClientOptions ActiveOptions { get; }
        bool IsConnected { get; }
        string ClientId { get; }
        string RemoteIP { get; }
        int RemotePort { get; }
        CustomMqttStatistics Statistics { get; }

        event EventHandler<CustomMqttConnectionChangedEventArgs>? ConnectionChanged;
        event EventHandler<MqttApplicationMessage>? MessageReceived;
        event EventHandler<CustomMqttErrorEventArgs>? ErrorOccurred;

        Task<OperateResult> ConnectAsync(string moduleName);
        Task<OperateResult> ConnectAsync(string moduleName, string ip, int port);
        Task DisconnectAsync();
        Task<OperateResult> SubscribeAsync(string topic);
        Task<OperateResult> SubscribeAsync(string[] topics);
        Task<OperateResult> UnsubscribeAsync(string topic);
        Task<OperateResult> PublishAsync(string topic, string payload, MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce, bool retain = false);
        Task<OperateResult> PublishAsync(MqttApplicationMessage message);
    }
}
