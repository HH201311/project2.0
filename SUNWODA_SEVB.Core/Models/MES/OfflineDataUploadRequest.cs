using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.MES
{
    public class OfflineDataUploadRequest : BaseRequest
    {
        [JsonPropertyName("operatorId")]
        public string OperatorId { get; set; } = string.Empty;

        [JsonPropertyName("productSn")]
        public string ProductSn { get; set; } = string.Empty;

        [JsonPropertyName("groupCode")]
        public string GroupCode { get; set; } = string.Empty;

        [JsonPropertyName("deviceSn")]
        public string DeviceSn { get; set; } = string.Empty;

        [JsonPropertyName("moNumber")]
        public string MoNumber { get; set; } = string.Empty;

        [JsonPropertyName("timeStamp")]
        public string TimeStamp { get; set; } = string.Empty;

        [JsonPropertyName("testResult")]
        public string TestResult { get; set; } = string.Empty;

        [JsonPropertyName("testData")]
        public List<OfflineTestData> TestData { get; set; } = new();

        [JsonPropertyName("environment")]
        public List<EnvironmentData>? Environment { get; set; }

        [JsonPropertyName("stepData")]
        public List<StepData>? StepData { get; set; }

        public override bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(ProductSn))
            { errorMessage = "ProductSn不能为空"; return false; }
            if (string.IsNullOrWhiteSpace(TestResult))
            { errorMessage = "TestResult不能为空"; return false; }
            if (TestData == null || TestData.Count == 0)
            { errorMessage = "TestData不能为空"; return false; }
            if (string.IsNullOrWhiteSpace(TimeStamp))
            { errorMessage = "TimeStamp不能为空"; return false; }
            errorMessage = string.Empty;
            return true;
        }
    }

    public class OfflineDataUploadResponse : BaseResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public string Result { get; set; } = string.Empty;

        [JsonPropertyName("traceId")]
        public string TraceId { get; set; } = string.Empty;
    }
}