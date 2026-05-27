using SUNWODA_SEVB.Core.Models.MES;

namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    public interface IOfflineDataUploadService : IMesService
    {
        Task<OfflineDataUploadResponse> UploadAsync(
            string productSn,
            string testResult,
            List<OfflineTestData> testDatas,
            List<EnvironmentData>? environments = null,
            List<StepData>? stepDatas = null,
            CancellationToken cancellationToken = default);
    }
}