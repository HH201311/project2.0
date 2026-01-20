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
                    var projectSettingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();

                    var newSettings = new WebSettings();

                    // 从GlobalSetting表获取配置
                    newSettings.IsWebEnabled = await globalSettingRepo.GetSettingValueAsync<bool>("isUseWEB", false);
                    newSettings.HeartbeatIntervalSeconds = 10;
                    newSettings.DeviceBindingRetrySeconds =  5;
                    newSettings.PcInfoUploadIntervalMinutes = 10;
                    newSettings.VersionCheckIntervalSeconds = 60;
                    newSettings.EnableHeartbeat = await globalSettingRepo.GetSettingValueAsync<bool>("isEnableHeartbeat", false);
                    newSettings.EnableMachineErrorAlert = await globalSettingRepo.GetSettingValueAsync<bool>("isEnableMachineErrorLog", false);
                    newSettings.EnableVersionCheck = await globalSettingRepo.GetSettingValueAsync<bool>("isEnableVersionCheck", false);
                    newSettings.EnablePcInfoUpload = await globalSettingRepo.GetSettingValueAsync<bool>("isEnablePcInfoUpload", false);

                    newSettings.WebTestUrlSysCenter = await globalSettingRepo.GetSettingValueAsync<string>("WebTestUrlSysCenter", string.Empty);
                    newSettings.CentralControlWebUrl = await globalSettingRepo.GetSettingValueAsync<string>("CentralControlWEBUrl", string.Empty);
                    newSettings.WebPushApiUrl = await globalSettingRepo.GetSettingValueAsync<string>("WebPushApiUrl", string.Empty);

                    newSettings.DeviceSn = await globalSettingRepo.GetSettingValueAsync<string>("DeviceSn");
                    newSettings.DeviceName = await globalSettingRepo.GetSettingValueAsync<string>("DeviceName");
                    newSettings.Workship = await globalSettingRepo.GetSettingValueAsync<string>("workship");
                    newSettings.SoftVersion = await globalSettingRepo.GetSettingValueAsync<string>("LabVersion");
                    newSettings.UUID = await globalSettingRepo.GetSettingValueAsync<string>("UUID");

                    newSettings.SqlPath = await globalSettingRepo.GetSettingValueAsync<string>("SqlPath");
                    newSettings.IpHeader = await globalSettingRepo.GetSettingValueAsync<string>("IPHeader") ?? "10.";

                    //项目参数配置
                    var vmName = await globalSettingRepo.GetSettingValueAsync<string>("CurrentVMName", "Default");
                    newSettings.EnableWebPush = await projectSettingRepo.GetSettingValueAsync("EnableWebPush", vmName)?? false;
                    newSettings.PushSystemTypeId = await projectSettingRepo.GetSettingValueAsync("PushSystemTypeId",vmName)??31;
                    newSettings.PushUserId = await projectSettingRepo.GetSettingValueAsync("PushUserId",vmName)?? "oc_713fbc8ebd0af75235e06c93346c4752";
                    newSettings.PushAppId = await projectSettingRepo.GetSettingValueAsync("PushAppId",vmName)?? "cli_a68c3b89643bd00c";
                    newSettings.PushSystemType = await projectSettingRepo.GetSettingValueAsync("PushSystemType",vmName)?? "12";
                    newSettings.PushRetryCount = await projectSettingRepo.GetSettingValueAsync("PushRetryCount",vmName)?? 3;
                    newSettings.PushRetryDelayMs = await projectSettingRepo.GetSettingValueAsync("PushRetryDelayMs",vmName)?? 1000;

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
