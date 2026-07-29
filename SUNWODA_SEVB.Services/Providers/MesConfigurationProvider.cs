//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using SUNWODA_SEVB.Core.Interfaces.Data;
//using SUNWODA_SEVB.Core.Interfaces.MES;
//using SUNWODA_SEVB.Core.Interfaces;
//using SUNWODA_SEVB.Core.Models.MES;

//namespace SUNWODA_SEVB.MES.Providers
//{
//    /// <summary>
//    /// MES配置提供者实现
//    /// </summary>
//    public class MesConfigurationProvider : IMesConfigurationProvider
//    {
//        private readonly IConfiguration _configuration;
//        private readonly IServiceProvider _serviceProvider;
//        private readonly ILoggerService<MesConfigurationProvider> _logger;

//        private readonly SemaphoreSlim _loadLock = new(1, 1);

//        // 用于检测重入
//        private readonly AsyncLocal<bool> _isLoadingOnCurrentThread = new();

//        private MesApiConfiguration? _currentConfiguration;
//        private bool _hasAttemptedLoad = false;
//        private volatile bool _isLoading = false;

//        private DateTimeOffset _lastSuccessfulLoadUtc = DateTimeOffset.MinValue;
//        private DateTimeOffset _lastLoadAttemptUtc = DateTimeOffset.MinValue;

//        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
//        private readonly TimeSpan _errorBackoff = TimeSpan.FromSeconds(30);
//        private readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);

//        public MesApiConfiguration? CurrentConfiguration => _currentConfiguration;

//        public MesConfigurationProvider(
//            IConfiguration configuration,
//            IServiceProvider serviceProvider,
//            ILoggerService<MesConfigurationProvider> logger)
//        {
//            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
//            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//        }


//        /// <summary>
//        /// 获取当前是否正在加载配置
//        /// </summary>
//        public bool IsLoading => _isLoading;

//        public async Task<MesApiConfiguration?> GetConfigurationAsync()
//        {
//            if (_isLoadingOnCurrentThread.Value)
//            {
//                _logger.Debug("检测到重入调用，返回当前缓存配置");
//                return _currentConfiguration;
//            }
//            // 如果其他线程正在加载，短暂等待
//            if (_isLoading)
//            {
//                _logger.Debug("其他线程正在加载配置，等待完成或返回缓存");
//                var waitStart = DateTimeOffset.UtcNow;
//                while (_isLoading && DateTimeOffset.UtcNow - waitStart < TimeSpan.FromSeconds(5))
//                {
//                    await Task.Delay(50);
//                }

//                if (_isLoading)
//                {
//                    _logger.Warn("等待其他线程加载超时，继续执行");
//                }
//                else
//                {
//                    _logger.Debug("其他线程加载完成，检查缓存");
//                    var recentConfig = GetCachedConfigurationIfValid();
//                    if (recentConfig != null)
//                    {
//                        return recentConfig;
//                    }
//                }
//            }
//            // 快速路径：如果有有效缓存，直接返回
//            var cachedConfig = GetCachedConfigurationIfValid();
//            if (cachedConfig != null)
//            {
//                return cachedConfig;
//            }

//            // 首次调用或需要刷新缓存
//            return await LoadWithLockAsync(isFirstLoad: !_hasAttemptedLoad);
//        }

//        private MesApiConfiguration? GetCachedConfigurationIfValid()
//        {
//            var now = DateTimeOffset.UtcNow;

//            // 如果有缓存且未过期，直接返回
//            if (_currentConfiguration != null &&
//                _lastSuccessfulLoadUtc != DateTimeOffset.MinValue &&
//                now - _lastSuccessfulLoadUtc < _cacheExpiration)
//            {
//                return _currentConfiguration;
//            }

//            // 如果已尝试加载但配置为null（MES禁用），且在有效期内，返回null但不重新加载
//            if (_hasAttemptedLoad &&
//                _currentConfiguration == null &&
//                _lastSuccessfulLoadUtc != DateTimeOffset.MinValue &&
//                now - _lastSuccessfulLoadUtc < _cacheExpiration)
//            {
//                return null; // 这是有效的"空配置"缓存
//            }

//            return null;
//        }

//        private async Task<MesApiConfiguration?> LoadWithLockAsync(bool isFirstLoad)
//        {
//            var now = DateTimeOffset.UtcNow;

//            // 失败回退检查（在获取锁之前）
//            if (!isFirstLoad &&
//                _currentConfiguration == null &&
//                _lastLoadAttemptUtc != DateTimeOffset.MinValue &&
//                now - _lastLoadAttemptUtc < _errorBackoff)
//            {
//                _logger.Debug($"跳过重新加载（仍在失败回退窗口 {_errorBackoff.TotalSeconds}s 内）");
//                return null;
//            }

