using System.Diagnostics;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Tool.Helper;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public class ProcessMonitorService
    {
        private readonly ILoggerService<ProcessMonitorService> _logger;
        private CancellationTokenSource? _cts;

        public ProcessMonitorService(ILoggerService<ProcessMonitorService> logger)
        {
            _logger = logger;
        }

        public void StartIfNotRunning(int sampleRateMs = 5000)
        {
            if (_cts != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = Task.Run(() => MonitorLoopAsync(sampleRateMs, token), token);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private async Task MonitorLoopAsync(int sampleRateMs, CancellationToken cancellationToken)
        {
            var processName = $"{Process.GetCurrentProcess().ProcessName}.exe";
            var monitor = new UIAutoMonitorCore.Monitor
            {
                WindowProcessMonitorList = new List<string> { processName },
            };
            monitor.MonitorProcessInfo(true);
            _logger.Info($"开启 CT 模块进程信息监控: {processName}");

            var header = "时间,进程Id,主线程Id,CPU占用(%),内存占用(MB),GPU占用(%),I/O读写速度(MB/s),每秒异常数,线程数,等待获取托管锁线程数,";
            var oldTime = DateTime.Now;
            var outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CTAutoSlicer", "进程监控信息");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!monitor.WindowProcessMonitorInfoDic.TryGetValue(processName, out var info))
                    {
                        await Task.Delay(sampleRateMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var newTime = DateTime.Now;
                    var contentBuilder = new StringBuilder();
                    contentBuilder.Append($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},");
                    contentBuilder.Append($"{info.Pid},");
                    contentBuilder.Append($"{info.MainThreadId},");
                    contentBuilder.Append($"{info.CPULoad},");
                    contentBuilder.Append($"{info.RAMUsage},");
                    contentBuilder.Append($"{info.GPULoad},");
                    contentBuilder.Append($"{info.IODataRate},");
                    contentBuilder.Append($"{info.ExceptionCountPerSecond},");
                    contentBuilder.Append($"{info.ThreadCount},");
                    contentBuilder.Append($"{info.ThreadCurrentQueueLength},");

                    if (
                        newTime.Year > oldTime.Year
                        || (newTime.Year == oldTime.Year && newTime.Month > oldTime.Month)
                        || (newTime.Year == oldTime.Year && newTime.Month == oldTime.Month && newTime.Day > oldTime.Day)
                    )
                    {
                        oldTime = newTime;
                    }

                    lock (CsvFileHelper.FileObj)
                    {
                        CsvFileHelper.SaveCSVFile(outputDirectory, $"{oldTime:yyyy_MM_dd}", header, contentBuilder.ToString());
                    }

                    WinAPIHelper.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("进程监控写入异常", ex);
                }

                await Task.Delay(sampleRateMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
