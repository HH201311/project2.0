using SUNWODA_SEVB.Core.Common;

namespace SUNWODA_SEVB.Core.Models.MES
{
    public enum MesContentType
    {
        JsonBody,            // application/json
        FormUrlEncoded       // application/x-www-form-urlencoded，字段名为 jsonData
    }

    /// <summary>
    /// MES API配置
    /// </summary>
    public class MesApiConfiguration : ModelBase
    {
        /// <summary>
        /// 配置文件名称
        /// </summary>
        public string ProfileName { get; set; } = string.Empty;

        /// <summary>
        /// 基础URL
        /// </summary>  
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// 超时时间（秒）
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 是否启用重试
        /// </summary>
        public bool EnableRetry { get; set; } = true;

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// SessionID:登录时返回的Result的值
        /// </summary>
        public string OperatorId { get; set; } = string.Empty;

        /// <summary>
        /// 工序代码
        /// </summary>
        public string GroupCode { get; set; } = string.Empty;

        /// <summary>
        /// 设备编号
        /// </summary>
        public string DeviceSn { get; set; } = string.Empty;

        /// <summary>
        /// 工单号
        /// </summary>
        public string MoNumber { get; set; } = string.Empty;

        /// <summary>
        /// 控制组
        /// </summary>
        public string ControlGroup { get; set; } = string.Empty;

        /// <summary>
        /// 条码类型 cell:产品条码；tray:托盘号；group:产品组
        /// </summary>
        public string SnType { get; set; } = string.Empty;

        /// <summary>
        /// 端点映射，key为端点名称，value为相对于BaseUrl的路径
        /// </summary>
        public IReadOnlyDictionary<string, string> Endpoints { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 自定义请求头，key为头名称，value为头值
        /// </summary>
        public IReadOnlyDictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 自定义设置，key为设置名称，value为设置值，可用于存储其他业务相关的配置项
        /// </summary>
        public IReadOnlyDictionary<string, string> CustomSettings { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取完整的端点URL，如果Endpoints中没有对应的key，则直接使用key作为相对路径
        /// </summary>
        /// <param name="endpointKey"></param>
        /// <returns></returns>
        public string GetEndpointUrl(string endpointKey)
        {
            if (Endpoints.TryGetValue(endpointKey, out var endpoint))
                return $"{BaseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
            return $"{BaseUrl.TrimEnd('/')}/{endpointKey.TrimStart('/')}";
        }

        /// <summary>
        /// 验证配置是否有效，主要检查BaseUrl是否为空、格式是否正确，以及TimeoutSeconds和MaxRetryCount的合理性
        /// </summary>
        /// <param name="errorMessage">错误信息，如果配置无效则返回具体的错误描述</param>
        /// <returns>配置是否有效</returns>
        public bool IsValid(out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(BaseUrl))
                errorMessage = "BaseUrl未配置";
            else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
                errorMessage = "BaseUrl格式无效";
            else if (TimeoutSeconds <= 0)
                errorMessage = "超时时间必须大于0";
            else if (MaxRetryCount < 0)
                errorMessage = "重试次数不能小于0";
            return string.IsNullOrEmpty(errorMessage);
        }
    }
}
