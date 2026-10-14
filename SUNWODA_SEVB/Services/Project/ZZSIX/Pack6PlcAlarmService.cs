using HslCommunication;
using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.PLC;
using SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX;
using SUNWODA_SEVB.Core.Models.PLC;
using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Services.Project.ZZSIX
{
    /// <summary>
    /// 欧姆龙 D1996：.0 报警写；.1 本机每秒翻转心跳；.2 读对方心跳，5 秒无跳变判断连。
    /// </summary>
    public sealed class Pack6PlcAlarmService : IZZSIXPack6PlcAlarmService
    {
        private readonly IPLCService _plcService;
        private readonly IZZSIXPack6ConfigService _configService;
        private readonly ILoggerService<Pack6PlcAlarmService> _logger;
        private readonly object _gate = new();
        private int _openWindows;
        private bool _reachable;

        private bool _outHeartbeatBit;
        private bool? _lastPeerBit;
        private DateTime _lastPeerToggleUtc = DateTime.MinValue;
        private DateTime _lastOutWriteUtc = DateTime.MinValue;

        public int OpenWindowCount
        {
            get { lock (_gate) return _openWindows; }
        }

        public bool IsPlcReachable
        {
            get { lock (_gate) return _reachable; }
            private set
            {
                bool changed;
                lock (_gate)
                {
                    changed = _reachable != value;
                    _reachable = value;
                }
                if (changed)
                    PlcReachabilityChanged?.Invoke(value);
            }
        }

        public event Action<bool>? PlcReachabilityChanged;

        public Pack6PlcAlarmService(
            IPLCService plcService,
            IZZSIXPack6ConfigService configService,
            ILoggerService<Pack6PlcAlarmService> logger)
        {
            _plcService = plcService;
            _configService = configService;
            _logger = logger;
        }

        public async Task NotifyWindowOpenedAsync(CancellationToken ct = default)
        {
            lock (_gate)
                _openWindows++;

            await WriteAlarmBitAsync(true, ct).ConfigureAwait(false);
        }

        public async Task NotifyWindowClosedAsync(CancellationToken ct = default)
        {
            int remain;
            lock (_gate)
            {
                if (_openWindows > 0)
                    _openWindows--;
                remain = _openWindows;
            }

            if (remain == 0)
                await WriteAlarmBitAsync(false, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 心跳节拍：到点翻转写 .1；读 .2 检测跳变；超过超时无跳变则断连。
        /// 建议调用方约 500ms 一轮，写间隔 1s，超时 5s。
        /// </summary>
        public async Task<bool> ProbeAsync(CancellationToken ct = default)
        {
            try
            {
                var cfg = _configService.Current;
                var outAddr = string.IsNullOrWhiteSpace(cfg.PlcHeartbeatOutBitAddress)
                    ? "D1996.1"
                    : cfg.PlcHeartbeatOutBitAddress.Trim();
                var inAddr = string.IsNullOrWhiteSpace(cfg.PlcHeartbeatInBitAddress)
                    ? "D1996.2"
                    : cfg.PlcHeartbeatInBitAddress.Trim();
                var writeInterval = TimeSpan.FromMilliseconds(Math.Max(200, cfg.PlcHeartbeatIntervalMs));
                var timeout = TimeSpan.FromMilliseconds(Math.Max(1000, cfg.PlcHeartbeatTimeoutMs));

                var plc = await ResolvePlcAsync(ct).ConfigureAwait(false);
                if (plc?.Device is null)
                {
                    IsPlcReachable = false;
                    return false;
                }

                ct.ThrowIfCancellationRequested();
                var now = DateTime.UtcNow;

                // 本机心跳：到间隔翻转写 .1
                if (now - _lastOutWriteUtc >= writeInterval)
                {
                    _outHeartbeatBit = !_outHeartbeatBit;
                    OperateResult write = await plc.Device.WriteAsync(outAddr, _outHeartbeatBit).ConfigureAwait(false);
                    _lastOutWriteUtc = now;
                    if (!write.IsSuccess)
                    {
                        plc.IsConnect = false;
                        IsPlcReachable = false;
                        _logger.Warn($"六面检PLC写心跳 {outAddr}={_outHeartbeatBit} 失败: {write.Message}", false);
                        return false;
                    }
                    plc.IsConnect = true;
                }

                // 对方心跳：读 .2，值变化则刷新“最后跳变时刻”
                OperateResult<bool> read = await plc.Device.ReadBoolAsync(inAddr).ConfigureAwait(false);
                if (!read.IsSuccess)
                {
                    plc.IsConnect = false;
                    IsPlcReachable = false;
                    _logger.Warn($"六面检PLC读心跳 {inAddr} 失败: {read.Message}", false);
                    return false;
                }

                plc.IsConnect = true;
                var bit = read.Content;
                if (_lastPeerBit is null)
                {
                    _lastPeerBit = bit;
                    // 首次采样只建基线，等下一次跳变再判在线
                }
                else if (_lastPeerBit.Value != bit)
                {
                    _lastPeerBit = bit;
                    _lastPeerToggleUtc = now;
                }

                var ok = _lastPeerToggleUtc != DateTime.MinValue
                         && now - _lastPeerToggleUtc < timeout;
                IsPlcReachable = ok;
                return ok;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                IsPlcReachable = false;
                _logger.Warn($"六面检PLC心跳异常: {ex.Message}", false);
                return false;
            }
        }

        private async Task WriteAlarmBitAsync(bool value, CancellationToken ct)
        {
            try
            {
                var plc = await ResolvePlcAsync(ct).ConfigureAwait(false);
                if (plc?.Device is null)
                {
                    _logger.Warn($"六面检PLC未就绪，无法写报警位={(value ? "true" : "false")}", false);
                    return;
                }

                var addr = _configService.Current.PlcNgAlarmBitAddress;
                if (string.IsNullOrWhiteSpace(addr))
                    addr = "D1996.0";

                ct.ThrowIfCancellationRequested();
                OperateResult result = await plc.Device.WriteAsync(addr, value).ConfigureAwait(false);
                if (result.IsSuccess)
                    _logger.Info($"六面检PLC写 {addr}={(value ? "true" : "false")} 成功（开窗数={OpenWindowCount}）", false);
                else
                {
                    _logger.Warn($"六面检PLC写 {addr} 失败: {result.Message}", false);
                    IsPlcReachable = false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Warn($"六面检PLC写报警位异常: {ex.Message}", false);
            }
        }

        private async Task<PLCDevice?> ResolvePlcAsync(CancellationToken ct)
        {
            var name = _configService.Current.PlcDeviceName;
            if (string.IsNullOrWhiteSpace(name))
                name = "六面检PLC";

            var plc = _plcService.GetPLCByName(name);
            if (plc is null)
            {
                plc = _plcService.PLCs.Values.FirstOrDefault(p => p.Device is not null)
                      ?? _plcService.PLCs.Values.FirstOrDefault();
            }

            if (plc is null)
                return null;

            if (!plc.IsConnect)
                await _plcService.EnsurePLCConnectedAsync(plc, ct).ConfigureAwait(false);

            return plc;
        }
    }
}
