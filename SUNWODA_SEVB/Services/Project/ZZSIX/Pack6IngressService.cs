using System.Collections.Concurrent;
using System.IO;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 监视 Top/Bottom/Side 三根目录；按文件名关键字路由到八窗队列。
    /// </summary>
    public sealed class Pack6IngressService : IZZSIXPack6IngressService
    {
        private readonly ILoggerService<Pack6IngressService> _logger;
        private readonly List<FileSystemWatcher> _watchers = [];
        private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.OrdinalIgnoreCase);
        private Pack6Config? _config;

        public event Action<Pack6IngressItem>? ItemEnqueued;
        public bool IsRunning { get; private set; }

        public Pack6IngressService(ILoggerService<Pack6IngressService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(Pack6Config config)
        {
            StopInternal();
            _config = config;

            TryWatch(config.TopCamPath, config.TopCamFilter, "Top");
            TryWatch(config.BottomCamPath, config.BottomCamFilter, "Bottom");
            TryWatch(config.SideCamPath, config.SideCamFilter, "Side");

            IsRunning = true;
            _logger.Info("Pack6收图监视已启动", false);
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
            var item = new Pack6IngressItem
            {
                FilePath = $"sim://{queueName}/{code}.jpg",
                QueueName = queueName,
                ProductCode = code
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

        private void OnFile(string fullPath, string role)
        {
            if (!IsRunning || string.IsNullOrWhiteSpace(fullPath))
                return;
            if (!_seen.TryAdd(fullPath, 0))
                return;

            var queue = ResolveQueue(Path.GetFileNameWithoutExtension(fullPath), role);
            var product = TryParseProductCode(Path.GetFileNameWithoutExtension(fullPath));
            var item = new Pack6IngressItem
            {
                FilePath = fullPath,
                QueueName = queue,
                ProductCode = product
            };
            ItemEnqueued?.Invoke(item);
        }

        /// <summary>
        /// 路由规则：文件名含关键字优先；否则按目录角色落到默认队列。
        /// </summary>
        internal static string ResolveQueue(string fileName, string role)
        {
            var name = fileName ?? string.Empty;
            if (Contains(name, "upper", "top", "上盖")) return "UpperQueue";
            if (Contains(name, "bottom1", "底1", "bot1")) return "Bottom1Queue";
            if (Contains(name, "bottom2", "底2", "bot2")) return "Bottom2Queue";
            if (Contains(name, "bottom3", "底3", "bot3")) return "Bottom3Queue";
            if (Contains(name, "front", "前")) return "FrontQueue";
            if (Contains(name, "rear", "后", "back")) return "RearQueue";
            if (Contains(name, "left", "左")) return "LeftQueue";
            if (Contains(name, "right", "右")) return "RightQueue";

            return role switch
            {
                "Top" => "UpperQueue",
                "Bottom" => "Bottom1Queue",
                _ => "FrontQueue"
            };
        }

        private static string? TryParseProductCode(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;
            // 常见：SN_窗口_时间戳；取第一段
            var parts = fileName.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : fileName;
        }

        private static bool Contains(string name, params string[] keys) =>
            keys.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

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
                    // ignore
                }
            }
            _watchers.Clear();
            _seen.Clear();
            _config = null;
        }
    }
}
