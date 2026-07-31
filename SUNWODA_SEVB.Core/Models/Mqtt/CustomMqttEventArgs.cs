using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Mqtt
{
    /// <summary>
    /// MQTT 连接事件参数
    /// </summary>
    public class CustomMqttConnectionChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
    }

    /// <summary>
    /// MQTT 错误事件参数
    /// </summary>
    public class CustomMqttErrorEventArgs : EventArgs
    {
        public string Operation { get; init; } = string.Empty;
        public Exception? Exception { get; init; }
    }

    /// <summary>
    /// MQTT统计信息
    /// </summary>
    public class CustomMqttStatistics
    {
        public long MessagesSent { get; set; }
        public long MessagesReceived { get; set; }
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        public int ReconnectCount { get; set; }
        public DateTime? LastConnectedTime { get; set; }
        public DateTime? LastDisconnectedTime { get; set; }
        public bool IsConnected { get; set; }
        public CustomMqttClientOptions? ActiveOptions { get; set; }
    }
}
