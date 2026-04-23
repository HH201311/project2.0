using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Models.Project.CTOL;
using System.IO;

namespace SUNWODA_SEVB.ViewModels.Pages.CTOL
{
    /// <summary>
    /// 实时通道：FileSystemWatcher 监听 Z:\LAB 及其所有子目录。
    /// SMB 网络盘注意：
    ///   - InternalBufferSize 调到最大 (64 KB)；
    ///   - 网络延迟可能导致部分事件丢失，兜底扫描负责补漏。
    /// </summary>
    public sealed class WatcherFileService : IDisposable
    {
        private readonly MonitorConfig _cfg;
        private readonly FileEventQueue _queue;
        private readonly HotDirectoryTracker _hot;
        private readonly ILoggerService _log;

        private FileSystemWatcher? _watcher;

        public WatcherFileService(
            MonitorConfig cfg,
            FileEventQueue queue,
            HotDirectoryTracker hot,
            ILoggerService log)
        {
            _cfg = cfg;
            _queue = queue;
            _hot = hot;
            _log = log;
        }

        public Task StartAsync()
        {
            if (!Directory.Exists(_cfg.WatchRoot))
            {
                _log.Error($"监控目录不存在: {_cfg.WatchRoot}");
                return Task.CompletedTask;
            }

            _watcher = new FileSystemWatcher(_cfg.WatchRoot)
            {
                Filter = _cfg.FilePattern,
                IncludeSubdirectories = true,
                // 最大内部缓冲（64 KB）降低内核丢事件概率
                InternalBufferSize = 65536,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            _watcher.Created += OnEvent;
            _watcher.Error += OnError;

            _log.Info($"文件监控服务启动: {_cfg.WatchRoot}");
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _watcher?.Dispose();
            return Task.CompletedTask;
        }

        // ─────────────────────────── handlers ─────────────────────────────

        private void OnEvent(object _, FileSystemEventArgs e)
        {
            Push(e.FullPath);
        }

        private void Push(string path)
        {
            _hot.Touch(path);
            _queue.Enqueue(new FileEvent(path, "Watcher"));
        }

        private void OnError(object _, ErrorEventArgs e)
        {
            _log.Error("FileSystemWatcher 错误", e.GetException());
            // 网络断线后 FSW 会停止工作；尝试重启
            TryRestart();
        }

        private void TryRestart()
        {
            try
            {
                if (_watcher is null) return;
                _watcher.EnableRaisingEvents = false;
                _watcher.EnableRaisingEvents = true;
                _log.Info("发送错误后文件监测服务重新启动");
            }
            catch (Exception ex)
            {
                _log.Error("不能重启文件监测服务", ex);
            }
        }

        public void Dispose() => _watcher?.Dispose();
    }
}
