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

        /// <summary>
        /// 增强图推理（ModelAction）；成功时默认叠抓点/尺寸后落盘。
        /// </summary>
        /// <param name="blackHeight">传给 NJCT 的打黑高度（主流程固定 0）。</param>
        /// <param name="bottomInfoBlackHeight">上位机底部信息遮罩高度（OfflineCtBlackHeight）。</param>
        /// <param name="rangeCheck">L1~L5 mm 范围判断（可选）。</param>
        /// <param name="applyHostOverlay">false=仅落盘 DLL 返回的 ResultImg（DLL 直调测试）。</param>
        OfflineCtInferResult InferEnhance(
            string imagePath,
            int blackHeight,
            string? resultImagePath = null,
            double pixelSizeUm = 1.0,
            int bottomInfoBlackHeight = 0,
            OfflineCtLRangeCheckOptions? rangeCheck = null,
            bool applyHostOverlay = true);

        /// <summary>
        /// 原图推理（ModelAction_origin）；成功时默认叠抓点/尺寸后落盘。
        /// </summary>
        OfflineCtInferResult InferOrigin(
            string imagePath,
            int blackHeight,
            string? resultImagePath = null,
            double pixelSizeUm = 1.0,
            int bottomInfoBlackHeight = 0,
            OfflineCtLRangeCheckOptions? rangeCheck = null,
            bool applyHostOverlay = true);

        /// <summary>释放模型句柄。</summary>
        void DisposeModel();
    }
}
