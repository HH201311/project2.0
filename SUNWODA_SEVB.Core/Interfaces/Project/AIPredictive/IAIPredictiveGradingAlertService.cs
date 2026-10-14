using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive
{
    /// <summary>
    /// 调用 GradingAlert Python 增量脚本；触发 UI 告警（有一级/二级即弹，不抑制）。
    /// </summary>
    public interface IAIPredictiveGradingAlertService
    {
        AIPredictiveGradingConfig Config { get; }

        /// <summary>执行一轮增量推理；无新数据则跳过。</summary>
        Task<AIPredictiveGradingRunResult> RunOnceAsync(CancellationToken cancellationToken = default);

        /// <summary>需要弹红遮罩时由 UI 订阅。</summary>
        event Action<IReadOnlyList<AIPredictiveGradingAlertItem>>? AlertsRaised;
    }
}
