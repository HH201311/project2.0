using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Web;
using SUNWODA_SEVB.Core.Models.Web;

namespace SUNWODA_SEVB.WEB.Services
{
    public class WebConfiguration : IWebConfiguration
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<WebConfiguration> _logger;
        private WebSettings _settings;
        private readonly object _lock = new object();

        public WebConfiguration(
            IServiceProvider serviceProvider,
            ILoggerService<WebConfiguration> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _settings = new WebSettings();
        }

        public WebSettings GetSettings()
        {
            lock (_lock)
            {
                return _settings;
            }
        }

        public async Task<bool> IsWebEnabledAsync()
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                    return await globalSettingRepo.GetSettingValueAsync<bool>("IsUseWeb", false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("检查WEB服务启用状态失败", ex);
                return false;
            }
        }

        public async Task ReloadAsync()
        {
            try
            {
                _logger.Info("开始加载WEB配置");

                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();

                    var newSettings = new WebSettings();

                    // 从GlobalSetting表获取配置
                    newSettings.IsWebEnabled = await globalSettingRepo.GetSettingValueAsync<bool>("isUseWEB", false);
                    newSettings.HeartbeatIntervalSeconds = 10;
                    newSettings.DeviceBindingRetrySeconds =  5;
                    newSettings.PcInfoUploadIntervalMinutes = 10;
                    newSettings.VersionCheckIntervalSeconds = 60;
                    newSettings.EnableHeartbeat = await globalSettingRepo.GetSettingValueAsync<bool>("isUseWEB", false);
                    newSettings.EnableVersionCheck = await globalSettingRepo.GetSettingValueAsync<bool>("isUseWEB", false);
                    newSettings.EnablePcInfoUpload = await globalSettingRepo.GetSettingValueAsync<bool>("isUseWEB", false);

                    newSettings.CentralControlWebUrl = await globalSettingRepo.GetSettingValueAsync<string>("CentralControlWEBUrl", string.Empty);

                    newSettings.DeviceSn = await globalSettingRepo.GetSettingValueAsync<string>("DeviceSn");
                    newSettings.DeviceName = await globalSettingRepo.GetSettingValueAsync<string>("DeviceName");
                    newSettings.Workship = await globalSettingRepo.GetSettingValueAsync<string>("workship");
                    newSettings.SoftVersion = await globalSettingRepo.GetSettingValueAsync<string>("LabVersion");
                    newSettings.UUID = await globalSettingRepo.GetSettingValueAsync<string>("UUID");

                    newSettings.SqlPath = await globalSettingRepo.GetSettingValueAsync<string>("SqlPath");
                    newSettings.IpHeader = await globalSettingRepo.GetSettingValueAsync<string>("IPHeader") ?? "10.";

                    lock (_lock)
                    {
                        _settings = newSettings;
                    }

                    _logger.Info($"WEB配置加载成功 - 启用状态: {newSettings.IsWebEnabled}, " +
                               $"心跳间隔: {newSettings.HeartbeatIntervalSeconds}秒, " +
                               $"版本检查间隔: {newSettings.VersionCheckIntervalSeconds}秒");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("加载WEB配置失败", ex);
                throw;
            }
        }

        public async Task UpdateUuidAsync(string uuid)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var globalSettingRepo = scope.ServiceProvider.GetRequiredService<IGlobalSettingRepository>();
                    await globalSettingRepo.UpdateSettingValueAsync("UUID", uuid);

                    lock (_lock)
                    {
                        _settings.UUID = uuid;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"更新UUID失败: {uuid}", ex);
                throw;
            }
        }
    }
}
