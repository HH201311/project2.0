using SUNWODA_SEVB.Core.Models.Project.NJSIX;

namespace SUNWODA_SEVB.Core.Interfaces.Project.NJSIX
{
    public interface INJSIXPack6ResultStore
    {
        Task EnsureFoldersAsync(Pack6Config config);
        Task AppendProductResultAsync(Pack6Config config, Pack6ProductResult result);
        Task AppendFrameResultAsync(Pack6Config config, Pack6FrameResult frame);
        string GetResultCsvDirectory(Pack6Config config);
    }
}
