using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    /// <summary>视觉引擎抽象；P0 为模拟实现，后续接 ZZPack.AI。</summary>
    public interface INJSIXPack6VisionEngine
    {
        Task InitializeAsync(Pack6Config config, CancellationToken ct = default);
        Task<Pack6FrameResult> DetectAsync(Pack6IngressItem item, CancellationToken ct = default);
        void Shutdown();
    }
}
