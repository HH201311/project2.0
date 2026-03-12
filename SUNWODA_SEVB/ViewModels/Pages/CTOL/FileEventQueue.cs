using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Channels;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 统一事件队列。BoundedChannel 提供背压保护。
    /// 重复路径在写入前做快速内存去重（LRU 大小有限）。
    /// </summary>
    public sealed class FileEventQueue
    {
        private readonly Channel<FileEvent> _channel;
        private readonly ILoggerService _log;

        // 简易去重窗口：最近已入队的路径，避免 Watcher 短时间内重复触发
        private readonly LinkedList<string> _recentList = new();
        private readonly HashSet<string> _recentSet = new(StringComparer.OrdinalIgnoreCase);
        private const int DedupeWindow = 2000;
        private readonly object _dedupeLock = new();

        public ChannelReader<FileEvent> Reader => _channel.Reader;

        public FileEventQueue(MonitorConfig cfg, ILoggerService log)
        {
            _log = log;
            _channel = Channel.CreateBounded<FileEvent>(
                new BoundedChannelOptions(cfg.ChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = false,
                });
        }

        /// <summary>写入一个事件；如果路径在去重窗口内则静默跳过。</summary>
        public void Enqueue(FileEvent ev)
        {
            lock (_dedupeLock)
            {
                if (!_recentSet.Add(ev.FilePath))
                    return; // already queued recently

                _recentList.AddLast(ev.FilePath);
                if (_recentList.Count > DedupeWindow)
                {
                    _recentSet.Remove(_recentList.First!.Value);
                    _recentList.RemoveFirst();
                }
            }

            if (!_channel.Writer.TryWrite(ev))
                _log.Warn($"队列超载 – 丢弃 {ev.FilePath}");
        }

        /// <summary>将路径从去重窗口移除（处理完毕后调用，使重新扫描时可再入队）</summary>
        public void ReleaseDedup(string filePath)
        {
            lock (_dedupeLock)
            {
                if (_recentSet.Remove(filePath))
                    _recentList.Remove(filePath);
            }
        }
    }

    /// <summary>
    /// 记录"最近有 FSW 事件"的目录，供兜底扫描只扫热点目录。
    /// </summary>
    public sealed class HotDirectoryTracker
    {
        private readonly int _windowMinutes;
        // key = 目录全路径（ OK 级），value = 最后事件时间
        private readonly ConcurrentDictionary<string, DateTime> _dirs = new(StringComparer.OrdinalIgnoreCase);

        public HotDirectoryTracker(MonitorConfig cfg) => _windowMinutes = cfg.HotDirectoryWindowMinutes;

        public void Touch(string filePath)
        {
            // 取到 OK 级目录（即文件所在目录，如 ...OK\355125701G66027）
            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
                _dirs[dir] = DateTime.UtcNow;
        }

        /// <summary>返回窗口期内活跃目录列表，同时清理过期项。</summary>
        public IReadOnlyList<string> GetHotDirectories()
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-_windowMinutes);
            var result = new List<string>();
            foreach (var kv in _dirs)
            {
                if (kv.Value >= cutoff)
                    result.Add(kv.Key);
                else
                    _dirs.TryRemove(kv.Key, out _);
            }
            return result;
        }
    }
}
