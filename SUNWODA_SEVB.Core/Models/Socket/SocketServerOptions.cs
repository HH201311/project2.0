
namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket服务器配置选项
    /// </summary>
    public class SocketServerOptions
    {
        /// <summary>
        /// 配置所属的项目/模块名称（用于数据库查询）
        /// </summary>
        public string ModuleName { get; set; } = "SocketServer";
        /// <summary>
        /// 最大客户端连接数
        /// </summary>
        public int MaxClients { get; set; } = 5;

        /// <summary>
        /// 接收缓冲区大小（字节）
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;

        /// <summary>
        /// 挂起连接队列的最大长度
        /// </summary>
        public int Backlog { get; set; } = 100;

        /// <summary>
        /// 接收超时时间，null表示无限等待
        /// </summary>
        public TimeSpan? ReceiveTimeout { get; set; } = null;

        /// <summary>
        /// 默认监听端口
        /// </summary>
        public int DefaultPort { get; set; } = 8080;
    }
}
