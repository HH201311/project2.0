using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.WatchDogService
{
    public class Worker : BackgroundService
    {
        private readonly ILoggerService<Worker> _logger;
        private readonly string mainExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\SUNWODA_SEVB.exe");
        private readonly string mutexName = "Global\\SUNWODA_SEVBMutex";

        public Worker(ILoggerService<Worker> logger)
        {
            _logger = logger;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info($"看门狗启动时间: {DateTimeOffset.Now}");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!IsMainAppRunning())
                {
                    try
                    {
                        _logger.Info("主程序未运行，通过用户会话启动程序");

                        // 检查是否有可用的用户会话
                        var userSessions = UserProcessLauncherHelper.GetActiveUserSessions();
                        if (userSessions.Length == 0)
                        {
                            _logger.Warn("没有活跃的用户会话，跳过启动UI程序");
                            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                            continue;
                        }

                        var result = UserProcessLauncherHelper.CreateProcess(mainExePath);
                        if (result.Success)
                        {
                            _logger.Info($"UI程序已在用户会话 {result.SessionId} 中启动，PID: {result.ProcessId}");
                        }
                        else
                        {
                            _logger.Error($"启动失败: {result.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("启动主程序失败", ex, true);
                    }
                }

                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private bool IsMainAppRunning()
        {
            try
            {
                Mutex.OpenExisting(mutexName).Dispose();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
