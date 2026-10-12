using SUNWODA_SEVB.Core.Models.Project.YWSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.YWSIX
{
    public interface IYWSIXPack6ConfigService
    {
        Pack6Config Current { get; }
        Task<Pack6Config> LoadAsync();
        Task SaveRuntimeSettingsAsync(Pack6Config config);
    }
}
