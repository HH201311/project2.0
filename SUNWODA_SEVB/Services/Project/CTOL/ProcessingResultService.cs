namespace SUNWODA_SEVB.Services.Project.CTOL
{
    /// <summary>
    /// 图片处理结果通知总线（单例）。
    ///
    /// 处理器完成一张图片后通过 <see cref="NotifyProcessed"/> 发布事件，
    /// ViewModel 订阅后在 UI 线程更新计数和预览图。
    /// </summary>
    public sealed class ProcessingResultService
    {
        /// <summary>
        /// 处理完成事件：(monitorId, outputFilePath)
        /// 从后台线程触发，订阅者自行负责切换到 UI 线程。
        /// </summary>
        public event Action<string, string>? ImageProcessed;

        internal void NotifyProcessed(string monitorId, string outputPath) =>
            ImageProcessed?.Invoke(monitorId, outputPath);
    }
}
