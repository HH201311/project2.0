using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// CSV 落盘（仅 500ms）：
    /// {Root}/yyyy-MM-dd/AIPredictive_500ms_yyyyMMdd_001.csv
    /// 与 CIP 基周期对齐，每采一轮写一行。
    /// 单文件上限 100MB；跨日换目录；启动时清理超过 30 天的日期文件夹。
    /// </summary>
    public sealed class AIPredictiveCsvStorage : IAIPredictiveCsvStorage, IDisposable
    {
        /// <summary>市面常见单文件分卷上限（约 HDFS/日志轮转常用量级）。</summary>
        public const long MaxFileBytes = 100L * 1024 * 1024;

        public const int DefaultRetentionDays = 30;
        public const string DefaultRootPath = @"D:\AIPredictiveData";

        /// <summary>与采集基周期对齐的落盘粒度（毫秒）及文件名标签。</summary>
        public static readonly (int IntervalMs, string Tag)[] RecordChannels =
        {
            (500, "500ms"),
        };

        private static readonly Regex DayFolderRegex = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
        private static readonly Regex PartFileRegex = new(
            @"^AIPredictive_(500ms)_(\d{8})_(\d{3})\.csv$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly ILoggerService<AIPredictiveCsvStorage> _logger;
        private readonly object _gate = new();
        private readonly UTF8Encoding _utf8Bom = new(encoderShouldEmitUTF8Identifier: true);
        private readonly UTF8Encoding _utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private IReadOnlyList<string> _pointKeys = Array.Empty<string>();
        private IReadOnlyList<string> _columnHeaders = Array.Empty<string>();
        private string? _headerLine;
        private ChannelWriter[] _channels = Array.Empty<ChannelWriter>();

        public string RootPath { get; }
        public string? CurrentFilePath { get; private set; }

        public AIPredictiveCsvStorage(ILoggerService<AIPredictiveCsvStorage> logger)
        {
            _logger = logger;
            RootPath = DefaultRootPath;
        }

        public void PrepareSession(IReadOnlyList<AIPredictivePointDef> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Count == 0)
                throw new ArgumentException("点位列不能为空", nameof(points));

            var keys = new string[points.Count];
            var headers = BuildUniqueColumnHeaders(points);
            for (var i = 0; i < points.Count; i++)
                keys[i] = points[i].PointKey;

            lock (_gate)
            {
                CloseAllWriters_NoLock();
                _pointKeys = keys;
                _columnHeaders = headers;
                _headerLine = BuildHeader(_columnHeaders);
                CurrentFilePath = null;

                var baseMs = AIPredictiveAcquisitionService.DefaultIntervalMs;
                _channels = new ChannelWriter[RecordChannels.Length];
                for (var i = 0; i < RecordChannels.Length; i++)
                {
                    var (intervalMs, tag) = RecordChannels[i];
                    if (intervalMs % baseMs != 0)
                        throw new InvalidOperationException(
                            $"CSV 粒度 {intervalMs}ms 必须能被采集基周期 {baseMs}ms 整除");

                    _channels[i] = new ChannelWriter
                    {
                        IntervalMs = intervalMs,
                        Tag = tag,
                        EveryNRounds = intervalMs / baseMs,
                    };
                }
            }

            Directory.CreateDirectory(RootPath);
            var removed = CleanupExpiredFolders(DefaultRetentionDays);
            var tags = string.Join("/", RecordChannels.Select(c => c.Tag));
            _logger.Info(
                $"AIPredictive CSV 会话就绪：根目录={RootPath}，列数={points.Count}（表头用参数名），" +
                $"粒度={tags}（基周期={AIPredictiveAcquisitionService.DefaultIntervalMs}ms），" +
                $"单文件上限={MaxFileBytes / (1024 * 1024)}MB，清理过期目录={removed}",
                false);
        }

        /// <summary>
        /// 表头用参数中文名；空名回退 PointKey；重名时追加 PointKey 后缀保证唯一。
        /// </summary>
        private static IReadOnlyList<string> BuildUniqueColumnHeaders(IReadOnlyList<AIPredictivePointDef> points)
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            var headers = new string[points.Count];
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var baseName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.PointKey : p.DisplayName.Trim();
                var header = baseName;
                if (!used.Add(header))
                {
                    header = $"{baseName}({p.PointKey})";
                    var n = 2;
                    while (!used.Add(header))
                    {
                        header = $"{baseName}({p.PointKey})_{n}";
                        n++;
                    }
                }

                headers[i] = header;
            }

            return headers;
        }

        public Task WriteRoundAsync(
            DateTime timestampLocal,
            long round,
            AIPredictiveRoundStats stats,
            IReadOnlyDictionary<string, AIPredictivePointValue> values)
        {
            if (_pointKeys.Count == 0 || _headerLine is null || _channels.Length == 0)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                lock (_gate)
                {
                    try
                    {
                        var line = BuildDataLine(timestampLocal, round, stats, values, _pointKeys);
                        var day = timestampLocal.Date;
                        foreach (var ch in _channels)
                        {
                            if (round % ch.EveryNRounds != 0)
                                continue;

                            EnsureWriter_NoLock(ch, day);
                            ch.Writer!.Write(line);
                            ch.Writer.Write('\n');
                            ch.BytesWritten += Encoding.UTF8.GetByteCount(line) + 1;

                            if (ch.BytesWritten >= MaxFileBytes)
                            {
                                CloseWriter_NoLock(ch);
                                ch.PartIndex++;
                                OpenNewPart_NoLock(ch, day);
                            }
                        }

                        CurrentFilePath = _channels[0].FilePath;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("AIPredictive CSV 写入失败", ex, false);
                        CloseAllWriters_NoLock();
                    }
                }
            });
        }

        public Task FlushAsync()
        {
            return Task.Run(() =>
            {
                lock (_gate)
                {
                    foreach (var ch in _channels)
                    {
                        try
                        {
                            ch.Writer?.Flush();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"AIPredictive CSV Flush 失败（{ch.Tag}）", ex, false);
                        }
                    }
                }
            });
        }

        public int CleanupExpiredFolders(int retentionDays = DefaultRetentionDays)
        {
            if (retentionDays < 1)
                retentionDays = DefaultRetentionDays;

            if (!Directory.Exists(RootPath))
                return 0;

            var cutoff = DateTime.Today.AddDays(-retentionDays);
            var removed = 0;

            foreach (var dir in Directory.EnumerateDirectories(RootPath))
            {
                var name = Path.GetFileName(dir);
                if (!DayFolderRegex.IsMatch(name))
                    continue;
                if (!DateTime.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var day))
                    continue;
                if (day.Date >= cutoff)
                    continue;

                try
                {
                    Directory.Delete(dir, recursive: true);
                    removed++;
                    _logger.Info($"AIPredictive CSV 已删除过期目录: {name}", false);
                }
                catch (Exception ex)
                {
                    _logger.Error($"AIPredictive CSV 删除过期目录失败: {name}", ex, false);
                }
            }

            return removed;
        }

        private void EnsureWriter_NoLock(ChannelWriter ch, DateTime day)
        {
            if (ch.Writer is not null && ch.CurrentDay == day)
                return;

            CloseWriter_NoLock(ch);
            ch.CurrentDay = day;
            ch.PartIndex = ResolveNextPartIndex(ch.Tag, day);
            OpenNewPart_NoLock(ch, day);

            CleanupExpiredFolders(DefaultRetentionDays);
        }

        private void OpenNewPart_NoLock(ChannelWriter ch, DateTime day)
        {
            var dayFolder = Path.Combine(RootPath, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dayFolder);

            var fileName = $"AIPredictive_{ch.Tag}_{day:yyyyMMdd}_{ch.PartIndex:D3}.csv";
            var path = Path.Combine(dayFolder, fileName);
            var exists = File.Exists(path);
            var length = exists ? new FileInfo(path).Length : 0L;
            var append = exists && length > 0 && length < MaxFileBytes;

            if (exists && length >= MaxFileBytes)
            {
                ch.PartIndex++;
                OpenNewPart_NoLock(ch, day);
                return;
            }

            ch.Writer = new StreamWriter(path, append, append ? _utf8NoBom : _utf8Bom)
            {
                AutoFlush = false,
                NewLine = "\n",
            };
            ch.FilePath = path;
            ch.BytesWritten = length;

            if (!append)
            {
                ch.Writer.Write(_headerLine);
                ch.Writer.Write('\n');
                ch.BytesWritten += Encoding.UTF8.GetByteCount(_headerLine!) + 1 + 3;
                ch.Writer.Flush();
            }

            _logger.Info($"AIPredictive CSV 打开分卷: {path}", false);
        }

        private int ResolveNextPartIndex(string tag, DateTime day)
        {
            var dayFolder = Path.Combine(RootPath, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!Directory.Exists(dayFolder))
                return 1;

            var max = 0;
            string? lastUnderLimit = null;
            foreach (var file in Directory.EnumerateFiles(dayFolder, $"AIPredictive_{tag}_*.csv"))
            {
                var name = Path.GetFileName(file);
                var m = PartFileRegex.Match(name);
                if (!m.Success)
                    continue;
                if (!string.Equals(m.Groups[1].Value, tag, StringComparison.OrdinalIgnoreCase))
                    continue;
                var idx = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                if (idx > max)
                    max = idx;
                var len = new FileInfo(file).Length;
                if (len < MaxFileBytes)
                    lastUnderLimit = file;
            }

            if (lastUnderLimit is not null)
            {
                var m = PartFileRegex.Match(Path.GetFileName(lastUnderLimit));
                if (m.Success)
                    return int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            }

            return max + 1;
        }

        private static string BuildHeader(IReadOnlyList<string> columnHeaders)
        {
            var sb = new StringBuilder(64 + columnHeaders.Count * 24);
            sb.Append("Timestamp,Round,ElapsedMs,SuccessCount,FailCount");
            foreach (var header in columnHeaders)
            {
                sb.Append(',');
                sb.Append(EscapeCsv(header));
            }

            return sb.ToString();
        }

        private static string BuildDataLine(
            DateTime timestampLocal,
            long round,
            AIPredictiveRoundStats stats,
            IReadOnlyDictionary<string, AIPredictivePointValue> values,
            IReadOnlyList<string> pointKeys)
        {
            var sb = new StringBuilder(64 + pointKeys.Count * 12);
            sb.Append('"');
            sb.Append(timestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append('"');
            sb.Append(',');
            sb.Append(round.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(stats.ElapsedMs.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(stats.SuccessCount.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(stats.FailCount.ToString(CultureInfo.InvariantCulture));

            foreach (var key in pointKeys)
            {
                sb.Append(',');
                if (!values.TryGetValue(key, out var pv) || !pv.Success || pv.Value is null)
                    continue;

                switch (pv.Value)
                {
                    case float f:
                        sb.Append(f.ToString("G9", CultureInfo.InvariantCulture));
                        break;
                    case double d:
                        sb.Append(d.ToString("G17", CultureInfo.InvariantCulture));
                        break;
                    case bool b:
                        sb.Append(b ? "1" : "0");
                        break;
                    default:
                        sb.Append(EscapeCsv(Convert.ToString(pv.Value, CultureInfo.InvariantCulture) ?? ""));
                        break;
                }
            }

            return sb.ToString();
        }

        private static string EscapeCsv(string value)
        {
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private void CloseWriter_NoLock(ChannelWriter ch)
        {
            if (ch.Writer is null)
                return;
            try
            {
                ch.Writer.Flush();
                ch.Writer.Dispose();
            }
            catch
            {
                // ignore
            }
            finally
            {
                ch.Writer = null;
            }
        }

        private void CloseAllWriters_NoLock()
        {
            foreach (var ch in _channels)
                CloseWriter_NoLock(ch);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                CloseAllWriters_NoLock();
            }
        }

        private sealed class ChannelWriter
        {
            public int IntervalMs { get; init; }
            public string Tag { get; init; } = "";
            public int EveryNRounds { get; init; }
            public DateTime CurrentDay { get; set; } = DateTime.MinValue;
            public int PartIndex { get; set; }
            public StreamWriter? Writer { get; set; }
            public long BytesWritten { get; set; }
            public string? FilePath { get; set; }
        }
    }
}
