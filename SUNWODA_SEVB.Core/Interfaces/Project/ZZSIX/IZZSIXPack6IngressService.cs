using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    /// <summary>文件夹收图 → 按队列入队。仅 FileSystemWatcher 新文件；会话去重，无回看/已处理索引。</summary>
    public interface IZZSIXPack6IngressService : IDisposable
    {
        event Action<Pack6IngressItem>? ItemEnqueued;
        /// <summary>监视状态文案（供 UI 日志）。</summary>
        event Action<string>? StatusEmitted;
        bool IsRunning { get; }
        Task StartAsync(Pack6Config config);
        Task StopAsync();
        /// <summary>模拟注入一帧（UseSimulation / 联调）。</summary>
        void EnqueueSimulated(string queueName, string? productCode = null);

        /// <summary>管线处理完后回调；收图侧可做会话去重集裁剪。</summary>
        void NotifyProcessed(string fullPath);
    }
}
