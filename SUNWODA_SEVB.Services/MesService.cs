using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.MES.Client;

namespace SUNWODA_SEVB.MES
{
    /// <summary>
    /// MES主服务实现
    /// </summary>
    public class MesService : IMesService
    {
        private readonly IConfiguration _configuration;
        private readonly ILoggerService<MesService> _logger;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly IMesHttpClientManager _httpClientManager;
        private readonly IServiceProvider _serviceProvider;

        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized;
        private bool _isEnabled;

        public string ServiceName => "MesService";
        public bool IsEnabled => _isEnabled && _isInitialized;

        public MesService(
            IConfiguration configuration,
            ILoggerService<MesService> logger,
            IMesConfigurationProvider configProvider,
            IMesHttpClientManager httpClientManager,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _httpClientManager = httpClientManager ?? throw new ArgumentNullException(nameof(httpClientManager));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<bool> InitializeAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    _logger.Debug("MES服务已经初始化");
                    return IsEnabled;
                }

                _logger.Info("========== 开始初始化MES服务 ==========");

                // 1. 检查数据库中的MES启用状态
                var enabledInDatabase = await CheckMesEnabledInDatabase();
                if (!enabledInDatabase)
                {
                    _logger.Info("MES服务在数据库配置中已禁用 (EnableMES = false)");
                    _isEnabled = false;
                    _isInitialized = true;
                    return false;
                }

                // 2. 加载MES配置
                var mesConfig = await _configProvider.GetConfigurationAsync();
                if (mesConfig == null)
                {
                    _logger.Warn("未能加载MES配置，服务将被禁用");
                    _isEnabled = false;
                    _isInitialized = true;
                    return false;
                }

                // 3. 初始化HTTP客户端
                InitializeHttpClient(mesConfig);

                _isEnabled = true;
                _isInitialized = true;

                _logger.Info($"========== MES服务初始化完成 (启用状态: {IsEnabled}) ==========");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("MES服务初始化失败", ex);
                _isEnabled = false;
                _isInitialized = true;
                return false;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// 检查数据库中的MES启用状态
        /// </summary>
        private async Task<bool> CheckMesEnabledInDatabase()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                var isEnabled = await globalSettingRepo.GetSettingValueAsync("IsMESEnabled") ?? false;             

                return isEnabled;
            }
            catch (Exception ex)
            {
                _logger.Error($"检查数据库MES启用状态时发生错误: {ex.Message}", ex);
                // 发生错误时默认启用，避免阻止MES服务
                return true;
            }
        }

        private void InitializeHttpClient(MesApiConfiguration config)
        {
            _httpClientManager.ConfigureClient("MES", client =>
            {
                client.BaseAddress = new Uri(config.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("User-Agent", "SUNWODA-SEVB-MES/1.0");

                // 添加自定义头
                foreach (var header in config.CustomHeaders)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            });

            _logger.Info($"HTTP客户端初始化完成: BaseUrl={config.BaseUrl}");
        }

        public async Task ResetAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                _logger.Info("正在重置MES服务...");

                _isInitialized = false;
                _isEnabled = false;

                // 重新加载配置
                await _configProvider.ReloadConfigurationAsync();

                _logger.Info("MES服务重置完成");
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// 动态更新MES启用状态
        /// </summary>
        public async Task<bool> UpdateEnabledStatusAsync()
        {
            await _initLock.WaitAsync();
            try
            {
                var enabledInDatabase = await CheckMesEnabledInDatabase();

                if (enabledInDatabase && !_isEnabled && _isInitialized)
                {
                    // 如果数据库中启用了MES，但当前服务未启用，则重新初始化
                    _logger.Info("检测到MES已在数据库中启用，正在重新初始化...");
                    _isInitialized = false;
                    return await InitializeAsync();
                }
                else if (!enabledInDatabase && _isEnabled)
                {
                    // 如果数据库中禁用了MES，但当前服务已启用，则禁用服务
                    _logger.Info("检测到MES已在数据库中禁用，正在停止服务...");
                    _isEnabled = false;
                }

                return _isEnabled;
            }
            finally
            {
                _initLock.Release();
            }
        }

        //public string GetVersion()
        //{
        //    return "1.0.0";
        //}
    }
}