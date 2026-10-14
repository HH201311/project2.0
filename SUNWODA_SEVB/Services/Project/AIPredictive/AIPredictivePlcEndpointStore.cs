using System.IO;
using System.Text.Json;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 本机配置：程序目录 <c>AIPredictive/plc_endpoint.json</c>
    ///（厂家 + IP + IntervalMs；端口固定 44818）。
    /// </summary>
    public sealed class AIPredictivePlcEndpointStore : IAIPredictivePlcEndpointStore
    {
        public const string RelativeFileName = @"AIPredictive\plc_endpoint.json";
        public const string DefaultIp = "10.147.72.10";
        public const int DefaultPort = AIPredictiveVendorNames.FixedCipPort;
        public const int DefaultIntervalMs = AIPredictivePlcEndpoint.DefaultIntervalMs;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        private readonly ILoggerService<AIPredictivePlcEndpointStore> _logger;
        private readonly object _gate = new();

        public string SettingsFilePath { get; }

        public AIPredictivePlcEndpointStore(ILoggerService<AIPredictivePlcEndpointStore> logger)
        {
            _logger = logger;
            SettingsFilePath = Path.Combine(AppContext.BaseDirectory, RelativeFileName);
        }

        public AIPredictivePlcEndpoint Load()
        {
            lock (_gate)
            {
                try
                {
                    if (!File.Exists(SettingsFilePath))
                        return CreateDefault();

                    var json = File.ReadAllText(SettingsFilePath);
                    var ep = JsonSerializer.Deserialize<AIPredictivePlcEndpoint>(json);
                    if (ep is null || string.IsNullOrWhiteSpace(ep.Ip))
                    {
                        _logger.Warn($"AIPredictive PLC 配置无效，回退默认：{SettingsFilePath}", false);
                        return CreateDefault();
                    }

                    return Normalize(ep);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"AIPredictive 读取 PLC 配置失败，回退默认：{ex.Message}", false);
                    return CreateDefault();
                }
            }
        }

        public void Save(AIPredictivePlcEndpoint endpoint)
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            if (string.IsNullOrWhiteSpace(endpoint.Ip))
                throw new ArgumentException("PLC IP 不能为空", nameof(endpoint));

            var toSave = Normalize(endpoint);

            lock (_gate)
            {
                var dir = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(toSave, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
                _logger.Info(
                    $"AIPredictive 配置已保存：厂家={toSave.Vendor}，{toSave.Ip}:{toSave.Port}，" +
                    $"周期={toSave.IntervalMs}ms → {SettingsFilePath}",
                    true);
            }
        }

        private static AIPredictivePlcEndpoint Normalize(AIPredictivePlcEndpoint ep) => new()
        {
            Vendor = AIPredictiveVendorNames.Normalize(ep.Vendor),
            Ip = ep.Ip.Trim(),
            Port = AIPredictiveVendorNames.FixedCipPort,
            IntervalMs = AIPredictivePlcEndpoint.ClampIntervalMs(ep.IntervalMs),
        };

        private static AIPredictivePlcEndpoint CreateDefault() => new()
        {
            Vendor = AIPredictiveVendorNames.XianDao,
            Ip = DefaultIp,
            Port = DefaultPort,
            IntervalMs = DefaultIntervalMs,
        };
    }
}
