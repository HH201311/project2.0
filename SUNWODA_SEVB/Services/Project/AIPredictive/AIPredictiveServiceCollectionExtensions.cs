using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Services.Project.AIPredictive._TempBossReport;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    public static class AIPredictiveServiceCollectionExtensions
    {
        public static IServiceCollection AddAIPredictiveServices(this IServiceCollection services)
        {
            services.AddSingleton<IAIPredictiveCollectBlockLoader, AIPredictiveCollectBlockLoader>();
            services.AddSingleton<IAIPredictiveCsvStorage, AIPredictiveCsvStorage>();
            services.AddSingleton<IAIPredictivePlcEndpointStore, AIPredictivePlcEndpointStore>();
            services.AddSingleton<IAIPredictiveAcquisitionService, AIPredictiveAcquisitionService>();
            services.AddSingleton<IAIPredictiveGradingAlertService, AIPredictiveGradingAlertService>();
            services.AddSingleton<AIPredictiveGradingAlertUiBinder>();
            services.AddHostedService<AIPredictiveGradingAlertHostedService>();
            // TEMP_BOSS_REPORT — 正式交付删除本行及 _TempBossReport 目录
            services.AddSingleton<AIPredictiveTempRuntimeMonitor>();
            return services;
        }
    }
}
