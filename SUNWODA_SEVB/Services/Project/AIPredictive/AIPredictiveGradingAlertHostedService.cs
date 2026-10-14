using Microsoft.Extensions.Hosting;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 应用内按配置周期触发分级预警（默认 2 小时；不依赖 Windows 计划任务）。
    /// </summary>
    public sealed class AIPredictiveGradingAlertHostedService : BackgroundService
    {
        private readonly IAIPredictiveGradingAlertService _grading;
        private readonly ILoggerService<AIPredictiveGradingAlertHostedService> _logger;

        public AIPredictiveGradingAlertHostedService(
            IAIPredictiveGradingAlertService grading,
            ILoggerService<AIPredictiveGradingAlertHostedService> logger)
        {
            _grading = grading;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var cfg = _grading.Config;
            if (!cfg.Enabled)
            {
                _logger.Info("AIPredictive 分级预警后台服务未启用（grading_config.Enabled=false）", false);
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, cfg.IntervalMinutes));
            _logger.Info(
                $"AIPredictive 分级预警后台服务启动：间隔={interval.TotalMinutes:0}min，设备={cfg.DeviceName}，回看={cfg.LookbackHours}h",
                true);

            // 启动后略等，让采集先写出若干 CSV
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _grading.RunOnceAsync(stoppingToken).ConfigureAwait(false);
                    if (!result.Success)
                        _logger.Warn($"分级预警本轮失败: {result.Message}", false);
                    else if (result.Skipped)
                        _logger.Info($"分级预警本轮跳过: {result.Message}", false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("分级预警周期任务异常", ex);
                }

                try
                {
                    await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
