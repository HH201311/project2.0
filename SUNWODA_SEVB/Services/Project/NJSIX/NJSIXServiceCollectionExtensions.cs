using Microsoft.Extensions.DependencyInjection;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    public static class NJSIXServiceCollectionExtensions
    {
        public static IServiceCollection AddNJSIXPack6Services(this IServiceCollection services)
        {
            services.AddSingleton<INJSIXPack6ConfigService, Pack6ConfigService>();
            services.AddSingleton<INJSIXPack6ProcessedStore, Pack6ProcessedFileStore>();
            services.AddSingleton<INJSIXPack6IngressService, Pack6IngressService>();
            services.AddSingleton<INJSIXPack6VisionEngine, Pack6NjtwVisionEngine>();
            services.AddSingleton<INJSIXPack6ProductAggregator, Pack6ProductAggregator>();
            services.AddSingleton<INJSIXPack6ResultStore, Pack6CsvResultStore>();
            services.AddSingleton<INJSIXPack6ImageArchiveService, Pack6ImageArchiveService>();
            services.AddSingleton<INJSIXPack6PlcAlarmService, Pack6PlcAlarmService>();
            services.AddSingleton<INJSIXPack6PipelineService, Pack6PipelineService>();
            return services;
        }
    }
}
