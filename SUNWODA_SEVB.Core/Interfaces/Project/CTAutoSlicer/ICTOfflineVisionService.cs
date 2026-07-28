using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT 正极 AI 视觉（NJCT.NJSwdCT）反射调用封装。
    /// </summary>
    public interface ICTOfflineVisionService
    {
        bool IsInitialized { get; }

        /// <summary>
        /// 反射加载 NJCT.dll 并调用 ModelInit。
        /// </summary>
        void Initialize(string modelPath, bool isRunCuda = true);

        /// <summary>增强图推理（ModelAction）。</summary>
        OfflineCtInferResult InferEnhance(string imagePath, int blackHeight, string? resultImagePath = null);

        /// <summary>原图推理（ModelAction_origin）。</summary>
        OfflineCtInferResult InferOrigin(string imagePath, int blackHeight, string? resultImagePath = null);

        /// <summary>释放模型句柄。</summary>
        void DisposeModel();
    }
}
