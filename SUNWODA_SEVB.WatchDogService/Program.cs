using SUNWODA_SEVB.Logging;

namespace SUNWODA_SEVB.WatchDogService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            IHost host = Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    options.ServiceName = "WatchDogService";
                })
                .ConfigureServices(services =>
                {
                    services.AddNLogServices();
                    services.AddSingleton<Worker>();
                    services.AddHostedService<Worker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
