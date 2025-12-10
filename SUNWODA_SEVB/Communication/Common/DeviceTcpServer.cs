using Azure.Messaging;
using SUNWODA_SEVB.Core.Interfaces;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using TouchSocket.Core;
using TouchSocket.Sockets;

namespace SUNWODA_SEVB.Communication.Common
{
    /// <summary>
    /// 设备TCP服务器
    /// </summary>
    public class DeviceTcpServer
    {
        private readonly ILoggerService _logger;
        private TcpService _service = new();
        public string DeviceName { get; set; } = "";

        public delegate Task ParseJsonEventHandler(
            string msgType,
            DateTime getDataTime,
            JsonElement dataJsonElement
        );

        public event ParseJsonEventHandler? DataTypeJsonParse;

        public event ParseJsonEventHandler? AlarmTypeJsonParse;

        public event ParseJsonEventHandler? HBTypeJsonParse;

        public event Action? ConnectedEvent;

        public event Action? ClosedEvent;

        public DeviceTcpServer(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(int port = 9000)
        {
            var config = new TouchSocketConfig()
                .SetListenIPHosts(new[] { new IPHost($"0.0.0.0:{port}") })
                // 固定包头：前4字节长度（小端），后接UTF-8 JSON
                .SetTcpDataHandlingAdapter(() => new FixedHeaderPackageAdapter());
            _service.Received += OnReceived;
            _service.Connected += OnConnected;
            _service.Closed += OnClosed;

            await _service.SetupAsync(config);
            await _service.StartAsync();
            _logger.Info($"{DeviceName}服务启动在 {port} 端口");
        }

        private async Task OnClosed(TcpSessionClient client, ClosedEventArgs e)
        {
            _logger.Info($"{DeviceName} {client.Id} 客户端断开连接");
            ClosedEvent?.Invoke();
            await Task.CompletedTask;
        }

        private async Task OnConnected(TcpSessionClient client, ConnectedEventArgs e)
        {
            _logger.Info($"{DeviceName} {client.Id} 客户端连接");
            ConnectedEvent?.Invoke();
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
                _logger.Info($"{DeviceName}数据接收：{json}");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string msgType = root.GetProperty("msgType").GetString() ?? "";
                string sid = root.GetProperty("sid").GetString() ?? "";
                long ts = root.GetProperty("ts").GetInt64();
                var z = root.GetProperty("z").GetString();
                var dRoot = GetDataJsonElement(z, root);

                // 可选校验
                if (root.TryGetProperty("sig", out var sigProp))
                {
                    string sig = sigProp.GetString() ?? "";

                    if (sig != "")
                    {
                        if (!VerifyCrc32(dRoot, sig))
                        {
                            _logger.Error($"{DeviceName} 错误校验：{json}");
                            return;
                        }
                    }
                }

                switch (msgType)
                {
                    case "hb":
                        // 心跳：可记录在线/状态
                        if (HBTypeJsonParse != null)
                            await HBTypeJsonParse.Invoke(msgType, ToLocalDateTime(ts), dRoot);
                        // 可选响应
                        //await SendAsync(client, new { msgType = "pong", ts = NowMs() });
                        break;
                    case "data":
                    case "data_a_wet":
                    case "data_b_wet":
                    case "data_b_dry":
                        if (DataTypeJsonParse != null)
                            await DataTypeJsonParse.Invoke(msgType, ToLocalDateTime(ts), dRoot);
                        // 可选响应
                        //await SendAsync(client, new { msgType = "ok", ts = NowMs() });
                        break;
                    case "alarm":
                        if (AlarmTypeJsonParse != null)
                            await AlarmTypeJsonParse.Invoke(msgType, ToLocalDateTime(ts), dRoot);
                        //var level = dRoot.GetProperty("level").GetString();
                        //var info = dRoot.GetProperty("info").GetString();
                        // 可选响应
                        //await SendAsync(client, new { msgType = "ok", ts = NowMs() });
                        break;
                    default:
                        _logger.Error($"{DeviceName} 未知消息类型：{json}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"{DeviceName} 接收数据错误", ex);
            }
        }

        private static JsonElement GetDataJsonElement(string? z, JsonElement root)
        {
            if (z == "gzip")
            {
                byte[] gz = Convert.FromBase64String(root.GetProperty("data").GetString()!);
                byte[] raw = GzipDecompress(gz);
                string dJson = Encoding.UTF8.GetString(raw);
                return JsonDocument.Parse(dJson).RootElement;
            }
            else
            {
                return root.GetProperty("data");
            }
        }

        private static async Task SendAsync(TcpSessionClient client, object obj)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            var data = Encoding.UTF8.GetBytes(json);
            // 使用 FixedHeaderPackageAdapter：WriteAsync 会自动带4字节长度头
            await client.SendAsync(client.Id, data);
        }

        private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public static DateTime ToLocalDateTime(long ts)
        {
            var dto =
                ts >= 1_000_000_000_000L // 13 位多为毫秒
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
                    : DateTimeOffset.FromUnixTimeSeconds(ts);

            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        /// 对 data 对象做最小化JSON后CRC32校验，sig格式："crc32:XXXXXXXX"
        /// </summary>
        /// <param name="d"></param>
        /// <param name="sig"></param>
        /// <returns></returns>
        private static bool VerifyCrc32(JsonElement d, string sig)
        {
            if (!sig.StartsWith("crc32:", StringComparison.OrdinalIgnoreCase))
                return false;
            var hex = sig.AsSpan(6);
            if (hex.Length != 8)
                return false;

            // 最小化 JSON：System.Text.Json 默认序列化已无多余空格
            string dJson = System.Text.Json.JsonSerializer.Serialize(d);
            uint crc = Crc32(Encoding.UTF8.GetBytes(dJson));
            Span<char> calc = stackalloc char[8];
            ToHex8Upper(crc, calc);
            ReadOnlySpan<char> calcRead = calc;
            return calcRead.SequenceEqual(hex, new CaseInsensitiveCharComparer());
        }

        // 轻量CRC32(IEEE)
        private static uint Crc32(byte[] data)
        {
            const uint poly = 0xEDB88320u;
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ poly : (crc >> 1);
            }
            return ~crc;
        }

        private static void ToHex8Upper(uint v, Span<char> dest)
        {
            const string Hex = "0123456789ABCDEF";
            for (int i = 7; i >= 0; i--)
            {
                dest[7 - i] = Hex[(int)((v >> (i * 4)) & 0xF)];
            }
        }

        private static byte[] GzipCompress(byte[] raw)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionLevel.Fastest, true))
                gz.Write(raw, 0, raw.Length);
            return ms.ToArray();
        }

        private static byte[] GzipDecompress(byte[] gzBytes)
        {
            using var inMs = new MemoryStream(gzBytes);
            using var gz = new GZipStream(inMs, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            gz.CopyTo(outMs);
            return outMs.ToArray();
        }
    }

    public class CaseInsensitiveCharComparer : IEqualityComparer<char>
    {
        public bool Equals(char x, char y)
        {
            // 使用大小写不敏感的比较
            return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
        }

        public int GetHashCode(char obj)
        {
            // 保证大小写一致性，统一转换再取哈希
            return char.ToUpperInvariant(obj).GetHashCode();
        }
    }
}
