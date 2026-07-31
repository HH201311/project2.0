using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Enumerations.Project.JRCW;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Data;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer;
using SUNWODA_SEVB.Core.Interfaces.Project.JECW;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Services.Project;
using SUNWODA_SEVB.Services.Project.CCTB;
using SUNWODA_SEVB.Services.Project.CTAutoSlicer;
using SUNWODA_SEVB.Services.Project.JRCW.Business;
using SUNWODA_SEVB.Tool.Services.Mqtt;
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

            // 添加CCTB项目服务
            services.AddCCTBServices();

            // 添加JRCW项目服务
            services.AddJRCWServices();

            // 添加 CT 自动化切片服务
            services.AddCTAutoSlicerServices();

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

            // 注册工厂，供 Manager 创建多个独立实例
            services.AddSingleton<Func<ISocketServer>>(sp => () =>
            {
                var logger = sp.GetRequiredService<ILoggerService<SocketServer>>();
                var handler = sp.GetRequiredService<IMessageHandler>();
                var loader = sp.GetRequiredService<ISocketOptionsLoader>();
                var options = sp.GetRequiredService<IOptions<SocketServerOptions>>();
                return new SocketServer(logger, handler, loader, options);
            });

            // 注册管理器
            services.AddSingleton<ISocketServerManager, SocketServerManager>();

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

        /// <summary>
        /// 添加CCTB项目服务
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddCCTBServices(this IServiceCollection services)
        {
            // 注册 算法服务
            services.AddSingleton<IAlgorithmService, AlgorithmService>();
            // 注册 通讯服务
            services.AddSingleton<ICommunicationService, CommunicationService>();
            // 注册后台服务
            services.AddHostedService<CommunicationMonitorService>();

            // 注册 SocketClient 相关服务
            services.AddScoped<ISocketClient, SocketClient>();
            services.AddSingleton<ISocketClientFactory, SocketClientFactory>();

            // 注册 MqttClient 相关服务
            // 配置 MqttClient 默认选项
            services.Configure<CustomMqttClientOptions>(options =>
            {
                options.ModuleName = "MqttClient";
                options.IpAddress = "lacalhost";
                options.Port = 1883;
                options.ClientId = "CCTBClient";
                options.ConnectTimeout = 5000;
                options.UserName = "mqtt_user";
                options.Password = "mqtt_password";
                options.KeepAlivePeriod = 10000;
                options.KeepAliveSendInterval = 10000;
            });
            services.AddSingleton<ICustomMqttClient, CustomMqttClient>();
            services.AddSingleton<ICustomMqttOptionsLoader, CustomMqttOptionsLoader>();
            services.AddSingleton<ICustomMqttClientFactory, CustomMqttClientFactory>();

            return services;
        }

        /// <summary>
        /// 添加JRCW项目服务
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddJRCWServices(this IServiceCollection services)
        {
            // 注册具体业务类型
            services.AddSingleton<YWC05BusinessService>();

            // 注册接口映射
            services.AddSingleton<IBusinessService>(sp => sp.GetRequiredService<YWC05BusinessService>());

            // 注册工厂和管理器
            services.AddSingleton<BusinessServiceFactory>();
            services.AddSingleton<BusinessServiceManager>();

            return services;
        }

        /// <summary>
        /// 添加 CT 自动化切片项目服务
        /// </summary>
        public static IServiceCollection AddCTAutoSlicerServices(this IServiceCollection services)
        {
            services.AddSingleton<VGAutomationService>();
            services.AddSingleton<IAngleAlgorithmService, AngleAlgorithmService>();
            services.AddSingleton<ICutImgAlgorithmService, CutImgAlgorithmService>();
            services.AddSingleton<ICTOfflineVisionService, OfflineCtVisionService>();
            services.AddSingleton<ICTAutoSlicerService, CTAutoSlicerService>();
            services.AddSingleton<CTFolderMonitorService>();
            services.AddSingleton<CTAutoSlicerInputHookService>();
            services.AddSingleton<ProcessMonitorService>();
            services.AddSingleton<CTAutoSlicerLocalConfigService>();
            return services;
        }
    }
}
