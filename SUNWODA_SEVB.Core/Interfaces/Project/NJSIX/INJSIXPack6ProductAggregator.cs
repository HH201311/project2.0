using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    public interface INJSIXPack6ProductAggregator
    {
        event Action<Pack6ProductResult>? ProductCompleted;
        Pack6ProductResult? ApplyFrame(Pack6FrameResult frame, TimeSpan packTimeout);
        /// <summary>缺张/超时强制结案；已结案返回 null。</summary>
        Pack6ProductResult? ForceComplete(string productCode, bool timedOut, string? defectReason);
        /// <summary>产品已结案（含超时）后忽略迟到帧。</summary>
        bool IsClosed(string productCode);
        void Reset();
        IReadOnlyCollection<Pack6ProductStatus> GetInFlight();
    }
}