//            // 尝试获取锁，带超时
//            bool lockAcquired = false;
//            try
//            {
//                lockAcquired = await _loadLock.WaitAsync(_lockTimeout);

//                if (!lockAcquired)
//                {
//                    _logger.Warn($"获取配置加载锁超时（{_lockTimeout.TotalSeconds}s），返回当前缓存");
//                    return _currentConfiguration;
//                }

//                // 双重检查：获取锁后再次验证是否需要加载
//                var cachedConfig = GetCachedConfigurationIfValid();
//                if (cachedConfig != null || (_hasAttemptedLoad && _currentConfiguration == null &&
//                    DateTimeOffset.UtcNow - _lastSuccessfulLoadUtc < _cacheExpiration))
//                {
//                    return _currentConfiguration;
//                }

//                // 标记当前上下文正在加载
//                _isLoadingOnCurrentThread.Value = true;
//                _isLoading = true;

//                try
//                {
//                    if (isFirstLoad)
//                    {
//                        _logger.Info("首次获取MES配置，尝试从数据库加载");
//                    }

//                    _lastLoadAttemptUtc = DateTimeOffset.UtcNow;
//                    var config = await LoadConfigurationFromDatabaseCoreAsync();

//                    _hasAttemptedLoad = true;

//                    if (config != null)
//                    {
//                        _currentConfiguration = config;
//                        _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
//                        _logger.Info(isFirstLoad ? "首次加载MES配置成功" : "MES配置刷新成功");
//                    }
//                    else
//                    {
//                        // config为null可能是MES禁用，也记录成功时间以启用缓存
//                        _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
//                        _logger.Info(isFirstLoad ? "首次加载MES配置完成（无配置或已禁用）" : "MES配置刷新完成（无配置或已禁用）");
//                    }

//                    return config;
//                }
//                finally
//                {
//                    _isLoadingOnCurrentThread.Value = false;
//                    _isLoading = false;
//                }
//            }
//            finally
//            {
//                if (lockAcquired)
//                {
//                    _loadLock.Release();
//                }
//            }
//        }

//        public async Task<bool> ReloadConfigurationAsync()
//        {
//            // 检测重入
//            if (_isLoadingOnCurrentThread.Value)
//            {
//                _logger.Warn("检测到重入的ReloadConfigurationAsync调用，忽略");
//                return false;
//            }

//            bool lockAcquired = false;
//            try
//            {
//                lockAcquired = await _loadLock.WaitAsync(_lockTimeout);

//                if (!lockAcquired)
//                {
//                    _logger.Warn($"获取配置重载锁超时（{_lockTimeout.TotalSeconds}s）");
//                    return false;
//                }

//                _isLoadingOnCurrentThread.Value = true;
//                _isLoading = true;

//                try
//                {
//                    _logger.Info("强制重新加载MES配置");

//                    // 重置状态
//                    _lastLoadAttemptUtc = DateTimeOffset.MinValue;
//                    _hasAttemptedLoad = false;

//                    var config = await LoadConfigurationFromDatabaseCoreAsync();

//                    _hasAttemptedLoad = true;
//                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;

//                    if (config != null)
//                    {
//                        _currentConfiguration = config;
//                        _logger.Info("MES配置重新加载成功");
//                        return true;
//                    }
//                    else
//                    {
//                        _currentConfiguration = null;
//                        _logger.Warn("MES配置重新加载完成（无配置或已禁用）");
//                        return false;
//                    }
//                }
//                finally
//                {
//                    _isLoadingOnCurrentThread.Value = false;
//                    _isLoading = false;
//                }
//            }
//            finally
//            {
//                if (lockAcquired)
//                {
//                    _loadLock.Release();
//                }
//            }
//        }

//        /// <summary>
//        /// 核心加载逻辑，不包含锁管理
//        /// </summary>
//        private async Task<MesApiConfiguration?> LoadConfigurationFromDatabaseCoreAsync()
//        {
//            try
//            {
//                _logger.Debug("开始从数据库加载MES配置");

//                // 使用独立的scope进行数据库操作
//                using var scope = _serviceProvider.CreateScope();
//                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
//                var mesSettingRepo = scope.ServiceProvider.GetRequiredService<IMESSettingRepository>();

