using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Socket;

namespace SUNWODA_SEVB.Core.Models.Socket
{
    /// <summary>
    /// Socket配置加载器实现
    /// </summary>
    public class SocketOptionsLoader : ISocketOptionsLoader
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<SocketOptionsLoader> _logger;

        public SocketOptionsLoader(
            IServiceProvider serviceProvider,
            ILoggerService<SocketOptionsLoader> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<SocketServerOptions> LoadOptionsAsync(string moduleName)
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
                options.ReceiveTimeout = timeoutMs > 0 ? TimeSpan.FromMilliseconds(timeoutMs) : null;

                _logger.Info($"从数据库加载Socket配置成功 [{moduleName}]: Port={options.DefaultPort}, MaxClients={options.MaxClients}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"从数据库加载配置失败，使用默认值: {ex.Message}");
            }

            return options;
        }

        private async Task<int> GetIntSettingAsync(
            IProjectSettingRepository repo,
            string key,
            string moduleName,
            int defaultValue)
        {
            try
            {
                var value = await repo.GetSettingValueAsync(key, moduleName);
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
    }
}
