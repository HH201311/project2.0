using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project.YWSIX;

namespace SUNWODA_SEVB.Services.Project.YWSIX
{
    public static class YWSIXServiceCollectionExtensions
    {
        public static IServiceCollection AddYWSIXPack6Services(this IServiceCollection services)
        {
            services.AddSingleton<IYWSIXPack6ConfigService, Pack6ConfigService>();
            services.AddSingleton<IYWSIXPack6ProcessedStore, Pack6ProcessedFileStore>();
            services.AddSingleton<IYWSIXPack6IngressService, Pack6IngressService>();
            services.AddSingleton<IYWSIXPack6VisionEngine, Pack6Ywx01VisionEngine>();
            services.AddSingleton<IYWSIXPack6ProductAggregator, Pack6ProductAggregator>();
            services.AddSingleton<IYWSIXPack6ResultStore, Pack6CsvResultStore>();
            services.AddSingleton<IYWSIXPack6ImageArchiveService, Pack6ImageArchiveService>();
            services.AddSingleton<IYWSIXPack6PlcAlarmService, Pack6PlcAlarmService>();
            services.AddSingleton<IYWSIXPack6PipelineService, Pack6PipelineService>();
            return services;
        }
    }
}
