using System.Text.Json.Serialization;
using SUNWODA_SEVB.Core.Interfaces.MES;

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// 获取测试数据
    /// 下发工序参数项配置界面在mes系统开发
    /// </summary>
    public class GetTestDataRequest : BaseRequest
    {
        /// <summary>
        /// 工序代码:固定值
        /// </summary>
        [JsonPropertyName("groupCode")]
        public string? GroupCode { get; set; }

        /// <summary>
        /// SessionID:登录时返回的Result的值
        /// </summary>
        [JsonPropertyName("operatorId")]
        public string? OperatorId { get; set; }

        /// <summary>
        /// 产品条码或者托盘号:设备上位机（设备扫码录入）
        /// </summary>
        [JsonPropertyName("sn")]
        public string? Sn { get; set; }

        /// <summary>
        /// 条码类型cell:产品条码；tray:托盘号；group:产品组
        /// </summary>
        [JsonPropertyName("snType")]
        public string? SnType { get; set; }

        public override bool Validate(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(Sn))
            {
                errorMessage = "PACK码不能为空";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }
    }

    public class GetTestDataResponse : IMesResponse
    {
        /// 登录结果，true为登录成功，false为登录失败
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>
        /// 登录返回信息，登录成功时，返回的是登录id，登录失败时，返回的是错误信息
        /// </summary>
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        /// <summary>
        /// 测试结果，预留字段
        /// </summary>
        [JsonPropertyName("testResult")]
        public string? TestResult { get; set; }

        /// <summary>
        /// 备注，预留字段
        /// </summary>
        [JsonPropertyName("remark")]
        public string? Remark { get; set; }

        /// <summary>
        /// 详细测试结果，预留字段
        /// </summary>
        [JsonPropertyName("testResultDetails")]
        public List<TestResultDetails>? TestResultDetails { get; set; }

        /// <summary> 判断接口是否成功 </summary>
        [JsonIgnore]
        public bool IsSuccess => string.Equals(Status, "true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 接口是否成功，Success为true表示接口调用成功，Success为false表示接口调用失败
        /// </summary>
        [JsonIgnore]
        public bool Success
        {
            get => IsSuccess;
            set { }
        }

        /// <summary>
        /// 接口返回码，接口调用成功时，Code为"200"，接口调用失败时，Code为Status的值
        /// </summary>
        [JsonIgnore]
        public string Code
        {
            get => IsSuccess ? "200" : Status ?? "null";
            set { }
        }

        /// <summary>
        /// 接口返回信息，接口调用成功时，Message为Result的值，接口调用失败时，Message为"失败: "加上Result的值
        /// </summary>
        [JsonIgnore]
        public string Message
        {
            get => IsSuccess ? Result ?? "null" : $"失败: {Result ?? "null"}";
            set { }
        }
    }

    public class TestResultDetails
    {
        /// <summary>
        /// 产品条码
        /// </summary>
        [JsonPropertyName("productSn")]
        public string? ProductSn { get; set; }

        /// <summary>
        /// 工序代码
        /// </summary>
        [JsonPropertyName("groupCode")]
        public string? GroupCode { get; set; }

        /// <summary>
        /// 参数代码
        /// </summary>
        [JsonPropertyName("paramCode")]
        public string? ParamCode { get; set; }

        /// <summary>
        /// 参数项名称
        /// </summary>
        [JsonPropertyName("paramName")]
        public string? ParamName { get; set; }

        /// <summary>
        /// 参数项值
        /// </summary>
        [JsonPropertyName("paramValue")]
        public string? ParamValue { get; set; }

        /// <summary>
        /// 设备号
        /// </summary>
        [JsonPropertyName("deviceNo")]
        public string? DeviceNo { get; set; }
    }
}
