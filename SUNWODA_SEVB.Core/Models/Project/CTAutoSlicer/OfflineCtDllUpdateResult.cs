namespace SUNWODA_SEVB.Core.Models.Project.CTAutoSlicer
{
    /// <summary>
    /// 惠州离线 CT — NJCT.dll 远端版本检查结果。
    /// </summary>
    public sealed class OfflineCtDllUpdateCheckResult
    {
        public bool IsSuccess { get; init; }

        /// <summary>远端版本与本地记录不一致，或本地缺少 DLL 时为 true。</summary>
        public bool UpdateAvailable { get; init; }

        public string? RemoteVersion { get; init; }

        public string? LocalVersion { get; init; }

        public string? Message { get; init; }
    }

    /// <summary>
    /// 惠州离线 CT — NJCT.dll 远端下载落盘结果。
    /// </summary>
    public sealed class OfflineCtDllDownloadResult
    {
        public bool IsSuccess { get; init; }

        public string? InstalledVersion { get; init; }

        public string? Message { get; init; }
    }
}
