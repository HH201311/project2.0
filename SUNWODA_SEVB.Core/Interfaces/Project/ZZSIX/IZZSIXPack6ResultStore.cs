using SUNWODA_SEVB.Core.Models.Project.ZZSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.ZZSIX
{
    public interface IZZSIXPack6ResultStore
    {
        Task EnsureFoldersAsync(Pack6Config config);
        Task AppendProductResultAsync(Pack6Config config, Pack6ProductResult result);
        string GetResultCsvDirectory(Pack6Config config);
    }
}
