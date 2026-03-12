using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// 默认消息处理器
    /// </summary>
    public class DefaultMessageHandler : IMessageHandler
    {
        private readonly ILoggerService<DefaultMessageHandler> _logger;

        public DefaultMessageHandler(ILoggerService<DefaultMessageHandler> logger)
        {
            _logger = logger;
        }

        public async Task<SocketResponse> HandleMessageAsync(string message, string clientEndPoint)
        {
            await Task.Delay(10);

            _logger.Info($"收到来自 {clientEndPoint} 的消息: {message}", true);

            if (string.IsNullOrEmpty(message))
            {
                return SocketResponse.Error("400", "消息内容不能为空");
            }

            if (message.StartsWith("ECHO:"))
            {
                var echoMessage = message.Substring(5);
                return SocketResponse.Success($"回显: {echoMessage}");
            }

            return SocketResponse.Success($"成功接收，信息长度为: {message.Length}");
        }
    }
}