using SUNWODA_SEVB.Core.Models.Socket;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// 消息处理器接口
    /// </summary>
    public interface IMessageHandler
    {
        /// <summary>
        /// 处理接收到的消息
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="clientEndPoint">客户端端点</param>
        /// <returns>响应结果</returns>
        Task<SocketResponse> HandleMessageAsync(string message, string clientEndPoint);
    }
}
