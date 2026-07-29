/*using SUNWODA_SEVB.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Helper.SocketHelper
{
    public class Socket_Server2
    {
        private Socket ServerSocket = null;//服务端  
        public Dictionary<string, PublciSession> dic_ClientSocket = new Dictionary<string, PublciSession>();//tcp客户端字典
        private Dictionary<string, Thread> dic_ClientThread = new Dictionary<string, Thread>();//线程字典,每新增一个连接就添加一条线程
        private bool Flag_Listen = true;//监听客户端连接的标志
        public int CycleTime = 100;
        public Socket_Server2()
        {

        }
        /// <summary>
        /// 启动服务
        /// </summary>
        /// <param name="port">端口号</param>
        public bool OpenServer(int port)
        {
            try
            {
                Flag_Listen = true;
                // 创建负责监听的套接字，注意其中的参数；
                ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // 创建包含ip和端口号的网络节点对象；
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
                try
                {
                    // 将负责监听的套接字绑定到唯一的ip和端口上；
                    ServerSocket.Bind(endPoint);
                }
                catch
                {
                    return false;
                }
                // 设置监听队列的长度；
                ServerSocket.Listen(100);
                // 创建负责监听的线程；
                Thread Thread_ServerListen = new Thread(ListenConnecting);
                Thread_ServerListen.IsBackground = true;
                Flag_Listen = true;
                Thread_ServerListen.Start();

                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 关闭服务
        /// </summary>
        public void CloseServer()
        {
            lock (dic_ClientSocket)
            {
                foreach (var item in dic_ClientSocket)
                {
                    item.Value.Close();//关闭每一个连接
                }
                dic_ClientSocket.Clear();//清除字典
            }
            lock (dic_ClientThread)
            {
                foreach (var item in dic_ClientThread)
                {
                    item.Value.Abort();//停止线程
                }
                dic_ClientThread.Clear();
            }
            Flag_Listen = false;
            //ServerSocket.Shutdown(SocketShutdown.Both);//服务端不能主动关闭连接,需要把监听到的连接逐个关闭
            if (ServerSocket != null)
                ServerSocket.Close();

        }
        /// <summary>
        /// 监听客户端请求的方法；
        /// </summary>
        private void ListenConnecting()
        {
            while (Flag_Listen)  // 持续不断的监听客户端的连接请求；
            {
                try
                {
                    Socket sokConnection = ServerSocket.Accept(); // 一旦监听到一个客户端的请求，就返回一个与该客户端通信的 套接字；
                                                                  // 将与客户端连接的 套接字 对象添加到集合中；
                    string str_EndPoint = sokConnection.RemoteEndPoint.ToString();
                    PublciSession myTcpClient = new PublciSession() { Name = str_EndPoint, TcpSocket = sokConnection };
                    //创建线程接收数据
                    Thread th_ReceiveData = new Thread(ReceiveData);
                    th_ReceiveData.IsBackground = true;
                    th_ReceiveData.Start(myTcpClient);
                    //把线程及客户连接加入字典
                    dic_ClientThread.Add(str_EndPoint, th_ReceiveData);
                    dic_ClientSocket.Add(str_EndPoint, myTcpClient);

                }
                catch
                {

                }
                Thread.Sleep(200);
            }
        }

        /// <summary>
        /// 接收数据
        /// </summary>
        /// <param name="sokConnectionparn"></param>
        private void ReceiveData(object sokConnectionparn)
        {
            PublciSession tcpClient = sokConnectionparn as PublciSession;
            Socket socketClient = tcpClient.TcpSocket;
            bool Flag_Receive = true;

            while (Flag_Receive)
            {
                try
                {
                    // 定义一个2M的缓存区；
                    var arrMsgRec = new byte[1024 * 1024 * 2];
                    // 将接受到的数据存入到输入  arrMsgRec中；
                    var length = -1;
                    try
                    {
                        length = socketClient.Receive(arrMsgRec); // 接收数据，并返回数据的长度；
                    }
                    catch
                    {
                        Flag_Receive = false;
                        // 从通信线程集合中删除被中断连接的通信线程对象；
                        var keystr = socketClient.RemoteEndPoint.ToString();
                        dic_ClientSocket.Remove(keystr);//删除客户端字典中该socket
                        dic_ClientThread[keystr].Abort();//关闭线程
                        dic_ClientThread.Remove(keystr);//删除字典中该线程

                        tcpClient = null;
                        socketClient = null;
                        break;
                    }

                    //byte[] buf = new byte[length];
                    //Array.Copy(arrMsgRec, buf, length);
                    lock (tcpClient.m_Buffer)
                    {
                        if (length <= 0) continue;
                        var tmpType = arrMsgRec.Take(length).ToArray();
                        ReceiveDataEven?.Invoke(tcpClient.Name, tcpClient.GetString(), tmpType);
                        OnReceiveDataEven?.Invoke(tcpClient.Name, tcpClient.GetString(), tmpType);
                    }
                }
                catch
                {

                }
                Thread.Sleep(CycleTime);
            }
        }

        /// <summary>
        /// 发送数据给指定的客户端
        /// </summary>
        /// <param name="_endPoint">客户端套接字</param>
        /// <param name="_buf">发送的数组</param>
        /// <returns></returns>
        public bool SendData(string _endPoint, byte[] _buf)
        {
            var myT = new PublciSession();

            foreach (var item in dic_ClientSocket)
            {
                {
                    var tcpClient = item.Value as PublciSession;
                    tcpClient.Send(_buf);
                }
            }
            return true;
        }

        /// <summary>
        /// 发送数据给指定的客户端
        /// </summary>
        /// <param name="_endPoint">客户端套接字</param>
        /// <param name="_buf">发送的数组</param>
        /// <returns></returns>
        public bool SendDataByEndPoint(string _endPoint, byte[] _buf)
        {
            var myT = new PublciSession();
            if (_endPoint != null && dic_ClientSocket.TryGetValue(_endPoint, out myT))
            {
                myT.Send(_buf);
                return true;
            }
            return false;
        }
        //发布日志事件
        public delegate void ReceiveDataDelegate(string str, string str2, byte[] buf);
        public static event ReceiveDataDelegate ReceiveDataEven;
        public event ReceiveDataDelegate OnReceiveDataEven;
    }

    /// <summary>
    /// 会话端
    /// </summary>
    public class PublciSession
    {
        public Socket TcpSocket;//socket对象
        public List<byte> m_Buffer = new List<byte>();//数据缓存区
        public List<string> m_Collection = new List<string>();//数据缓存区-string
        public string Name;
        public string StartCode;
        public bool State;
        public byte[] recieve_Buffer;
        public AutoResetEvent autoResetEvent = new AutoResetEvent(false);
        public PublciSession()
        {

        }

        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="buf"></param>
        public void Send(byte[] buf)
        {
            if (buf != null)
            {
                TcpSocket.Send(buf);
            }
        }
        /// <summary>
        /// 发送数据
        /// </summary>
        /// <param name="buf"></param>
        public void SendStr(string str, Encoding encoding = null)
        {
            if (str != null)
            {
                encoding = encoding ?? Encoding.Default;
                var buffer = encoding.GetBytes(str);
                TcpSocket.Send(buffer);
            }
        }
        /// <summary>
        /// 获取连接的ip
        /// </summary>
        /// <returns></returns>
        public string GetIp()
        {
            IPEndPoint clientipe = (IPEndPoint)TcpSocket.RemoteEndPoint;
            string _ip = clientipe.Address.ToString();
            return _ip;
        }
        /// <summary>
        /// 关闭连接
        /// </summary>
        public void Close()
        {
            TcpSocket.Shutdown(SocketShutdown.Both);
        }
        /// <summary>
        /// 提取正确数据包
        /// </summary>
        public byte[] GetBuffer(int startIndex, int size)
        {
            byte[] buf = new byte[size];
            m_Buffer.CopyTo(startIndex, buf, 0, size);
            m_Buffer.RemoveRange(0, startIndex + size);
            return buf;
        }
        /// <summary>
        /// 获取队列字符串
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            if (m_Collection.Count > 0)
            {
                string m_String = m_Collection.First();
                m_Collection.Remove(m_String);
                return m_String;
            }
            return string.Empty;
        }
        /// <summary>
        /// 添加队列数据
        /// </summary>
        /// <param name="buffer"></param>
        public void AddQueue(byte[] buffer)
        {
            //m_Buffer.AddRange(buffer);
            //if (buffer.Contains<byte>((byte)59))
            //{
            m_Collection.Add(Encoding.Default.GetString(buffer));
            if (m_Collection.Count > 200)
            {
                string m_String = m_Collection.First();
                m_Collection.Remove(m_String);
            }
            //}
        }
        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearQueue()
        {
            m_Buffer.Clear();
        }
    }

    /// <summary>
    /// 服务端
    /// </summary>
    public class SocketServer : IDisposable
    {
        private ILoggerService _logger;
        /// <summary>
        /// 服务端IP
        /// </summary>
        public string ServerIp { get; private set; }
        /// <summary>
        /// 监听端口
        /// </summary>
        public int ServerPort { get; private set; }

        public event EventHandler<ClientEventArgs<string>>? OnClientConnected;
        public event EventHandler<ClientEventArgs<string>>? OnClientDisconnected;
        public event EventHandler<ClientEventArgs<byte[]>>? OnMessageReceived;
        public event EventHandler<Exception>? OnError;

        private Socket? _server;
        private bool _isRunning = false;
        private bool _disposed = false;
        private List<Socket> _connectedClients = new List<Socket>();
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverIp"></param>
        /// <param name="serverPort"></param>
        public SocketServer(string serverIp, int serverPort, ILoggerService logger)
        {
            _logger = logger;
            ServerIp = serverIp;
            ServerPort = serverPort;
        }

        /// <summary>
        /// 开启
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            try
            {
                lock (this)
                {
                    if (_isRunning) return;
                    _isRunning = true;
                }

                _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _server.Bind(new IPEndPoint(IPAddress.Parse(ServerIp), ServerPort));
                _server.Listen(10); // 允许最大 10 个连接队列
                _cancellationTokenSource = new CancellationTokenSource();
                _logger.Info($"服务端已启动，监听地址: {ServerIp}:{ServerPort}");
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var client = await _server.AcceptAsync();
                        var msg = $"新客户端连接: {client.RemoteEndPoint}";
                        _logger.Info(msg);
                        _connectedClients.Add(client);
                        OnClientConnected?.Invoke(this, new ClientEventArgs<string>(client, msg));
                        // 启动任务处理该客户端
                        _ = HandleClientReceiveAsync(client, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"错误: {ex.Message}");
                        OnError?.Invoke(this, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _logger.Error($"错误: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        /// <param name="client"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task HandleClientReceiveAsync(Socket client, CancellationToken cancellationToken)
        {
            try
            {
                var buffer = new byte[2 * 1024 * 1024];
                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    var received = await client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    if (received == 0) break;
                    var bytes = new byte[received];
                    Array.Copy(buffer, 0, bytes, 0, received);
                    OnMessageReceived?.Invoke(this, new ClientEventArgs<byte[]>(client, bytes));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"客户端处理异常: {ex.Message}");
                OnError?.Invoke(this, ex);
            }
            finally
            {
                // 从连接列表中移除
                DisconnectedClient(client);
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="Message"></param>
        /// <returns></returns>
        public async Task<int> SendToClientAsync(Socket client, string Message)
        {
            var buffer = Encoding.UTF8.GetBytes(Message);
            return await SendToClientAsync(client, buffer);
        }
        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="Message"></param>
        /// <returns></returns>
        public async Task<int> SendToClientAsync(Socket client, byte[] Message)
        {
            if (!_connectedClients.Contains(client)) return 0;
            try
            {
                return await client.SendAsync(new ArraySegment<byte>(Message), SocketFlags.None);
            }
            catch (Exception ex)
            {
                var msg = $"发送失败给客户端 {client.RemoteEndPoint}: {ex.Message}";
                _logger.Error(msg);
                // 移除断开的客户端
                DisconnectedClient(client);
                return 0;
            }
        }

        /// <summary>
        /// 发送
        /// </summary>
        /// <param name="Message"></param>
        /// <returns></returns>
        public async Task SendToAllAsync(string Message)
        {
            var buffer = Encoding.UTF8.GetBytes(Message + "\n"); // 确保消息格式兼容
            var disconnectedClients = new List<Socket>();
            var clients = new List<Socket>(_connectedClients);
            foreach (var client in clients)
            {
                var send = await SendToClientAsync(client, Message);
            }
        }

        /// <summary>
        /// 溢出客户端
        /// </summary>
        /// <param name="client"></param>
        private void DisconnectedClient(Socket client)
        {
            lock (_connectedClients)
            {
                if (_connectedClients.Contains(client))
                {
                    _connectedClients.Remove(client);
                    var remoteEndPoint = client.RemoteEndPoint;
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    client.Dispose();
                    var msg = $"客户端已断开连接:{remoteEndPoint}";
                    _logger.Info(msg);
                    OnClientDisconnected?.Invoke(this, new ClientEventArgs<string>(client, msg));
                }
            }
        }

        /// <summary>
        /// 停止
        /// </summary>
        public void Stop()
        {
            lock (this)
            {
                if (!_isRunning) return;
                _isRunning = false;
                _cancellationTokenSource?.Cancel();
                _server?.Close();
                _server?.Dispose();
                _server = null;
                _logger.Info("服务端已停止");
            }
        }

        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
*/