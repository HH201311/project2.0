
using SUNWODA_SEVB.Core.Common;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket客户端配置选项
    /// </summary>
    public class SocketClientOptions : ModelBase
    {
        private string moduleName = "SocketClient";
        private int connectTimeoutMilliseconds = 5000;
        public int receiveBufferSize = 8192;
        public int receiveTimeout = 5000;
        public int sendTimeout = 5000;
        public string defaultIP = "localhost";
        public int defaultPort = 8080;

        /// <summary>
        /// 配置所属的项目/模块名称（用于数据库查询）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public string ModuleName { get => moduleName; set => SetProperty(ref moduleName, value); }

        /// <summary>
        /// 连接超时时间（单位：毫秒）
        /// </summary>
        public int ConnectTimeoutMilliseconds { get => connectTimeoutMilliseconds; set => SetProperty(ref connectTimeoutMilliseconds, value); }

        /// <summary>
        /// 接收缓冲区大小（字节）
        /// </summary>
        public int ReceiveBufferSize { get => receiveBufferSize; set => SetProperty(ref receiveBufferSize, value); }

        /// <summary>
        /// 接收超时时间（单位：毫秒）
        /// </summary>
        public int ReceiveTimeout { get => receiveTimeout; set => SetProperty(ref receiveTimeout, value); }

        /// <summary>
        /// 发送超时时间（单位：毫秒）
        /// </summary>
        public int SendTimeout { get => sendTimeout; set => SetProperty(ref sendTimeout, value); }

        /// <summary>
        /// 默认远程IP
        /// </summary>
        public string DefaultIP { get => defaultIP; set => SetProperty(ref defaultIP, value); }

        /// <summary>
        /// 默认远程端口
        /// </summary>
        public int DefaultPort { get => defaultPort; set => SetProperty(ref defaultPort, value); }

        /// <summary>
        /// 连接超时时间，null表示无限等待
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public TimeSpan ConnectTimeout => ConnectTimeoutMilliseconds <= 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(ConnectTimeoutMilliseconds);

        /// <summary>
        /// 协议配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public ProtocolOptions Protocol { get; set; } = new();
    }
}
