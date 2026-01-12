using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Services.Project;
using SUNWODA_SEVB.Tool.Services.Socket;
using SUNWODA_SEVB.WEB.Services.STC;

namespace SUNWODA_SEVB.Services
{
    /// <summary>
    /// 项目服务统一注册扩展
    /// </summary>
    public static class ProjectServiceCollectionExtensions
    {
        /// <summary>
        /// 添加所有项目相关服务
        /// </summary>
        public static IServiceCollection AddProjectServices(this IServiceCollection services)
        {
            // 添加螺丝位置配置服务
            services.AddScrewPositionConfigService();

            // 添加Socket服务
            services.AddProjectSocketServices();

            // 添加螺丝控制器服务
            services.AddScrewServices();

            return services;
        }
        /// <summary>
        /// 添加螺丝位置配置服务
        /// </summary>
        public static IServiceCollection AddScrewPositionConfigService(this IServiceCollection services)
        {
            services.AddSingleton<IScrewPositionConfigService, ScrewPositionConfigService>();
            return services;
        }

        /// <summary>
        /// 添加项目Socket服务
        /// </summary>
        public static IServiceCollection AddProjectSocketServices(this IServiceCollection services)
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

            return services;
        }

        public static IServiceCollection AddScrewServices(this IServiceCollection services)
        {
            // 注册HttpClient
            services.AddHttpClient("ScrewController", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            // 注册螺丝控制器服务（单例）
            services.AddSingleton<IScrewControllerService, ScrewControllerService>();

            return services;
        }
    }
}
