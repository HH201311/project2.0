using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 统一处理：从 Channel 读事件 → 判断文件写入完成 → DB 去重 → 业务处理。
    /// 单消费者（SingleReader=true），天然串行，简化并发控制。
    /// 如需并行处理可改为多 Task + SemaphoreSlim 控制并发数。
    /// </summary>
    public sealed class ProcessorService
    {
        private readonly MonitorConfig _cfg;
        private readonly FileEventQueue _queue;
        private readonly HotDirectoryTracker _hot;
        private readonly DatabaseManager _db;
        private readonly ILoggerService _log;

        private static readonly TimeSpan StabilityPollInterval = TimeSpan.FromSeconds(1);

        public ProcessorService(
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
            _log.Info("处理服务启动");

            // 并发处理：多 Task 消费同一 Channel（Reader 线程安全）
            const int Parallelism = 4;
            var tasks = Enumerable.Range(0, Parallelism)
                .Select(_ => ConsumeLoopAsync(ct))
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task ConsumeLoopAsync(CancellationToken ct)
        {
            await foreach (var ev in _queue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    await HandleEventAsync(ev, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.Error($"未能处理的错误: {ev.FilePath}", ex);
                }
                finally
                {
                    // 处理完后从去重窗口释放，允许兜底扫描再次入队（如果未成功）
                    _queue.ReleaseDedup(ev.FilePath);
                }
            }
        }

        private async Task HandleEventAsync(FileEvent ev, CancellationToken ct)
        {
            // 1. 文件是否存在
            if (!File.Exists(ev.FilePath))
                return;

            // 2. 路径合法性（只处理符合结构的文件）
            if (!IsValidPath(ev.FilePath))
            {
                _log.Debug($"跳过非匹配路径: {ev.FilePath}");
                return;
            }

            // 3. 等待文件写入完成
            if (!await WaitUntilFileStableAsync(ev.FilePath, ct))
            {
                _log.Warn($"文件未稳定跳过: {ev.FilePath}");
                return;
            }

            // 4. DB 去重（防止并发重复处理）
            var date = ExtractDate(ev.FilePath);
            if (await _db.IsProcessedAsync(ev.FilePath, date))
                return;

            // 5. 执行业务处理
            await ProcessImageAsync(ev, ct);

            // 6. 标记已完成
            await _db.MarkProcessedAsync(ev.FilePath, date, ev.Source);

            _log.Info($"[{ev.Source}] 完成: {ev.FilePath}");
        }

        /// <summary>
        /// 文件完成判定：
        ///   连续两次轮询（间隔 1 s）文件大小相同，
        ///   且距最后写入时间 >= FileStableSeconds，
        ///   且能以 FileShare.None 独占打开。
        /// 最多等待 FileStableSeconds * 10 秒放弃。
        /// </summary>
        private async Task<bool> WaitUntilFileStableAsync(string path, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow.AddSeconds(_cfg.FileStableSeconds * 10);
            long prevSize = -1;

            while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                await Task.Delay(StabilityPollInterval, ct);

                if (!File.Exists(path)) return false;

                try
                {
                    var fi = new FileInfo(path);
                    fi.Refresh();
                    long curSize = fi.Length;

                    // 大小稳定 + 最后写入时间够老
                    var timeSinceWrite = DateTime.UtcNow - fi.LastWriteTimeUtc;
                    if (curSize == prevSize && curSize > 0 && timeSinceWrite.TotalSeconds >= _cfg.FileStableSeconds)
                    {
                        // 再验证：尝试独占打开（确保没有其他进程持有写锁）
                        if (TryOpenExclusive(path))
                            return true;
                    }

                    prevSize = curSize;
                }
                catch (IOException)
                {
                    // 文件被占用，继续等待
                    prevSize = -1;
                }
            }

            return false;
        }

        private static bool TryOpenExclusive(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return fs.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ★ 在此处填写实际业务逻辑 ★
        /// 示例：记录日志 / 触发 OCR / 发消息队列 / 复制到处理目录等。
        /// </summary>
        private Task ProcessImageAsync(FileEvent ev, CancellationToken ct)
        {
            // 解析路径结构：Z:\LAB\{date}\OK\{barcode}\{filename}
            var parts = ev.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // 简单示例：打印解析信息
            if (parts.Length >= 5)
            {
                var date = parts[^4]; // yyyy-MM-dd
                var status = parts[^3]; // OK
                var barcode = parts[^2]; // 355125701G66027
                var file = parts[^1]; // 355125701G66027_1-X_origin.png
                _log.Info(
                    "处理: 日期={date} 状态={status} 条码={barcode} 文件名称={file} 来源={ev.Source}");
            }

            // TODO: 替换为实际处理逻辑
            return Task.CompletedTask;
        }

        private static bool IsValidPath(string path)
        {
            // 基本验证：路径中包含 \OK\ 且以 _origin.png 结尾
            return path.Contains(Path.DirectorySeparatorChar + "OK" + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   && path.EndsWith("_origin.png", StringComparison.OrdinalIgnoreCase);
        }

        private static DateOnly ExtractDate(string filePath)
        {
            var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var part in parts)
                if (DateOnly.TryParseExact(part, "yyyy-MM-dd", out var d))
                    return d;
            return DateOnly.FromDateTime(DateTime.Now);
        }
    }
}
