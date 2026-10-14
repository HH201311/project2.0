using SUNWODA_SEVB.Core.Models.Project.AIPredictive;

namespace SUNWODA_SEVB.Core.Interfaces.Project.AIPredictive
{
    /// <summary>从输出目录按当前厂家加载采集块 CSV（先导/赢合二选一）。</summary>
    public interface IAIPredictiveCollectBlockLoader
    {
        /// <summary>按本机已保存厂家加载；未指定时读 endpoint 配置。</summary>
        IReadOnlyList<AIPredictiveCollectBlock> LoadAll();

        /// <summary>按指定厂家加载（用于保存前预览或强制切换）。</summary>
        IReadOnlyList<AIPredictiveCollectBlock> LoadAll(string vendor);
    }

    /// <summary>CIP 符号采集：默认 500ms 一轮，失败置空，并记录每轮耗时。</summary>
    public interface IAIPredictiveAcquisitionService
    {
        bool IsRunning { get; }
        /// <summary>
        /// 采集运行中：表示 CIP 会话已建立；
        /// 未采集时：表示最近一次「测试连接」是否成功。
        /// </summary>
        bool IsPlcConnected { get; }
        string LastStatus { get; }
        long RoundCount { get; }
        AIPredictiveRoundStats? LastRoundStats { get; }
        IReadOnlyDictionary<string, AIPredictivePointValue> LatestValues { get; }
        event EventHandler? StateChanged;
        /// <summary>仅探测 CIP 能否连通（不启动采集循环）；测完断开会话，结果反映到 <see cref="IsPlcConnected"/>。</summary>
        Task TestConnectionAsync(CancellationToken cancellationToken = default);
        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();
    }
}
