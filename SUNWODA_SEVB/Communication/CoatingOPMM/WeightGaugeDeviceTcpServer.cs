using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CoatingOPMM;
using System.Text;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace SUNWODA_SEVB.Communication.CoatingOPMM
{
    /// <summary>
    /// 测重仪设备TCP服务器
    /// </summary>
    public class WeightGaugeDeviceTcpServer
    {
        private readonly ILoggerService _logger;
        private TcpService _service = new();

        public delegate Task WeightMessageParseSEventHandler(
            DateTime gatherTime,
            CCTBWEIGHTQUEST? weight
        );

        public event WeightMessageParseSEventHandler? WeightMessageParseSEvent;

        public WeightGaugeDeviceTcpServer(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(int port = 9001)
        {
            var config = new TouchSocketConfig()
                .SetListenIPHosts(new[] { new IPHost($"0.0.0.0:{port}") })
                .SetMaxBufferSize(2 * 1024 * 1024)
                .SetTcpDataHandlingAdapter(() => new FixedHeaderPackageAdapter());
            _service.Received += OnReceived;
            _service.Connected += OnConnected;
            _service.Closed += OnClosed;

            await _service.SetupAsync(config);
            await _service.StartAsync();
            _logger.Info($"测重仪数据采集服务启动在 {port} 端口");
        }

        private async Task OnClosed(TcpSessionClient client, ClosedEventArgs e)
        {
            _logger.Info($"测重仪 {client.Id} 客户端断开连接");
            await Task.CompletedTask;
        }

        private async Task OnConnected(TcpSessionClient client, ConnectedEventArgs e)
        {
            _logger.Info($"测重仪 {client.Id} 客户端连接");
            await Task.CompletedTask;
        }

        private async Task OnReceived(TcpSessionClient client, ReceivedDataEventArgs e)
        {
            try
            {
                // 拆到的是“一个完整帧”的 Payload
                var json = Encoding.UTF8.GetString(
                    e.ByteBlock.Memory.ToArray(),
                    0,
                    e.ByteBlock.Length
                );
                await WeightMessageParseS(DateTime.Now, json);
            }
            catch (Exception ex)
            {
                _logger.Error("接收测重仪数据错误", ex);
            }
        }

        /// <summary>
        /// 解析报文
        /// </summary>
        /// <param name="gatherTime"></param>
        /// <param name="str"></param>
        private async Task WeightMessageParseS(DateTime gatherTime, string str)
        {
            try
            {
                _logger.Info($"接收报文[{str}]");
                var tbLis = TBProtocolData.ProtocolAnalysis(str, false);
                foreach (var tb in tbLis)
                {
                    if (tb == null || !tb.AnalysisFlag)
                    {
                        _logger.Error($"报文解析失败");
                        continue;
                    }
                    if ((tb.NeedCrcCheck && !tb.DataCrcFlag))
                    {
                        _logger.Error($"[{tb.CommandName}]CRC[{tb.DataCrc}]校验失败");
                        continue;
                    }
                    switch (tb.CommandName)
                    {
                        case "CCTBHEARTQUEST":
                            var cCTBHEARTQUEST = JsonConvert.DeserializeObject<CCTBHEARTQUEST>(
                                tb.DataJson
                            );
                            WeightMessageParseS_CCTBHEARTQUEST(gatherTime, cCTBHEARTQUEST);
                            break;
                        case "CCTBWEIGHTQUEST":
                            var cCTBWEIGHTQUEST = JsonConvert.DeserializeObject<CCTBWEIGHTQUEST>(
                                tb.DataJson
                            );
                            if (WeightMessageParseSEvent != null)
                                await WeightMessageParseSEvent.Invoke(gatherTime, cCTBWEIGHTQUEST);
                            break;
                        default:
                            _logger.Warn($"未实现报文指令[{tb.CommandName}]");
                            break;
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error("涂布报文解析异常：", ex);
                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// 心跳处理
        /// </summary>
        /// <param name="gatherTime"></param>
        /// <param name="messageTagStr"></param>
        /// <param name="weight"></param>
        /// <exception cref="Exception"></exception>
        private void WeightMessageParseS_CCTBHEARTQUEST(DateTime gatherTime, CCTBHEARTQUEST? weight)
        {
            try
            {
                // -TODO    心跳处理
            }
            catch (Exception ex)
            {
                throw new Exception($"设备[{weight?.DEVICESN}]心跳报文处理异常！", ex);
            }
        }
    }
}
