using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;

namespace SUNWODA_SEVB.MES
{
    /// <summary>
    /// MES主服务实现
    /// </summary>
    public class MesService : IMesService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly ILoggerService<MesService> _logger;
        private volatile bool _isEnabled;
        private volatile bool _isInitialized;

        public string ServiceName => "MesService";
        public bool IsEnabled => _isEnabled && _isInitialized;

        public MesService(
            IServiceScopeFactory scopeFactory,
            IMesConfigurationProvider configProvider,
            ILoggerService<MesService> logger)
        {
            _scopeFactory = scopeFactory;
            _configProvider = configProvider;
            _logger = logger;
        }

        /// <summary>
        /// 初始化服务，加载配置并设置启用状态
        /// </summary>
        /// <returns></returns>
        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
                return IsEnabled;

            var config = await _configProvider.GetConfigurationAsync();
            _isEnabled = config != null;
            _isInitialized = true;
            _logger.Info($"MesService 初始化：{_isEnabled}");
            return _isEnabled;
        }

        /// <summary>
        /// 重置服务状态，通常在配置变更后调用
        /// </summary>
        /// <returns></returns>
        public Task ResetAsync()
        {
            _isInitialized = false;
            _isEnabled = false;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 动态更新MES启用状态
        /// </summary>
        /// <returns></returns>
        public async Task<bool> UpdateEnabledStatusAsync()
        {
            var config = await _configProvider.GetConfigurationAsync();
            _isEnabled = config != null;
            return _isEnabled;
        }
    }
}