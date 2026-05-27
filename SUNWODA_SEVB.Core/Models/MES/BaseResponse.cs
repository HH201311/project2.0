using SUNWODA_SEVB.Core.Common;
using SUNWODA_SEVB.Core.Interfaces.MES;

namespace SUNWODA_SEVB.Core.Models.MES
{
    /// <summary>
    /// MES响应基类
    /// </summary>
    public class BaseResponse : ModelBase, IMesResponse
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class BaseResponse<T> : BaseResponse, IMesResponse<T>
    {
        public T? Data { get; set; }
    }
}
