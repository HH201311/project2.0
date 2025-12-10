using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Tool.Services.Socket;

namespace SUNWODA_SEVB.WEB
{
    public static class SocketServiceCollectionExtensions
    {
        public static IServiceCollection AddSocketServices(this IServiceCollection services)
        {
            // 配置Socket默认选项
            services.Configure<SocketServerOptions>(options =>
            {
                options.ModuleName = "SocketServer";
                options.MaxClients = 10;
                options.ReceiveBufferSize = 8192;
                options.DefaultPort = 8080;
                options.Backlog = 100;
            });

            // 注册配置加载器
            services.AddSingleton<ISocketOptionsLoader, SocketOptionsLoader>();
            // 注册服务
            services.AddSingleton<IMessageHandler, DefaultMessageHandler>();
            services.AddSingleton<ISocketServer, SocketServer>();

            // 如果有需要，可以注册托管服务
            // services.AddHostedService<SocketHostedService>();

            return services;
        }
    }
}
