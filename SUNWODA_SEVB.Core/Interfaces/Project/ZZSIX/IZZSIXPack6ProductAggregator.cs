using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    public interface IZZSIXPack6ProductAggregator
    {
        event Action<Pack6ProductResult>? ProductCompleted;
        Pack6ProductResult? ApplyFrame(Pack6FrameResult frame, TimeSpan packTimeout);
        void Reset();
        IReadOnlyCollection<Pack6ProductStatus> GetInFlight();
    }
}
