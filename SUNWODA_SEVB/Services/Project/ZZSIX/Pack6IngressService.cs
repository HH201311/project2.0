using System.Collections.Concurrent;
using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 监视 Y/Z OK原图新文件入队（FileSystemWatcher）。
    /// 不做回看补扫、不写已处理索引；会话内 _seen 防重复事件。打开软件不自动开检。
    /// </summary>
    public sealed class Pack6IngressService : IZZSIXPack6IngressService
    {
        private readonly ILoggerService<Pack6IngressService> _logger;
        private readonly List<FileSystemWatcher> _watchers = [];
        private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.OrdinalIgnoreCase);

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

            var filter = string.IsNullOrWhiteSpace(config.ImageFilter)
                ? "*.jpg;*.bmp;*.png;*.jpeg"
                : config.ImageFilter;

            TryWatch(config.YOkRootPath, filter, "Y");
            TryWatch(config.ZOkRootPath, filter, "Z");

            IsRunning = true;
            EmitStatus($"已监视新文件：Y={config.YOkRootPath} | Z={config.ZOkRootPath}（无回看补扫/无已处理索引）");
            _logger.Info("Pack6收图监视已启动（仅新文件事件）", false);
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

        public void Dispose() => StopInternal();

        private void TryWatch(string path, string filterCsv, string role)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception ex)
            {
                _logger.Warn($"无法创建监视目录 {path}: {ex.Message}", false);
                return;
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

        /// <returns>是否真正入队。</returns>
        private bool OnFile(string fullPath, string role)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(fullPath))
                return false;

            if (!_seen.TryAdd(fullPath, 0))
                return false;

            var fileName = Path.GetFileName(fullPath);
            if (fileName.Contains("-定位-", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("1-定位", StringComparison.OrdinalIgnoreCase))
            {
                _seen.TryRemove(fullPath, out _);
                return false;
            }

            string queue;
            string camera = string.Empty;
            int shot = 0;
            string? product = Pack6FileNameParser.ResolveFolderSn(fullPath);

            if (Pack6FileNameParser.TryParseFileName(fileName, out var parsed))
            {
                camera = parsed.CameraName;
                shot = parsed.ShotIndex;
                product ??= parsed.ProductSn;
                if (Pack6CameraCatalog.TryGetByCameraName(camera, out var def))
                    queue = def.QueueName;
                else
                    queue = ResolveQueueFallback(fileName, role);
            }
            else
            {
                queue = ResolveQueueFallback(fileName, role);
            }

            var item = new Pack6IngressItem
            {
                FilePath = fullPath,
                QueueName = queue,
                ProductCode = product,
                CameraName = camera,
                ShotIndex = shot
            };
            ItemEnqueued?.Invoke(item);
            return true;
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

        internal static string ResolveQueueFallback(string fileName, string role)
        {
            var name = fileName ?? string.Empty;
            if (Contains(name, "上左", "upperleft")) return "UpperLeftQueue";
            if (Contains(name, "上右", "upperright")) return "UpperRightQueue";
            if (Contains(name, "长左1", "longleft1")) return "LongLeft1Queue";
            if (Contains(name, "长左2", "longleft2")) return "LongLeft2Queue";
            if (Contains(name, "长右1", "longright1")) return "LongRight1Queue";
            if (Contains(name, "长右2", "longright2")) return "LongRight2Queue";
            if (Contains(name, "底1", "bottom1", "bot1")) return "Bottom1Queue";
            if (Contains(name, "底2", "bottom2", "bot2")) return "Bottom2Queue";
            if (Contains(name, "底3", "bottom3", "bot3")) return "Bottom3Queue";
            if (Contains(name, "底4", "bottom4", "bot4")) return "Bottom4Queue";
            if (Contains(name, "后", "rear", "back")) return "RearQueue";
            if (Contains(name, "前", "front")) return "FrontQueue";
            return role.Equals("Z", StringComparison.OrdinalIgnoreCase)
                ? "UpperLeftQueue"
                : "FrontQueue";
        }

        private static bool Contains(string name, params string[] keys) =>
            keys.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

        private void EmitStatus(string msg)
        {
            _logger.Info(msg, false);
            StatusEmitted?.Invoke(msg);
        }

        private void StopInternal()
        {
            IsRunning = false;
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
        }
    }
}
