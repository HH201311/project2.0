using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.Web
{
    /// <summary>
    /// 基础响应模型
    /// </summary>
    public class BaseResponse
    {
        [JsonPropertyName("code")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Code { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("success")]
        public bool IsSuccess { get; set; }
    }

    /// <summary>
    /// 设备绑定响应
    /// </summary>
    public class DeviceBindingResponse : BaseResponse
    {
        [JsonPropertyName("data")]
        public string? Data { get; set; } // UUID
    }

    /// <summary>
    /// 心跳响应
    /// </summary>
    public class HeartbeatResponse : BaseResponse
    {
    }

    /// <summary>
    /// PC信息响应
    /// </summary>
    public class PcInfoResponse : BaseResponse
    {
    }

    /// <summary>
    /// 版本检查响应
    /// </summary>
    public class CheckVersionResponse : BaseResponse
    {
        public VersionData? Data { get; set; }

        public class VersionData
        {
            public string? Code { get; set; }
            public string? Info { get; set; } // 新版本号
            public bool NeedUpdate => Code == "C002";
        }
    }

    /// <summary>
    /// 文件下载响应
    /// </summary>
    public class DownloadFileResponse : BaseResponse
    {
        public byte[]? FileData { get; set; }
    }

    /// <summary>
    /// 文件上传响应
    /// </summary>
    public class UploadFileResponse : BaseResponse
    {
        public string? FileId { get; set; }
    }

    /// <summary>
    /// SysCenter文件响应
    /// </summary>
    public class SysCenterFileResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? DataStr { get; set; }
    }

    /// <summary>
    /// 库文件.dll下载接口响应
    /// </summary>
    public class DownloadDllResponse : BaseResponse
    {
        /// <summary>
        /// 接收到的字节流文件数据
        /// </summary>
        [JsonIgnore]
        public byte[]? DataStr { get; set; }
    }

    /// <summary>
    /// 文件版本响应
    /// </summary>
    public class FileVersionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Version { get; set; }
    }

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：视觉机损检测
    /// </summary>
    public class AiDetectionDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } 
    }

    /// <summary>
    /// 摄像头新增接口响应
    /// </summary>
    public class AddCameraResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } 
    }    

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：轨迹检测
    /// </summary>
    public class TrajectoryDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } 
    }

    /// <summary>
    /// UE端数据接收接口响应
    /// 备注：轨迹检测
    /// </summary>
    public class CapacityPredictionDataResponse : BaseResponse
    {
        /// <summary>
        /// 返回信息
        /// </summary>
        [JsonPropertyName("data")]
        public string? Data { get; set; } 
    }
}
