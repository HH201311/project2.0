using System.Collections.Concurrent;
using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.YWSIX;
using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Services.Project.YWSIX
{
    /// <summary>
    /// 监视义乌 SaveImage 根目录 OK 原图新文件入队（FileSystemWatcher）。
    /// 路径：{根}\{日期}\{SN}\{相机}\原始图像\；会话内 _seen 防重复。
    /// 支持按 SN 补扫：产品首次入队、缺张结案前各扫一次当日日期夹。
    /// 入队前等待文件写稳（体积连续不变 + JPEG 尾部 EOI），避免半截图下半灰；
    /// EOI 迟迟读不到则打日志后仍入队，避免卡死丢检。
    /// </summary>
    public sealed class Pack6IngressService : IYWSIXPack6IngressService
    {
        /// <summary>写稳判定：连续相同 Length 次数。</summary>
        private const int StablePollsRequired = 5;
        /// <summary>轮询间隔。</summary>
        private static readonly TimeSpan StablePollInterval = TimeSpan.FromMilliseconds(200);
        /// <summary>单文件最长等待（网络盘大图 + 等 EOI）。</summary>
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(30);
        /// <summary>过小视为未写完。</summary>
        private const long MinReadyBytes = 256;

        private static readonly string[] ImageExtensions =
            [".jpg", ".jpeg", ".bmp", ".png"];

        /// <summary>写稳/EOI 等待结果。</summary>
        internal enum FileReadyOutcome
        {
            /// <summary>不可用，应跳过。</summary>
            NotReady = 0,
            /// <summary>体积稳定且（非 JPEG 或已有 EOI）。</summary>
            Ready = 1,
            /// <summary>超时仍无 JPEG EOI，允许继续检测但需打日志。</summary>
            ReadyWithoutEoi = 2
        }

        private readonly ILoggerService<Pack6IngressService> _logger;
        private readonly List<FileSystemWatcher> _watchers = [];
        private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _runCts;
        private string _saveImageRoot = string.Empty;

        public event Action<Pack6IngressItem>? ItemEnqueued;
        public event Action<string>? StatusEmitted;
        public bool IsRunning { get; private set; }

        public Pack6IngressService(ILoggerService<Pack6IngressService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Pack6Config config)
        {
            StopInternal();
            _runCts = new CancellationTokenSource();
            _saveImageRoot = (config.SaveImageRoot ?? config.YOkRootPath)?.Trim() ?? string.Empty;

            var filter = string.IsNullOrWhiteSpace(config.ImageFilter)
                ? "*.jpg;*.bmp;*.png;*.jpeg"
                : config.ImageFilter;

            TryWatch(_saveImageRoot, filter, "All");

            IsRunning = true;
            EmitStatus($"已监视：SaveImage={_saveImageRoot}（仅OK；写稳入队；按SN补扫；本步不调DLL）");
            _logger.Info("YWSIX 收图监视已启动（单根 SaveImage + 按 SN 补扫，写稳后入队）", false);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopInternal();
            return Task.CompletedTask;
        }

        public void EnqueueSimulated(string queueName, string? productCode = null)
        {
            var code = string.IsNullOrWhiteSpace(productCode)
                ? $"SIM-{DateTime.Now:HHmmss}"
                : productCode!;
            var cam = Pack6CameraCatalog.ByQueueName.TryGetValue(queueName, out var def)
                ? def.CameraName
                : queueName;
            var item = new Pack6IngressItem
            {
                FilePath = $"sim://{queueName}/{code}.jpg",
                QueueName = queueName,
                ProductCode = code,
                CameraName = cam,
                ShotIndex = 1
            };
            ItemEnqueued?.Invoke(item);
        }

        /// <inheritdoc />
        public async Task<int> BackfillProductFolderAsync(
            string productSn,
            string? dateFolderHint = null,
            CancellationToken ct = default)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(productSn))
                return 0;

            var sn = productSn.Trim();
            var dateAliases = Pack6FileNameParser.ExpandDateFolderAliases(dateFolderHint);
            var candidates = new List<(string Path, string Role, DateTime WriteUtc)>();

            CollectSnFolderFiles(_saveImageRoot, "All", sn, dateAliases, candidates);

            if (candidates.Count == 0)
                return 0;

            candidates.Sort((a, b) => a.WriteUtc.CompareTo(b.WriteUtc));

            var enqueued = 0;
            foreach (var (path, role, _) in candidates)
            {
                if (ct.IsCancellationRequested || !IsRunning)
                    break;

                // 已在会话 _seen（Watcher 或先前补扫）则跳过，避免重复检
                if (!_seen.TryAdd(path, 0))
                    continue;

                var fileName = Path.GetFileName(path);
                if (fileName.Contains("-定位-", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("1-定位", StringComparison.OrdinalIgnoreCase))
                {
                    _seen.TryRemove(path, out _);
                    continue;
                }

                try
                {
                    var ready = await WaitForFileReadyAsync(path, ReadyTimeout, ct).ConfigureAwait(false);
                    if (ct.IsCancellationRequested || !IsRunning)
                    {
                        _seen.TryRemove(path, out _);
                        break;
                    }

                    if (ready == FileReadyOutcome.NotReady)
                    {
                        _seen.TryRemove(path, out _);
                        _logger.Warn($"SN补扫写稳超时，跳过: {fileName}", false);
                        continue;
                    }

                    if (ready == FileReadyOutcome.ReadyWithoutEoi)
                    {
                        _logger.Warn(
                            $"SN补扫 JPEG 尾部无 EOI(FF D9)，超时后仍入队继续检测: {fileName}",
                            false);
                    }

                    EnqueueReadyFile(path, role);
                    enqueued++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _seen.TryRemove(path, out _);
                    break;
                }
                catch (Exception ex)
                {
                    _seen.TryRemove(path, out _);
                    _logger.Warn($"SN补扫入队异常 {fileName}: {ex.Message}", false);
                }
            }

            if (enqueued > 0)
                EmitStatus($"SN补扫 {sn}: 新入队 {enqueued} 张（日期={string.Join('|', dateAliases)}）");

            return enqueued;
        }

        private static bool IsImageExtension(string? ext)
        {
            if (string.IsNullOrEmpty(ext))
                return false;
            foreach (var e in ImageExtensions)
            {
                if (e.Equals(ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void CollectSnFolderFiles(
            string okRoot,
            string role,
            string sn,
            IReadOnlyList<string> dateAliases,
            List<(string Path, string Role, DateTime WriteUtc)> sink)
        {
            if (string.IsNullOrWhiteSpace(okRoot))
                return;

            foreach (var date in dateAliases)
            {
                string folder;
                try
                {
                    folder = Path.Combine(okRoot, date, sn);
                }
                catch
                {
                    continue;
                }

                if (!Directory.Exists(folder))
                    continue;

                IEnumerable<string> files;
                try
                {
                    // 义乌：SN\{相机}\原始图像\*.jpg
                    files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);
                    if (!IsImageExtension(ext))
                        continue;

                    DateTime writeUtc;
                    try
                    {
                        writeUtc = File.GetLastWriteTimeUtc(file);
                    }
                    catch
                    {
                        writeUtc = DateTime.MinValue;
                    }

                    sink.Add((file, role, writeUtc));
                }
            }
        }

        public void Dispose() => StopInternal();

        private void TryWatch(string path, string filterCsv, string role)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (!Directory.Exists(path))
            {
                try
                {
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"监视目录不存在且无法创建 {path}: {ex.Message}", false);
                    return;
                }
            }

            var filters = filterCsv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (filters.Length == 0)
                filters = ["*.*"];

            foreach (var filter in filters)
            {
                var watcher = new FileSystemWatcher(path)
                {
                    Filter = filter,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };
                watcher.Created += (_, e) => OnFile(e.FullPath, role);
                watcher.Renamed += (_, e) => OnFile(e.FullPath, role);
                _watchers.Add(watcher);
            }

            _logger.Info($"监视 {role}: {path}", false);
        }

        /// <summary>Created/Renamed：先占坑防重，后台等写稳再入队（勿阻塞 Watcher 线程）。</summary>
        private void OnFile(string fullPath, string role)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(fullPath))
                return;

            if (!_seen.TryAdd(fullPath, 0))
                return;

            var fileName = Path.GetFileName(fullPath);
            if (fileName.Contains("-定位-", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("1-定位", StringComparison.OrdinalIgnoreCase))
            {
                _seen.TryRemove(fullPath, out _);
                return;
            }

            var ct = _runCts?.Token ?? CancellationToken.None;
            _ = Task.Run(() => WaitAndEnqueueAsync(fullPath, role, ct), ct);
        }

        private async Task WaitAndEnqueueAsync(string fullPath, string role, CancellationToken ct)
        {
            try
            {
                var ready = await WaitForFileReadyAsync(fullPath, ReadyTimeout, ct).ConfigureAwait(false);
                if (ct.IsCancellationRequested || !IsRunning)
                {
                    _seen.TryRemove(fullPath, out _);
                    return;
                }

                if (ready == FileReadyOutcome.NotReady)
                {
                    _seen.TryRemove(fullPath, out _);
                    _logger.Warn($"收图写稳超时，跳过: {Path.GetFileName(fullPath)}", false);
                    return;
                }

                if (ready == FileReadyOutcome.ReadyWithoutEoi)
                {
                    _logger.Warn(
                        $"收图 JPEG 尾部无 EOI(FF D9)，超时后仍入队继续检测: {Path.GetFileName(fullPath)}",
                        false);
                }

                EnqueueReadyFile(fullPath, role);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _seen.TryRemove(fullPath, out _);
            }
            catch (Exception ex)
            {
                _seen.TryRemove(fullPath, out _);
                _logger.Warn($"收图入队异常 {Path.GetFileName(fullPath)}: {ex.Message}", false);
            }
        }

        private void EnqueueReadyFile(string fullPath, string role)
        {
            var fileName = Path.GetFileName(fullPath);

            // 义乌：OK-{相机}-{张序}-{时间}-{SN}.jpg
            if (!Pack6FileNameParser.TryParseIngress(fullPath, role, out var parsed))
            {
                _logger.Warn($"非标准义乌文件名或非OK图，跳过入队/存图: {fileName}", false);
                return;
            }

            if (!Pack6CameraCatalog.TryGetByCameraName(parsed.CameraName, out var def))
            {
                _logger.Warn(
                    $"相机名不在六相机清单，跳过入队/存图: {fileName} (cam={parsed.CameraName})",
                    false);
                return;
            }

            if (parsed.ShotIndex < 1 || parsed.ShotIndex > def.DesignShotCount)
            {
                _logger.Warn(
                    $"张序超出设计张数，跳过入队/存图: {fileName} (cam={def.CameraName} shot={parsed.ShotIndex} 设计={def.DesignShotCount})",
                    false);
                return;
            }

            var product = Pack6FileNameParser.ResolveFolderSn(fullPath) ?? parsed.ProductSn;

            var item = new Pack6IngressItem
            {
                FilePath = fullPath,
                QueueName = def.QueueName,
                ProductCode = product,
                CameraName = def.CameraName,
                ShotIndex = parsed.ShotIndex
            };
            ItemEnqueued?.Invoke(item);
        }

        /// <summary>
        /// 体积连续 StablePollsRequired 次不变且可读；JPEG 另等尾部 EOI(FF D9)。
        /// 超时仍无 EOI 时返回 <see cref="FileReadyOutcome.ReadyWithoutEoi"/>（由调用方打日志后继续检测）。
        /// </summary>
        internal static async Task<FileReadyOutcome> WaitForFileReadyAsync(
            string fullPath,
            TimeSpan timeout,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return FileReadyOutcome.NotReady;

            var needEoi = IsJpegPath(fullPath);
            var deadline = DateTime.UtcNow + timeout;
            long lastLen = -1;
            var stable = 0;

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (!File.Exists(fullPath))
                {
                    lastLen = -1;
                    stable = 0;
                    await Task.Delay(StablePollInterval, ct).ConfigureAwait(false);
                    continue;
                }

                if (!TryGetLength(fullPath, out var len) || len < MinReadyBytes)
                {
                    lastLen = -1;
                    stable = 0;
                    await Task.Delay(StablePollInterval, ct).ConfigureAwait(false);
                    continue;
                }

                if (len == lastLen)
                    stable++;
                else
                {
                    lastLen = len;
                    stable = 1;
                }

                if (stable >= StablePollsRequired && TryOpenReadable(fullPath))
                {
                    if (!needEoi || TryHasJpegEoi(fullPath))
                        return FileReadyOutcome.Ready;

                    // 体积已稳但尚无 EOI：继续等到超时（网络盘偶发尾部晚落盘）
                }

                await Task.Delay(StablePollInterval, ct).ConfigureAwait(false);
            }

            // 超时兜底：可读则仍入队；JPEG 无 EOI 时标记 ReadyWithoutEoi
            if (!TryGetLength(fullPath, out var finalLen) ||
                finalLen < MinReadyBytes ||
                !TryOpenReadable(fullPath))
                return FileReadyOutcome.NotReady;

            if (needEoi && !TryHasJpegEoi(fullPath))
                return FileReadyOutcome.ReadyWithoutEoi;

            return FileReadyOutcome.Ready;
        }

        private static bool IsJpegPath(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>JPEG 文件末尾是否为 EOI 标记 FF D9（只读 2 字节，开销可忽略）。</summary>
        internal static bool TryHasJpegEoi(string path)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 4)
                    return false;

                // 可选：开头应为 SOI FF D8
                var head = new byte[2];
                if (fs.Read(head, 0, 2) != 2 || head[0] != 0xFF || head[1] != 0xD8)
                    return false;

                fs.Seek(-2, SeekOrigin.End);
                var tail = new byte[2];
                if (fs.Read(tail, 0, 2) != 2)
                    return false;
                return tail[0] == 0xFF && tail[1] == 0xD9;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetLength(string path, out long length)
        {
            length = 0;
            try
            {
                length = new FileInfo(path).Length;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryOpenReadable(string path)
        {
            try
            {
                // 先试独占：确认相机进程已关写句柄
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                    return true;
            }
            catch (IOException)
            {
                // 网络盘常见无法独占，退化为共享读
            }
            catch (UnauthorizedAccessException)
            {
                // fall through
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return fs.Length >= MinReadyBytes;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>管线处理完后调用：本会话内仍保留路径防重复事件；仅做上限裁剪。</summary>
        public void NotifyProcessed(string fullPath)
        {
            PruneSeenIfNeeded();
        }

        private void PruneSeenIfNeeded()
        {
            if (_seen.Count < 5000)
                return;
            // 会话去重集过大时清空一半键（新文件路径仍会再入 _seen）
            var i = 0;
            foreach (var key in _seen.Keys)
            {
                if (_seen.TryRemove(key, out _) && ++i >= 2500)
                    break;
            }
        }

        private void EmitStatus(string msg)
        {
            _logger.Info(msg, false);
            StatusEmitted?.Invoke(msg);
        }

        private void StopInternal()
        {
            IsRunning = false;
            try
            {
                _runCts?.Cancel();
                _runCts?.Dispose();
            }
            catch
            {
                /* ignore */
            }
            _runCts = null;

            foreach (var w in _watchers)
            {
                try
                {
                    w.EnableRaisingEvents = false;
                    w.Dispose();
                }
                catch
                {
                    /* ignore */
                }
            }
            _watchers.Clear();
            _seen.Clear();
            _saveImageRoot = string.Empty;
        }
    }
}
