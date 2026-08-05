using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Services.Mqtt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.Tool.Services.Socket
{
    /// <summary>
    /// Socket 客户端工厂实现
    /// </summary>
    public class SocketClientFactory : ISocketClientFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public SocketClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ISocketClient CreateClient()
        {
            var logger = _serviceProvider.GetRequiredService<ILoggerService<SocketClient>>();
            var optionsLoader = _serviceProvider.GetRequiredService<ISocketOptionsLoader>();
            var defaultOptions = _serviceProvider.GetRequiredService<IOptions<SocketClientOptions>>();
            var client = new SocketClient(logger, optionsLoader, defaultOptions);
            return client;
        }
    }
}
