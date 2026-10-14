using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive._TempBossReport
{
    /// <summary>
    /// 【TEMP_BOSS_REPORT】测试阶段专用：每秒采样本进程资源，结束时写出独立报告。
    /// 与采集循环解耦，仅只读观察 <see cref="IAIPredictiveAcquisitionService"/>。
    /// 正式交付前删除本目录及全部 TEMP_BOSS_REPORT 挂钩。
    /// </summary>
    public sealed class AIPredictiveTempRuntimeMonitor : IDisposable
    {
        public const string ReportRoot = @"D:\AIPredictiveData\TempBossReport";
        public const int SampleIntervalMs = 1000;

        private readonly IAIPredictiveAcquisitionService _acquisition;
        private readonly ILoggerService<AIPredictiveTempRuntimeMonitor> _logger;
        private readonly object _gate = new();

        private CancellationTokenSource? _cts;
        private Task? _loopTask;
        private readonly List<ResourceSample> _samples = new(600);
        private readonly List<double> _roundElapsedMs = new(600);
        private long _lastSeenRound;
        private DateTime _startedLocal;
        private TimeSpan _prevCpu;
        private long _prevWallTicks;

        public bool IsRunning { get; private set; }
        public string LastStatus { get; private set; } = "未开始";
        public string? LastResourceReportPath { get; private set; }
        public string? LastRateReportPath { get; private set; }
        public string? LastSamplesCsvPath { get; private set; }
        public event EventHandler? StateChanged;

        public AIPredictiveTempRuntimeMonitor(
            IAIPredictiveAcquisitionService acquisition,
            ILoggerService<AIPredictiveTempRuntimeMonitor> logger)
        {
            _acquisition = acquisition;
            _logger = logger;
            _acquisition.StateChanged += OnAcquisitionStateChanged;
        }

        public Task StartAsync()
        {
            lock (_gate)
            {
                if (IsRunning)
                    return Task.CompletedTask;

                _samples.Clear();
                _roundElapsedMs.Clear();
                _lastSeenRound = _acquisition.RoundCount;
                _startedLocal = DateTime.Now;
                LastResourceReportPath = null;
                LastRateReportPath = null;
                LastSamplesCsvPath = null;

                var proc = Process.GetCurrentProcess();
                proc.Refresh();
                _prevCpu = proc.TotalProcessorTime;
                _prevWallTicks = Stopwatch.GetTimestamp();

                _cts = new CancellationTokenSource();
                IsRunning = true;
                LastStatus = "资源监控运行中（每秒采样）";
                _loopTask = Task.Run(() => LoopAsync(_cts.Token));
                RaiseState();
                _logger.Info("【TEMP_BOSS_REPORT】资源监控已启动", true);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            Task? loop;
            CancellationTokenSource? cts;
            lock (_gate)
            {
                if (!IsRunning)
                    return;
                cts = _cts;
                loop = _loopTask;
                _cts = null;
                _loopTask = null;
                IsRunning = false;
                LastStatus = "正在生成报告…";
            }

            RaiseState();
            try { cts?.Cancel(); } catch { /* ignore */ }
            if (loop is not null)
            {
                try { await loop.ConfigureAwait(false); } catch { /* ignore */ }
            }

            cts?.Dispose();

            string resourcePath;
            string ratePath;
            string csvPath;
            lock (_gate)
            {
                (resourcePath, ratePath, csvPath) = WriteReports_NoLock();
                LastResourceReportPath = resourcePath;
                LastRateReportPath = ratePath;
                LastSamplesCsvPath = csvPath;
                LastStatus = $"报告已生成：{Path.GetFileName(resourcePath)} / {Path.GetFileName(ratePath)}";
            }

            RaiseState();
            _logger.Info($"【TEMP_BOSS_REPORT】资源报告: {resourcePath}", true);
            _logger.Info($"【TEMP_BOSS_REPORT】速率报告: {ratePath}", true);
        }

        private async Task LoopAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(SampleIntervalMs));
            try
            {
                // 立即采一次，再按周期
                SampleOnce();
                while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    SampleOnce();
            }
            catch (OperationCanceledException)
            {
                // normal
            }
            catch (Exception ex)
            {
                _logger.Warn($"【TEMP_BOSS_REPORT】采样循环异常: {ex.Message}", false);
            }
        }

        private void SampleOnce()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                proc.Refresh();

                var nowTicks = Stopwatch.GetTimestamp();
                var cpuNow = proc.TotalProcessorTime;
                var wallSec = (nowTicks - _prevWallTicks) / (double)Stopwatch.Frequency;
                double cpuPct = 0;
                if (wallSec > 0.05)
                {
                    var cpuSec = (cpuNow - _prevCpu).TotalSeconds;
                    cpuPct = 100.0 * cpuSec / (Environment.ProcessorCount * wallSec);
                    if (cpuPct < 0) cpuPct = 0;
                    if (cpuPct > 100 * Environment.ProcessorCount)
                        cpuPct = 100 * Environment.ProcessorCount;
                }

                _prevCpu = cpuNow;
                _prevWallTicks = nowTicks;

                var sample = new ResourceSample
                {
                    Timestamp = DateTime.Now,
                    WorkingSetMb = proc.WorkingSet64 / (1024.0 * 1024.0),
                    PrivateMb = proc.PrivateMemorySize64 / (1024.0 * 1024.0),
                    ManagedMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
                    CpuPercent = cpuPct,
                    ThreadCount = proc.Threads.Count,
                    HandleCount = proc.HandleCount,
                    AcquisitionRunning = _acquisition.IsRunning,
                    RoundCount = _acquisition.RoundCount,
                    LastRoundElapsedMs = _acquisition.LastRoundStats?.ElapsedMs,
                };

                lock (_gate)
                {
                    _samples.Add(sample);
                }

                LastStatus =
                    $"监控中｜CPU {cpuPct:F1}%｜工作集 {sample.WorkingSetMb:F0} MB｜轮次 {_acquisition.RoundCount}";
                RaiseState();
            }
            catch (Exception ex)
            {
                _logger.Warn($"【TEMP_BOSS_REPORT】单次采样失败: {ex.Message}", false);
            }
        }

        private void OnAcquisitionStateChanged(object? sender, EventArgs e)
        {
            if (!IsRunning)
                return;

            lock (_gate)
            {
                var round = _acquisition.RoundCount;
                if (round <= _lastSeenRound)
                    return;
                if (_acquisition.LastRoundStats is { } stats)
                    _roundElapsedMs.Add(stats.ElapsedMs);
                _lastSeenRound = round;
            }
        }

        private (string resourcePath, string ratePath, string csvPath) WriteReports_NoLock()
        {
            Directory.CreateDirectory(ReportRoot);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var resourcePath = Path.Combine(ReportRoot, $"resource_report_{stamp}.txt");
            var ratePath = Path.Combine(ReportRoot, $"acquisition_rate_report_{stamp}.txt");
            var csvPath = Path.Combine(ReportRoot, $"resource_samples_{stamp}.csv");

            var ended = DateTime.Now;
            var wallSec = Math.Max(0.001, (ended - _startedLocal).TotalSeconds);

            // samples csv
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            using (var csv = new StreamWriter(csvPath, false, utf8))
            {
                csv.WriteLine("Timestamp,WorkingSetMB,PrivateMB,ManagedMB,CpuPercent,ThreadCount,HandleCount,AcquisitionRunning,RoundCount,LastRoundElapsedMs");
                foreach (var s in _samples)
                {
                    csv.WriteLine(string.Join(',',
                        s.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                        F(s.WorkingSetMb),
                        F(s.PrivateMb),
                        F(s.ManagedMb),
                        F(s.CpuPercent),
                        s.ThreadCount,
                        s.HandleCount,
                        s.AcquisitionRunning ? 1 : 0,
                        s.RoundCount,
                        s.LastRoundElapsedMs?.ToString(CultureInfo.InvariantCulture) ?? ""));
                }
            }

            // resource report
            using (var w = new StreamWriter(resourcePath, false, utf8))
            {
                w.WriteLine("=== AIPredictive 临时资源占用报告（TEMP_BOSS_REPORT）===");
                w.WriteLine($"生成时间: {ended:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine($"监控起止: {_startedLocal:yyyy-MM-dd HH:mm:ss} ~ {ended:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine($"持续: {wallSec:F1} s");
                w.WriteLine($"采样间隔: {SampleIntervalMs} ms");
                w.WriteLine($"样本数: {_samples.Count}");
                w.WriteLine($"进程: {Process.GetCurrentProcess().ProcessName} (PID {Environment.ProcessId})");
                w.WriteLine($"逻辑处理器: {Environment.ProcessorCount}");
                w.WriteLine("");

                if (_samples.Count == 0)
                {
                    w.WriteLine("无样本。");
                }
                else
                {
                    WriteMetric(w, "CPU %", _samples.Select(x => x.CpuPercent));
                    WriteMetric(w, "WorkingSet MB", _samples.Select(x => x.WorkingSetMb));
                    WriteMetric(w, "Private MB", _samples.Select(x => x.PrivateMb));
                    WriteMetric(w, "Managed(GC) MB", _samples.Select(x => x.ManagedMb));
                    WriteMetric(w, "ThreadCount", _samples.Select(x => (double)x.ThreadCount));
                    WriteMetric(w, "HandleCount", _samples.Select(x => (double)x.HandleCount));
                    w.WriteLine("");
                    w.WriteLine("说明: CPU% 为相对全部逻辑核的进程占用（差分法）。");
                    w.WriteLine($"明细 CSV: {csvPath}");
                }

                w.WriteLine("");
                w.WriteLine("【删除提示】本功能为测试汇报临时模块，见 _TempBossReport/README_DELETE_ME.txt");
            }

            // acquisition rate report
            using (var w = new StreamWriter(ratePath, false, utf8))
            {
                w.WriteLine("=== AIPredictive 临时采集速率报告（TEMP_BOSS_REPORT）===");
                w.WriteLine($"生成时间: {ended:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine($"监控起止: {_startedLocal:yyyy-MM-dd HH:mm:ss} ~ {ended:yyyy-MM-dd HH:mm:ss}");
                w.WriteLine($"持续: {wallSec:F1} s");
                w.WriteLine($"监控期间采集是否曾运行: {(_samples.Any(s => s.AcquisitionRunning) ? "是" : "否")}");
                w.WriteLine($"观测到的轮次增量样本数: {_roundElapsedMs.Count}");
                w.WriteLine($"结束时 RoundCount: {_acquisition.RoundCount}");
                w.WriteLine("");

                if (_roundElapsedMs.Count == 0)
                {
                    w.WriteLine("无采集轮次数据。请先「开始采集」，再开资源监控（或监控期间保持采集运行）。");
                }
                else
                {
                    var times = _roundElapsedMs.OrderBy(x => x).ToList();
                    var avg = times.Average();
                    w.WriteLine($"ElapsedMs  min={times.First():F1}  avg={avg:F1}  p50={Pct(times, 0.50):F1}  p95={Pct(times, 0.95):F1}  max={times.Last():F1}");
                    w.WriteLine($"平均检测速率 ≈ {1000.0 / Math.Max(0.001, avg):F2} 轮/秒");
                    w.WriteLine($"平均检测速率 ≈ {60000.0 / Math.Max(0.001, avg):F1} 轮/分钟");
                    w.WriteLine($"监控窗口吞吐 ≈ {_roundElapsedMs.Count / (wallSec / 60.0):F1} 轮/分钟（按观测到的完成轮次/墙钟）");
                    w.WriteLine("");
                    w.WriteLine("口径: 「平均检测速率」= 1000 / 平均单轮 CIP 耗时；与界面周期补齐无关，反映真实读点能力。");
                }

                w.WriteLine("");
                w.WriteLine("【删除提示】本功能为测试汇报临时模块，见 _TempBossReport/README_DELETE_ME.txt");
            }

            return (resourcePath, ratePath, csvPath);
        }

        private static void WriteMetric(StreamWriter w, string name, IEnumerable<double> values)
        {
            var list = values.OrderBy(x => x).ToList();
            if (list.Count == 0) return;
            w.WriteLine($"{name}: min={list.First():F2}  avg={list.Average():F2}  p50={Pct(list, 0.50):F2}  p95={Pct(list, 0.95):F2}  max={list.Last():F2}");
        }

        private static double Pct(List<double> xs, double p)
        {
            if (xs.Count == 0) return 0;
            var i = (int)Math.Clamp(Math.Round((xs.Count - 1) * p), 0, xs.Count - 1);
            return xs[i];
        }

        private static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

        private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            _acquisition.StateChanged -= OnAcquisitionStateChanged;
            try { StopAsync().GetAwaiter().GetResult(); } catch { /* ignore */ }
        }

        private sealed class ResourceSample
        {
            public DateTime Timestamp;
            public double WorkingSetMb;
            public double PrivateMb;
            public double ManagedMb;
            public double CpuPercent;
            public int ThreadCount;
            public int HandleCount;
            public bool AcquisitionRunning;
            public long RoundCount;
            public long? LastRoundElapsedMs;
        }
    }
}
