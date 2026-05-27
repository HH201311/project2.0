
namespace SUNWODA_SEVB.Core.Interfaces.MES
{
    /// <summary>
    /// MES响应接口
    /// </summary>
    public interface IMesResponse
    {
        bool Success { get; set; }
        string Code { get; set; }
        string Message { get; set; }
    }

    /// <summary>
    /// MES泛型响应接口
    /// </summary>
    public interface IMesResponse<T> : IMesResponse
    {
        T? Data { get; set; }
    }
}
