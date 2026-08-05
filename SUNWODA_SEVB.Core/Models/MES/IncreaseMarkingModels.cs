using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// Marking数据上传请求
    /// </summary>
    public class IncreaseMarkingRequest : BaseRequest
    {
        [JsonPropertyName("deviceSn")]
        public string DeviceSn { get; set; } = string.Empty;

        [JsonPropertyName("serialNumber")]
        public string SerialNumber { get; set; } = string.Empty;

        [JsonPropertyName("groupCode")]
        public string GroupCode { get; set; } = string.Empty;

        [JsonPropertyName("controlGroup")]
        public string ControlGroup { get; set; } = string.Empty;

        [JsonPropertyName("timeStamp")]
        public string TimeStamp { get; set; } = string.Empty;

        [JsonPropertyName("remark")]
        public string Remark { get; set; } = string.Empty;

        public override bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(SerialNumber))
            { errorMessage = "序列号不能为空"; return false; }
            if (string.IsNullOrWhiteSpace(DeviceSn))
            { errorMessage = "设备序列号不能为空"; return false; }
            errorMessage = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Marking数据上传响应
    /// </summary>
    public class IncreaseMarkingResponse : BaseResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;
    }
}
