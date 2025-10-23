using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Fleck;

namespace SUNWODA_SEVB.Tool.Helper
{
    public class WebSocketServerHelper
    {
        static void Run(string[] args)
        {
            int port = 8181;
            var server = new WebSocketServer($"ws://0.0.0.0:{port}");
            var allSockets = new List<IWebSocketConnection>();

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine($"客户端已连接: {socket.ConnectionInfo.ClientIpAddress}");
                    allSockets.Add(socket);
                };

                socket.OnClose = () =>
                {
                    Console.WriteLine($"客户端断开: {socket.ConnectionInfo.ClientIpAddress}");
                    allSockets.Remove(socket);
                };

                socket.OnMessage = message =>
                {
                    Console.WriteLine($"收到消息: {message}");
                    // 回发消息给所有客户端
                    foreach (var s in allSockets)
                    {
                        s.Send($"服务端收到: {message}");
                    }
                };
            });

            Console.WriteLine($" WebSocket 服务已启动: ws://{GetLocalIPAddress()}:{port}");
            Console.ReadKey();
        }

        static string GetLocalIPAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }
    }
}
