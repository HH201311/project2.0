using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;
using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// 本地 TSV 索引：{LocalPathPre}/state/processed_files.idx
    /// 不写 Y/Z 原图目录；对齐 CTOL「已处理」语义的轻量实现。
    /// </summary>
    public sealed class Pack6ProcessedFileStore : INJSIXPack6ProcessedStore
    {
        private readonly ILoggerService<Pack6ProcessedFileStore> _logger;
        private readonly ConcurrentDictionary<string, long> _map = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _ioLock = new();
        private string? _indexPath;
        private int _lookbackDays = 2;
        private int _marksSincePrune;

        public Pack6ProcessedFileStore(ILoggerService<Pack6ProcessedFileStore> logger)
        {
            _logger = logger;
        }

        public void Open(Pack6Config config)
        {
            Close();
            _lookbackDays = Math.Clamp(config.IngressLookbackDays, 1, 30);
            var stateDir = Path.Combine(config.LocalPathPre.Trim(), "state");
            Directory.CreateDirectory(stateDir);
            _indexPath = Path.Combine(stateDir, "processed_files.idx");

            if (!File.Exists(_indexPath))
            {
                File.WriteAllText(_indexPath, string.Empty, Encoding.UTF8);
                return;
            }

            var cutoffTicks = DateTime.UtcNow.AddDays(-_lookbackDays * 2).Ticks;
            var keep = new List<string>();
            lock (_ioLock)
            {
                foreach (var line in File.ReadLines(_indexPath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var parts = line.Split('\t');
                    if (parts.Length < 2)
                        continue;
                    var path = Normalize(parts[0]);
                    if (path.Length == 0)
                        continue;
                    if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
                        ticks = DateTime.UtcNow.Ticks;
                    if (ticks < cutoffTicks)
                        continue;
                    _map[path] = ticks;
                    keep.Add($"{path}\t{ticks.ToString(CultureInfo.InvariantCulture)}");
                }

                File.WriteAllLines(_indexPath, keep, Encoding.UTF8);
            }

            _logger.Info($"已处理索引已加载：{_map.Count} 条 → {_indexPath}", false);
        }

        public bool IsProcessed(string fullPath)
        {
            var key = Normalize(fullPath);
            return key.Length > 0 && _map.ContainsKey(key);
        }

        public void MarkProcessed(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) ||
                fullPath.StartsWith("sim://", StringComparison.OrdinalIgnoreCase))
                return;

            var key = Normalize(fullPath);
            if (key.Length == 0)
                return;

            var ticks = DateTime.UtcNow.Ticks;
            _map[key] = ticks;

            var path = _indexPath;
            if (path is null)
                return;

            lock (_ioLock)
            {
                try
                {
                    File.AppendAllText(
                        path,
                        $"{key}\t{ticks.ToString(CultureInfo.InvariantCulture)}{Environment.NewLine}",
                        Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"写入已处理标记失败: {ex.Message}", false);
                }
            }

            // 运行期定期裁剪，防止 _map / idx 无限增长
            if (++_marksSincePrune >= 800)
            {
                _marksSincePrune = 0;
                PruneInMemoryAndRewrite();
            }
        }

        public void Close()
        {
            _map.Clear();
            _indexPath = null;
            _marksSincePrune = 0;
        }

        private void PruneInMemoryAndRewrite()
        {
            var path = _indexPath;
            if (path is null)
                return;

            var cutoffTicks = DateTime.UtcNow.AddDays(-_lookbackDays * 2).Ticks;
            var keep = new List<string>();
            foreach (var kv in _map.ToArray())
            {
                if (kv.Value < cutoffTicks)
                {
                    _map.TryRemove(kv.Key, out _);
                    continue;
                }
                keep.Add($"{kv.Key}\t{kv.Value.ToString(CultureInfo.InvariantCulture)}");
            }

            lock (_ioLock)
            {
                try
                {
                    File.WriteAllLines(path, keep, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"裁剪已处理索引失败: {ex.Message}", false);
                }
            }
        }

        private static string Normalize(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
