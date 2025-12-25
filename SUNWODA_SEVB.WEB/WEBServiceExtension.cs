using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.WEB.Services;

namespace SUNWODA_SEVB.WEB
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加WEB服务并自动初始化
        /// </summary>
        public static IServiceCollection AddWebServices(this IServiceCollection services)
        {
            // 注册配置服务
            services.AddSingleton<IWebConfiguration, WebConfiguration>();

            // 注册API客户端
            services.AddSingleton<IWebApiClient, WebApiClient>();

            // 注册状态管理服务
            services.AddSingleton<WebStateService>();

            // 注册后台任务服务
            services.AddSingleton<BackgroundTaskService>();

            // 注册主服务
            services.AddSingleton<WebService>();
            services.AddSingleton<IWebService>(provider => provider.GetRequiredService<WebService>());

            // 注册托管服务，自动启动WEB服务
            services.AddHostedService<WebHostedService>();

            return services;
        }
    }

    /// <summary>
    /// WEB托管服务，用于自动初始化和启动
    /// </summary>
    internal class WebHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<WebHostedService> _logger;
        private IWebService? _webService;

        public WebHostedService(
            IServiceProvider serviceProvider,
            ILoggerService<WebHostedService> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("开始初始化WEB托管服务");

                // 获取WEB服务
                _webService = _serviceProvider.GetRequiredService<IWebService>();

                // 获取配置服务
                var configuration = _serviceProvider.GetRequiredService<IWebConfiguration>();

                // 检查是否启用WEB服务
                if (!await configuration.IsWebEnabledAsync())
                {
                    _logger.Info("WEB服务未启用，跳过初始化");
                    return;
                }

                // 加载配置
                await configuration.ReloadAsync();

                // 启动服务
                await _webService.StartAsync();

                _logger.Info("WEB托管服务初始化完成");
            }
            catch (Exception ex)
            {
                _logger.Error("WEB托管服务初始化失败", ex);                
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("停止WEB托管服务");

                if (_webService != null)
                {
                    await _webService.StopAsync();
                }

                _logger.Info("WEB托管服务已停止");
            }
            catch (Exception ex)
            {
                _logger.Error("停止WEB托管服务失败", ex);
            }
        }
    }
}
