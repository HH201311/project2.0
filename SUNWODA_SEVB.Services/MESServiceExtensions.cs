using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.MES.Client;
using SUNWODA_SEVB.MES.Providers;
using SUNWODA_SEVB.MES.Services;

namespace SUNWODA_SEVB.MES
{
    /// <summary>
    /// MES服务依赖注入扩展
    /// </summary>
    public static class MesServiceExtensions
    {
        public static IServiceCollection AddMesServices(this IServiceCollection services)
        {
            // 1. MES 配置提供者（单例，负责数据库加载与缓存）
            services.AddSingleton<IMesConfigurationProvider, MesConfigurationProvider>();

            // 2. MES 配置快照（单例，启动后由托管服务填充，此处返回缓存值或默认配置）
            services.AddSingleton(sp =>
            {
                var provider = sp.GetRequiredService<IMesConfigurationProvider>();
                return provider.CurrentConfiguration ?? new MesApiConfiguration();
            });

            // 3. 注册类型化 HttpClient，配置主消息处理器，然后添加弹性策略
            services.AddHttpClient<IMesApiClient, MesApiClient>(client =>
            {
                client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SUNWODA-SEVB-MES/1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                MaxConnectionsPerServer = 10
            })
            .AddStandardResilienceHandler(); 

            // 4. 业务服务注册为 Scoped
            services.AddScoped<IMesService, MesService>();
            services.AddScoped<IOfflineDataUploadService, OfflineDataUploadService>();
            services.AddScoped<IMarkingDataUploadService, MarkingDataUploadService>();
            services.AddScoped<ITestDataService, TestDataService>();

            // 5. 托管服务：启动时自动初始化所有 MES 服务
            services.AddHostedService<MesInitializationHostedService>();

            return services;
        }
    }
}