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

    public class DownloadDllResponse
    {
        /// <summary>
        /// 是否成功编码，200表示成功，其余表示失败
        /// </summary>
        public string? Code { get; set; }
        /// <summary>
        /// 文本信息
        /// </summary>
        public string? Message { get; set; }
        /// <summary>
        /// 返回python脚本执行的数据
        /// </summary>
        public string? Data { get; set; }
        /// <summary>
        /// 成功为true，失败为false
        /// </summary>
        public bool Success { get; set; }
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
}
