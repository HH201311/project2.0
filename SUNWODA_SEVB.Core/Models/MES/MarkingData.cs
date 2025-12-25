

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// Marking数据模型
    /// </summary>
    public class MarkingData
    {
        public string? SerialNumber { get; set; }
        public string? Data { get; set; }
        public DateTime Timestamp { get; set; }
        public string? StationCode { get; set; }
        public string? OperatorId { get; set; }
    }

    /// <summary>
    /// 上传状态枚举
    /// </summary>
    public enum UploadStatus
    {
        Unknown = 0,
        Pending = 1,
        Uploading = 2,
        Success = 3,
        Failed = 4,
        Error = 5,
        ServiceDisabled = 6
    }

    /// <summary>
    /// 上传状态响应模型
    /// </summary>
    public class UploadStatusResponse
    {
        public UploadStatus Status { get; set; }
        public string? Message { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
