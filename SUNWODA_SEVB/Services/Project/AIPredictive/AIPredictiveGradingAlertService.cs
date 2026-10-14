using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 上位机侧按 IntervalMinutes 调度；每轮切「最近 LookbackHours」临时 CSV（覆盖旧切片），
    /// 再调用对方原版 <c>分级预警推理.py</c>；有一级/二级即弹窗，不抑制。不改算法 py。
    /// </summary>
    public sealed class AIPredictiveGradingAlertService : IAIPredictiveGradingAlertService
    {
        private const string ScriptFileName = "分级预警推理.py";
        private const string ModelFileName = "V8.0_model.json";
        private const string SliceFileName = "input_slice.csv";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };

        private readonly ILoggerService<AIPredictiveGradingAlertService> _logger;
        private readonly IAIPredictiveCsvStorage _csvStorage;
        private readonly object _gate = new();
        private readonly SemaphoreSlim _runLock = new(1, 1);

        private AIPredictiveGradingConfig _config;

        public AIPredictiveGradingConfig Config
        {
            get
            {
                lock (_gate)
                    return CloneConfig(_config);
            }
        }

        public event Action<IReadOnlyList<AIPredictiveGradingAlertItem>>? AlertsRaised;

        public AIPredictiveGradingAlertService(
            ILoggerService<AIPredictiveGradingAlertService> logger,
            IAIPredictiveCsvStorage csvStorage)
        {
            _logger = logger;
            _csvStorage = csvStorage;
            _config = LoadConfigOrDefault();
        }

        public async Task<AIPredictiveGradingRunResult> RunOnceAsync(CancellationToken cancellationToken = default)
        {
            if (!await _runLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                return new AIPredictiveGradingRunResult
                {
                    Skipped = true,
                    SkipReason = "busy",
                    Success = true,
                    Message = "上一轮仍在执行，跳过",
                };
            }

            try
            {
                var cfg = Config;
                if (!cfg.Enabled)
                {
                    return new AIPredictiveGradingRunResult
                    {
                        Skipped = true,
                        SkipReason = "disabled",
                        Success = true,
                        Message = "分级预警已禁用",
                    };
                }

                var packDir = ResolvePackDirectory();
                var script = Path.Combine(packDir, ScriptFileName);
                var model = Path.Combine(packDir, ModelFileName);
                if (!File.Exists(script) || !File.Exists(model))
                {
                    var msg = $"GradingAlert 脚本/模型缺失: {packDir}";
                    _logger.Error(msg, true);
                    return new AIPredictiveGradingRunResult { Success = false, Message = msg };
                }

                var dataRoot = _csvStorage.RootPath;
                if (!Directory.Exists(dataRoot))
                {
                    return new AIPredictiveGradingRunResult
                    {
                        Skipped = true,
                        SkipReason = "no_data_root",
                        Success = true,
                        Message = $"采集目录不存在: {dataRoot}",
                    };
                }

                var stateDir = Path.Combine(dataRoot, "_grading_state");
                Directory.CreateDirectory(stateDir);
                var slicePath = Path.Combine(stateDir, SliceFileName);
                var outputCsv = Path.Combine(stateDir, "latest_alerts.csv");
                var watermarkPath = Path.Combine(stateDir, "watermark.json");

                // 调度间隔 ≠ 数据窗口：IntervalMinutes 管多久跑一次，LookbackHours 管喂多少数据
                var lookbackHours = cfg.LookbackHours > 0 ? cfg.LookbackHours : 2.0;
                var windowEnd = DateTime.Now;
                var windowStart = windowEnd.AddHours(-lookbackHours);

                // 删旧切片，写最新回看窗
                TryDelete(slicePath);
                TryDelete(outputCsv);

                var slice = BuildRecentSlice(dataRoot, windowStart, windowEnd);
                if (slice.RowCount == 0)
                {
                    WriteMeta(stateDir, skipped: true, "no_rows_in_window", windowStart, windowEnd, 0);
                    return new AIPredictiveGradingRunResult
                    {
                        Skipped = true,
                        SkipReason = "no_rows_in_window",
                        Success = true,
                        Message = $"近 {lookbackHours:0.##} 小时无采集行",
                    };
                }

                // 无新时间戳则跳过（避免同一批数据反复调 Python）
                var lastTs = LoadWatermark(watermarkPath);
                if (lastTs is not null && slice.MaxTimestamp <= lastTs.Value)
                {
                    WriteMeta(stateDir, skipped: true, "no_new_data", windowStart, slice.MaxTimestamp, slice.RowCount);
                    return new AIPredictiveGradingRunResult
                    {
                        Skipped = true,
                        SkipReason = "no_new_data",
                        Success = true,
                        Message = $"无新数据（最大={slice.MaxTimestamp:O}）",
                    };
                }

                await File.WriteAllTextAsync(slicePath, slice.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken)
                    .ConfigureAwait(false);

                // Windows Process.Start 不会按 WorkingDirectory 解析相对 FileName；相对路径先相对 packDir 拼成绝对路径
                var pythonExe = ResolvePythonExe(cfg.PythonExe, packDir);
                if (Path.IsPathRooted(pythonExe) && !File.Exists(pythonExe))
                {
                    var msg = $"Python 可执行文件不存在: {pythonExe}（配置={cfg.PythonExe}，目录={packDir}）";
                    _logger.Error(msg, true);
                    return new AIPredictiveGradingRunResult { Success = false, Message = msg };
                }

                var args =
                    $"\"{script}\" --model \"{model}\" --input \"{slicePath}\" " +
                    $"--output \"{outputCsv}\" --device \"{cfg.DeviceName}\"";

                var sw = Stopwatch.StartNew();
                var (exitCode, stdOut, stdErr) = await RunProcessAsync(
                    pythonExe,
                    args,
                    packDir,
                    TimeSpan.FromSeconds(Math.Max(30, cfg.TimeoutSeconds)),
                    cancellationToken).ConfigureAwait(false);
                sw.Stop();

                if (exitCode != 0)
                {
                    var msg = $"分级预警脚本退出码 {exitCode}，耗时 {sw.ElapsedMilliseconds}ms。stderr={Trim(stdErr, 500)}";
                    _logger.Error(msg, true);
                    return new AIPredictiveGradingRunResult { Success = false, Message = msg };
                }

                SaveWatermark(watermarkPath, slice.MaxTimestamp);
                WriteMeta(stateDir, skipped: false, null, windowStart, slice.MaxTimestamp, slice.RowCount);

                var allAlerts = ParseAlertCsv(outputCsv, cfg.AlertLevels);

                _logger.Info(
                    $"分级预警完成：切片={slice.RowCount}行/{lookbackHours:0.##}h，耗时={sw.ElapsedMilliseconds}ms，报警={allAlerts.Count}（直接弹窗）。{Trim(stdOut, 200)}",
                    false);

                if (allAlerts.Count > 0)
                {
                    try
                    {
                        AlertsRaised?.Invoke(allAlerts);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("分级预警 UI 回调异常", ex);
                    }
                }

                return new AIPredictiveGradingRunResult
                {
                    Skipped = false,
                    Success = true,
                    Message = $"完成，切片 {slice.RowCount} 行，报警 {allAlerts.Count}",
                    Alerts = allAlerts,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error("分级预警执行失败", ex);
                return new AIPredictiveGradingRunResult { Success = false, Message = ex.Message };
            }
            finally
            {
                _runLock.Release();
            }
        }

        /// <summary>
        /// 从采集根目录按天文件夹读取，只保留 [windowStart, windowEnd] 内行，写成带表头的临时 CSV 文本。
        /// </summary>
        private static SliceBuildResult BuildRecentSlice(string dataRoot, DateTime windowStart, DateTime windowEnd)
        {
            var dayFolders = new List<string>();
            for (var d = windowStart.Date; d <= windowEnd.Date; d = d.AddDays(1))
            {
                var folder = Path.Combine(dataRoot, d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                if (Directory.Exists(folder))
                    dayFolders.Add(folder);
            }

            // 无标准日期目录时：不递归全盘，避免扫历史
            if (dayFolders.Count == 0)
                return SliceBuildResult.Empty;

            string? headerLine = null;
            var dataLines = new List<(DateTime Ts, string Line)>();

            foreach (var folder in dayFolders)
            {
                foreach (var file in Directory.EnumerateFiles(folder, "AIPredictive_500ms_*.csv", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    // 采集侧 StreamWriter 正占用同一文件；必须 ReadWrite 共享，否则 IOException
                    using var fs = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    var header = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(header))
                        continue;
                    headerLine ??= header;

                    string? line;
                    while ((line = reader.ReadLine()) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;
                        if (!TryParseTimestamp(line, out var ts))
                            continue;
                        if (ts < windowStart || ts > windowEnd)
                            continue;
                        dataLines.Add((ts, line));
                    }
                }
            }

            if (headerLine is null || dataLines.Count == 0)
                return SliceBuildResult.Empty;

            // 按时间去重（保留先出现的）
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var ordered = dataLines.OrderBy(x => x.Ts).ToList();
            var sb = new StringBuilder(headerLine.Length + ordered.Count * 64);
            sb.Append(headerLine).Append('\n');
            var maxTs = DateTime.MinValue;
            var count = 0;
            foreach (var (ts, line) in ordered)
            {
                var key = ts.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
                if (!seen.Add(key))
                    continue;
                sb.Append(line).Append('\n');
                count++;
                if (ts > maxTs)
                    maxTs = ts;
            }

            return new SliceBuildResult(count, maxTs, sb.ToString());
        }

        private static bool TryParseTimestamp(string csvLine, out DateTime ts)
        {
            ts = default;
            // 首列 Timestamp，可能带引号： "2026-10-08 00:05:32.579",...
            var cols = SplitCsvLine(csvLine);
            if (cols.Length == 0)
                return false;
            var raw = cols[0].Trim().Trim('"');
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out ts)
                   || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out ts);
        }

        private static DateTime? LoadWatermark(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                if (!doc.RootElement.TryGetProperty("last_processed_timestamp", out var p))
                    return null;
                var s = p.GetString();
                if (string.IsNullOrWhiteSpace(s))
                    return null;
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
                if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
                    return dt;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private static void SaveWatermark(string path, DateTime maxTs)
        {
            var obj = new Dictionary<string, string>
            {
                ["last_processed_timestamp"] = maxTs.ToString("o", CultureInfo.InvariantCulture),
                ["updated_at"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            };
            File.WriteAllText(path, JsonSerializer.Serialize(obj, JsonOpts), new UTF8Encoding(false));
        }

        private static void WriteMeta(
            string stateDir,
            bool skipped,
            string? reason,
            DateTime windowStart,
            DateTime windowEnd,
            int rowCount)
        {
            try
            {
                var meta = new Dictionary<string, object?>
                {
                    ["skipped"] = skipped,
                    ["reason"] = reason,
                    ["window_start"] = windowStart.ToString("o", CultureInfo.InvariantCulture),
                    ["window_end"] = windowEnd.ToString("o", CultureInfo.InvariantCulture),
                    ["row_count"] = rowCount,
                    ["finished_at"] = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
                };
                File.WriteAllText(
                    Path.Combine(stateDir, "last_run_meta.json"),
                    JsonSerializer.Serialize(meta, JsonOpts),
                    new UTF8Encoding(false));
            }
            catch
            {
                // ignore
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }

        private string ResolvePackDirectory() =>
            Path.Combine(AppContext.BaseDirectory, "AIPredictive", "GradingAlert");

        /// <summary>
        /// 将配置中的 PythonExe 解析为可启动路径。
        /// 含目录分隔符或明确相对段时，相对 <paramref name="packDir"/> 转绝对路径；
        /// 纯命令名（如 python）保留，走 PATH。
        /// </summary>
        private static string ResolvePythonExe(string configured, string packDir)
        {
            if (string.IsNullOrWhiteSpace(configured))
                return "python";

            var raw = configured.Trim().Trim('"');
            if (Path.IsPathRooted(raw))
                return Path.GetFullPath(raw);

            // 含 \ 或 / 或 .：视为相对 packDir（如 runtime\python.exe）
            if (raw.IndexOfAny(new[] { '\\', '/', '.' }) >= 0)
                return Path.GetFullPath(Path.Combine(packDir, raw));

            // 纯命令名：python / py
            return raw;
        }

        private AIPredictiveGradingConfig LoadConfigOrDefault()
        {
            try
            {
                var path = Path.Combine(ResolvePackDirectory(), "grading_config.json");
                if (!File.Exists(path))
                    return new AIPredictiveGradingConfig();
                var cfg = JsonSerializer.Deserialize<AIPredictiveGradingConfig>(
                    File.ReadAllText(path, Encoding.UTF8), JsonOpts);
                return cfg ?? new AIPredictiveGradingConfig();
            }
            catch (Exception ex)
            {
                _logger.Warn($"读取 grading_config 失败，使用默认: {ex.Message}", false);
                return new AIPredictiveGradingConfig();
            }
        }

        private static AIPredictiveGradingConfig CloneConfig(AIPredictiveGradingConfig src) => new()
        {
            Enabled = src.Enabled,
            PythonExe = src.PythonExe,
            DeviceName = src.DeviceName,
            IntervalMinutes = src.IntervalMinutes,
            LookbackHours = src.LookbackHours,
            TimeoutSeconds = src.TimeoutSeconds,
            AlertLevels = src.AlertLevels?.ToList() ?? new List<string> { "一级", "二级" },
        };

        private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    stdout.AppendLine(e.Data);
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    stderr.AppendLine(e.Data);
            };

            if (!proc.Start())
                throw new InvalidOperationException($"无法启动进程: {fileName}");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }

                throw new TimeoutException($"分级预警脚本超时（>{timeout.TotalSeconds:0}s）");
            }

            return (proc.ExitCode, stdout.ToString(), stderr.ToString());
        }

        private static List<AIPredictiveGradingAlertItem> ParseAlertCsv(
            string path,
            IReadOnlyList<string> levels)
        {
            var list = new List<AIPredictiveGradingAlertItem>();
            if (!File.Exists(path))
                return list;

            var levelSet = new HashSet<string>(levels ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (levelSet.Count == 0)
            {
                levelSet.Add("一级");
                levelSet.Add("二级");
            }

            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length <= 1)
                return list;

            var header = SplitCsvLine(lines[0]);
            int Idx(string name) =>
                Array.FindIndex(header, h => string.Equals(h.Trim().Trim('"'), name, StringComparison.Ordinal));

            var iRank = Idx("排名");
            var iAxis = Idx("轴");
            var iMech = Idx("机构");
            var iSt = Idx("工位");
            var iLv = Idx("预警等级");
            var iRisk = Idx("风险分");
            var iEv = Idx("证据类型");
            var iAdvice = Idx("维护建议");

            for (var i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                var cols = SplitCsvLine(lines[i]);
                string Cell(int idx) => idx >= 0 && idx < cols.Length ? cols[idx].Trim().Trim('"') : "";

                var level = Cell(iLv);
                if (!levelSet.Contains(level))
                    continue;

                _ = int.TryParse(Cell(iRank), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank);
                _ = double.TryParse(Cell(iRisk), NumberStyles.Float, CultureInfo.InvariantCulture, out var risk);

                list.Add(new AIPredictiveGradingAlertItem
                {
                    Rank = rank,
                    Axis = Cell(iAxis),
                    Mechanism = Cell(iMech),
                    Station = Cell(iSt),
                    Level = level,
                    RiskScore = risk,
                    EvidenceType = Cell(iEv),
                    Advice = Cell(iAdvice),
                });
            }

            return list;
        }

        private static string[] SplitCsvLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            result.Add(sb.ToString());
            return result.ToArray();
        }

        private static string Trim(string? s, int max)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            s = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return s.Length <= max ? s : s[..max] + "…";
        }

        private readonly record struct SliceBuildResult(int RowCount, DateTime MaxTimestamp, string Content)
        {
            public static SliceBuildResult Empty => new(0, DateTime.MinValue, "");
        }
    }
}
