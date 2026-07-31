using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Mqtt;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

        public SocketOptionsLoader(IServiceProvider serviceProvider, ILoggerService<SocketOptionsLoader> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<SocketServerOptions> LoadServerOptionsAsync(string moduleName)
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
                options.ReceiveTimeoutMilliseconds = timeoutMs;

                // 读取协议配置
                options.Protocol = await LoadProtocolOptionsAsync(settingRepo, moduleName);

                _logger.Info($"从数据库加载Socket配置成功 [{moduleName}]: " +
                  $"Port={options.DefaultPort}, MaxClients={options.MaxClients}, " +
                  $"FramingMode={options.Protocol.FramingMode}, " +
                  $"StartDelimiter={ByteArrayToString(options.Protocol.StartDelimiter)}, " +
                  $"EndDelimiter={ByteArrayToString(options.Protocol.EndDelimiter)}", true);
            }
            catch (Exception ex)
            {
                _logger.Warn($"从数据库加载配置失败，使用默认值: {ex.Message}");
            }

            return options;
        }

        /// <summary>
        /// 加载 Socket 客户端配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        public async Task<SocketClientOptions> LoadClientOptionsAsync(string moduleName)
        {
            var options = new SocketClientOptions();
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                InitializeSocketClientOptions(settingRepo, moduleName);

                // 读取各项配置
                var optionsJson = await settingRepo.GetSettingValueAsync("SocketClientOptions", moduleName);
                if (!string.IsNullOrEmpty(optionsJson) && !string.IsNullOrWhiteSpace(optionsJson))
                {
                    var loaded = JsonSerializer.Deserialize<SocketClientOptions>(optionsJson, JsonOptions);
                    if (loaded != null) options = loaded;

                    try
                    {
                        var parsedOptions = JsonSerializer.Deserialize<SocketClientOptions>(optionsJson, JsonOptions);
                        if (parsedOptions != null) options = parsedOptions;
                        else _logger.Error($"未找到 {moduleName} 的 Socket 客户端配置，使用默认配置");
                    }
                    catch (Exception)
                    {
                        _logger.Error($"{moduleName} 的 Socket 客户端配置解析异常，使用默认配置");
                    }
                }

                // 读取协议配置
                options.Protocol = await LoadProtocolOptionsAsync(settingRepo, moduleName);

                _logger.Info($"从数据库加载 Socket 客户端配置成功 [{moduleName}]: " +
                  $"IP={options.DefaultIP}, Port={options.DefaultPort}, " +
                  $"FramingMode={options.Protocol.FramingMode}, " +
                  $"StartDelimiter={ByteArrayToString(options.Protocol.StartDelimiter)}, " +
                  $"EndDelimiter={ByteArrayToString(options.Protocol.EndDelimiter)}", true);
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
        /// <param name="moduleName"></param>
        /// <returns></returns>
        public async Task<ProtocolOptions> LoadProtocolOptionsAsync(string moduleName)
        {
            using var scope = _serviceProvider.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

            return await LoadProtocolOptionsAsync(settingRepo, moduleName);
        }

        /// <summary>
        /// 更新 Socket 服务端配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task<bool> UpdateServerOptionsAsync(string moduleName, SocketServerOptions options)
        {
            if (options == null) return false;
            using var scope = _serviceProvider.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
            var paramName = "Port";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, "8080", "int", 2)
                    {
                        Remark = "Soket端口号"
                    }
                );
            }
            var result = await settingRepo.UpdateSettingValueAsync(paramName, moduleName, options.DefaultPort);
            paramName = "MaxClients";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, "10", "int", 2)
                    {
                        Remark = "最大客户端连接数"
                    }
                );
            }
            result &= await settingRepo.UpdateSettingValueAsync(paramName, moduleName, options.MaxClients);
            paramName = "ReceiveBufferSize";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, "8192", "int", 2)
                    {
                        Remark = "接受缓冲区大小"
                    }
                );
            }
            result &= await settingRepo.UpdateSettingValueAsync(paramName, moduleName, options.ReceiveBufferSize);
            paramName = "Backlog";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, "100", "int", 2)
                    {
                        Remark = "连接对接长度"
                    }
                );
            }
            result &= await settingRepo.UpdateSettingValueAsync(paramName, moduleName, options.Backlog);
            paramName = "ReceiveTimeoutMs";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, "0", "int", 2)
                    {
                        Remark = "接收超时(毫秒)，0为无限"
                    }
                );
            }
            result &= await settingRepo.UpdateSettingValueAsync(paramName, moduleName, options.ReceiveTimeoutMilliseconds);

            return result;
        }

        /// <summary>
        /// 更新 Socket 客户端配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task<bool> UpdateClientOptionsAsync(string moduleName, SocketClientOptions options)
        {
            if (options == null) return false;
            using var scope = _serviceProvider.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
            var paramName = "SocketClientOptions";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new SocketClientOptions();
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Socket 客户端参数信息"
                    }
                );
            }
            return await settingRepo.UpdateSettingValueAsync(paramName, moduleName, JsonSerializer.Serialize(options, JsonOptions));
        }

        /// <summary>
        /// 更新 Socket 协议配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public async Task<bool> UpdateProtocolOptionsAsync(string moduleName, ProtocolOptions options)
        {
            if (options == null) return false;

            using var scope = _serviceProvider.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
            var paramName = "Protocol";
            if (!settingRepo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new ProtocolOptions() { FramingMode = FramingMode.None };
                settingRepo.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Socket 协议配置"
                    }
                );
            }
            return await settingRepo.UpdateSettingValueAsync(paramName, moduleName, JsonSerializer.Serialize(options, JsonOptions));
        }

        /// <summary>
        /// 初始化moduleName对应的Socket客户端配置
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="moduleName"></param>
        private void InitializeSocketClientOptions(IProjectSettingRepository repo, string moduleName)
        {
            var paramName = "SocketClientOptions";
            if (!repo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new SocketClientOptions();
                repo.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Socket 客户端参数信息"
                    }
                );
            }

            paramName = "Protocol";
            if (!repo.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new ProtocolOptions();
                model.FramingMode = FramingMode.None;
                repo.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Socket 客户端协议配置"
                    }
                );
            }
        }

        /// <summary>
        /// 字节数组转显示字符串
        /// </summary>
        private string ByteArrayToString(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return "null";

            var hex = BitConverter.ToString(bytes);
            var text = Encoding.UTF8.GetString(bytes);
            return $"HEX=[{hex}] TEXT=[{text}] Length={bytes.Length}";
        }

        /// <summary>
        /// 加载协议配置
        /// </summary>
        private async Task<ProtocolOptions> LoadProtocolOptionsAsync(IProjectSettingRepository repo, string moduleName)
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
        private async Task<ProtocolOptions> LoadProtocolFromSeparateSettingsAsync(IProjectSettingRepository repo, string moduleName)
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
        private async Task<string?> GetStringSettingAsync(IProjectSettingRepository repo, string key, string moduleName)
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
                case "":
                    return null;
            }
            // 处理 /uXXXX 或 \uXXXX 格式 (Unicode转义序列)
            if (value.Contains("/u") || value.Contains("\\u"))
            {
                try
                {
                    var hasEscapePattern = Regex.IsMatch(value, @"[/\\]u[0-9A-Fa-f]{4}");

                    if (hasEscapePattern)
                    {
                        var bytes = Encoding.UTF8.GetBytes(value);
                        return bytes;
                    }

                    //// 匹配所有 \uXXXX 模式
                    //var regex = new System.Text.RegularExpressions.Regex(@"\\u([0-9A-Fa-f]{4})");
                    //var matches = regex.Matches(pattern);

                    //if (matches.Count > 0)
                    //{
                    //    foreach (System.Text.RegularExpressions.Match match in matches)
                    //    {
                    //        var hexValue = match.Groups[1].Value;
                    //        var intValue = Convert.ToInt32(hexValue, 16);

                    //        // 如果是单字节范围 (0-255)，直接添加
                    //        if (intValue <= 0xFF)
                    //        {
                    //            bytes.Add((byte)intValue);
                    //        }
                    //        else
                    //        {
                    //            // 否则转换为UTF-8字节
                    //            var charBytes = Encoding.UTF8.GetBytes(new char[] { (char)intValue });
                    //            bytes.AddRange(charBytes);
                    //        }
                    //    }

                    //    if (bytes.Count > 0)
                    //    {
                    //        //_logger.Debug($"解析Unicode转义符成功: {value} => [{string.Join(",", bytes.Select(b => $"0x{b:X2}"))}]");
                    //        return bytes.ToArray();
                    //    }


                    // 如果没有匹配到 \uXXXX，尝试解析为普通字符串
                    _logger.Warn($"Unicode转义格式不正确: {value}，将作为普通字符串处理");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"解析Unicode转义符失败: {value}, 错误: {ex.Message}");
                }
            }
            // 尝试解析JSON数组格式 [2, 3]
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                try
                {
                    var array = JsonSerializer.Deserialize<byte[]>(value, JsonOptions);
                    if (array != null && array.Length > 0)
                    {
                        //_logger.Debug($"解析JSON数组成功: {value}");
                        return array;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"解析JSON数组失败: {value}, {ex.Message}");
                }
            }

            // 尝试解析十六进制格式 "0x02" 或 "02,03"
            if (value.Contains(",") || value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                value.All(c => char.IsDigit(c) || "ABCDEFabcdef".Contains(c)))
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
                    {
                        //_logger.Debug($"解析十六进制成功: {value}");
                        return bytes.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Debug($"解析十六进制失败: {value}, {ex.Message}");
                }
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
        private async Task<int> GetIntSettingAsync(IProjectSettingRepository repo, string key, string moduleName, int defaultValue)
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
        private async Task<bool> GetBoolSettingAsync(IProjectSettingRepository repo, string key, string moduleName, bool defaultValue)
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
