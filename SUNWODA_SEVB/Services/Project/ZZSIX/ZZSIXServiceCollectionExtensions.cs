using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    public static class ZZSIXServiceCollectionExtensions
    {
        public static IServiceCollection AddZZSIXPack6Services(this IServiceCollection services)
        {
            services.AddSingleton<IZZSIXPack6ConfigService, Pack6ConfigService>();
            services.AddSingleton<IZZSIXPack6IngressService, Pack6IngressService>();
            services.AddSingleton<IZZSIXPack6VisionEngine, Pack6SimulationVisionEngine>();
            services.AddSingleton<IZZSIXPack6ProductAggregator, Pack6ProductAggregator>();
            services.AddSingleton<IZZSIXPack6ResultStore, Pack6CsvResultStore>();
            services.AddSingleton<IZZSIXPack6PipelineService, Pack6PipelineService>();
            return services;
        }
    }
}
