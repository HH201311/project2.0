using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive
{
    /// <summary>
    /// 采集时序 CSV 落盘：按天目录、单文件约 100MB 滚动、保留 30 天。
    /// 粒度标签与 CIP 基周期绑定：<c>AIPredictive_{IntervalMs}ms_*.csv</c>。
    /// </summary>
    public interface IAIPredictiveCsvStorage
    {
        /// <summary>根目录（默认 D:\AIPredictiveData）。</summary>
        string RootPath { get; }

        /// <summary>当前正在写入的采集粒度文件（可能为空）。</summary>
        string? CurrentFilePath { get; }

        /// <summary>当前会话采集周期（毫秒）；未准备会话时为 0。</summary>
        int CurrentIntervalMs { get; }

        /// <summary>当前会话 CSV 标签（如 <c>500ms</c>）；未准备会话时为空。</summary>
        string? CurrentIntervalTag { get; }

        /// <summary>
        /// 开始/重开会话：固定列顺序、清理超期目录；文件名标签随 <paramref name="intervalMs"/> 变。
        /// 列顺序与点位一致；表头优先用 <see cref="AIPredictivePointDef.DisplayName"/>（参数中文名），
        /// 查找写入仍用 <see cref="AIPredictivePointDef.PointKey"/>。
        /// </summary>
        void PrepareSession(IReadOnlyList<AIPredictivePointDef> points, int intervalMs);

        /// <summary>
        /// 写入一轮快照到当前粒度 CSV（宽表：一行=一轮）。
        /// </summary>
        Task WriteRoundAsync(
            DateTime timestampLocal,
            long round,
            AIPredictiveRoundStats stats,
            IReadOnlyDictionary<string, AIPredictivePointValue> values);

        Task FlushAsync();

        /// <summary>删除超过保留天数的日期文件夹。</summary>
        int CleanupExpiredFolders(int retentionDays = 30);
    }
}
