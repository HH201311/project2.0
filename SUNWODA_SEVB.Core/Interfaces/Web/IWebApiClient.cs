
using SUNWODA_SEVB.Core.Models.Web;

namespace SUNWODA_SEVB.Core.Interfaces.Web
{
    /// <summary>
    /// WEB API客户端接口
    /// </summary>
    public interface IWebApiClient
    {
        /// <summary>
        /// 设置UUID
        /// </summary>
        void SetUuid(string uuid);

        /// <summary>
        /// 获取UUID
        /// </summary>
        string? GetUuid();

        // 设备管理接口
        Task<DeviceBindingResponse> BindDeviceAsync(DeviceBindingRequest request);
        Task<HeartbeatResponse> SendHeartbeatAsync(string deviceSn, string state, string info, string uuid);

        // 信息上传接口
        Task<PcInfoResponse> UploadPcInfoAsync(PcInfoRequest request, string uuid);

        // 版本管理接口
        Task<CheckVersionResponse> CheckVersionAsync(string softName, string softVersion, string deviceSn);
        Task<DownloadFileResponse> DownloadFileAsync(string softName, string softVersion, string savePath, string fileName, string uuid);
        Task<UploadFileResponse> UploadFileAsync(string filePath, string version, string name, string fileName);

        // SysCenter接口
        Task<SysCenterFileResponse> DownloadSysCenterFileAsync(string appKey, string signature, string pythonCode, string executeType);
        Task<FileVersionResponse> GetFileVersionAsync(string pythonCode);

    }
}
