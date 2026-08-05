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
                EnableRetry = false,
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
