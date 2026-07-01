using HslCommunication.MQTT;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Models.Data;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Project.CCTB.SettingModels;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Mqtt
{
    /// <summary>
    /// MQTT 选项加载器
    /// </summary>
    public class CustomMqttOptionsLoader : ICustomMqttOptionsLoader
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<CustomMqttOptionsLoader> _logger;
        private readonly IProjectSettingRepository _projectSettingRepository;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public CustomMqttOptionsLoader(
            IServiceProvider serviceProvider,
            ILoggerService<CustomMqttOptionsLoader> logger,
            IProjectSettingRepository projectSettingRepository)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _projectSettingRepository = projectSettingRepository;
        }

        /// <summary>
        /// 加载指定模块的 MQTT 连接选项
        /// </summary>
        /// <param name="moduleName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<CustomMqttClientOptions> LoadMqttClientOptionsAsync(string moduleName)
        {
            var options = new CustomMqttClientOptions();

            try
            {
                InitializeMqttOptions(moduleName);

                using var scope = _serviceProvider.CreateScope();
                var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
                if (settingRepo == null) throw new Exception("项目设置存储库未找到");

                var optionsJson = await settingRepo.GetSettingValueAsync("MqttClientOptions", moduleName);
                if (!string.IsNullOrEmpty(optionsJson))
                {
                    try
                    {
                        var parsedOptions = JsonSerializer.Deserialize<CustomMqttClientOptions>(optionsJson, JsonOptions);
                        if (parsedOptions != null) options = parsedOptions;
                        else _logger.Error($"未找到 {moduleName} 的 MQTT 客户端配置，使用默认配置");
                    }
                    catch (Exception)
                    {
                        _logger.Error($"{moduleName} 的 MQTT 客户端配置解析异常，使用默认配置");
                    }
                }

                _logger.Info($"从数据库加载 MQTT 客户端配置成功 [{moduleName}]: " +
                    $"IP={options.IpAddress}, Port={options.Port}, " +
                    $"ClientId={options.ClientId}, " +
                    $"ConnectTimeout={options.ConnectTimeout}ms, " +
                    $"KeepAlivePeriod={options.KeepAlivePeriod}s, " +
                    $"KeepAliveSendInterval={options.KeepAliveSendInterval}s, " +
                    $"UserName={options.UserName}, " +
                    $"PublishTopics=[{string.Join(", ", options.PublishTopics?.Select(t => t.Topic) ?? Array.Empty<string>())}], " +
                    $"SubscribeTopics=[{string.Join(", ", options.SubscribeTopics?.Select(t => t.Topic) ?? Array.Empty<string>())}]", true);
            }
            catch (Exception ex)
            {
                _logger.Warn($"从数据库加载配置失败，使用默认值: {ex.Message}");
            }

            return options;
        }

        /// <summary>
        /// 更新 Mqtt 客户端配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateMqttClientOptionsAsync(string moduleName, CustomMqttClientOptions options)
        {
            if (options == null) return false;
            using var scope = _serviceProvider.CreateScope();
            var settingRepo = scope.ServiceProvider.GetRequiredService<IProjectSettingRepository>();
            if (settingRepo == null) throw new Exception("项目设置存储库未找到");

            var paramName = "MqttClientOptions";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new CustomMqttClientOptions();
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Mqtt 客户端参数信息"
                    }
                );
            }
            return await settingRepo.UpdateSettingValueAsync(paramName, moduleName, JsonSerializer.Serialize(options, JsonOptions));
        }

        /// <summary>
        /// 初始化moduleName对应的Mqtt配置
        /// </summary>
        /// <param name="moduleName"></param>
        /// <exception cref="Exception"></exception>
        private void InitializeMqttOptions(string moduleName)
        {
            var paramName = "MqttClientOptions";
            if (!_projectSettingRepository.Exists(model => model.Name == paramName && model.BelongToVM == moduleName))
            {
                var model = new CustomMqttClientOptions();
                _projectSettingRepository.Add(
                    new ProjectSettingModel(paramName, moduleName, JsonSerializer.Serialize(model, JsonOptions), "string", 2)
                    {
                        Remark = "Mqtt 客户端参数信息"
                    }
                );
            }
        }
    }
}
