using HslCommunication.MQTT;
using SUNWODA_SEVB.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Models.Mqtt
{
    /// <summary>
    /// 自定义MQTT客户端发布主题类
    /// </summary>
    public class CustomMqttPublishTopic : ModelBase
    {
        private string? topicName;
        private bool isPublish;
        private string? topic;
        private MqttQualityOfServiceLevel qualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce;
        private bool retain;

        /// <summary>
        /// 主题名称
        /// </summary>
        public string? TopicName { get => topicName; set => SetProperty(ref topicName, value); }

        /// <summary>
        /// 是否发布
        /// </summary>
        public bool IsPublish { get => isPublish; set => SetProperty(ref isPublish, value); }

        /// <summary>
        /// 主题
        /// </summary>
        public string? Topic { get => topic; set => SetProperty(ref topic, value); }

        /// <summary>
        /// 服务质量等级
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get => qualityOfServiceLevel; set => SetProperty(ref qualityOfServiceLevel, value); }

        /// <summary>
        /// 是否保留
        /// </summary>
        public bool Retain { get => retain; set => SetProperty(ref retain, value); }
    }

    /// <summary>
    /// 自定义MQTT客户端订阅主题类
    /// </summary>
    public class CustomMqttSubscribeTopic : ModelBase
    {
        private string? topicName;
        private bool isSubscribe;
        private string? topic;

        /// <summary>
        /// 主题名称
        /// </summary>
        public string? TopicName { get => topicName; set => SetProperty(ref topicName, value); }

        /// <summary>
        /// 是否订阅
        /// </summary>
        public bool IsSubscribe { get => isSubscribe; set => SetProperty(ref isSubscribe, value); }

        /// <summary>
        /// 主题
        /// </summary>
        public string? Topic { get => topic; set => SetProperty(ref topic, value); }
    }

    /// <summary>
    /// 自定义MQTT客户端选项类
    /// </summary>
    public class CustomMqttClientOptions : ModelBase
    {
        private string? ipAddress = "localhost";
        private int port = 1883;
        private string? clientId = "ime";
        private int connectTimeout = 5000;
        private string? userName = "ime";
        private string? password = "123456";
        private int keepAlivePeriod = 10;
        private int keepAliveSendInterval = 10;
        private List<CustomMqttPublishTopic> publishTopics = new List<CustomMqttPublishTopic>();
        private List<CustomMqttSubscribeTopic> subscribeTopics = new List<CustomMqttSubscribeTopic>();

        /// <summary>
        /// 配置所属的项目/模块名称（用于数据库查询）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public string ModuleName { get; set; } = "MqttClient";

        /// <summary>
        /// MQTT服务器IP地址
        /// </summary>
        public string? IpAddress { get => ipAddress; set => SetProperty(ref ipAddress, value); }

        /// <summary>
        /// MQTT服务器端口
        /// </summary>
        public int Port { get => port; set => SetProperty(ref port, value); }

        /// <summary>
        /// 客户端ID
        /// </summary>
        public string? ClientId { get => clientId; set => SetProperty(ref clientId, value); }

        /// <summary>
        /// 连接超时时间（毫秒）
        /// </summary>
        public int ConnectTimeout { get => connectTimeout; set => SetProperty(ref connectTimeout, value); }

        /// <summary>
        /// 用户名
        /// </summary>
        public string? UserName { get => userName; set => SetProperty(ref userName, value); }

        /// <summary>
        /// 密码
        /// </summary>
        public string? Password { get => password; set => SetProperty(ref password, value); }

        /// <summary>
        /// 保持连接周期（秒）
        /// </summary>
        public int KeepAlivePeriod { get => keepAlivePeriod; set => SetProperty(ref keepAlivePeriod, value); }

        /// <summary>
        /// 发送保持连接消息的间隔（秒）
        /// </summary>
        public int KeepAliveSendInterval { get => keepAliveSendInterval; set => SetProperty(ref keepAliveSendInterval, value); }

        /// <summary>
        /// 发布主题列表
        /// </summary>
        public List<CustomMqttPublishTopic> PublishTopics { get => publishTopics; set => SetProperty(ref publishTopics, value); }

        /// <summary>
        /// 订阅主题列表
        /// </summary>
        public List<CustomMqttSubscribeTopic> SubscribeTopics { get => subscribeTopics; set => SetProperty(ref subscribeTopics, value); }

        /// <summary>
        /// 将当前配置转换为HslCommunication.MQTT.MqttClientOptions对象
        /// </summary>
        /// <returns></returns>
        public MqttConnectionOptions GetMqttConnectionOptions()
        {
            var options = new MqttConnectionOptions()
            {
                IpAddress = ipAddress,
                Port = port,
                ClientId = clientId,
                ConnectTimeout = connectTimeout,
                Credentials = new MqttCredential(userName, password),
                KeepAlivePeriod = TimeSpan.FromSeconds(keepAlivePeriod),
                KeepAliveSendInterval = TimeSpan.FromSeconds(keepAliveSendInterval),
            };

            return options;
        }
    }
}
