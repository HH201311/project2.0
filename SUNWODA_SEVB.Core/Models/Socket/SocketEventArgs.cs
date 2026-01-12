
using System.Text;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        public string ClientEndPoint { get; init; } = string.Empty;
        public string? ClientId { get; set; }
        public byte[] Data { get; init; } = Array.Empty<byte>();
        public DateTime ReceivedTime { get; init; } = DateTime.Now;

        public string GetDataAsString(Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            return encoding.GetString(Data);
        }
    }

    /// <summary>
    /// 客户端连接事件参数
    /// </summary>
    public class ClientConnectionEventArgs : EventArgs
    {
        public string ClientId { get; init; } = string.Empty;
        public string EndPoint { get; init; } = string.Empty;
        public DateTime ConnectionTime { get; init; } = DateTime.Now;
        public bool IsConnected { get; init; }
    }

    /// <summary>
    /// 服务器错误事件参数
    /// </summary>
    public class ServerErrorEventArgs : EventArgs
    {
        public string Message { get; init; } = string.Empty;
        public Exception? Exception { get; init; }
        public string? ClientEndPoint { get; init; }
    }
}
