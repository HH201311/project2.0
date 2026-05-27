using System.Text.Json.Serialization;

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// 离线测试数据模型
    /// </summary>
    public class OfflineTestData
    {
        /// <summary>
        /// 参数代码
        /// </summary>
        [JsonPropertyName("paramCode")]
        public string ParamCode { get; set; } = string.Empty;

        /// <summary>
        /// 参数名称
        /// </summary>
        [JsonPropertyName("paramName")]
        public string ParamName { get; set; } = string.Empty;

        /// <summary>
        /// 参数值
        /// </summary>
        [JsonPropertyName("paramValue")]
        public string ParamValue { get; set; } = string.Empty;

        /// <summary>
        /// 参数结果
        /// </summary>
        [JsonPropertyName("paramResult")]
        public string ParamResult { get; set; } = string.Empty;

        /// <summary>
        /// 参数单位
        /// </summary>
        [JsonPropertyName("paramUnit")]
        public string ParamUnit { get; set; } = string.Empty;
    }

    public class EnvironmentData
    {
        /// <summary>
        /// 参数代码
        /// </summary>
        [JsonPropertyName("paramCode")]
        public string ParamCode { get; set; } = string.Empty;

        /// <summary>
        /// 参数名称
        /// </summary>
        [JsonPropertyName("paramName")]
        public string ParamName { get; set; } = string.Empty;

        /// <summary>
        /// 参数值
        /// </summary>
        [JsonPropertyName("paramValue")]
        public string ParamValue { get; set; } = string.Empty;

        /// <summary>
        /// 参数结果
        /// </summary>
        [JsonPropertyName("paramResult")]
        public string ParamResult { get; set; } = string.Empty;
    }

    /// <summary>
    /// 步骤数据模型
    /// </summary>
    public class StepData
    {
        /// <summary>
        /// 步骤名称
        /// </summary>
        [JsonPropertyName("stepName")]
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// 参数值
        /// </summary>
        [JsonPropertyName("value")]
        public double Value { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}