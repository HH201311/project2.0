using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;

namespace SUNWODA_SEVB.Services.Project.BoxGluing
{
    public static class BoxGluingServiceCollectionExtensions
    {
        public static IServiceCollection AddBoxGluingServices(this IServiceCollection services)
        {
            services.AddSingleton<IBoxGluingConfigService, BoxGluingConfigService>();
            services.AddSingleton<IBoxGluingCsvStorage, BoxGluingCsvStorage>();
            services.AddSingleton<IBoxGluingChartDataService, BoxGluingChartDataService>();
            services.AddSingleton<IBoxGluingAcquisitionService, BoxGluingAcquisitionService>();
            return services;
        }
    }
}
