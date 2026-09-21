using SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing;
using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Services.Project.BoxGluing
{
    /// <summary>
    /// 每通道环形缓冲；UI 通过快照拷贝绘图。
    /// </summary>
    public class BoxGluingChartDataService : IBoxGluingChartDataService
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, Ring> _rings = new();
        private BoxGluingAcquisitionConfig _config = BoxGluingDefaultConfig.Create();

        public void Configure(BoxGluingAcquisitionConfig config)
        {
            lock (_sync)
            {
                _config = config;
                var capacity = Math.Max(200, config.ChartWindowPoints);
                foreach (var channel in config.EnabledChannels)
                {
                    if (channel.Group == "Level")
                    {
                        for (var station = 1; station <= 3; station++)
                            EnsureRing(LevelRingKey(channel.Label, station), capacity);
                    }
                    else
                    {
                        EnsureRing(channel.Label, capacity);
                    }
                }
            }
        }

        public void ClearStation(int station)
        {
            lock (_sync)
            {
                foreach (var channel in _config.ChannelsOfStation(station))
                {
                    if (_rings.TryGetValue(channel.Label, out var ring))
                        ring.Clear();
                }

                foreach (var channel in _config.EnabledChannels.Where(c => c.Group == "Level"))
                {
                    if (_rings.TryGetValue(LevelRingKey(channel.Label, station), out var levelRing))
                        levelRing.Clear();
                }
            }
        }

        public void ClearAll()
        {
            lock (_sync)
            {
                foreach (var ring in _rings.Values)
                    ring.Clear();
            }
        }

        public void Append(BoxGluingSampleRecord record, IReadOnlyList<BoxGluingChannelConfig> channels)
        {
            lock (_sync)
            {
                for (var i = 0; i < channels.Count && i < record.Values.Count; i++)
                {
                    if (!record.Values[i].HasValue)
                        continue;

                    var channel = channels[i];
                    var y = record.Values[i]!.Value;
                    if (channel.Group == "Level")
                    {
                        foreach (var kv in record.ElapsedByStation)
                        {
                            var key = LevelRingKey(channel.Label, kv.Key);
                            if (!_rings.TryGetValue(key, out var levelRing))
                            {
                                levelRing = new Ring(Math.Max(200, _config.ChartWindowPoints));
                                _rings[key] = levelRing;
                            }

                            levelRing.Add(kv.Value, y);
                        }

                        continue;
                    }

                    if (!_rings.TryGetValue(channel.Label, out var ring))
                        continue;
                    var x = record.ElapsedByStation.TryGetValue(channel.Meter, out var elapsed)
                        ? elapsed
                        : record.ElapsedSeconds;
                    ring.Add(x, y);
                }
            }
        }

        public BoxGluingChartSnapshot CaptureSnapshot()
        {
            lock (_sync)
            {
                var series = new List<BoxGluingSeriesPoints>(_config.Channels.Count);
                foreach (var channel in _config.EnabledChannels)
                {
                    if (channel.Group == "Level")
                    {
                        for (var station = 1; station <= 3; station++)
                        {
                            if (!_rings.TryGetValue(LevelRingKey(channel.Label, station), out var levelRing))
                                continue;
                            var (xs, ys) = levelRing.Copy();
                            series.Add(new BoxGluingSeriesPoints
                            {
                                Label = channel.Label,
                                Group = channel.Group,
                                Meter = station,
                                Side = channel.Side,
                                Xs = xs,
                                Ys = ys
                            });
                        }

                        continue;
                    }

                    if (!_rings.TryGetValue(channel.Label, out var ring))
                        continue;

                    var copied = ring.Copy();
                    series.Add(new BoxGluingSeriesPoints
                    {
                        Label = channel.Label,
                        Group = channel.Group,
                        Meter = channel.Meter,
                        Side = channel.Side,
                        Xs = copied.Xs,
                        Ys = copied.Ys
                    });
                }

                return new BoxGluingChartSnapshot
                {
                    Series = series,
                    Thresholds = _config.Thresholds.ToList()
                };
            }
        }

        private void EnsureRing(string key, int capacity)
        {
            if (!_rings.ContainsKey(key))
                _rings[key] = new Ring(capacity);
            else
                _rings[key].Resize(capacity);
        }

        private static string LevelRingKey(string label, int station) => $"{label}#S{station}";

        private sealed class Ring
        {
            private double[] _xs;
            private double[] _ys;
            private int _start;
            private int _count;
            private int _capacity;

            public Ring(int capacity)
            {
                _capacity = capacity;
                _xs = new double[capacity];
                _ys = new double[capacity];
            }

            public void Resize(int capacity)
            {
                if (capacity == _capacity)
                    return;
                var (xs, ys) = Copy();
                _capacity = capacity;
                _xs = new double[capacity];
                _ys = new double[capacity];
                _start = 0;
                _count = 0;
                var take = Math.Min(xs.Length, capacity);
                var offset = xs.Length - take;
                for (var i = 0; i < take; i++)
                    Add(xs[offset + i], ys[offset + i]);
            }

            public void Clear()
            {
                _start = 0;
                _count = 0;
            }

            public void Add(double x, double y)
            {
                var index = (_start + _count) % _capacity;
                if (_count == _capacity)
                {
                    _xs[index] = x;
                    _ys[index] = y;
                    _start = (_start + 1) % _capacity;
                }
                else
                {
                    _xs[index] = x;
                    _ys[index] = y;
                    _count++;
                }
            }

            public (double[] Xs, double[] Ys) Copy()
            {
                var xs = new double[_count];
                var ys = new double[_count];
                for (var i = 0; i < _count; i++)
                {
                    var index = (_start + i) % _capacity;
                    xs[i] = _xs[index];
                    ys[i] = _ys[index];
                }

                return (xs, ys);
            }
        }
    }
}
