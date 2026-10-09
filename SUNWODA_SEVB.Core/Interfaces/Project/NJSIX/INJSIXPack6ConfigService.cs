using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    public interface INJSIXPack6ConfigService
    {
        Pack6Config Current { get; }
        Task<Pack6Config> LoadAsync();
        Task SaveRuntimeSettingsAsync(Pack6Config config);
    }
}
