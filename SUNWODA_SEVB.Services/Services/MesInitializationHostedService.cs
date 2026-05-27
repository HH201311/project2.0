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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerService<MesInitializationHostedService> _logger;
        private Timer? _healthCheckTimer;

        public bool IsInitialized { get; private set; }
        public string? StatusMessage { get; private set; }

        public MesInitializationHostedService(IServiceScopeFactory scopeFactory, ILoggerService<MesInitializationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("MES初始化托管服务启动...");
                await InitializeMesServicesAsync(cancellationToken);
                if (IsInitialized)
                {
                    _healthCheckTimer = new Timer(_ => CheckHealth(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
                }
                _logger.Info($"MES初始化完成：{StatusMessage}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"初始化异常: {ex.Message}";
                _logger.Error("MES初始化失败", ex);
            }
        }

        private async Task InitializeMesServicesAsync(CancellationToken cancellationToken, int maxRetries = 3)
        {
            for (int i = 1; i <= maxRetries; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                var configProvider = scope.ServiceProvider.GetRequiredService<IMesConfigurationProvider>();
                var config = await configProvider.GetConfigurationAsync();
                if (config == null)
                {
                    StatusMessage = "MES未启用或无配置";
                    _logger.Info(StatusMessage);
                    return;
                }

                // 初始化所有 IMesService 实现
                var services = scope.ServiceProvider.GetServices<IMesService>();
                bool allOk = true;
                foreach (var svc in services)
                {
                    var result = await svc.InitializeAsync();
                    allOk &= result;
                }

                if (allOk)
                {
                    IsInitialized = true;
                    StatusMessage = "所有MES服务初始化成功";
                    _logger.Info(StatusMessage);
                    return;
                }

                StatusMessage = $"初始化失败，重试 {i}/{maxRetries}";
                _logger.Warn(StatusMessage);
                if (i < maxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)), cancellationToken);
            }
            StatusMessage = "初始化达到最大重试次数";
            _logger.Error(StatusMessage);
        }

        private void CheckHealth()
        {
            _logger.Debug($"MES健康检查：已初始化={IsInitialized}，状态={StatusMessage}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _healthCheckTimer?.Dispose();
            return Task.CompletedTask;
        }
    }
}