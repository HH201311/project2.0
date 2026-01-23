using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.MES;
using SUNWODA_SEVB.MES.Client;
using SUNWODA_SEVB.MES.Services;
using SUNWODA_SEVB.Core.Models.MES;
using SUNWODA_SEVB.MES.Providers;
using System.Net.Http;

namespace SUNWODA_SEVB.MES.Extensions
{
    /// <summary>
    /// MES服务依赖注入扩展
    /// </summary>
    public static class MesServiceExtensions
    {
        /// <summary>
        /// 添加MES服务（支持数据库配置自动检查）
        /// </summary>
        public static IServiceCollection AddMesServices(this IServiceCollection services)
        {
            // 注册核心MES服务
            AddMesServicesCore(services);

            // 注册MES初始化托管服务（支持数据库配置检查）
            services.AddHostedService<MesInitializationHostedService>();

            return services;
        }

        /// <summary>
        /// 添加MES服务（不自动初始化版本，用于需要手动控制初始化时机的场景）
        /// </summary>
        public static IServiceCollection AddMesServicesWithoutAutoInit(this IServiceCollection services)
        {
            return AddMesServicesCore(services);
        }

        private static IServiceCollection AddMesServicesCore(IServiceCollection services)
        {
            // 1. 注册MES配置提供者（单例，负责从数据库加载配置）
            services.AddSingleton<IMesConfigurationProvider, MesConfigurationProvider>();

            // 2. 注册MES配置（作用域，每次请求时从提供者获取）
            services.AddScoped(provider =>
            {
                var configProvider = provider.GetRequiredService<IMesConfigurationProvider>();
                try
                {
                    var configTask = configProvider.GetConfigurationAsync();
                    return configTask.ConfigureAwait(false).GetAwaiter().GetResult() ?? new MesApiConfiguration();
                }
                catch (Exception ex)
                {
                    var logger = provider.GetService<ILoggerService<MesApiConfiguration>>();
                    logger?.Error($"获取MES配置失败，使用默认配置: {ex.Message}", ex);
                    return new MesApiConfiguration();
                }
            });

            // 3. 注册HTTP客户端管理器（单例）
            services.AddSingleton<IMesHttpClientManager, MesHttpClientManager>();

            // 4. 注册HttpClient和MES API客户端
            services.AddHttpClient<IMesApiClient, MesApiClient>((provider, client) =>
            {
                try
                {
                    var config = provider.GetRequiredService<MesApiConfiguration>();
                    client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

                    // 设置headers
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Apifox/1.0.0 (https://apifox.com)");
                    client.DefaultRequestHeaders.ExpectContinue = false;
                }
                catch
                {
                    // 如果配置获取失败，使用默认设置
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.ParseAdd("*/*");
                    client.DefaultRequestHeaders.UserAgent.Clear();
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("SUNWODA-SEVB-MES/1.0");
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler
                {
                    UseProxy = false,
                    AllowAutoRedirect = false,
                    MaxConnectionsPerServer = 10
                };
            });

            // 5. 注册MES主服务（单例）
            services.AddSingleton<IMesService, MesService>();

            // 6. 注册具体的业务服务（作用域）
            services.AddScoped<IOfflineDataUploadService, OfflineDataUploadService>();
            services.AddScoped<IMarkingDataUploadService, MarkingDataUploadService>();

            // 7. 注册MES服务工厂
            services.AddScoped<Func<string, IMesService>>(provider =>
            {
                return serviceName =>
                {
                    return serviceName.ToLower() switch
                    {
                        "offlinedataupload" => provider.GetRequiredService<IOfflineDataUploadService>(),
                        "increasemarking" => provider.GetRequiredService<IMarkingDataUploadService>(),
                        _ => provider.GetRequiredService<IMesService>()
                    };
                };
            });

            return services;
        }
    }
}