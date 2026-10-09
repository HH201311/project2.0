using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    /// <summary>
    /// 欧姆龙 D1996：.0 报警；.1 本机心跳翻转写；.2 对方心跳读跳变；超时无跳变断连。
    /// </summary>
    public interface IZZSIXPack6PlcAlarmService
    {
        /// <summary>当前仍打开的报警窗数量。</summary>
        int OpenWindowCount { get; }

        /// <summary>对方心跳是否在超时内仍有跳变。</summary>
        bool IsPlcReachable { get; }

        /// <summary>连通状态变化（UI 顶栏呼吸灯）。</summary>
        event Action<bool>? PlcReachabilityChanged;

        /// <summary>报警窗已打开：计数+1 并写报警位 true。</summary>
        Task NotifyWindowOpenedAsync(CancellationToken ct = default);

        /// <summary>报警窗已关闭：计数-1；归零时写报警位 false。</summary>
        Task NotifyWindowClosedAsync(CancellationToken ct = default);

        /// <summary>心跳节拍：写 .1 翻转 + 读 .2 跳变（建议约 500ms 调用）。</summary>
        Task<bool> ProbeAsync(CancellationToken ct = default);
    }
}
