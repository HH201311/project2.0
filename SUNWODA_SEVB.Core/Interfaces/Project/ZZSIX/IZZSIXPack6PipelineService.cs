using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    /// <summary>中央处理管线（对齐旧 ProcessCentralQueueThreadFunc）。</summary>
    public interface IZZSIXPack6PipelineService : IDisposable
    {
        event Action<string>? LogEmitted;
        event Action<Pack6FrameResult>? FrameCompleted;
        event Action<Pack6ProductResult>? ProductCompleted;
        bool IsRunning { get; }
        Task StartAsync();
        Task StopAsync();
        void Enqueue(Pack6IngressItem item);
        void ResetStatistics();
        IReadOnlyList<Pack6StationStat> StationStats { get; }
    }
}
