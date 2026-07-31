using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RemoteFileMonitor.Abstractions;

namespace SUNWODA_SEVB.Services.Project.JRRY
{
    public class ModelPreloader : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ModelPreloader> _logger;

        public ModelPreloader(IServiceProvider services, ILogger<ModelPreloader> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[JRRY] 正在预加载视觉模型...");
            // 强制解析单例，此时会执行 ImageProcessor 的构造函数，初始化模型
            _ = _services.GetRequiredService<IFileProcessor>();
            _logger.LogInformation("[JRRY] 视觉模型预加载完成");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}