//                // 检查配置文件中的MES开关
//                bool mesEnabled;
//                try
//                {
//                    var mesEnabledValue = await globalSettingRepo.GetSettingValueAsync("IsMESEnabled");
//                    mesEnabled = mesEnabledValue ?? false;
//                    _logger.Debug($"IsMESEnabled 数据库值: {mesEnabled}");
//                }
//                catch (Exception ex)
//                {
//                    _logger.Error("获取IsMESEnabled设置失败", ex);
//                    return null;
//                }

//                if (!mesEnabled)
//                {
//                    _logger.Info("MES服务未开启");
//                    return null;
//                }

//                // 查询项目的MES Profile
//                dynamic? mesProfileSetting;
//                try
//                {
//                    mesProfileSetting = await globalSettingRepo.GetByNameAsync("MesProfile");
//                }
//                catch (Exception ex)
//                {
//                    _logger.Error("获取MesProfile设置失败", ex);
//                    return null;
//                }

//                if (mesProfileSetting == null || string.IsNullOrWhiteSpace(mesProfileSetting?.Value?.ToString()))
//                {
//                    _logger.Info("当前项目未配置MES Profile");
//                    return null;
//                }

//                var profileName = mesProfileSetting?.Value.ToString();
//                _logger.Info($"项目使用MES Profile: {profileName}");

//                // 加载MES详细配置
//                IEnumerable<dynamic>? mesSettings;
//                try
//                {
//                    mesSettings = await mesSettingRepo.GetByProfileNameAsync(profileName);
//                }
//                catch (Exception ex)
//                {
//                    _logger.Error($"获取Profile '{profileName}' 的MES配置失败", ex);
//                    return null;
//                }

//                if (mesSettings == null || !mesSettings.Any())
//                {
//                    _logger.Warn($"未找到Profile '{profileName}' 的MES配置");
//                    return null;
//                }

//                // 构建配置对象
//                var configuration = BuildMesConfiguration(profileName, mesSettings);

//                // 验证配置
//                if (!configuration.IsValid(out string? errorMessage))
//                {
//                    _logger.Error($"MES配置验证失败: {errorMessage}");
//                    return null;
//                }

//                _logger.Info($"MES配置加载成功: BaseUrl={configuration.BaseUrl}, " +
//                           $"Timeout={configuration.TimeoutSeconds}s, " +
//                           $"EnableRetry={configuration.EnableRetry}");

//                return configuration;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error("从数据库加载MES配置时发生错误", ex);
//                return null;
//            }
//        }




//        private async Task<MesApiConfiguration?> LoadConfigurationFromDatabaseAsync()
//        {
//            try
//            {
//                _logger.Debug("开始从数据库加载MES配置");

//                using var scope = _serviceProvider.CreateScope();
//                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
//                var mesSettingRepo = scope.ServiceProvider.GetRequiredService<IMESSettingRepository>();

//                // 检查配置文件中的MES开关
//                var mesEnabledValue = await globalSettingRepo.GetSettingValueAsync("IsMESEnabled");
//                var mesEnabled = mesEnabledValue ?? false;  // 如果为null，默认为false
//                _logger.Debug($"IsMESEnabled 数据库值: {mesEnabled}");

//                if (!mesEnabled)
//                {
//                    _logger.Info("MES服务未开启，将缓存禁用状态，防止重复加载");
//                    _currentConfiguration = null;
//                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
//                    return null;
//                }

//                // 查询项目的MES Profile
//                var mesProfileSetting = await globalSettingRepo.GetByNameAsync("MesProfile");

//                if (mesProfileSetting == null || string.IsNullOrWhiteSpace(mesProfileSetting.Value))
//                {
//                    _logger.Info($"当前项目未配置MES Profile");
//                    return null;
//                }

//                var profileName = mesProfileSetting.Value;
//                _logger.Info($"项目使用MES Profile: {profileName}");

//                // 加载MES详细配置
//                var mesSettings = await mesSettingRepo.GetByProfileNameAsync(profileName);
//                if (mesSettings == null || !mesSettings.Any())
//                {
//                    _logger.Warn($"未找到Profile '{profileName}' 的MES配置");
//                    return null;
//                }

//                // 构建配置对象
//                var configuration = BuildMesConfiguration(profileName, mesSettings);

//                // 验证配置
//                if (!configuration.IsValid(out var errorMessage))
//                {
//                    _logger.Error($"MES配置验证失败: {errorMessage}");
//                    return null;
//                }

//                // 7. 更新缓存
//                _currentConfiguration = configuration;
//                _lastSuccessfulLoadUtc = DateTime.Now;

//                _logger.Info($"MES配置加载成功: BaseUrl={configuration.BaseUrl}, " +
//                           $"Timeout={configuration.TimeoutSeconds}s, " +
//                           $"EnableRetry={configuration.EnableRetry}");

