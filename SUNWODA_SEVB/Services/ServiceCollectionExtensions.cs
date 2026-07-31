using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RemoteFileMonitor.Abstractions;
using RemoteFileMonitor.Extensions;
using SUNWODA_SEVB.Core.Attributes;
using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Services.Project.CTOL;
using SUNWODA_SEVB.Services.Project.CTOL.Processing;
using SUNWODA_SEVB.Services.Project.JRRY;

namespace SUNWODA_SEVB.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMvvmFramework(this IServiceCollection services)
        {
            // 注册核心服务
            services.AddSingleton<IModuleManager, ModuleManager>();
            services.AddSingleton<INavigationService, NavigationService>();

            // 自动注册所有标记了Module特性的Views和ViewModels
            services.AddModuleViewModels();
            services.AddModuleViews();

            return services;
        }

        public static IServiceCollection AddModuleViewModels(this IServiceCollection services)
        {
            var viewModelTypes = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Type.EmptyTypes;
                    }
                })
                .Where(t =>
                    t.IsClass
                    && !t.IsAbstract
                    && t.IsSubclassOf(typeof(ViewModelBase))
                    && t.GetCustomAttributes(typeof(ModuleAttribute), false).Any()
                );

            foreach (var vmType in viewModelTypes)
            {
                //services.AddTransient(vmType);
                if (vmType is not null)
                {
                    services.AddSingleton(vmType);
                }
            }

            return services;
        }

        public static IServiceCollection AddModuleViews(this IServiceCollection services)
        {
            var viewModelTypes = AppDomain
                .CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try
                    {
                        return a.GetTypes();
                    }
                    catch
                    {
                        return Type.EmptyTypes;
                    }
                })
                .Where(t =>
                    t.IsClass
                    && !t.IsAbstract
                    && t.IsSubclassOf(typeof(ViewModelBase))
                    && t.GetCustomAttributes(typeof(ModuleAttribute), false).Any()
                );

            foreach (var vmType in viewModelTypes)
            {
                // 约定：ViewModel名称去掉"VM_"前缀就是View名称
                var viewName = vmType.Name.Replace("VM_", "");
                var viewNamespace = vmType.Namespace!.Replace(".ViewModels", ".Views");
                var fullViewName = $"{viewNamespace}.{viewName}";

                var viewType =
                    vmType.Assembly.GetType(fullViewName)
                    ?? vmType.Assembly.GetTypes().FirstOrDefault(t => t.Name == viewName);

                if (viewType is not null)
                {
                    services.AddSingleton(viewType);
                }
            }
            return services;
        }

        public static IServiceCollection AddCTOLRemoteFileMonitor(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRemoteFileMonitor(configuration);
            services.Configure<BarcodeGroupingOptions>(configuration.GetSection("BarcodeGrouping"));
            //services.AddFileProcessor<LABImageProcessor>();
            //services.AddFileProcessor<LCDImageProcessor>();
            //services.AddFileProcessor<RABImageProcessor>();
            //services.AddFileProcessor<RCDImageProcessor>();
            services.AddSingletonFileProcessor<LABImageProcessor>();
            services.AddSingletonFileProcessor<LCDImageProcessor>();
            services.AddSingletonFileProcessor<RABImageProcessor>();
            services.AddSingletonFileProcessor<RCDImageProcessor>();
            services.AddSingleton<RemoteFileMonitorConfigService>();
            services.AddSingleton<GapThresholdService>();
            services.AddSingleton<BarcodeGroupingService>();
            services.AddSingleton<Project.CTOL.InspectionResultService>();
            services.AddSingleton<ProcessingResultService>();
            services.AddHostedService(sp => sp.GetRequiredService<BarcodeGroupingService>());
            services.AddSingleton<GapCsvService>();        // 间隙值 CSV 按天写入服务
            services.AddHostedService<ResultImageCleanupService>(); // 结果图定期清理（超过 20 天的日期文件夹）

            // 每日上传服务
            services.Configure<DailyUploadOptions>(configuration.GetSection("DailyUpload"));
            services.AddSingleton<DailyUploadService>();
            services.AddHostedService(sp => sp.GetRequiredService<DailyUploadService>());

            return services;
        }

        /// <summary>
        /// 添加极耳冗余项目远程文件监控服务
        /// </summary>    
        public static IServiceCollection AddJRRYRemoteFileMonitor(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRemoteFileMonitor(configuration);            
            services.AddSingleton<ThresholdService>();
            services.AddSingleton<ResultAggregator>();
            services.AddSingleton<ProcessingResultService>();
            services.AddSingleton<RemoteFileMonitorConfigService>();
            services.AddSingletonFileProcessor<ImageProcessor>();
            services.AddHostedService<ModelPreloader>();
            services.AddSingleton<Project.JRRY.InspectionResultService>();

            return services;
        }
    }
}
