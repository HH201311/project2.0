using System.Diagnostics;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Services.Project.BoxGluing
{
    /// <summary>
    /// PerLane 采集：出胶N=1 时采工位N 启用通道；80ms 采样，50ms 轮询触发。
    /// 连续区 D1600～D1622 一次读 23 字。
    /// </summary>
    public class BoxGluingAcquisitionService : IBoxGluingAcquisitionService, IDisposable
    {
        private const string BlockStart = "D1600";
        private const ushort BlockLength = 23;
        private const int ProcessWordCount = 20;

        private readonly IBoxGluingConfigService _configService;
        private readonly IBoxGluingCsvStorage _csvStorage;
        private readonly IBoxGluingChartDataService _chartDataService;
        private readonly IPLCService _plcService;
        private readonly ILoggerService<BoxGluingAcquisitionService> _logger;
        private readonly Dictionary<int, bool> _stationActive = new() { [1] = false, [2] = false, [3] = false };
        private readonly Dictionary<int, DateTime> _stationStart = new();
        private readonly object _simLock = new();

        private readonly Dictionary<int, CancellationTokenSource> _simPulseCts = new();
        private HashSet<int> _simulatedStations = new();
        private CancellationTokenSource? _cts;
        private Task? _loopTask;

        public bool IsRunning { get; private set; }

        public IReadOnlyDictionary<int, bool> StationActive
        {
            get
            {
                lock (_stationActive)
                {
                    return new Dictionary<int, bool>(_stationActive);
                }
            }
        }

        public string LastStatus { get; private set; } = "未启动";

        public long SampleCount { get; private set; }

        public event EventHandler? StateChanged;

        public BoxGluingAcquisitionService(
            IBoxGluingConfigService configService,
            IBoxGluingCsvStorage csvStorage,
            IBoxGluingChartDataService chartDataService,
            IPLCService plcService,
            ILoggerService<BoxGluingAcquisitionService> logger)
        {
            _configService = configService;
            _csvStorage = csvStorage;
            _chartDataService = chartDataService;
            _plcService = plcService;
            _logger = logger;
        }

        public async Task StartAsync(IReadOnlySet<int>? simulatedStations = null)
        {
            if (IsRunning)
                return;

            var config = _configService.Current;
            _chartDataService.Configure(config);
            _chartDataService.ClearAll();
            _stationStart.Clear();
            if (simulatedStations is not null)
                SetSimulatedStations(simulatedStations);

            _cts = new CancellationTokenSource();
            IsRunning = true;
            SampleCount = 0;
            LastStatus = config.UseSimulation ? "模拟采集运行中" : "PLC采集运行中";
            _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
            RaiseState();
            _logger.Info($"BoxGluing采集启动，模拟={config.UseSimulation}，周期={config.SampleIntervalMs}ms", false);
        }

        public async Task StopAsync()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            try
            {
                _cts?.Cancel();
                if (_loopTask is not null)
                    await _loopTask;
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _loopTask = null;
                lock (_stationActive)
                {
                    foreach (var key in _stationActive.Keys.ToList())
                        _stationActive[key] = false;
                }

                _stationStart.Clear();
                CancelAllSimPulses();

                await _csvStorage.FlushAsync();
                foreach (var station in new[] { 1, 2, 3 })
                    await _csvStorage.WriteSegmentBreakAsync(station);
                LastStatus = "已停止";
                RaiseState();
                _logger.Info("BoxGluing采集已停止", false);
            }
        }

        public void SetSimulatedStations(IReadOnlySet<int> stations)
        {
            lock (_simLock)
            {
                _simulatedStations = stations.ToHashSet();
            }
        }

        public void TriggerSimulatedGlue(int station)
        {
            if (station is < 1 or > 3)
                return;

            if (!_configService.Current.UseSimulation)
            {
                LastStatus = "非模拟模式：等待 PLC 出胶信号 D1620～D1622";
                RaiseState();
                return;
            }

            lock (_simLock)
            {
                if (_simulatedStations.Contains(station))
                    return;
                _simulatedStations.Add(station);
            }

            var durationMs = Random.Shared.Next(5000, 8001);
            CancellationTokenSource cts;
            lock (_simLock)
            {
                if (_simPulseCts.TryGetValue(station, out var old))
                {
                    old.Cancel();
                    old.Dispose();
                }

                cts = new CancellationTokenSource();
                _simPulseCts[station] = cts;
            }

            _logger.Info($"模拟出胶{station}触发，{durationMs}ms 后自动结束", false);
            _ = EndSimulatedGlueAfterAsync(station, durationMs, cts.Token);
        }

        private async Task EndSimulatedGlueAfterAsync(int station, int durationMs, CancellationToken token)
        {
            try
            {
                await Task.Delay(durationMs, token);
                lock (_simLock)
                {
                    _simulatedStations.Remove(station);
                }

                RaiseState();
            }
            catch (OperationCanceledException)
            {
                // 停止监测或重复触发
            }
        }

        private void CancelAllSimPulses()
        {
            lock (_simLock)
            {
                foreach (var cts in _simPulseCts.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                _simPulseCts.Clear();
                _simulatedStations.Clear();
            }
        }

        private async Task RunLoopAsync(CancellationToken token)
        {
            var lastSample = DateTime.MinValue;
            var lastFlush = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                var config = _configService.Current;
                var poll = Math.Max(20, config.TriggerPollIntervalMs);
                try
                {
                    var (process, triggers, source) = await ReadOnceAsync(config, token);
                    var ended = ApplyTriggers(config, triggers);

                    var active = GetActiveStations();
                    var now = DateTime.Now;
                    var sampleStations = active.Union(ended).Distinct().OrderBy(x => x).ToList();
                    var due = (now - lastSample).TotalMilliseconds >= config.SampleIntervalMs;
                    if (sampleStations.Count > 0 && (due || ended.Count > 0))
                    {
                        lastSample = now;
                        AppendSample(config, process, sampleStations, now);
                    }

                    if (ended.Count > 0)
                    {
                        foreach (var station in ended)
                            _stationStart.Remove(station);
                        foreach (var station in ended)
                            await _csvStorage.WriteSegmentBreakAsync(station);
                    }

                    if ((now - lastFlush).TotalSeconds >= 1)
                    {
                        lastFlush = now;
                        await _csvStorage.FlushAsync();
                    }

                    LastStatus = BuildStatus(config, source, active);
                    RaiseState();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LastStatus = $"采集异常: {ex.Message}";
                    _logger.Error("BoxGluing采集循环异常", ex, true);
                    RaiseState();
                    await Task.Delay(500, token);
                }

                await Task.Delay(poll, token);
            }
        }

        private async Task<(short[] Process, Dictionary<int, bool> Triggers, string Source)> ReadOnceAsync(
            BoxGluingAcquisitionConfig config,
            CancellationToken token)
        {
            if (config.UseSimulation)
            {
                HashSet<int> sim;
                lock (_simLock)
                {
                    sim = _simulatedStations.ToHashSet();
                }

                var triggers = config.Triggers.ToDictionary(t => t.Station, t => sim.Contains(t.Station));
                var process = BuildSimulationProcess(config, sim);
                return (process, triggers, "模拟");
            }

            var block = await _plcService.ReadInt16BlockAsync(BlockStart, BlockLength, token);
            if (block is null || block.Length < ProcessWordCount + config.Triggers.Count)
            {
                var emptyTriggers = config.Triggers.ToDictionary(t => t.Station, _ => false);
                return (Array.Empty<short>(), emptyTriggers, _plcService.HasConnectedDevice ? "PLC读失败" : "PLC未连接");
            }

            var processWords = block.Take(ProcessWordCount).ToArray();
            var triggerMap = new Dictionary<int, bool>();
            foreach (var trigger in config.Triggers)
            {
                var offset = ParseDOffset(trigger.Address) - 1600;
                var on = offset >= 0 && offset < block.Length && block[offset] == trigger.ActiveValue;
                triggerMap[trigger.Station] = on;
            }

            return (processWords, triggerMap, "PLC");
        }

        private short[] BuildSimulationProcess(BoxGluingAcquisitionConfig config, HashSet<int> activeStations)
        {
            var values = new short[ProcessWordCount];
            var t = DateTime.Now.TimeOfDay.TotalSeconds;
            foreach (var channel in config.EnabledChannels)
            {
                var index = ParseDOffset(channel.Address) - 1600;
                if (index < 0 || index >= values.Length)
                    continue;
                if (!activeStations.Contains(channel.Meter) && channel.Group != "Level")
                {
                    values[index] = 0;
                    continue;
                }

                var sideShift = string.Equals(channel.Side, "B", StringComparison.OrdinalIgnoreCase) ? 1.1 : 0;
                var sideScale = string.Equals(channel.Side, "B", StringComparison.OrdinalIgnoreCase) ? 0.82 : 1.0;
                values[index] = channel.Group switch
                {
                    "Torque" => (short)Math.Clamp(
                        10 + 8 * sideScale * Math.Sin(t * 1.7 + channel.Meter + sideShift),
                        0,
                        40),
                    "Pressure" => (short)Math.Clamp(
                        (channel.Meter >= 3 ? 58 : 42) + 14 * sideScale * Math.Sin(t * 1.2 + channel.Meter + sideShift),
                        0,
                        100),
                    "Level" => (short)Math.Clamp(
                        (channel.Side == "B" ? 28 : 40) + 12 * Math.Sin(t * 0.55 + sideShift),
                        5,
                        70),
                    _ => 0
                };
            }

            return values;
        }

        private List<int> ApplyTriggers(BoxGluingAcquisitionConfig config, Dictionary<int, bool> triggers)
        {
            var ended = new List<int>();
            foreach (var trigger in config.Triggers)
            {
                var next = triggers.TryGetValue(trigger.Station, out var on) && on;
                bool prev;
                lock (_stationActive)
                {
                    prev = _stationActive[trigger.Station];
                    _stationActive[trigger.Station] = next;
                }

                if (next && !prev)
                {
                    _stationStart[trigger.Station] = DateTime.Now;
                    _chartDataService.ClearStation(trigger.Station);
                    _logger.Info($"{trigger.Label} 启动，工位{trigger.Station} 开始采集", false);
                }
                else if (!next && prev)
                {
                    ended.Add(trigger.Station);
                    if (_stationStart.TryGetValue(trigger.Station, out var start))
                    {
                        var duration = DateTime.Now - start;
                        _logger.Info($"{trigger.Label} 停止，工位{trigger.Station} 时长 {duration:hh\\:mm\\:ss\\.fff}", false);
                    }
                }
            }

            return ended;
        }

        private void AppendSample(
            BoxGluingAcquisitionConfig config,
            short[] process,
            List<int> activeStations,
            DateTime now)
        {
            var enabled = config.EnabledChannels.ToList();
            var values = new double?[enabled.Count];
            for (var i = 0; i < enabled.Count; i++)
            {
                var channel = enabled[i];
                if (!activeStations.Contains(channel.Meter) && channel.Group != "Level")
                    continue;

                var index = ParseDOffset(channel.Address) - 1600;
                if (index >= 0 && index < process.Length)
                    values[i] = process[index];
            }

            if (values.All(v => !v.HasValue))
                return;

            var elapsedByStation = new Dictionary<int, double>();
            foreach (var station in activeStations)
            {
                elapsedByStation[station] = _stationStart.TryGetValue(station, out var start)
                    ? (now - start).TotalSeconds
                    : 0;
            }

            var record = new BoxGluingSampleRecord
            {
                Timestamp = now,
                TriggerStations = string.Join("|", activeStations.OrderBy(x => x)),
                ChannelLabels = enabled.Select(c => c.Label).ToList(),
                Values = values,
                ElapsedSeconds = elapsedByStation.Count == 0 ? 0 : elapsedByStation.Values.Min(),
                ElapsedByStation = elapsedByStation
            };

            SampleCount++;
            _chartDataService.Append(record, enabled);
            foreach (var station in activeStations)
                EnqueueStationCsv(config, process, station, now, elapsedByStation);
        }

        private void EnqueueStationCsv(
            BoxGluingAcquisitionConfig config,
            short[] process,
            int station,
            DateTime now,
            Dictionary<int, double> elapsedByStation)
        {
            var channels = config.EnabledChannels
                .Where(c => c.Meter == station || c.Group == "Level")
                .ToList();
            if (channels.Count == 0)
                return;

            var values = new double?[channels.Count];
            for (var i = 0; i < channels.Count; i++)
            {
                var index = ParseDOffset(channels[i].Address) - 1600;
                if (index >= 0 && index < process.Length)
                    values[i] = process[index];
            }

            var stationRecord = new BoxGluingSampleRecord
            {
                Timestamp = now,
                TriggerStations = station.ToString(),
                ChannelLabels = channels.Select(c => c.Label).ToList(),
                Values = values,
                ElapsedSeconds = elapsedByStation.TryGetValue(station, out var elapsed) ? elapsed : 0
            };

            _ = _csvStorage.EnqueueAsync(station, stationRecord, stationRecord.ChannelLabels);
        }

        private List<int> GetActiveStations()
        {
            lock (_stationActive)
            {
                return _stationActive.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(x => x).ToList();
            }
        }

        private string BuildStatus(BoxGluingAcquisitionConfig config, string source, List<int> active)
        {
            var stations = active.Count == 0 ? "无出胶" : "工位" + string.Join(",", active);
            var plc = _plcService.HasConnectedDevice ? "PLC已连接" : "PLC未连接";
            return $"{source} | {stations} | {plc} | 已采{SampleCount}点 | {config.SampleIntervalMs}ms";
        }

        private static int ParseDOffset(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return -1;
            var digits = new string(address.SkipWhile(c => !char.IsDigit(c)).ToArray());
            return int.TryParse(digits, out var value) ? value : -1;
        }

        private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
