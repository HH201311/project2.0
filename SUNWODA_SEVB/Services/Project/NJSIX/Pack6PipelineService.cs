using System.Windows;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// 萝卜坑调度：一检项一通道并行填**当前提交头**；头产品全部坑齐套（临时等同 PLC 信号）后才跑下一包。
    /// 缺张计时仅对提交头武装。
    /// </summary>
    public sealed class Pack6PipelineService : INJSIXPack6PipelineService
    {
        private readonly INJSIXPack6ConfigService _configService;
        private readonly INJSIXPack6VisionEngine _vision;
        private readonly INJSIXPack6ProductAggregator _aggregator;
        private readonly INJSIXPack6ResultStore _store;
        private readonly INJSIXPack6ImageArchiveService _imageArchive;
        private readonly INJSIXPack6IngressService _ingress;
        private readonly ILoggerService<Pack6PipelineService> _logger;
        private readonly List<Pack6StationStat> _stats;

        private readonly object _gate = new();
        private readonly List<string> _order = [];
        private readonly Dictionary<string, ProductBucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Pack6ProductResult> _commitReady = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _flushLock = new(1, 1);

        private CancellationTokenSource? _cts;
        private readonly List<Task> _channelLoops = [];
        private Task? _timeoutLoop;
        private int _framesSinceGc;

        public event Action<string>? LogEmitted;
        public event Action<Pack6FrameResult>? FrameCompleted;
        public event Action<Pack6ProductResult>? ProductCompleted;
        public bool IsRunning { get; private set; }
        public IReadOnlyList<Pack6StationStat> StationStats => _stats;

        public Pack6PipelineService(
            INJSIXPack6ConfigService configService,
            INJSIXPack6VisionEngine vision,
            INJSIXPack6ProductAggregator aggregator,
            INJSIXPack6ResultStore store,
            INJSIXPack6ImageArchiveService imageArchive,
            INJSIXPack6IngressService ingress,
            ILoggerService<Pack6PipelineService> logger)
        {
            _configService = configService;
            _vision = vision;
            _aggregator = aggregator;
            _store = store;
            _imageArchive = imageArchive;
            _ingress = ingress;
            _logger = logger;
            _stats =
            [
                new Pack6StationStat("三码校验"),
                new Pack6StationStat("一次优率")
            ];
            _aggregator.ProductCompleted += OnAggregatorProductCompleted;
        }

        public async Task StartAsync()
        {
            if (IsRunning)
                return;

            var config = _configService.Current;
            await _store.EnsureFoldersAsync(config);
            _imageArchive.EnsureRoots(config);
            _ = Task.Run(() => _imageArchive.PurgeIfNeeded(config));
            await _vision.InitializeAsync(config);
            _cts = new CancellationTokenSource();
            IsRunning = true;
            var ct = _cts.Token;
            _channelLoops.Clear();
            foreach (var kind in Pack6WorkClassifier.AllChannels)
            {
                var channel = kind;
                _channelLoops.Add(Task.Run(() => ChannelLoopAsync(channel, ct), ct));
            }
            _timeoutLoop = Task.Run(() => TimeoutLoopAsync(ct), ct);
            Emit($"管线已启动（一检项一通道×{Pack6WorkClassifier.AllChannels.Count}，不超前跑下一包；缺张超时{_configService.Current.PackMissingTimeoutSeconds}s）");
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

            foreach (var t in _channelLoops)
                await AwaitQuiet(t);
            _channelLoops.Clear();
            await AwaitQuiet(_timeoutLoop);
            _timeoutLoop = null;
            _vision.Shutdown();
            ClearBuckets();
            Emit("管线已停止");
        }

        public void Enqueue(Pack6IngressItem item)
        {
            if (!IsRunning)
                return;

            var key = Pack6WorkClassifier.ResolveProductKey(item);
            // 已结案产品的迟到图不得再占提交坑，否则 IsClosed 导致永不超时、堵死全线
            if (_aggregator.IsClosed(key))
            {
                try { _ingress.NotifyProcessed(item.FilePath); }
                catch { /* ignore */ }
                return;
            }

            var channel = Pack6WorkClassifier.ResolveChannel(item, _configService.Current);
            var triggerSnBackfill = false;
            string? dateHint = null;

            lock (_gate)
            {
                // 二次确认：持锁期间可能刚结案
                if (_aggregator.IsClosed(key))
                {
                    try { _ingress.NotifyProcessed(item.FilePath); }
                    catch { /* ignore */ }
                    return;
                }

                if (!_buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new ProductBucket(key);
                    _buckets[key] = bucket;
                    _order.Add(key);
                    dateHint = Pack6FileNameParser.ResolveDateFolderName(item.FilePath);
                    bucket.DateFolderHint = dateHint;
                    triggerSnBackfill = true;
                }
                else if (string.IsNullOrWhiteSpace(bucket.DateFolderHint))
                {
                    dateHint = Pack6FileNameParser.ResolveDateFolderName(item.FilePath);
                    if (!string.IsNullOrWhiteSpace(dateHint))
                        bucket.DateFolderHint = dateHint;
                }

                if (bucket.Abandoned)
                {
                    try { _ingress.NotifyProcessed(item.FilePath); }
                    catch { /* ignore */ }
                    return;
                }

                bucket.Queues[channel].Enqueue(item);
                bucket.TouchProgress();
                EnsureHeadTimeoutArmed_NoLock();
            }

            if (triggerSnBackfill)
                FireSnBackfill(key, dateHint, firstEnqueue: true);
        }

        /// <summary>产品首次建桶：后台补扫 Y/Z 当日 SN 夹已有图（不阻塞收图线程）。</summary>
        private void FireSnBackfill(string productSn, string? dateHint, bool firstEnqueue)
        {
            var ct = _cts?.Token ?? CancellationToken.None;
            _ = Task.Run(async () =>
            {
                try
                {
                    var n = await _ingress.BackfillProductFolderAsync(productSn, dateHint, ct)
                        .ConfigureAwait(false);
                    if (n > 0)
                        Emit(firstEnqueue
                            ? $"产品 {productSn} 首次入队补扫：新入队 {n} 张"
                            : $"产品 {productSn} 补扫：新入队 {n} 张");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    /* stop */
                }
                catch (Exception ex)
                {
                    _logger.Warn($"SN补扫异常 {productSn}: {ex.Message}", false);
                }
            }, ct);
        }

        public void ResetStatistics()
        {
            foreach (var s in _stats)
                s.Clear();
            _aggregator.Reset();
            ClearBuckets();
            try
            {
                Pack6UiStatsStore.Save(
                    _configService.Current,
                    _stats,
                    Array.Empty<Pack6UiResultLogDto>());
            }
            catch (Exception ex)
            {
                _logger.Warn($"清零后写统计文件失败: {ex.Message}", false);
            }

            Emit("统计已清零");
        }

        /// <summary>从磁盘恢复三码/一次优率计数（结果日志由 VM 恢复）。</summary>
        public void LoadPersistedStatistics()
        {
            try
            {
                if (!Pack6UiStatsStore.TryLoad(_configService.Current, out var snap))
                    return;
                RunOnUi(() => Pack6UiStatsStore.ApplyStations(_stats, snap.Stations));
                Emit($"已恢复界面统计（存于 {Pack6UiStatsStore.GetStorePath(_configService.Current)}）");
            }
            catch (Exception ex)
            {
                _logger.Warn($"加载界面统计失败: {ex.Message}", false);
            }
        }

        /// <summary>把当前工位计数写入磁盘（日志行由 VM 一并写入）。</summary>
        public void PersistStationStatistics(IEnumerable<Pack6UiResultLogDto> logs)
        {
            try
            {
                Pack6UiStatsStore.Save(_configService.Current, _stats, logs);
            }
            catch (Exception ex)
            {
                _logger.Warn($"保存界面统计失败: {ex.Message}", false);
            }
        }

        public void Dispose()
        {
            _aggregator.ProductCompleted -= OnAggregatorProductCompleted;
            _ = StopAsync();
            _flushLock.Dispose();
        }

        private async Task ChannelLoopAsync(Pack6DetectChannelKind channel, CancellationToken ct)
        {
            var name = Pack6WorkClassifier.ChannelDisplayName(channel);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Pack6IngressItem? item;
                    lock (_gate)
                        item = TryDequeue(channel);

                    if (item is null)
                    {
                        await Task.Delay(20, ct);
                        continue;
                    }

                    await ProcessItemAsync(item, channel, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Pack6 通道[{name}]异常: {ex.Message}", false);
                    Emit($"通道[{name}]异常: {ex.Message}");
                }
            }
        }

        private async Task TimeoutLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(500, ct);
                    await ScanMissingTimeoutsAsync(ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warn($"缺张超时扫描异常: {ex.Message}", false);
                }
            }
        }

        /// <summary>
        /// 仅从提交头取本通道待检坑；头产品未齐套（临时 PLC）前不跑下一包。
        /// 队头已结案/无桶/僵死时踢出，避免堵死后续。
        /// </summary>
        private Pack6IngressItem? TryDequeue(Pack6DetectChannelKind channel)
        {
            while (_order.Count > 0)
            {
                var key = _order[0];
                if (!_buckets.TryGetValue(key, out var bucket) ||
                    bucket.Abandoned ||
                    _aggregator.IsClosed(key))
                {
                    if (!TryEvictDeadHead_NoLock(out var dropped))
                        return null;
                    foreach (var d in dropped)
                    {
                        try { _ingress.NotifyProcessed(d.FilePath); }
                        catch { /* ignore */ }
                    }
                    EnsureHeadTimeoutArmed_NoLock();
                    continue;
                }

                var q = bucket.Queues[channel];
                if (q.Count == 0)
                    return null;

                bucket.InFlight[channel]++;
                return q.Dequeue();
            }

            return null;
        }

        private async Task ProcessItemAsync(
            Pack6IngressItem item,
            Pack6DetectChannelKind channel,
            CancellationToken ct)
        {
            var key = Pack6WorkClassifier.ResolveProductKey(item);
            try
            {
                if (_aggregator.IsClosed(key))
                {
                    _ingress.NotifyProcessed(item.FilePath);
                    return;
                }

                lock (_gate)
                {
                    if (_buckets.TryGetValue(key, out var b) && b.Abandoned)
                    {
                        _ingress.NotifyProcessed(item.FilePath);
                        return;
                    }
                }

                var frame = await _vision.DetectAsync(item, ct);
                frame = ApplyBarcodeRules(frame, item, _configService.Current);

                var packKey = string.IsNullOrWhiteSpace(frame.ProductCode)
                    ? key
                    : frame.ProductCode.Trim().ToUpperInvariant();

                if (_aggregator.IsClosed(packKey))
                {
                    _ingress.NotifyProcessed(item.FilePath);
                    return;
                }

                // 三码 OK/NG 在整包结案 UpdateThreeCodeStat 计一次，不按帧累加
                try
                {
                    _imageArchive.ArchiveFrame(_configService.Current, frame);
                    _imageArchive.PurgeIfNeeded(_configService.Current);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"图像归档异常: {ex.Message}", false);
                }

                FrameCompleted?.Invoke(frame);
                _ingress.NotifyProcessed(item.FilePath);

                if (frame.OcrMode != Pack6OcrMode.None && ++_framesSinceGc >= 40)
                {
                    _framesSinceGc = 0;
                    GC.Collect(2, GCCollectionMode.Optimized, blocking: false);
                }

                var ruleHint = frame.Defects.Count == 0
                    ? string.Empty
                    : $" | {string.Join(';', frame.Defects.Take(3))}";
                var fileHint = string.IsNullOrWhiteSpace(frame.FilePath)
                    ? string.Empty
                    : $" | {System.IO.Path.GetFileName(frame.FilePath)}";
                Emit($"[{frame.StationName}] {frame.ProductCode} => {(frame.IsOk ? "OK" : "NG")}" +
                     (string.IsNullOrWhiteSpace(frame.ProductSN) ? string.Empty : $" SN={frame.ProductSN}") +
                     ruleHint + fileHint);
                _ = _store.AppendFrameResultAsync(_configService.Current, frame);

                _ = _aggregator.ApplyFrame(frame, MissingTimeout());

                lock (_gate)
                {
                    if (_buckets.TryGetValue(key, out var bucket) && !bucket.Abandoned)
                        bucket.TouchProgress();
                }
            }
            finally
            {
                lock (_gate)
                {
                    if (_buckets.TryGetValue(key, out var bucket) &&
                        bucket.InFlight.TryGetValue(channel, out var n) &&
                        n > 0)
                        bucket.InFlight[channel] = n - 1;
                }
            }
        }

        /// <summary>仅对提交头产品计缺张：已武装计时 + 各通道队列空 + 无在检 + 未齐套 + 空闲超时；结案前先按 SN 补扫一次。</summary>
        private async Task ScanMissingTimeoutsAsync(CancellationToken ct)
        {
            var limit = MissingTimeout();
            List<Pack6IngressItem> droppedLate = [];
            string? timeoutKey = null;
            string? dateHint = null;

            lock (_gate)
            {
                while (TryEvictDeadHead_NoLock(out var dropped))
                {
                    if (dropped.Count > 0)
                        droppedLate.AddRange(dropped);
                }

                EnsureHeadTimeoutArmed_NoLock();
                if (_order.Count > 0)
                {
                    var headKey = _order[0];
                    if (_buckets.TryGetValue(headKey, out var head) &&
                        ShouldMissingTimeout_NoLock(head, headKey, DateTime.UtcNow, limit))
                    {
                        timeoutKey = headKey;
                        dateHint = head.DateFolderHint;
                    }
                }
            }

            foreach (var item in droppedLate)
            {
                try { _ingress.NotifyProcessed(item.FilePath); }
                catch { /* ignore */ }
            }

            if (timeoutKey is null)
                return;

            // 缺张结案前再补扫一次：中途开检漏图 / Watcher 丢事件
            try
            {
                var n = await _ingress.BackfillProductFolderAsync(timeoutKey, dateHint, ct)
                    .ConfigureAwait(false);
                if (n > 0)
                {
                    Emit($"缺张前补扫 {timeoutKey}: 新入队 {n} 张，暂缓缺张结案");
                    lock (_gate)
                    {
                        if (_buckets.TryGetValue(timeoutKey, out var bucket) && !bucket.Abandoned)
                            bucket.TouchProgress();
                    }
                    return;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.Warn($"缺张前补扫异常 {timeoutKey}: {ex.Message}", false);
            }

            ForceTimeoutProduct(timeoutKey, limit);
        }

        /// <summary>
        /// 踢掉已结案/无桶队头；若队头仅 Abandoned 且已在 commitReady，交给 FlushCommits，不在此踢。
        /// 调用方须已持有 _gate。返回 true 表示踢出了队头（不自动武装下一头，由调用方 Ensure）。
        /// </summary>
        private bool TryEvictDeadHead_NoLock(out List<Pack6IngressItem> dropped)
        {
            dropped = [];
            if (_order.Count == 0)
                return false;

            var key = _order[0];
            var closed = _aggregator.IsClosed(key);
            var hasBucket = _buckets.TryGetValue(key, out var bucket);

            // 正常提交头
            if (!closed && hasBucket && bucket is not null && !bucket.Abandoned)
                return false;

            // 已 Abandoned 且等待 Flush 落盘提交：勿抢踢，避免与 FlushCommits 竞态
            if (_commitReady.ContainsKey(key))
                return false;

            if (hasBucket && bucket is not null)
            {
                bucket.Abandoned = true;
                foreach (var kind in Pack6WorkClassifier.AllChannels)
                {
                    var q = bucket.Queues[kind];
                    while (q.Count > 0)
                        dropped.Add(q.Dequeue());
                }
            }

            _order.RemoveAt(0);
            _buckets.Remove(key);
            Emit(closed
                ? $"踢出已结案产品 {key}（迟到入队），释放提交坑位"
                : $"踢出无效提交头 {key}，释放提交坑位");
            return true;
        }

        private bool ShouldMissingTimeout_NoLock(
            ProductBucket bucket,
            string key,
            DateTime now,
            TimeSpan limit)
        {
            if (bucket.Abandoned || !bucket.TimeoutArmed)
                return false;
            if (_order.Count == 0 || !string.Equals(_order[0], key, StringComparison.OrdinalIgnoreCase))
                return false;
            if (bucket.TotalQueued > 0 || bucket.TotalInFlight > 0)
                return false;
            if (_aggregator.IsClosed(key))
                return false;

            var status = _aggregator.GetInFlight()
                .FirstOrDefault(p => string.Equals(p.ProductCode, key, StringComparison.OrdinalIgnoreCase));
            if (status is not null && status.CheckAllQueuesCompleted())
                return false;

            return (now - bucket.LastProgressUtc) >= limit;
        }

        private void ForceTimeoutProduct(string key, TimeSpan limit)
        {
            List<Pack6IngressItem> dropped = [];
            lock (_gate)
            {
                if (!_buckets.TryGetValue(key, out var bucket) || bucket.Abandoned)
                    return;
                if (!ShouldMissingTimeout_NoLock(bucket, key, DateTime.UtcNow, limit))
                    return;
                bucket.Abandoned = true;
                foreach (var kind in Pack6WorkClassifier.AllChannels)
                {
                    var q = bucket.Queues[kind];
                    while (q.Count > 0)
                        dropped.Add(q.Dequeue());
                }
            }

            foreach (var item in dropped)
            {
                try
                {
                    _ingress.NotifyProcessed(item.FilePath);
                }
                catch
                {
                    /* ignore */
                }
            }

            var reason = $"缺张超时(>{limit.TotalSeconds:0}s)";
            Emit(dropped.Count > 0
                ? $"缺张超时 {key}: {limit.TotalSeconds:0}s，顺带丢弃迟到入队 {dropped.Count} 张，强制整包提交"
                : $"缺张超时 {key}: {limit.TotalSeconds:0}s 且未齐套，强制整包提交");
            var result = _aggregator.ForceComplete(key, timedOut: true, reason);
            if (result is null)
            {
                bool needSynthetic;
                lock (_gate)
                    needSynthetic = !_commitReady.ContainsKey(key) && _order.Contains(key);
                if (needSynthetic)
                {
                    var missing = new Pack6ProductStatus(key, Pack6DefaultConfig.QueueRequirements)
                        .DescribeMissingShots();
                    OnAggregatorProductCompleted(new Pack6ProductResult
                    {
                        ProductCode = key,
                        IsOk = false,
                        TimedOut = true,
                        Defects = [$"{reason}；{missing}"],
                        CompletedAt = DateTime.Now
                    });
                }
            }
        }

        private void OnAggregatorProductCompleted(Pack6ProductResult result)
        {
            var key = string.IsNullOrWhiteSpace(result.ProductCode)
                ? "UNKNOWN"
                : result.ProductCode.Trim().ToUpperInvariant();
            lock (_gate)
            {
                _commitReady[key] = result;
                if (_buckets.TryGetValue(key, out var b))
                    b.Abandoned = true;
            }

            _ = FlushCommitsAsync();
        }

        private async Task FlushCommitsAsync()
        {
            await _flushLock.WaitAsync();
            try
            {
                while (true)
                {
                    Pack6ProductResult? next;
                    lock (_gate)
                    {
                        if (_order.Count == 0)
                            return;
                        var head = _order[0];
                        if (!_commitReady.Remove(head, out next))
                            return;
                        _order.RemoveAt(0);
                        _buckets.Remove(head);
                        // 上一坑清除 → 下一产品进入提交坑位并开始缺张计时
                        EnsureHeadTimeoutArmed_NoLock();
                    }

                    await PersistProductAsync(next);
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }

        /// <summary>调用方须已持有 _gate。</summary>
        private void EnsureHeadTimeoutArmed_NoLock()
        {
            while (_order.Count > 0)
            {
                var headKey = _order[0];
                if (_aggregator.IsClosed(headKey) ||
                    !_buckets.TryGetValue(headKey, out var head) ||
                    head.Abandoned)
                {
                    // 等待 Flush 的 Abandoned 头不要在此处硬踢
                    if (_commitReady.ContainsKey(headKey))
                        return;
                    if (!TryEvictDeadHead_NoLock(out _))
                        return;
                    continue;
                }

                if (head.TimeoutArmed)
                    return;
                head.TimeoutArmed = true;
                head.TouchProgress();
                Emit($"产品 {headKey} 进入提交坑位，开始缺张计时({_configService.Current.PackMissingTimeoutSeconds}s)");
                return;
            }
        }

        private async Task PersistProductAsync(Pack6ProductResult result)
        {
            try
            {
                await _store.AppendProductResultAsync(_configService.Current, result);
                UpdateThreeCodeStat(result);
                UpdateYield(result);
                ProductCompleted?.Invoke(result);
                // 无 PLC 前：整包齐套/超时提交视为临时放行信号，下一包才可出队检测
                Emit($"Pack完成 {result.ProductCode} => {(result.IsOk ? "OK" : "NG")}" +
                     (result.TimedOut ? " (缺张超时)" : string.Empty) +
                     "｜临时PLC：本包检测完毕，可开下一包");
            }
            catch (Exception ex)
            {
                _logger.Warn($"落盘 Pack 结果失败: {ex.Message}", false);
            }
        }

        private TimeSpan MissingTimeout()
        {
            var sec = Math.Clamp(_configService.Current.PackMissingTimeoutSeconds, 5, 3600);
            return TimeSpan.FromSeconds(sec);
        }

        private void ClearBuckets()
        {
            lock (_gate)
            {
                _order.Clear();
                _buckets.Clear();
                _commitReady.Clear();
            }
        }

        private static async Task AwaitQuiet(Task? task)
        {
            if (task is null)
                return;
            try { await task; }
            catch { /* ignore */ }
        }

        private Pack6FrameResult ApplyBarcodeRules(
            Pack6FrameResult frame,
            Pack6IngressItem item,
            Pack6Config config)
        {
            if (frame.OcrMode == Pack6OcrMode.None)
                return frame;
            if (frame.OcrMode == Pack6OcrMode.WhiteLabelAndNameplate)
            {
                if (string.IsNullOrWhiteSpace(frame.ProductSN) && string.IsNullOrWhiteSpace(frame.NameplateProductSN))
                    return frame;
            }
            else if (string.IsNullOrWhiteSpace(frame.ProductSN))
            {
                return frame;
            }

            var folder = !string.IsNullOrWhiteSpace(frame.FolderSn)
                ? frame.FolderSn
                : item.ProductCode ?? Pack6FileNameParser.ResolveFolderSn(item.FilePath) ?? string.Empty;
            folder = folder.Trim().ToUpperInvariant();

            var rule = Pack6BarcodeRuleValidator.Validate(
                frame.ProductSN,
                folder,
                frame.QrData,
                frame.OcrMode,
                config,
                frame.PartSN,
                frame.Version,
                frame.NameplateProductSN);

            var packCode = !string.IsNullOrWhiteSpace(folder) ? folder : frame.ProductCode;

            if (rule.IsOk)
            {
                if (string.Equals(packCode, frame.ProductCode, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(folder, frame.FolderSn, StringComparison.OrdinalIgnoreCase))
                    return frame;

                return CloneFrame(frame, packCode, folder, frame.IsOk, frame.Defects.ToList(),
                    frame.PreviewImagePath, frame.NgBanner);
            }

            var defects = frame.Defects.Concat(rule.Failures).Distinct().ToList();
            var preview = frame.PreviewImagePath;
            if (!string.IsNullOrWhiteSpace(rule.NgBanner))
            {
                var rewritten = Pack6ResultOverlayHelper.TryWrite(
                    config,
                    item,
                    item.FilePath,
                    frame.OverlayBoxes,
                    algorithmError: frame.IsVisionAlgorithmError,
                    detailMessage: frame.EngineMessage,
                    ngBanner: rule.NgBanner,
                    isOk: false);
                if (!string.IsNullOrWhiteSpace(rewritten))
                    preview = rewritten;
            }

            return CloneFrame(frame, packCode, folder, isOk: false, defects, preview, rule.NgBanner);
        }

        private static Pack6FrameResult CloneFrame(
            Pack6FrameResult frame,
            string productCode,
            string folderSn,
            bool isOk,
            IReadOnlyList<string> defects,
            string previewPath,
            string ngBanner) =>
            new()
            {
                FilePath = frame.FilePath,
                QueueName = frame.QueueName,
                ProductCode = productCode,
                CameraName = frame.CameraName,
                ShotIndex = frame.ShotIndex,
                FolderSn = folderSn,
                IsOk = isOk,
                Defects = defects,
                StationName = frame.StationName,
                DetectedAt = frame.DetectedAt,
                FromSimulation = frame.FromSimulation,
                OcrMode = frame.OcrMode,
                QrData = frame.QrData,
                PartSN = frame.PartSN,
                ProductSN = frame.ProductSN,
                NameplateProductSN = frame.NameplateProductSN,
                Version = frame.Version,
                EngineMessage = frame.EngineMessage,
                PreviewImagePath = previewPath,
                IsVisionAlgorithmError = frame.IsVisionAlgorithmError,
                NgBanner = ngBanner,
                OverlayBoxes = frame.OverlayBoxes
            };

        /// <summary>前1+长左2-2+前3 整套算 1 次 OK/NG；未做码检的包不计。</summary>
        private void UpdateThreeCodeStat(Pack6ProductResult result)
        {
            if (!result.ThreeCodeChecked)
                return;

            RunOnUi(() =>
            {
                var row = _stats.FirstOrDefault(s => s.Station == "三码校验");
                if (row is null)
                    return;
                row.Total++;
                if (result.ThreeCodeOk) row.OK++;
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

        private sealed class ProductBucket
        {
            public string Key { get; }
            public Dictionary<Pack6DetectChannelKind, Queue<Pack6IngressItem>> Queues { get; } = new();
            public Dictionary<Pack6DetectChannelKind, int> InFlight { get; } = new();
            public bool Abandoned { get; set; }
            /// <summary>已成为提交头（上一坑已清）后才武装缺张计时。</summary>
            public bool TimeoutArmed { get; set; }
            /// <summary>首帧路径解析出的日期夹（yyyyMMdd / yyyy-MM-dd），供 SN 补扫。</summary>
            public string? DateFolderHint { get; set; }
            public DateTime LastProgressUtc { get; private set; } = DateTime.UtcNow;

            public ProductBucket(string key)
            {
                Key = key;
                foreach (var kind in Pack6WorkClassifier.AllChannels)
                {
                    Queues[kind] = new Queue<Pack6IngressItem>();
                    InFlight[kind] = 0;
                }
            }

            public int TotalQueued
            {
                get
                {
                    var n = 0;
                    foreach (var q in Queues.Values)
                        n += q.Count;
                    return n;
                }
            }

            public int TotalInFlight
            {
                get
                {
                    var n = 0;
                    foreach (var v in InFlight.Values)
                        n += v;
                    return n;
                }
            }

            public void TouchProgress() => LastProgressUtc = DateTime.UtcNow;
        }
    }
}
