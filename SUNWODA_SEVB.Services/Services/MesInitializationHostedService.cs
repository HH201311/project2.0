using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;

namespace SUNWODA_SEVB.MES.Services
{
    /// <summary>
    /// MES服务初始化托管服务
    /// </summary>
    public class MesInitializationHostedService : IHostedService
    {
        private readonly IMesService _mesService;
        private readonly IMesConfigurationProvider _configProvider;
        private readonly ILoggerService<MesInitializationHostedService> _logger;
        private Timer? _statusCheckTimer;

        public MesInitializationHostedService(
            IMesService mesService,
            IMesConfigurationProvider configProvider,
            ILoggerService<MesInitializationHostedService> logger)
        {
            _mesService = mesService ?? throw new ArgumentNullException(nameof(mesService));
            _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("========== MES初始化服务启动 ==========");

                // 1. 预加载MES配置（确保配置提供者初始化）
                var config = await _configProvider.GetConfigurationAsync();
                if (config != null)
                {
                    _logger.Info($"成功预加载MES配置: Profile={config.ProfileName}");
                }
                else
                {
                    _logger.Info("未找到有效的MES配置或MES服务已禁用");
                }

                // 2. 初始化主MES服务
                var result = await _mesService.InitializeAsync();
                if (result)
                {
                    _logger.Info($"MES主服务初始化成功 (启用状态: {_mesService.IsEnabled})");
                }
                else
                {
                    _logger.Warn("MES主服务初始化失败或未启用");
                }

                _logger.Info("========== MES初始化服务完成 ==========");
            }
            catch (Exception ex)
            {
                _logger.Error("MES初始化服务启动失败", ex);
                // 不抛出异常，避免影响应用程序启动
            }
        }

     
      

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Info("MES服务托管服务正在停止...");

            _statusCheckTimer?.Dispose();
            _statusCheckTimer = null;

            return Task.CompletedTask;
        }
    }
}
