using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System.IO;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 兜底通道：定期增量扫描"热点目录"（最近有 FSW 事件的目录）。
    /// 仅补漏，不会替代 Watcher；对已处理文件 DB 检查前先入队（Processor 再去重）。
    ///
    /// 目录层级：
    ///   Z:\LAB\{date}\OK\{barcode}\*_origin.png
    /// 热点目录粒度 = barcode 目录（FSW 触发时 Touch 的层级）。
    /// </summary>
    public sealed class IncrementalScanService
    {
        private readonly MonitorConfig _cfg;
        private readonly FileEventQueue _queue;
        private readonly HotDirectoryTracker _hot;
        private readonly DatabaseManager _db;
        private readonly ILoggerService _log;

        public IncrementalScanService(
            MonitorConfig cfg,
            FileEventQueue queue,
            HotDirectoryTracker hot,
            DatabaseManager db,
            ILoggerService log)
        {
            _cfg = cfg;
            _queue = queue;
            _hot = hot;
            _db = db;
            _log = log;
        }

        public async Task ExecuteAsync(CancellationToken ct)
        {
            // 启动时做一次全量冷启动扫描（补漏昨天/今天进程重启前的文件）
            await ColdStartScanAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(_cfg.ScanIntervalSeconds), ct);
                await ScanHotDirectoriesAsync(ct);
            }
        }

        /// <summary>
        /// 冷启动：扫描今天和昨天的日期目录下所有 OK 子目录，
        /// 只扫这两天避免全盘爆炸。
        /// </summary>
        private async Task ColdStartScanAsync(CancellationToken ct)
        {
            _log.Info("冷启动扫描开始");
            var today = DateOnly.FromDateTime(DateTime.Now);
            var yesterday = today.AddDays(-1);

            int count = 0;
            foreach (var date in new[] { yesterday, today })
            {
                var dateDir = Path.Combine(_cfg.WatchRoot, date.ToString("yyyy-MM-dd"));
                if (!Directory.Exists(dateDir)) continue;

                count += await ScanDateDirectoryAsync(dateDir, date, ct);
            }
            _log.Info($"冷启动扫描完成, 进队 {count} 个文件");
        }

        /// <summary>只扫热点目录（barcode 级）。</summary>
        private async Task ScanHotDirectoriesAsync(CancellationToken ct)
        {
            var hotDirs = _hot.GetHotDirectories();
            if (hotDirs.Count == 0) return;

            _log.Debug($"增量扫描: {hotDirs.Count} 热点目录");
            int total = 0;

            foreach (var dir in hotDirs)
            {
                if (ct.IsCancellationRequested) break;
                if (!Directory.Exists(dir)) continue;

                try
                {
                    // barcode 目录直接枚举文件
                    foreach (var file in Directory.EnumerateFiles(dir, _cfg.FilePattern, SearchOption.TopDirectoryOnly))
                    {
                        var date = ExtractDate(file) ?? DateOnly.FromDateTime(DateTime.Now);
                        if (!await _db.IsProcessedAsync(file, date))
                        {
                            _queue.Enqueue(new FileEvent(file, "Scanner"));
                            total++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"扫描错误出现在: {dir}", ex);
                }
            }

            if (total > 0)
                _log.Info($"增量扫描进队 {total} 个未处理文件");
        }

        private async Task<int> ScanDateDirectoryAsync(string dateDir, DateOnly date, CancellationToken ct)
        {
            int count = 0;
            // dateDir\OK\{barcode}
            var okDir = Path.Combine(dateDir, "OK");
            if (!Directory.Exists(okDir)) return 0;

            foreach (var barcodeDir in Directory.EnumerateDirectories(okDir))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(barcodeDir, _cfg.FilePattern, SearchOption.TopDirectoryOnly))
                    {
                        if (!await _db.IsProcessedAsync(file, date))
                        {
                            _queue.Enqueue(new FileEvent(file, "Scanner"));
                            count++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"扫描错误出现在: {barcodeDir}", ex);
                }
            }
            return count;
        }

        /// <summary>
        /// 从路径 D:\LAB\2025-12-12\OK\... 提取日期。
        /// </summary>
        private static DateOnly? ExtractDate(string filePath)
        {
            // 向上找"yyyy-MM-dd"段
            var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var part in parts)
                if (DateOnly.TryParseExact(part, "yyyy-MM-dd", out var d))
                    return d;
            return null;
        }
    }
}
