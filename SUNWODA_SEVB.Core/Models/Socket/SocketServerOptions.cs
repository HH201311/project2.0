
using SUNWODA_SEVB.Core.Common;
using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket服务器配置选项
    /// </summary>
    public class SocketServerOptions : ModelBase
    {
        private string moduleName = "SocketServer";
        private int maxClients = 5;
        private int receiveBufferSize = 8192;
        private int backlog = 100;
        private int receiveTimeoutMilliseconds = 0;
        private int defaultPort = 8080;

        /// <summary>
        /// 配置所属的项目/模块名称（用于数据库查询）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public string ModuleName { get => moduleName; set => SetProperty(ref moduleName, value); }

        /// <summary>
        /// 最大客户端连接数
        /// </summary>
        public int MaxClients { get => maxClients; set => SetProperty(ref maxClients, value); }

        /// <summary>
        /// 接收缓冲区大小（字节）
        /// </summary>
        public int ReceiveBufferSize { get => receiveBufferSize; set => SetProperty(ref receiveBufferSize, value); }

        /// <summary>
        /// 挂起连接队列的最大长度
        /// </summary>
        public int Backlog { get => backlog; set => SetProperty(ref backlog, value); }

        /// <summary>
        /// 接收超时时间，null表示无限等待
        /// </summary>
        public int ReceiveTimeoutMilliseconds { get => receiveTimeoutMilliseconds; set => SetProperty(ref receiveTimeoutMilliseconds, value); }

        /// <summary>
        /// 默认监听端口
        /// </summary>
        public int DefaultPort { get => defaultPort; set => SetProperty(ref defaultPort, value); }

        /// <summary>
        /// 接收超时时间，null表示无限等待
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public TimeSpan? ReceiveTimeout => ReceiveTimeoutMilliseconds > 0 ? TimeSpan.FromMilliseconds(ReceiveTimeoutMilliseconds) : null;

        /// <summary>
        /// 协议配置
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        [Newtonsoft.Json.JsonIgnore]
        public ProtocolOptions Protocol { get; set; } = new();
    }
}
