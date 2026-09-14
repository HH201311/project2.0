using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    /// <summary>文件夹收图 → 按队列入队（对齐旧 FolderMonitorModel + OnFileCreated）。</summary>
    public interface IZZSIXPack6IngressService : IDisposable
    {
        event Action<Pack6IngressItem>? ItemEnqueued;
        bool IsRunning { get; }
        Task StartAsync(Pack6Config config);
        Task StopAsync();
        /// <summary>模拟注入一帧（UseSimulation / 联调）。</summary>
        void EnqueueSimulated(string queueName, string? productCode = null);
    }
}
