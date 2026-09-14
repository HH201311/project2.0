using System.Collections.Concurrent;
using System.Windows;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>单消费者中央队列：入队 → 视觉 → 汇总 → CSV。</summary>
    public sealed class Pack6PipelineService : IZZSIXPack6PipelineService
    {
        private readonly IZZSIXPack6ConfigService _configService;
        private readonly IZZSIXPack6VisionEngine _vision;
        private readonly IZZSIXPack6ProductAggregator _aggregator;
        private readonly IZZSIXPack6ResultStore _store;
        private readonly ILoggerService<Pack6PipelineService> _logger;
        private readonly ConcurrentQueue<Pack6IngressItem> _queue = new();
        private readonly List<Pack6StationStat> _stats;
        private CancellationTokenSource? _cts;
        private Task? _loop;

        public event Action<string>? LogEmitted;
        public event Action<Pack6FrameResult>? FrameCompleted;
        public event Action<Pack6ProductResult>? ProductCompleted;
        public bool IsRunning { get; private set; }
        public IReadOnlyList<Pack6StationStat> StationStats => _stats;

        public Pack6PipelineService(
            IZZSIXPack6ConfigService configService,
            IZZSIXPack6VisionEngine vision,
            IZZSIXPack6ProductAggregator aggregator,
            IZZSIXPack6ResultStore store,
            ILoggerService<Pack6PipelineService> logger)
        {
            _configService = configService;
            _vision = vision;
            _aggregator = aggregator;
            _store = store;
            _logger = logger;
            _stats =
            [
                new Pack6StationStat("上盖"),
                new Pack6StationStat("底部1"),
                new Pack6StationStat("底部2"),
                new Pack6StationStat("底部3"),
                new Pack6StationStat("前面"),
                new Pack6StationStat("后面"),
                new Pack6StationStat("左面"),
                new Pack6StationStat("右面"),
                new Pack6StationStat("一次优率")
            ];
            _aggregator.ProductCompleted += OnProductCompleted;
        }

        public async Task StartAsync()
        {
            if (IsRunning)
                return;

            var config = _configService.Current;
            await _store.EnsureFoldersAsync(config);
            await _vision.InitializeAsync(config);
            _cts = new CancellationTokenSource();
            IsRunning = true;
            _loop = Task.Run(() => LoopAsync(_cts.Token));
            Emit("管线已启动");
        }

        public async Task StopAsync()
        {
            IsRunning = false;
            if (_cts is not null)
            {
                await _cts.CancelAsync();
                _cts.Dispose();
                _cts = null;
            }
            if (_loop is not null)
            {
                try { await _loop; } catch { /* ignore */ }
                _loop = null;
            }
            _vision.Shutdown();
            while (_queue.TryDequeue(out _)) { }
            Emit("管线已停止");
        }

        public void Enqueue(Pack6IngressItem item)
        {
            if (!IsRunning)
                return;
            _queue.Enqueue(item);
        }

        public void ResetStatistics()
        {
            foreach (var s in _stats)
                s.Clear();
            _aggregator.Reset();
            Emit("统计已清零");
        }

        public void Dispose()
        {
            _aggregator.ProductCompleted -= OnProductCompleted;
            _ = StopAsync();
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!_queue.TryDequeue(out var item))
                    {
                        await Task.Delay(30, ct);
                        continue;
                    }

                    var frame = await _vision.DetectAsync(item, ct);
                    UpdateStation(frame);
                    FrameCompleted?.Invoke(frame);
                    Emit($"[{frame.StationName}] {frame.ProductCode} => {(frame.IsOk ? "OK" : "NG")}");

                    var timeout = TimeSpan.FromMinutes(Math.Max(1, _configService.Current.PackTimeoutMinutes));
                    _ = _aggregator.ApplyFrame(frame, timeout);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Pack6 管线异常: {ex.Message}", false);
                    Emit($"管线异常: {ex.Message}");
                }
            }
        }

        private async void OnProductCompleted(Pack6ProductResult result) =>
            await PersistProductAsync(result);

        private async Task PersistProductAsync(Pack6ProductResult result)
        {
            try
            {
                await _store.AppendProductResultAsync(_configService.Current, result);
                UpdateYield(result);
                ProductCompleted?.Invoke(result);
                Emit($"Pack完成 {result.ProductCode} => {(result.IsOk ? "OK" : "NG")}" +
                     (result.TimedOut ? " (超时)" : string.Empty));
            }
            catch (Exception ex)
            {
                _logger.Warn($"落盘 Pack 结果失败: {ex.Message}", false);
            }
        }

        private void UpdateStation(Pack6FrameResult frame)
        {
            var row = _stats.FirstOrDefault(s => s.Station == frame.StationName);
            if (row is null)
                return;
            RunOnUi(() =>
            {
                row.Total++;
                if (frame.IsOk) row.OK++;
                else row.NG++;
            });
        }

        private void UpdateYield(Pack6ProductResult result)
        {
            var row = _stats.FirstOrDefault(s => s.Station == "一次优率");
            if (row is null)
                return;
            RunOnUi(() =>
            {
                row.Total++;
                if (result.IsOk) row.OK++;
                else row.NG++;
            });
        }

        private void Emit(string msg)
        {
            _logger.Info(msg, false);
            LogEmitted?.Invoke($"{DateTime.Now:HH:mm:ss} {msg}");
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return;
            }
            dispatcher.Invoke(action);
        }
    }
}
