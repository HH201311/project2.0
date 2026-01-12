using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Core.Interfaces.Socket
{
    /// <summary>
    /// 客户端会话接口
    /// </summary>
    public interface IClientSession : IAsyncDisposable
    {
        /// <summary>
        /// 会话唯一标识
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 客户端端点地址
        /// </summary>
        string EndPoint { get; }

        /// <summary>
        /// 连接时间
        /// </summary>
        DateTime ConnectedTime { get; }

        /// <summary>
        /// 最后活动时间
        /// </summary>
        DateTime LastActivityTime { get; }

        /// <summary>
        /// 是否已连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 接收的字节数
        /// </summary>
        long ReceivedBytes { get; }

        /// <summary>
        /// 发送的字节数
        /// </summary>
        long SentBytes { get; }

        /// <summary>
        /// 自定义标签数据
        /// </summary>
        object? Tag { get; set; }

        /// <summary>
        /// 发送字节数据
        /// </summary>
        Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default);

        /// <summary>
        /// 发送字符串数据
        /// </summary>
        Task<bool> SendAsync(string message, Encoding? encoding = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// 关闭连接
        /// </summary>
        void Close();
    }
}
