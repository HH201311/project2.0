using SUNWODA_SEVB.Core.Interfaces;
using SUNWODA_SEVB.Core.Interfaces.Project.NJSIX;

namespace SUNWODA_SEVB.Services.Project.NJSIX
{
    /// <summary>
    /// 南京暂无 PLC 报警/心跳：空实现，不写设备、不探活。
    /// 接口保留以便日后恢复，无需改 DI / VM 签名。
    /// </summary>
    public sealed class Pack6PlcAlarmService : INJSIXPack6PlcAlarmService
    {
        private readonly ILoggerService<Pack6PlcAlarmService> _logger;

        public Pack6PlcAlarmService(ILoggerService<Pack6PlcAlarmService> logger)
        {
            _logger = logger;
        }

        public int OpenWindowCount => 0;

        public bool IsPlcReachable => false;

        public event Action<bool>? PlcReachabilityChanged;

        public Task NotifyWindowOpenedAsync(CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task NotifyWindowClosedAsync(CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> ProbeAsync(CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
