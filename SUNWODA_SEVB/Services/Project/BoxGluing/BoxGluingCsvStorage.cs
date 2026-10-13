using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Services.Project.BoxGluing
{
    /// <summary>
    /// 按出胶分文件、按天追加：
    /// {CsvRootPath}/yyyy年MM月dd日/yyyy_MM_dd_出胶N.csv
    /// </summary>
    public class BoxGluingCsvStorage : IBoxGluingCsvStorage, IDisposable
    {
        private readonly IBoxGluingConfigService _configService;
        private readonly ILoggerService<BoxGluingCsvStorage> _logger;
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private readonly Dictionary<int, StationFile> _files = new()
        {
            [1] = new StationFile(1),
            [2] = new StationFile(2),
            [3] = new StationFile(3)
        };

        public BoxGluingCsvStorage(
            IBoxGluingConfigService configService,
            ILoggerService<BoxGluingCsvStorage> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public async Task EnqueueAsync(int station, BoxGluingSampleRecord record, IReadOnlyList<string> headerLabels)
        {
            if (!_files.TryGetValue(station, out var file))
                return;

            var line = BuildLine(record);
            file.Header = "时间," + string.Join(",", headerLabels);
            file.Queue.Enqueue(line);
            Interlocked.Increment(ref file.RowsSinceBreak);
            var count = Interlocked.Increment(ref file.Queued);
            if (count >= 50)
                await FlushAsync();
        }

        public async Task FlushAsync()
        {
            await _flushLock.WaitAsync();
            try
            {
                foreach (var file in _files.Values)
                    await FlushStationAsync(file);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        public async Task WriteSegmentBreakAsync(int station)
        {
            if (!_files.TryGetValue(station, out var file))
                return;

            await _flushLock.WaitAsync();
            try
            {
                await FlushStationAsync(file);
                if (Interlocked.Exchange(ref file.RowsSinceBreak, 0) <= 0)
                    return;

                var path = file.FilePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return;

                await File.AppendAllTextAsync(
                    path,
                    Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                _logger.Error($"BoxGluing 出胶{station} CSV 段分隔写入失败", ex, false);
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private async Task FlushStationAsync(StationFile file)
        {
            if (file.Queue.IsEmpty)
                return;

            try
            {
                var lines = new List<string>(64);
                while (file.Queue.TryDequeue(out var line))
                    lines.Add(line);

                if (lines.Count == 0)
                    return;

                Interlocked.Add(ref file.Queued, -lines.Count);
                var header = file.Header ?? "时间";
                var path = EnsureFile(file, header);
                await File.AppendAllLinesAsync(
                    path,
                    lines,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                _logger.Error($"BoxGluing 出胶{file.Station} CSV 落盘失败", ex, false);
            }
        }

        private string EnsureFile(StationFile file, string header)
        {
            var now = DateTime.Now;
            var root = _configService.Current.CsvRootPath;
            if (string.IsNullOrWhiteSpace(root))
                root = @"D:\BoxGluingData";

            var dayFolder = Path.Combine(root, now.ToString("yyyy年MM月dd日"));
            Directory.CreateDirectory(dayFolder);
            var filePath = ResolvePathForHeader(dayFolder, file.Station, now, header);

            if (file.Date.Date != now.Date
                || !string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                file.Date = now.Date;
                file.FilePath = filePath;
            }

            if (!File.Exists(filePath))
            {
                var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                File.WriteAllText(filePath, header + Environment.NewLine, utf8Bom);
            }

            return filePath;
        }

        /// <summary>
        /// 表头与已有文件不一致时另开文件（_v2/_v3…），避免同日新旧列混写。
        /// </summary>
        private static string ResolvePathForHeader(string dayFolder, int station, DateTime now, string header)
        {
            var baseName = $"{now:yyyy_MM_dd}_出胶{station}";
            var candidate = Path.Combine(dayFolder, baseName + ".csv");
            var version = 2;
            while (File.Exists(candidate))
            {
                if (HeaderMatches(candidate, header))
                    return candidate;

                candidate = Path.Combine(dayFolder, $"{baseName}_v{version}.csv");
                version++;
                if (version > 50)
                    break;
            }

            return candidate;
        }

        private static bool HeaderMatches(string filePath, string expectedHeader)
        {
            try
            {
                using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var first = reader.ReadLine();
                return string.Equals(
                    (first ?? string.Empty).Trim(),
                    expectedHeader.Trim(),
                    StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static string BuildLine(BoxGluingSampleRecord record)
        {
            var builder = new StringBuilder();
            // 引号包裹 + InvariantCulture，保证含小时；避免 Excel 把带毫秒时间误显示成「分:秒」
            builder.Append('"');
            builder.Append(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            builder.Append('"');
            foreach (var value in record.Values)
            {
                builder.Append(',');
                if (value.HasValue)
                    builder.Append(value.Value.ToString("G", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            FlushAsync().GetAwaiter().GetResult();
            _flushLock.Dispose();
        }

        private sealed class StationFile
        {
            public StationFile(int station)
            {
                Station = station;
            }

            public int Station { get; }

            public ConcurrentQueue<string> Queue { get; } = new();

            public string? Header { get; set; }

            public string? FilePath { get; set; }

            public DateTime Date { get; set; } = DateTime.MinValue;

            public int Queued;

            public int RowsSinceBreak;
        }
    }
}
