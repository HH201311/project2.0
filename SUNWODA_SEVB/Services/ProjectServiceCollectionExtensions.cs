using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Mqtt;
using SUNWODA_SEVB.Core.Interfaces.Project;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB;
using SUNWODA_SEVB.Core.Interfaces.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Interfaces.Socket;
using SUNWODA_SEVB.Core.Models.Mqtt;
using SUNWODA_SEVB.Core.Models.Project.CCTB.Core;
using SUNWODA_SEVB.Core.Models.Socket;
using SUNWODA_SEVB.Services.Project;
using SUNWODA_SEVB.Services.Project.CCTB;
using SUNWODA_SEVB.Services.Project.CCTB.Business;
using SUNWODA_SEVB.Services.Project.JRCW.Business;
using SUNWODA_SEVB.Services.Project.JROH.Business;
using SUNWODA_SEVB.Tool.Services.Mqtt;
using SUNWODA_SEVB.Tool.Services.Socket;
using SUNWODA_SEVB.WEB.Services.STC;
using ICCTBBusinessService = SUNWODA_SEVB.Core.Interfaces.Project.CCTB.IBusinessService;
using CCTBBusinessServiceFactory = SUNWODA_SEVB.Services.Project.CCTB.Business.BusinessServiceFactory;
using CCTBBusinessServiceManager = SUNWODA_SEVB.Services.Project.CCTB.Business.BusinessServiceManager;
using IJRCWBusinessService = SUNWODA_SEVB.Core.Interfaces.Project.JRCW.IBusinessService;
using JRCWBusinessServiceFactory = SUNWODA_SEVB.Services.Project.JRCW.Business.BusinessServiceFactory;
using JRCWBusinessServiceManager = SUNWODA_SEVB.Services.Project.JRCW.Business.BusinessServiceManager;
using IJROHBusinessService = SUNWODA_SEVB.Core.Interfaces.Project.JROH.IBusinessService;
using JROHBusinessServiceFactory = SUNWODA_SEVB.Services.Project.JROH.Business.BusinessServiceFactory;
using JROHBusinessServiceManager = SUNWODA_SEVB.Services.Project.JROH.Business.BusinessServiceManager;

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

            // 添加JROH项目服务
            services.AddJROHServices();

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
            // 注册 界面全局上下文初始化服务
            services.AddSingleton<IGlobalContextInitializer, GlobalContextInitializer>();

            // 注册 算法服务
            services.AddSingleton<IAlgorithmService, AlgorithmService>();

            #region 设备/通讯
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
            #endregion

            #region 业务/逻辑
            // 注册具体业务类型
            services.AddSingleton<BackBusinessService>();
            services.AddSingleton<MDBusinessService>();
            services.AddSingleton<TDBusinessService>();
            services.AddSingleton<MDTDBusinessService>();

            // 注册接口映射
            services.AddSingleton<ICCTBBusinessService>(sp => sp.GetRequiredService<BackBusinessService>());
            services.AddSingleton<ICCTBBusinessService>(sp => sp.GetRequiredService<MDBusinessService>());
            services.AddSingleton<ICCTBBusinessService>(sp => sp.GetRequiredService<TDBusinessService>());
            services.AddSingleton<ICCTBBusinessService>(sp => sp.GetRequiredService<MDTDBusinessService>());

            // 注册工厂和管理器
            services.AddSingleton<CCTBBusinessServiceFactory>();
            services.AddSingleton<CCTBBusinessServiceManager>();
            #endregion

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
            services.AddSingleton<DYC04BusinessService>();

            // 注册接口映射
            services.AddSingleton<IJRCWBusinessService>(sp => sp.GetRequiredService<YWC05BusinessService>());
            services.AddSingleton<IJRCWBusinessService>(sp => sp.GetRequiredService<DYC04BusinessService>());

            // 注册工厂和管理器
            services.AddSingleton<JRCWBusinessServiceFactory>();
            services.AddSingleton<JRCWBusinessServiceManager>();

            return services;
        }

        /// <summary>
        /// 添加JROH项目服务
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddJROHServices(this IServiceCollection services)
        {
            // 注册具体业务类型
            services.AddSingleton<PWBusinessService>();
            services.AddSingleton<OHBusinessService>();
            services.AddSingleton<MCBusinessService>();

            // 注册接口映射
            services.AddSingleton<IJROHBusinessService>(sp => sp.GetRequiredService<PWBusinessService>());
            services.AddSingleton<IJROHBusinessService>(sp => sp.GetRequiredService<OHBusinessService>());
            services.AddSingleton<IJROHBusinessService>(sp => sp.GetRequiredService<MCBusinessService>());

            // 注册工厂和管理器
            services.AddSingleton<JROHBusinessServiceFactory>();
            services.AddSingleton<JROHBusinessServiceManager>();

            return services;
        }
    }
}
