using HslCommunication.MQTT;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Mqtt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Mqtt
{
    /// <summary>
    /// MQTT 客户端工厂实现
    /// </summary>
    public class CustomMqttClientFactory : ICustomMqttClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerService<CustomMqttClientFactory> _logger;
        private readonly ICustomMqttOptionsLoader _mqttOptionsLoader;

        /// <summary>
        /// MQTT 客户端工厂实现
        /// </summary>
        /// <param name="serviceProvider"></param>
        /// <param name="logger"></param>
        /// <param name="mqttOptionsLoader"></param>
        public CustomMqttClientFactory(
            IServiceProvider serviceProvider,
            ILoggerService<CustomMqttClientFactory> logger,
            ICustomMqttOptionsLoader mqttOptionsLoader)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mqttOptionsLoader = mqttOptionsLoader;
        }

        /// <summary>
        /// 创建 MQTT 客户端实例
        /// </summary>
        /// <returns></returns>
        public ICustomMqttClient CreateClient()
        {
            var logger = _serviceProvider.GetRequiredService<ILoggerService<CustomMqttClient>>();
            var optionsLoader = _serviceProvider.GetRequiredService<ICustomMqttOptionsLoader>();
            var defaultOptions = _serviceProvider.GetRequiredService<IOptions<CustomMqttClientOptions>>();
            var client = new CustomMqttClient(logger, optionsLoader, defaultOptions);
            return client;
        }
    }
}
