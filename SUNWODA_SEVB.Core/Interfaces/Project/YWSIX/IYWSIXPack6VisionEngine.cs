using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.YWSIX
{
    /// <summary>视觉引擎抽象；P0 为模拟实现，后续接 ZZPack.AI。</summary>
    public interface IYWSIXPack6VisionEngine
    {
        Task InitializeAsync(Pack6Config config, CancellationToken ct = default);
        Task<Pack6FrameResult> DetectAsync(Pack6IngressItem item, CancellationToken ct = default);
        /// <summary>运行中热更新配置（不重建 AI；PIN 阈值等立即生效）。</summary>
        void RefreshRuntimeConfig(Pack6Config config);
        void Shutdown();
    }
}
