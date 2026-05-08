using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RemoteFileMonitor.Options;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 结果图清理后台服务。
    /// <para>
    /// 每天零点（以及程序启动时延迟 2 分钟后）扫描各工位的 OutputDirectory，
    /// 删除最后写入时间超过 <see cref="RetentionDays"/> 天的一级子目录（即日期文件夹）。
    /// 只清理 OutputDirectory 的直接子目录，不会递归到更深层级，也不会删除 OutputDirectory 本身。
    /// </para>
    /// </summary>
    public sealed class ResultImageCleanupService : BackgroundService
    {
        /// <summary>结果图保留天数（超过此天数的日期文件夹将被删除）。</summary>
        private const int RetentionDays = 20;

        private readonly IOptions<MonitorOptions> _monitorOptions;
        private readonly ILoggerService<ResultImageCleanupService> _logger;

        public ResultImageCleanupService(
            IOptions<MonitorOptions> monitorOptions,
            ILoggerService<ResultImageCleanupService> logger
        )
        {
            _monitorOptions = monitorOptions;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.Info($"[清理结果图] 定时清理服务启动，结果图将只保留 {RetentionDays} 天，将在 2 分钟后做第一次清理，之后每日零点进行清理");
            // 启动后延迟 2 分钟，避免与初始化流程竞争
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                RunCleanup();

                // 计算到次日零点的等待时长，实现每天定时执行
                var now = DateTime.Now;
                var nextMidnight = now.Date.AddDays(1);
                var delay = nextMidnight - now;
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void RunCleanup()
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            var targets = _monitorOptions.Value.Targets;

            if (targets is null || targets.Count == 0)
                return;

            // 去重：多个 Target 可能共享同一 OutputDirectory
            var outputDirs = targets
                .Where(t => !string.IsNullOrWhiteSpace(t.OutputDirectory))
                .Select(t => Path.GetFullPath(t.OutputDirectory))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var outputDir in outputDirs)
            {
                if (!Directory.Exists(outputDir))
                    continue;

                DirectoryInfo[] dateFolders;
                try
                {
                    dateFolders = new DirectoryInfo(outputDir).GetDirectories();
                }
                catch (Exception ex)
                {
                    _logger.Error($"[清理结果图] 无法枚举目录：{outputDir}", ex);
                    continue;
                }

                foreach (var folder in dateFolders)
                {
                    // 以文件夹创建时间判断是否超期
                    if (folder.CreationTime >= cutoff)
                        continue;

                    try
                    {
                        folder.Delete(recursive: true);
                        _logger.Info(
                            $"[清理结果图] 已删除过期日期文件夹（最后创建：{folder.CreationTime:yyyy-MM-dd}）：{folder.FullName}"
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[清理结果图] 删除失败：{folder.FullName}", ex);
                    }
                }
            }
        }
    }
}