//                return configuration;
//            }
//            catch (Exception ex)
//            {
//                _logger.Error("从数据库加载MES配置时发生错误", ex);
//                return null;
//            }
//        }

//        private MesApiConfiguration BuildMesConfiguration(string profileName, IEnumerable<dynamic> settings)
//        {
//            var config = new MesApiConfiguration
//            {
//                ProfileName = profileName,
//                TimeoutSeconds = 30,  // 默认值
//                EnableRetry = true,    // 默认值
//                MaxRetryCount = 3      // 默认值
//            };

//            // 定义端点映射
//            var endpointMappings = new Dictionary<string, string>();

//            foreach (var setting in settings)
//            {
//                if (setting == null)
//                    continue;

//                // 安全获取Key和Value
//                string? keyStr = setting.Key?.ToString();
//                string? valueStr = setting.Value?.ToString();

//                // 如果Key或Value为空，跳过此配置
//                if (string.IsNullOrWhiteSpace(keyStr) || string.IsNullOrWhiteSpace(valueStr))
//                    continue;

//                var key = keyStr.ToLower();
//                var value = valueStr;

//                switch (key)
//                {
//                    case "baseurl":
//                        config.BaseUrl = value;
//                        break;

//                    case "timeout":
//                    case "timeoutseconds":
//                        if (int.TryParse(value, out int timeout))
//                            config.TimeoutSeconds = timeout;
//                        break;

//                    case "enableretry":
//                        if (bool.TryParse(value, out bool retry))
//                            config.EnableRetry = retry;
//                        break;

//                    case "maxretrycount":
//                        if (int.TryParse(value, out int retryCount))
//                            config.MaxRetryCount = retryCount;
//                        break;

//                    case "operatorid":
//                        config.OperatorId = value;
//                        break;

//                    case "groupcode":
//                        config.GroupCode = value;
//                        break;

//                    case "devicesn":
//                        config.DeviceSn = value;
//                        break;

//                    case "monumber":
//                        config.MoNumber = value;
//                        break;

//                    case "controlgroup":
//                        config.ControlGroup = value;
//                        break;

//                    default:
//                        // 检查是否是端点配置（以endpoint_开头）
//                        if (key.StartsWith("endpoint_"))
//                        {
//                            var endpointName = key.Substring("endpoint_".Length);
//                            endpointMappings[endpointName] = value;
//                        }
//                        // 检查是否是自定义头（以header_开头）
//                        else if (key.StartsWith("header_"))
//                        {
//                            var headerName = key.Substring("header_".Length);
//                            config.CustomHeaders[headerName] = value;
//                        }
//                        else
//                        {
//                            // 存储为自定义设置
//                            config.CustomSettings[setting.Key.ToString()] = value;
//                        }
//                        break;
//                }
//            }


//            // 设置端点映射
//            if (endpointMappings.Any())
//            {
//                config.Endpoints = endpointMappings;
//                _logger.Info($"加载了 {endpointMappings.Count} 个端点映射");
//            }

//            // 设置默认端点（如果数据库中没有配置）
//            if (!config.Endpoints.ContainsKey("OfflineDataUpload"))
//                config.Endpoints["OfflineDataUpload"] = "mes/OfflineDataUpload";
//            if (!config.Endpoints.ContainsKey("IncreaseMarking"))
//                config.Endpoints["IncreaseMarking"] = "mes/IncreaseMarking";


//            return config;
//        }

//        /// <summary>
//        /// 释放资源
//        /// </summary>
//        public void Dispose()
//        {
//            _loadLock?.Dispose();
//        }
//    }
//}

