using SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer;

namespace SUNWODA_SEVB.Core.Interfaces.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT — NJCT.dll 远端版本检查与下载。
    /// </summary>
    public interface IOfflineCtDllUpdateService
    {
        /// <summary>读取本机已记录的 NJCT.dll 远端版本号（local.json）。</summary>
        string? GetLocalCachedVersion();

        /// <summary>查询远端版本并与本地记录比对。</summary>
        Task<OfflineCtDllUpdateCheckResult> CheckForUpdateAsync(
            string dllDownloadBaseUrl,
            CancellationToken cancellationToken = default);

        /// <summary>下载 NJCT.dll 并写入 Dlls\ 与 exe 同级目录。</summary>
        Task<OfflineCtDllDownloadResult> DownloadAndInstallAsync(
            string dllDownloadBaseUrl,
            string remoteVersion,
            CancellationToken cancellationToken = default);
    }
}
