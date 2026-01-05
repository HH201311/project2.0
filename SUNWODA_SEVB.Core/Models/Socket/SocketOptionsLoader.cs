using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using System.Text.Json;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket配置加载器实现
    /// </summary>
    public class SocketOptionsLoader : ISocketOptionsLoader
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<SocketOptionsLoader> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public SocketOptionsLoader(
            IServiceProvider serviceProvider,
            ILoggerService<SocketOptionsLoader> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<SocketServerOptions> LoadOptionsAsync(string moduleName)
        {
            var options = new SocketServerOptions { ModuleName = moduleName };

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                // 读取各项配置
                options.DefaultPort = await GetIntSettingAsync(settingRepo, "Port", moduleName, 8080);
                options.MaxClients = await GetIntSettingAsync(settingRepo, "MaxClients", moduleName, 10);
                options.ReceiveBufferSize = await GetIntSettingAsync(settingRepo, "ReceiveBufferSize", moduleName, 8192);
                options.Backlog = await GetIntSettingAsync(settingRepo, "Backlog", moduleName, 100);

                var timeoutMs = await GetIntSettingAsync(settingRepo, "ReceiveTimeoutMs", moduleName, 0);
                options.ReceiveTimeout = timeoutMs > 0 ? TimeSpan.FromMilliseconds(timeoutMs) : null;

                // 读取协议配置
                options.Protocol = await LoadProtocolOptionsAsync(settingRepo, moduleName);

                _logger.Info($"从数据库加载Socket配置成功 [{moduleName}]: " +
                    $"Port={options.DefaultPort}, MaxClients={options.MaxClients}, " +
                    $"FramingMode={options.Protocol.FramingMode}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"从数据库加载配置失败，使用默认值: {ex.Message}");
            }

            return options;
        }


        /// <summary>
        /// 加载协议配置
        /// </summary>
        private async Task<ProtocolOptions> LoadProtocolOptionsAsync(
            IProjectSettingRepository repo,
            string moduleName)
        {
            var protocol = new ProtocolOptions();

            try
            {
                // 方式1：尝试读取完整的JSON配置
                var protocolJson = await repo.GetSettingValueAsync("Protocol", moduleName);
                if (!string.IsNullOrWhiteSpace(protocolJson))
                {
                    var loaded = JsonSerializer.Deserialize<ProtocolOptions>(protocolJson, JsonOptions);
                    if (loaded != null)
                    {
                        _logger.Debug($"从JSON加载协议配置成功 [{moduleName}]");
                        return loaded;
                    }
                }

                // 方式2：读取单独的配置项
                protocol = await LoadProtocolFromSeparateSettingsAsync(repo, moduleName);
            }
            catch (Exception ex)
            {
                _logger.Debug($"加载协议配置失败，使用默认值: {ex.Message}");
            }

            return protocol;
        }

        /// <summary>
        /// 从单独的配置项加载协议配置
        /// </summary>
        private async Task<ProtocolOptions> LoadProtocolFromSeparateSettingsAsync(
            IProjectSettingRepository repo,
            string moduleName)
        {
            var protocol = new ProtocolOptions();

            try
            {
                // 分包模式
                var framingModeStr = await GetStringSettingAsync(repo, "FramingMode", moduleName);
                if (!string.IsNullOrWhiteSpace(framingModeStr) &&
                    Enum.TryParse<FramingMode>(framingModeStr, true, out FramingMode framingMode))
                {
                    protocol.FramingMode = framingMode;
                }

                // 起始符
                var startDelimiterStr = await GetStringSettingAsync(repo, "StartDelimiter", moduleName);
                protocol.StartDelimiter = ParseDelimiter(startDelimiterStr);

                // 结束符
                var endDelimiterStr = await GetStringSettingAsync(repo, "EndDelimiter", moduleName);
                protocol.EndDelimiter = ParseDelimiter(endDelimiterStr);

                // 固定长度
                protocol.FixedFrameLength = await GetIntSettingAsync(repo, "FixedFrameLength", moduleName, 1024);

                // 长度前缀相关
                protocol.LengthFieldOffset = await GetIntSettingAsync(repo, "LengthFieldOffset", moduleName, 0);
                protocol.LengthFieldSize = await GetIntSettingAsync(repo, "LengthFieldSize", moduleName, 4);
                protocol.LengthIncludesHeader = await GetBoolSettingAsync(repo, "LengthIncludesHeader", moduleName, false);
                protocol.BigEndian = await GetBoolSettingAsync(repo, "BigEndian", moduleName, false);

                // 其他选项
                protocol.StripDelimiters = await GetBoolSettingAsync(repo, "StripDelimiters", moduleName, true);
                protocol.MaxFrameLength = await GetIntSettingAsync(repo, "MaxFrameLength", moduleName, 65536);

                _logger.Debug($"协议配置加载完成 [{moduleName}]: FramingMode={protocol.FramingMode}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载协议配置时发生异常 [{moduleName}]: {ex.Message}，使用默认值");
            }

            return protocol;
        }

        /// <summary>
        /// 获取字符串配置
        /// </summary>
        private async Task<string?> GetStringSettingAsync(
            IProjectSettingRepository repo,
            string key,
            string moduleName)
        {
            try
            {
                var setting = await repo.GetByNameAndVMAsync(key, moduleName);
                return setting?.Value;
            }
            catch (Exception ex)
            {
                _logger.Debug($"读取配置 {key} 失败: {ex.Message}");
                return null;
            }
        }


        /// <summary>
        /// 解析分隔符字符串
        /// 支持格式：
        /// - 十六进制数组: "0x02" 或 "02,03" 或 "0x02,0x03"
        /// - 字符串: "\r\n" 或 "STX"
        /// - JSON数组: "[2,3]"
        /// </summary>
        private byte[]? ParseDelimiter(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Trim();

            // 处理特殊字符串
            switch (value.ToUpperInvariant())
            {
                case "STX":
                    return new byte[] { 0x02 };
                case "ETX":
                    return new byte[] { 0x03 };
                case "CR":
                    return new byte[] { 0x0D };
                case "LF":
                    return new byte[] { 0x0A };
                case "CRLF":
                case "\\R\\N":
                    return new byte[] { 0x0D, 0x0A };
                case "TAB":
                    return new byte[] { 0x09 };
                case "NULL":
                case "NONE":
                    return null;
            }

            // 尝试解析JSON数组格式 [2, 3]
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                try
                {
                    var array = JsonSerializer.Deserialize<byte[]>(value, JsonOptions);
                    return array;
                }
                catch { }
            }

            // 尝试解析十六进制格式 "0x02" 或 "02,03"
            if (value.Contains(",") || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var parts = value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var bytes = new List<byte>();

                    foreach (var part in parts)
                    {
                        var hex = part.Trim();
                        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        {
                            hex = hex.Substring(2);
                        }
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var b))
                        {
                            bytes.Add(b);
                        }
                    }

                    if (bytes.Count > 0)
                        return bytes.ToArray();
                }
                catch { }
            }

            // 尝试解析单个十六进制数
            if (value.Length <= 4)
            {
                var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring(2)
                    : value;

                if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var b))
                {
                    return new byte[] { b };
                }
            }

            // 作为普通字符串处理
            return System.Text.Encoding.UTF8.GetBytes(value);
        }
        private async Task<int> GetIntSettingAsync(
    IProjectSettingRepository repo, string key, string moduleName, int defaultValue)
        {
            try
            {
                var pa_value = await repo.GetByNameAndVMAsync(key, moduleName);
                var value = pa_value?.Value;

                if (int.TryParse(value, out int result) && result > 0)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"读取配置 {key} 失败: {ex.Message}");
            }
            return defaultValue;
        }
        private async Task<bool> GetBoolSettingAsync(
           IProjectSettingRepository repo,
           string key,
           string moduleName,
           bool defaultValue)
        {
            try
            {
                var pa_value = await repo.GetByNameAndVMAsync(key, moduleName);
                var value = pa_value?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // 支持多种布尔值格式
                    return value.Trim().ToLowerInvariant() switch
                    {
                        "true" or "1" or "yes" or "on" => true,
                        "false" or "0" or "no" or "off" => false,
                        _ => defaultValue
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"读取配置 {key} 失败: {ex.Message}");
            }
            return defaultValue;
        }
    }
}
