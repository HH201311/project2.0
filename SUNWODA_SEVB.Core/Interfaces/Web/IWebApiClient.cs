
using SUNWODA_SEVB.Core.Models.Project.HawkEye;
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
        Task<MachineErrorResponse> SendMachineErrorAlertAsync(string deviceId, string info, string state, string type = "WX", string projectName = "", string level = "10");//设备预警接口

        // 信息上传接口
        Task<PcInfoResponse> UploadPcInfoAsync(PcInfoRequest request, string uuid);

        // 版本管理接口
        Task<CheckVersionResponse> CheckVersionAsync(string softName, string softVersion, string deviceSn);
        Task<DownloadFileResponse> DownloadFileAsync(string softName, string softVersion, string savePath, string fileName, string uuid);
        Task<UploadFileResponse> UploadFileAsync(string filePath, string version, string name, string fileName);

        // SysCenter接口
        Task<SysCenterFileResponse> DownloadSysCenterFileAsync(string appKey, string signature, string pythonCode, string executeType);
        Task<DownloadDllResponse> DownloadDllAsync(string appKey, string signature, string pythonCode, string executeType);
        Task<FileVersionResponse> GetFileVersionAsync(string pythonCode);

        // 鹰眼项目接口
        Task<AiDetectionDataResponse> AiDetectionDataAsync(string position, string barCode, long abnormalTimeStamp, string detectionResult, byte[]? pictureFile, List<FrameDtoList>? frameResultDataList = null, string contentType = "image/png", string fileExt = "png", int dataType = 2);
        Task<AddCameraResponse> AddCameraAsync(List<CameraModel> cameras);
        Task<TrajectoryDataResponse> TrajectoryDataAsync(string position, string barCode, long abnormalTimeStamp, long abnormalTimeStampEnd, List<(byte[] FileBytes, string ContentType, string FileName, string FileExt)> videoFiles, List<FrameDtoList> frameDtoList, string abnormaVideoStatus);

        // 容量预测接口
        Task<CapacityPredictionDataResponse> CapacityPredictionAsync(string barCode, CapacityPredictionData[] capacityPredictionDataLis);

        // web文件or信息推送
        Task<WebApiSendResponse> PushMessageAsync(string messageContent);
        Task<WebApiSendResponse> PushFileAsync(string filePath);

    }
}