using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.MES.Providers
{
    public class MesConfigurationProvider : IMesConfigurationProvider, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerService<MesConfigurationProvider> _logger;
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _errorBackoff = TimeSpan.FromSeconds(30);

        private MesApiConfiguration? _cachedConfig;
        private bool _hasAttemptedLoad;
        private DateTimeOffset _lastSuccessfulLoadUtc = DateTimeOffset.MinValue;
        private DateTimeOffset _lastLoadAttemptUtc = DateTimeOffset.MinValue;

        public MesApiConfiguration? CurrentConfiguration => _cachedConfig;

        public MesConfigurationProvider(IServiceScopeFactory scopeFactory, ILoggerService<MesConfigurationProvider> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<MesApiConfiguration?> GetConfigurationAsync()
        {
            if (TryGetCached(out var config))
                return config;

            await _loadLock.WaitAsync();
            try
            {
                if (TryGetCached(out config))
                    return config;
                config = await LoadFromDatabaseAsync();
                return config;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private bool TryGetCached(out MesApiConfiguration? config)
        {
            config = _cachedConfig;
            if (!_hasAttemptedLoad) return false;
            var age = DateTimeOffset.UtcNow - _lastSuccessfulLoadUtc;
            if (age < _cacheExpiration) return true;
            return false;
        }

        private async Task<MesApiConfiguration?> LoadFromDatabaseAsync()
        {
            if (_hasAttemptedLoad && DateTimeOffset.UtcNow - _lastLoadAttemptUtc < _errorBackoff)
                return _cachedConfig;

            _lastLoadAttemptUtc = DateTimeOffset.UtcNow;
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var globalRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                var mesRepo = scope.ServiceProvider.GetRequiredService<IMESSettingRepository>();

                bool mesEnabled = await globalRepo.GetSettingValueAsync("IsMESEnabled") ?? false;
                if (!mesEnabled)
                {
                    _logger.Info("MES功能未启用");
                    _cachedConfig = null;
                    _hasAttemptedLoad = true;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                    return null;
                }

                var profileSetting = await globalRepo.GetByNameAsync("MesProfile");
                if (profileSetting == null || string.IsNullOrWhiteSpace(profileSetting.Value?.ToString()))
                {
                    _cachedConfig = null;
                    _hasAttemptedLoad = true;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                    return null;
                }

                var profileName = profileSetting.Value.ToString()!;
                var mesSettings = await mesRepo.GetByProfileNameAsync(profileName);
                if (mesSettings == null || !mesSettings.Any())
                {
                    _cachedConfig = null;
                    _hasAttemptedLoad = true;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                    return null;
                }

                var config = BuildMesConfiguration(profileName, mesSettings);
                if (!config.IsValid(out var error))
                {
                    _logger.Error($"配置验证失败: {error}");
                    _cachedConfig = null;
                    _hasAttemptedLoad = true;
                    _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                    return null;
                }

                _cachedConfig = config;
                _hasAttemptedLoad = true;
                _lastSuccessfulLoadUtc = DateTimeOffset.UtcNow;
                _logger.Info($"MES配置加载成功: {profileName}");
                return config;
            }
            catch (Exception ex)
            {
                _logger.Error("加载MES配置异常", ex);
                return _cachedConfig; // 保留旧值
            }
        }

        public async Task<bool> ReloadConfigurationAsync()
        {
            await _loadLock.WaitAsync();
            try
            {
                _lastLoadAttemptUtc = DateTimeOffset.MinValue; // 强制立即重试
                var config = await LoadFromDatabaseAsync();
                return config != null;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private MesApiConfiguration BuildMesConfiguration(string profileName, IEnumerable<dynamic> settings)
        {
            var config = new MesApiConfiguration
            {
                ProfileName = profileName,
                TimeoutSeconds = 30,
                EnableRetry = true,
                MaxRetryCount = 3
            };

            var endPoints = new Dictionary<string, string>();
            foreach (var setting in settings)
            {
                if (setting == null) continue;
                string? keyStr = setting.Key?.ToString();
                string? valueStr = setting.Value?.ToString();
                if (string.IsNullOrWhiteSpace(keyStr) || string.IsNullOrWhiteSpace(valueStr)) continue;

                var key = keyStr.ToLowerInvariant();
                switch (key)
                {
                    case "baseurl": config.BaseUrl = valueStr; break;
                    case "timeout": if (int.TryParse(valueStr, out var t)) config.TimeoutSeconds = t; break;
                    case "enableretry": if (bool.TryParse(valueStr, out var r)) config.EnableRetry = r; break;
                    case "maxretrycount": if (int.TryParse(valueStr, out var rc)) config.MaxRetryCount = rc; break;
                    case "operatorid": config.OperatorId = valueStr; break;
                    case "groupcode": config.GroupCode = valueStr; break;
                    case "devicesn": config.DeviceSn = valueStr; break;
                    case "monumber": config.MoNumber = valueStr; break;
                    case "controlgroup": config.ControlGroup = valueStr; break;
                    case "sntype": config.SnType = valueStr; break;
                    default:
                        if (key.StartsWith("endpoint_"))
                            endPoints[key["endpoint_".Length..]] = valueStr;
                        else if (key.StartsWith("header_"))
                            ((Dictionary<string, string>)config.CustomHeaders)[key["header_".Length..]] = valueStr;
                        else
                            ((Dictionary<string, string>)config.CustomSettings)[setting.Key.ToString()!] = valueStr;
                        break;
                }
            }            
            return config;
        }

        public void Dispose() => _loadLock.Dispose();
    }
}
