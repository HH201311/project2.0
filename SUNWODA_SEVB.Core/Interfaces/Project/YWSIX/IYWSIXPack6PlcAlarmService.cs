using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.YWSIX
{
    /// <summary>
    /// PLC 报警/心跳（南京暂空实现，不写设备）。接口保留以便日后恢复。
    /// </summary>
    public interface IYWSIXPack6PlcAlarmService
    {
        /// <summary>当前仍打开的报警窗数量（南京恒为 0）。</summary>
        int OpenWindowCount { get; }

        /// <summary>PLC 是否可达（南京恒为 false）。</summary>
        bool IsPlcReachable { get; }

        /// <summary>连通状态变化（南京不触发）。</summary>
        event Action<bool>? PlcReachabilityChanged;

        /// <summary>报警窗已打开（南京空操作）。</summary>
        Task NotifyWindowOpenedAsync(CancellationToken ct = default);

        /// <summary>报警窗已关闭（南京空操作）。</summary>
        Task NotifyWindowClosedAsync(CancellationToken ct = default);

        /// <summary>心跳探活（南京恒返回 false）。</summary>
        Task<bool> ProbeAsync(CancellationToken ct = default);
    }
}
