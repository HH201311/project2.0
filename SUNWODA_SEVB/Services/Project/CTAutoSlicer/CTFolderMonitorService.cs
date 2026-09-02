using System.IO;
using SUNWODA_SEVB.Core.Interfaces;

namespace SUNWODA_SEVB.Services.Project.CTAutoSlicer
{
    public class CTFolderMonitorService : IDisposable
    {
        private readonly ILoggerService<CTFolderMonitorService> _logger;
        private FileSystemWatcher? _watcher;

        public CTFolderMonitorService(ILoggerService<CTFolderMonitorService> logger)
        {
            _logger = logger;
        }

        public void Start(string folderPath)
        {
            Stop();
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                _logger.Warn($"CT 文件夹监控路径无效：{folderPath}");
                return;
            }

            _watcher = new FileSystemWatcher(folderPath)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
                Filter = string.Empty,
            };
            _watcher.Created += OnCreated;
            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _logger.Info($"CT 文件夹监控已启动：{folderPath}");
        }

        public void Stop()
        {
            if (_watcher == null)
            {
                return;
            }

            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnCreated;
            _watcher.Changed -= OnChanged;
            _watcher.Deleted -= OnDeleted;
            _watcher.Renamed -= OnRenamed;
            _watcher.Dispose();
            _watcher = null;
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            if (Directory.Exists(e.FullPath))
            {
                _logger.Info($"文件夹创建：{e.FullPath}");
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            var directoryInfo = new DirectoryInfo(e.FullPath);
            if (directoryInfo.Exists)
            {
                _logger.Info($"文件夹变化：{directoryInfo.FullName}");
            }
            else if (directoryInfo.Parent != null)
            {
                var fileNum = directoryInfo.Parent.GetFiles().Length;
                _logger.Info($"文件改变：文件夹{directoryInfo.Parent.FullName}中文件数为{fileNum}");
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            var directoryInfo = new DirectoryInfo(e.FullPath);
            if (directoryInfo.Parent != null)
            {
                var fileNum = directoryInfo.Parent.GetFiles().Length;
                _logger.Info($"文件/文件夹删除：文件夹{directoryInfo.Parent.FullName}中文件数为{fileNum}");
            }
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            var directoryInfo = new DirectoryInfo(e.FullPath);
            if (directoryInfo.Exists)
            {
                _logger.Info($"文件夹重命名：{directoryInfo.FullName}");
            }
            else if (directoryInfo.Parent != null)
            {
                var fileNum = directoryInfo.Parent.GetFiles().Length;
                _logger.Info($"文件重命名：文件夹{directoryInfo.Parent.FullName}中文件数为{fileNum}");
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
