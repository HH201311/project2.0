using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    public interface IZZSIXPack6ConfigService
    {
        Pack6Config Current { get; }
        Task<Pack6Config> LoadAsync();
        Task SaveRuntimeSettingsAsync(Pack6Config config);
    }
}
