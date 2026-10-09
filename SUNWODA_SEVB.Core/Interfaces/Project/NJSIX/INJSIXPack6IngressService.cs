using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    /// <summary>文件夹收图 → 按队列入队。Watcher 新文件 + 按 SN 补扫；会话去重，无整天回看/已处理索引。</summary>
    public interface INJSIXPack6IngressService : IDisposable
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

        /// <summary>
        /// 按产品 SN 补扫 Y/Z 当日（或 hint）日期夹下已有图：写稳后入队，会话 _seen 去重。
        /// 用于中途开检漏图、缺张结案前兜底。
        /// </summary>
        /// <returns>新调度入队的文件数（含写稳等待）。</returns>
        Task<int> BackfillProductFolderAsync(
            string productSn,
            string? dateFolderHint = null,
            CancellationToken ct = default);
    }
}
