using SUNWODA_SEVB.Core.Models.Project.BoxGluing;

namespace SUNWODA_SEVB.Core.Interfaces.Project.BoxGluing
{
    public interface IBoxGluingConfigService
    {
        BoxGluingAcquisitionConfig Current { get; }

        Task<BoxGluingAcquisitionConfig> LoadAsync();

        Task SaveRuntimeSettingsAsync(string csvRootPath, int sampleIntervalMs, bool useSimulation);
    }
}
