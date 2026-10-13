using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.YWSIX
{
    /// <summary>中央处理管线（对齐旧 ProcessCentralQueueThreadFunc）。</summary>
    public interface IYWSIXPack6PipelineService : IDisposable
    {
        event Action<string>? LogEmitted;
        event Action<Pack6FrameResult>? FrameCompleted;
        event Action<Pack6ProductResult>? ProductCompleted;
        bool IsRunning { get; }
        Task StartAsync();
        Task StopAsync();
        void Enqueue(Pack6IngressItem item);
        void ResetStatistics();
        /// <summary>启动/进页时从磁盘恢复工位计数。</summary>
        void LoadPersistedStatistics();
        /// <summary>落盘工位计数 + 结果日志快照。</summary>
        void PersistStationStatistics(IEnumerable<Pack6UiResultLogDto> logs);
        IReadOnlyList<Pack6StationStat> StationStats { get; }
    }
}
