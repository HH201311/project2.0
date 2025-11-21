using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.MES.Providers
{
    /// <summary>
    /// MES配置提供者实现
    /// </summary>
    public class MesConfigurationProvider : IMesConfigurationProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<MesConfigurationProvider> _logger;
        //private readonly IGlobalSettingRepository _globalSettingRepository;
        private readonly SemaphoreSlim _loadLock = new(1, 1);

        private MesApiConfiguration? _currentConfiguration;
        private bool _hasAttemptedLoad = false;

        private DateTimeOffset _lastSuccessfulLoadUtc = DateTimeOffset.MinValue;
        private DateTimeOffset _lastLoadAttemptUtc = DateTimeOffset.MinValue;

        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _errorBackoff = TimeSpan.FromSeconds(30);

        public MesApiConfiguration? CurrentConfiguration => _currentConfiguration;

        public MesConfigurationProvider(
            IConfiguration configuration,
            IServiceProvider serviceProvider,
            ILoggerService<MesConfigurationProvider> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MesApiConfiguration?> GetConfigurationAsync()
        {
            // 首次调用时，总是尝试加载
            if (!_hasAttemptedLoad)
            {
                _logger.Info("首次获取MES配置，尝试从数据库加载");
                await _loadLock.WaitAsync();
                try
                {
                    if (!_hasAttemptedLoad) // 双重检查
                    {
                        var config = await LoadConfigurationFromDatabaseAsync();
                        _hasAttemptedLoad = true;
                        if (config != null)
                        {
                            _currentConfiguration = config;
                            _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                            _logger.Info("首次加载MES配置成功");
                        }
                        else
                        {
                            _logger.Warn("首次加载MES配置失败或无配置");
                        }
                        return config;
                    }
                }
                finally
                {
                    _loadLock.Release();
                }
            }

            var now = DateTimeOffset.UtcNow;

            // 如果有缓存且未过期，直接返回
            if (_currentConfiguration != null &&
                now - _lastSuccessfulLoadUtc < _cacheExpiration)
            {
                return _currentConfiguration;
            }

            // 如果最近失败过，进行失败回退
            if (_currentConfiguration == null &&
                _lastLoadAttemptUtc != DateTimeOffset.MinValue &&
                now - _lastLoadAttemptUtc < _errorBackoff)
            {
                _logger.Debug($"跳过重新加载（仍在失败回退窗口 {_errorBackoff.TotalSeconds}s 内）");
                return null;
            }

            await _loadLock.WaitAsync();
            try
            {
                now = DateTimeOffset.UtcNow;
                if (_currentConfiguration != null &&
                    now - _lastSuccessfulLoadUtc < _cacheExpiration)
                {
                    return _currentConfiguration;
                }

                _lastLoadAttemptUtc = now;
                var cfg = await LoadConfigurationFromDatabaseAsync();

                if (cfg != null)
                {
                    _currentConfiguration = cfg;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                }

                return _currentConfiguration;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<bool> ReloadConfigurationAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                _logger.Info("强制重新加载MES配置");
                _lastLoadAttemptUtc = DateTimeOffset.MinValue; // 重置失败回退
                _hasAttemptedLoad = false; // 重置首次加载标志

                var config = await LoadConfigurationFromDatabaseAsync();

                if (config != null)
                {
                    _currentConfiguration = config;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                    _hasAttemptedLoad = true;
                    _logger.Info("MES配置重新加载成功");
                    return true;
                }
                else
                {
                    _hasAttemptedLoad = true;
                    _logger.Warn("MES配置重新加载失败或无配置");
                    return false;
                }
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task<MesApiConfiguration?> LoadConfigurationFromDatabaseAsync()
        {
            try
            {
                _logger.Debug("开始从数据库加载MES配置");

                using var scope = _serviceProvider.CreateScope();
                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var mesSettingRepo = scope.ServiceProvider.GetRequiredService<IMESSettingRepository>();

                // 检查配置文件中的MES开关
                var mesEnabledValue = await globalSettingRepo.GetSettingValueAsync("IsMESEnabled");
                var mesEnabled = mesEnabledValue ?? false;  // 如果为null，默认为false
                _logger.Debug($"IsMESEnabled 数据库值: {mesEnabled}");

                if (!mesEnabled)
                {
                    _logger.Info("MES服务未开启");
                    return null;
                }

                // 查询项目的MES Profile
                var mesProfileSetting = await globalSettingRepo.GetByNameAsync("MesProfile");

                if (mesProfileSetting == null || string.IsNullOrWhiteSpace(mesProfileSetting.Value))
                {
                    _logger.Info($"当前项目未配置MES Profile");
                    return null;
                }

                var profileName = mesProfileSetting.Value;
                _logger.Info($"项目使用MES Profile: {profileName}");

                // 加载MES详细配置
                var mesSettings = await mesSettingRepo.GetByProfileNameAsync(profileName);
                if (mesSettings == null || !mesSettings.Any())
                {
                    _logger.Warn($"未找到Profile '{profileName}' 的MES配置");
                    return null;
                }

                // 构建配置对象
                var configuration = BuildMesConfiguration(profileName, mesSettings);

                // 验证配置
                if (!configuration.IsValid(out var errorMessage))
                {
                    _logger.Error($"MES配置验证失败: {errorMessage}");
                    return null;
                }

                // 7. 更新缓存
                _currentConfiguration = configuration;
                _lastSuccessfulLoadUtc = DateTime.Now;

                _logger.Info($"MES配置加载成功: BaseUrl={configuration.BaseUrl}, " +
                           $"Timeout={configuration.TimeoutSeconds}s, " +
                           $"EnableRetry={configuration.EnableRetry}");

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.Error("从数据库加载MES配置时发生错误", ex);
                return null;
            }
        }

        private MesApiConfiguration BuildMesConfiguration(string profileName, IEnumerable<dynamic> settings)
        {
            var config = new MesApiConfiguration
            {
                ProfileName = profileName,
                TimeoutSeconds = 30,  // 默认值
                EnableRetry = true,    // 默认值
                MaxRetryCount = 3      // 默认值
            };

            // 定义端点映射
            var endpointMappings = new Dictionary<string, string>();

            foreach (var setting in settings)
            {
                if (setting == null)
                    continue;

                // 安全获取Key和Value
                string? keyStr = setting.Key?.ToString();
                string? valueStr = setting.Value?.ToString();

                // 如果Key或Value为空，跳过此配置
                if (string.IsNullOrWhiteSpace(keyStr) || string.IsNullOrWhiteSpace(valueStr))
                    continue;

                var key = keyStr.ToLower();
                var value = valueStr;

                switch (key)
                {
                    case "baseurl":
                        config.BaseUrl = value;
                        break;

                    case "timeout":
                    case "timeoutseconds":
                        if (int.TryParse(value, out int timeout))
                            config.TimeoutSeconds = timeout;
                        break;

                    case "enableretry":
                        if (bool.TryParse(value, out bool retry))
                            config.EnableRetry = retry;
                        break;

                    case "maxretrycount":
                        if (int.TryParse(value, out int retryCount))
                            config.MaxRetryCount = retryCount;
                        break;

                    case "operatorid":
                        config.OperatorId = value;
                        break;

                    case "groupcode":
                        config.GroupCode = value;
                        break;

                    case "devicesn":
                        config.DeviceSn = value;
                        break;

                    case "monumber":
                        config.MoNumber = value;
                        break;

                    case "controlgroup":
                        config.ControlGroup = value;
                        break;

                    default:
                        // 检查是否是端点配置（以endpoint_开头）
                        if (key.StartsWith("endpoint_"))
                        {
                            var endpointName = key.Substring("endpoint_".Length);
                            endpointMappings[endpointName] = value;
                        }
                        // 检查是否是自定义头（以header_开头）
                        else if (key.StartsWith("header_"))
                        {
                            var headerName = key.Substring("header_".Length);
                            config.CustomHeaders[headerName] = value;
                        }
                        else
                        {
                            // 存储为自定义设置
                            config.CustomSettings[setting.Key.ToString()] = value;
                        }
                        break;
                }
            }


            // 设置端点映射
            if (endpointMappings.Any())
            {
                config.Endpoints = endpointMappings;
                _logger.Info($"加载了 {endpointMappings.Count} 个端点映射");
            }

            // 设置默认端点（如果数据库中没有配置）
            if (!config.Endpoints.ContainsKey("OfflineDataUpload"))
                config.Endpoints["OfflineDataUpload"] = "mes/OfflineDataUpload";
            if (!config.Endpoints.ContainsKey("IncreaseMarking"))
                config.Endpoints["IncreaseMarking"] = "mes/IncreaseMarking";


            return config;
        }
    }
}
