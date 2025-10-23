using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.SocketHelper
{
    /// <summary>
    /// 
    /// </summary>
    public class ClientEventArgs<T> : EventArgs
    {
        /// <summary>
        /// 客户端
        /// </summary>
        public Socket ClientSocket { get; }

        /// <summary>
        /// 消息
        /// </summary>
        public T? Message { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="clientSocket"></param>
        /// <param name="message"></param>
        public ClientEventArgs(Socket clientSocket, T? message = default(T))
        {
            ClientSocket = clientSocket;
            Message = message;
        }
    }
}
