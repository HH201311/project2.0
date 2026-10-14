using System.Collections.Concurrent;
using System.Diagnostics;
using HslCommunication.Profinet.Omron;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive;
using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Services.Project.AIPredictive
{
    /// <summary>
    /// 欧姆龙 CIP 符号采集：旁路平台地址块轮询；周期取自本机配置（默认 500ms，扭矩子集）；失败点位置空；每轮记录总耗时。
    /// 厂家（先导/赢合）互斥：同一时刻只加载一家采集块与门控标签；CIP 端口固定 44818。
    /// 每轮先读 Andon 状态（INT16）；状态=7 采扭矩；由 7 变为异常后宽限再采 3 秒，仍异常则停采落盘。
    /// 数组整段连读；结构体成员按批 multi-tag Read（现场实测 batch=20）。
    /// 若单轮 CIP 超过周期，则不等待、立即进入下一轮（实际周期以耗时为准）。
    /// CSV 文件名标签与周期绑定（改周期即切 <c>AIPredictive_{N}ms_*.csv</c>）；周期可运行中热切换，厂家/IP 保存后重连。
    /// </summary>
    public sealed class AIPredictiveAcquisitionService : IAIPredictiveAcquisitionService, IDisposable
    {
        public const int DefaultIntervalMs = AIPredictivePlcEndpoint.DefaultIntervalMs;
        /// <summary>结构体成员多标签批量大小（现场探针验证可用）。</summary>
        public const int StructMultiBatchSize = 20;
        /// <summary>设备状态=运行，允许采扭矩。</summary>
        public const short AndonStatusRunning = 7;
        /// <summary>由运行切到异常后继续采集的宽限秒数。</summary>
        public const double AbnormalGraceSeconds = 3.0;
        /// <summary>机台安灯运行状态标签（按厂家，启动时写入）。</summary>
        private string _andonTag = AIPredictiveVendorNames.AndonTag(AIPredictiveVendorNames.XianDao);
        /// <summary>当前厂家（决定采集块与门控标签；同一时刻只跑一家）。</summary>
        private string _vendor = AIPredictiveVendorNames.XianDao;
        /// <summary>当前采集周期（毫秒）；与 CSV 标签绑定。</summary>
        private int _intervalMs = DefaultIntervalMs;

        private readonly IAIPredictiveCollectBlockLoader _loader;
        private readonly IAIPredictiveCsvStorage _csvStorage;
        private readonly IAIPredictivePlcEndpointStore _endpointStore;
        private readonly ILoggerService<AIPredictiveAcquisitionService> _logger;
        private readonly ConcurrentDictionary<string, AIPredictivePointValue> _latest = new();
        private readonly object _gate = new();

        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _delayCts;
        private Task? _loopTask;
        private OmronConnectedCipNet? _cip;
        private IReadOnlyList<AIPredictiveCollectBlock> _blocks = Array.Empty<AIPredictiveCollectBlock>();
        /// <summary>上一拍 Andon 状态；null=尚未成功读到或读失败后的空窗。</summary>
        private short? _lastAndonStatus;
        /// <summary>上一轮是否因门控允许采了扭矩。</summary>
        private bool _collectAllowedLastRound;
        /// <summary>离开运行态后进入宽限的起点；回到 7 时清空。</summary>
        private DateTime? _abnormalGraceStartedUtc;

        public bool IsRunning { get; private set; }
        public bool IsPlcConnected { get; private set; }
        public string LastStatus { get; private set; } = "未启动";
        public int CurrentIntervalMs => Volatile.Read(ref _intervalMs);
        public long RoundCount { get; private set; }
        public AIPredictiveRoundStats? LastRoundStats { get; private set; }
        public IReadOnlyDictionary<string, AIPredictivePointValue> LatestValues => _latest;
        public event EventHandler? StateChanged;

        public AIPredictiveAcquisitionService(
            IAIPredictiveCollectBlockLoader loader,
            IAIPredictiveCsvStorage csvStorage,
            IAIPredictivePlcEndpointStore endpointStore,
            ILoggerService<AIPredictiveAcquisitionService> logger)
        {
            _loader = loader;
            _csvStorage = csvStorage;
            _endpointStore = endpointStore;
            _logger = logger;
        }

        public Task TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (IsRunning)
                {
                    LastStatus = "采集运行中，请先停止再测连接";
                    RaiseState();
                    return Task.CompletedTask;
                }

                DisposeCip_NoLock();

                ApplyVendorFromEndpoint();
                _blocks = _loader.LoadAll(_vendor);
                if (_blocks.Count == 0)
                {
                    IsPlcConnected = false;
                    LastStatus = $"无采集块（厂家={_vendor}），无法测试连接";
                    _logger.Error($"AIPredictive 连接测试失败：厂家={_vendor} 未加载到任何采集块", true);
                    RaiseState();
                    return Task.CompletedTask;
                }

                var (ip, port) = ResolveEndpoint();
                if (string.IsNullOrWhiteSpace(ip))
                {
                    IsPlcConnected = false;
                    LastStatus = "PLC IP 未配置";
                    _logger.Error("AIPredictive 连接测试失败：PLC IP 为空", true);
                    RaiseState();
                    return Task.CompletedTask;
                }

                OmronConnectedCipNet? cip = null;
                try
                {
                    cip = new OmronConnectedCipNet(ip, port);
                    var connect = cip.ConnectServer();
                    if (!connect.IsSuccess)
                    {
                        IsPlcConnected = false;
                        LastStatus = $"连接测试失败: {connect.Message}";
                        _logger.Error(
                            $"AIPredictive CIP 连接测试失败 {ip}:{port} — {connect.Message}",
                            true);
                        RaiseState();
                        return Task.CompletedTask;
                    }

                    IsPlcConnected = true;
                    LastStatus = $"连接测试成功 {ip}:{port}";
                    _logger.Info($"AIPredictive CIP 连接测试成功 {ip}:{port}", true);
                    RaiseState();
                }
                finally
                {
                    if (cip is not null)
                    {
                        try { cip.ConnectClose(); } catch { /* ignore */ }
                        cip.Dispose();
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (IsRunning)
                    return Task.CompletedTask;

                ApplyVendorFromEndpoint();
                _blocks = _loader.LoadAll(_vendor);
                if (_blocks.Count == 0)
                {
                    IsPlcConnected = false;
                    LastStatus = $"无采集块（厂家={_vendor}），未启动";
                    _logger.Error($"AIPredictive 采集启动失败：厂家={_vendor} 未加载到任何采集块", true);
                    RaiseState();
                    return Task.CompletedTask;
                }

                var (ip, port) = ResolveEndpoint();
                _cip = new OmronConnectedCipNet(ip, port);
                var connect = _cip.ConnectServer();
                if (!connect.IsSuccess)
                {
                    IsPlcConnected = false;
                    LastStatus = $"连接失败: {connect.Message}";
                    _logger.Error(
                        $"AIPredictive CIP 连接失败 厂家={_vendor} {ip}:{port} — {connect.Message}",
                        true);
                    _cip.Dispose();
                    _cip = null;
                    RaiseState();
                    return Task.CompletedTask;
                }

                IsPlcConnected = true;

                _intervalMs = AIPredictivePlcEndpoint.ClampIntervalMs(_endpointStore.Load().IntervalMs);
                var csvTag = AIPredictivePlcEndpoint.FormatIntervalTag(_intervalMs);
                var points = _blocks.SelectMany(b => b.Points).ToList();
                try
                {
                    _csvStorage.PrepareSession(points, _intervalMs);
                }
                catch (Exception ex)
                {
                    _logger.Error("AIPredictive CSV 会话准备失败（采集仍将启动）", ex, true);
                }

                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                IsRunning = true;
                RoundCount = 0;
                _lastAndonStatus = null;
                _collectAllowedLastRound = false;
                _abnormalGraceStartedUtc = null;
                LastStatus =
                    $"运行中（厂家={_vendor}，周期 {_intervalMs}ms，门控={_andonTag}=={AndonStatusRunning}，异常宽限 {AbnormalGraceSeconds}s）";
                _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
                _logger.Info(
                    $"AIPredictive 采集启动：厂家={_vendor}，{ip}:{port}，块={_blocks.Count}，点≈{points.Count}，" +
                    $"周期={_intervalMs}ms，门控={_andonTag}（运行={AndonStatusRunning}，异常宽限={AbnormalGraceSeconds}s），" +
                    $"CSV根={_csvStorage.RootPath}，CSV粒度={csvTag}",
                    false);
                RaiseState();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public int ApplyIntervalMs(int intervalMs)
        {
            var clamped = AIPredictivePlcEndpoint.ClampIntervalMs(intervalMs);
            var tag = AIPredictivePlcEndpoint.FormatIntervalTag(clamped);

            lock (_gate)
            {
                Volatile.Write(ref _intervalMs, clamped);

                if (IsRunning && _blocks.Count > 0)
                {
                    var points = _blocks.SelectMany(b => b.Points).ToList();
                    try
                    {
                        _csvStorage.PrepareSession(points, clamped);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("AIPredictive 热切换周期时 CSV 会话准备失败", ex, true);
                    }

                    LastStatus =
                        $"周期已热切换为 {clamped}ms（CSV={tag}）；厂家={_vendor}，门控={_andonTag}";
                    _logger.Info(
                        $"AIPredictive 采集周期热切换：{clamped}ms，CSV粒度={tag}，厂家={_vendor}",
                        true);
                }
                else
                {
                    LastStatus = $"周期已设为 {clamped}ms（下次启动生效，CSV={tag}）";
                }

                try
                {
                    _delayCts?.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // 等待令牌已释放，忽略
                }

                RaiseState();
            }

            return clamped;
        }

        /// <summary>从本机配置刷新厂家与门控标签（先导/赢合互斥）。</summary>
        private void ApplyVendorFromEndpoint()
        {
            var ep = _endpointStore.Load();
            _vendor = AIPredictiveVendorNames.Normalize(ep.Vendor);
            _andonTag = AIPredictiveVendorNames.AndonTag(_vendor);
        }

        /// <summary>约每秒一条 Info（按当前周期折算轮次）。</summary>
        private static int ResolveRoundLogEveryN(int intervalMs)
        {
            var ms = AIPredictivePlcEndpoint.ClampIntervalMs(intervalMs);
            return Math.Max(1, (int)Math.Round(1000.0 / ms));
        }

        /// <summary>优先本机配置 IP；端口永远 44818。</summary>
        private (string Ip, int Port) ResolveEndpoint()
        {
            var port = AIPredictiveVendorNames.FixedCipPort;
            var ep = _endpointStore.Load();
            if (!string.IsNullOrWhiteSpace(ep.Ip))
                return (ep.Ip.Trim(), port);

            var first = _blocks.FirstOrDefault();
            if (first is not null && !string.IsNullOrWhiteSpace(first.Ip))
                return (first.Ip.Trim(), port);

            return (AIPredictiveVendorNames.DefaultIp(_vendor), port);
        }

        private void DisposeCip_NoLock()
        {
            if (_cip is null)
                return;
            try { _cip.ConnectClose(); } catch { /* ignore */ }
            _cip.Dispose();
            _cip = null;
        }

        public async Task StopAsync()
        {
            Task? loop;
            lock (_gate)
            {
                if (!IsRunning)
                    return;
                IsRunning = false;
                _cts?.Cancel();
                loop = _loopTask;
            }

            try
            {
                if (loop is not null)
                    await loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            finally
            {
                try
                {
                    await _csvStorage.FlushAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error("AIPredictive CSV 停止刷盘失败", ex, false);
                }

                lock (_gate)
                {
                    _cts?.Dispose();
                    _cts = null;
                    _loopTask = null;
                    DisposeCip_NoLock();
                    IsPlcConnected = false;
                    LastStatus = "已停止";
                    RaiseState();
                    _logger.Info("AIPredictive 采集已停止", false);
                }
            }
        }

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                AIPredictiveRoundStats stats;
                try
                {
                    stats = await ReadAllOnceAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error("AIPredictive 采集轮次异常", ex, true);
                    stats = new AIPredictiveRoundStats
                    {
                        ElapsedMs = sw.ElapsedMilliseconds,
                        SuccessCount = 0,
                        FailCount = 0,
                        FinishedUtc = DateTime.UtcNow,
                    };
                }

                sw.Stop();
                RoundCount++;
                stats.ElapsedMs = sw.ElapsedMilliseconds;
                LastRoundStats = stats;
                var andonText = stats.AndonStatus.HasValue
                    ? FormatAndonStatus(stats.AndonStatus.Value)
                    : "读失败";
                LastStatus = stats.TorqueCollected
                    ? (stats.InAbnormalGrace
                        ? $"第{RoundCount}轮 异常宽限采 耗时{stats.ElapsedMs}ms 成功{stats.SuccessCount} 失败{stats.FailCount} 状态={andonText}"
                        : $"第{RoundCount}轮 运行中 耗时{stats.ElapsedMs}ms 成功{stats.SuccessCount} 失败{stats.FailCount} 状态={andonText}")
                    : $"第{RoundCount}轮 门控跳过 耗时{stats.ElapsedMs}ms 状态={andonText}";

                var intervalMs = CurrentIntervalMs;
                var overrun = stats.TorqueCollected && stats.ElapsedMs > intervalMs;
                if (overrun)
                {
                    _logger.Warn(
                        $"AIPredictive 采集超时：轮次={RoundCount}，总耗时={stats.ElapsedMs}ms > 周期={intervalMs}ms，" +
                        $"成功={stats.SuccessCount}，失败={stats.FailCount}，状态={andonText}",
                        false);
                }
                else if (stats.TorqueCollected
                         && (RoundCount == 1 || RoundCount % ResolveRoundLogEveryN(intervalMs) == 0))
                {
                    _logger.Info(
                        $"AIPredictive 采集完成：轮次={RoundCount}，总耗时={stats.ElapsedMs}ms，" +
                        $"成功={stats.SuccessCount}，失败={stats.FailCount}，" +
                        $"数组块={stats.ArrayBlockCount}，结构体成员请求={stats.StructMemberCount}，状态={andonText}",
                        false);
                }

                try
                {
                    // 仅机台运行且本轮读了扭矩才落盘，避免停机时用旧值刷 CSV
                    if (stats.TorqueCollected)
                    {
                        await _csvStorage.WriteRoundAsync(DateTime.Now, RoundCount, stats, _latest)
                            .ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("AIPredictive CSV 落盘异常", ex, false);
                }

                RaiseState();

                var wait = intervalMs - (int)sw.ElapsedMilliseconds;
                if (wait > 0)
                {
                    CancellationTokenSource? delayCts = null;
                    try
                    {
                        delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        Volatile.Write(ref _delayCts, delayCts);
                        await Task.Delay(wait, delayCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (ct.IsCancellationRequested)
                            break;
                        // 周期热切换打断等待：立即进入下一轮
                    }
                    finally
                    {
                        Interlocked.CompareExchange(ref _delayCts, null, delayCts);
                        delayCts?.Dispose();
                    }
                }
            }
        }

        private Task<AIPredictiveRoundStats> ReadAllOnceAsync(CancellationToken ct)
        {
            return Task.Run(() =>
            {
                var cip = _cip ?? throw new InvalidOperationException("CIP 未连接");
                var now = DateTime.UtcNow;

                var andon = TryReadAndonStatus(cip, out var andonError);
                NotifyAndonStatusChanged(andon, andonError);

                var allowCollect = DecideCollectAllowed(andon, out var inGrace);
                _collectAllowedLastRound = allowCollect;

                if (!allowCollect)
                {
                    return new AIPredictiveRoundStats
                    {
                        SuccessCount = 0,
                        FailCount = 0,
                        ArrayBlockCount = 0,
                        StructMemberCount = 0,
                        FinishedUtc = DateTime.UtcNow,
                        TorqueCollected = false,
                        AndonStatus = andon,
                        InAbnormalGrace = false,
                    };
                }

                var success = 0;
                var fail = 0;
                var arrayBlocks = 0;
                var structMembers = 0;

                // 结构体 FLOAT/BOOL 跨块汇总后按批 multi-read（减少 CIP 往返）
                var floatMembers = new List<AIPredictivePointDef>(1280);
                var boolMembers = new List<AIPredictivePointDef>(768);

                foreach (var block in _blocks)
                {
                    ct.ThrowIfCancellationRequested();
                    if (block.Mode == AIPredictiveCollectMode.ArrayBatch)
                    {
                        arrayBlocks++;
                        var (ok, bad) = ReadArrayBlock(cip, block, now);
                        success += ok;
                        fail += bad;
                        continue;
                    }

                    foreach (var point in block.Points)
                    {
                        structMembers++;
                        var type = point.PlatformType.ToUpperInvariant();
                        if (type is "FLOAT" or "REAL" or "SINGLE" or "DOUBLE" or "LREAL")
                            floatMembers.Add(point);
                        else if (type is "BOOL")
                            boolMembers.Add(point);
                        else
                        {
                            SetFail(point, $"不支持的类型 {point.PlatformType}", now);
                            fail++;
                        }
                    }
                }

                ct.ThrowIfCancellationRequested();
                var (fOk, fBad) = ReadStructFloatMulti(cip, floatMembers, now, ct);
                success += fOk;
                fail += fBad;

                ct.ThrowIfCancellationRequested();
                var (bOk, bBad) = ReadStructBoolMulti(cip, boolMembers, now, ct);
                success += bOk;
                fail += bBad;

                return new AIPredictiveRoundStats
                {
                    SuccessCount = success,
                    FailCount = fail,
                    ArrayBlockCount = arrayBlocks,
                    StructMemberCount = structMembers,
                    FinishedUtc = DateTime.UtcNow,
                    TorqueCollected = true,
                    AndonStatus = andon,
                    InAbnormalGrace = inGrace,
                };
            }, ct);
        }

        /// <summary>
        /// 运行(=7)立即采；由采中切入异常后宽限 <see cref="AbnormalGraceSeconds"/> 秒仍采；
        /// 宽限结束仍异常则停，直到再次回到 7。启动时已是异常则不采。
        /// </summary>
        private bool DecideCollectAllowed(short? andon, out bool inGrace)
        {
            inGrace = false;
            if (andon == AndonStatusRunning)
            {
                if (_abnormalGraceStartedUtc is not null)
                {
                    _logger.Info(
                        $"AIPredictive 机台恢复运行，结束异常宽限（曾于 {_abnormalGraceStartedUtc:HH:mm:ss.fff} UTC 进入）",
                        true);
                }

                _abnormalGraceStartedUtc = null;
                return true;
            }

            // 异常或读失败
            if (_collectAllowedLastRound && _abnormalGraceStartedUtc is null)
            {
                _abnormalGraceStartedUtc = DateTime.UtcNow;
                _logger.Info(
                    $"AIPredictive 机台离开运行态，进入异常宽限 {AbnormalGraceSeconds}s 继续采集（防漏数）",
                    true);
            }

            if (_abnormalGraceStartedUtc is null)
                return false;

            var elapsed = (DateTime.UtcNow - _abnormalGraceStartedUtc.Value).TotalSeconds;
            if (elapsed < AbnormalGraceSeconds)
            {
                inGrace = true;
                return true;
            }

            // 宽限已过：保持 started 标记，避免每轮重新开宽限，直到回到 7 才清空
            return false;
        }

        /// <summary>读机台安灯状态；失败返回 null。</summary>
        private short? TryReadAndonStatus(OmronConnectedCipNet cip, out string? error)
        {
            try
            {
                var result = cip.ReadInt16(_andonTag, 1);
                if (!result.IsSuccess || result.Content is null || result.Content.Length < 1)
                {
                    error = result.Message ?? "ReadInt16 失败";
                    _logger.Warn($"AIPredictive 读机台状态失败 {_andonTag}: {error}", false);
                    return null;
                }

                error = null;
                return result.Content[0];
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger.Warn($"AIPredictive 读机台状态异常 {_andonTag}: {ex.Message}", false);
                return null;
            }
        }

        private void NotifyAndonStatusChanged(short? current, string? readError)
        {
            if (current is null)
            {
                if (_lastAndonStatus is not null)
                {
                    _logger.Warn(
                        $"AIPredictive 机台状态变更：{FormatAndonStatus(_lastAndonStatus.Value)} → 读失败" +
                        (string.IsNullOrWhiteSpace(readError) ? "" : $"（{readError}）"),
                        true);
                    _lastAndonStatus = null;
                }

                return;
            }

            if (_lastAndonStatus == current.Value)
                return;

            var from = _lastAndonStatus.HasValue
                ? FormatAndonStatus(_lastAndonStatus.Value)
                : "（启动/未知）";
            var to = FormatAndonStatus(current.Value);
            _logger.Info($"AIPredictive 机台状态变更：{from} → {to}", true);
            _lastAndonStatus = current.Value;
        }

        private static string FormatAndonStatus(short code) => code switch
        {
            7 => "7(运行)",
            18 => "18(停机)",
            19 => "19(待机)",
            9 => "9(故障)",
            _ => $"{code}(未知)",
        };

        private (int ok, int bad) ReadArrayBlock(OmronConnectedCipNet cip, AIPredictiveCollectBlock block, DateTime now)
        {
            var type = block.PlatformType.ToUpperInvariant();
            var length = (ushort)Math.Max(block.ElementCount, 1);

            if (type is "FLOAT" or "REAL" or "SINGLE")
            {
                var result = cip.ReadFloat(block.SymbolName, length);
                if (!result.IsSuccess || result.Content is null)
                {
                    foreach (var p in block.Points)
                        SetFail(p, result.Message, now);
                    _logger.Warn($"数组连读失败 {block.SymbolName}: {result.Message}", false);
                    return (0, block.Points.Count);
                }

                var ok = 0;
                var bad = 0;
                foreach (var p in block.Points)
                {
                    var idx = p.ArrayIndex ?? -1;
                    if (idx < 0 || idx >= result.Content.Length)
                    {
                        SetFail(p, "下标超出返回长度", now);
                        bad++;
                    }
                    else
                    {
                        SetOk(p, result.Content[idx], now);
                        ok++;
                    }
                }

                return (ok, bad);
            }

            if (type is "BOOL")
            {
                var result = cip.ReadBool(block.SymbolName, length);
                if (!result.IsSuccess || result.Content is null)
                {
                    foreach (var p in block.Points)
                        SetFail(p, result.Message, now);
                    _logger.Warn($"数组连读失败 {block.SymbolName}: {result.Message}", false);
                    return (0, block.Points.Count);
                }

                var ok = 0;
                var bad = 0;
                foreach (var p in block.Points)
                {
                    var idx = p.ArrayIndex ?? -1;
                    if (idx < 0 || idx >= result.Content.Length)
                    {
                        SetFail(p, "下标超出返回长度", now);
                        bad++;
                    }
                    else
                    {
                        SetOk(p, result.Content[idx], now);
                        ok++;
                    }
                }

                return (ok, bad);
            }

            foreach (var p in block.Points)
                SetFail(p, $"不支持的数组类型 {block.PlatformType}", now);
            return (0, block.Points.Count);
        }

        /// <summary>
        /// 结构体 FLOAT：多标签一次 Read，载荷为连续 little-endian float32（现场方案 E / P0）。
        /// </summary>
        private (int ok, int bad) ReadStructFloatMulti(
            OmronConnectedCipNet cip,
            List<AIPredictivePointDef> points,
            DateTime now,
            CancellationToken ct)
        {
            if (points.Count == 0)
                return (0, 0);

            var ok = 0;
            var bad = 0;
            for (var i = 0; i < points.Count; i += StructMultiBatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var n = Math.Min(StructMultiBatchSize, points.Count - i);
                var batch = points.GetRange(i, n);
                try
                {
                    var addrs = new string[n];
                    var lens = new ushort[n];
                    for (var k = 0; k < n; k++)
                    {
                        addrs[k] = batch[k].CipAddress;
                        lens[k] = 1;
                    }

                    var result = cip.Read(addrs, lens);
                    if (!result.IsSuccess || result.Content is null)
                    {
                        var err = result.Message ?? "multi-read failed";
                        foreach (var p in batch)
                            SetFail(p, err, now);
                        bad += n;
                        _logger.Warn($"结构体 FLOAT 批量读失败 batch@{i} n={n}: {err}", false);
                        continue;
                    }

                    var bytes = result.Content;
                    var parsed = Math.Min(n, bytes.Length / 4);
                    for (var k = 0; k < n; k++)
                    {
                        if (k < parsed)
                        {
                            var value = BitConverter.ToSingle(bytes, k * 4);
                            SetOk(batch[k], value, now);
                            ok++;
                        }
                        else
                        {
                            SetFail(batch[k], $"批量载荷不足 expect={n * 4} got={bytes.Length}", now);
                            bad++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    foreach (var p in batch)
                        SetFail(p, ex.Message, now);
                    bad += n;
                    _logger.Warn($"结构体 FLOAT 批量读异常 batch@{i}: {ex.Message}", false);
                }
            }

            return (ok, bad);
        }

        /// <summary>
        /// 结构体 BOOL：多标签一次 Read；欧姆龙 CIP 常见每项 1/2/4 字节，非 0 为 true。
        /// </summary>
        private (int ok, int bad) ReadStructBoolMulti(
            OmronConnectedCipNet cip,
            List<AIPredictivePointDef> points,
            DateTime now,
            CancellationToken ct)
        {
            if (points.Count == 0)
                return (0, 0);

            var ok = 0;
            var bad = 0;
            for (var i = 0; i < points.Count; i += StructMultiBatchSize)
            {
                ct.ThrowIfCancellationRequested();
                var n = Math.Min(StructMultiBatchSize, points.Count - i);
                var batch = points.GetRange(i, n);
                try
                {
                    var addrs = new string[n];
                    var lens = new ushort[n];
                    for (var k = 0; k < n; k++)
                    {
                        addrs[k] = batch[k].CipAddress;
                        lens[k] = 1;
                    }

                    var result = cip.Read(addrs, lens);
                    if (!result.IsSuccess || result.Content is null)
                    {
                        var err = result.Message ?? "multi-read failed";
                        foreach (var p in batch)
                            SetFail(p, err, now);
                        bad += n;
                        _logger.Warn($"结构体 BOOL 批量读失败 batch@{i} n={n}: {err}", false);
                        continue;
                    }

                    var bytes = result.Content;
                    int stride;
                    if (bytes.Length == n) stride = 1;
                    else if (bytes.Length == n * 2) stride = 2;
                    else if (bytes.Length == n * 4) stride = 4;
                    else
                    {
                        var err = $"BOOL 批量载荷长度异常 expect={n}/{n * 2}/{n * 4} got={bytes.Length}";
                        foreach (var p in batch)
                            SetFail(p, err, now);
                        bad += n;
                        _logger.Warn($"结构体 BOOL 批量读失败 batch@{i}: {err}", false);
                        continue;
                    }

                    for (var k = 0; k < n; k++)
                    {
                        var flag = bytes[k * stride] != 0;
                        SetOk(batch[k], flag, now);
                        ok++;
                    }
                }
                catch (Exception ex)
                {
                    foreach (var p in batch)
                        SetFail(p, ex.Message, now);
                    bad += n;
                    _logger.Warn($"结构体 BOOL 批量读异常 batch@{i}: {ex.Message}", false);
                }
            }

            return (ok, bad);
        }

        private void SetOk(AIPredictivePointDef point, object? value, DateTime now)
        {
            _latest[point.PointKey] = new AIPredictivePointValue
            {
                PointKey = point.PointKey,
                CipAddress = point.CipAddress,
                Value = value,
                Success = true,
                TimestampUtc = now,
            };
        }

        private void SetFail(AIPredictivePointDef point, string? error, DateTime now)
        {
            _latest[point.PointKey] = new AIPredictivePointValue
            {
                PointKey = point.PointKey,
                CipAddress = point.CipAddress,
                Value = null,
                Success = false,
                Error = error,
                TimestampUtc = now,
            };
        }

        private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
